using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Runs the whole Map Test pipeline in dependency order: character import → animator →
    /// weapon prefabs → map. Each step is idempotent, so this is safe to re-run after dropping
    /// in new art. Menu <b>ShadowVale ▸ Rebuild Test Map (All)</b>, or headless:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod ShadowVale.Editor.TestbedPipeline.RunAll</c>
    /// </summary>
    public static class TestbedPipeline
    {
        [MenuItem("ShadowVale/Rebuild Test Map (All)")]
        public static void RunAll()
        {
            // Order matters: the animator needs the retargeted clips, and the map needs both the
            // animator and the weapon prefabs before it can wire the player.
            CharacterImportSetup.Run();
            PlayerAnimatorBuilder.Build();
            EnemyAnimatorBuilder.Build();
            WeaponPrefabBuilder.Build();
            GreyboxSandboxBuilder.Build();

            AssetDatabase.SaveAssets();
            Debug.Log("[Testbed] Full rebuild complete.");
        }
    }
}
