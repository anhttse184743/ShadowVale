using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Staging of Nhịp 3 (the tank shot), Nhịp 5 (the farewell and the sacrifice) and Nhịp 6 (the guns and the
    /// counter-attack). Screen and sound effects are a function of the story state and the scene clock, so a skip
    /// or a load never leaves a red screen, a muffled ear or a stray silhouette behind. Faces are never framed close.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        // N3_09, seconds from the start of the scene (script: Nhịp 5 phần 3 + Nhịp 6).
        public const float RiseEnd=3,PlantEnd=6,ShotAt=8,BlastAt=9,EchoAt=13,FireOrderAt=30,MilitiaAt=32,ThanhAt=38,CounterAt=40,FadeAt=60;
        public float EffectRed {get;private set;}
        public float EffectGrey {get;private set;}
        public float EffectBlack {get;private set;}
        public float EffectFlash {get;private set;}
        public float ListenerCutoff {get;private set;}=22000;
        public int MilitiaCount=>militia.Count(m=>m!=null);
        public GameObject RadioProp=>radioProp;
        readonly List<Transform> militia=new List<Transform>();Vector3[] militiaRoad;
        GameObject radioProp;AudioLowPassFilter muffle;AudioSource ring;Light impactLight,rearFlash,tankFire;ParticleSystem impactSmoke;
        bool shotFired,blasted,woundHit,thanhRunning;int impactsFired;Vector3 hungRiseFrom;

        float SacrificeLineAt(string id)=>id=="m2v3_sacrifice_02"?EchoAt:id=="m2v3_sacrifice_03"?FireOrderAt:id=="m2v3_sacrifice_04"?CounterAt:0;
        bool AfterLine(string task,string line)=>sceneTask?.id==task&&lines!=null&&lines.Take(lineIndex+1).Any(l=>l.id==line);

        void PrepareFinale(Map02ChapterTask task)
        {
            shotFired=blasted=woundHit=thanhRunning=false;impactsFired=0;
            if(task.id=="N3_00")PrepareHandover();
            if(task.id=="N3_WOUND"){breachApproach=null;grazed=false;PlanRetreats();rig.Shake(.05f,BreachDrive);if(Layout?.tankTracksClip!=null)AudioSource.PlayClipAtPoint(Layout.tankTracksClip,sites["tankBreach"].position,.9f);}
            if(task.id=="N3_07")
            {
                // A cut, as the script allows: Hùng is already beside Nam, between him and the tank's fire.
                var nam=Mission.Player.position;var toward=Vector3.ProjectOnPlane(sites["tankDock"].position-nam,Vector3.up).normalized;
                var spot=nam+toward*1.3f;if(NavMesh.SamplePosition(spot,out var hit,2,NavMesh.AllAreas))spot=hit.position;roles["Hùng"].StopAt(spot);
                if(Layout?.tankTracksClip!=null)AudioSource.PlayClipAtPoint(Layout.tankTracksClip,sites["tankHide"].position,.9f);
            }
            if(task.id=="N3_09")
            {
                KeepNamClearOfTheBlast();ClearCounterAttack();
                // He comes up out of the ditch a few metres short of the tracks.
                hungRiseFrom=Along(HungCanal,Mathf.Max(0,Length(HungCanal)-7));   // a hard run to the tracks
                sacrificePath=new[]{hungRiseFrom,ChargePoint};sacrificeTravel=RiseEnd;
            }
        }
        void SampleSacrifice()
        {
            var hung=roles["Hùng"];var t=sceneClock;var tracks=tank.transform.position;
            if(t<RiseEnd)hung.SceneMove(Vector3.Lerp(hungRiseFrom,ChargePoint,t/RiseEnd),tracks,2.6f);
            else if(t<PlantEnd)hung.SceneMove(ChargePoint,tracks,0);
            else if(t<BlastAt)
            {
                // Set: he steps back from the tracks and turns to look at Nam (26/10/2026: no shot from behind the tank).
                float u=Mathf.Clamp01((t-PlantEnd)/(LookBackFrom-PlantEnd));
                hung.SceneMove(Vector3.Lerp(ChargePoint,LookBackSpot,u),t<LookBackFrom?LookBackSpot:Mission.Player.position,t<LookBackFrom?1.3f:0);
            }
            else if(!shotFired){shotFired=true;hung.StopAt(LookBackSpot);hung.SetDead(true);}   // gone in the blast
            if(t>=BlastAt&&!blasted)
            {
                blasted=true;rig.Shake(.6f,1.2f);BlowTank();
                foreach(var e in Enemies)if(e!=null&&!e.Dead&&e.group==Escort)e.HitPoints.TakeDamage(1000,e.transform.position+Vector3.up,null);   // the escort goes with it
            }
            var clipNow=t<PlantEnd?Layout?.hungPlantRetreatClip:t<LookBackFrom?ordinaryWalk:t<BlastAt?namCalmIdle:null;
            if(t>=RiseEnd&&clipNow!=null&&hung.visual!=null)
            {var animator=hung.visual.GetComponentInChildren<Animator>();if(animator!=null){hungActionPose??=new Map01CinematicPose(animator,null,false);hungActionPose.Sample(clipNow,t-RiseEnd,t>=PlantEnd);}}
            TickNhipSau(t);
        }
        void RearFlash()
        {
            if(rearFlash==null){var go=new GameObject("Shot from behind the tank");go.transform.SetParent(tank.transform,false);go.transform.localPosition=new Vector3(0,1.4f,-3f);rearFlash=go.AddComponent<Light>();rearFlash.type=LightType.Point;rearFlash.range=8;rearFlash.color=new Color(1,.8f,.45f);rearFlash.shadows=LightShadows.None;}
            rearFlash.intensity=5;rearFlash.enabled=true;
        }
        /// <summary>Nhịp 6: the guns fall on the village edge and the militia pour down the road past Nam.</summary>
        void TickNhipSau(float t)
        {
            if(rearFlash!=null)rearFlash.enabled=t>=ShotAt&&t<ShotAt+.12f;
            if(t>=FireOrderAt+2&&t<FadeAt-2&&impactsFired<=(int)((t-FireOrderAt-2)/2.6f))
            {
                var at=water["artyImpact"+(impactsFired%3+1)];impactsFired++;
                if(impactLight==null)
                {
                    var go=new GameObject("Artillery impact light");go.transform.SetParent(root.transform);impactLight=go.AddComponent<Light>();impactLight.type=LightType.Point;impactLight.range=45;impactLight.color=new Color(1,.7f,.35f);impactLight.shadows=LightShadows.None;
                    impactSmoke=Map01Fx.System(root.transform,"Artillery smoke",at,Map01Fx.Lit,2,120);var main=impactSmoke.main;main.startLifetime=3.5f;main.startSpeed=6;main.startSize=4;main.startColor=new Color(.2f,.18f,.16f,.8f);
                }
                impactLight.transform.position=at+Vector3.up*4;impactLight.intensity=8;impactSmoke.transform.position=at;impactSmoke.Emit(30);rig.Shake(.07f,.4f);
                if(Layout?.artilleryClip!=null)AudioSource.PlayClipAtPoint(Layout.artilleryClip,at,1);
                else{var sound=Mission.Player.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var clip=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(clip.clip,at,1.2f*clip.EffectiveGain,.35f,0,.85f,clip.startTime,clip.duration);}}
            }
            if(impactLight!=null)impactLight.intensity=Mathf.MoveTowards(impactLight.intensity,0,Time.deltaTime*14);
            if(t>=PocketAt)SpawnPocket();
            if(t>=MilitiaAt&&militia.Count==0)SpawnMilitia();
            while(militiaRoadAt.Count<militia.Count)militiaRoadAt.Add(0);
            TickMilitiaFight(t);TickShell(t);
            if(t>=ThanhAt&&!thanhRunning){thanhRunning=true;var thanh=roles["Anh Thành"];thanh.StopAt(BehindCamera());thanh.SceneDriven=true;thanh.GoTo(pocket.Count>0?pocketCenter:sites["tankDock"].position,3.6f);}
        }
        void SpawnMilitia()
        {
            // Silhouettes against the gun flashes: the militia model again, no new soldiers' model. Into the village, at the
            // soldiers still holding a lane, not at the tank.
            var road=new NavMeshPath();var entry=sites["militiaEntry"].position;var to=pocket.Count>0?pocketCenter:sites["tankDock"].position;
            militiaRoad=NavMesh.CalculatePath(entry,to,NavMesh.AllAreas,road)&&road.status==NavMeshPathStatus.PathComplete?road.corners:new[]{entry,to};
            var art=Layout?.militiaArt!=null?Layout.militiaArt:Layout?.actors?.FirstOrDefault(a=>a.role=="Anh Lực")?.prefab;
            for(int i=0;i<7;i++)
            {
                GameObject body;
                if(art!=null)body=Instantiate(art);
                else{body=GameObject.CreatePrimitive(PrimitiveType.Capsule);Destroy(body.GetComponent<Collider>());var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(.05f,.05f,.05f));body.GetComponent<Renderer>().SetPropertyBlock(block);body.transform.localScale=new Vector3(.6f,.9f,.6f);}
                body.name="Counter-attack silhouette "+i;body.transform.SetParent(root.transform);militia.Add(body.transform);ArmMilitiaman(body.transform);
            }
        }
        void ClearFinale()
        {
            foreach(var m in militia)if(m!=null)Destroy(m.gameObject);militia.Clear();DropCharge();
            if(thanhRunning&&pocket.Count>0){roles["Anh Thành"].StopAt(pocketCenter);thanhRunning=false;}
            ClearCounterAttack();
            if(impactLight!=null)impactLight.intensity=0;if(rearFlash!=null)rearFlash.enabled=false;
            if(thanhRunning){roles["Anh Thành"].StopAt(sites["tankDock"].position);thanhRunning=false;}
        }

        /// <summary>Screen tints, flash and what Nam hears, from the story state and the scene clock.</summary>
        void TickEffects()
        {
            float red=0,grey=0,black=0,flash=0,cutoff=22000,ringing=0;
            if(sceneTask?.id=="N3_WOUND"&&lines!=null&&(lineIndex>0||spoken&&lines[0].id=="m2v3_wound_04"&&lineClock>=BurstHit))
            {
                if(!woundHit){woundHit=true;rig.Shake(.35f,.6f);}
                red=.45f;cutoff=900;
            }
            if(sceneTask?.id=="N3_WOUND"&&lines!=null&&spoken&&lines[lineIndex].id=="m2v3_wound_04")flash=Mathf.Clamp01(1-Mathf.Abs(lineClock-BurstHit)*3);
            if(State.Done("N3_WOUND")&&!State.Done("N3_CRAWL")){red=Mathf.Lerp(.35f,.15f,Mathf.Clamp01(State.taskProgress/8));cutoff=900;} // Crawling: blurred red, muffled.
            if(sceneTask?.id=="N3_CRAWL"){red=.12f;cutoff=1800;}
            if(Bleeding&&sceneTask==null)red=Mathf.Max(red,.04f+.05f*Mathf.Max(0,Mathf.Sin(Time.time*7.5f)));   // bleeding: a faint red pulse
            if(sceneTask?.id=="N3_00")black=AttackClock<0?Mathf.Clamp01((sceneClock-(Handover-HandoverFade))/HandoverFade):AttackClock<1?1:Mathf.Clamp01(1-(AttackClock-1)/.5f); // The watch handed over, Nam lies down.
            if(sceneTask?.id=="N3_09")
            {
                float t=sceneClock;
                flash=t<BlastAt-.35f?0:t<BlastAt+.15f?(t-(BlastAt-.35f))/.5f:t<BlastAt+1.2f?1:Mathf.Clamp01(1-(t-BlastAt-1.2f)/1.4f);   // white as it goes up, coming back slowly
                if(t>=BlastAt)
                {
                    float after=t-BlastAt;
                    cutoff=after<.3f?22000:after<1?10:Mathf.Lerp(500,22000,Mathf.Clamp01((after-1)/9f)*Mathf.Clamp01((after-1)/9f)); // Deaf, then the ringing fades.
                    ringing=after<10?Mathf.Lerp(.28f,0,after/10f):0;grey=after<10?Mathf.Lerp(.5f,0,after/10f):0;
                }
                if(t>=FadeAt)black=Mathf.Clamp01((t-FadeAt)/8f);
            }
            EffectRed=red;EffectGrey=grey;EffectBlack=black;EffectFlash=flash;ListenerCutoff=cutoff;
            ApplyHearing(cutoff,ringing);
            RefreshRadioProp();
        }
        void ApplyHearing(float cutoff,float ringing)
        {
            var listener=muffle!=null?muffle.GetComponent<AudioListener>():FindFirstObjectByType<AudioListener>();
            if(listener==null)return;
            if(cutoff<22000&&muffle==null)muffle=listener.gameObject.AddComponent<AudioLowPassFilter>();
            if(muffle!=null){muffle.enabled=cutoff<22000;muffle.cutoffFrequency=cutoff;}
            if(ringing>0&&ring==null)
            {
                ring=gameObject.AddComponent<AudioSource>();ring.bypassListenerEffects=true;ring.loop=true;ring.spatialBlend=0;
                if(Layout?.tinnitusClip!=null)ring.clip=Layout.tinnitusClip;
                else{const int rate=44100;var tone=new float[rate*2];for(int i=0;i<tone.Length;i++)tone[i]=Mathf.Sin(2*Mathf.PI*3700*i/rate)*.5f;ring.clip=AudioClip.Create("Tinnitus tone",tone.Length,1,rate,false);ring.clip.SetData(tone,0);}
                ring.Play();
            }
            if(ring!=null){ring.volume=ringing;if(ringing<=0){ring.Stop();Destroy(ring);ring=null;}}
        }
        void ResetEffects(){EffectRed=EffectGrey=EffectBlack=EffectFlash=0;ListenerCutoff=22000;ApplyHearing(22000,0);if(muffle!=null){Destroy(muffle);muffle=null;}}
        void RefreshRadioProp()
        {
            // Hùng puts the radio in Nam's lap before he goes; it stays with Nam to the end.
            bool carried=State.radioTransferred||AfterLine("N3_07","m2v3_farewell_09");
            if(!carried){if(radioProp!=null)radioProp.SetActive(false);return;}
            if(radioProp==null)
            {
                if(Layout?.radioPropArt!=null)radioProp=Instantiate(Layout.radioPropArt);
                else{radioProp=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(radioProp.GetComponent<Collider>());radioProp.transform.localScale=new Vector3(.28f,.18f,.1f);var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(.17f,.2f,.13f));radioProp.GetComponent<Renderer>().SetPropertyBlock(block);}
                radioProp.name="Hùng's radio — art slot";radioProp.transform.SetParent(Mission.Player,false);radioProp.transform.localPosition=new Vector3(0,1.05f,.24f);
            }
            radioProp.SetActive(true);
        }
        void DrawEffects()
        {
            void Fill(Color c){if(c.a<=0)return;var tint=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(0,0,1600,900),Texture2D.whiteTexture);GUI.color=tint;}
            Fill(new Color(.55f,0,0,EffectRed));Fill(new Color(.5f,.5f,.5f,EffectGrey));Fill(new Color(1,1,1,EffectFlash));Fill(new Color(0,0,0,EffectBlack));
        }

        /// <summary>Scene framing for Nhịp 3 and 5. Returns false to keep the default framing.</summary>
        bool FinaleCamera(out Vector3 eye,out Vector3 focus)
        {
            eye=focus=default;var nam=Mission.Player.position;stagedFov=null;
            if(HandingOver&&roles.ContainsKey("Anh Lực")){HandoverShot(out eye,out focus);return true;}
            if(sceneTask.id=="N3_07"&&lineIndex==0&&tank!=null){BoatShot(nam,out eye,out focus);return true;}
            if(WreckOpening&&sceneClock<WreckEnd&&wreckPlaced){WreckShot(nam,out eye,out focus);return true;}
            if(sceneTask.id=="N3_WOUND"&&!woundHit&&tank!=null){BreachShot(nam,out eye,out focus);return true;}
            if(sceneTask.id=="N3_WOUND"&&woundHit)
            {
                // Face down among the rubble, looking toward the tank.
                var at=tank!=null?tank.transform.position:sites["tankBreach"].position;var dir=Vector3.ProjectOnPlane(at-nam,Vector3.up).normalized;
                eye=nam+Vector3.up*.35f-dir*.9f+Vector3.Cross(Vector3.up,dir)*.3f;focus=at+Vector3.up*1.4f;return true;
            }
            if(sceneTask.id=="N3_07"&&lineIndex==0)
            {
                // No tank to film: wide on the boat pinned mid-river. Later lines are the talk's own shots (TalkCamera).
                var mid=Vector3.Lerp(sites["tankDock"].position,water["boatMid"],.5f);var back=Vector3.ProjectOnPlane(nam-mid,Vector3.up).normalized;
                eye=nam+back*3+Vector3.up*2.6f;focus=mid+Vector3.up;return true;
            }
            if(sceneTask.id=="N3_09")
            {
                var at=tank!=null?tank.transform.position:sites["tankDock"].position;var dir=Vector3.ProjectOnPlane(at-nam,Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,dir);
                var hungAt=roles["Hùng"].transform.position;
                if(sceneClock<RiseEnd){eye=nam-dir*2.2f+Vector3.up*1.7f+side*.6f;focus=Vector3.Lerp(hungAt,at,.3f)+Vector3.up;return true;}
                if(sceneClock<PlantEnd)
                {
                    // Close at the tracks: his hands setting the charge.
                    var toTank=Vector3.ProjectOnPlane(at-ChargePoint,Vector3.up).normalized;var across=Vector3.Cross(Vector3.up,toTank);if(Vector3.Dot(nam-ChargePoint,across)<0)across=-across;
                    eye=ChargePoint+across*2.1f-toTank*1.1f+Vector3.up*.85f;focus=ChargePoint+toTank*.8f+Vector3.up*.6f;stagedFov=46;return true;
                }
                if(sceneClock<BlastAt)
                {
                    // From Nam, on a long lens: Hùng steps back and turns to look at him.
                    var toHung=Vector3.ProjectOnPlane(hungAt-nam,Vector3.up).normalized;
                    eye=nam+Vector3.up*1.3f+toHung*1.2f+Vector3.Cross(Vector3.up,toHung)*.3f;focus=hungAt+Vector3.up*1.5f;
                    stagedFov=Mathf.Lerp(28,16,Mathf.SmoothStep(0,1,(sceneClock-PlantEnd)/(BlastAt-PlantEnd)));return true;
                }
                if(sceneClock<FireOrderAt){eye=nam+Vector3.up*.6f-dir*1.2f+side;focus=Vector3.Lerp(water["boatMid"],at,.5f)+Vector3.up*.8f;return true;}
                if(CounterAttackCamera(sceneClock,out eye,out focus))return true;
                var road=militia.Count>0&&militia[0]!=null?militia[0].position:at;
                eye=nam-dir*4+Vector3.up*3+side*2;focus=Vector3.Lerp(nam,road,.5f)+Vector3.up;return true;
            }
            return false;
        }
        /// <summary>The tank comes in: tracked low from beside the road as it drives; stopped, close on its machine gun as it
        /// swings onto Nam; then, as Anh Lực shouts, from above at 45 degrees on Nam as the burst walks in and hits him.</summary>
        void BreachShot(Vector3 nam,out Vector3 eye,out Vector3 focus)
        {
            var leg=BreachApproach();var at=tank.transform.position;var stop=leg[leg.Length-1];
            var heading=Vector3.ProjectOnPlane(stop-leg[0],Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,heading);
            if(Vector3.Dot(nam-stop,side)<0)side=-side;   // the road side Nam is on
            var tracking=side*7.5f+heading*2.5f+Vector3.up*1.1f;
            focus=at+Vector3.up*1.5f;
            if(sceneClock<BreachDrive){eye=at+tracking;return;}
            if(sceneClock<BreachLineAt)
            {
                // Close on the .50 on the turret as it swings onto Nam, pushing in.
                var mg=mgPart!=null?mgPart:mgMount!=null?mgMount:turret;var gunAt=mg!=null?mg.position:at+Vector3.up*2.4f;
                var aim=Vector3.ProjectOnPlane(tank.transform.rotation*Quaternion.AngleAxis(turretYaw+mgYaw,Vector3.up)*Vector3.forward,Vector3.up).normalized;
                eye=gunAt-aim*1.1f+Vector3.Cross(Vector3.up,aim)*.55f+Vector3.up*.3f;focus=gunAt+aim*2.2f;
                stagedFov=Mathf.Lerp(50,24,Mathf.SmoothStep(0,1,(sceneClock-BreachDrive)/BreachPan));return;
            }
            // From above at 45 degrees, down on Nam: the burst walks in round him and hits him.
            var dir=Vector3.ProjectOnPlane(at-nam,Vector3.up).normalized;var off=(-dir*.75f+Vector3.Cross(Vector3.up,dir)*.66f).normalized;
            eye=nam+off*3.6f+Vector3.up*3.6f;focus=nam+Vector3.up*.7f;
        }
        /// <summary>Field of view a staged shot asks for (a long lens on the boat); null keeps the scene default.</summary>
        float? stagedFov;
        /// <summary>N3_07 opens on the tank: low by the dock road as it rolls out and stands; rising to the gun and following it
        /// as the turret swings round; round along the barrel to stop in front of its muzzle; then a pan to the last boat and
        /// in close on it, the villagers aboard, while the machine gun fires and chú Bảy shouts.</summary>
        void BoatShot(Vector3 nam,out Vector3 eye,out Vector3 focus)
        {
            var at=tank.transform.position;var dock=sites["tankDock"].position;var boat=lastBoat!=null?lastBoat.transform.position:water["boatMid"];
            var road=Vector3.ProjectOnPlane(dock-sites["tankHide"].position,Vector3.up);road=road.sqrMagnitude>.01f?road.normalized:tank.transform.forward;
            var side=Vector3.Cross(Vector3.up,road);if(Vector3.Dot(nam-dock,side)<0)side=-side;
            var low=dock+side*7+road*5+Vector3.up*.9f;var hull=at+Vector3.up*1.3f;
            var gun=Vector3.ProjectOnPlane(tank.transform.rotation*Quaternion.AngleAxis(turretYaw,Vector3.up)*Vector3.forward,Vector3.up).normalized;
            var across=Vector3.Cross(Vector3.up,gun);if(Vector3.Dot(across,side)<0)across=-across;   // round the barrel on the camera's side
            var pivot=turret!=null?turret.position:at+Vector3.up*2.1f;var muzzle=pivot+gun*4.2f;
            // Along the barrel: behind it (167 degrees off its line) round to just off its front (20 degrees).
            Vector3 Round(float degrees,float radius,float height){float r=degrees*Mathf.Deg2Rad;return pivot+(gun*Mathf.Cos(r)+across*Mathf.Sin(r))*radius+Vector3.up*height;}
            var behind=Round(167,3.3f,1);var front=Round(20,5.8f,.35f);
            float t=sceneClock,riseEnd=EmergeEnd+TurretRise;
            if(t<EmergeEnd){eye=low;focus=hull;return;}
            if(t<riseEnd){float u=Mathf.SmoothStep(0,1,(t-EmergeEnd)/TurretRise);eye=Vector3.Lerp(low,behind,u);focus=Vector3.Lerp(hull,muzzle,u);return;}
            if(t<FrontAt)
            {
                float u=Mathf.SmoothStep(0,1,(t-riseEnd)/(FrontAt-riseEnd));
                eye=Round(Mathf.Lerp(167,20,u),Mathf.Lerp(3.3f,5.8f,u),Mathf.Lerp(1,.35f,u));focus=muzzle;stagedFov=Mathf.Lerp(58,40,u);return;
            }
            if(t<WhipAt){eye=front;focus=muzzle;stagedFov=40;return;}   // held in front of the muzzle
            // Then round to the boat and in close on it, the pan leading the move.
            float w=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-WhipAt)/BoatMove));
            var toBoat=Vector3.ProjectOnPlane(boat-front,Vector3.up);toBoat=toBoat.sqrMagnitude>.01f?toBoat.normalized:gun;
            var near=boat-toBoat*4.5f+Vector3.Cross(Vector3.up,toBoat)*1.2f+Vector3.up*1.6f;
            eye=Vector3.Lerp(front,near,w);focus=Vector3.Lerp(muzzle,boat+Vector3.up*.8f,Mathf.SmoothStep(0,1,Mathf.Clamp01(w*1.6f)));
            stagedFov=Mathf.Lerp(40,34,w)-Mathf.Clamp01((t-WhipAt-BoatMove)/4)*4;
        }
        AnimationClip NamPoseClip()
        {
            if(sceneTask==null)return null;
            if(HandoverWalking)return ordinaryWalk;   // walking up to hand over the watch
            if(Layout==null)return null;
            if(sceneTask.id=="DAWN")return Layout.namMournClip;
            if(sceneTask.id=="N3_WOUND"&&woundHit)return Layout.namProneClip;
            if(sceneTask.id=="N3_09"&&sceneClock>=BlastAt)return sceneClock<25?Layout.namLieBackClip:Layout.namSitRadioClip;
            return null;
        }
    }
}
