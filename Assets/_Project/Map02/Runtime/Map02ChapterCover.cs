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
    /// soldier is outlined in red for <see cref="FocusWindow"/> real seconds: one hit drops a man. Miss
    /// one and Hùng is seen: the cover fails and that wave starts again a moment before it comes out (25/11/2026).
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        public const float CoverSeconds=30,FocusWindow=3,FocusScale=.25f,FocusLead=.8f,CoverFailShow=2.6f,CoverLeash=.6f,CoverAimZoom=.45f,CoverReload=.5f;
        /// <summary>Seconds into the crawl when each wave comes out, and how many men are in it.</summary>
        public static readonly float[] WaveAt={3,6,9,12,15,18,21,24,27};   // a man every 3 s (25/11/2026)
        public static readonly int[] WaveSize={1,1,1,1,1,1,1,1,1};   // one man at a time
        const string Escort="N3_08";
        float hungDitchAt;
        Vector3[] hungCanal;float panClock;int pansDone;ParticleSystem sparks;
        int liveWave=-1;float focusLead,focusLeft=-1,coverFailClock=-1,coverFixedStep;bool focusSlowed;Material outlineMaterial,outlineMask;Vector3[] coverSpots;
        readonly System.Collections.Generic.Dictionary<Map02ChapterEnemy,System.Collections.Generic.List<GameObject>> outlines=new();AnimatorUpdateMode namUpdateMode;
        Animator NamAnimator=>Mission!=null&&Mission.Player!=null?Mission.Player.GetComponentInChildren<Animator>():null;
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
        /// <summary>Each man steps out beside the tank where Nam can see him clearly, alternating sides; never behind the hull
        /// or the bamboo, where he could not be shot in three seconds (25/11/2026).</summary>
        void SpawnWave(int wave)
        {
            var spots=coverSpots??=ClearSpots();   // the tank stands still through the cover: worked out once
            for(int i=0;i<WaveSize[wave];i++)
            {
                var p=spots[(wave+i)%spots.Length];
                SpawnEnemy(WaveId(wave,i),Escort,p,100,true);
                var man=Enemies[Enemies.Count-1];var face=Vector3.ProjectOnPlane(Mission.Player.position-p,Vector3.up);if(face.sqrMagnitude>.01f)man.transform.rotation=Quaternion.LookRotation(face);
                man.HitPoints.Damaged+=_=>{if(!man.Dead)man.HitPoints.TakeDamage(10000,man.transform.position,Mission.Player.gameObject);};   // one hit drops a marked man
            }
            if(!State.combatGroups.Contains(Escort))State.combatGroups.Add(Escort);
            liveWave=wave;focusLead=FocusLead;focusLeft=-1;
            Mission.Say(wave==0?"Lính địch nhô ra sau xe tăng!":"Thêm lính sau xe tăng!");
        }
        /// <summary>Spots beside the tank in clear sight of Nam's eye, best first, alternating left and right of the hull.</summary>
        Vector3[] ClearSpots()
        {
            var tankAt=tank!=null?tank.transform.position:sites["tankDock"].position;var eye=Mission.Player.position+Vector3.up*1.5f;
            var toward=Vector3.ProjectOnPlane(Mission.Player.position-tankAt,Vector3.up).normalized;if(toward.sqrMagnitude<.01f)toward=Vector3.forward;
            var blockers=SightBlockers(tankAt);
            var left=new System.Collections.Generic.List<(Vector3 p,float score)>();var right=new System.Collections.Generic.List<(Vector3 p,float score)>();
            // In front of the hull, toward Nam, on the road it has just cleared: never back in the grove beside it.
            foreach(float angle in new[]{0f,12f,24f,36f,48f})foreach(float distance in new[]{4.5f,6f,7.5f})foreach(int side in new[]{-1,1})
            {
                if(angle==0&&side<0)continue;
                var p=tankAt+Quaternion.AngleAxis(angle*side,Vector3.up)*toward*distance;
                if(!NavMesh.SamplePosition(p,out var hit,1.5f,NavMesh.AllAreas))continue;p=hit.position;
                if(Flat(p,Mission.Player.position)<8)continue;
                float score=Hidden(eye,p+Vector3.up*1.3f,blockers)+Hidden(eye,p+Vector3.up*.9f,blockers)+angle*.004f;
                // A shot must reach him: anything solid on the line from Nam's eye to his chest rules the spot out.
                if(Physics.Linecast(eye,p+Vector3.up*1.3f,out var block,~0,QueryTriggerInteraction.Ignore)&&!block.transform.IsChildOf(Mission.Player))score+=50;
                (side<0?left:right).Add((p,score));
            }
            bool anyClear=left.Concat(right).Any(s=>s.score<1);   // a clear spot exists: use only clear ones
            var l=left.Where(s=>!anyClear||s.score<1).OrderBy(s=>s.score).Select(s=>s.p).ToArray();var r=right.Where(s=>!anyClear||s.score<1).OrderBy(s=>s.score).Select(s=>s.p).ToArray();
            var spots=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<Mathf.Max(l.Length,r.Length);i++){if(i<r.Length)spots.Add(r[i]);if(i<l.Length)spots.Add(l[i]);}
            if(spots.Count==0)spots.Add(tankAt+toward*4);
            return spots.ToArray();
        }
        /// <summary>Scenery near the tank that can stand between Nam and a man: props and foliage drawn as objects (they often
        /// have no collider) and the terrain's own trees.</summary>
        (Bounds[] boxes,Vector3[] trees) SightBlockers(Vector3 around)
        {
            var boxes=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is SkinnedMeshRenderer)&&!(r is ParticleSystemRenderer)&&!(r is LineRenderer)
                &&(tank==null||!r.transform.IsChildOf(tank.transform))&&!r.transform.IsChildOf(Mission.Player)&&r.bounds.size.magnitude<25&&Vector3.Distance(r.bounds.center,around)<45).Select(r=>r.bounds).ToArray();
            var trees=new System.Collections.Generic.List<Vector3>();
            foreach(var terrain in Terrain.activeTerrains)
            {
                var data=terrain.terrainData;if(data==null)continue;
                foreach(var tree in data.treeInstances){var w=terrain.transform.position+Vector3.Scale(tree.position,data.size);if(Vector3.Distance(w,around)<45)trees.Add(w);}
            }
            return (boxes,trees.ToArray());
        }
        /// <summary>0 when the line from the eye to the point is clear, more the more is in the way.</summary>
        float Hidden(Vector3 eye,Vector3 point,(Bounds[] boxes,Vector3[] trees) blockers)
        {
            float hidden=0,length=Vector3.Distance(eye,point);var ray=new Ray(eye,(point-eye)/length);
            foreach(var hit in Physics.RaycastAll(ray,length-.4f,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(Mission.Player)&&hit.collider.GetComponentInParent<Map02ChapterEnemy>()==null)hidden+=hit.collider is TerrainCollider?5:2;
            foreach(var box in blockers.boxes)if(box.IntersectRay(ray,out var at)&&at<length-.6f&&at>1)hidden+=1;
            foreach(var tree in blockers.trees)
            {
                var flat=new Vector3(tree.x,0,tree.z);var a=new Vector3(eye.x,0,eye.z);var b=new Vector3(point.x,0,point.z);var ab=b-a;
                float t=Mathf.Clamp01(Vector3.Dot(flat-a,ab)/Mathf.Max(ab.sqrMagnitude,.01f));if(t<.98f&&Vector3.Distance(a+ab*t,flat)<1.6f)hidden+=2;
            }
            return hidden;
        }
        void StartCover(Map02ChapterTask t)
        {
            health.CinematicInvulnerable=true;hungCanal=null;coverSpots=null;EndCoverFocus();coverFailClock=-1;liveWave=-1;
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
            focusLeft=FocusWindow;OutlineWave();
            if(!focusSlowed){coverFixedStep=Time.fixedDeltaTime;focusSlowed=true;var nam=NamAnimator;if(nam!=null)namUpdateMode=nam.updateMode;}
            ApplyCoverTime();
        }
        void ApplyCoverTime()
        {
            if(!focusSlowed)return;
            bool hold=Mission.Paused||ForestMenu.Visible;
            Time.timeScale=hold?0:FocusScale;Time.fixedDeltaTime=coverFixedStep*FocusScale;
            // Only the world slows: Nam's firing, reloading, recoil, view and his own animation keep real pace.
            PlayerTime.IgnoreSlowMotion=true;var nam=NamAnimator;if(nam!=null)nam.updateMode=AnimatorUpdateMode.UnscaledTime;
        }
        void EndCoverFocus()
        {
            focusLeft=-1;ClearOutlines();
            if(!focusSlowed)return;
            focusSlowed=false;Time.timeScale=Mission!=null&&Mission.Paused?0:1;Time.fixedDeltaTime=coverFixedStep;
            PlayerTime.IgnoreSlowMotion=false;var nam=NamAnimator;if(nam!=null)nam.updateMode=namUpdateMode;
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
        /// <summary>A red rim round each marked man still standing: his body drawn again, pushed out and back-faced
        /// (Resources/Map02Cover/EnemyOutline), following his animation; gone as he drops or the slow ends.</summary>
        void OutlineWave()
        {
            if(outlineMaterial==null)
            {
                var rim=Resources.Load<Shader>("Map02Cover/EnemyOutline");var mask=Resources.Load<Shader>("Map02Cover/EnemyOutlineMask");
                if(rim==null||mask==null||!rim.isSupported||!mask.isSupported)return;
                outlineMaterial=new Material(rim){name="Cover focus outline",hideFlags=HideFlags.DontSave};
                outlineMask=new Material(mask){name="Cover focus outline mask",hideFlags=HideFlags.DontSave};
            }
            foreach(var man in CoverWaveEnemies)
            {
                if(man.Dead||outlines.ContainsKey(man))continue;
                var parts=new System.Collections.Generic.List<GameObject>();
                foreach(var body in man.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if(body.enabled&&body.sharedMesh!=null)foreach(var material in new[]{outlineMask,outlineMaterial})
                    {
                        var copy=new GameObject(material==outlineMask?"Focus outline mask":"Focus outline");copy.transform.SetParent(body.transform,false);
                        var skin=copy.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=body.sharedMesh;skin.bones=body.bones;skin.rootBone=body.rootBone;skin.localBounds=body.localBounds;
                        skin.updateWhenOffscreen=true;skin.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;skin.receiveShadows=false;
                        skin.sharedMaterials=Enumerable.Repeat(material,body.sharedMesh.subMeshCount).ToArray();parts.Add(copy);
                    }
                foreach(var prop in man.GetComponentsInChildren<MeshRenderer>())   // his rifle and kit
                {
                    var filter=prop.GetComponent<MeshFilter>();if(!prop.enabled||filter==null||filter.sharedMesh==null)continue;
                    foreach(var material in new[]{outlineMask,outlineMaterial})
                    {
                        var copy=new GameObject(material==outlineMask?"Focus outline mask":"Focus outline");copy.transform.SetParent(prop.transform,false);
                        copy.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var r=copy.AddComponent<MeshRenderer>();
                        r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;r.sharedMaterials=Enumerable.Repeat(material,filter.sharedMesh.subMeshCount).ToArray();parts.Add(copy);
                    }
                }
                outlines[man]=parts;
            }
        }
        void TickOutlines()
        {
            if(outlines.Count==0)return;
            foreach(var man in outlines.Keys.Where(m=>m==null||m.Dead||!CoverFocusActive).ToArray()){foreach(var rim in outlines[man])if(rim!=null)Destroy(rim);outlines.Remove(man);}
            if(outlineMaterial!=null)outlineMaterial.SetColor("_Color",new Color(1,.08f,.05f)*(.8f+.2f*Mathf.Sin(Time.unscaledTime*7)));   // a gentle blink
        }
        void ClearOutlines(){foreach(var parts in outlines.Values)foreach(var rim in parts)if(rim!=null)Destroy(rim);outlines.Clear();}
        public int OutlinedMen=>outlines.Count(p=>p.Key!=null&&!p.Key.Dead&&p.Value.Any(r=>r!=null&&r.name=="Focus outline"));
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
            // Slowed: a cold tint and the seconds left; the men themselves carry a red outline (OutlineWave).
            GUI.color=new Color(.05f,.08f,.12f,.22f);GUI.DrawTexture(new Rect(0,0,1600,900),Texture2D.whiteTexture);
            GUI.color=tint;
            GUI.Label(new Rect(500,170,600,60),CoverFocusLeft.ToString("0.0"),new GUIStyle(label){fontSize=46,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,.3f,.25f)}});
            GUI.Label(new Rect(400,226,800,34),"Hạ lính viền đỏ trước khi chúng thấy Hùng",new GUIStyle(label){fontSize=20,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,.9f,.85f)}});
        }
    }
    static class WeaponKindExtensions
    {
        public static bool IsGunKind(this WeaponKind kind)=>kind==WeaponKind.Rifle||kind==WeaponKind.Pistol||kind==WeaponKind.Sniper;
    }
}
