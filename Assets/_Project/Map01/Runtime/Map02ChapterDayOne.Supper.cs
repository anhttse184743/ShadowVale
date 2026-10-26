using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // "Lời hứa" (approved 2026-10-24): the Blender scene played as it was made (chuyen_dong/blender_scripts/loihua_timeline.py,
        // mocap layers in loihua_mixamo.py, exported by export_loihua_unity.py). Tray frame: local x out to the yard, y up,
        // z toward Hùng. Nam and Hùng sit cross-legged side by side behind the tray, facing the doorway, Hùng on Nam's right.
        // The clips, the props and the line starts all run on one clock: seconds since both sat down (MealTime).
        [System.Serializable] sealed class SupperProp{public string name,shape;public float[] size,pos,rot;}
        [System.Serializable] sealed class SupperTrack{public int fps,frames;public SupperProp[] props;}
        SupperTrack supperTrack;readonly List<(SupperProp data,Transform art)> supperArt=new();
        static readonly Vector3 NamSeatLocal=new Vector3(.76f,0,-.35f),HungSeatLocal=new Vector3(.76f,0,.35f);
        const float MealEnd=70;
        // Blender's LINES, with the rice-bowl exchange set into the silent opening: Nam has just pushed the fish over (8.0 s);
        // Hùng pushes it back while laying his chopsticks aside, Nam answers, Hùng sips and says his belly is fine.
        static readonly Dictionary<string,float> MealCueStarts=new()
        {
            {"m2v3_day1report_04",8.3f},{"m2v3_day1report_05",10.75f},{"m2v3_day1report_06",12.7f},{"m2v3_promise_01",14.8f},
            {"m2v3_promise_02",22},{"m2v3_promise_03",25.8f},{"m2v3_promise_04",31.8f},{"m2v3_promise_05",33.4f},{"m2v3_promise_05b",39.4f},
            {"m2v3_promise_06",41.2f},{"m2v3_promise_07",43.6f},{"m2v3_promise_08",46.2f},{"m2v3_promise_09",49.6f},{"m2v3_promise_10",53.4f},
            {"m2v3_promise_11",58.4f},{"m2v3_promise_12",63.4f},
        };
        float[] mealCues;
        float MealTime=>sceneClock-dayApproachDuration;
        float MealCue(int index)=>mealCues!=null&&index<mealCues.Length?mealCues[index]:MealEnd;
        Vector3 MealSeat(bool hungSide)=>mealRoot.TransformPoint(hungSide?HungSeatLocal:NamSeatLocal);
        Quaternion MealFrame=>Quaternion.LookRotation(Vector3.Cross(porchOut,Vector3.up),Vector3.up);   // local x = porchOut
        Quaternion MealFacing=>Quaternion.LookRotation(-porchOut,Vector3.up);                            // toward the tray and the doorway
        Transform hungBody;Vector3 hungBodyPosition,hungBodyScale;bool hungBodyNative;

        void PrepareMealTiming()
        {
            mealCues=new float[lines.Length];
            for(int i=0;i<lines.Length;i++)mealCues[i]=MealCueStarts.TryGetValue(lines[i].id,out var start)?start:i==0?8.3f:mealCues[i-1]+LineSeconds(lines[i-1])+1;
            mealBegan=false;
        }
        void BuildSupper(Material wood,Material cloth,Material iron)
        {
            Part(mealRoot,"Woven floor mat",new Vector3(.35f,.006f,0),new Vector3(1.9f,.012f,2f),cloth);
            Part(mealRoot,"Round wooden tray",new Vector3(0,.0825f,0),new Vector3(.8f,.0175f,.8f),wood,PrimitiveType.Cylinder);
            foreach(int a in new[]{45,135,225,315})Part(mealRoot,"Tray foot",new Vector3(.29f*Mathf.Cos(a*Mathf.Deg2Rad),.0325f,.29f*Mathf.Sin(a*Mathf.Deg2Rad)),new Vector3(.05f,.0325f,.05f),wood,PrimitiveType.Cylinder);
            var json=Resources.Load<TextAsset>("Map02DayOne/LoiHuaProps");supperTrack=json!=null?JsonUtility.FromJson<SupperTrack>(json.text):null;supperArt.Clear();
            if(supperTrack?.props==null){Debug.LogError("Map 2 supper props track is missing (Resources/Map02DayOne/LoiHuaProps).",this);return;}
            var ceramic=DayMaterial("Glazed rice bowl",new Color(.78f,.76f,.68f));var clay=DayMaterial("Clay soup pot",new Color(.36f,.2f,.12f));var bamboo=DayMaterial("Bamboo chopsticks",new Color(.72f,.62f,.38f));
            foreach(var prop in supperTrack.props)
            {
                bool stick=prop.shape=="stick";float r=stick?0:prop.size[0],h=stick?0:prop.size[1];
                var art=Part(mealRoot,prop.name,Vector3.zero,stick?new Vector3(.006f,.006f,.23f):new Vector3(2*r,h/2,2*r),stick?bamboo:prop.name=="Noi_canh"?clay:ceramic,stick?PrimitiveType.Cube:PrimitiveType.Cylinder);
                supperArt.Add((prop,art));if(prop.name=="To_ca")fishBowl=art;if(prop.name=="Noi_canh")pot=art;
            }
            SampleSupperProps(0);
        }
        void SampleSupperProps(float time)
        {
            if(supperTrack==null)return;
            float frame=Mathf.Clamp(time*supperTrack.fps,0,supperTrack.frames-1);int a=Mathf.FloorToInt(frame),b=Mathf.Min(a+1,supperTrack.frames-1);float k=frame-a;
            foreach(var (data,art) in supperArt)
            {
                Vector3 P(int i)=>new Vector3(data.pos[i*3],data.pos[i*3+1],data.pos[i*3+2]);Quaternion Q(int i)=>new Quaternion(data.rot[i*4],data.rot[i*4+1],data.rot[i*4+2],data.rot[i*4+3]);
                art.SetLocalPositionAndRotation(Vector3.Lerp(P(a),P(b),k),Quaternion.Slerp(Q(a),Q(b),k));
            }
        }
        /// <summary>Where an actor's root must stand so his body (the animator the take was made round) lands on the seat.</summary>
        static void AlignBody(Transform root,Transform body,Vector3 position,Quaternion rotation,out Vector3 rootPosition,out Quaternion rootRotation)
        {
            var relative=Quaternion.Inverse(root.rotation)*body.rotation;var offset=Quaternion.Inverse(root.rotation)*(body.position-root.position);
            rootRotation=rotation*Quaternion.Inverse(relative);rootPosition=position-rootRotation*offset;
        }
        // Hùng's game model is scaled to Nam's height and lifted by its sole offset; the supper was made with both at their
        // own size on the floor, so for it his body goes back to its native size (under the arrival fade) and comes back after.
        void HungNativeBody(bool native)
        {
            if(native==hungBodyNative)return;var animator=roles["Hùng"].Animator;if(animator==null)return;
            if(native){hungBody=animator.transform;hungBodyPosition=hungBody.localPosition;hungBodyScale=hungBody.localScale;hungBody.localPosition=Vector3.zero;hungBody.localScale=Vector3.one;}
            else if(hungBody!=null){hungBody.localPosition=hungBodyPosition;hungBody.localScale=hungBodyScale;}
            hungBodyNative=native;
        }
        // Nam eats with his pack and webbing off, as the scene was made; they go back on when he stands.
        readonly List<Renderer> supperGear=new();
        void NamSupperGear(bool worn)
        {
            if(worn){foreach(var gear in supperGear)if(gear!=null)gear.forceRenderingOff=false;supperGear.Clear();return;}
            if(supperGear.Count>0||dayNamAnimator==null)return;
            foreach(var skin in dayNamAnimator.GetComponentsInChildren<SkinnedMeshRenderer>())if((skin.name=="Nam_BaloRanRi"||skin.name=="Nam_Gear")&&!skin.forceRenderingOff){skin.forceRenderingOff=true;supperGear.Add(skin);}
        }
        // Group C: his rifle laid down on the mat at his left as he sits to eat (the pack and webbing come off with it).
        Transform supperRifle;Vector3 rifleBarrel,rifleSide,rifleCentre;readonly List<Mesh> dayMeshes=new();
        void LaySupperRifle(bool shown)
        {
            if(!shown){if(supperRifle!=null)supperRifle.gameObject.SetActive(false);return;}
            if(supperRifle==null)
            {
                var gun=combat.WeaponFor(ShadowVale.Gameplay.Combat.WeaponKind.Rifle);if(gun==null)return;
                supperRifle=new GameObject("Nam's rifle laid on the mat — supper").transform;supperRifle.SetParent(mealRoot,false);supperRifle.localScale=gun.transform.lossyScale;
                var extent=new Bounds();bool measured=false;
                foreach(var renderer in gun.GetComponentsInChildren<Renderer>(true))
                {
                    if(!ActiveWeaponPart(renderer.transform,gun.transform))continue;var filter=renderer.GetComponent<MeshFilter>();Mesh mesh=filter!=null?filter.sharedMesh:null;
                    if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);dayMeshes.Add(mesh);}   // its own: the sling's are freed every frame
                    if(mesh==null)continue;
                    var go=new GameObject(renderer.name);go.transform.SetParent(supperRifle,false);go.transform.localPosition=gun.transform.InverseTransformPoint(renderer.transform.position);go.transform.localRotation=Quaternion.Inverse(gun.transform.rotation)*renderer.transform.rotation;
                    var s=renderer.transform.lossyScale;var g=gun.transform.lossyScale;go.transform.localScale=new Vector3(s.x/g.x,s.y/g.y,s.z/g.z);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                    var b=mesh.bounds;for(int c=0;c<8;c++){var corner=gun.transform.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1))));if(measured)extent.Encapsulate(corner);else{extent=new Bounds(corner,Vector3.zero);measured=true;}}
                }
                var size=extent.size;int along=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2,across=size.x<=size.y&&size.x<=size.z?0:size.y<=size.z?1:2;
                var muzzle=gun.transform.InverseTransformPoint(gun.Muzzle.position);rifleBarrel=rifleSide=Vector3.zero;rifleBarrel[along]=muzzle[along]>=extent.center[along]?1:-1;rifleSide[across]=1;rifleCentre=extent.center;
                if(!measured||along==across){rifleBarrel=Vector3.forward;rifleSide=Vector3.right;}
            }
            supperRifle.gameObject.SetActive(true);
            supperRifle.rotation=Quaternion.LookRotation(porchOut,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(rifleBarrel,rifleSide));   // barrel to the yard, flat on its side
            supperRifle.position+=mealRoot.TransformPoint(new Vector3(.82f,.035f,-.9f))-supperRifle.TransformPoint(rifleCentre);
        }
        void SampleSupper()
        {
            var hung=roles["Hùng"];float t=MealTime;
            if(t<0)
            {
                bool namArrived=sceneClock>=PathDuration(dayNamPath),hungArrived=sceneClock>=PathDuration(dayHungPath);
                namPose?.Sample(namArrived?namIdle:ordinaryWalk,sceneClock,true);
                hung.SamplePerformance(hungArrived?DayClip("InjuredIdle"):HungDayWalk,hungArrived?sceneClock:sceneClock*1.25f/HungDayWalkSpeed,true);
                if(hungArrived)hung.transform.rotation=Quaternion.RotateTowards(hung.transform.rotation,MealFacing,220*Time.deltaTime);
                SampleSupperProps(0);return;
            }
            HungNativeBody(true);NamSupperGear(false);LaySupperRifle(true);
            namPose?.Sample(DayClip("LoiHuaNam")??namIdle,t,false);hung.SamplePerformance(DayClip("LoiHuaHung")??DayClip("RestHung"),t,false);
            AlignBody(hung.transform,hung.Animator.transform,MealSeat(true),MealFacing,out var hp,out var hr);hung.transform.SetPositionAndRotation(hp,hr);
            SampleSupperProps(t);
        }
        void SeatNam()
        {
            if(MealTime<0||dayNamAnimator==null)return;
            AlignBody(Mission.Player,dayNamAnimator.transform,MealSeat(false),MealFacing,out lockedPosition,out lockedRotation);
        }
        // The shots of the Blender timeline, cut on the same clock (fov as the game shows it; Blender's wide was tighter for review).
        void MealShot(out Vector3 eye,out Vector3 focus,out float fov)
        {
            float t=MealTime;
            Vector3 e,f;
            if(t>=32.9f&&t<41.2f||t>=43.6f&&t<46.2f){e=new Vector3(-1.9f,1.1f,0);f=new Vector3(.62f,.72f,0);fov=40;}          // the bump and the joke; "Nói rồi đó nha"
            else if(t>=58.4f&&t<63.4f){e=new Vector3(-.2f,.7f,-.9f);f=new Vector3(.74f,.8f,-.36f);fov=32;}                    // "Quê tôi... không còn ai đợi": low, under his helmet's brim, his head is bowed
            else{e=new Vector3(-4.6f,1.15f,0);f=new Vector3(.5f,.55f,0);fov=30;}                                              // through the doorway
            eye=mealRoot.TransformPoint(e);focus=mealRoot.TransformPoint(f);
        }
        /// <summary>A short dip to black as both sit, so standing never pops into the cross-legged take on screen.</summary>
        float MealFade
        {
            get
            {
                if(sceneTask?.id!="N1_01"||dayApproachDuration<.6f)return 0;float t=MealTime;
                return t<-.45f?0:t<0?Mathf.InverseLerp(-.45f,0,t):t<.15f?1:Mathf.Clamp01(1-(t-.15f)/.45f);
            }
        }
        // The porch deck in front of the doorway, measured out from the door until the boards end.
        float PorchDepth()
        {
            float depth=0;
            for(float d=.3f;d<4;d+=.1f)
            {
                var probe=porchDoor+porchOut*d;bool deck=false;
                foreach(var hit in Physics.RaycastAll(probe+Vector3.up*.8f,Vector3.down,1.2f,~0,QueryTriggerInteraction.Ignore))
                    if(hit.normal.y>.8f&&Mathf.Abs(hit.point.y-porchDoor.y)<.06f&&hit.collider.GetComponentInParent<Map02ChapterActor>()==null&&!(hit.collider is CharacterController))deck=true;
                if(!deck)break;depth=d;
            }
            return depth;
        }
    }
}
