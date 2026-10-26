using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// N3_08: Nam covers Hùng from the bank. The tank's escort holds behind it and ducks when shot at;
    /// Hùng crawls along the ditch and reaches the tracks after 60–90 s depending on how well Nam keeps
    /// their heads down. Nam cannot fall and cannot fail this; it only changes the pace.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public const float CoverFastest=40,CoverSlowest=55,EscortDownBeat=1.5f,CoverLeash=3.5f;   // with the tank 30 m off, a steady crouched run along the ditch (the script had 60–90 s)
        float hungDitchAt;
        float escortDownFor;
        const string Escort="N3_08";
        Vector3[] hungCanal;float panClock;int pansDone;ParticleSystem sparks;
        public Vector3[] HungCanal=>hungCanal??=BuildCanal();
        public int EscortStanding=>Enemies.Count(e=>e!=null&&e.group==Escort&&!e.Dead&&!e.Suppressed);

        Vector3[] BuildCanal()
        {
            // Authored "hungCanal1..n" points win; otherwise an arc off the road from the bank to the tracks.
            var authored=Layout?.sites?.Where(s=>s.id.StartsWith("hungCanal")).OrderBy(s=>s.id).Select(s=>s.position).ToArray()??Array.Empty<Vector3>();
            var start=sites["ditch"].position;var end=ChargePoint;
            if(authored.Length>0)return new[]{start}.Concat(authored).Concat(new[]{end}).ToArray();
            var along=Vector3.ProjectOnPlane(end-start,Vector3.up);var side=Vector3.Cross(Vector3.up,along.normalized);
            if(Vector3.Dot(side,water["boatMid"]-start)>0)side=-side; // Inland, away from the river and the gun.
            var points=new Vector3[9];
            for(int i=0;i<points.Length;i++)
            {
                float t=i/(points.Length-1f);var p=Vector3.Lerp(start,end,t)+side*3f*Mathf.Sin(Mathf.PI*t);
                points[i]=NavMesh.SamplePosition(p,out var hit,3,NavMesh.AllAreas)?hit.position:p;
            }
            points[0]=start;points[points.Length-1]=end;return points;
        }
        void SpawnEscort()
        {
            if(State.combatGroups.Contains(Escort)||tank==null)return;
            var dock=sites["tankDock"].position;var forward=Vector3.ProjectOnPlane(water["boatMid"]-dock,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            var spots=new[]{-forward*4+right*2.5f,-forward*4-right*2.5f,-forward*6.5f+right*1,-forward*6.5f-right*1};
            for(int i=0;i<spots.Length;i++)
            {
                var p=dock+spots[i];if(NavMesh.SamplePosition(p,out var hit,3,NavMesh.AllAreas))p=hit.position;
                SpawnEnemy(Escort+"_"+i,Escort,p,100,true);
            }
            State.combatGroups.Add(Escort);
        }
        void StartCover(Map02ChapterTask t)
        {
            SpawnEscort();health.CinematicInvulnerable=true;hungCanal=null;pansDone=Mathf.FloorToInt(State.taskProgress*3);hungDitchAt=State.taskProgress*Length(HungCanal);
            roles["Hùng"].StopAt(Along(HungCanal,State.taskProgress*Length(HungCanal)));
            combat.Equip(WeaponKind.Rifle);combat.SetHandsOccupied(false);
            Mission.Say("Yểm trợ Hùng: bắn tốp lính sau xe tăng cho chúng cúi đầu.");Bark("m2v3_sacrifice_01");
        }
        /// <summary>Covering Hùng, Nam holds the bank: he can shift a few metres along it, never go after Hùng.</summary>
        void HoldCoverPosition()
        {
            if(!CoverFireActive||sceneTask!=null||!markers.TryGetValue("N3_08",out var mark))return;
            var off=Vector3.ProjectOnPlane(Mission.Player.position-mark.transform.position,Vector3.up);if(off.magnitude<=CoverLeash)return;
            var cc=Mission.Player.GetComponent<CharacterController>();var back=-off.normalized*(off.magnitude-CoverLeash);
            if(cc!=null&&cc.enabled)cc.Move(back);else Mission.Player.position+=back;
            if(Time.time>leashSaidAt+4){leashSaidAt=Time.time;Mission.Say("Giữ vị trí trên bờ đất mà yểm trợ — Hùng tự tiếp cận xe tăng.");}
        }
        float leashSaidAt=-10;
        /// <summary>Returns true when Hùng has reached the tracks and the sacrifice begins.</summary>
        bool TickCover(Map02ChapterTask t,float dt)
        {
            SpawnEscort();health.CinematicInvulnerable=true;
            var squad=Enemies.Where(e=>e!=null&&e.group==Escort).ToArray();int down=squad.Count(e=>e.Dead||e.Suppressed);
            float pace=squad.Length>0&&squad.All(e=>e.Dead)?2:down*2>=squad.Length?1.5f:1;
            State.coverClock+=dt;
            // The men round the tank all down: no one left to see the ditch, Hùng goes for the tracks now.
            if(squad.Length>0&&squad.All(e=>e.Dead)){escortDownFor+=dt;if(escortDownFor>=EscortDownBeat){health.CinematicInvulnerable=false;State.taskProgress=1;BeginScene(t);return true;}}else escortDownFor=0;
            // Never ahead of the 60 s schedule, never behind the 90 s one.
            State.taskProgress=Mathf.Clamp(State.taskProgress+dt/CoverSlowest*pace,State.coverClock/CoverSlowest,State.coverClock/CoverFastest);
            State.taskProgress=Mathf.Clamp01(State.taskProgress);
            float distance=State.taskProgress*Length(HungCanal);
            float walk=dt>0?Mathf.Clamp((distance-hungDitchAt)/dt,0,3):0;hungDitchAt=distance;   // the walk plays at the speed he really moves
            roles["Hùng"].SceneMove(Along(HungCanal,distance),Along(HungCanal,distance+.6f),walk);
            if(pansDone<2&&State.taskProgress>=(pansDone+1)/3f){pansDone++;panClock=1.5f;} // Glance at Hùng a third and two thirds of the way.
            if(State.taskProgress<1||State.coverClock<CoverFastest)return false;
            health.CinematicInvulnerable=false;BeginScene(t);return true;
        }
        void OnPlayerShot(WeaponKind kind,Vector3 muzzle)
        {
            if(!kind.IsGunKind()||State.activeTask!=Escort)return;
            var cam=rig.GetComponent<Camera>().transform;SuppressAlong(new Ray(cam.position,cam.forward));
        }
        /// <summary>A round passing within 1.5 m makes an escort soldier duck; one hitting the tank only sparks.</summary>
        internal void SuppressAlong(Ray ray)
        {
            foreach(var e in Enemies)
            {
                if(e==null||e.Dead||e.group!=Escort)continue;
                var chest=e.transform.position+Vector3.up*1.3f;var along=Vector3.Dot(chest-ray.origin,ray.direction);
                if(along>0&&Vector3.Distance(ray.origin+ray.direction*along,chest)<1.5f)e.Suppress();
            }
            if(tank!=null&&tank.activeSelf&&Physics.Raycast(ray,out var hit,120,~0,QueryTriggerInteraction.Ignore)&&hit.transform.IsChildOf(tank.transform))
            {
                if(sparks==null){sparks=Map01Fx.System(root.transform,"Rounds sparking off the tank",hit.point,Map01Fx.Glow,.4f,40);var main=sparks.main;main.startLifetime=.25f;main.startSpeed=5;main.startSize=.08f;main.startColor=new Color(1,.85f,.5f);}
                sparks.transform.position=hit.point;sparks.Emit(10);
            }
        }
        void PanToHung()
        {
            if(panClock<=0||sceneTask!=null)return;
            panClock-=Time.deltaTime;var cam=rig.GetComponent<Camera>().transform;
            float weight=Mathf.Sin(Mathf.Clamp01(1-panClock/1.5f)*Mathf.PI)*.85f;
            var look=Quaternion.LookRotation(roles["Hùng"].transform.position+Vector3.up*.6f-cam.position);
            if(panClock>0)rig.SetCinematicView(cam.position,look,cam.GetComponent<Camera>().fieldOfView,weight);else rig.ClearCinematicView();
        }
        void DrawCover(GUIStyle label)
        {
            if(State.activeTask!=Escort||sceneTask!=null)return;
            var tint=GUI.color;GUI.color=new Color(0,0,0,.55f);GUI.DrawTexture(new Rect(560,108,480,18),Texture2D.whiteTexture);
            GUI.color=new Color(.92f,.8f,.45f);GUI.DrawTexture(new Rect(560,108,480*Mathf.Clamp01(State.taskProgress),18),Texture2D.whiteTexture);GUI.color=tint;
            GUI.Label(new Rect(560,128,600,30),"Hùng đang bò dọc bờ mương",new GUIStyle(label){fontSize=18});
        }
    }
    static class WeaponKindExtensions
    {
        public static bool IsGunKind(this WeaponKind kind)=>kind==WeaponKind.Rifle||kind==WeaponKind.Pistol||kind==WeaponKind.Sniper;
    }
}
