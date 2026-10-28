using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Night 3 lane holds. Evacuation rises while Nam holds the lane (slower while soldiers still press it);
    /// a stage is held when its mark is reached and no soldier of either wave is left. No countdown.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        static readonly Dictionary<string,Vector2Int> Stages=new Dictionary<string,Vector2Int>
        {{"N3_04",new Vector2Int(0,60)},{"N3_05A",new Vector2Int(60,70)},{"N3_05B",new Vector2Int(70,80)},{"N3_05C",new Vector2Int(80,90)}};
        const float LaneRadius=12,PressedRate=.4f,SecondWaveAt=.5f;
        bool ammoHinted;

        /// <summary>Evacuation shown to the player, including the stage being held.</summary>
        public int Evacuation
        {
            get
            {
                var active=Map02ChapterDefinition.Load().Find(State.activeTask);
                if(active==null||!Stages.TryGetValue(active.id,out var stage)||!State.Available(active))return State.evacuation;
                return Mathf.FloorToInt(Mathf.Lerp(stage.x,stage.y,Mathf.Clamp01(State.taskProgress)));
            }
        }
        public int SoldiersPressing(string task)=>Enemies.Count(e=>e!=null&&!e.Dead&&(e.group==task||e.group==task+"_b"));
        bool InLane(Map02ChapterTask t)=>Vector3.Distance(Mission.Player.position,markers[t.id].transform.position)<LaneRadius;

        void StartDefend(Map02ChapterTask t)
        {
            if(State.activeTask!=t.id){State.activeTask=t.id;State.workCount=State.Count(t.id);State.taskProgress=0;}
            workTask=t;State.trackedTask=t.id;EnsureGroup(t.id,4,true,t.site);
            Mission.Say("Giữ ngõ và đẩy lùi tốp địch để dân đi qua.");
        }
        /// <summary>Returns true when the stage is held and its closing step has started.</summary>
        bool TickLane(Map02ChapterTask t,float dt)
        {
            if(!State.combatGroups.Contains(t.id))EnsureGroup(t.id,4,true,t.site);
            if(State.taskProgress>=SecondWaveAt&&!State.combatGroups.Contains(t.id+"_b"))EnsureGroup(t.id+"_b",3,true,t.site,1);
            int pressing=SoldiersPressing(t.id);float before=State.taskProgress;
            if(InLane(t)&&State.combatGroups.Contains(t.id))
                State.taskProgress=Mathf.Min(1,State.taskProgress+dt/Mathf.Max(1,t.seconds)*(pressing>0?PressedRate:1));
            if(t.id=="N3_04"&&before<SecondWaveAt&&State.taskProgress>=SecondWaveAt)Bark("m2v3_boatsafe_01"); // Boat 1 leaves at 30 %.
            if(State.taskProgress<1||pressing>0||!State.combatGroups.Contains(t.id+"_b"))return false;
            BeginScene(t);return true;
        }

        // Rounds come back from the fallen: 5–8 each, picked up by walking over them, once (saved).
        static int DropFor(string id){int sum=0;foreach(var ch in id)sum+=ch;return 5+sum%4;}
        void TickAmmo()
        {
            if(State.phase!=5)return;
            foreach(var e in Enemies)
            {
                if(e==null||!e.Dead||e.Looted||Vector3.Distance(e.transform.position,Mission.Player.position)>1.8f)continue;
                int rounds=DropFor(e.id);e.Looted=true;
                Mission.Inventory[Mission.RifleAmmo]=Inventory(Mission.RifleAmmo)+rounds;Mission.Say($"Nhặt {rounds} viên đạn.");
            }
            if(ammoHinted||State.Done("N3_WOUND"))return;
            int left=Inventory(Mission.RifleAmmo)+combat.RoundsInMagazine;
            if(left<10&&Enemies.Any(e=>e!=null&&!e.Dead&&e.group!="scout_patrol")){ammoHinted=true;Bark("m2v3_ammo_01");}
        }
        void DrawAmmoMarks(GUIStyle label)
        {
            if(State.phase!=5||sceneTask!=null)return;
            var cam=rig.GetComponent<Camera>();var style=new GUIStyle(label){fontSize=17,alignment=TextAnchor.MiddleCenter};
            foreach(var e in Enemies)
            {
                if(e==null||!e.Dead||e.Looted||Vector3.Distance(e.transform.position,Mission.Player.position)>15)continue;
                var p=cam.WorldToViewportPoint(e.transform.position+Vector3.up*.4f);
                if(p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1)GUI.Label(new Rect(p.x*1600-40,(1-p.y)*900-14,80,28),"◇ đạn",style);
            }
        }
    }
}
