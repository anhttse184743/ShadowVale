using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01 {
 public sealed partial class Map02ChapterDirector {
  readonly Dictionary<string,Dictionary<Transform,Quaternion>> sceneFingerPoses=new();float sleepTorsoDepth;
  static Transform SceneFinger(Animator body,HumanBodyBones id){
   var found=body.GetBoneTransform(id);if(found!=null)return found;int n=(int)id-(int)HumanBodyBones.LeftThumbProximal;bool left=n<15;if(!left)n-=15;if(n<0||n>=15)return null;
   string name=(left?"Left":"Right")+"Hand"+new[]{"Thumb","Index","Middle","Ring","Pinky"}[n/3]+(n%3+1);var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
   foreach(var node in hand.GetComponentsInChildren<Transform>(true))if(node.name==name)return node;return null;
  }
  static Transform SceneMiddle(Animator body,bool left){var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);return SceneFinger(body,left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal)??(hand.childCount>0?hand.GetChild(0):null);}
  static Vector3 ScenePalm(Animator body,bool left){var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var finger=SceneMiddle(body,left);return hand.position+(finger.position-hand.position).normalized*.05f;}
  void SceneFingers(Animator body,bool left,bool pinch=false,float weight=1){
   string key=body.GetInstanceID()+":"+left+":"+pinch;
   if(!sceneFingerPoses.TryGetValue(key,out var fingers)){
    var nodes=body.GetComponentsInChildren<Transform>(true);var pos=new Vector3[nodes.Length];var rot=new Quaternion[nodes.Length];for(int i=0;i<nodes.Length;i++){pos[i]=nodes[i].localPosition;rot[i]=nodes[i].localRotation;}
    var handler=new HumanPoseHandler(body==dayNamAnimator&&dayArt.namFingerReference!=null?dayArt.namFingerReference:body.avatar,body.transform);var pose=new HumanPose();fingers=new Dictionary<Transform,Quaternion>();
    try{handler.GetHumanPose(ref pose);string side=left?"Left":"Right";
     for(int i=0;i<HumanTrait.MuscleName.Length;i++){var n=HumanTrait.MuscleName[i];if(!n.StartsWith(side))continue;bool digit=n.Contains("Thumb")||n.Contains("Index")||n.Contains("Middle")||n.Contains("Ring")||n.Contains("Little");if(!digit)continue;pose.muscles[i]=n.Contains("Spread")?0:pinch?(n.Contains("Thumb")?-.12f:n.Contains("Index")?-.22f:n.Contains("Middle")?-.32f:-.45f):.18f;}
     handler.SetHumanPose(ref pose);int first=(int)(left?HumanBodyBones.LeftThumbProximal:HumanBodyBones.RightThumbProximal),last=(int)(left?HumanBodyBones.LeftLittleDistal:HumanBodyBones.RightLittleDistal);
     for(int i=first;i<=last;i++){var bone=SceneFinger(body,(HumanBodyBones)i);if(bone!=null)fingers[bone]=bone.localRotation;}
    }finally{handler.Dispose();for(int i=0;i<nodes.Length;i++)nodes[i].SetLocalPositionAndRotation(pos[i],rot[i]);}
    sceneFingerPoses[key]=fingers;
   }
   foreach(var finger in fingers)finger.Key.localRotation=Quaternion.Slerp(finger.Key.localRotation,finger.Value,weight);
  }
  void RelaxSceneFingers(Animator body,bool left)=>SceneFingers(body,left);
  static void ArmFrame(Animator body,out Vector3 right,out Vector3 up,out Vector3 front){
   right=(body.GetBoneTransform(HumanBodyBones.RightUpperArm).position-body.GetBoneTransform(HumanBodyBones.LeftUpperArm).position).normalized;
   up=(body.GetBoneTransform(HumanBodyBones.Neck).position-body.GetBoneTransform(HumanBodyBones.Hips).position).normalized;
   front=Vector3.Cross(right,up).normalized;
  }
  static void SceneArm(Animator body,bool left,Vector3 palm,Vector3 hint,float limit=30){
   var lower=body.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var finger=SceneMiddle(body,left);
   if(lower==null||hand==null||finger==null)return;
   for(int i=0;i<4;i++){
    Map01ArmContact.Reach(body,left,palm-(ScenePalm(body,left)-hand.position),hint,1);
    var along=(hand.position-lower.position).normalized;var fingerDirection=(finger.position-hand.position).normalized;float angle=Vector3.Angle(fingerDirection,along);
    if(angle>limit)hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.FromToRotation(fingerDirection,along)*hand.rotation,(angle-limit)/angle);
   }
  }
  void CorrectSeatedArms(Animator body,bool injured){
   if(body==null||!body.isHuman)return;ArmFrame(body,out var right,out var up,out var front);var chest=body.GetBoneTransform(HumanBodyBones.Chest).position;var hips=body.GetBoneTransform(HumanBodyBones.Hips).position;
   var leftPalm=ScenePalm(body,true);var rightPalm=ScenePalm(body,false);
   if(injured){rightPalm=Vector3.Lerp(hips,chest,.4f)+front*.16f+right*.035f;RelaxSceneFingers(body,false);}
   SceneArm(body,false,rightPalm,chest+right*.38f-up*.22f+front*.08f);
   SceneArm(body,true,leftPalm,chest-right*.38f-up*.22f+front*.08f);
  }
  static void RestPalmDown(Animator body,bool left){
   var index=SceneFinger(body,left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);var little=SceneFinger(body,left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);if(index==null||little==null)return;
   var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var lower=body.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var axis=(hand.position-lower.position).normalized;
   var normal=Vector3.Cross(index.position-hand.position,little.position-hand.position)*(left?1:-1);var from=Vector3.ProjectOnPlane(normal,axis);var to=Vector3.ProjectOnPlane(Vector3.down,axis);
   if(from.sqrMagnitude>.00001f&&to.sqrMagnitude>.00001f)hand.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(from,to,axis),axis)*hand.rotation;
  }
  void CorrectShiftArms(){
   CorrectSeatedArms(dayNamAnimator,false);ArmFrame(dayNamAnimator,out var nr,out var nu,out var nf);var nc=dayNamAnimator.GetBoneTransform(HumanBodyBones.Chest).position;
   foreach(bool left in new[]{true,false}){var knee=dayNamAnimator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg).position;RelaxSceneFingers(dayNamAnimator,left);SceneArm(dayNamAnimator,left,knee+Vector3.up*.09f+nf*.02f,nc+nr*(left?-.38f:.38f)-nu*.22f+nf*.08f);RestPalmDown(dayNamAnimator,left);}
   var body=roles["Hùng"].Animator;if(body==null||!body.isHuman)return;ArmFrame(body,out var right,out var up,out var front);var chest=body.GetBoneTransform(HumanBodyBones.Chest).position;var hips=body.GetBoneTransform(HumanBodyBones.Hips).position;
   RelaxSceneFingers(body,false);RelaxSceneFingers(body,true);
   var centre=Vector3.Lerp(hips,chest,.46f);
   if(sleepTorsoDepth<=0){
    var depths=new List<float>();var mesh=new Mesh();
    foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var v in mesh.vertices){var d=skin.transform.TransformPoint(v)-centre;float z=Vector3.Dot(d,front);if(Mathf.Abs(Vector3.Dot(d,right))<.18f&&Mathf.Abs(Vector3.Dot(d,up))<.13f&&z>.05f&&z<.4f)depths.Add(z);}}
    Destroy(mesh);depths.Sort();sleepTorsoDepth=depths.Count>0?Mathf.Clamp(depths[(int)(depths.Count*.95f)]+.025f,.16f,.36f):.28f;
   }
   var abdomen=centre+front*sleepTorsoDepth;
   SceneArm(body,false,abdomen+right*.055f,chest+right*.32f-up*.25f+front*.035f,25);
   var elbow=body.GetBoneTransform(HumanBodyBones.RightLowerArm).position;var wrist=body.GetBoneTransform(HumanBodyBones.RightHand).position;
   SceneArm(body,true,Vector3.Lerp(elbow,wrist,.7f)+front*.05f,chest-right*.32f-up*.25f+front*.035f);
  }
  Transform MealItem(string name){foreach(var item in supperArt)if(item.data.name==name)return item.art;return null;}
  void HoldMealSticks(Animator body,bool left,string suffix,float weight){
   var first=MealItem("Dua_"+suffix+"_0");var second=MealItem("Dua_"+suffix+"_1");if(first==null||second==null)return;
   if(weight<=0)return;SceneFingers(body,left,true,weight);var thumb=SceneFinger(body,left?HumanBodyBones.LeftThumbDistal:HumanBodyBones.RightThumbDistal);var index=SceneFinger(body,left?HumanBodyBones.LeftIndexDistal:HumanBodyBones.RightIndexDistal);
   var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var end=SceneMiddle(body,left);
   Vector3 Grip()=>thumb!=null&&index!=null?(thumb.position+index.position)*.5f:Vector3.Lerp(hand.position,end.position,.45f);
   ArmFrame(body,out var right,out var up,out var front);var chest=body.GetBoneTransform(HumanBodyBones.Chest).position;var hint=chest+right*(left?-.38f:.38f)-up*.22f+front*.08f;
   var axis=first.forward;var gripTarget=first.position-axis*.055f;
   for(int i=0;i<3;i++){var grip=Grip();SceneArm(body,left,ScenePalm(body,left)+(gripTarget-grip)*weight,hint);}
   var pinch=Grip();var side=Vector3.Cross(axis,Vector3.up).normalized;
   first.position=Vector3.Lerp(first.position,pinch+axis*.055f-side*.004f,weight);second.SetPositionAndRotation(Vector3.Lerp(second.position,pinch+axis*.055f+side*.004f,weight),first.rotation);
  }
  void HoldMealBowl(Animator body,bool left,string name,float weight=1){
   var bowl=MealItem(name);if(bowl==null)return;ArmFrame(body,out var right,out var up,out var front);var chest=body.GetBoneTransform(HumanBodyBones.Chest).position;
   RelaxSceneFingers(body,left);SceneArm(body,left,Vector3.Lerp(ScenePalm(body,left),bowl.position-Vector3.up*.035f,weight),chest+right*(left?-.38f:.38f)-up*.22f+front*.08f);
  }
  void CorrectMealHands(float time){
   var hung=roles["Hùng"].Animator;CorrectSeatedArms(dayNamAnimator,false);CorrectSeatedArms(hung,true);
   HoldMealBowl(dayNamAnimator,true,"Chen_Nam");
   float Envelope(float start,float finish)=>Mathf.SmoothStep(0,1,(time-start)/.6f)*(1-Mathf.SmoothStep(0,1,(time-(finish-.6f))/.6f));
   float namGrip=Mathf.Max(Envelope(-.6f,6.4f),Envelope(10.3f,19.8f)),hungGrip=Mathf.Max(Envelope(-.6f,10.2f),Envelope(20.4f,66.3f));
   if(time<32||time>46)RelaxSceneFingers(dayNamAnimator,false);HoldMealSticks(dayNamAnimator,false,"Nam",namGrip);
   HoldMealSticks(hung,true,"Hung",hungGrip);float bowl=Envelope(11,20.5f);if(bowl>0)HoldMealBowl(hung,true,"Chen_Hung",bowl);
  }
 }
}
