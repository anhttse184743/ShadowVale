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
    /// <summary>One-session entry points for manually playing the Day 2 / Night 2 routes, Day 3 and points of Night 3.</summary>
    [InitializeOnLoad] public static class Map02RoutePreview
    {
        const string Key="ShadowVale.Map02RoutePreview";
        const string MapPath="Assets/_Project/Scenes/Maps/Map 2.unity";
        static Map02RoutePreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayModeChanged;}

        [MenuItem("ShadowVale/Map 2/Chạy thử Ngày 2",priority=20)]
        public static void PlayDayTwo()=>Begin(2);
        [MenuItem("ShadowVale/Map 2/Checkpoint cứu Tâm · M2_D2_04",priority=20)]
        public static void PlayScoutRescue()=>BeginAfter("D2_03");
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 2",priority=21)]
        public static void PlayNightTwo()=>Begin(3);
        // Day 3 / Night 3: every task up to the named one is done; Nam starts by the next one.
        [MenuItem("ShadowVale/Map 2/Chạy thử Ngày 3",priority=22)]
        public static void PlayDayThree()=>BeginAfter("N2_05");
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 3",priority=23)]
        public static void PlayNightThree()=>BeginAfter("D3_06");
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 3 · ra bến, xe tăng chặn đường",priority=24)]
        public static void PlayNightThreeDock()=>BeginAfter("N3_06");
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 3 · Hùng ở lại yểm trợ",priority=25)]
        public static void PlayNightThreeCover()=>BeginAfter("N3_07");
        [MenuItem("ShadowVale/Map 2/Chạy thử Ngày 2",true)]
        [MenuItem("ShadowVale/Map 2/Checkpoint cứu Tâm · M2_D2_04",true)]
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 2",true)]
        [MenuItem("ShadowVale/Map 2/Chạy thử Ngày 3",true)]
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 3",true)]
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 3 · ra bến, xe tăng chặn đường",true)]
        [MenuItem("ShadowVale/Map 2/Chạy thử Đêm 3 · Hùng ở lại yểm trợ",true)]
        static bool CanBegin()=>!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling;

        static void Begin(int phase)=>Enter(phase==2?"Day2":"Night2",()=>SessionState.SetInt(Key+".phase",phase));
        static void BeginAfter(string task)=>Enter(task,()=>SessionState.SetString(Key+".after",task));
        static void Enter(string saves,Action choose)
        {
            if(!CanBegin()||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var map=AssetDatabase.LoadAssetAtPath<SceneAsset>(MapPath);
            if(map==null){Debug.LogError("Không tìm thấy scene Map 2.");return;}
            SessionState.SetString(Key+".startScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Key+".saveRoot",SessionState.GetString(ForestSaveSlots.EditorTestRootKey,""));
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath("Temp/Map02RoutePreviewSaves/"+saves));
            SessionState.SetBool(Key+".active",true);choose();
            EditorSceneManager.playModeStartScene=map;
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            int phase=SessionState.GetInt(Key+".phase",0);string after=SessionState.GetString(Key+".after","");
            if(phase==0&&after==""||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            var arrival=Object.FindFirstObjectByType<Map02Arrival>();
            if(arrival!=null&&arrival.IsPlaying){arrival.Skip();return;}
            var mission=Object.FindFirstObjectByType<Map02Mission>();
            if(mission?.Ready!=true||mission.Chapter==null)return;
            SessionState.SetInt(Key+".phase",0);SessionState.EraseString(Key+".after");
            if(after!=""){ApplyAfter(mission,after);Debug.Log("[Map 2 chạy thử] Đã xong mọi nhiệm vụ tới "+after+". Save thử nằm trong Temp/Map02RoutePreviewSaves.");return;}
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
        /// <summary>Every task in chapter order up to and including <paramref name="task"/> done.</summary>
        public static Map02ChapterState BuildStateAfter(string task)
        {
            var tasks=Map02ChapterDefinition.Load().tasks;
            if(tasks.All(t=>t.id!=task))throw new ArgumentException("Map 2 không có nhiệm vụ "+task,nameof(task));
            var state=new Map02ChapterState();
            foreach(var t in tasks){if(!state.Complete(t.id))throw new InvalidOperationException("Không thể dựng mốc thử: "+t.id);if(t.id==task)break;}
            state.storedRounds=90;state.gearStored=true;state.Validate();return state;
        }
        public static void ApplyToMission(Map02Mission mission,int phase)
        {
            var spawn=phase==2?mission.Chapter.Sites["command"].position+Vector3.back*3:mission.Chapter.NightObservationSpawn;
            var lookAt=phase==2?mission.Chapter.Sites["command"].position:mission.Chapter.NightPatrolPosition;
            Apply(mission,BuildPreviewState(phase),spawn,lookAt,
                phase==2?"CHẠY THỬ NGÀY 2 · Tới gặp anh Thành, nhấn E nhận lệnh tìm Tâm.":"CHẠY THỬ ĐÊM 2 · Bắt đầu tại cầu. Giữ F quan sát tổ đèn pin ngoài đồng; nhấn Z nằm xuống để tránh bị phát hiện.");
        }
        /// <summary>Day 3 / Night 3: the chapter as it stands once <paramref name="task"/> is done, Nam a few metres from the next task.</summary>
        public static void ApplyAfter(Map02Mission mission,string task)
        {
            var state=BuildStateAfter(task);
            if(task=="D2_03")
            {
                state.activeTask=state.trackedTask="D2_04";state.Validate();
                var rescue=mission.Chapter.Sites["scout"].position;
                Apply(mission,state,rescue,mission.Chapter.Sites["command"].position,"CHECKPOINT CỨU TÂM · Đang dìu Tâm, chỉ dùng K54. Đi ra khỏi bán kính 4 m để bắt đầu phục kích.");
                return;
            }
            var next=Map02ChapterDefinition.Load().tasks.FirstOrDefault(state.Available);
            var site=next!=null&&!string.IsNullOrEmpty(next.site)&&mission.Chapter.Sites.TryGetValue(next.site,out var s)&&s!=null?s.position:mission.Player.position;
            string day=state.phase==4?"NGÀY 3":"ĐÊM 3";
            Apply(mission,state,site+Vector3.back*4,site,"CHẠY THỬ "+day+(next!=null?" · "+next.purpose:""));
        }
        static void Apply(Map02Mission mission,Map02ChapterState state,Vector3 spawn,Vector3 lookAt,string message)
        {
            var snapshot=mission.Capture();snapshot.chapter=state;snapshot.world=new Map02WorldState();
            snapshot.checkpoint=snapshot.patrolCheckpoint=null;snapshot.playSeconds=0;snapshot.attackCooldown=0;
            snapshot.health=100;snapshot.equipped=(int)WeaponKind.Rifle;
            snapshot.magazine=mission.Player.GetComponent<PlayerCombat>().MagazineCapacity;
            var previewItems=new System.Collections.Generic.List<Map02Mission.Item>{new Map02Mission.Item{id=mission.RifleAmmo,count=90},new Map02Mission.Item{id="medkit_small",count=3},new Map02Mission.Item{id=snapshot.rifleItemId,count=1},new Map02Mission.Item{id="knife",count=1}};
            string pistol=snapshot.bagLayout?.pistol;if(!string.IsNullOrEmpty(pistol))previewItems.Add(new Map02Mission.Item{id=pistol,count=1});
            snapshot.inventory=previewItems.ToArray();
            snapshot.bagLayout=new Map01Inventory.Layout{primary=snapshot.rifleItemId,pistol=pistol,throwable="stone"};
            snapshot.stamina=mission.CharacterSettings.stamina;snapshot.exploration=null;snapshot.crafting=null;snapshot.craftRemaining=0;snapshot.ground=null;
            if(NavMesh.SamplePosition(spawn,out var hit,4,NavMesh.AllAreas))spawn=hit.position;
            if(state.activeTask=="D2_04")
            {
                state.scoutRescueOrigin=spawn;state.scoutRescueOriginSet=true;state.scoutAmbushStarted=false;
            }
            snapshot.position=spawn;
            var look=Vector3.ProjectOnPlane(lookAt-spawn,Vector3.up);
            snapshot.yaw=look.sqrMagnitude>1e-4f?Quaternion.LookRotation(look).eulerAngles.y:snapshot.yaw;
            mission.Restore(snapshot);mission.SetPaused(false);mission.StoreCheckpoint();ForestMenu.RefreshGameplayBinding();
            mission.Say(message);
        }
        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+".active",false))return;
            var previousRoot=SessionState.GetString(Key+".saveRoot","");
            if(string.IsNullOrEmpty(previousRoot))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previousRoot);
            var previousScene=SessionState.GetString(Key+".startScene","");
            SessionState.SetBool(Key+".active",false);SessionState.SetInt(Key+".phase",0);SessionState.EraseString(Key+".after");
            EditorApplication.delayCall+=()=>EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previousScene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene);
        }
    }
}
