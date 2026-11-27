using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    [DefaultExecutionOrder(160)]
    public sealed class Map01HungRest:MonoBehaviour
    {
        bool mapOne;Map01Mission mission;Map01Quest quest;Map01HungVisual owner;GameObject bedActor,model;Transform cot,head;
        Renderer[] originals;bool[] previous;Collider[] colliders;bool[] colliderEnabled;AnimationClip clip;Vector3 anchor;Quaternion rotation;
        Map01CinematicPose pose;HumanPoseHandler handler;HumanPose human;float nod;
        Transform[] thighs,shins,soles;Vector3[] footRest;readonly List<Mesh> stowed=new();
        public bool IsResting=>bedActor!=null&&bedActor.activeSelf;
        public Vector3 DialogueFocus=>head!=null?head.position+Vector3.up*.12f-(cot!=null?cot.forward*.22f:Vector3.zero):anchor+Vector3.up*.3f;
        public Transform Cot=>cot;
        /// <summary>How the cot pose was fitted (pillow, chosen neck bend, each tried lift), for reviews.</summary>
        public string FitReport{get;private set;}
        public void Initialize(Map01Mission m,Map01HungVisual v){mission=m;owner=v;quest=m.GetComponent<Map01Quest>();mapOne=m.gameObject.scene.name=="Map 1";}

        // The reviewed cot clip (old Hùng rig) keeps the breathing; on top of it the director's 26/11 notes:
        // legs out straight with the feet falling open, both arms resting on the canvas along his sides, and the
        // head lying on the folded cover with the neck bent only as far as the cover needs.
        static readonly (string muscle,float value)[] Settled={
            ("Left Upper Leg Front-Back",.65f),("Right Upper Leg Front-Back",.65f),("Left Lower Leg Stretch",.95f),("Right Lower Leg Stretch",.95f),
            ("Left Upper Leg In-Out",.08f),("Right Upper Leg In-Out",.08f),("Left Upper Leg Twist In-Out",-.2f),("Right Upper Leg Twist In-Out",-.2f),
            // 26/11 again: "lưng chưa nằm thẳng" - the old clip's spine keeps a bend, lean and twist; lying on a cot the
            // back is straight, so every spine and neck muscle returns to neutral before the pillow nod is added.
            ("Spine Front-Back",0),("Spine Left-Right",0),("Spine Twist Left-Right",0),
            ("Chest Front-Back",0),("Chest Left-Right",0),("Chest Twist Left-Right",0),
            ("UpperChest Front-Back",0),("UpperChest Left-Right",0),("UpperChest Twist Left-Right",0),
            ("Neck Nod Down-Up",0),("Neck Tilt Left-Right",0),("Neck Turn Left-Right",0),
            ("Head Nod Down-Up",0),("Head Tilt Left-Right",0),("Head Turn Left-Right",0),
            // 27/11: "tay kê dưới đít", "tay xiên người" - the clip's left hand was tucked under his buttock and the right
            // upper arm ran through his ribs (~2100 skin vertices inside the body). Reference pose for a man resting on
            // his back: both arms lie on the canvas along his sides, elbows softly bent, hands beside the hips, palms
            // turned down. Found on the V119 cot copy by two-bone IK to targets measured on his own body (wrist beside
            // the hip on the canvas, elbow on the canvas outside the ribs), read back as muscles and refined so no arm
            // skin lies inside the body and no muscle sits near its limit (all within ±.53).
            ("Left Shoulder Down-Up",-.436f),("Left Shoulder Front-Back",-.018f),("Left Arm Down-Up",-.42f),("Left Arm Front-Back",.337f),("Left Arm Twist In-Out",-.471f),
            ("Left Forearm Stretch",.662f),("Left Forearm Twist In-Out",.403f),("Left Hand Down-Up",.35f),("Left Hand In-Out",-.407f),
            ("Right Shoulder Down-Up",-.367f),("Right Shoulder Front-Back",-.156f),("Right Arm Down-Up",-.521f),("Right Arm Front-Back",.304f),("Right Arm Twist In-Out",-.22f),
            ("Right Forearm Stretch",.769f),("Right Forearm Twist In-Out",.135f),("Right Hand Down-Up",.049f),("Right Hand In-Out",-.346f)};
        static readonly (string muscle,float value)[] Nod={("UpperChest Front-Back",-.5f),("Neck Nod Down-Up",-.7f),("Head Nod Down-Up",-.4f)};
        static int[] settledIndex,nodIndex;

        /// <summary>
        /// 27/11: V119 is one welded Meshy skin, belt pouches included; lying on his back the big rear pouch propped
        /// him 10 cm off the canvas. For the cot copy only, the pouch vertices (bind space, belt-height band, skinned to
        /// the body, never an arm or finger) that stand out behind his back are pressed onto the plane of his back.
        /// Bones and weights stay as they are.
        /// </summary>
        public static Mesh StowPouches(SkinnedMeshRenderer skin)
        {
            var source=skin.sharedMesh;if(source==null||!source.isReadable)return null;
            var bones=skin.bones;var weights=source.boneWeights;var v=source.vertices;int moved=0;
            // The hands hang beside the side pouches: anything skinned mainly to an arm or a finger is left alone.
            string[] limbs={"Arm","Hand","Thumb","Index","Middle","Ring","Pinky"};
            bool Body(int i){var b=weights[i].boneIndex0;if(b<0||b>=bones.Length||bones[b]==null)return false;var n=bones[b].name;return !limbs.Any(l=>n.Contains(l));}
            for(int i=0;i<v.Length;i++){
                var p=v[i];if(p.y<PouchLow||p.y>PouchHigh||p.z<=PouchBack||Mathf.Abs(p.x)>PouchReach||!Body(i))continue;
                // The rear pouch goes flat into his back; the side pouches stay at his sides, only their rear half is
                // pressed in so neither props him off the canvas.
                v[i]=new Vector3(p.x,p.y,PouchBack);moved++;
            }
            if(moved==0)return null;
            var mesh=Instantiate(source);mesh.name=source.name+" (pouches stowed)";mesh.vertices=v;mesh.RecalculateBounds();
            return mesh;
        }
        // Bind-space band of the belt pouches on Hung_V119 (metres): measured from the rest mesh on 27/11.
        const float PouchLow=.76f,PouchHigh=.99f,PouchBack=.075f,PouchReach=.3f;

        void LateUpdate()
        {
            if(!mapOne||mission==null||quest==null)return;
            bool rest=quest.Stage>=Map01Quest.BriefingStage&&quest.Stage<Map01Quest.ReportBossStage&&!(mission.GetComponent<Map01Rescue>()?.HungDown??false);
            if(rest&&bedActor==null)Create();
            if(bedActor==null)return;
            if(bedActor.activeSelf!=rest){bedActor.SetActive(rest);for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].forceRenderingOff=rest||previous[i];for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=!rest&&colliderEnabled[i];}
            if(!rest)return;
            for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].forceRenderingOff=true;
            // A quiet breathing pose remains lying down even while the dialogue system marks him speaking.
            Settle(clip.length-1.2f+Mathf.PingPong(Time.time*.25f,1.2f),nod);
            bedActor.transform.SetPositionAndRotation(anchor,rotation);
            PlantFeet();
        }
        void Settle(float time,float bend)
        {
            pose.Sample(clip,time);
            handler.GetHumanPose(ref human);
            for(int i=0;i<Settled.Length;i++)if(settledIndex[i]>=0)human.muscles[settledIndex[i]]=Settled[i].value;
            for(int i=0;i<Nod.Length;i++)if(nodIndex[i]>=0)human.muscles[nodIndex[i]]+=Nod[i].value*bend;
            handler.SetHumanPose(ref human);
        }
        // Heels rest on the canvas whatever the clip's hip height on V119's longer legs.
        void PlantFeet(){
            if(footRest==null)return;
            for(int i=0;i<2;i++)if(thighs[i]!=null&&shins[i]!=null&&soles[i]!=null)
                Map01ArmContact.ReachLimb(thighs[i],shins[i],soles[i],bedActor.transform.TransformPoint(footRest[i]),shins[i].position+Vector3.up*.3f,bedActor.transform.forward,-Vector3.up,1);
        }
        void Create()
        {
            var data=Map01SupplyPerformance.Data;if(data==null||data.hungRestHuman==null||owner.actor==null)return;
            var parent=mission.alliedCommanderPost!=null?mission.alliedCommanderPost.parent:null;if(parent==null)return;
            cot=parent.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Cot"||t.name.StartsWith("Cot Row ",StringComparison.Ordinal)).OrderBy(t=>(t.position-mission.hung.position).sqrMagnitude).FirstOrDefault();
            if(cot==null){Debug.LogWarning("Hung resting pose requires a cot in the command shelter.",this);return;}
            originals=owner.actor.GetComponentsInChildren<Renderer>(true);previous=originals.Select(r=>r.forceRenderingOff).ToArray();colliders=mission.hung.GetComponentsInChildren<Collider>(true);colliderEnabled=colliders.Select(c=>c.enabled).ToArray();foreach(var collider in colliders)collider.enabled=false;
            // The live V119 Hùng is cloned and posed with the Humanoid copy of the reviewed cot clip, so the
            // resting body is the same model, skin and weights as the one that walked home.
            bedActor=new GameObject("Hung resting on cot");var source=owner.actor.transform;
            model=Instantiate(source.gameObject,bedActor.transform,false);model.name=source.name;
            model.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=source.lossyScale;
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=false;
            foreach(var s in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){var flat=StowPouches(s);if(flat!=null){s.sharedMesh=flat;stowed.Add(flat);}}
            var restActor=model.GetComponent<Animator>();restActor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            pose=new Map01CinematicPose(restActor,null,false);clip=data.hungRestHuman;
            handler=new HumanPoseHandler(restActor.avatar,restActor.transform);human=new HumanPose();
            var names=HumanTrait.MuscleName;settledIndex??=Settled.Select(m=>Array.IndexOf(names,m.muscle)).ToArray();nodIndex??=Nod.Select(m=>Array.IndexOf(names,m.muscle)).ToArray();
            Settle(clip.length,0);
            head=restActor.GetBoneTransform(HumanBodyBones.Head);var hips=restActor.GetBoneTransform(HumanBodyBones.Hips);
            soles=new[]{restActor.GetBoneTransform(HumanBodyBones.LeftFoot),restActor.GetBoneTransform(HumanBodyBones.RightFoot)};
            var lengthwise=Vector3.ProjectOnPlane(cot.forward,Vector3.up).normalized;var across=Vector3.Cross(Vector3.up,lengthwise);
            var towardHead=Vector3.ProjectOnPlane(head.position-(soles[0].position+soles[1].position)*.5f,Vector3.up).normalized;
            bedActor.transform.rotation=Quaternion.FromToRotation(towardHead,lengthwise)*bedActor.transform.rotation;
            var renderers=cot.GetComponentsInChildren<Renderer>();
            var canvas=renderers.FirstOrDefault(r=>r.name.IndexOf("canvas",StringComparison.OrdinalIgnoreCase)>=0||r.sharedMaterials.Any(m=>m!=null&&m.name.IndexOf("canvas",StringComparison.OrdinalIgnoreCase)>=0));
            var surface=canvas!=null?canvas.bounds:renderers.Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            float top=canvas!=null?surface.max.y:surface.max.y-.035f;
            var skins=bedActor.GetComponentsInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();var points=new List<Vector3>();var buffer=new List<Vector3>();var onArm=new List<bool>();
            // BakeMesh(useScale:true) already applies the skin's 1.08 scale: map to world without scaling again.
            // The arms lie on the canvas beside him: only his body decides how high he lies and how far the head sinks.
            var arms=new HashSet<Transform>(new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm}.Select(b=>restActor.GetBoneTransform(b)).Where(t=>t!=null).SelectMany(t=>t.GetComponentsInChildren<Transform>(true)));
            var armVertex=skins.Select(s=>{var bones=s.bones;return s.sharedMesh!=null&&s.sharedMesh.isReadable?s.sharedMesh.boneWeights.Select(w=>w.boneIndex0>=0&&w.boneIndex0<bones.Length&&arms.Contains(bones[w.boneIndex0])).ToArray():new bool[0];}).ToArray();
            void Bake(){points.Clear();onArm.Clear();for(int s=0;s<skins.Length;s++){skins[s].BakeMesh(mesh,true);mesh.GetVertices(buffer);var m=Matrix4x4.TRS(skins[s].transform.position,skins[s].transform.rotation,Vector3.one);for(int i=0;i<buffer.Count;i++){points.Add(m.MultiplyPoint3x4(buffer[i]));onArm.Add(i<armVertex[s].Length&&armVertex[s][i]);}}}
            float Lowest(Func<Vector3,bool> where){float low=float.PositiveInfinity;for(int i=0;i<points.Count;i++){var w=points[i];if(w.y<low&&!onArm[i]&&where(w))low=w.y;}return low;}
            // The cot carries him on his back and seat: V119's hip satchel hangs lower when he lies down.
            bool Back(Vector3 w){var d=w-hips.position;return Mathf.Abs(Vector3.Dot(d,across))<.11f&&Vector3.Dot(d,lengthwise)>-.2f&&Vector3.Dot(d,lengthwise)<.45f;}
            bool Skull(Vector3 w){var d=w-head.position;return Vector3.Dot(d,lengthwise)>-.01f&&Mathf.Abs(Vector3.Dot(d,across))<.09f;}
            void Place(){
                Bake();var bounds=new Bounds(points[0],Vector3.zero);foreach(var w in points)bounds.Encapsulate(w);
                float back=Lowest(Back);
                // His weight presses the canvas: the lowest point of his back sits 8 mm into it, so the seat does not float.
                bedActor.transform.position+=new Vector3(surface.center.x-bounds.center.x,top-.008f-(float.IsInfinity(back)?bounds.min.y:back),surface.center.z-bounds.center.z);
            }
            Place();
            // Bend the neck and upper chest just enough for the head to sink 3 cm into the folded cover.
            var headAt=head.position;
            float pillow=renderers.Where(r=>r!=canvas&&r.bounds.min.x<=headAt.x&&r.bounds.max.x>=headAt.x&&r.bounds.min.z<=headAt.z&&r.bounds.max.z>=headAt.z).Select(r=>r.bounds.max.y).DefaultIfEmpty(top).Max();
            if(pillow>top+.03f){
                float best=float.MaxValue;var trace=new System.Text.StringBuilder($"canvas {top:F3} pillow {pillow:F3} target lift {pillow-.03f-top:F3}:");
                for(float k=0;k<=1.21f;k+=.1f){
                    Settle(clip.length,k);Bake();float lift=Lowest(Skull)-Lowest(Back),error=Mathf.Abs(lift-(pillow-.03f-top));trace.Append($" k{k:F1}={lift:F3}");
                    if(error<best){best=error;nod=k;}
                }
                FitReport=trace.Append($" -> k{nod:F1}").ToString();Settle(clip.length,nod);Place();
            }
            thighs=new[]{restActor.GetBoneTransform(HumanBodyBones.LeftUpperLeg),restActor.GetBoneTransform(HumanBodyBones.RightUpperLeg)};
            shins=new[]{restActor.GetBoneTransform(HumanBodyBones.LeftLowerLeg),restActor.GetBoneTransform(HumanBodyBones.RightLowerLeg)};
            footRest=new Vector3[2];Bake();
            for(int i=0;i<2;i++){var at=soles[i].position;float sole=Lowest(w=>{var d=w-at;d.y=0;return d.magnitude<.1f;});footRest[i]=bedActor.transform.InverseTransformPoint(at+Vector3.up*(float.IsInfinity(sole)?0:top+.004f-sole));}
            Destroy(mesh);
            PlantFeet();
            anchor=bedActor.transform.position;rotation=bedActor.transform.rotation;
            foreach(var r in originals)r.forceRenderingOff=true;
        }
        void OnDisable(){if(bedActor!=null)bedActor.SetActive(false);if(originals!=null)for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].forceRenderingOff=previous[i];if(colliders!=null)for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=colliderEnabled[i];}
        void OnDestroy(){handler?.Dispose();handler=null;pose?.Dispose();pose=null;if(bedActor!=null)Destroy(bedActor);foreach(var m in stowed)if(m!=null)Destroy(m);stowed.Clear();}
    }
}
