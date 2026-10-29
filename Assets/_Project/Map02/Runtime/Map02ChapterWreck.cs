using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Nhịp 4 (N3_06): the tank Nam escaped is loose in the village. Before the radio call the scene opens on it in the lanes,
    /// its gun firing into the houses: three shots, each a blast and a house left burning, thick smoke going up, the camera
    /// shaking, and fires out beyond the village edge too. From the crawl until it comes out on the dock road it roams the
    /// lanes and its shells keep landing in the village now and then. The fires burn on through Night 3 and smoke thinly at
    /// dawn. Everything here follows from the finished tasks, so a load shows the same.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        static readonly float[] WreckShots={1,2.6f,4.1f};
        const float WreckEnd=5.6f;
        static readonly string[] WreckHouses={"family","store","command","radio","gate","rest"};
        Vector3[] wreckTargets;Vector3 wreckSpot;bool wreckPlaced;readonly bool[] wreckFired=new bool[3];float wreckClock=4;
        readonly List<(Light light,ParticleSystem smoke,ParticleSystem flame)> wreckFires=new List<(Light,ParticleSystem,ParticleSystem)>();
        Light wreckFlash;float wreckFlashUntil;
        Vector3? shellAim;Vector3 nextShell;bool nextShellSet;
        /// <summary>Fires beyond the houses: the village edge and the fields the tank came through.</summary>
        Vector3[] wreckOuter=System.Array.Empty<Vector3>();
        readonly List<(Light light,ParticleSystem smoke,ParticleSystem flame)> outerFires=new List<(Light,ParticleSystem,ParticleSystem)>();
        /// <summary>N3_06's opening: the tank shelling the houses, before the radio call.</summary>
        bool WreckOpening=>sceneTask?.id=="N3_06"&&lines!=null&&lines.Length>0&&lines[0].id=="m2v3_coordinates_01"&&sceneClock<WreckEnd+.5f;
        public int WreckFiresBurning=>wreckFires.Count(f=>f.light!=null&&f.light.enabled);
        /// <summary>Where the tank stands in the village lanes, and the houses it shells.</summary>
        void PlaceWreck()
        {
            if(wreckPlaced)return;wreckPlaced=true;
            var leg=Leg("tankBreach","tankHide");wreckSpot=Along(leg,Length(leg)*.55f);
            var fallen=sites.ContainsKey("fallen")?sites["fallen"].position:Mission.Player.position;
            var houses=WreckHouses.Where(sites.ContainsKey).Select(h=>sites[h].position)
                .Where(p=>Flat(p,wreckSpot)>9&&Flat(p,wreckSpot)<45&&Flat(p,fallen)>12).OrderBy(p=>Flat(p,wreckSpot)).Take(3).ToList();
            for(int i=houses.Count;i<3;i++)houses.Add(wreckSpot+Quaternion.Euler(0,i*70-70,0)*Vector3.forward*(14+i*3));
            wreckTargets=houses.Select(p=>p+Vector3.up*1.5f).ToArray();
            var outer=new List<Vector3>();
            foreach(var id in new[]{"artyImpact1","artyImpact2","artyImpact3"})if(water.ContainsKey(id))outer.Add(GroundAt(water[id]));
            var breach=sites["tankBreach"].position;var outward=Vector3.ProjectOnPlane(breach-sites["lane3"].position,Vector3.up);outward=outward.sqrMagnitude>.01f?outward.normalized:Vector3.forward;
            var across=Vector3.Cross(Vector3.up,outward);outer.Add(GroundAt(breach+outward*14+across*6));outer.Add(GroundAt(breach+outward*22-across*9));
            wreckOuter=outer.Select(p=>p+Vector3.up*1.5f).ToArray();
        }
        static Vector3 GroundAt(Vector3 p)=>Physics.Raycast(p+Vector3.up*30,Vector3.down,out var hit,60,~0,QueryTriggerInteraction.Ignore)?hit.point:p;
        Vector3 WreckCenter=>(wreckTargets[0]+wreckTargets[1]+wreckTargets[2])/3;
        /// <summary>The house the gun is on: the next to be hit, or the last one hit.</summary>
        Vector3 WreckAim{get{for(int i=0;i<3;i++)if(!wreckFired[i])return wreckTargets[i];return wreckTargets[2];}}
        void TickWreck(float dt)
        {
            if(State.phase<5){foreach(var f in wreckFires)Burn(f,0);foreach(var f in outerFires)Burn(f,0);return;}
            if(sceneTask?.id=="N3_06"&&sceneClock<.05f)for(int i=0;i<3;i++)wreckFired[i]=false;
            bool opening=WreckOpening;
            if(opening||State.Done("N3_CRAWL"))PlaceWreck();
            if(!wreckPlaced)return;
            if(opening)for(int i=0;i<3;i++)if(!wreckFired[i]&&sceneClock>=WreckShots[i]){wreckFired[i]=true;Shell(wreckTargets[i],.32f);}
            int lit=State.phase>=6||State.Done("N3_06")||State.introduced.Contains("N3_06")?3:opening?wreckFired.Count(f=>f):0;
            for(int i=0;i<3;i++){if(i>=wreckFires.Count&&i<lit)wreckFires.Add(Fire(wreckTargets[i]));if(i<wreckFires.Count)Burn(wreckFires[i],i<lit?(State.phase>=6?.25f:1):0);}
            for(int i=0;i<wreckOuter.Length;i++){if(i>=outerFires.Count&&lit>=3)outerFires.Add(Thin(Fire(wreckOuter[i]),.55f));if(i<outerFires.Count)Burn(outerFires[i],lit>=3?(State.phase>=6?.25f:1):0);}   // and beyond the village
            if(wreckFlash!=null)wreckFlash.enabled=Time.time<wreckFlashUntil;
            // Loose in the lanes from the crawl to the dock road: its gun swings onto a house and fires now and then.
            bool loose=State.phase==5&&State.Done("N3_CRAWL")&&!State.Done("N3_07")&&sceneTask==null&&!Mission.Paused&&tank!=null&&tank.activeInHierarchy;
            if(!loose){if(sceneTask==null)shellAim=null;return;}
            if(!nextShellSet){nextShellSet=true;nextShell=wreckTargets[Random.Range(0,3)]+new Vector3(Random.Range(-4f,4f),0,Random.Range(-4f,4f));}
            shellAim=nextShell;wreckClock-=dt;if(wreckClock>0)return;
            Shell(nextShell,.14f);wreckClock=Random.Range(6f,10f);nextShellSet=false;
        }
        /// <summary>A shell into the village: flash at the gun when it is in sight, the blast, the shake.</summary>
        void Shell(Vector3 target,float shake)
        {
            var ground=Physics.Raycast(target+Vector3.up*6,Vector3.down,out var hit,14,~0,QueryTriggerInteraction.Ignore)?hit:default;
            var point=ground.collider!=null?ground.point:target-Vector3.up*1.5f;
            Map01Blast.Spawn(point,ground.collider!=null?ground.normal:Vector3.up,null,4);rig.Shake(shake,.6f);Fireball(point);
            if(tank==null||!tank.activeInHierarchy)return;
            if(wreckFlash==null)
            {
                var go=new GameObject("Tank gun flash");go.transform.SetParent(root.transform);wreckFlash=go.AddComponent<Light>();
                wreckFlash.type=LightType.Point;wreckFlash.range=16;wreckFlash.intensity=9;wreckFlash.color=new Color(1,.7f,.38f);wreckFlash.shadows=LightShadows.None;
            }
            var gun=Vector3.ProjectOnPlane(tank.transform.rotation*Quaternion.AngleAxis(turretYaw,Vector3.up)*Vector3.forward,Vector3.up).normalized;
            wreckFlash.transform.position=(turret!=null?turret.position:tank.transform.position+Vector3.up*2.1f)+gun*4.6f;wreckFlash.enabled=true;wreckFlashUntil=Time.time+.08f;
        }
        /// <summary>The shell's fireball and the black smoke it throws up.</summary>
        void Fireball(Vector3 at,float scale=1)
        {
            var ball=Map01Fx.System(root.transform,"Shell fireball",at+Vector3.up*.6f,Map01Fx.Glow,.5f,200);
            var bm=ball.main;bm.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.75f);bm.startSpeed=new ParticleSystem.MinMaxCurve(4*scale,9*scale);bm.startSize=new ParticleSystem.MinMaxCurve(1.8f*scale,4.5f*scale);
            bm.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.85f,.4f,1),new Color(1,.4f,.08f,.95f));var bs=ball.shape;bs.shapeType=ParticleSystemShapeType.Hemisphere;bs.radius=.8f;
            Map01Fx.Grow(ball,1,.3f);ball.Emit(Mathf.RoundToInt(70*scale));
            var puff=Map01Fx.System(root.transform,"Shell smoke",at+Vector3.up,Map01Fx.Lit,3,40);
            var pm=puff.main;pm.startLifetime=new ParticleSystem.MinMaxCurve(2.5f,4);pm.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,4);pm.startSize=new ParticleSystem.MinMaxCurve(3*scale,5.5f*scale);pm.startColor=new Color(.07f,.06f,.06f,.9f);
            var ps=puff.shape;ps.shapeType=ParticleSystemShapeType.Hemisphere;ps.radius=1.2f;Map01Fx.Grow(puff,1,2);puff.Emit(30);
            var flash=new GameObject("Shell fireball light");flash.transform.SetParent(root.transform);flash.transform.position=at+Vector3.up*1.5f;
            var light=flash.AddComponent<Light>();light.type=LightType.Point;light.range=26*scale;light.intensity=12;light.color=new Color(1,.6f,.25f);light.shadows=LightShadows.None;
            var debris=Map01Fx.System(root.transform,"Shell debris",at+Vector3.up*.5f,Map01Fx.Unlit,.5f,60);
            var dm=debris.main;dm.startLifetime=new ParticleSystem.MinMaxCurve(1,2.2f);dm.startSpeed=new ParticleSystem.MinMaxCurve(7*scale,14*scale);dm.startSize=new ParticleSystem.MinMaxCurve(.12f,.35f);dm.gravityModifier=1.4f;dm.startColor=new Color(.08f,.07f,.06f,1);
            var ds=debris.shape;ds.shapeType=ParticleSystemShapeType.Cone;ds.angle=35;ds.rotation=new Vector3(-90,0,0);debris.Emit(Mathf.RoundToInt(40*scale));
            Destroy(flash,.25f);Destroy(ball.gameObject,3);Destroy(puff.gameObject,6);Destroy(debris.gameObject,4);
            Smoulder(at,25*scale);
        }
        /// <summary>What a shell leaves burning where it lands, for a while.</summary>
        void Smoulder(Vector3 at,float seconds)
        {
            var flames=Map01Fx.System(root.transform,"Shell crater fire",GroundAt(at)+Vector3.up*.3f,Map01Fx.Glow,1,140);
            var fm=flames.main;fm.loop=true;fm.startLifetime=new ParticleSystem.MinMaxCurve(.5f,1.1f);fm.startSpeed=new ParticleSystem.MinMaxCurve(1,2.4f);fm.startSize=new ParticleSystem.MinMaxCurve(.8f,1.8f);fm.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.7f,.25f,.95f),new Color(1,.33f,.07f,.9f));
            var fe=flames.emission;fe.rateOverTime=55;var fs=flames.shape;fs.shapeType=ParticleSystemShapeType.Box;fs.scale=new Vector3(2.2f,.2f,2.2f);Map01Fx.Grow(flames,1,.3f);flames.Play();
            var smoke=Map01Fx.System(flames.transform,"Shell crater smoke",GroundAt(at)+Vector3.up*1.5f,Map01Fx.Lit,6,120);
            var sm=smoke.main;sm.loop=true;sm.startLifetime=new ParticleSystem.MinMaxCurve(5,8);sm.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,2.6f);sm.startSize=new ParticleSystem.MinMaxCurve(2.4f,4);sm.startColor=new Color(.08f,.075f,.07f,.7f);
            var se=smoke.emission;se.rateOverTime=9;Map01Fx.Grow(smoke,1,2.4f);Map01Fx.Wind(smoke,Map01Fx.Breeze*3);smoke.Play();
            var glow=new GameObject("Crater firelight").AddComponent<Light>();glow.transform.SetParent(flames.transform,false);glow.transform.localPosition=Vector3.up;glow.type=LightType.Point;glow.range=12;glow.intensity=4;glow.color=new Color(1,.5f,.2f);glow.shadows=LightShadows.None;
            Destroy(flames.gameObject,seconds);
        }
        /// <summary>A burning house: firelight, flames over the roof, embers and a thick column of smoke.</summary>
        (Light,ParticleSystem,ParticleSystem) Fire(Vector3 at)
        {
            var go=new GameObject("Burning house (tank)");go.transform.SetParent(root.transform);go.transform.position=at+Vector3.up*1.5f;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=28;light.color=new Color(1,.46f,.17f);light.shadows=LightShadows.None;
            // Smoke in two layers: a dense black core off the fire, a wide grey column above it drifting with the wind.
            var smoke=Map01Fx.System(root.transform,"House smoke (tank)",at+Vector3.up*2.5f,Map01Fx.Lit,9,420);
            var sm=smoke.main;sm.loop=true;sm.startLifetime=new ParticleSystem.MinMaxCurve(7,10);sm.startSpeed=new ParticleSystem.MinMaxCurve(2,3.2f);sm.startSize=new ParticleSystem.MinMaxCurve(3.5f,6);sm.startColor=new Color(.07f,.065f,.06f,.72f);
            var se=smoke.emission;se.rateOverTime=24;var ss=smoke.shape;ss.shapeType=ParticleSystemShapeType.Sphere;ss.radius=1.8f;
            Map01Fx.Grow(smoke,1,2.8f);Map01Fx.Wind(smoke,Map01Fx.Breeze*3);smoke.Play();
            var column=Map01Fx.System(smoke.transform,"House smoke column (tank)",at+Vector3.up*9,Map01Fx.Lit,12,220);
            var cm=column.main;cm.loop=true;cm.startLifetime=new ParticleSystem.MinMaxCurve(10,14);cm.startSpeed=new ParticleSystem.MinMaxCurve(2.8f,4);cm.startSize=new ParticleSystem.MinMaxCurve(6,10);cm.startColor=new Color(.24f,.23f,.22f,.38f);
            var ce=column.emission;ce.rateOverTime=8;var cs=column.shape;cs.shapeType=ParticleSystemShapeType.Sphere;cs.radius=2.5f;Map01Fx.Grow(column,1,2.2f);Map01Fx.Wind(column,Map01Fx.Breeze*5);column.Play();
            var flame=Map01Fx.System(root.transform,"House flames (tank)",at+Vector3.up*1.2f,Map01Fx.Glow,1,600);
            var fm=flame.main;fm.loop=true;fm.startLifetime=new ParticleSystem.MinMaxCurve(.7f,1.4f);fm.startSpeed=new ParticleSystem.MinMaxCurve(1.6f,3.8f);fm.startSize=new ParticleSystem.MinMaxCurve(1.6f,3.8f);
            fm.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.72f,.28f,1),new Color(1,.34f,.07f,.95f));
            var fe=flame.emission;fe.rateOverTime=170;var fs=flame.shape;fs.shapeType=ParticleSystemShapeType.Box;fs.scale=new Vector3(4.8f,2,4.8f);Map01Fx.Grow(flame,1,.2f);flame.Play();
            // A white-yellow heart to the fire, and burning patches round the foot of the house.
            var core=Map01Fx.System(flame.transform,"Fire core",at+Vector3.up*1.4f,Map01Fx.Glow,1,200);
            var km=core.main;km.loop=true;km.startLifetime=new ParticleSystem.MinMaxCurve(.4f,.8f);km.startSpeed=new ParticleSystem.MinMaxCurve(1,2.2f);km.startSize=new ParticleSystem.MinMaxCurve(.9f,1.8f);km.startColor=new Color(1,.93f,.62f,1);
            var ke=core.emission;ke.rateOverTime=70;var ks=core.shape;ks.shapeType=ParticleSystemShapeType.Box;ks.scale=new Vector3(3.2f,1.2f,3.2f);Map01Fx.Grow(core,1,.3f);core.Play();
            for(int i=0;i<3;i++)
            {
                var patch=Map01Fx.System(flame.transform,"Ground fire",GroundAt(at+Quaternion.Euler(0,i*120+30,0)*Vector3.forward*3.4f)+Vector3.up*.3f,Map01Fx.Glow,1,90);
                var pm=patch.main;pm.loop=true;pm.startLifetime=new ParticleSystem.MinMaxCurve(.5f,1);pm.startSpeed=new ParticleSystem.MinMaxCurve(.8f,1.8f);pm.startSize=new ParticleSystem.MinMaxCurve(.6f,1.3f);pm.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.7f,.25f,.95f),new Color(1,.35f,.08f,.9f));
                var pe=patch.emission;pe.rateOverTime=40;var ps=patch.shape;ps.shapeType=ParticleSystemShapeType.Box;ps.scale=new Vector3(1.4f,.2f,1.4f);Map01Fx.Grow(patch,1,.3f);patch.Play();
            }
            var embers=Map01Fx.System(flame.transform,"Embers",at+Vector3.up*2,Map01Fx.Glow,2,80);
            var em=embers.main;em.loop=true;em.startLifetime=new ParticleSystem.MinMaxCurve(1.5f,3);em.startSpeed=new ParticleSystem.MinMaxCurve(2.5f,6);em.startSize=new ParticleSystem.MinMaxCurve(.06f,.16f);em.startColor=new Color(1,.6f,.2f,1);em.gravityModifier=-.05f;
            var ee=embers.emission;ee.rateOverTime=45;var es=embers.shape;es.shapeType=ParticleSystemShapeType.Box;es.scale=new Vector3(4,1,4);embers.Play();
            return (light,smoke,flame);
        }
        /// <summary>A smaller fire (out in the fields): fewer flames and less of the upper column.</summary>
        (Light,ParticleSystem,ParticleSystem) Thin((Light light,ParticleSystem smoke,ParticleSystem flame) fire,float size)
        {
            foreach(var ps in fire.flame.GetComponentsInChildren<ParticleSystem>()){var e=ps.emission;e.rateOverTimeMultiplier*=size;}
            foreach(var ps in fire.smoke.GetComponentsInChildren<ParticleSystem>())if(ps!=fire.smoke){var e=ps.emission;e.rateOverTimeMultiplier*=size;}
            if(fire.light!=null)fire.light.range*=Mathf.Lerp(.7f,1,size);
            return fire;
        }
        /// <summary>1 burning, a fraction smoulders (dawn), 0 out.</summary>
        void Burn((Light light,ParticleSystem smoke,ParticleSystem flame) fire,float strength)
        {
            if(fire.light!=null){fire.light.enabled=strength>.5f;fire.light.intensity=7+Mathf.PerlinNoise(Time.time*5.5f,fire.light.GetInstanceID()*.01f)*4.5f;}
            if(fire.smoke!=null){var e=fire.smoke.emission;e.rateOverTime=24*strength;if(strength>0&&!fire.smoke.isPlaying)fire.smoke.Play();else if(strength<=0&&fire.smoke.isPlaying)fire.smoke.Stop();}
            if(fire.flame!=null){bool on=strength>.5f;if(on&&!fire.flame.isPlaying)fire.flame.Play();else if(!on&&fire.flame.isPlaying)fire.flame.Stop();}
        }
        /// <summary>The opening's framing: high and wide across the lanes, the tank and the houses it is shelling.</summary>
        void WreckShot(Vector3 nam,out Vector3 eye,out Vector3 focus)
        {
            var center=WreckCenter;var mid=Vector3.Lerp(wreckSpot,center,.5f);var axis=Vector3.ProjectOnPlane(center-wreckSpot,Vector3.up);
            axis=axis.sqrMagnitude>.01f?axis.normalized:Vector3.forward;var side=Vector3.Cross(Vector3.up,axis);if(Vector3.Dot(nam-mid,side)<0)side=-side;
            eye=mid+side*18+Vector3.up*8-axis*3;focus=mid+Vector3.up*2;stagedFov=50;
        }
    }
}
