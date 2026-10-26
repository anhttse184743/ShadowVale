using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using Object=UnityEngine.Object;
public static class ScoutMapSpawnAudit
{
 static Vector3 scoutBridgeEntry(Map02ChapterDirector chapter)=>(Vector3)typeof(Map02ChapterDirector).GetField("scoutBridgeEntry",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
 public static void Run()
 {
  var report=new List<string>();var data=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset");var nav=NavMesh.AddNavMeshData(data);
  var root=new GameObject("Actual Map 2 ambush audit");
  try
  {
   var chapter=root.AddComponent<Map02ChapterDirector>();chapter.enabled=false;
   var sites=(Dictionary<string,Transform>)typeof(Map02ChapterDirector).GetField("sites",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
   foreach(var item in new[]{("scout",new Vector3(29,.77f,-16.66f)),("bridge",new Vector3(29,.73f,-4.66f)),("command",new Vector3(-79.29f,1.1f,10.91f))})
   {var site=new GameObject(item.Item1);site.transform.SetParent(root.transform);site.transform.position=NavMesh.SamplePosition(item.Item2,out var hit,12,NavMesh.AllAreas)?hit.position:item.Item2;sites[item.Item1]=site.transform;report.Add(item.Item1+"="+site.transform.position.ToString("F3"));}
   foreach(var loc in new[]{new Vector3(-6.764572f,1,-5.646862f),new Vector3(-13.1754265f,1,-8.113138f)})
   {var deck=new GameObject("Walkable bridge deck");deck.transform.SetParent(root.transform);deck.transform.position=loc;deck.transform.rotation=new Quaternion(0,-.18237078f,0,.9832298f);deck.transform.localScale=new Vector3(.88f,1,1);deck.AddComponent<BoxCollider>().size=new Vector3(24,.18f,3.7f);}
   var prepare=typeof(Map02ChapterDirector).GetMethod("PrepareScoutBridge",BindingFlags.Instance|BindingFlags.NonPublic);prepare.Invoke(chapter,null);
   foreach(var field in new[]{"scoutBridgeEntry","scoutBridgeForward"})report.Add(field+"="+typeof(Map02ChapterDirector).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter));
   var cross=typeof(Map02ChapterDirector).GetMethod("UpdateScoutBridgeCrossing",BindingFlags.Instance|BindingFlags.NonPublic);report.Add("crossed at rescue="+cross.Invoke(chapter,new object[]{sites["scout"].position}));
   var entry=(Vector3)typeof(Map02ChapterDirector).GetField("scoutBridgeEntry",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
   var forward=(Vector3)typeof(Map02ChapterDirector).GetField("scoutBridgeForward",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
   bool endok=NavMesh.SamplePosition(entry-forward*3,out var end,2,NavMesh.AllAreas);report.Add("end sample="+endok+" "+end.position);
   var find=typeof(Map02ChapterDirector).GetMethod("TryScoutAmbushPoint",BindingFlags.Instance|BindingFlags.NonPublic);
   typeof(Map02ChapterDirector).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(chapter,root);
   var spawn=typeof(Map02ChapterDirector).GetMethod("SpawnEnemy",BindingFlags.Instance|BindingFlags.NonPublic);
   chapter.State.scoutRescueOriginSet=true;chapter.State.scoutRescueOrigin=sites["scout"].position;
   if(chapter.State.TryStartScoutAmbush(sites["scout"].position+Vector3.forward*3.99f))throw new Exception("Ambush triggered too early");
   if(!chapter.State.TryStartScoutAmbush(sites["scout"].position+Vector3.forward*4))throw new Exception("Ambush missing at 4 m");
   report.Add("PASS trigger stays off at 3.99 m and turns on at 4 m on actual Map 2");
   for(int i=0;i<5;i++)
   {
    object[] args={i,Vector3.zero};bool ok=(bool)find.Invoke(chapter,args);var point=(Vector3)args[1];
    if(!ok)throw new Exception("No valid actual Map 2 spawn for soldier "+i);
    report.Add("PASS spawn "+i+" valid="+ok+" point="+point.ToString("F3"));
    if((bool)cross.Invoke(chapter,new object[]{point}))throw new Exception("Field bank incorrectly treated as bridge");
    if(i==0)
    {
     var nam=sites["scout"].position+Vector3.forward*4;var tam=nam+Vector3.left*.48f;
     if(!Map02ChapterDirector.ScoutAmbushDistanceClear(point,nam,tam))throw new Exception("First enemy blocked by 20 m clearance at 4 m trigger");
     report.Add("PASS first enemy clears both actors at trigger: Nam="+Vector3.ProjectOnPlane(point-nam,Vector3.up).magnitude+" m; Tam="+Vector3.ProjectOnPlane(point-tam,Vector3.up).magnitude+" m");
    }
    if(!Map02ChapterDirector.CanSpawnScoutAmbush(i,chapter.Enemies.Any(e=>!e.Dead)))throw new Exception("Next enemy incorrectly blocked");
    spawn.Invoke(chapter,new object[]{"audit_"+i,Map02ChapterDirector.ScoutAmbushGroup,point,100f,true,false});
    if(chapter.Enemies.Count(e=>!e.Dead)!=1||Map02ChapterDirector.CanSpawnScoutAmbush(i+1,true))throw new Exception("Sequential enemy invariant failed");
    report.Add("PASS actual enemy "+i+" created on Map 2 NavMesh; only one alive");
    chapter.Enemies.Last().HitPoints.RestoreHealth(0);
   }
   var chosen=scoutBridgeEntry(chapter);
   if(!(bool)cross.Invoke(chapter,new object[]{chosen}))throw new Exception("Actual bridge entrance did not stop spawning");
   report.Add("PASS actual bridge deck stops spawning; field points do not");

  }finally{Object.DestroyImmediate(root);nav.Remove();File.WriteAllLines("map2-spawn-audit.txt",report);Debug.Log(string.Join("\n",report));}
 }
}
