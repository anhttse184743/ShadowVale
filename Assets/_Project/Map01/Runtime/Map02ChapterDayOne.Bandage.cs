using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        // D1_02 "thay băng" (plan P3, group B, 2026-10-25): chuyen_dong/blender_scripts/map2_thaybang_mixamo.py, on mocap.
        // Hùng stands holding his arm, sits on the stool and holds his right forearm out; chị Lành says her line standing at
        // his right, kneels on one knee and wraps it, the roll in her right hand, the turns spiralling from the wrist up;
        // he winces at her tug (6.7 s). Both takes run on the scene clock (24 s). Places are in Hùng's frame (x right, z ahead).
        static readonly Vector3 BandageStool=new Vector3(-.001f,0,-.026f),BandageLanh=new Vector3(.608f,0,.343f);
        const float BandageRollIn=3.3f,BandageRollOut=19.9f,WrapStart=5f,WrapEnd=18.6f;
        Transform bandageSleeve;Vector3 bandageAt;Quaternion bandageFacing;
        bool BandageMocap=>DayClip("HungThayBang")!=null&&DayClip("LanhThayBang")!=null;
        void StageBandage()
        {
            var hung=roles["Hùng"];var lanh=roles["Chị Lành"];bandageAt=hung.transform.position;bandageFacing=Quaternion.LookRotation(Vector3.ProjectOnPlane(hung.transform.forward,Vector3.up));
            sceneChair.SetPositionAndRotation(bandageAt+bandageFacing*BandageStool,bandageFacing);
            if(!BandageMocap)return;
            HungNativeBody(true);   // made at his own size, his arm where her hands are (as the supper)
            lanh.StopAt(bandageAt+bandageFacing*BandageLanh);lanh.transform.rotation=bandageFacing*Quaternion.LookRotation(Vector3.left);
            roles["Ông Sáu"].SetVisible(false);   // P3: he is in the inner room, not behind Hùng
            if(bandageSleeve==null)
            {
                var gauze=DayMaterial("Clean bandage",new Color(.9f,.88f,.82f));
                bandageSleeve=Part(dayProps,"Bandage on Hùng's right forearm",Vector3.zero,Vector3.one,gauze,PrimitiveType.Cylinder);
                if(bandageRoll!=null)foreach(var r in bandageRoll.GetComponentsInChildren<Renderer>())r.sharedMaterial=gauze;   // a roll of the same clean cloth
            }
        }
        void SampleBandage()
        {
            var hung=roles["Hùng"];var lanh=roles["Chị Lành"];float t=sceneClock;
            hung.transform.SetPositionAndRotation(bandageAt,bandageFacing);lanh.transform.SetPositionAndRotation(bandageAt+bandageFacing*BandageLanh,bandageFacing*Quaternion.LookRotation(Vector3.left));
            hung.BlendPerformance(DayClip("InjuredIdle"),DayClip("HungThayBang"),t,Mathf.SmoothStep(0,1,t/.4f));lanh.SamplePerformance(DayClip("LanhThayBang"),t,false);lanh.GroundPerformance(bandageAt.y);   // her kneeling knee and toes on the floor, not her standing offset
            var hands=lanh.Animator;
            if(bandageRoll!=null&&hands!=null&&hands.isHuman)
            {
                bool held=t>=BandageRollIn&&t<=BandageRollOut;bandageRoll.gameObject.SetActive(held);
                if(held){var hand=hands.GetBoneTransform(HumanBodyBones.RightHand);bandageRoll.SetPositionAndRotation(hand.position+hand.rotation*new Vector3(0,.075f,0),hand.rotation*Quaternion.Euler(0,0,90));}
            }
            DressForearm(Mathf.SmoothStep(0,1,(t-WrapStart)/(WrapEnd-WrapStart)),t>=WrapStart+.4f);
        }
        /// <summary>The turns already on his forearm: from near the wrist up toward the elbow, as far as she has wrapped.</summary>
        void DressForearm(float wrapped,bool shown)
        {
            if(bandageSleeve==null)return;var body=roles["Hùng"].Animator;
            if(!shown||body==null||!body.isHuman||!roles["Hùng"].visual.gameObject.activeInHierarchy){bandageSleeve.gameObject.SetActive(false);return;}
            var elbow=body.GetBoneTransform(HumanBodyBones.RightLowerArm).position;var wrist=body.GetBoneTransform(HumanBodyBones.RightHand).position;
            var a=Vector3.Lerp(elbow,wrist,.84f);var b=Vector3.Lerp(elbow,wrist,.84f-.52f*wrapped);var length=Mathf.Max(.01f,Vector3.Distance(a,b));
            bandageSleeve.gameObject.SetActive(true);bandageSleeve.SetPositionAndRotation((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,(a-b).normalized));
            float girth=.082f*body.transform.lossyScale.y;bandageSleeve.localScale=new Vector3(girth,length*.5f,girth);
        }
        /// <summary>After D1_02 the bandage stays on his arm through Day 1 and Night 1.</summary>
        void UpdateBandageSleeve(){if(bandageSleeve==null||sceneTask?.id=="D1_02")return;DressForearm(1,State.phase<=1&&State.Done("D1_02")&&BandageMocap);}
    }
}
