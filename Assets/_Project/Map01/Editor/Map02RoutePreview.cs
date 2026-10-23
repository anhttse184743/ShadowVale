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
        [MenuItem("ShadowVale/Map 2/Xem diễn xuất/Nhận lệnh Ngày 2",priority=30)]
        public static void PreviewDayTwoScene()=>BeginPerformancePreview("D2_01",2);
        [MenuItem("ShadowVale/Map 2/Xem diễn xuất/Kết Đêm 2",priority=31)]
        public static void PreviewNightTwoScene()=>BeginPerformancePreview("N2_05",3);
        [MenuItem("ShadowVale/Map 2/Xem diễn xuất/Nhận lệnh Ngày 2",true)]
        [MenuItem("ShadowVale/Map 2/Xem diễn xuất/Kết Đêm 2",true)]
        static bool CanPreviewPerformance()=>CanBegin();
        static void BeginPerformancePreview(string id,int phase)
        {Begin(phase);if(SessionState.GetInt(Key+".phase",0)==phase)SessionState.SetString(Key+".performance",id);}
        [MenuItem("ShadowVale/Map 2/Chạy thử Ngày 2",true)]
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 2",true)]
        static bool CanBegin()=>!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling;

        static void Begin(int phase)
        {
            if(!CanBegin()||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            SessionState.EraseString(Key+".performance");
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
            var performance=SessionState.GetString(Key+".performance","");SessionState.EraseString(Key+".performance");
            if(string.IsNullOrEmpty(performance))ApplyToMission(mission,phase);else ApplyPerformancePreview(mission,performance);
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
        public static bool ApplyPerformancePreview(Map02Mission mission,string id)
        {
            if(id!="D2_01"&&id!="N2_05")throw new ArgumentOutOfRangeException(nameof(id));
            ApplyToMission(mission,id=="D2_01"?2:3);
            if(id=="N2_05")
            {
                var saved=mission.Capture();foreach(var task in Map02ChapterDefinition.Load().tasks.Where(t=>t.phase==3&&t.id!=id))saved.chapter.Complete(task.id);
                saved.chapter.night2GuidanceStage=7;saved.chapter.night2ChainCuePlayed=true;saved.patrolCheckpoint=null;mission.Restore(saved);
            }
            var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            var body=mission.Player.GetComponent<CharacterController>();bool enabled=body.enabled;body.enabled=false;mission.Player.position=marker.transform.position+Vector3.back*1.6f;body.enabled=enabled;Physics.SyncTransforms();
            return mission.Interact(marker);
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
            var spawn=mission.Chapter.Sites["command"].position+Vector3.back*3;
            if(NavMesh.SamplePosition(spawn,out var hit,4,NavMesh.AllAreas))spawn=hit.position;
            snapshot.position=spawn;
            var look=mission.Chapter.Sites["command"].position-spawn;
            snapshot.yaw=Quaternion.LookRotation(Vector3.ProjectOnPlane(look,Vector3.up)).eulerAngles.y;
            mission.Restore(snapshot);mission.SetPaused(false);mission.StoreCheckpoint();ForestMenu.RefreshGameplayBinding();
            mission.Say(phase==2?"CHẠY THỬ NGÀY 2 · Tới gặp anh Thành, nhấn E nhận lệnh tìm Tâm.":"CHẠY THỬ ĐÊM 2 · Bắt đầu tại nơi báo cáo cuối Ngày 2. Tới cầu, giữ F quan sát tổ tuần tra.");
        }
        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+".active",false))return;
            var previousRoot=SessionState.GetString(Key+".saveRoot","");
            if(string.IsNullOrEmpty(previousRoot))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previousRoot);
            var previousScene=SessionState.GetString(Key+".startScene","");
            SessionState.SetBool(Key+".active",false);SessionState.SetInt(Key+".phase",0);
            SessionState.EraseString(Key+".performance");
            EditorApplication.delayCall+=()=>EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previousScene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene);
        }
    }
}
