using UnityEngine;
namespace ShadowVale.Map01 {
    /// <summary>Alternating planted feet during the short root-guided grapple only.</summary>
    public sealed class Map01GrappleFeet {
        sealed class Foot { public Transform upper,lower,end;public Vector3 local,anchor,from,to;public Quaternion relative,rotation,fromRotation,toRotation;public float height; }
        readonly Foot[] feet=new Foot[2];readonly Transform root;readonly Map01Mission mission;
        int moving=-1,last=1;float elapsed;
        const float StepSeconds=.27f;
        public Map01GrappleFeet(Animator actor,Transform root,Map01Mission mission){
            this.root=root;this.mission=mission;
            for(int i=0;i<2;i++){
                var end=actor.GetBoneTransform(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                feet[i]=new Foot{upper=actor.GetBoneTransform(i==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg),lower=actor.GetBoneTransform(i==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg),end=end,
                    local=root.InverseTransformPoint(end.position),height=Mathf.Clamp(end.position.y-root.position.y,.04f,.18f),anchor=end.position,rotation=end.rotation,relative=Quaternion.Inverse(root.rotation)*end.rotation};
            }
        }
        public void Sample(float dt){
            for(int i=0;i<2;i++){
                var f=feet[i];var wanted=root.TransformPoint(f.local);wanted.y=root.position.y;
                var normal=Vector3.up;float floor=root.position.y;
                foreach(var hit in Physics.RaycastAll(wanted+Vector3.up*.6f,Vector3.down,1.5f,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                    if(hit.normal.y>.65f&&hit.point.y<=root.position.y+.25f&&hit.transform.GetComponentInParent<ShadowVale.Gameplay.Combat.Health>()==null){floor=hit.point.y;normal=hit.normal;break;}
                wanted.y=floor+f.height;var rotation=Quaternion.FromToRotation(Vector3.up,normal)*root.rotation*f.relative;
                if(moving<0&&i!=last&&(Vector3.ProjectOnPlane(wanted-f.anchor,Vector3.up).magnitude>.14f||Quaternion.Angle(f.rotation,rotation)>20)){
                    moving=i;elapsed=0;f.from=f.anchor;f.to=wanted;f.fromRotation=f.rotation;f.toRotation=rotation;
                }
            }
            if(moving>=0){
                var f=feet[moving];elapsed+=dt;float t=Mathf.Clamp01(elapsed/StepSeconds),s=t*t*(3-2*t);
                f.anchor=Vector3.Lerp(f.from,f.to,s)+Vector3.up*(.065f*Mathf.Sin(Mathf.PI*t));f.rotation=Quaternion.Slerp(f.fromRotation,f.toRotation,s);
                if(t>=1){last=moving;moving=-1;}
            }
            for(int i=0;i<2;i++)Solve(feet[i],i==0?-1:1);
        }
        void Solve(Foot foot,float side){
            var a=foot.upper;var b=foot.lower;var end=foot.end;var origin=a.position;
            float first=Vector3.Distance(origin,b.position),second=Vector3.Distance(b.position,end.position);
            var axis=(foot.anchor-origin).normalized;float distance=Mathf.Clamp(Vector3.Distance(origin,foot.anchor),Mathf.Abs(first-second)+.001f,(first+second)*.98f);
            var bend=Vector3.ProjectOnPlane(root.forward+root.right*(side*.15f),axis).normalized;
            float x=(first*first+distance*distance-second*second)/(2*distance);var knee=origin+axis*x+bend*Mathf.Sqrt(Mathf.Max(0,first*first-x*x));
            a.rotation=Quaternion.FromToRotation(b.position-origin,knee-origin)*a.rotation;
            b.rotation=Quaternion.FromToRotation(end.position-b.position,origin+axis*distance-b.position)*b.rotation;end.rotation=foot.rotation;
        }
    }
}
