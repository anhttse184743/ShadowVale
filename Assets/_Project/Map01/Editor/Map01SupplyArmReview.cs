using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    public static class Map01SupplyArmReview
    {
        public static void ImportAndMeasure(){Map01OperationAnimationSetup.Import();Measure();}
        public static void Measure()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var root=UnityEngine.Object.Instantiate(prefab);root.SetActive(true);
            var actor=root.GetComponentInChildren<Animator>();
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            var forearm=actor.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rest=actor.avatar.humanDescription.skeleton.First(b=>b.name==hand.name).rotation;
            var restForearm=actor.avatar.humanDescription.skeleton.First(b=>b.name==forearm.name).rotation;
            var clip=Resources.Load<AnimationClip>("OperationsAnimation/Nam_SupplyCollect");
            var csv=new StringBuilder("seconds,wrist_degrees,forearm_degrees,wrist_frame_delta\n");
            float max=0,peakTime=0,jump=0;Quaternion previous=hand.localRotation;
            using(var pose=new Map01CinematicPose(actor,clip.humanMotion?null:Resources.Load<Avatar>("OperationsAnimation/Nam_SupplyCollectAvatar"),false))for(int i=0;i<=204;i++){
                float t=i/30f;pose.Sample(clip,t);float angle=Quaternion.Angle(rest,hand.localRotation);
                float change=i==0?0:Quaternion.Angle(previous,hand.localRotation);previous=hand.localRotation;
                if(angle>max){max=angle;peakTime=t;}jump=Mathf.Max(jump,change);
                csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0:F4},{1:F3},{2:F3},{3:F3}\n",t,angle,Quaternion.Angle(restForearm,forearm.localRotation),change);
            }
            UnityEngine.Object.DestroyImmediate(root);Directory.CreateDirectory("Logs/SupplyArmReview");
            File.WriteAllText("Logs/SupplyArmReview/measure.csv",csv.ToString());
            File.WriteAllText("Logs/SupplyArmReview/summary.txt","Wrist maximum "+max+" degrees at "+peakTime+"s; maximum frame change "+jump);
            Debug.Log("Supply arm review: wrist "+max+" degrees at "+peakTime+"s; frame change "+jump);
        }
    }
}