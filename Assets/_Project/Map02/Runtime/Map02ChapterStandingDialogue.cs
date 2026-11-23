using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        readonly RaycastHit[] dialogueFloorHits=new RaycastHit[32];
        bool standingDialogue;
        float dialogueFloor;
        Animator dialogueNam;
        Mesh dialogueGroundMesh;
        readonly System.Collections.Generic.List<Vector3> dialogueGroundVertices=new();
        SkinnedMeshRenderer[] dialogueSkins;
        bool IsStandingDialogue(Map02ChapterTask task)=>task!=null&&task.kind!="cinematic"&&task.kind!="crawl"&&task.id!="D1_02"&&task.id!="D1_03";
        float StandingSurface(Vector3 at)
        {
            float floor=float.NegativeInfinity;
            int count=Physics.RaycastNonAlloc(at+Vector3.up*.65f,Vector3.down,dialogueFloorHits,2,~(1<<4),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){
                var hit=dialogueFloorHits[i];var collider=hit.collider;
                if(hit.normal.y<.65f||hit.point.y>at.y+.30f||collider.transform.IsChildOf(Mission.Player)||collider.GetComponentInParent<Map02ChapterActor>()!=null)continue;
                floor=Mathf.Max(floor,hit.point.y);
            }
            if(!float.IsNegativeInfinity(floor))return floor;
            return NavMesh.SamplePosition(at,out var nav,.6f,NavMesh.AllAreas)?nav.position.y:at.y;
        }
        void PrepareStandingDialogue(Map02ChapterTask task)
        {
            standingDialogue=IsStandingDialogue(task);
            if(!standingDialogue)return;
            dialogueNam=Mission.Player.GetComponentInChildren<Animator>();
            dialogueSkins=dialogueNam.GetComponentsInChildren<SkinnedMeshRenderer>();
            dialogueGroundMesh??=new Mesh();
            // The conversation can be far from the landing and on another height.
            dialogueFloor=StandingSurface(lockedPosition);lockedPosition.y=dialogueFloor;
        }
        Transform NamDialogueTarget()
        {
            if(lines==null||lines.Length==0)return null;
            var speaker=Speaker(lineIndex);
            if(speaker!=null&&speaker!=Mission.Player)return speaker;
            for(int i=lineIndex-1;i>=0;i--){var other=Speaker(i);if(other!=null&&other!=Mission.Player)return other;}
            for(int i=lineIndex+1;i<lines.Length;i++){var other=Speaker(i);if(other!=null&&other!=Mission.Player)return other;}
            return null;
        }
        void CorrectStandingDialogue()
        {
            if(!standingDialogue||sceneTask==null||dialogueNam==null)return;
            var target=NamDialogueTarget();
            if(target!=null&&Vector3.ProjectOnPlane(target.position-Mission.Player.position,Vector3.up).magnitude<14){
                var animator=target.GetComponentInChildren<Animator>();
                var targetHead=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
                var point=targetHead!=null?targetHead.position:target.position+Vector3.up*1.4f;
                var direction=Vector3.ProjectOnPlane(point-Mission.Player.position,Vector3.up);
                if(direction.sqrMagnitude>.01f){
                    var facing=BodyForward(dialogueNam,Mission.Player);
                    float delta=Vector3.SignedAngle(facing,direction,Vector3.up);
                    lockedRotation=Quaternion.AngleAxis(Mathf.Clamp(delta,-240*Time.deltaTime,240*Time.deltaTime),Vector3.up)*lockedRotation;
                    Mission.Player.rotation=lockedRotation;
                    var head=dialogueNam.GetBoneTransform(HumanBodyBones.Head);var neck=dialogueNam.GetBoneTransform(HumanBodyBones.Neck);
                    if(head!=null){
                        var look=point-head.position;var forward=BodyForward(dialogueNam,Mission.Player);
                        float yaw=Mathf.Clamp(Vector3.SignedAngle(forward,Vector3.ProjectOnPlane(look,Vector3.up),Vector3.up),-35,35);
                        float pitch=Mathf.Clamp(-Mathf.Atan2(look.y,Vector3.ProjectOnPlane(look,Vector3.up).magnitude)*Mathf.Rad2Deg,-15,20);
                        if(neck!=null)neck.rotation=Quaternion.AngleAxis(yaw*.35f,Vector3.up)*neck.rotation;
                        head.rotation=Quaternion.AngleAxis(yaw*.65f,Vector3.up)*Quaternion.AngleAxis(pitch,Vector3.Cross(Vector3.up,forward))*head.rotation;
                    }
                }
            }
            dialogueFloor=StandingSurface(lockedPosition);
            float sole=float.PositiveInfinity;
            var replacement=dialogueNam.transform.Find("Nam_AoBaBa_Visual");
            foreach(var skin in dialogueSkins){
                if(!skin.enabled||!skin.gameObject.activeInHierarchy||skin.forceRenderingOff||replacement!=null&&!skin.transform.IsChildOf(replacement))continue;
                skin.BakeMesh(dialogueGroundMesh,replacement!=null);dialogueGroundMesh.GetVertices(dialogueGroundVertices);
                foreach(var v in dialogueGroundVertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(v).y);
            }
            if(!float.IsInfinity(sole))dialogueNam.transform.position+=Vector3.up*Mathf.Clamp(dialogueFloor-sole,-2,2);
        }
    }
}
