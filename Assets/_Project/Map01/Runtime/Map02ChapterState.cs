using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    [Serializable] public sealed class Map02ChapterTask
    {
        public string id,title,kind,site,scene,purpose;
        public int phase,count=1;
        public float seconds=3;
        public string[] requires;
    }
    [Serializable] public sealed class Map02ChapterDefinition
    {
        public int version;
        public Map02ChapterTask[] tasks;
        public Map02DialogueCatalog.Entry[] entries;
        static Map02ChapterDefinition cached;
        public static Map02ChapterDefinition Load()=>cached??=JsonUtility.FromJson<Map02ChapterDefinition>(Resources.Load<TextAsset>("Story/Map02Chapter").text);
        public Map02ChapterTask Find(string id)=>tasks.FirstOrDefault(t=>t.id==id);
        public Map02DialogueCatalog.Entry[] Lines(string scene)=>entries.Where(e=>e.conversation=="m2v3_"+scene).ToArray();
    }
    [Serializable] public sealed class Map02ChapterState
    {
        public Map02DayOneState dayOne=new Map02DayOneState();
        public int version=3,phase,evacuation,patrolWarnings,storedRounds;
        public bool scoutPistolIssued;
        public int scoutAmbushSpawned;
        public bool scoutRescueOriginSet,scoutAmbushStarted;
        public Vector3 scoutRescueOrigin;
        public bool TryStartScoutAmbush(Vector3 position)
        {
            if(!scoutRescueOriginSet)return false;
            if(Vector3.ProjectOnPlane(position-scoutRescueOrigin,Vector3.up).sqrMagnitude>=16)scoutAmbushStarted=true;
            return scoutAmbushStarted;
        }
        // Additive version 3 fields: older completed tasks stay completed.
        public int wireStage; // 0 in store, 1 carried, 2 delivered
        public float patrolScanSeconds;
        public float patrolFocusSeconds;
        public Vector3 patrolFocusDirection;
        public int night2GuidanceStage;
        public bool night2ChainCuePlayed;
        public float villageDaySeconds; public bool villagePlanePlayed;
        public List<string> night2RouteVisited=new List<string>();
        public bool PatrolRouteActive=>phase==3&&Done("N2_02")&&!Done("N2_05");
        public bool TakeWire()
        {
            if(!Available(Map02ChapterDefinition.Load().Find("D2_05"))||wireStage!=0||!string.IsNullOrEmpty(carry))return false;
            wireStage=1;activeTask=trackedTask=carry="D2_05";return true;
        }
        public bool DeliverWire()
        {
            if(wireStage!=1||carry!="D2_05"||activeTask!="D2_05")return false;
            return Complete("D2_05");
        }
        public void ResetPatrolRoute()
        {
            if(!PatrolRouteActive)return;
            completed.RemoveAll(id=>id=="N2_03"||id=="N2_04");
            introduced.RemoveAll(id=>id=="N2_03"||id=="N2_04"||id=="N2_05");
            activeTask=carry=null;trackedTask="N2_03";taskProgress=0;workCount=0;
            patrolWarnings=0;patrolScanSeconds=patrolFocusSeconds=0;patrolFocusDirection=Vector3.zero;night2GuidanceStage=1;night2ChainCuePlayed=false;night2RouteVisited.Clear();
        }
        public List<string> introduced=new List<string>();
        public List<string> completed=new List<string>();
        public List<Counter> counts=new List<Counter>();
        [Serializable] public sealed class Counter {public string id;public int count;}
        public int Count(string id)=>counts.FirstOrDefault(c=>c.id==id)?.count??0;
        public int Increment(string id){var c=counts.FirstOrDefault(x=>x.id==id);if(c==null){c=new Counter{id=id};counts.Add(c);}return ++c.count;}
        public List<string> combatGroups=new List<string>();
        public List<UnitSnapshot> enemies=new List<UnitSnapshot>();
        public string activeTask,carry,trackedTask;
        [Serializable] public sealed class ActorSnapshot {public string role;public Vector3 position;}
        public List<ActorSnapshot> actors=new List<ActorSnapshot>();
        public Map02ChapterTask TrackedTask
        {
            get
            {
                var def=Map02ChapterDefinition.Load();var active=def.Find(activeTask);
                if(Available(active)&&(!string.IsNullOrEmpty(carry)||active.kind=="defend"||active.kind=="coverFire"||active.kind=="escortHung"||active.kind=="escortScout"||active.kind=="patrolHung"||active.kind.StartsWith("rescue")))return active;
                var tracked=def.Find(trackedTask);if(Available(tracked))return tracked;
                tracked=AvailableTasks.FirstOrDefault();trackedTask=tracked?.id;return tracked;
            }
        }
        public bool Track(string id){var task=Map02ChapterDefinition.Load().Find(id);if(!Available(task))return false;var active=Map02ChapterDefinition.Load().Find(activeTask);if(Available(active)&&active.id!=id&&(!string.IsNullOrEmpty(carry)||active.kind=="defend"||active.kind=="coverFire"||active.kind=="escortHung"||active.kind=="escortScout"||active.kind=="patrolHung"||active.kind.StartsWith("rescue")))return false;trackedTask=id;return true;}
        public float taskProgress;
        /// <summary>Seconds of the cover fire (N3_08) so far; taskProgress is then Hùng's share of the ditch.</summary>
        public float coverClock;
        public int workCount;
        public bool wounded,hungDead,radioTransferred,tankDestroyed,artilleryFired,finished,gearStored;
        [Serializable] public sealed class UnitSnapshot {public string id,group;public Vector3 position;public float health;public bool looted;}
        public bool Done(string id)=>completed.Contains(id);
        public bool Available(Map02ChapterTask t)=>t!=null&&!finished&&t.phase==phase&&!Done(t.id)&&(t.requires??Array.Empty<string>()).All(Done);
        public IEnumerable<Map02ChapterTask> AvailableTasks=>Map02ChapterDefinition.Load().tasks.Where(Available);
        public bool Complete(string id)
        {
            var t=Map02ChapterDefinition.Load().Find(id);
            if(!Available(t))return false;
            completed.Add(id);activeTask=null;taskProgress=0;coverClock=0;workCount=0;carry=null;
            if(id=="D2_05")wireStage=2;
            if(id=="N3_04")evacuation=60;
            if(id=="N3_05A")evacuation=70;if(id=="N3_05B")evacuation=80;if(id=="N3_05C")evacuation=90;
            if(id=="N3_WOUND")wounded=true;
            if(id=="N3_07")radioTransferred=true;
            if(id=="N3_09"){hungDead=tankDestroyed=artilleryFired=true;evacuation=100;}
            if(id=="DAWN")finished=true;
            while(phase<6&&Map02ChapterDefinition.Load().tasks.Where(q=>q.phase==phase).All(q=>Done(q.id)))phase++;
            return true;
        }
        public void Validate()
        {
            dayOne??=new Map02DayOneState();dayOne.Validate();
            var def=Map02ChapterDefinition.Load();
            if(completed==null||enemies==null||combatGroups==null)throw new System.IO.InvalidDataException("Dữ liệu Map 2 bị thiếu.");
            actors??=new List<ActorSnapshot>();
            if(scoutAmbushSpawned<0||scoutAmbushSpawned>5)throw new System.IO.InvalidDataException("Tốp phục kích dìu Tâm không hợp lệ.");
            if(!float.IsFinite(scoutRescueOrigin.x)||!float.IsFinite(scoutRescueOrigin.y)||!float.IsFinite(scoutRescueOrigin.z))throw new System.IO.InvalidDataException("Điểm đón Tâm không hợp lệ.");
            night2RouteVisited??=new List<string>();
            if(night2GuidanceStage<0||night2GuidanceStage>7||!float.IsFinite(patrolFocusSeconds)||patrolFocusSeconds<0||patrolFocusSeconds>3.01f||
                !float.IsFinite(patrolFocusDirection.x)||!float.IsFinite(patrolFocusDirection.y)||!float.IsFinite(patrolFocusDirection.z))
                throw new System.IO.InvalidDataException("Trạng thái đèn và hướng dẫn bộ đàm Đêm 2 không hợp lệ.");
            if(Done("N2_02")&&night2GuidanceStage==0)night2GuidanceStage=night2ChainCuePlayed?7:1;
            if(wireStage<0||wireStage>2||patrolWarnings<0||patrolWarnings>2||!float.IsFinite(patrolScanSeconds)||patrolScanSeconds<0||
                night2RouteVisited.Any(string.IsNullOrWhiteSpace)||night2RouteVisited.Distinct().Count()!=night2RouteVisited.Count)
                throw new System.IO.InvalidDataException("Trạng thái tuyến Ngày 2 / Đêm 2 không hợp lệ.");
            if(Done("D2_05"))wireStage=2; // Missing fields in old version 3 saves.
            else if(wireStage==2||wireStage==1&&(activeTask!="D2_05"||carry!="D2_05"||!Available(def.Find("D2_05"))))
                throw new System.IO.InvalidDataException("Cuộn dây ăng-ten không khớp tiến trình.");
            if(actors.Any(a=>a==null||string.IsNullOrEmpty(a.role)||!float.IsFinite(a.position.x)||!float.IsFinite(a.position.y)||!float.IsFinite(a.position.z))||actors.Select(a=>a.role).Distinct().Count()!=actors.Count)throw new System.IO.InvalidDataException("Vị trí nhân vật Map 2 không hợp lệ.");
            if(counts==null||counts.Any(c=>c==null||def.Find(c.id)==null||c.count<0||c.count>def.Find(c.id).count)||counts.Select(c=>c.id).Distinct().Count()!=counts.Count)throw new System.IO.InvalidDataException("Số lượt thao tác Map 2 không hợp lệ.");
            if(completed.Any(id=>def.Find(id)==null))throw new System.IO.InvalidDataException("Nhiệm vụ Map 2 không tồn tại.");
            if(def.tasks.Any(t=>t.phase<phase&&!Done(t.id))||completed.Any(id=>def.Find(id).phase>phase))throw new System.IO.InvalidDataException("Ngày đêm không khớp các nhiệm vụ đã làm.");
            if(version!=3||phase<0||phase>6||completed==null||enemies==null||combatGroups==null||completed.Distinct().Count()!=completed.Count||
                completed.Any(id=>def.Find(id)==null)||completed.Any(id=>(def.Find(id).requires??Array.Empty<string>()).Any(pre=>!Done(pre)))||
                enemies.Any(e=>e==null||string.IsNullOrEmpty(e.id)||e.health<0||!float.IsFinite(e.health))||enemies.Select(e=>e.id).Distinct().Count()!=enemies.Count||
                workCount<0||!float.IsFinite(taskProgress)||taskProgress<0||!float.IsFinite(coverClock)||coverClock<0||evacuation<0||evacuation>100||
                (hungDead&&!Done("N3_09"))||(finished&&!Done("DAWN")))throw new System.IO.InvalidDataException("Tiến trình ba ngày đêm Map 2 không hợp lệ.");
        }
        public static string PhaseName(int p)=>new[]{"NGÀY 1 · CÓ CHỖ NGHỈ CHÂN","ĐÊM 1 · BUỔI TRỰC CHUNG","NGÀY 2 · NGƯỜI CHƯA TRỞ VỀ","ĐÊM 2 · GIỮ MỘT KHOẢNG YÊN","NGÀY 3 · LO CHO NGƯỜI Ở LẠI","ĐÊM 3 · KHÔNG KỊP TRỞ TAY","BÌNH MINH · CHỖ TRỰC CÒN TRỐNG"}[Mathf.Clamp(p,0,6)];
        public string Objective=>finished?"Ta giữ được làng. Người dân an toàn. Một chỗ trực bên Nam còn trống.":string.Join("\n",AvailableTasks.Select(t=>"• "+t.title));
        // Old saves preserve the player's chapter position rather than restarting their inventory.
        public static Map02ChapterState Migrate(Map02WorldState old)
        {
            var s=new Map02ChapterState();int last=old.story.stage switch {
                Map02Stage.Contact=>-1,Map02Stage.VillageHelp=>0,Map02Stage.MissingScout=>9,
                Map02Stage.QuietPreparation=>21,Map02Stage.Encirclement=>27,Map02Stage.HoldLanes=>30,
                Map02Stage.RescueFamilies=>31,Map02Stage.Breakout=>35,Map02Stage.Departure=>41,
                Map02Stage.Reception=>41,Map02Stage.Complete=>42,_=>-1};
            foreach(var t in Map02ChapterDefinition.Load().tasks.Take(last+1))s.Complete(t.id);
            return s;
        }
    }
}
