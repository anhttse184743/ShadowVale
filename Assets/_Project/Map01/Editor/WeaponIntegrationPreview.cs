using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01.Editor
{
    /// <summary>An isolated playable copy for reviewing both guns without changing the real starting loadout or saves.</summary>
    [InitializeOnLoad]
    public static class WeaponIntegrationPreview
    {
        public const string ScenePath="Assets/_Project/Art/Weapon_Integration_20261018/Map1_WeaponDemo.unity";
        const string Pending="ShadowVale.WeaponIntegration.DemoPending";
        const string Playing="ShadowVale.WeaponIntegration.DemoPlaying";
        const string PreviousSaveRoot="ShadowVale.WeaponIntegration.PreviousSaveRoot";
        static WeaponIntegrationPreview()
        {
            EditorApplication.update+=SetupWhenReady;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Playing,false)){
                string previous=SessionState.GetString(PreviousSaveRoot,"");
                if(string.IsNullOrEmpty(previous))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previous);
                SessionState.EraseString(PreviousSaveRoot);SessionState.EraseBool(Playing);ForestMenuTools.ConfigureStartup();
            }};
        }
        [MenuItem("ShadowVale/Weapons/Open K54 + Mosin playable demo")]
        public static void OpenDemo()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Stop Play before opening the weapon demo.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new System.InvalidOperationException("Save your current scene before opening the demo.");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if(!File.Exists(ScenePath))AssetDatabase.CopyAsset("Assets/_Project/Scenes/Maps/Map 1.unity",ScenePath);
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetString(PreviousSaveRoot,SessionState.GetString(ForestSaveSlots.EditorTestRootKey,""));
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath("Docs/Reviews/K54_Integration_20261018/DemoSaves"));
            SessionState.SetBool(Pending,true);SessionState.SetBool(Playing,true);
            EditorSceneManager.playModeStartScene=null;
            EditorApplication.delayCall+=()=>{EditorSceneManager.playModeStartScene=null;EditorApplication.isPlaying=true;};
        }
        static void SetupWhenReady()
        {
            if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            var mission=Object.FindFirstObjectByType<Map01Mission>();if(mission==null||mission.ModernCombat==null||!mission.IsInitialized||mission.gameObject.scene.name!="Map1_WeaponDemo")return;
            if(mission.player.GetComponent<Map01Mosin>()==null||mission.player.GetComponent<Map01Pistol>()==null)return;
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            var bag=mission.GetComponent<Map01Inventory>();bag.Add("rifle_mosin",1);bag.Add("ammo_sniper",10);
            int cell=Enumerable.Range(0,Map01Inventory.BagSize).First(i=>bag.Cell(i)?.id=="rifle_mosin");bag.EquipFromCell(cell);bag.Draw(Map01Inventory.Slot.Primary);
            mission.Say("DEMO VŨ KHÍ · 1 Mosin · 2 K54 · R nạp đạn · Chuột phải bật/tắt ngắm · Chuột trái bắn",12);
            SessionState.EraseBool(Pending);
            var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));game.Focus();
            Directory.CreateDirectory("Docs/Reviews/K54_Integration_20261018");File.WriteAllText("Docs/Reviews/K54_Integration_20261018/preview_ready.txt","READY Map1_WeaponDemo; K54 and Mosin installed; separate sandbox; original starting loadout and scene unchanged.");
        }
    }
}
