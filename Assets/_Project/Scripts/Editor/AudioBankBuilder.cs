using System.IO;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Builds <c>FootstepBank.asset</c> and wires the audio components onto the player prefab.
    /// <para>
    /// Every number below was measured off the waveform rather than guessed. The recordings are
    /// untrimmed and were made at wildly different levels, so each one needs to be told where its
    /// sound actually starts and how much to scale it to sit level with the others. Keeping those
    /// numbers in code means re-running this menu item restores them after anyone edits the asset
    /// by hand — the same contract the animator and weapon builders already follow.
    /// </para>
    /// Menu <b>ShadowVale ▸ Build Audio Banks</b>.
    /// </summary>
    public static class AudioBankBuilder
    {
        private const string BankPath = "Assets/_Project/Audio/FootstepBank.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        private const string GrassClip = "Assets/_Project/Audio/SFX/Footsteps/running_in_grass.mp3";
        private const string WaterLeftClip = "Assets/_Project/Audio/SFX/Footsteps/step_water_1.mp3";
        private const string WaterRightClip = "Assets/_Project/Audio/SFX/Footsteps/step_water_2.mp3";
        private const string GunClip = "Assets/_Project/Audio/SFX/Weapons/gun.mp3";
        private const string KnifeClip = "Assets/_Project/Audio/SFX/Weapons/knife.mp3";

        [MenuItem("ShadowVale/Build Audio Banks")]
        public static void Build()
        {
            FootstepBank bank = BuildFootstepBank();
            if (bank == null) return;
            WirePlayerPrefab(bank);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioBanks] Built {BankPath} and wired {PlayerPrefabPath}");
        }

        private static FootstepBank BuildFootstepBank()
        {
            AudioClip grass = Load(GrassClip);
            AudioClip waterLeft = Load(WaterLeftClip);
            AudioClip waterRight = Load(WaterRightClip);
            if (grass == null || waterLeft == null || waterRight == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(BankPath)!);
            var bank = AssetDatabase.LoadAssetAtPath<FootstepBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<FootstepBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            var serialized = new SerializedObject(bank);
            SerializedProperty surfaces = serialized.FindProperty("surfaces");
            surfaces.arraySize = 2;

            // Grass: one recording, peaking at -33 dBFS with its strike 0.06 s in. It is the
            // quietest asset in the project, so it sets full gain and the water is brought down
            // to meet it rather than the other way round — volume cannot exceed 1.
            WriteSurface(surfaces.GetArrayElementAtIndex(0), FootSurface.Grass,
                left: new AudioSlice(grass, 0.05f, 0.26f, 1f),
                right: new AudioSlice(grass, 0.05f, 0.26f, 1f));

            // Water: both takes open with a quarter second of nothing, and the splash in take 1
            // lands much later than in take 2. Starting either from zero would delay the step
            // by longer than a sprint stride lasts.
            WriteSurface(surfaces.GetArrayElementAtIndex(1), FootSurface.Water,
                left: new AudioSlice(waterLeft, 0.44f, 0.25f, 0.06f),
                right: new AudioSlice(waterRight, 0.24f, 0.25f, 0.05f));

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bank);
            return bank;
        }

        private static void WriteSurface(SerializedProperty element, FootSurface surface,
            AudioSlice left, AudioSlice right)
        {
            element.FindPropertyRelative("surface").enumValueIndex = (int)surface;
            WriteSlice(element.FindPropertyRelative("left"), left);
            WriteSlice(element.FindPropertyRelative("right"), right);
        }

        private static void WriteSlice(SerializedProperty property, AudioSlice slice)
        {
            property.FindPropertyRelative("clip").objectReferenceValue = slice.clip;
            property.FindPropertyRelative("startTime").floatValue = slice.startTime;
            property.FindPropertyRelative("duration").floatValue = slice.duration;
            property.FindPropertyRelative("gain").floatValue = slice.gain;
        }

        private static void WirePlayerPrefab(FootstepBank bank)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[AudioBanks] No player prefab at {PlayerPrefabPath}.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var steps = root.GetComponent<PlayerFootsteps>() ?? root.AddComponent<PlayerFootsteps>();
                var serialized = new SerializedObject(steps);
                serialized.FindProperty("bank").objectReferenceValue = bank;
                serialized.FindProperty("controller").objectReferenceValue = root.GetComponent<PlayerController>();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (root.GetComponent<PlayerCombat>() != null)
                {
                    var combatAudio = root.GetComponent<CombatAudio>() ?? root.AddComponent<CombatAudio>();
                    var combatSerialized = new SerializedObject(combatAudio);
                    // The gunshot's report begins 0.04 s in; the knife's swing peaks around 0.3 s
                    // after a long, quiet wind-up that would otherwise be heard as lag.
                    WriteSlice(combatSerialized.FindProperty("gunshot"),
                        new AudioSlice(Load(GunClip), 0.03f, 0.13f, 1f));
                    WriteSlice(combatSerialized.FindProperty("melee"),
                        new AudioSlice(Load(KnifeClip), 0.15f, 0.55f, 0.9f));
                    combatSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AudioClip Load(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogError($"[AudioBanks] Missing audio clip: {path}");
            return clip;
        }
    }
}
