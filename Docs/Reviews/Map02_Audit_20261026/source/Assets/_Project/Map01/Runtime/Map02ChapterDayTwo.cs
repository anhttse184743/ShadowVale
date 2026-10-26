using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        GameObject dayTwoRoute,scoutTracks,wireCoil,carriedWire,antennaMast;
        Transform antennaBase;
        Vector3 antennaFoot;
        float climbHeight,tieClock;
        bool antennaTied,antennaAborting;
        Map01CinematicPose antennaPose;AnimationClip antennaIdle;
        readonly List<Material> dayTwoMaterials=new List<Material>();
        readonly List<GameObject> dayTwoApproaches=new List<GameObject>();
        public Vector3[] DayTwoOpenPath {get;private set;}=System.Array.Empty<Vector3>();
        public Vector3[] DayTwoCoveredPath {get;private set;}=System.Array.Empty<Vector3>();
        public Transform AntennaBase=>antennaBase;
        public float AntennaClimbHeight=>climbHeight;
        public bool AntennaTied=>antennaTied;
        public string AntennaInstruction=>antennaAborting?"Đang xuống điểm bám an toàn để hủy thao tác.":antennaTied?"Đã cột ăng-ten. Giữ S xuống đất để Hùng nối máy.":climbHeight<Layout.antennaHeight-.05f?"Giữ W leo theo điểm bám · thả W để dừng · giữ S xuống · Esc hủy":"A/D xoay hướng · tín hiệu ổn định 2 giây rồi giữ Space cột dây · S xuống · Esc hủy";

        Material DayTwoMaterial(string name,Color color)
        {
            var material=new Material(Map01Fx.Lit){name=name,color=color};dayTwoMaterials.Add(material);return material;
        }
        GameObject RoutePart(string name,PrimitiveType type,Transform parent,Vector3 local,Vector3 size,Material material,bool solid=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            var collider=go.GetComponent<Collider>();if(!solid){collider.enabled=false;Destroy(collider);}return go;
        }
        Vector3[] BuildApproach(string name,Vector3[] via,Material wood)
        {
            var points=new List<Vector3>{sites["gate"].position};
            foreach(var point in via)
            {
                if(!NavMesh.SamplePosition(point,out var floor,8,NavMesh.AllAreas)){Debug.LogWarning(name+": waypoint outside NavMesh "+point);return System.Array.Empty<Vector3>();}
                points.Add(floor.position);
            }
            var bridge=FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
                .Where(c=>c.name=="Walkable bridge deck"||c.name=="Bridge deck")
                .OrderBy(c=>Vector3.Distance(c.bounds.center,sites["bridge"].position)).FirstOrDefault();
            if(bridge!=null)
            {
                var a=bridge.transform.TransformPoint(bridge.center-Vector3.right*(bridge.size.x*.5f+1.5f));
                var b=bridge.transform.TransformPoint(bridge.center+Vector3.right*(bridge.size.x*.5f+1.5f));
                if(Vector3.Distance(points[points.Count-1],a)>Vector3.Distance(points[points.Count-1],b)){var swap=a;a=b;b=swap;}
                foreach(var p in new[]{a,bridge.bounds.center,b})
                {
                    if(!NavMesh.SamplePosition(p,out var floor,2,NavMesh.AllAreas)){Debug.LogWarning(name+": missing bridge centre NavMesh.");return System.Array.Empty<Vector3>();}
                    points.Add(floor.position);
                }
            }
            points.Add(sites["bridge"].position);
            var corners=new List<Vector3>();
            for(int i=1;i<points.Count;i++)
            {
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(points[i-1],points[i],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete){Debug.LogWarning(name+": incomplete approach.");return System.Array.Empty<Vector3>();}
                corners.AddRange(path.corners.Skip(i>1?1:0));
            }
            var trail=new GameObject(name);trail.transform.SetParent(dayTwoRoute.transform,false);
            dayTwoApproaches.Add(trail);
            // Sparse physical stakes describe a choice; the player can leave either route freely.
            foreach(var point in points.Skip(1).Take(via.Length))RoutePart("Cọc chỉ lối",PrimitiveType.Cylinder,trail.transform,point+Vector3.up*.3f,new Vector3(.07f,.3f,.07f),wood);
            return corners.ToArray();
        }
        void BuildDayTwoRoute()
        {
            dayTwoRoute=new GameObject("Ngày 2 — đường đồng và bờ tre");dayTwoRoute.transform.SetParent(root.transform,false);
            var wood=DayTwoMaterial("Day 2 weathered wood",new Color(.3f,.24f,.12f));
            var leaves=DayTwoMaterial("Day 2 bamboo leaves",new Color(.19f,.29f,.09f));
            var blood=DayTwoMaterial("Day 2 dried blood",new Color(.22f,.045f,.025f));
            var soil=DayTwoMaterial("Day 2 footprints",new Color(.19f,.14f,.08f));
            DayTwoOpenPath=BuildApproach("Lối đồng trống",Layout.dayTwoOpenRoute,wood);
            DayTwoCoveredPath=BuildApproach("Lối bờ tre",Layout.dayTwoCoveredRoute,wood);
            // Supplement the existing vegetation with cover beside, never across, the path.
            foreach(var point in Layout.dayTwoCoveredRoute)
            {
                if(!NavMesh.SamplePosition(point,out var floor,8,NavMesh.AllAreas))continue;
                var screen=new GameObject("Bụi tre che lối");screen.transform.SetParent(dayTwoRoute.transform,false);screen.transform.position=floor.position+Vector3.back*2.5f;
                for(int i=0;i<7;i++)RoutePart("Thân tre",PrimitiveType.Cylinder,screen.transform,new Vector3((i-3)*.22f,1.5f,0),new Vector3(.07f,1.5f,.07f),wood,true);
                RoutePart("Tán tre cản tầm nhìn",PrimitiveType.Sphere,screen.transform,new Vector3(0,2.1f,0),new Vector3(2.4f,2.5f,1.15f),leaves,true);
            }
            scoutTracks=new GameObject("Dấu chân và vết máu tới Tâm");scoutTracks.transform.SetParent(dayTwoRoute.transform,false);
            var trace=new NavMeshPath();
            if(NavMesh.CalculatePath(sites["bridge"].position,sites["scout"].position,NavMesh.AllAreas,trace)&&trace.status==NavMeshPathStatus.PathComplete)
            {
                int step=0;
                for(int i=1;i<trace.corners.Length;i++)
                {
                    var a=trace.corners[i-1];var b=trace.corners[i];var direction=(b-a).normalized;
                    for(float d=.4f;d<Vector3.Distance(a,b);d+=.8f)
                    {
                        var p=Vector3.MoveTowards(a,b,d)+Vector3.Cross(Vector3.up,direction)*(step%2==0?.14f:-.14f);
                        if(Physics.Raycast(p+Vector3.up,Vector3.down,out var hit,2,~0,QueryTriggerInteraction.Ignore))p=hit.point;
                        var footprint=RoutePart("Dấu chân "+step,PrimitiveType.Cube,scoutTracks.transform,p+Vector3.up*.012f,new Vector3(.12f,.018f,.29f),soil);
                        footprint.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Vector3.up));
                        if(step++%3==0)RoutePart("Vết máu khô",PrimitiveType.Sphere,scoutTracks.transform,p+Vector3.right*.15f+Vector3.up*.018f,new Vector3(.28f,.025f,.19f),blood);
                    }
                }
            }
            // Reuse the map's authored coconut mesh rather than loading a second character/environment pack.
            var palm=FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Coconut at field edge");
            var station=sites["radio"].position+Vector3.right*3.5f;
            if(NavMesh.SamplePosition(station,out var baseFloor,3,NavMesh.AllAreas))station=baseFloor.position;
            var baseObject=new GameObject("Cây dựng ăng-ten — điểm bám");baseObject.transform.SetParent(root.transform,false);baseObject.transform.position=station;antennaBase=baseObject.transform;
            if(palm!=null){var tree=Instantiate(palm.gameObject,antennaBase);tree.name="Dừa cạnh trạm";tree.transform.localPosition=Vector3.forward*.8f;}
            else RoutePart("Thân cột ăng-ten",PrimitiveType.Cylinder,antennaBase,Vector3.forward*.8f+Vector3.up*3,new Vector3(.4f,3,.4f),wood,true);
            for(float h=.4f;h<=Layout.antennaHeight+.2f;h+=.4f)RoutePart("Điểm bám",PrimitiveType.Cube,antennaBase,new Vector3(0,h,.35f),new Vector3(.85f,.07f,.18f),wood);
            antennaMast=RoutePart("Cột ăng-ten",PrimitiveType.Cylinder,antennaBase,new Vector3(0,Layout.antennaHeight+1,.8f),new Vector3(.045f,1,.045f),wood);
            RoutePart("Thanh thu sóng",PrimitiveType.Cube,antennaMast.transform,Vector3.zero,new Vector3(35,.035f,2),wood);
            var copper=DayTwoMaterial("Dây ăng-ten",new Color(.2f,.15f,.09f));
            wireCoil=BuildWire("Cuộn dây tại kho",root.transform,copper);wireCoil.transform.position=sites["store"].position+Vector3.up*.2f;
            carriedWire=BuildWire("Cuộn dây Nam đang mang",Mission.Player,copper);carriedWire.transform.localPosition=new Vector3(-.3f,1,-.2f);
        }
        GameObject BuildWire(string name,Transform parent,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            for(int ring=0;ring<4;ring++)
            {
                var line=new GameObject("Vòng dây").AddComponent<LineRenderer>();line.transform.SetParent(go.transform,false);line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.018f;line.sharedMaterial=material;line.positionCount=24;
                for(int i=0;i<24;i++){float a=i*Mathf.PI/12;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.22f,ring*.025f,Mathf.Sin(a)*.22f));}
            }
            return go;
        }
        void RefreshDayTwoProps()
        {
            if(dayTwoRoute==null)return;
            foreach(var approach in dayTwoApproaches)approach.SetActive(State.phase==2);
            scoutTracks.SetActive(State.phase==2&&!State.Done("D2_04"));
            wireCoil.SetActive(State.Available(Map02ChapterDefinition.Load().Find("D2_05"))&&State.wireStage==0);
            carriedWire.SetActive(State.wireStage==1&&State.carry=="D2_05");
            antennaMast.SetActive(State.Done("D2_05"));
        }
        void StartAntennaTraversal()
        {
            var actor=Mission.Player.GetComponentInChildren<Animator>();
            if(actor!=null){antennaIdle=actor.runtimeAnimatorController?.animationClips.FirstOrDefault(c=>c.name.Contains("Idle"));antennaPose=new Map01CinematicPose(actor,skinGrounding:false);}
            climbHeight=tieClock=signalHold=0;signal=.15f;antennaTied=antennaAborting=false;
            antennaFoot=antennaBase.position;MoveAntennaPlayer(antennaFoot);
            Mission.Player.rotation=Quaternion.LookRotation(antennaBase.forward);
        }
        void MoveAntennaPlayer(Vector3 position)
        {
            var body=Mission.Player.GetComponent<CharacterController>();bool enabled=body.enabled;body.enabled=false;Mission.Player.position=position;body.enabled=enabled;player.RestoreMotion(false);
        }
        void TickAntennaTraversal(float dt,Keyboard keys)
        {
            StepAntennaTraversal(dt,keys?.wKey.isPressed==true,keys?.sKey.isPressed==true,keys?.aKey.isPressed==true,keys?.dKey.isPressed==true,keys?.spaceKey.isPressed==true,keys?.escapeKey.wasPressedThisFrame==true);
        }
        // One transaction: cancelling never connects the radio; a stable signal alone is insufficient.
        void StepAntennaTraversal(float dt,bool up,bool down,bool left,bool right,bool tie,bool cancel)
        {
            if(cancel)antennaAborting=true;
            float change=antennaAborting?-2.5f:down?-1.2f:up&&!antennaTied?1.2f:0;
            climbHeight=Mathf.Clamp(climbHeight+change*dt,0,Layout.antennaHeight);
            MoveAntennaPlayer(antennaFoot+Vector3.up*climbHeight);
            bool atTop=climbHeight>=Layout.antennaHeight-.05f&&!antennaAborting;
            if(atTop&&!antennaTied)
            {
                signal=Mathf.Clamp01(signal+(right?dt*.22f:0)-(left?dt*.22f:0));
                antennaMast.transform.localRotation=Quaternion.Euler(0,Mathf.Lerp(-85,85,signal),0);
                if(signalAudio!=null)signalAudio.volume=Mathf.Abs(signal-.72f)*.35f;
                signalHold=Mathf.Abs(signal-.72f)<.055f?signalHold+dt:0;
                tieClock=signalHold>=2&&tie?tieClock+dt:0;
                if(tieClock>=1.5f){antennaTied=true;Mission.Say("Đã cột đúng hướng. Giữ S xuống đất; Hùng sẽ nối máy.");}
            }
            else if(!antennaTied){signalHold=tieClock=0;}
            if(climbHeight<=.001f&&(antennaTied||antennaAborting))EndWork(antennaTied&&!antennaAborting);
        }
        void SampleAntennaTraversal()
        {
            // A procedural handhold pose until a dedicated climb clip is authored.
            var animator=Mission.Player.GetComponentInChildren<Animator>();if(animator==null||!animator.isHuman)return;
            var clip=tieClock>0?Layout.namAntennaTie:Layout.namAntennaClimb;
            antennaPose?.Sample(clip!=null?clip:antennaIdle,clip!=null?climbHeight/1.2f:workClock,true);
            if(clip!=null)return;
            float phase=climbHeight*Mathf.PI*2.5f;
            SetClimbBone(animator,HumanBodyBones.LeftUpperArm,new Vector3(-110+Mathf.Sin(phase)*18,0,-15));
            SetClimbBone(animator,HumanBodyBones.RightUpperArm,new Vector3(-110-Mathf.Sin(phase)*18,0,15));
            SetClimbBone(animator,HumanBodyBones.LeftLowerArm,new Vector3(-45,0,0));SetClimbBone(animator,HumanBodyBones.RightLowerArm,new Vector3(-45,0,0));
            SetClimbBone(animator,HumanBodyBones.LeftUpperLeg,new Vector3(-25-Mathf.Sin(phase)*20,0,-8));SetClimbBone(animator,HumanBodyBones.RightUpperLeg,new Vector3(-25+Mathf.Sin(phase)*20,0,8));
            if(tieClock>0)
            {
                var knot=antennaFoot+Vector3.up*(climbHeight+1.35f)+antennaBase.forward*.6f;
                var chest=animator.GetBoneTransform(HumanBodyBones.Chest).position;
                float loop=Mathf.Sin(tieClock*12)*.06f;
                Map01ArmContact.Reach(animator,true,knot-antennaBase.right*.08f+Vector3.up*loop,chest-antennaBase.right*.4f,1);
                Map01ArmContact.Reach(animator,false,knot+antennaBase.right*.08f-Vector3.up*loop,chest+antennaBase.right*.4f,1);
            }
        }
        static void SetClimbBone(Animator animator,HumanBodyBones bone,Vector3 angles){var t=animator.GetBoneTransform(bone);if(t!=null)t.localRotation*=Quaternion.Euler(angles);}
        void ReleaseAntennaTraversal()
        {
            antennaPose?.Dispose();antennaPose=null;
            ReleasePerformance();
            if(Mission?.Player!=null)MoveAntennaPlayer(antennaFoot);
            climbHeight=0;antennaAborting=false;
        }
        void ReleaseDayTwoProps()
        {
            if(carriedWire!=null)Destroy(carriedWire);
            foreach(var material in dayTwoMaterials)if(material!=null)Destroy(material);
        }
        void DrawAntennaWork(GUIStyle label,GUIStyle title)
        {
            GUI.Box(new Rect(350,650,900,230),GUIContent.none);
            GUI.Label(new Rect(375,665,850,42),$"Dựng ăng-ten · {climbHeight:0.0} / {Layout.antennaHeight:0.0} m",title);
            GUI.Label(new Rect(375,710,850,70),AntennaInstruction,label);
            if(climbHeight<Layout.antennaHeight-.05f||antennaTied||antennaAborting)return;
            GUI.Box(new Rect(390,795,810,22),"");var before=GUI.color;
            GUI.color=Color.green;GUI.DrawTexture(new Rect(390+810*.665f,795,810*.11f,22),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(390+810*signal,791,5,30),Texture2D.whiteTexture);GUI.color=before;
            GUI.Label(new Rect(390,828,810,40),$"Tín hiệu: {Mathf.Min(2,signalHold):0.0}/2 giây · Cột dây: {Mathf.Clamp01(tieClock/1.5f)*100:0}%",label);
        }
    }
}
