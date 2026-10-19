using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class BossCutsceneDemo
    {
        static double next;
        static BossCutsceneDemo() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += OnPlayState; }
        static void OnPlayState(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool("BossCutsceneDemo",false))return;
            SessionState.SetBool("BossCutsceneDemo",false);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("BossDemoPreviousStart",""));
        }
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            const string request="Tools/BossCutsceneDemo.request";
            if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(request);
                SessionState.SetString("BossDemoPreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool("BossCutsceneDemo",true);
                SessionState.SetBool("BossDemoFirst",true);
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");
                EditorApplication.isPlaying=true;return;
            }
            if(!SessionState.GetBool("BossCutsceneDemo",false)||!EditorApplication.isPlaying)return;
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="01_MainMenu")
            {
                EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Maps/Map 1.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));return;
            }
            var mission=Object.FindFirstObjectByType<Map01Mission>();if(mission==null||!mission.IsInitialized)return;
            var quest=mission.GetComponent<Map01Quest>();var boss=quest.CommanderEnemy;if(boss==null)return;
            var ending=mission.GetComponent<Map01EndingCutscene>();
            if(ending!=null&&ending.IsPlaying){next=EditorApplication.timeSinceStartup+3;return;}
            if(EditorApplication.timeSinceStartup<next)return;
            if(SessionState.GetBool("BossDemoFirst",false))
            {
                SessionState.SetBool("BossDemoFirst",false);
                foreach(var guard in mission.Enemies)guard.enabled=false;
                if(mission.ModernPlayer!=null)mission.ModernPlayer.enabled=false;
                if(mission.ModernCombat!=null)mission.ModernCombat.enabled=false;
                var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();view.maximized=true;
                next=EditorApplication.timeSinceStartup+4;return;
            }
            quest.RestoreStoryStage(Map01Quest.BossStage,new Map01Story.Snapshot{
                rescueAccepted=true,supplies=true,keyCollected=true,doorUnlocked=true,chestUnlocked=true,
                supplyReported=true,suppliesDelivered=true,supplyRewardGranted=true,radioDisabled=true,
                commanderAreaReached=true,commanderConfirmed=true,commanderReported=true,commanderDiscovery=true});
            var health=boss.GetComponent<Health>();if(health.IsDead)health.Revive();
            health.TakeDamage(999999,boss.transform.position,null);
            next=EditorApplication.timeSinceStartup+20;
            Directory.CreateDirectory("Tools/BossDeathReports");
            File.WriteAllText("Tools/BossDeathReports/demo.txt","PLAYING in Game View; repeats 3 seconds after each ending. Stop Play Mode to exit. Save files are not changed.");
        }
    }
}
