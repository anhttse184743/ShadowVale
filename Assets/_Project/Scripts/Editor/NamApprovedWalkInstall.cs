using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class NamApprovedWalkInstall {
 const string Prefab="Assets/_Project/Prefabs/Player/Player.prefab";
 const string Base="Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";
 const string Old="Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_WalkLow_F.fbx";
 const string Candidate="Assets/_Project/Art/Characters/NamAoBaBa_20261111/Animations/Nam_Rifle_Walk_Approved01.fbx";
 const string Override="Assets/_Project/Art/Characters/NamAoBaBa_20261111/Animations/Nam_ApprovedWalk_Override.overrideController";
 public static void Run(){try{
  if(File.Exists(Override))throw new InvalidOperationException("Override asset already exists; refusing to overwrite.");
  var baseController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Base);if(baseController==null)throw new InvalidOperationException("Base controller missing.");
  var old=AssetDatabase.LoadAllAssetsAtPath(Old).OfType<AnimationClip>().Single(c=>c.name=="Nam_Rifle_WalkLow_F");
  var clip=AssetDatabase.LoadAllAssetsAtPath(Candidate).OfType<AnimationClip>().Single(c=>c.name=="Nam_Rifle_WalkLow_F_Approved01");
  if(!clip.humanMotion||!AnimationUtility.GetAnimationClipSettings(clip).loopTime||Mathf.Abs(clip.length-1)>.05f)throw new InvalidOperationException("Candidate clip configuration invalid.");
  var prefab=PrefabUtility.LoadPrefabContents(Prefab);
  try{
   var actor=prefab.GetComponentsInChildren<Animator>(true).Single();
   if(actor.runtimeAnimatorController!=baseController)throw new InvalidOperationException("Player controller changed; refusing to replace it.");
   if(actor.transform.Find("Nam_AoBaBa_Visual")==null)throw new InvalidOperationException("Approved appearance missing.");
   var avatar=actor.avatar;var mapping=new AnimatorOverrideController(baseController);mapping.name="Nam approved forward walk";
   var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();mapping.GetOverrides(pairs);int index=pairs.FindIndex(x=>x.Key==old);
   if(index<0)throw new InvalidOperationException("Original forward clip is not used by this controller.");
   pairs[index]=new KeyValuePair<AnimationClip,AnimationClip>(old,clip);mapping.ApplyOverrides(pairs);
   AssetDatabase.CreateAsset(mapping,Override);AssetDatabase.SaveAssets();
   actor.runtimeAnimatorController=mapping;if(actor.avatar!=avatar)throw new InvalidOperationException("Avatar changed unexpectedly.");
   PrefabUtility.SaveAsPrefabAsset(prefab,Prefab,out bool saved);if(!saved)throw new InvalidOperationException("Unable to save prefab.");
  }finally{PrefabUtility.UnloadPrefabContents(prefab);}
  AssetDatabase.SaveAssets();AssetDatabase.Refresh();
  var written=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponentsInChildren<Animator>(true).Single();
  var assigned=written.runtimeAnimatorController as AnimatorOverrideController;if(assigned==null||assigned[old]!=clip)throw new InvalidOperationException("Prefab override verification failed.");
  Debug.Log("NAM_APPROVED_WALK_INSTALLED: forward slot override; base controller and other slots untouched.");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
