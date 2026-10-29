using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // P3 (doc, D1_02): Bé Tí hides behind a porch post to stare at the radio set, giggles at "Nhẹ thì đưa Tí xách thử
        // coi" and runs off behind the house. The post is found from the house itself: the tallest thin collider on the
        // front edge of chị Lành's veranda; she stands on its far side from Hùng.
        const int ChildCueLine=2;const float ChildGiggle=1.9f;bool childRan;float childLaughAt=-1;
        bool ChildMocap=>DayClip("TiNapCot")!=null&&DayClip("TiCuoi")!=null;
        void StageChildPeek()
        {
            childRan=false;childLaughAt=-1;var child=roles["Bé Tí"];var hung=roles["Hùng"].transform.position;
            var edge=porchDoor+porchOut*Mathf.Max(.8f,PorchDepth()-.1f);Collider post=null;float best=float.PositiveInfinity;
            foreach(var c in Physics.OverlapSphere(edge,3.5f,~0,QueryTriggerInteraction.Ignore))
            {
                var b=c.bounds;if(b.size.y<1.4f||Mathf.Max(b.size.x,b.size.z)>.45f||c.GetComponentInParent<Map02ChapterActor>()!=null)continue;
                float d=Vector3.Distance(new Vector3(b.center.x,edge.y,b.center.z),edge);if(d<best){best=d;post=c;}
            }
            var at=post!=null?post.bounds.center:edge+porchAlong*1.4f;at.y=porchDoor.y;
            var away=Vector3.ProjectOnPlane(at-hung,Vector3.up).normalized;var spot=at+away*.38f;
            if(NavMesh.SamplePosition(spot,out var hit,.6f,NavMesh.AllAreas))spot=hit.position;
            child.StopAt(spot);child.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(hung-spot,Vector3.up));
        }
        void TickChildPeek()
        {
            if(sceneTask?.id!="D1_02"||childRan||lineIndex<ChildCueLine)return;var child=roles["Bé Tí"];
            if(childLaughAt<0){childLaughAt=sceneClock;PlayDaySound("child_giggle",.7f,child.transform.position+Vector3.up*1.0f);}
            if(ChildMocap&&sceneClock<childLaughAt+ChildGiggle)return;   // she giggles first, then runs
            childRan=true;child.ReleasePerformance();
            var behind=porchDoor-porchOut*4.5f+porchAlong*2.5f;
            if(NavMesh.SamplePosition(behind,out var hit,3,NavMesh.AllAreas)){child.GoTo(hit.position,2.4f);child.SceneDriven=true;}
        }
        /// <summary>Crouched behind the post (a looping crouch) until her giggle, then the giggle itself.</summary>
        void SampleChildPeek()
        {
            if(!ChildMocap||childRan)return;var child=roles["Bé Tí"];
            if(childLaughAt<0)child.SamplePerformance(DayClip("TiNapCot"),sceneClock,true);else child.SamplePerformance(DayClip("TiCuoi"),sceneClock-childLaughAt,false);
        }
    }
}
