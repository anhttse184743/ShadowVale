using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Editor {
public static class UrgentBoardingSetup {
public static void Prepare() {
const string path="Assets/_Project/Map01/Resources/Cutscenes/Nam_Urgent_Board_Jump.anim";
var source=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Clips/Jump.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
if(!source.humanMotion)throw new System.InvalidOperationException("Jump must be Humanoid.");
var clip=Object.Instantiate(source);clip.name="Nam_Urgent_Board_Jump";clip.frameRate=30;
var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(existing!=null){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,path);
var run=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Run.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
var prefab=PrefabUtility.LoadPrefabContents(ExtractionSetup.PrefabPath);
try{prefab.GetComponent<ShadowVale.Map01.Map01Extraction>().gameplayRifleRun=run;PrefabUtility.SaveAsPrefabAsset(prefab,ExtractionSetup.PrefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
Debug.Log("Explicit gameplay rifle run: "+run.name);
AssetDatabase.SaveAssets();Debug.Log("Prepared Humanoid urgent boarding jump.");
}}}

