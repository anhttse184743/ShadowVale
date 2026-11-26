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
        Transform[] thighs,shins,soles;Vector3[] footRest;
        public bool IsResting=>bedActor!=null&&bedActor.activeSelf;
        public Vector3 DialogueFocus=>head!=null?head.position+Vector3.up*.12f-(cot!=null?cot.forward*.22f:Vector3.zero):anchor+Vector3.up*.3f;
        public Transform Cot=>cot;
        /// <summary>How the cot pose was fitted (pillow, chosen neck bend, each tried lift), for reviews.</summary>
        public string FitReport{get;private set;}
        public void Initialize(Map01Mission m,Map01HungVisual v){mission=m;owner=v;quest=m.GetComponent<Map01Quest>();mapOne=m.gameObject.scene.name=="Map 1";}

        // The reviewed cot clip (old Hùng rig) keeps the breathing; on top of it the director's 26/11 notes:
        // legs out straight with the feet falling open, right hand resting on his belly, left arm along his side
        // on the canvas, and the head lying on the folded cover with the neck bent only as far as the cover needs.
        static readonly (string muscle,float value)[] Settled={
            ("Left Upper Leg Front-Back",.65f),("Right Upper Leg Front-Back",.65f),("Left Lower Leg Stretch",.95f),("Right Lower Leg Stretch",.95f),
            ("Left Upper Leg In-Out",.08f),("Right Upper Leg In-Out",.08f),("Left Upper Leg Twist In-Out",-.2f),("Right Upper Leg Twist In-Out",-.2f),
            ("Right Arm Front-Back",-.08f),("Right Forearm Stretch",.7f),("Right Hand Down-Up",-.35f),("Left Arm Front-Back",.2f),("Left Forearm Stretch",.95f)};
        static readonly (string muscle,float value)[] Nod={("UpperChest Front-Back",-.5f),("Neck Nod Down-Up",-.7f),("Head Nod Down-Up",-.4f)};
        static int[] settledIndex,nodIndex;

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
            for(int i=0;i<Settled.Length;i++)human.muscles[settledIndex[i]]=Settled[i].value;
            for(int i=0;i<Nod.Length;i++)human.muscles[nodIndex[i]]+=Nod[i].value*bend;
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
            var skins=bedActor.GetComponentsInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();var points=new List<Vector3>();var buffer=new List<Vector3>();
            void Bake(){points.Clear();foreach(var skin in skins){skin.BakeMesh(mesh,true);mesh.GetVertices(buffer);var m=skin.transform.localToWorldMatrix;foreach(var v in buffer)points.Add(m.MultiplyPoint3x4(v));}}
            float Lowest(Func<Vector3,bool> where){float low=float.PositiveInfinity;foreach(var w in points)if(w.y<low&&where(w))low=w.y;return low;}
            // The cot carries him on his back and seat: V119's hip satchel hangs lower when he lies down.
            bool Back(Vector3 w){var d=w-hips.position;return Mathf.Abs(Vector3.Dot(d,across))<.11f&&Vector3.Dot(d,lengthwise)>-.2f&&Vector3.Dot(d,lengthwise)<.45f;}
            bool Skull(Vector3 w){var d=w-head.position;return Vector3.Dot(d,lengthwise)>-.01f&&Mathf.Abs(Vector3.Dot(d,across))<.09f;}
            void Place(){
                Bake();var bounds=new Bounds(points[0],Vector3.zero);foreach(var w in points)bounds.Encapsulate(w);
                float back=Lowest(Back);
                bedActor.transform.position+=new Vector3(surface.center.x-bounds.center.x,top+.008f-(float.IsInfinity(back)?bounds.min.y:back),surface.center.z-bounds.center.z);
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
        void OnDestroy(){handler?.Dispose();handler=null;pose?.Dispose();pose=null;if(bedActor!=null)Destroy(bedActor);}
    }
}
