using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// N3_00 opens on the shift change the script has ("ngay sau khi Nam vừa giao ca"): as Nam comes to the rest spot the men
    /// there turn to him, he walks up to anh Lực, who takes the watch from him, and only then the screen goes dark (Nam lies
    /// down) and the attack comes. The attack's own beats (the black, the flare, the first shout) run on
    /// <see cref="AttackClock"/>, after the handover.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        const float Handover=4.5f,HandoverGap=1.1f,HandoverFade=.6f,HandoverPace=1.25f;
        Vector3 handFrom,handTo;
        readonly List<Map02ChapterActor> handWatchers=new List<Map02ChapterActor>();
        /// <summary>Seconds into N3_00's attack: negative during the handover.</summary>
        float AttackClock=>sceneTask?.id=="N3_00"?sceneClock-Handover:sceneClock;
        bool HandingOver=>sceneTask?.id=="N3_00"&&sceneClock<Handover;
        bool HandoverWalking=>HandingOver&&Flat(lockedPosition,handTo)>.05f;
        /// <summary>Where Nam stands to hand the watch to anh Lực.</summary>
        public Vector3 HandoverSpot=>handTo;
        public IReadOnlyList<Map02ChapterActor> HandoverWatchers=>handWatchers;

        void PrepareHandover()
        {
            handWatchers.Clear();var nam=Mission.Player.position;handFrom=nam;handTo=nam;
            foreach(var role in new[]{"Anh Lực","Hùng","Anh Thành"})
                if(roles.TryGetValue(role,out var a)&&a!=null&&a.visual!=null&&a.visual.gameObject.activeInHierarchy&&Flat(a.transform.position,nam)<10)handWatchers.Add(a);
            if(!roles.TryGetValue("Anh Lực",out var luc)||luc==null)return;
            var back=Vector3.ProjectOnPlane(nam-luc.transform.position,Vector3.up);if(back.sqrMagnitude<.01f)back=-luc.transform.forward;
            var spot=luc.transform.position+back.normalized*HandoverGap;
            handTo=NavMesh.SamplePosition(spot,out var hit,.8f,NavMesh.AllAreas)?hit.position:spot;
        }
        /// <summary>Nam walks up to anh Lực and faces him; the others turn to Nam as he comes.</summary>
        void TickHandover(float dt)
        {
            if(!HandingOver)return;
            var toward=Vector3.ProjectOnPlane(handTo-lockedPosition,Vector3.up);
            if(toward.magnitude>.05f)
            {
                lockedPosition=Vector3.MoveTowards(lockedPosition,handTo,HandoverPace*dt);
                lockedRotation=Quaternion.RotateTowards(lockedRotation,Quaternion.LookRotation(toward.normalized),360*dt);
            }
            else if(roles.TryGetValue("Anh Lực",out var luc))
            {
                var face=Vector3.ProjectOnPlane(luc.transform.position-lockedPosition,Vector3.up);
                if(face.sqrMagnitude>.01f)lockedRotation=Quaternion.RotateTowards(lockedRotation,Quaternion.LookRotation(face),240*dt);
            }
            foreach(var w in handWatchers)
            {
                var look=Vector3.ProjectOnPlane(lockedPosition-w.transform.position,Vector3.up);
                if(look.sqrMagnitude>.01f)w.transform.rotation=Quaternion.RotateTowards(w.transform.rotation,Quaternion.LookRotation(look),200*dt);
            }
        }
        /// <summary>A two-shot from the side: Nam walking up, anh Lực and the others turned to him.</summary>
        void HandoverShot(out Vector3 eye,out Vector3 focus)
        {
            var luc=roles["Anh Lực"].transform.position;var mid=Vector3.Lerp(lockedPosition,luc,.5f);
            var axis=Vector3.ProjectOnPlane(luc-handFrom,Vector3.up);axis=axis.sqrMagnitude>.01f?axis.normalized:Mission.Player.forward;
            var side=Vector3.Cross(Vector3.up,axis);if(Vector3.Dot(cameraBefore-mid,side)<0)side=-side;
            eye=mid+side*4.2f-axis*1.6f+Vector3.up*1.7f;focus=mid+Vector3.up*1.3f;
        }
    }
}
