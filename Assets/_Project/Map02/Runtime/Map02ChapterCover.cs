using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// N3_08: Nam covers Hùng from a fixed spot on the bank while Hùng crawls the ditch to the tank (<see cref="CoverSeconds"/>).
    /// Nam does not move; the player brings the rifle up and aims. Until Hùng reaches the tracks, the tank's escort comes out
    /// from behind it in waves (<see cref="WaveAt"/>). As each wave is out, time slows (<see cref="FocusScale"/>) and every
    /// soldier is marked with a red ring that closes over <see cref="FocusWindow"/> real seconds: one hit drops a man. Miss
    /// one and Hùng is seen: the cover fails and that wave starts again a moment before it comes out (25/11/2026).
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public const float CoverSeconds=30,FocusWindow=3,FocusScale=.25f,FocusLead=.8f,CoverFailShow=2.6f,CoverLeash=.6f;
        /// <summary>Seconds into the crawl when each wave comes out, and how many men are in it.</summary>
        public static readonly float[] WaveAt={5,14,23};
        public static readonly int[] WaveSize={2,3,3};
        const string Escort="N3_08";
        float hungDitchAt;
        Vector3[] hungCanal;float panClock;int pansDone;ParticleSystem sparks;
        int liveWave=-1;float focusLead,focusLeft=-1,coverFailClock=-1,coverFixedStep;bool focusSlowed;Texture2D focusRing;
        public Vector3[] HungCanal=>hungCanal??=BuildCanal();
        /// <summary>A wave is out and its 3 s are running (time slowed, rings shown).</summary>
        public bool CoverFocusActive=>focusLeft>=0;
        public float CoverFocusLeft=>Mathf.Max(0,focusLeft);
        public bool CoverFailed=>coverFailClock>=0;
        public int CoverWave=>liveWave;
        public Map02ChapterEnemy[] CoverWaveEnemies=>liveWave<0?Array.Empty<Map02ChapterEnemy>():WaveMen(liveWave);
        public int EscortStanding=>CoverWaveEnemies.Count(e=>!e.Dead);

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
        static string WaveId(int wave,int i)=>Escort+"_w"+wave+"_"+i;
        Map02ChapterEnemy[] WaveMen(int wave)=>Enemies.Where(e=>e!=null&&e.group==Escort&&e.id.StartsWith(Escort+"_w"+wave+"_")).ToArray();
        /// <summary>Each wave steps out from behind the tank, on alternating sides, so the rings are spread across the hull.</summary>
        void SpawnWave(int wave)
        {
            var dock=tank!=null?tank.transform.position:sites["tankDock"].position;
            var forward=Vector3.ProjectOnPlane(Mission.Player.position-dock,Vector3.up).normalized;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            var right=Vector3.Cross(Vector3.up,forward);
            var spots=new[]{right*3.6f,-right*3.6f,-forward*4.5f+right*1.6f,right*5.4f-forward*2f,-right*5.4f-forward*2f};
            int offset=wave*2;
            for(int i=0;i<WaveSize[wave];i++)
            {
                var p=dock+spots[(i+offset)%spots.Length];if(NavMesh.SamplePosition(p,out var hit,3,NavMesh.AllAreas))p=hit.position;
                SpawnEnemy(WaveId(wave,i),Escort,p,100,true);
                var man=Enemies[Enemies.Count-1];man.transform.rotation=Quaternion.LookRotation(-forward);
                man.HitPoints.Damaged+=_=>{if(!man.Dead)man.HitPoints.TakeDamage(10000,man.transform.position,Mission.Player.gameObject);};   // one hit drops a marked man
            }
            if(!State.combatGroups.Contains(Escort))State.combatGroups.Add(Escort);
            liveWave=wave;focusLead=FocusLead;focusLeft=-1;
            Mission.Say(wave==0?"Lính địch nhô ra sau xe tăng!":"Thêm lính sau xe tăng!");
        }
        void StartCover(Map02ChapterTask t)
        {
            health.CinematicInvulnerable=true;hungCanal=null;EndCoverFocus();coverFailClock=-1;liveWave=-1;
            PlaceAtCoverSpot();
            ResumeCover();
            combat.Equip(WeaponKind.Rifle);combat.SetHandsOccupied(false);
            Mission.Say("Yểm trợ Hùng: bật ngắm, hạ lính nhô ra sau xe tăng trước khi chúng thấy Hùng.");Bark("m2v3_sacrifice_01");
        }
        /// <summary>A load or a retry: any man of a wave still unfinished goes, and the crawl starts again just before that wave.</summary>
        void ResumeCover()
        {
            EndCoverFocus();coverFailClock=-1;liveWave=-1;
            foreach(var e in Enemies.Where(e=>e!=null&&e.group==Escort&&(!e.Dead||!e.id.StartsWith(Escort+"_w"))).ToArray()){Enemies.Remove(e);Destroy(e.gameObject);}
            State.coverWave=Mathf.Clamp(State.coverWave,0,WaveAt.Length);
            if(State.coverWave<WaveAt.Length)State.coverClock=Mathf.Min(State.coverClock,Mathf.Max(0,WaveAt[State.coverWave]-1.2f));
            State.taskProgress=Mathf.Clamp01(State.coverClock/CoverSeconds);
            hungDitchAt=State.taskProgress*Length(HungCanal);pansDone=Mathf.FloorToInt(State.taskProgress*3);
            roles["Hùng"].StopAt(Along(HungCanal,hungDitchAt));
            PlaceAtCoverSpot();
        }
        /// <summary>Nam's spot on the bank, facing the tank.</summary>
        void PlaceAtCoverSpot()
        {
            if(!markers.TryGetValue(Escort,out var mark)||Mission?.Player==null)return;
            var spot=mark.transform.position;if(NavMesh.SamplePosition(spot,out var hit,2,NavMesh.AllAreas))spot=hit.position;
            var at=tank!=null?tank.transform.position:sites["tankDock"].position;var look=Vector3.ProjectOnPlane(at-spot,Vector3.up);
            var cc=Mission.Player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;
            Mission.Player.SetPositionAndRotation(spot,look.sqrMagnitude>.01f?Quaternion.LookRotation(look):Mission.Player.rotation);
            if(cc!=null)cc.enabled=true;Physics.SyncTransforms();
        }
        /// <summary>Covering Hùng, Nam stays put on the bank: only aiming and firing.</summary>
        Vector3 HoldCoverMovement(Vector3 displacement)=>CoverFireActive?new Vector3(0,displacement.y,0):displacement;
        void HoldCoverPosition()
        {
            if(!CoverFireActive||sceneTask!=null||!markers.TryGetValue(Escort,out var mark))return;
            if(Vector3.ProjectOnPlane(Mission.Player.position-mark.transform.position,Vector3.up).magnitude>CoverLeash+1.5f)PlaceAtCoverSpot();
        }
        void BeginCoverFocus()
        {
            focusLeft=FocusWindow;
            if(!focusSlowed){coverFixedStep=Time.fixedDeltaTime;focusSlowed=true;}
            ApplyCoverTime();
        }
        void ApplyCoverTime()
        {
            if(!focusSlowed)return;
            bool hold=Mission.Paused||ForestMenu.Visible;
            Time.timeScale=hold?0:FocusScale;Time.fixedDeltaTime=coverFixedStep*FocusScale;
        }
        void EndCoverFocus()
        {
            focusLeft=-1;
            if(!focusSlowed)return;
            focusSlowed=false;Time.timeScale=Mission!=null&&Mission.Paused?0:1;Time.fixedDeltaTime=coverFixedStep;
        }
        void FailCover()
        {
            EndCoverFocus();coverFailClock=0;rig.Shake(.25f,.5f);
            Mission.Say("Hùng bị phát hiện! Yểm trợ thất bại.");
        }
        /// <summary>Returns true when Hùng has reached the tracks and the sacrifice begins.</summary>
        bool TickCover(Map02ChapterTask t,float dt)
        {
            health.CinematicInvulnerable=true;
            if(coverFailClock>=0)
            {
                coverFailClock+=Time.unscaledDeltaTime;
                if(coverFailClock>=CoverFailShow)ResumeCover();
                return false;
            }
            State.coverClock+=dt;
            if(liveWave<0&&State.coverWave<WaveAt.Length&&State.coverClock>=WaveAt[State.coverWave])SpawnWave(State.coverWave);
            if(liveWave>=0)
            {
                var men=WaveMen(liveWave);
                if(men.All(e=>e.Dead))
                {
                    EndCoverFocus();State.coverWave=liveWave+1;liveWave=-1;
                    Mission.Say(State.coverWave<WaveAt.Length?"Hạ hết! Hùng tiếp tục bò.":"Sạch bóng địch quanh xe tăng!");
                }
                else if(focusLeft<0){focusLead-=dt;if(focusLead<=0)BeginCoverFocus();}
                else
                {
                    ApplyCoverTime();focusLeft-=Time.unscaledDeltaTime;
                    if(focusLeft<=0){FailCover();return false;}
                }
            }
            // Hùng crawls on, slowed with everyone else while a wave is out; he never reaches the tracks before the last wave is down.
            float cap=State.coverWave<WaveAt.Length?CoverSeconds-.5f:CoverSeconds;
            State.coverClock=Mathf.Min(State.coverClock,cap);
            State.taskProgress=Mathf.Clamp01(State.coverClock/CoverSeconds);
            float distance=State.taskProgress*Length(HungCanal);
            float walk=dt>0?Mathf.Clamp((distance-hungDitchAt)/dt,0,3):0;hungDitchAt=distance;   // the walk plays at the speed he really moves
            roles["Hùng"].SceneMove(Along(HungCanal,distance),Along(HungCanal,distance+.6f),walk);
            if(liveWave<0&&pansDone<2&&State.taskProgress>=(pansDone+1)/3f){pansDone++;panClock=1.5f;} // Glance at Hùng a third and two thirds of the way.
            if(State.coverClock<CoverSeconds||State.coverWave<WaveAt.Length)return false;
            EndCoverFocus();health.CinematicInvulnerable=false;State.taskProgress=1;BeginScene(t);return true;
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
            if(liveWave>=0){panClock=0;rig.ClearCinematicView();return;}   // never pull the view away from a wave
            panClock-=Time.deltaTime;var cam=rig.GetComponent<Camera>().transform;
            float weight=Mathf.Sin(Mathf.Clamp01(1-panClock/1.5f)*Mathf.PI)*.85f;
            var look=Quaternion.LookRotation(roles["Hùng"].transform.position+Vector3.up*.6f-cam.position);
            if(panClock>0)rig.SetCinematicView(cam.position,look,cam.GetComponent<Camera>().fieldOfView,weight);else rig.ClearCinematicView();
        }
        Texture2D FocusRing()
        {
            if(focusRing!=null)return focusRing;
            const int size=128;focusRing=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Cover focus ring",hideFlags=HideFlags.DontSave};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(size*.5f,size*.5f));
                float a=Mathf.Clamp01(1-Mathf.Abs(d-56)/4.5f);pixels[y*size+x]=new Color32(255,255,255,(byte)(a*255));
            }
            focusRing.SetPixels32(pixels);focusRing.Apply(false,true);return focusRing;
        }
        void DrawCover(GUIStyle label)
        {
            if(State.activeTask!=Escort||sceneTask!=null)return;
            var tint=GUI.color;
            if(CoverFailed)
            {
                GUI.color=new Color(0,0,0,.55f);GUI.DrawTexture(new Rect(0,0,1600,900),Texture2D.whiteTexture);GUI.color=tint;
                GUI.Label(new Rect(300,350,1000,200),"HÙNG BỊ PHÁT HIỆN\nYỂM TRỢ THẤT BẠI\nThử lại…",new GUIStyle(label){fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,.85f,.8f)}});
                return;
            }
            GUI.color=new Color(0,0,0,.55f);GUI.DrawTexture(new Rect(560,108,480,18),Texture2D.whiteTexture);
            GUI.color=new Color(.92f,.8f,.45f);GUI.DrawTexture(new Rect(560,108,480*Mathf.Clamp01(State.taskProgress),18),Texture2D.whiteTexture);GUI.color=tint;
            GUI.Label(new Rect(560,128,700,30),$"Hùng đang bò tới xe tăng · đợt lính {Mathf.Min(State.coverWave+(liveWave>=0?1:0),WaveAt.Length)}/{WaveAt.Length}",new GUIStyle(label){fontSize=18});
            if(!CoverFocusActive)return;
            // Slowed: a cold tint, a red ring closing on each man still standing, and the seconds left.
            GUI.color=new Color(.05f,.08f,.12f,.22f);GUI.DrawTexture(new Rect(0,0,1600,900),Texture2D.whiteTexture);
            var cam=rig.GetComponent<Camera>();float left=CoverFocusLeft/FocusWindow,size=Mathf.Lerp(44,120,left),pulse=.75f+.25f*Mathf.Sin(Time.unscaledTime*18);
            foreach(var e in CoverWaveEnemies)
            {
                if(e.Dead)continue;
                var p=cam.WorldToScreenPoint(e.transform.position+Vector3.up*1.25f);if(p.z<=0)continue;
                float x=p.x*1600f/Screen.width,y=(Screen.height-p.y)*900f/Screen.height;
                GUI.color=new Color(1,.12f,.08f,pulse);GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),FocusRing());
                GUI.color=new Color(1,.2f,.15f,.9f);GUI.DrawTexture(new Rect(x-3,y-3,6,6),Texture2D.whiteTexture);
            }
            GUI.color=tint;
            GUI.Label(new Rect(500,170,600,60),CoverFocusLeft.ToString("0.0"),new GUIStyle(label){fontSize=46,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,.3f,.25f)}});
            GUI.Label(new Rect(400,226,800,34),"Hạ hết lính trong vòng đỏ trước khi chúng thấy Hùng",new GUIStyle(label){fontSize=20,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,.9f,.85f)}});
        }
    }
    static class WeaponKindExtensions
    {
        public static bool IsGunKind(this WeaponKind kind)=>kind==WeaponKind.Rifle||kind==WeaponKind.Pistol||kind==WeaponKind.Sniper;
    }
}
