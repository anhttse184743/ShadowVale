using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // N1_04 "đổi ca" (plan P10, group B, 2026-10-25): chuyen_dong/blender_scripts/map2_doica_mixamo.py, on mocap.
        // Later in the night (a fade in, no walk): Hùng lies on his back on the watch mat along the veranda, eyes open, his
        // hurt forearm across his belly; Nam sits on the boards out toward the yard, back to the house, one knee up, and at
        // 9 s looks back at him. Seen from inside the house, through the doorway, 12 s. Both takes are in place.
        const float ShiftMatOut=.62f,ShiftMatAlong=.29f,ShiftHungAlong=-.387f,ShiftNamAlong=.75f;
        bool ShiftMocap=>DayClip("HungDoiCa")!=null&&DayClip("NamDoiCa")!=null;
        Vector3 ShiftMat=>porchDoor+porchOut*ShiftMatOut+porchAlong*ShiftMatAlong;
        /// <summary>Nam's place on the boards: 1.1–1.6 m out past the mat, short of the veranda's edge.</summary>
        Vector3 ShiftNam{get{float deck=PorchDepth();float out_=Mathf.Clamp(deck>.3f?deck-.5f:ShiftMatOut+1.3f,ShiftMatOut+1.1f,ShiftMatOut+1.6f);return porchDoor+porchOut*out_+porchAlong*(ShiftMatAlong+ShiftNamAlong);}}
        Vector3 shiftNamAt;
        void PlaceRestMat(){if(restMat==null)return;FindPorch();restMat.SetPositionAndRotation(ShiftMat,Quaternion.LookRotation(porchAlong));}   // along the veranda, in front of the doorway
        void StageShift()
        {
            dayApproachDuration=0;PlaceRestMat();HungNativeBody(true);shiftNamAt=ShiftNam;
            var hung=roles["Hùng"];hung.StopAt(ShiftMat+porchAlong*ShiftHungAlong);hung.transform.rotation=Quaternion.LookRotation(-porchAlong);
            if(dayNamAnimator!=null)   // on his place at once, under the fade (BeginScene locks him where he stands now)
            {
                AlignBody(Mission.Player,dayNamAnimator.transform,shiftNamAt,Quaternion.LookRotation(porchOut),out var np,out var nr);
                var cc=Mission.Player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;Mission.Player.SetPositionAndRotation(np,nr);if(cc!=null)cc.enabled=true;
            }
        }
        void SampleShift()
        {
            var hung=roles["Hùng"];float t=sceneClock;
            hung.SamplePerformance(DayClip("HungDoiCa"),t,false);namPose?.Sample(DayClip("NamDoiCa"),t,false);
            AlignBody(hung.transform,hung.Animator.transform,ShiftMat+porchAlong*ShiftHungAlong,Quaternion.LookRotation(-porchAlong),out var hp,out var hr);hung.transform.SetPositionAndRotation(hp,hr);
            if(dayNamAnimator!=null)AlignBody(Mission.Player,dayNamAnimator.transform,shiftNamAt,Quaternion.LookRotation(porchOut),out lockedPosition,out lockedRotation);
        }
        void ShiftShot(out Vector3 eye,out Vector3 focus)
        {
            eye=porchDoor-porchOut*2.2f+porchAlong*.35f+Vector3.up*1.0f;   // from inside the house, low, out through the doorway
            focus=ShiftMat+porchOut*.9f+porchAlong*.4f+Vector3.up*.45f;
        }
        /// <summary>A fade up from black: it is later in the night.</summary>
        float ShiftFade=>sceneTask?.id=="N1_04"&&ShiftMocap?Mathf.Clamp01(1-sceneClock/.8f):0;
    }
}
