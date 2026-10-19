using System;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    [DefaultExecutionOrder(160)]
    public sealed class Map01HungRest:MonoBehaviour
    {
        bool mapOne;Map01Mission mission;Map01Quest quest;Map01HungVisual owner;GameObject bedActor,model;Transform cot,head;
        Renderer[] originals;bool[] previous;Collider[] colliders;bool[] colliderEnabled;AnimationClip clip;Vector3 anchor;Quaternion rotation;
        public bool IsResting=>bedActor!=null&&bedActor.activeSelf;
        public Vector3 DialogueFocus=>head!=null?head.position+Vector3.up*.12f-(cot!=null?cot.forward*.22f:Vector3.zero):anchor+Vector3.up*.3f;
        public Transform Cot=>cot;
        public void Initialize(Map01Mission m,Map01HungVisual v){mission=m;owner=v;quest=m.GetComponent<Map01Quest>();mapOne=m.gameObject.scene.name=="Map 1";}
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
            clip.SampleAnimation(model,clip.length-1.2f+Mathf.PingPong(Time.time*.25f,1.2f));
            bedActor.transform.SetPositionAndRotation(anchor,rotation);
        }
        void Create()
        {
            var data=Map01SupplyPerformance.Data;if(data==null||data.hungModel==null)return;
            var parent=mission.alliedCommanderPost!=null?mission.alliedCommanderPost.parent:null;if(parent==null)return;
            cot=parent.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Cot"||t.name.StartsWith("Cot Row ",StringComparison.Ordinal)).OrderBy(t=>(t.position-mission.hung.position).sqrMagnitude).FirstOrDefault();
            if(cot==null){Debug.LogWarning("Hung resting pose requires a cot in the command shelter.",this);return;}
            originals=owner.actor.GetComponentsInChildren<Renderer>(true);previous=originals.Select(r=>r.forceRenderingOff).ToArray();colliders=mission.hung.GetComponentsInChildren<Collider>(true);colliderEnabled=colliders.Select(c=>c.enabled).ToArray();foreach(var collider in colliders)collider.enabled=false;
            bedActor=Instantiate(data.hungModel);bedActor.name="Hung resting on cot";model=bedActor.transform.GetChild(0).gameObject;clip=data.hungRest;clip.SampleAnimation(model,clip.length);
            var nodes=bedActor.GetComponentsInChildren<Transform>();head=nodes.First(t=>t.name=="Head");var feet=nodes.Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").ToArray();
            var towardHead=Vector3.ProjectOnPlane(head.position-(feet[0].position+feet[1].position)*.5f,Vector3.up).normalized;
            bedActor.transform.rotation=Quaternion.FromToRotation(towardHead,Vector3.ProjectOnPlane(cot.forward,Vector3.up).normalized)*bedActor.transform.rotation;
            var mesh=new Mesh();Bounds bounds=default;bool measured=false;
            foreach(var skin in bedActor.GetComponentsInChildren<SkinnedMeshRenderer>()){
                skin.BakeMesh(mesh);foreach(var vertex in mesh.vertices){var world=skin.transform.TransformPoint(vertex);if(!measured){bounds=new Bounds(world,Vector3.zero);measured=true;}else bounds.Encapsulate(world);}
            }
            Destroy(mesh);
            var renderers=cot.GetComponentsInChildren<Renderer>();
            var canvas=renderers.FirstOrDefault(r=>r.name.IndexOf("canvas",StringComparison.OrdinalIgnoreCase)>=0||r.sharedMaterials.Any(m=>m!=null&&m.name.IndexOf("canvas",StringComparison.OrdinalIgnoreCase)>=0));
            var surface=canvas!=null?canvas.bounds:renderers.Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            float top=canvas!=null?surface.max.y:surface.max.y-.035f;
            bedActor.transform.position+=new Vector3(surface.center.x-bounds.center.x,top+.008f-bounds.min.y,surface.center.z-bounds.center.z);
            anchor=bedActor.transform.position;rotation=bedActor.transform.rotation;
            foreach(var r in originals)r.forceRenderingOff=true;
        }
        void OnDisable(){if(bedActor!=null)bedActor.SetActive(false);if(originals!=null)for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].forceRenderingOff=previous[i];if(colliders!=null)for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=colliderEnabled[i];}
        void OnDestroy(){if(bedActor!=null)Destroy(bedActor);}
    }
}
