using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.EnvironmentReview
{
 public static partial class NativeWeaponPreviewTools
 {
  static void Inspect()
  {
   var p=UnityEngine.Object.FindFirstObjectByType<NativeWeaponPreviewPlayer>();
   if(p==null)throw new Exception("Preview not open");
   var rows=new List<string>();
   foreach(var skin in p.actor.GetComponentsInChildren<SkinnedMeshRenderer>())rows.Add("SKIN "+skin.name+" bones="+skin.bones.Length+" weights="+skin.sharedMesh.boneWeights.Length+" root="+skin.rootBone?.name);
   foreach(var t in p.takes){var curves=AnimationUtility.GetCurveBindings(t.clip);rows.Add(t.clip.name+" curves="+curves.Length+" bone="+curves.Count(c=>c.path.Contains("RightHand")||c.path.Contains("RightArm"))+" samples="+string.Join(";",curves.Take(5).Select(c=>c.path+":"+c.propertyName)));}
   foreach(string name in new[]{"pistol_k54__aim","rifle_mosin__bolt","grenade_f1__throw","ak47__reload"}){
    int n=Array.FindIndex(p.takes,t=>t.clip.name==name);p.Sample(n,p.takes[n].clip.length*.3f);
    var transforms=p.actor.GetComponentsInChildren<Transform>(true);rows.Add(name+" RH="+transforms.First(t=>t.name=="RightHand").position.ToString("F5")+" LH="+transforms.First(t=>t.name=="LeftHand").position.ToString("F5"));
    foreach(var skin in p.actor.GetComponentsInChildren<SkinnedMeshRenderer>()){var mesh=new Mesh();skin.BakeMesh(mesh);rows.Add("BAKED "+skin.name+" bounds="+mesh.bounds+" v0="+mesh.vertices[0].ToString("F5"));UnityEngine.Object.DestroyImmediate(mesh);}
   }
   File.WriteAllLines(Work+"/native_pose_probe.txt",rows);p.Release();
  }
 }
}
