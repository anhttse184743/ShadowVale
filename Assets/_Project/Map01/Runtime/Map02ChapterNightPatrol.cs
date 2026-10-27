using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public const string NightPatrolGroup="night2_patrol";
        public const float NightPatrolBulletDamage=24,NightPatrolHealth=60*NightPatrolBulletDamage;
        public static float NightPatrolSavedHealth(float hp,int version)=>version==0?Mathf.Clamp01(hp/100)*NightPatrolHealth:hp;
        public bool NightPatrolStopped=>State.phase!=3||State.night2ReachedStore||State.Done("N2_03")||State.Done("N2_05");
        public bool NightObservationRulesActive=>State.phase==3&&!State.Done("N2_05");
        public float NightPatrolScanRate=>NightObservationRulesActive?.5f:1f;
        public float NightPatrolDetectionRange=>NightObservationRulesActive?40:Layout!=null?Mathf.Clamp(Layout.nightPatrolLightRange,10,55):28;
        bool NightPatrolConceals(ShadowVale.Gameplay.Player.PlayerController motor)=>
            motor!=null&&(NightObservationRulesActive?motor.IsProne:VillageRiceInstances.Conceals(motor));
        GameObject nightBeamRoot;
        readonly Mesh[] nightBeamMeshes=new Mesh[3];
        readonly Transform[] nightBeams=new Transform[3];
        readonly Light[] nightLights=new Light[3];
        float previousNightAngle=180;
        public System.Collections.Generic.IReadOnlyList<Light> NightPatrolLights=>nightLights;
        bool NightPatrolExists=>Enemies.Any(e=>e!=null&&e.group==NightPatrolGroup&&e.gameObject.activeInHierarchy);
        Vector3 NightObservationPoint=>nightLights.FirstOrDefault(l=>l!=null&&l.isActiveAndEnabled) is Light light?light.transform.position:NightPatrolPosition+Vector3.up*.3f;
        Material nightBeamMaterial;
        Vector3 nightNominalOrigin,nightSweepDirection=Vector3.forward;
        public Vector3 NightPatrolPosition=>sites["nightPatrol"].position;
        public const float NightObservationRange=40;
        public bool TryReachNightObservationCheckpoint(Vector3 position)
        {
            if(State.phase!=3||State.night2ObservationCheckpointReached||State.Done("N2_01")||Vector3.Distance(position,NightObservationSpawn)>3)return false;
            State.night2ObservationCheckpointReached=true;return true;
        }
        public bool NightObservationPositionAllowed(Vector3 position)=>
            State.phase==3&&State.night2ObservationCheckpointReached&&
            Vector3.ProjectOnPlane(position-NightPatrolPosition,Vector3.up).sqrMagnitude<=NightObservationRange*NightObservationRange;
        bool TickNightObservation(float dt,float seconds)
        {
            var cam=rig.GetComponent<Camera>();var target=NightObservationPoint;var delta=target-cam.transform.position;
            bool view=binocular&&!player.IsMoving&&NightPatrolExists&&NightObservationPositionAllowed(Mission.Player.position)&&
                Vector3.Angle(cam.transform.forward,delta)<Mathf.Min(32,cam.fieldOfView*.5f+8)&&ObservationVisible(cam,target);
            observed=view?observed+dt:0;return observed>=seconds;
        }
        BoxCollider nightObservationBridge;
        BoxCollider NightObservationBridge
        {
            get
            {
                if(nightObservationBridge==null)nightObservationBridge=FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
                    .Where(c=>c.enabled&&(c.name=="Walkable bridge deck"||c.name=="Bridge deck"))
                    .OrderBy(c=>Vector3.Distance(c.bounds.center,sites["bridge"].position)).FirstOrDefault();
                return nightObservationBridge;
            }
        }
        public Vector3 NightObservationSpawn
        {
            get
            {
                var deck=NightObservationBridge;if(deck==null)return sites["bridge"].position;
                var a=deck.transform.TransformPoint(deck.center-Vector3.right*deck.size.x*.5f);
                var b=deck.transform.TransformPoint(deck.center+Vector3.right*deck.size.x*.5f);
                var end=Vector3.Distance(a,sites["bridge"].position)<Vector3.Distance(b,sites["bridge"].position)?a:b;
                var point=Vector3.MoveTowards(end,deck.bounds.center,1.5f);
                return NavMesh.SamplePosition(point,out var hit,1.5f,NavMesh.AllAreas)?hit.position:point;
            }
        }
        public bool AtNightObservationBridge(Vector3 position)
        {
            if(Vector3.Distance(position,sites["bridge"].position)<16)return true;
            var deck=NightObservationBridge;if(deck==null)return false;
            var local=deck.transform.InverseTransformPoint(position)-deck.center;
            return Mathf.Abs(local.x)<=deck.size.x*.5f+.5f&&Mathf.Abs(local.z)<=deck.size.z*.5f+.5f&&Mathf.Abs(local.y)<2;
        }
        Vector3 NightPatrolPost(int i)
        {
            return NightPatrolPosition+Vector3.ProjectOnPlane(Layout.nightTwoPatrolRowDirection,Vector3.up).normalized*(i*10);
        }
        Vector3 MigrateNightPatrolPosition(string id,Vector3 position)
        {
            var target=NightPatrolPost(id.EndsWith("_0")?0:id.EndsWith("_1")?1:2);
            return NavMesh.SamplePosition(target,out var floor,4,NavMesh.AllAreas)?floor.position:position;
        }
        public Light NightPatrolLight=>sweepLight;
        Vector3 RecoveredScoutPosition=>sites["rest"].position+Vector3.right*2;
        void PlaceRecoveredScout()
        {
            var scout=roles["Tâm"];scout.SetInjured(false);scout.StopAt(RecoveredScoutPosition);scout.SetSitting(true);
        }

        void RefreshNightPatrol()
        {
            bool active=State.phase==3&&!State.Done("N2_05");
            if(active)
            {
                if(State.nightPatrolSeed==0)State.nightPatrolSeed=Random.Range(1,int.MaxValue);
                for(int i=0;i<3;i++)
                {
                    string id=NightPatrolGroup+"_"+i;
                    if(Enemies.Any(unit=>unit!=null&&unit.id==id))continue;
                    var p=NightPatrolPost(i);
                    if(!NavMesh.SamplePosition(p,out var floor,4,NavMesh.AllAreas))continue;
                    SpawnEnemy(id,NightPatrolGroup,floor.position,NightPatrolHealth,false);
                }
                if(!State.combatGroups.Contains(NightPatrolGroup))State.combatGroups.Add(NightPatrolGroup);
                State.nightPatrolHealthVersion=1;
            }
            foreach(var unit in Enemies)if(unit!=null&&unit.group==NightPatrolGroup){unit.gameObject.SetActive(active);if(active)Map02PatrolFlashlight.Bind(unit);}
            if(active&&nightBeamRoot==null)BuildNightBeam();
            if(nightBeamRoot!=null)nightBeamRoot.SetActive(active);
            if(active){TickNightStoreArrival();TickPatrol(0);}
            if(NightPatrolStopped)StopNightPatrolAtStore();
        }
        public void TickNightStoreArrival()
        {
            if(State.phase!=3||State.night2ReachedStore||!State.Done("N2_02")||!sites.ContainsKey("nightHatch"))return;
            var delta=Mission.Player.position-NightDoorStand;
            if(Mathf.Abs(delta.y)>1.2f||Vector3.ProjectOnPlane(delta,Vector3.up).sqrMagnitude>9)return;
            State.night2ReachedStore=true;detectionClock=0;State.patrolFocusSeconds=0;
            StopNightPatrolAtStore();Mission.Say("Đã tới cửa hầm kho. Tổ tuần tra ngừng quét. E mở nắp · Space dùng thang · S xuống · W lên.");Mission.StoreCheckpoint();
        }
        void StopNightPatrolAtStore()
        {
            if(sweepLight!=null)sweepLight.enabled=false;
            foreach(var light in nightLights)if(light!=null)light.enabled=false;
            if(nightBeamRoot!=null)nightBeamRoot.SetActive(false);
            foreach(var unit in Enemies)if(unit!=null&&unit.group==NightPatrolGroup)unit.StopAtStoreArrival();
        }
        public void TickNightPatrolUnit(Map02ChapterEnemy unit,NavMeshAgent agent)
        {
            // Hold the three posts ten metres apart; only the beam scans until combat starts.
            agent.isStopped=true;
            if(NightPatrolStopped)return;
            int i=unit.id.EndsWith("_0")?0:unit.id.EndsWith("_1")?1:2;
            var facing=Vector3.ProjectOnPlane(NightLightDirection(i,nightSweepDirection),Vector3.up);
            if(facing.sqrMagnitude>.001f)unit.transform.rotation=Quaternion.LookRotation(facing);
        }
        void BuildNightBeam()
        {
            nightBeamRoot=new GameObject("Đêm 2 — đèn pin của tổ tuần tra");nightBeamRoot.transform.SetParent(root.transform,false);
            nightBeamRoot.AddComponent<Map02PatrolLightSync>().owner=this;
            nightBeamMaterial=new Material(Resources.Load<Shader>("Effects/Map02PatrolBeam")){name="Đêm 2 — luồng đèn trong sương"};
            for(int i=0;i<3;i++)
            {
                nightLights[i]=i==0?sweepLight:new GameObject("Đèn quét phụ "+i).AddComponent<Light>();
                if(i>0)nightLights[i].transform.SetParent(nightBeamRoot.transform,false);
                var light=nightLights[i];light.type=LightType.Spot;light.range=NightPatrolDetectionRange;light.spotAngle=24;light.shadows=LightShadows.Hard;light.intensity=i==0?7:4;light.color=new Color(.67f,.8f,1);
                nightBeamMeshes[i]=new Mesh{name="Night patrol cone "+i};
                var beam=new GameObject(i==0?"Luồng đèn nhìn thấy được":"Luồng đèn phụ "+i);beam.transform.SetParent(nightBeamRoot.transform,false);beam.AddComponent<MeshFilter>().sharedMesh=nightBeamMeshes[i];var renderer=beam.AddComponent<MeshRenderer>();renderer.sharedMaterial=nightBeamMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;nightBeams[i]=beam.transform;
            }
        }
        Vector3 NightLightDirection(int i,Vector3 main)
        {
            var unit=NightUnit(i);var prop=unit!=null?unit.GetComponent<Map02PatrolFlashlight>():null;
            if(unit!=null&&unit.TrackingVisibleTarget&&prop?.Emitter!=null)return prop.Emitter.forward;
            return i==0?main:NightPatrolSweepDirection(i);
        }
        public bool NightPatrolDarkWindow=>State.patrolScanSeconds>=Mathf.Max(10,Layout!=null?Layout.patrolSweepSeconds:15)-4;
        static float PatrolRandom(int seed,int cycle,int index)
        {
            unchecked{uint value=(uint)seed^(uint)(cycle*747796405)^(uint)((index+1)*2891336453u);value^=value>>16;value*=2246822519u;value^=value>>13;return (value&0xffffff)/16777216f;}
        }
        Vector3 NightPatrolMissionDirection(int i)
        {
            var unit=NightUnit(i);var origin=unit!=null?unit.transform.position:NightPatrolPost(i);
            var bank=NightBankScanPoint(origin);
            return Vector3.ProjectOnPlane(bank-origin,Vector3.up).normalized;
        }
        Vector3 NightPatrolSweepDirection(int i)
        {
            float period=Mathf.Max(10,Layout!=null?Layout.patrolSweepSeconds:15),active=period-4;
            var towardMission=NightPatrolMissionDirection(i);
            // Randomise the rhythm inside the mission-facing sector, never its global heading.
            float edge=PatrolRandom(State.nightPatrolSeed,State.nightPatrolCycle,6)>.5f?60:-60;
            if(NightPatrolDarkWindow)return Quaternion.AngleAxis(edge,Vector3.up)*towardMission;
            float t=Mathf.Clamp01(State.patrolScanSeconds/active);
            float phase=PatrolRandom(State.nightPatrolSeed,State.nightPatrolCycle,i)*Mathf.PI*2;
            float sign=PatrolRandom(State.nightPatrolSeed,State.nightPatrolCycle,i+3)>.5f?1:-1;
            float angle=Mathf.Sin(phase+t*Mathf.PI*2*sign)*60;
            float blend=Mathf.Min(Mathf.SmoothStep(0,1,State.patrolScanSeconds),Mathf.SmoothStep(0,1,active-State.patrolScanSeconds));
            return Quaternion.AngleAxis(Mathf.Lerp(edge,angle,blend),Vector3.up)*towardMission;
        }

        Map02ChapterEnemy NightUnit(int i)=>Enemies.FirstOrDefault(e=>e!=null&&e.id==NightPatrolGroup+"_"+i);
        Vector3 NightLightOrigin(int i,Vector3 main)
        {
            var unit=NightUnit(i);var emitter=unit!=null?unit.GetComponent<Map02PatrolFlashlight>()?.Emitter:null;
            if(emitter!=null)
                return i==0&&!unit.Alerted?main+emitter.position-unit.transform.position-Vector3.up*1.7f:emitter.position;
            return i==0?main:unit!=null?unit.transform.position+Vector3.up*1.7f:main;
        }
        public bool NightGuardIlluminates(Map02ChapterEnemy unit)
        {
            if(NightPatrolStopped)return false;
            if(NightPatrolConceals(Mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>()))return false;
            int i=unit.id.EndsWith("_0")?0:unit.id.EndsWith("_1")?1:2;
            var from=NightLightOrigin(i,nightNominalOrigin);var delta=Mission.Player.position+Vector3.up*(Mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>()?.IsProne==true?.4f:1f)-from;
            if(delta.magnitude>NightPatrolDetectionRange||Vector3.Angle(NightLightDirection(i,nightSweepDirection),delta)>=11||Map01Smoke.Blocks(from,from+delta))return false;
            return Physics.Raycast(from,delta.normalized,out var hit,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)&&(hit.transform==Mission.Player||hit.transform.IsChildOf(Mission.Player));
        }
        public void TickNightPatrolWorld(float dt)
        {
            if(!ready||Mission==null||Mission.Paused||ForestMenu.Visible||Mission.World.story.failed||Mission.Panel!=0||sceneTask!=null||phaseFade>0||State.finished)return;
            TickNightStoreArrival();
            TickPatrol(Mathf.Clamp(dt,0,.1f));
        }
        public void SyncNightPatrolPresentation(){if(nightBeamRoot!=null&&State.phase==3&&!State.Done("N2_05"))UpdateNightPatrolPresentation(nightNominalOrigin,nightSweepDirection);}
        bool NightGuardAlive(int i)=>Enemies.Any(e=>e!=null&&e.id==NightPatrolGroup+"_"+i&&!e.Dead&&e.gameObject.activeInHierarchy);
        void UpdateNightPatrolPresentation(Vector3 origin,Vector3 direction)
        {
            if(nightBeamRoot==null)return;
            nightNominalOrigin=origin;nightSweepDirection=direction;
            nightBeamRoot.SetActive(!NightPatrolStopped);
            if(NightPatrolStopped){StopNightPatrolAtStore();return;}
            for(int i=0;i<3;i++)
            {
                var unit=NightUnit(i);var prop=unit!=null?unit.GetComponent<Map02PatrolFlashlight>():null;
                bool live=NightGuardAlive(i);nightLights[i].range=NightPatrolDetectionRange;nightLights[i].enabled=live;nightBeams[i].gameObject.SetActive(live);if(!live)continue;
                var from=NightLightOrigin(i,origin);var toward=NightLightDirection(i,direction);var rotation=Quaternion.LookRotation(toward);
                prop?.SetDirection(toward);
                nightLights[i].transform.SetPositionAndRotation(from,rotation);nightBeams[i].SetPositionAndRotation(from,rotation);
                const int sides=20;var vertices=new Vector3[sides+1];var colors=new Color[sides+1];var triangles=new int[sides*3];vertices[0]=Vector3.forward*.15f;colors[0]=new Color(.67f,.8f,1,.018f);
                float length=nightLights[i].range;
                if(Physics.Raycast(from,toward,out var hit,length,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<Map02ChapterEnemy>()==null)length=Mathf.Max(.3f,hit.distance);
                float radius=Mathf.Tan(nightLights[i].spotAngle*.5f*Mathf.Deg2Rad)*length;
                for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;vertices[j+1]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,length);colors[j+1]=new Color(.67f,.8f,1,.001f);triangles[j*3]=0;triangles[j*3+1]=j+1;triangles[j*3+2]=(j+1)%sides+1;}
                var mesh=nightBeamMeshes[i];mesh.Clear();mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();
            }
        }
        bool NightPatrolExposure(Vector3 origin,Vector3 direction,out float angle,out float distance)
        {
            bool exposed=false;angle=180;distance=float.MaxValue;
            if(NightPatrolStopped)return false;
            bool concealed=NightPatrolConceals(Mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>());
            for(int i=0;i<3;i++)
            {
                if(!NightGuardAlive(i))continue;
                var from=NightLightOrigin(i,origin);var delta=Mission.Player.position+Vector3.up*(Mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>()?.IsProne==true?.4f:1f)-from;var toward=NightLightDirection(i,direction);
                float a=Vector3.Angle(toward,delta);angle=Mathf.Min(angle,a);distance=Mathf.Min(distance,delta.magnitude);
                if(concealed||Map01Smoke.Blocks(from,from+delta)||a>=11||delta.magnitude>=NightPatrolDetectionRange)continue;
                if(Physics.Raycast(from,delta.normalized,out var seen,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)&&(seen.transform==Mission.Player||seen.transform.IsChildOf(Mission.Player)))exposed=true;
            }
            return exposed;
        }
        void DrawNightObservationGuide(GUIStyle label)
        {
            var camera=rig.GetComponent<Camera>();var target=NightObservationPoint;var point=camera.WorldToViewportPoint(target);float distance=Vector3.Distance(Mission.Player.position,NightPatrolPosition);
            var style=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,fontSize=20};
            if(point.z>0&&point.x>.1f&&point.x<.9f&&point.y>.12f&&point.y<.85f&&ObservationVisible(camera,target))
                GUI.Label(new Rect(point.x*1600-190,(1-point.y)*900-80,380,75),$"◇ TỔ TUẦN TRA · {distance:0} m\n{(!State.night2ObservationCheckpointReached?"Đi qua ghim quan sát trước":!NightObservationPositionAllowed(Mission.Player.position)?"Tới trong bán kính 40 m":"Giữ F · nhìn người cầm đèn")}",style);
            else
            {
                string turn=point.z<0?"phía sau":point.x<.5f?"bên trái":"bên phải";
                GUI.Box(new Rect(460,95,810,65),GUIContent.none);GUI.Label(new Rect(470,102,790,55),$"Tổ tuần tra ở {turn} · {distance:0} m\n{(!State.night2ObservationCheckpointReached?"Đi qua ghim quan sát ở đầu cầu trước":"Chọn chỗ trong bán kính 40 m · nhìn theo ghim tổ tuần tra và giữ F")}",style);
            }
        }
        void ReleaseNightPatrolPresentation()
        {
            foreach(var mesh in nightBeamMeshes)if(mesh!=null)Destroy(mesh);if(nightBeamMaterial!=null)Destroy(nightBeamMaterial);
        }
    }
}
