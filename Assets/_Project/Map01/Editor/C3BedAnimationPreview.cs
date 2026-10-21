using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class C3BedAnimationPreview
    {
        const string Flag="ShadowVale.C3Bed.Preview";
        const string Scene="Assets/_Project/Map01/Tests/Generated/C3 Bed Knife Sandbox.unity";
        static C3BedAnimationPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("ShadowVale/C3/Play bedside execution preview")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save the current editor scene before opening this preview.");
            Directory.CreateDirectory(Path.GetDirectoryName(Scene));if(!File.Exists(Scene))AssetDatabase.CopyAsset("Assets/_Project/Scenes/Maps/Map 1.unity",Scene);
            SessionState.SetBool(Flag,true);SessionState.SetBool(Flag+".active",true);
            SessionState.SetString(Flag+".previousRoot",SessionState.GetString(ForestSaveSlots.EditorTestRootKey,""));
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(C3BedKnifeBuilder.Work+"/PreviewSaves"));
            EditorSceneManager.activeSceneChangedInEditMode-=KeepStart;EditorSceneManager.activeSceneChangedInEditMode+=KeepStart;
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene(Scene);EditorSceneManager.playModeStartScene=null;
            EditorApplication.isPlaying=true;
        }
        static void KeepStart(UnityEngine.SceneManagement.Scene a,UnityEngine.SceneManagement.Scene b)=>EditorSceneManager.playModeStartScene=null;
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag+".active",false))return;
            SessionState.EraseBool(Flag+".active");SessionState.EraseBool(Flag);
            string previous=SessionState.GetString(Flag+".previousRoot","");
            if(string.IsNullOrEmpty(previous))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previous);
            EditorSceneManager.activeSceneChangedInEditMode-=KeepStart;
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/00_Boot.unity");
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
            var m=Object.FindFirstObjectByType<Map01Mission>();if(m==null||!m.IsInitialized)return;
            var c3=m.GetComponent<Map01C3Mission>();if(c3==null||c3.Data==null||c3.Sleepers.Count<3)return;
            SessionState.SetBool(Flag,false);m.GetComponent<Map01Rescue>().MarkIntroSeen();m.ModernHealth.CinematicInvulnerable=true;
            foreach(var enemy in m.Enemies){enemy.enabled=false;var agent=enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent!=null)agent.enabled=false;}
            c3.enabled=false;
            var bag=m.GetComponent<Map01Inventory>();if(bag.Count("knife")==0)bag.Add("knife",1);
            m.ModernCombat.RestoreMagazine(ShadowVale.Gameplay.Combat.WeaponKind.Pistol,2);
            var bed=c3.Sleepers[0].GetComponent<Map01C3Sleeper>();var body=m.player.GetComponent<CharacterController>();body.enabled=false;
            m.player.SetPositionAndRotation(bed.Approach+bed.BedFrame.TransformDirection(Vector3.left*.8f),bed.BedFrame.rotation*Map01BedAnimationData.Load().approachRotation);
            // This editor-only art review is fixed in place; background keyboard
            // and mouse input must not move it while the user reads another app.
            m.ModernPlayer.RestoreMotion(false);m.ModernPlayer.enabled=false;m.ModernCombat.enabled=false;
            m.Say("Bản xem thử cố định: nhấn E cạnh giường. Dừng Play khi xem xong. Save riêng.",15);
            var camera=m.gameCamera;camera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().enabled=false;
            camera.transform.position=bed.BedFrame.TransformPoint(new Vector3(-1.9f,1.45f,1.4f));camera.transform.LookAt(bed.BodyPoint);camera.fieldOfView=53;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
    }
}
