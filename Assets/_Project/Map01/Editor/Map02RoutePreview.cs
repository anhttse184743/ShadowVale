using System;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>One-session entry points for manually playing the Day 2 / Night 2 routes.</summary>
    [InitializeOnLoad] public static class Map02RoutePreview
    {
        const string Key="ShadowVale.Map02RoutePreview";
        const string MapPath="Assets/_Project/Scenes/Maps/Map 2.unity";
        static Map02RoutePreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayModeChanged;}

        [MenuItem("ShadowVale/Map 2/Chạy thử Ngày 2",priority=20)]
        public static void PlayDayTwo()=>Begin(2);
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 2",priority=21)]
        public static void PlayNightTwo()=>Begin(3);
        [MenuItem("ShadowVale/Map 2/Chạy thử Ngày 2",true)]
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 2",true)]
        static bool CanBegin()=>!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling;

        static void Begin(int phase)
        {
            if(!CanBegin()||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var map=AssetDatabase.LoadAssetAtPath<SceneAsset>(MapPath);
            if(map==null){Debug.LogError("Không tìm thấy scene Map 2.");return;}
            SessionState.SetString(Key+".startScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Key+".saveRoot",SessionState.GetString(ForestSaveSlots.EditorTestRootKey,""));
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath("Temp/Map02RoutePreviewSaves/"+(phase==2?"Day2":"Night2")));
            SessionState.SetBool(Key+".active",true);SessionState.SetInt(Key+".phase",phase);
            EditorSceneManager.playModeStartScene=map;
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            int phase=SessionState.GetInt(Key+".phase",0);
            if(phase==0||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            var arrival=Object.FindFirstObjectByType<Map02Arrival>();
            if(arrival!=null&&arrival.IsPlaying){arrival.Skip();return;}
            var mission=Object.FindFirstObjectByType<Map02Mission>();
            if(mission?.Ready!=true||mission.Chapter==null)return;
            SessionState.SetInt(Key+".phase",0);
            ApplyToMission(mission,phase);
            Debug.Log("[Map 2 chạy thử] "+(phase==2?"Ngày 2: nhận lệnh tìm Tâm.":"Đêm 2: quan sát đèn tuần tra ở cầu.")+" Save thử nằm trong Temp/Map02RoutePreviewSaves.");
        }
        public static Map02ChapterState BuildPreviewState(int phase)
        {
            if(phase!=2&&phase!=3)throw new ArgumentOutOfRangeException(nameof(phase));
            var state=new Map02ChapterState();
            foreach(var task in Map02ChapterDefinition.Load().tasks.Where(t=>t.phase<phase))
                if(!state.Complete(task.id))throw new InvalidOperationException("Không thể dựng mốc thử: "+task.id);
            state.storedRounds=90;state.gearStored=true;state.Validate();return state;
        }
        public static void ApplyToMission(Map02Mission mission,int phase)
        {
            var state=BuildPreviewState(phase);
            var snapshot=mission.Capture();snapshot.chapter=state;snapshot.world=new Map02WorldState();
            snapshot.checkpoint=snapshot.patrolCheckpoint=null;snapshot.playSeconds=0;snapshot.attackCooldown=0;
            snapshot.health=100;snapshot.equipped=(int)WeaponKind.Rifle;
            snapshot.magazine=mission.Player.GetComponent<PlayerCombat>().MagazineCapacity;
            var previewItems=new System.Collections.Generic.List<Map02Mission.Item>{new Map02Mission.Item{id=mission.RifleAmmo,count=90},new Map02Mission.Item{id="medkit_small",count=3},new Map02Mission.Item{id=snapshot.rifleItemId,count=1},new Map02Mission.Item{id="knife",count=1}};
            string pistol=snapshot.bagLayout?.pistol;if(!string.IsNullOrEmpty(pistol))previewItems.Add(new Map02Mission.Item{id=pistol,count=1});
            snapshot.inventory=previewItems.ToArray();
            snapshot.bagLayout=new Map01Inventory.Layout{primary=snapshot.rifleItemId,pistol=pistol,throwable="stone"};
            snapshot.stamina=mission.CharacterSettings.stamina;snapshot.exploration=null;snapshot.crafting=null;snapshot.craftRemaining=0;snapshot.ground=null;
            var spawn=mission.Chapter.Sites[phase==2?"command":"bridge"].position+Vector3.back*3;
            if(NavMesh.SamplePosition(spawn,out var hit,4,NavMesh.AllAreas))spawn=hit.position;
            snapshot.position=spawn;
            var look=mission.Chapter.Sites[phase==2?"command":"scout"].position-spawn;
            snapshot.yaw=Quaternion.LookRotation(Vector3.ProjectOnPlane(look,Vector3.up)).eulerAngles.y;
            mission.Restore(snapshot);mission.SetPaused(false);mission.StoreCheckpoint();ForestMenu.RefreshGameplayBinding();
            mission.Say(phase==2?"CHẠY THỬ NGÀY 2 · Tới gặp anh Thành, nhấn E nhận lệnh tìm Tâm.":"CHẠY THỬ ĐÊM 2 · Tới điểm quan sát cầu, F mở ống nhòm nhìn ánh đèn ngoài đồng.");
        }
        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+".active",false))return;
            var previousRoot=SessionState.GetString(Key+".saveRoot","");
            if(string.IsNullOrEmpty(previousRoot))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previousRoot);
            var previousScene=SessionState.GetString(Key+".startScene","");
            SessionState.SetBool(Key+".active",false);SessionState.SetInt(Key+".phase",0);
            EditorApplication.delayCall+=()=>EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previousScene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene);
        }
    }
}
