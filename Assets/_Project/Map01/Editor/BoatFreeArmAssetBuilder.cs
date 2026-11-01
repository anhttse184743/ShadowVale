using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
 public static class BoatFreeArmAssetBuilder {
  [MenuItem("ShadowVale/Cutscene/Prepare boat free arms")]
  public static void Build(){
   const string target="Assets/Resources/Cutscenes/Map02/Nam_Boat_Free_Arms.anim";
   if(AssetDatabase.LoadAssetAtPath<AnimationClip>(target)!=null)return;
   var source=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Clips/Standard Walk.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
   var copy=Object.Instantiate(source);copy.name="Nam_Boat_Free_Arms";AssetDatabase.CreateAsset(copy,target);AssetDatabase.SaveAssets();
   Debug.Log("Boat free arms copied from existing Standard Walk, arms-only runtime mask; no rig changes.");
  }
 }
}
