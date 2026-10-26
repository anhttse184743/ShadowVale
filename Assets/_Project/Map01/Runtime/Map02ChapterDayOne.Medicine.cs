using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // D1_03 (plan P5, group B, 2026-10-25): chuyen_dong/blender_scripts/map2_ongsau_nhanthuoc_mixamo.py. Ông Sáu coughs
        // and speaks from the inner room; Nam, turned to chị Lành 0.70 m off, holds the packet out at his chest in both hands
        // as he answers, she takes it from under with both hands and draws it in. The hand-over clock starts with Nam's line.
        const float HandOverLetGo=3.3f,HandOverEnd=4.6f,HandOverGap=.70f;
        float medicineStart=-1;
        bool HandOverMocap=>DayClip("NamTraoThuoc")!=null&&DayClip("LanhNhanThuoc")!=null;
        bool MedicineHolding=>sceneTask?.id=="D1_03"&&HandOverMocap&&(medicineStart<0||sceneClock<medicineStart+HandOverEnd);
        void StageHandOver()
        {
            medicineStart=-1;if(!HandOverMocap)return;var lanh=roles["Chị Lành"];var nam=Mission.Player;
            var toward=Vector3.ProjectOnPlane(lanh.transform.position-nam.position,Vector3.up);if(toward.sqrMagnitude<.01f)toward=nam.forward;toward.Normalize();
            nam.rotation=Quaternion.LookRotation(toward);lanh.StopAt(nam.position+toward*HandOverGap);lanh.transform.rotation=Quaternion.LookRotation(-toward);
        }
        /// <summary>Side on to the two of them as he holds it out and she takes it.</summary>
        void HandOverShot(out Vector3 eye,out Vector3 focus)
        {
            var nam=Mission.Player;var mid=(nam.position+roles["Chị Lành"].transform.position)*.5f;focus=mid+Vector3.up*1.2f;eye=focus+nam.right*2.3f+Vector3.up*.25f;
        }
        void SampleHandOver()
        {
            var lanh=roles["Chị Lành"];if(medicineStart<0&&lines!=null&&lines[lineIndex].speaker=="Nam")medicineStart=sceneClock;
            float t=medicineStart<0?0:sceneClock-medicineStart;
            namPose?.Sample(medicineStart<0?namIdle:DayClip("NamTraoThuoc"),t,false);lanh.SamplePerformance(DayClip("LanhNhanThuoc"),t,false);lanh.GroundPerformance(lanh.transform.position.y);
            // Unity's humanoid retarget leaves the hands some centimetres off where Blender had them (his higher and nearer, hers
            // short), so the packet is held where the take means it, 0.40 m before his chest at 1.22 m, and both pairs of hands
            // are put on it: his from either end, hers either side from under; then it goes with her hands to her chest.
            var nam=Mission.Player;var across=nam.right;var packet=nam.position+nam.forward*.40f+Vector3.up*1.22f;
            float give=Mathf.SmoothStep(0,1,(t-.3f)/.9f)*(1-Mathf.SmoothStep(0,1,(t-HandOverLetGo)/.8f)),hold=Mathf.SmoothStep(0,1,(t-1f)/.8f)*(1-Mathf.SmoothStep(0,1,(t-HandOverLetGo)/.6f));
            if(dayNamAnimator!=null&&dayNamAnimator.isHuman){Reach(dayNamAnimator,false,packet+across*.07f+Vector3.up*.03f-nam.forward*.02f,give);Reach(dayNamAnimator,true,packet-across*.07f+Vector3.up*.03f-nam.forward*.02f,give);}
            var her=lanh.Animator;
            if(her!=null&&her.isHuman){Reach(her,true,packet+across*.06f-Vector3.up*.05f-nam.forward*.02f,hold);Reach(her,false,packet-across*.06f-Vector3.up*.05f-nam.forward*.02f,hold);}
            bool shown=medicineStart>=0&&t>=.3f&&t<HandOverEnd;medicineArt.gameObject.SetActive(shown);
            if(shown)
            {
                var at=packet;
                if(t<1.2f&&dayNamAnimator!=null){var l=dayNamAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position;var r=dayNamAnimator.GetBoneTransform(HumanBodyBones.RightHand).position;at=(l+r)*.5f+Vector3.up*.03f;}   // coming out to the hold
                else if(t>=HandOverLetGo&&her!=null){var l=her.GetBoneTransform(HumanBodyBones.LeftHand).position;var r=her.GetBoneTransform(HumanBodyBones.RightHand).position;at=(l+r)*.5f+Vector3.up*.05f;}   // hers now
                medicineArt.SetPositionAndRotation(at,Quaternion.LookRotation(nam.forward));
            }
            if(medicineStart>=0&&t>=HandOverLetGo)Day.GiveMedicine(0);
        }
    }
}
