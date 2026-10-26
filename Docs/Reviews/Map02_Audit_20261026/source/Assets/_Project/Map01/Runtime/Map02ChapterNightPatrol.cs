using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public const string NightPatrolGroup="night2_patrol";
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
        Vector3 MigrateNightPatrolPosition(string id,Vector3 position)
        {
            if(Vector3.Distance(position,sites["scout"].position)>12||Vector3.Distance(NightPatrolPosition,sites["scout"].position)<20)return position;
            var target=NightPatrolPosition+(id.EndsWith("_0")?Vector3.zero:new Vector3(id.EndsWith("_1")?3:-3,0,-2));
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
            if(active&&!State.combatGroups.Contains(NightPatrolGroup))
            {
                var positions=new Vector3[3];
                for(int i=0;i<3;i++)
                {
                    var p=NightPatrolPosition+(i==0?Vector3.zero:new Vector3(i==1?3:-3,0,-2));
                    if(!NavMesh.SamplePosition(p,out var floor,4,NavMesh.AllAreas))return;
                    positions[i]=floor.position;
                }
                for(int i=0;i<3;i++)SpawnEnemy(NightPatrolGroup+"_"+i,NightPatrolGroup,positions[i],100,false);
                State.combatGroups.Add(NightPatrolGroup);
            }
            foreach(var unit in Enemies)if(unit!=null&&unit.group==NightPatrolGroup){unit.gameObject.SetActive(active);if(active)Map02PatrolFlashlight.Bind(unit);}
            if(active&&nightBeamRoot==null)BuildNightBeam();
            if(nightBeamRoot!=null)nightBeamRoot.SetActive(active);
            if(active)TickPatrol(0);
        }
        public void TickNightPatrolUnit(Map02ChapterEnemy unit,NavMeshAgent agent)
        {
            // Unalerted guards keep their authored patrol. Sight or damage enters normal combat.
            if(unit.id==NightPatrolGroup+"_0")
            {
                agent.isStopped=true;var facing=Vector3.ProjectOnPlane(sweepLight.transform.forward,Vector3.up);if(facing.sqrMagnitude>.001f)unit.transform.rotation=Quaternion.LookRotation(facing);return;
            }
            agent.isStopped=false;agent.speed=1;agent.stoppingDistance=.4f;
            if(agent.pathPending||agent.hasPath&&agent.remainingDistance>.6f)return;
            int side=unit.id.EndsWith("_1")?1:-1;
            float shift=Mathf.PingPong(State.patrolScanSeconds,6)-3;
            var target=NightPatrolPosition+new Vector3(side*3,0,-3+shift);
            if(NavMesh.SamplePosition(target,out var floor,3,NavMesh.AllAreas))agent.SetDestination(floor.position);
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
                var light=nightLights[i];light.type=LightType.Spot;light.range=Layout!=null?Mathf.Clamp(Layout.nightPatrolLightRange,10,55):28;light.spotAngle=24;light.shadows=LightShadows.Hard;light.intensity=i==0?7:4;light.color=new Color(.67f,.8f,1);
                nightBeamMeshes[i]=new Mesh{name="Night patrol cone "+i};
                var beam=new GameObject(i==0?"Luồng đèn nhìn thấy được":"Luồng đèn phụ "+i);beam.transform.SetParent(nightBeamRoot.transform,false);beam.AddComponent<MeshFilter>().sharedMesh=nightBeamMeshes[i];var renderer=beam.AddComponent<MeshRenderer>();renderer.sharedMaterial=nightBeamMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;nightBeams[i]=beam.transform;
            }
        }
        Vector3 NightLightDirection(int i,Vector3 main)
        {
            var unit=NightUnit(i);var prop=unit!=null?unit.GetComponent<Map02PatrolFlashlight>():null;
            if(unit!=null&&unit.Alerted&&prop?.Emitter!=null)return prop.Emitter.forward;
            return i==0||unit==null?main:Quaternion.AngleAxis(Mathf.Sin(State.patrolScanSeconds*.6f)*18,Vector3.up)*unit.transform.forward;
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
            int i=unit.id.EndsWith("_0")?0:unit.id.EndsWith("_1")?1:2;
            var from=NightLightOrigin(i,nightNominalOrigin);var delta=Mission.Player.position+Vector3.up*(Mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>()?.IsProne==true?.4f:1f)-from;
            if(delta.magnitude>(nightLights[i]!=null?nightLights[i].range:28)||Vector3.Angle(NightLightDirection(i,nightSweepDirection),delta)>=11||Map01Smoke.Blocks(from,from+delta))return false;
            return Physics.Raycast(from,delta.normalized,out var hit,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)&&(hit.transform==Mission.Player||hit.transform.IsChildOf(Mission.Player));
        }
        public void SyncNightPatrolPresentation(){if(nightBeamRoot!=null&&State.phase==3&&!State.Done("N2_05"))UpdateNightPatrolPresentation(nightNominalOrigin,nightSweepDirection);}
        bool NightGuardAlive(int i)=>Enemies.Any(e=>e!=null&&e.id==NightPatrolGroup+"_"+i&&!e.Dead&&e.gameObject.activeInHierarchy);
        void UpdateNightPatrolPresentation(Vector3 origin,Vector3 direction)
        {
            if(nightBeamRoot==null)return;
            nightNominalOrigin=origin;nightSweepDirection=direction;
            nightBeamRoot.SetActive(State.phase==3&&!State.Done("N2_05"));
            for(int i=0;i<3;i++)
            {
                var unit=NightUnit(i);var prop=unit!=null?unit.GetComponent<Map02PatrolFlashlight>():null;
                bool live=NightGuardAlive(i);nightLights[i].enabled=live;nightBeams[i].gameObject.SetActive(live);if(!live)continue;
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
            for(int i=0;i<3;i++)
            {
                if(!NightGuardAlive(i))continue;
                var from=NightLightOrigin(i,origin);var delta=Mission.Player.position+Vector3.up*(Mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>()?.IsProne==true?.4f:1f)-from;var toward=NightLightDirection(i,direction);
                float a=Vector3.Angle(toward,delta);angle=Mathf.Min(angle,a);distance=Mathf.Min(distance,delta.magnitude);
                if(a>=11||delta.magnitude>=(nightLights[i]!=null?nightLights[i].range:28))continue;
                if(Physics.Raycast(from,delta.normalized,out var seen,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)&&(seen.transform==Mission.Player||seen.transform.IsChildOf(Mission.Player)))exposed=true;
            }
            return exposed;
        }
        void DrawNightObservationGuide(GUIStyle label)
        {
            var camera=rig.GetComponent<Camera>();var target=NightObservationPoint;var point=camera.WorldToViewportPoint(target);float distance=Vector3.Distance(Mission.Player.position,NightPatrolPosition);
            var style=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,fontSize=20};
            if(point.z>0&&point.x>.1f&&point.x<.9f&&point.y>.12f&&point.y<.85f&&ObservationVisible(camera,target))
                GUI.Label(new Rect(point.x*1600-190,(1-point.y)*900-80,380,75),$"◇ TỔ TUẦN TRA · {distance:0} m\nGiữ F · nhìn người cầm đèn",style);
            else
            {
                string turn=point.z<0?"phía sau":point.x<.5f?"bên trái":"bên phải";
                GUI.Box(new Rect(460,95,810,65),GUIContent.none);GUI.Label(new Rect(470,102,790,55),$"Tổ tuần tra ở {turn} · {distance:0} m · đồng bên trái khi nhìn về nam\nTới đầu cầu, nhìn theo ghim tổ tuần tra và giữ F",style);
            }
        }
        void ReleaseNightPatrolPresentation()
        {
            foreach(var mesh in nightBeamMeshes)if(mesh!=null)Destroy(mesh);if(nightBeamMaterial!=null)Destroy(nightBeamMaterial);
        }
    }
}
