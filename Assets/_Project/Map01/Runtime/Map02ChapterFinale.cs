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
            if(task.id=="N3_WOUND"){rig.Shake(.05f,2.5f);if(Layout?.tankTracksClip!=null)AudioSource.PlayClipAtPoint(Layout.tankTracksClip,sites["tankBreach"].position,.9f);}
            if(task.id=="N3_07")
            {
                // A cut, as the script allows: Hùng is already beside Nam, between him and the tank's fire.
                var nam=Mission.Player.position;var toward=Vector3.ProjectOnPlane(sites["tankDock"].position-nam,Vector3.up).normalized;
                var spot=nam+toward*1.3f;if(NavMesh.SamplePosition(spot,out var hit,2,NavMesh.AllAreas))spot=hit.position;roles["Hùng"].StopAt(spot);
                if(Layout?.tankTracksClip!=null)AudioSource.PlayClipAtPoint(Layout.tankTracksClip,sites["tankHide"].position,.9f);
            }
            if(task.id=="N3_09")
            {
                // He comes up out of the ditch a few metres short of the tracks.
                hungRiseFrom=Along(HungCanal,Mathf.Max(0,Length(HungCanal)-4));
                sacrificePath=new[]{hungRiseFrom,ChargePoint};sacrificeTravel=RiseEnd;
            }
        }
        void SampleSacrifice()
        {
            var hung=roles["Hùng"];var t=sceneClock;var tracks=tank.transform.position;
            if(t<RiseEnd)hung.SceneMove(Vector3.Lerp(hungRiseFrom,ChargePoint,t/RiseEnd),tracks,2.6f);
            else if(t<PlantEnd)hung.SceneMove(ChargePoint,tracks,0);
            else if(t<ShotAt)hung.SceneMove(Vector3.Lerp(ChargePoint,RetreatPoint,(t-PlantEnd)/(ShotAt-PlantEnd)),RetreatPoint,2.6f);
            else if(!shotFired)
            {
                // A muzzle flash from behind the tank; he falls a few metres from it.
                shotFired=true;hung.StopAt(RetreatPoint);if(Layout?.hungFallClip==null)hung.SetDead(true);
                RearFlash();var sound=Mission.Player.GetComponent<CombatAudio>();if(sound!=null&&sound.Gunshot.IsValid){var clip=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(clip.clip,RetreatPoint,.7f*clip.EffectiveGain,1,0,.85f,clip.startTime,clip.duration);}
            }
            if(t>=BlastAt&&!blasted){blasted=true;rig.Shake(.6f,1.2f);}
            var clipNow=t<ShotAt?Layout?.hungPlantRetreatClip:Layout?.hungFallClip;
            if(t>=RiseEnd&&clipNow!=null&&hung.visual!=null)
            {var animator=hung.visual.GetComponentInChildren<Animator>();if(animator!=null){hungActionPose??=new Map01CinematicPose(animator,null,false);hungActionPose.Sample(clipNow,t<ShotAt?t-RiseEnd:t-ShotAt,false);}}
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
            if(t>=MilitiaAt&&militia.Count==0)SpawnMilitia();
            for(int i=0;i<militia.Count;i++)
            {
                if(militia[i]==null)continue;float d=Mathf.Max(0,(t-MilitiaAt-i*.4f)*4.2f);
                var side=Vector3.Cross(Vector3.up,(Along(militiaRoad,d+1)-Along(militiaRoad,d)).normalized)*((i%3-1)*1.6f);
                militia[i].position=Along(militiaRoad,d)+side;var ahead=Along(militiaRoad,d+1)-Along(militiaRoad,d);if(ahead.sqrMagnitude>.01f)militia[i].rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(ahead,Vector3.up));
            }
            if(t>=ThanhAt&&!thanhRunning){thanhRunning=true;var thanh=roles["Anh Thành"];thanh.StopAt(BehindCamera());thanh.SceneDriven=true;thanh.GoTo(sites["tankDock"].position,3.6f);}
        }
        void SpawnMilitia()
        {
            // Silhouettes against the gun flashes: the militia model again, no new soldiers' model.
            militiaRoad=Leg("militiaEntry","tankDock");
            var art=Layout?.militiaArt!=null?Layout.militiaArt:Layout?.actors?.FirstOrDefault(a=>a.role=="Anh Lực")?.prefab;
            for(int i=0;i<7;i++)
            {
                GameObject body;
                if(art!=null)body=Instantiate(art);
                else{body=GameObject.CreatePrimitive(PrimitiveType.Capsule);Destroy(body.GetComponent<Collider>());var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(.05f,.05f,.05f));body.GetComponent<Renderer>().SetPropertyBlock(block);body.transform.localScale=new Vector3(.6f,.9f,.6f);}
                body.name="Counter-attack silhouette "+i;body.transform.SetParent(root.transform);militia.Add(body.transform);
            }
        }
        void ClearFinale()
        {
            foreach(var m in militia)if(m!=null)Destroy(m.gameObject);militia.Clear();
            if(impactLight!=null)impactLight.intensity=0;if(rearFlash!=null)rearFlash.enabled=false;
            if(thanhRunning){roles["Anh Thành"].StopAt(sites["tankDock"].position);thanhRunning=false;}
        }

        /// <summary>Screen tints, flash and what Nam hears, from the story state and the scene clock.</summary>
        void TickEffects()
        {
            float red=0,grey=0,black=0,flash=0,cutoff=22000,ringing=0;
            if(sceneTask?.id=="N3_WOUND"&&lines!=null&&(lineIndex>0||lines[0].id=="m2v3_wound_04"&&lineClock>=1))
            {
                if(!woundHit){woundHit=true;rig.Shake(.35f,.6f);}
                red=.45f;cutoff=900;
            }
            if(sceneTask?.id=="N3_WOUND"&&lines!=null&&lines[lineIndex].id=="m2v3_wound_04")flash=Mathf.Clamp01(1-Mathf.Abs(lineClock-1)*3);
            if(State.Done("N3_WOUND")&&!State.Done("N3_CRAWL")){red=Mathf.Lerp(.35f,.15f,Mathf.Clamp01(State.taskProgress/8));cutoff=900;} // Crawling: blurred red, muffled.
            if(sceneTask?.id=="N3_CRAWL"){red=.12f;cutoff=1800;}
            if(sceneTask?.id=="N3_00")black=sceneClock<1?1:Mathf.Clamp01(1-(sceneClock-1)/.5f); // Nam has just lain down.
            if(sceneTask?.id=="N3_09")
            {
                float t=sceneClock;
                flash=Mathf.Clamp01(1-Mathf.Abs(t-BlastAt)*3);
                if(t>=BlastAt)
                {
                    float after=t-BlastAt;
                    cutoff=after<1?10:Mathf.Lerp(500,22000,Mathf.Clamp01((after-1)/9f)*Mathf.Clamp01((after-1)/9f)); // Deaf, then the ringing fades.
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
            eye=focus=default;var nam=Mission.Player.position;
            if(sceneTask.id=="N3_WOUND"&&woundHit)
            {
                // Face down among the rubble, looking toward the tank.
                var at=tank!=null?tank.transform.position:sites["tankBreach"].position;var dir=Vector3.ProjectOnPlane(at-nam,Vector3.up).normalized;
                eye=nam+Vector3.up*.35f-dir*.9f+Vector3.Cross(Vector3.up,dir)*.3f;focus=at+Vector3.up*1.4f;return true;
            }
            if(sceneTask.id=="N3_07")
            {
                if(lineIndex==0)
                {
                    // Wide: the boat pinned mid-river and the tank at the head of the road.
                    var mid=Vector3.Lerp(sites["tankDock"].position,water["boatMid"],.5f);var back=Vector3.ProjectOnPlane(nam-mid,Vector3.up).normalized;
                    eye=nam+back*3+Vector3.up*2.6f;focus=mid+Vector3.up;return true;
                }
                // Behind Nam's shoulder; Hùng between him and the fire, read as a silhouette.
                var hung=roles["Hùng"].transform.position;var d=Vector3.ProjectOnPlane(hung-nam,Vector3.up).normalized;
                eye=nam-d*1.7f+Vector3.Cross(Vector3.up,d)*.5f+Vector3.up*1.55f;focus=hung+Vector3.up*1.3f;return true;
            }
            if(sceneTask.id=="N3_09")
            {
                var at=tank!=null?tank.transform.position:sites["tankDock"].position;var dir=Vector3.ProjectOnPlane(at-nam,Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,dir);
                if(sceneClock<BlastAt){eye=nam-dir*2.2f+Vector3.up*1.7f+side*.6f;focus=Vector3.Lerp(roles["Hùng"].transform.position,at,.3f)+Vector3.up;return true;}
                if(sceneClock<FireOrderAt){eye=nam+Vector3.up*.6f-dir*1.2f+side;focus=Vector3.Lerp(water["boatMid"],at,.5f)+Vector3.up*.8f;return true;}
                var road=militia.Count>0&&militia[0]!=null?militia[0].position:at;
                eye=nam-dir*4+Vector3.up*3+side*2;focus=Vector3.Lerp(nam,road,.5f)+Vector3.up;return true;
            }
            return false;
        }
        AnimationClip NamPoseClip()
        {
            if(Layout==null||sceneTask==null)return null;
            if(sceneTask.id=="DAWN")return Layout.namMournClip;
            if(sceneTask.id=="N3_WOUND"&&woundHit)return Layout.namProneClip;
            if(sceneTask.id=="N3_09"&&sceneClock>=BlastAt)return sceneClock<25?Layout.namLieBackClip:Layout.namSitRadioClip;
            return null;
        }
    }
}
