using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // D1_05 "đóng cọc" (group B, 2026-10-25): chuyen_dong/blender_scripts/map2_dongcoc_mixamo.py. Nam stands at the stake
        // with a two-handed mallet held up, ready; each Space press plays one blow that lands 0.12 s in (on the press), then
        // he raises it again. Chú Bảy squats beside the stake holding it low, his hands following it down as it is driven.
        const float StakeStand=.95f,StakeHolder=.42f;
        float strikeClock=9;Transform twoHandMallet;bool stakeStaged;
        bool StakeMocap=>DayClip("NamVoCoc")!=null&&DayClip("BayGiuCoc")!=null;
        Transform Stake=>physicalStakes.Count>0?physicalStakes[Mathf.Min(Day.stakeHits/3,physicalStakes.Count-1)]:null;
        void StrikeStake(){strikeClock=0;}
        void StageStake()
        {
            var stake=Stake;if(stake==null)return;stakeStaged=true;var at=new Vector3(stake.position.x,sites["dock"].position.y,stake.position.z);
            var cc=Mission.Player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;
            Mission.Player.SetPositionAndRotation(at-Vector3.forward*StakeStand,Quaternion.LookRotation(Vector3.forward));if(cc!=null)cc.enabled=true;
            var bay=roles["Chú Bảy"];bay.StopAt(at+Vector3.right*StakeHolder);bay.transform.rotation=Quaternion.LookRotation(Vector3.left);
            if(twoHandMallet==null)
            {
                var wood=dayMaterials.Find(m=>m!=null&&m.name=="Weathered wood");var iron=dayMaterials.Find(m=>m!=null&&m.name=="Iron latch");
                twoHandMallet=Group("Two-handed dock mallet",Vector3.zero);Part(twoHandMallet,"Handle",new Vector3(0,.31f,0),new Vector3(.035f,.62f,.035f),wood);Part(twoHandMallet,"Head",new Vector3(0,.6f,0),new Vector3(.22f,.12f,.12f),wood??iron);
            }
        }
        /// <summary>The hands' middle and the handle's way (toward the head) at t s into a blow, in the world: raised and back at
        /// 0, on the stake's top at 0.12, a rebound, raised again by 0.8. Nam stands 0.95 m before the stake, facing it.</summary>
        void MalletPose(float t,float stakeTop,out Vector3 hands,out Vector3 along)
        {
            float k=t<.12f?Mathf.Pow(Mathf.SmoothStep(0,1,t/.12f),1.6f):t<.35f?1-.18f*Mathf.Sin(Mathf.PI*(t-.12f)/.23f):1-Mathf.SmoothStep(0,1,(t-.35f)/.45f);
            var nam=Mission.Player;
            Vector3 W(Vector3 local)=>nam.position+nam.rotation*local;
            var handsTop=new Vector3(-.02f,1.92f,-.06f);var handsHit=new Vector3(0,1.05f,.46f);var dirTop=new Vector3(0,.707f,-.707f);
            var top=nam.InverseTransformPoint(new Vector3(0,stakeTop,0)+Vector3.ProjectOnPlane(Stake.position,Vector3.up));top.y=stakeTop-nam.position.y;
            var dirHit=(top-handsHit).normalized;var local=Vector3.Lerp(handsTop,handsHit,k);
            hands=W(local);along=nam.rotation*Vector3.Slerp(dirTop,dirHit,k).normalized;
        }
        void ReleaseStake(){if(!stakeStaged)return;stakeStaged=false;roles["Chú Bảy"].ReleasePerformance();roles["Chú Bảy"].StopAt(sites["dock"].position);if(twoHandMallet!=null)twoHandMallet.gameObject.SetActive(false);}
        void SampleStake(float dt)
        {
            if(!stakeStaged)StageStake();var stake=Stake;if(stake==null)return;strikeClock+=dt;
            FreeNamPose(DayClip("NamVoCoc"),Mathf.Min(strikeClock,DayClip("NamVoCoc").length),false);
            // The mallet's way is the take's own (map2_dongcoc_mixamo.py), aimed at this stake's real top at the blow; his hands
            // are put on its handle (the retarget alone leaves them a hand's width off, which a 15 cm grip turns into a wild swing).
            MalletPose(Mathf.Min(strikeClock,.8f),stake.position.y+1.08f,out var hands,out var along);
            Reach(dayNamAnimator,false,hands-along*.07f,1);Reach(dayNamAnimator,true,hands+along*.08f,1);
            twoHandMallet.gameObject.SetActive(true);twoHandMallet.SetPositionAndRotation(hands-along*.11f,Quaternion.FromToRotation(Vector3.up,along));
            var bay=roles["Chú Bảy"];bay.SamplePerformance(DayClip("BayGiuCoc"),Time.time,true);bay.GroundPerformance(sites["dock"].position.y);   // his own breathing, not the needle's clock
            var body=bay.Animator;if(body==null||!body.isHuman)return;
            float top=stake.position.y+1.08f;   // the post stands 1.08 m from the stake's origin, which sinks with each hit
            foreach(var(left,low) in new[]{(true,.62f),(false,.72f)})Reach(body,left,new Vector3(stake.position.x+.07f,top-low,stake.position.z+(left?-.04f:.04f)),1);
        }
    }
}
