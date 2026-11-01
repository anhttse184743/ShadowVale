using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The chapter's world over time: Hùng's walk, the child beside his mother, daylight, the patrols and their groups, and the tank.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        /// <summary>Hùng's stand and walk: wounded on Day 1 / Night 1; on Day 3 an ordinary walk, the rifle carry of his Map 1
        /// controller belongs to the fighting (Day 2 / Night 2 and Night 3 keep it).</summary>
        void HungLocomotion()
        {
            var calm=State.phase==4?namCalmIdle:null;
            roles["Hùng"].SetLocomotion(State.phase<=1?DayClip("InjuredIdle"):calm,State.phase<=1?HungDayWalk:calm!=null?ordinaryWalk:null,State.phase<=1?HungDayWalkSpeed:0);   // Day 1 / Night 1: his arm is hurt, not his leg
        }
        void PlaceChildWithMother()
        {
            // Keep the authored lost-child/rescue segment in Night 3; normal village life stays by Lành.
            if(State.phase<=1||State.phase==5&&!State.Done("N3_03"))return;
            var mother=roles["Chị Lành"].transform;
            roles["Bé Tí"].StopAt(BesideMother(mother.position));
            roles["Bé Tí"].SetSitting(false);
        }
        /// <summary>0 in the dark of Night 3, rising from the farewell (the sky greys), through the cover fire, to dawn as the
        /// tank goes up and the counter-attack comes in (decided 26/10/2026: the battle runs from night into first light).</summary>
        public float FirstLight
        {
            get
            {
                if(State.phase!=5)return 0;
                if(State.Done("N3_09"))return .9f;
                if(sceneTask?.id=="N3_09")return Mathf.Lerp(.6f,.9f,Mathf.Clamp01(sceneClock/FadeAt));
                if(State.Done("N3_08"))return .6f;
                if(State.activeTask=="N3_08")return Mathf.Lerp(.35f,.6f,Mathf.Clamp01(State.coverClock/CoverSlowest));
                if(State.Done("N3_07")||sceneTask?.id=="N3_07")return sceneTask?.id=="N3_07"?Mathf.Lerp(.15f,.35f,Mathf.Clamp01(sceneClock/60)):.35f;
                return 0;
            }
        }
        void TickLight(float dt)
        {
            bool night=State.phase==1||State.phase==3||State.phase==5;bool afternoon=State.phase==0&&State.Available(Map02ChapterDefinition.Load().Find("D1_06"));
            // Day 1 / Night 1 the family lamp is the small one on the supper mat: it lights the tray and little else.
            foreach(var lamp in lamps){bool supperLamp=lamp.name.EndsWith("family")&&State.phase<=1;lamp.range=supperLamp?3.5f:State.phase<=1?6:10;if(supperLamp)lamp.transform.position=dayLampFlame!=null?dayLampFlame.position+Vector3.up*.1f:PorchMeal+Vector3.up*.3f;lamp.intensity=Mathf.Lerp(lamp.intensity,night?(State.phase<=1?(Layout!=null?Layout.dayOneOilLampIntensity:.65f):2.5f):0,Mathf.Clamp01(dt));}
            sweepLight.enabled=!NightPatrolStopped;
            if(phaseApplied!=State.phase){phaseApplied=State.phase;}
            if(TickNaturalLighting(dt))return;
            bool golden=State.phase==4; // Day 3: the warmest afternoon of the chapter, against Night 3.
            // Night 3 is fought into first light: the sky greys from the farewell and the tank goes up at dawn (FirstLight).
            float dawn=FirstLight,nightSun=Layout!=null?Layout.nightSunIntensity:.08f,nightSky=Layout!=null?Layout.nightSkyExposure:.08f;
            if(sun!=null){sun.intensity=Mathf.Lerp(sun.intensity,night?Mathf.Lerp(nightSun,.4f,dawn):State.phase==6?.45f:golden?sunIntensity*.9f:afternoon?sunIntensity*.65f:sunIntensity,dt*.6f);sun.color=Color.Lerp(sun.color,night?Color.Lerp(new Color(.5f,.63f,1),new Color(1,.66f,.5f),dawn):golden?new Color(1,.74f,.46f):afternoon?new Color(1,.67f,.36f):new Color(1,.9f,.75f),dt*.6f);sun.transform.rotation=Quaternion.Slerp(sun.transform.rotation,night?Quaternion.Slerp(Quaternion.Euler(25,190,0),Quaternion.Euler(7,95,0),dawn):golden?Quaternion.Euler(14,250,0):afternoon?Quaternion.Euler(18,220,0):sunRotation,dt*.6f);}
            if(chapterSky!=null&&chapterSky.HasProperty("_Exposure"))chapterSky.SetFloat("_Exposure",Mathf.Lerp(chapterSky.GetFloat("_Exposure"),night?Mathf.Lerp(nightSky,.75f,dawn):1f,dt));
            RenderSettings.ambientLight=Color.Lerp(RenderSettings.ambientLight,night?Color.Lerp(new Color(.12f,.15f,.23f),new Color(.42f,.38f,.43f),dawn):golden?Color.Lerp(ambientBefore,new Color(.62f,.48f,.34f),.45f):ambientBefore,dt*.6f);
        }
        void TickPatrol(float dt)
        {
            if(State.phase!=3||State.Done("N2_05"))return;
            if(NightPatrolStopped){StopNightPatrolAtStore();return;}
            float period=Mathf.Max(10,Layout!=null?Layout.patrolSweepSeconds:15);
            bool focusing=State.patrolFocusSeconds>0;
            if(focusing)
            {
                State.patrolFocusSeconds=Mathf.Max(0,State.patrolFocusSeconds-dt);
                if(State.patrolFocusSeconds<=0&&NightPatrolScriptedApproach)
                {
                    if(!nightWarningHidden&&!player.IsProne){BeginNightPatrolFailure();return;}
                    nightWarningHidden=false;
                }
                else if(player.IsProne)nightWarningHidden=true;
            }
            else
            {
                float elapsed=State.patrolScanSeconds+dt*NightPatrolScanRate;
                State.nightPatrolCycle=(State.nightPatrolCycle+Mathf.FloorToInt(elapsed/period))%1000000;
                State.patrolScanSeconds=Mathf.Repeat(elapsed,period);
            }
            float phase=State.patrolScanSeconds;
            // A sweep can notice Nam only with line of sight and without cover; one warning per pass.
            var origin=NightPatrolPosition+Vector3.up*1.7f;var delta=Mission.Player.position+Vector3.up-origin;
            sweepLight.transform.position=origin;
            var sweep=NightPatrolSweepDirection(0);
            if(focusing&&State.patrolFocusDirection.sqrMagnitude>.001f)sweep=NightPatrolFocusDirection(0);
            sweepLight.transform.rotation=Quaternion.LookRotation(sweep);
            UpdateNightPatrolPresentation(origin,sweep);
            if(patrolLoadGrace>0){patrolLoadGrace=Mathf.Max(0,patrolLoadGrace-dt);return;}
            if(!State.Done("N2_02"))return;
            bool exposed=NightPatrolExposure(origin,sweep,out float angle,out float distance);
            TickNightRadio(dt,angle,angle<previousNightAngle,distance);previousNightAngle=angle;
            if(focusing)return;
            if(exposed)
            {
                State.patrolWarnings=Mathf.Min(2,State.patrolWarnings+1);
                if(State.patrolWarnings>=2)BeginNightPatrolFailure();
                else
                {
                    nightWarningHidden=false;State.patrolFocusSeconds=NightPatrolWarningSeconds;
                    State.patrolFocusDirection=Mission.Player.position; // Fixed world point, shared by all guards.
                    ShowNightPatrolSuspicion();
                }
            }
        }
        void EnsureGroup(string id,int count,bool hostile=true,string site=null,int minimum=0)
        {
            if(State.combatGroups.Contains(id)||Time.time<spawnRetry)return;
            var task=Map02ChapterDefinition.Load().Find(id);var center=sites[site??(task!=null?task.site:id=="scout_patrol"?"scout":"lane1")].position;var locations=new List<Vector3>();
            for(int i=0;i<36&&locations.Count<count;i++)
            {
                var p=center+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*(22+i%3*4);
                if(!NavMesh.SamplePosition(p,out var h,5,NavMesh.AllAreas)||Vector3.Distance(h.position,center)<18||Vector3.Distance(h.position,Mission.Player.position)<18)continue;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(h.position,center,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                var view=rig.GetComponent<Camera>().WorldToViewportPoint(h.position+Vector3.up);if(view.z>0&&view.x>0&&view.x<1&&view.y>0&&view.y<1)continue;
                if(locations.Any(q=>Vector3.Distance(q,h.position)<4))continue;locations.Add(h.position);
            }
            if(locations.Count<(minimum>0?minimum:count)){spawnRetry=Time.time+2;Mission.Say("Địch đang cơ động ngoài tuyến. Giữ vị trí có vật che.");return;}
            for(int i=0;i<locations.Count;i++)SpawnEnemy(id+"_"+i,id,locations[i],100,hostile);
            State.combatGroups.Add(id);
        }
        void SpawnEnemy(string id,string group,Vector3 position,float hp,bool hostile=true,bool looted=false)
        {
            var go=new GameObject("Map2Enemy_"+id);go.transform.SetPositionAndRotation(position,Quaternion.identity);go.transform.SetParent(root.transform);
            var unit=go.AddComponent<Map02ChapterEnemy>();unit.Initialize(this,id,group,hp,hostile);unit.Looted=looted;unit.HoldPosition=group==Escort;Enemies.Add(unit);
        }
        void CreateTank()
        {
            tank=new GameObject("Tank art slot — scripted boss, no rifle damage");tank.transform.SetParent(root.transform);tank.transform.position=sites["tankBreach"].position;
            // Temporary prop volume; the art team replaces the Art child, never its gameplay owner.
            var art=Layout!=null&&Layout.tankArt!=null?Instantiate(Layout.tankArt):GameObject.CreatePrimitive(PrimitiveType.Cube);art.name="Art placeholder — tank hull";art.transform.SetParent(tank.transform,false);
            if(Layout!=null&&Layout.tankArt!=null){art.name="Art — M41 tank";art.transform.localPosition=Vector3.zero;BindTankParts(art.transform);} // Modelled tank: sits on the ground, parts move.
            else{art.transform.localPosition=Vector3.up;art.transform.localScale=new Vector3(2.8f,2,5);}
            var smoke=new GameObject("Destroyed smoke");smoke.transform.SetParent(tank.transform,false);smoke.transform.localPosition=Vector3.up*2;
            tankSmoke=smoke.AddComponent<ParticleSystem>();var main=tankSmoke.main;main.playOnAwake=false;main.startLifetime=3;main.startSpeed=1.5f;main.startSize=1.8f;main.startColor=new Color(.15f,.15f,.15f,.6f);tankSmoke.Stop();
            tankSmoke.GetComponent<ParticleSystemRenderer>().sharedMaterial=Map01Fx.Lit;
            main.maxParticles=80;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=tankSmoke.emission;emission.rateOverTime=12;
            var shape=tankSmoke.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.35f;shape.rotation=new Vector3(-90,0,0);
            var fade=tankSmoke.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});fade.color=gradient;
        }
    }
}
