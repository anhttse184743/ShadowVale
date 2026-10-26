using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // The duck in the bushes (doc, Night 1; approved take 2026-10-24, hung_mixamo_omtay_giatminh.py): it flaps, Hùng jolts
        // (Mixamo "Terrified", 10 frames after the flap) and grabs Nam's right shoulder with his good hand from behind on
        // the left; Nam looks back over his shoulder, "Vịt thôi." / "Biết mà." and Hùng lets go. Nam stands still for it.
        float duckClock=-1;bool duckGrab;Map02ChapterActor duckHung;Vector3 duckFrom,duckSpot,duckNamForward;Quaternion duckFromRotation;int duckSpoken;
        const float DuckJolt=10/30f;
        static readonly (string id,float at)[] DuckLines={("m2v3_duck_01",DuckJolt+1.25f),("m2v3_duck_02",DuckJolt+2.65f)};
        public bool DuckStartled=>duckClock>=0;
        float DuckLength=>DayClip(duckGrab?"HungStartleGrab":"HungStartle")?.length??3.2f;
        void BeginDuckStartle()
        {
            PlayDaySound("duck",.9f,Mission.Player.position+Mission.Player.right*2.6f+Vector3.up*.4f);duckHung=escort;duckClock=0;duckSpoken=0;   // out of the bush on their right
            var nam=Mission.Player;duckNamForward=Vector3.ProjectOnPlane(nam.forward,Vector3.up).normalized;
            // Where the take has him: a step behind Nam and 0.40 m to his right, facing the way Nam faces.
            duckSpot=nam.position-duckNamForward*.62f+Vector3.Cross(Vector3.up,duckNamForward)*.40f;
            duckFrom=duckHung.transform.position;duckFromRotation=duckHung.transform.rotation;
            duckGrab=DayClip("HungStartleGrab")!=null&&Vector3.Distance(Vector3.ProjectOnPlane(duckFrom-duckSpot,Vector3.up),Vector3.zero)<1.6f;   // farther back he only jolts
            duckHung.ConversationHeld=true;player.RestoreMotion(false);
        }
        void TickDuck(float dt)
        {
            if(duckClock<0)return;
            if(duckHung==null||State.phase!=1||sceneTask!=null){EndDuck();return;}
            duckClock+=dt;
            while(duckSpoken<DuckLines.Length&&duckClock>=DuckLines[duckSpoken].at)
            {
                string id=DuckLines[duckSpoken].id;var entry=Map02ChapterDefinition.Load().Lines("duckevent").FirstOrDefault(l=>l.id==id);duckSpoken++;
                if(entry==null)continue;Mission.Say(entry.Caption);if(Map02DialogueCatalog.HasApprovedRecording(entry))voice.Play(entry.RecordingId);
            }
            if(duckClock>=DuckLength)EndDuck();
        }
        void EndDuck(){if(duckHung!=null)duckHung.ConversationHeld=false;duckHung=null;duckClock=-1;}
        void DuckLateUpdate()
        {
            if(duckClock<0||duckHung==null)return;
            float k=Mathf.SmoothStep(0,1,duckClock/.5f);   // his lurch toward Nam lands with the jolt
            var goal=duckGrab?duckSpot:duckFrom;var facing=duckGrab?Quaternion.LookRotation(duckNamForward):duckFromRotation;
            duckHung.transform.SetPositionAndRotation(Vector3.Lerp(duckFrom,goal,k),Quaternion.Slerp(duckFromRotation,facing,k));
            duckHung.SamplePerformance(DayClip(duckGrab?"HungStartleGrab":"HungStartle"),duckClock,false);duckHung.GroundPerformance(duckHung.transform.position.y);
            float frame=duckClock*30;var body=duckHung.Animator;var nam=dayNamAnimator;if(body==null||nam==null||!body.isHuman||!nam.isHuman)return;
            if(duckGrab)
            {
                // The take was made at Hùng's own size, his hand on Nam's shoulder; at his game size the same reach is solved
                // onto Nam's real shoulder (the hand target moves, the joints follow: no joint is slerped between far poses).
                float w=Smooth((frame-9)/6)*(1-Smooth((frame-88)/10));var shoulder=nam.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if(shoulder!=null)Reach(body,shoulder.position+Vector3.up*.07f-duckNamForward*.07f,w);
                // Nam turns his head back to him as he is grabbed, and away again after "Biết mà".
                LookBack(nam,duckHung.transform.position,Smooth((frame-22)/14)*(1-Smooth((frame-100)/20)));
            }
        }
        static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        static void Reach(Animator body,Vector3 target,float weight)=>Reach(body,true,target,weight);
        /// <summary>Two-bone reach of a hand to a wrist target, keeping the elbow on the side the take bends it and the hand's own turn.</summary>
        internal static void Reach(Animator body,bool left,Vector3 target,float weight)
        {
            if(weight<=0)return;
            var a=body.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var b=body.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var c=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);if(a==null||b==null||c==null)return;
            target=Vector3.Lerp(c.position,target,weight);
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
            var axis=target-a.position;float d=Mathf.Clamp(axis.magnitude,Mathf.Abs(l1-l2)+.001f,(l1+l2)*.995f);axis.Normalize();
            var bend=Vector3.ProjectOnPlane(b.position-a.position,axis);if(bend.sqrMagnitude<1e-6f)bend=Vector3.ProjectOnPlane(-body.transform.up,axis);bend.Normalize();
            float x=(l1*l1-l2*l2+d*d)/(2*d);var elbow=a.position+axis*x+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-x*x));
            var hand=c.rotation;
            a.rotation=Quaternion.FromToRotation(b.position-a.position,elbow-a.position)*a.rotation;
            b.rotation=Quaternion.FromToRotation(c.position-b.position,a.position+axis*d-b.position)*b.rotation;c.rotation=hand;
        }
        /// <summary>Turn Nam's neck and head (yaw only, after his animator has posed him) toward a point behind him.</summary>
        static void LookBack(Animator nam,Vector3 point,float weight)
        {
            if(weight<=0)return;var neck=nam.GetBoneTransform(HumanBodyBones.Neck);var head=nam.GetBoneTransform(HumanBodyBones.Head);if(neck==null||head==null)return;
            var forward=Vector3.ProjectOnPlane(nam.transform.forward,Vector3.up);var to=Vector3.ProjectOnPlane(point-head.position,Vector3.up);
            float yaw=Mathf.Clamp(Vector3.SignedAngle(forward,to,Vector3.up),-60,60)*weight;
            neck.rotation=Quaternion.AngleAxis(yaw*.4f,Vector3.up)*neck.rotation;head.rotation=Quaternion.AngleAxis(yaw*.6f,Vector3.up)*head.rotation;
        }
    }
}
