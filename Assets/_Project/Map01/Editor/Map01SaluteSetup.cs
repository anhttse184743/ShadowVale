using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
public static class Map01SaluteSetup {
public const string ClipPath="Assets/_Project/Art/Characters/Animations/Generated/Nam_Salute_Briefing.anim";
[MenuItem("ShadowVale/Cutscene/Prepare Nam salute")]
public static void Run() {
const string path="Assets/_Project/Art/Characters/Animations/Opening/Nam_Salute_Briefing.fbx";
AssetDatabase.Refresh();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
var importer=(ModelImporter)AssetImporter.GetAtPath(path);
importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.materialImportMode=ModelImporterMaterialImportMode.None;
var clips=importer.defaultClipAnimations;
foreach(var c in clips){c.name="Nam_Salute_Briefing";c.loopTime=false;c.lockRootRotation=true;c.lockRootPositionXZ=true;c.lockRootHeightY=true;}
importer.clipAnimations=clips;importer.SaveAndReimport();
var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
var baked=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
if(baked==null){baked=new AnimationClip();AssetDatabase.CreateAsset(baked,ClipPath);}
EditorUtility.CopySerialized(source,baked);baked.name="Nam_Salute_Briefing";
CalibrateStraightElbow(baked);
var fingerValues=CalibrateFingers();
// On avatars with finger bones, keep fingers together and extended during the
// salute. The original 28-bone animation skeleton itself is not modified.
foreach(var binding in AnimationUtility.GetCurveBindings(baked).Where(b=>b.propertyName.StartsWith("RightHand.")&&(b.propertyName.Contains("Stretched")||b.propertyName.Contains("Spread")))) {
var muscle=binding.propertyName;
float rest=muscle.Contains("Stretched")?.55f:0,held=muscle.Contains("Stretched")?1:0;
if(fingerValues.TryGetValue(muscle,out var calibrated))held=calibrated;
var curve=new AnimationCurve(new Keyframe(0,rest),new Keyframe(.18f,held),new Keyframe(.7f,held),new Keyframe(.9f,held),new Keyframe(baked.length-.25f,held),new Keyframe(baked.length,rest));
AnimationUtility.SetEditorCurve(baked,binding,curve);
}
EditorUtility.SetDirty(baked);
var idleSource=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim");
const string idlePath="Assets/_Project/Art/Characters/Animations/Generated/Nam_Briefing_Idle.anim";
var attention=AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
if(attention==null){attention=new AnimationClip();AssetDatabase.CreateAsset(attention,idlePath);}
EditorUtility.CopySerialized(idleSource,attention);attention.name="Nam_Briefing_Idle";
foreach(var binding in AnimationUtility.GetCurveBindings(attention).Where(b=>new[]{"Head ","Neck ","Spine ","Chest ","UpperChest "}.Any(prefix=>b.propertyName.StartsWith(prefix))))
AnimationUtility.SetEditorCurve(attention,binding,AnimationCurve.Constant(0,attention.length,0));
// Listening and salute frame zero must share the same straight arm. Leaving
// the rifle idle's bent elbow here causes a snap before the salute starts.
foreach(var binding in AnimationUtility.GetCurveBindings(baked).Where(b=>new[]{"Right Shoulder ","Right Arm ","Right Forearm ","Right Hand "}.Any(prefix=>b.propertyName.StartsWith(prefix))))
AnimationUtility.SetEditorCurve(attention,binding,AnimationCurve.Constant(0,attention.length,AnimationUtility.GetEditorCurve(baked,binding).Evaluate(0)));
EditorUtility.SetDirty(attention);
const string prefabPath="Assets/_Project/Map01/Resources/Cutscenes/Map01Opening.prefab";
var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
try{var opening=prefab.GetComponent<Map01OpeningCutscene>();opening.salute=baked;opening.soldierIdle=attention;opening.saluteHoldTime=.7f;opening.saluteLowerStart=.9f;opening.saluteRaiseSeconds=.7f;opening.saluteLowerSeconds=baked.length-.9f;PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
AssetDatabase.SaveAssets();Debug.Log("NAM_SALUTE_READY "+baked.length+"s human="+baked.humanMotion);
}
static void CalibrateStraightElbow(AnimationClip clip) {
var bindings=AnimationUtility.GetCurveBindings(clip);
var stretchBinding=bindings.First(b=>b.propertyName=="Right Forearm Stretch");
var stretchCurve=AnimationUtility.GetEditorCurve(clip,stretchBinding);
float original=stretchCurve.Evaluate(0),held=stretchCurve.Evaluate(.7f),best=original,error=float.MaxValue;
var visual=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"));
try {
var actor=visual.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);
using(var handler=new HumanPoseHandler(actor.avatar,actor.transform)) {
var pose=new HumanPose();handler.GetHumanPose(ref pose);
foreach(var binding in bindings) {
int index=System.Array.IndexOf(HumanTrait.MuscleName,binding.propertyName);
if(index>=0)pose.muscles[index]=AnimationUtility.GetEditorCurve(clip,binding).Evaluate(0);
}
int stretch=System.Array.IndexOf(HumanTrait.MuscleName,"Right Forearm Stretch");
for(int step=0;step<=400;step++) {
float candidate=step*.005f;pose.muscles[stretch]=candidate;handler.SetHumanPose(ref pose);
var upper=actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
var elbow=actor.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
var hand=actor.GetBoneTransform(HumanBodyBones.RightHand).position;
float bend=Vector3.Angle(elbow-upper,hand-elbow);
if(bend<error){error=bend;best=candidate;}
}
}
} finally {Object.DestroyImmediate(visual);}
var keys=stretchCurve.keys;
for(int i=0;i<keys.Length;i++) {
// Derive the blend from the authored elbow curve, so manual key changes
// preserve their own timing instead of relying on a second timing formula.
float amount=Mathf.Abs(held-original)>.0001f?Mathf.Clamp01((keys[i].value-original)/(held-original)):0;
keys[i].value+=(best-original)*(1-amount);
}
var calibrated=new AnimationCurve(keys);
for(int i=0;i<keys.Length;i++)calibrated.SmoothTangents(i,0);
AnimationUtility.SetEditorCurve(clip,stretchBinding,calibrated);
Directory.CreateDirectory("SourceArt/Map01_Opening");
File.WriteAllText("SourceArt/Map01_Opening/elbow-calibration.txt","Original stretch: "+original+"\nStraight stretch: "+best+"\nRest elbow bend degrees: "+error+"\n");
Debug.Log("SALUTE_ELBOW_CALIBRATION original="+original+" straight="+best+" bend="+error);
}
static System.Collections.Generic.Dictionary<string,float> CalibrateFingers() {
var values=new System.Collections.Generic.Dictionary<string,float>();
var visual=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"));
try {
var actor=visual.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);
using(var handler=new HumanPoseHandler(actor.avatar,actor.transform)) {
var pose=new HumanPose();handler.GetHumanPose(ref pose);
for(int i=0;i<HumanTrait.MuscleName.Length;i++)if(HumanTrait.MuscleName[i].StartsWith("Right ")&&(HumanTrait.MuscleName[i].Contains("Stretched")||HumanTrait.MuscleName[i].Contains("Spread")))pose.muscles[i]=HumanTrait.MuscleName[i].Contains("Stretched")?1:0;
handler.SetHumanPose(ref pose);
var middle=(actor.GetBoneTransform(HumanBodyBones.RightMiddleDistal).position-actor.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position).normalized;
var proximal=new[]{HumanBodyBones.RightIndexProximal,HumanBodyBones.RightRingProximal,HumanBodyBones.RightLittleProximal};
var distal=new[]{HumanBodyBones.RightIndexDistal,HumanBodyBones.RightRingDistal,HumanBodyBones.RightLittleDistal};
var names=new[]{"Index","Ring","Little"};
for(int finger=0;finger<3;finger++) {
int index=System.Array.IndexOf(HumanTrait.MuscleName,"Right "+names[finger]+" Spread");float best=0,error=float.MaxValue;
for(int step=0;step<=40;step++) {
float candidate=-1+step*.05f;pose.muscles[index]=candidate;handler.SetHumanPose(ref pose);
var direction=(actor.GetBoneTransform(distal[finger]).position-actor.GetBoneTransform(proximal[finger]).position).normalized;
float score=1-Vector3.Dot(direction,middle);
if(score<error){error=score;best=candidate;}
}
pose.muscles[index]=best;handler.SetHumanPose(ref pose);values["RightHand."+names[finger]+".Spread"]=best;
}
int spread=System.Array.IndexOf(HumanTrait.MuscleName,"Right Thumb Spread"),stretch=System.Array.IndexOf(HumanTrait.MuscleName,"Right Thumb 1 Stretched");
float bestSpread=0,bestStretch=1,thumbError=float.MaxValue;
var indexBase=actor.GetBoneTransform(HumanBodyBones.RightIndexProximal).position;
var target=indexBase+middle*.015f;
for(int s=0;s<=40;s++)for(int e=0;e<=40;e++) {
pose.muscles[spread]=-1+s*.05f;pose.muscles[stretch]=-1+e*.05f;handler.SetHumanPose(ref pose);
var thumb=actor.GetBoneTransform(HumanBodyBones.RightThumbDistal).position;
float score=(thumb-target).sqrMagnitude;
if(score<thumbError){thumbError=score;bestSpread=pose.muscles[spread];bestStretch=pose.muscles[stretch];}
}
values["RightHand.Thumb.Spread"]=bestSpread;values["RightHand.Thumb.1 Stretched"]=bestStretch;
// Adduct the complete thumb, including its last two joints. Optimizing only
// its base leaves the visible fingertip pointing away from the flat palm.
int[] thumbMuscles={spread,stretch,System.Array.IndexOf(HumanTrait.MuscleName,"Right Thumb 2 Stretched"),System.Array.IndexOf(HumanTrait.MuscleName,"Right Thumb 3 Stretched")};
pose.muscles[spread]=bestSpread;pose.muscles[stretch]=bestStretch;
for(int pass=0;pass<5;pass++)foreach(int muscleIndex in thumbMuscles) {
float chosen=pose.muscles[muscleIndex],error=float.MaxValue;
for(int step=0;step<=40;step++) {
pose.muscles[muscleIndex]=-1+step*.05f;handler.SetHumanPose(ref pose);
var near=actor.GetBoneTransform(HumanBodyBones.RightThumbIntermediate).position;
var end=actor.GetBoneTransform(HumanBodyBones.RightThumbDistal).position;
var tip=end+(end-near).normalized*.018f;
float score=(tip-target).sqrMagnitude;
if(score<error){error=score;chosen=pose.muscles[muscleIndex];}
}
pose.muscles[muscleIndex]=chosen;handler.SetHumanPose(ref pose);
}
values["RightHand.Thumb.Spread"]=pose.muscles[spread];
for(int joint=1;joint<=3;joint++)values["RightHand.Thumb."+joint+" Stretched"]=pose.muscles[thumbMuscles[joint]];
Directory.CreateDirectory("SourceArt/Map01_Opening");
File.WriteAllLines("SourceArt/Map01_Opening/finger-calibration.txt",values.Select(v=>v.Key+" = "+v.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
} finally {Object.DestroyImmediate(visual);}
return values;
}
}}
