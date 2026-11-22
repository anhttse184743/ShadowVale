using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
namespace ShadowVale.Map01.Tests {
public sealed class FingerGripIsolationTests {
 [UnityTest] public IEnumerator FingerGripMovesDigitsWithoutChangingBodyPose() {
  yield return new EnterPlayMode();
  Verify();
  yield return new ExitPlayMode();
 }
 static void Verify() {
  var config=Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");Assert.NotNull(config);Assert.NotNull(config.commanderModel);
  var model=UnityEngine.Object.Instantiate(config.commanderModel);
  try {
   var actor=model.GetComponentInChildren<Animator>();Assert.NotNull(actor);Assert.IsTrue(actor.isHuman);actor.Rebind();actor.Update(0);
   var fingers=new HashSet<Transform>();
   foreach(var side in new[]{"Left","Right"})foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"})foreach(var segment in new[]{"Proximal","Intermediate","Distal"}){
    Assert.IsTrue(Enum.TryParse<HumanBodyBones>(side+finger+segment,out var id));var b=actor.GetBoneTransform(id);Assert.NotNull(b);fingers.Add(b);
   }
   var transforms=actor.GetComponentsInChildren<Transform>(true);var positions=new Vector3[transforms.Length];var rotations=new Quaternion[transforms.Length];var scales=new Vector3[transforms.Length];
   for(int i=0;i<transforms.Length;i++){positions[i]=transforms[i].localPosition;rotations[i]=transforms[i].localRotation;scales[i]=transforms[i].localScale;}
   using(var grip=new Map01PaddleGrip(actor)) {
    grip.Set(1,1);int moved=0;
    for(int i=0;i<transforms.Length;i++){
     Assert.Less(Vector3.Distance(positions[i],transforms[i].localPosition),.00001f,transforms[i].name+" position changed");
     Assert.Less(Vector3.Distance(scales[i],transforms[i].localScale),.00001f,transforms[i].name+" scale changed");
     var angle=Quaternion.Angle(rotations[i],transforms[i].localRotation);
     if(fingers.Contains(transforms[i])) {if(angle>1f)moved++;} else Assert.Less(angle,.02f,transforms[i].name+" rotation changed");
    }
    Assert.Greater(moved,5,"Finger bones should visibly curl");
   }
  } finally {UnityEngine.Object.Destroy(model);}
 }
}
}
