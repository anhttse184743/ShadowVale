using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using ShadowVale.Map01;
using Object=UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    [InitializeOnLoad]
    public static partial class Map01MergeIntegration
    {
        public const string Work="Docs/Reviews/MergeDev_20261016";
        public const string Root="Assets/_Project/Art/Environment/Map1_ArtRefresh_v1";
        public const string Map="Assets/_Project/Scenes/Maps/Map 1.unity";
        public const string Stage=Root+"/MergeReview/Map 1.unity";
        static double next;
        static Map01MergeIntegration(){Directory.CreateDirectory(Work);File.WriteAllText(Work+"/tool_version.txt","12");EditorApplication.update+=Check;}
        static void Check()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            string request=Work+"/integration.request";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var action=File.ReadAllText(request).Trim().TrimStart('\uFEFF');File.Move(request,request+"."+DateTime.UtcNow.Ticks+".consumed");
            if(action=="REVIEW"){EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/integration_status.txt","RUNNING REVIEW");ReviewRoster();File.WriteAllText(Work+"/integration_status.txt","COMPLETE REVIEW");}catch(Exception e){File.WriteAllText(Work+"/integration_status.txt","FAILED "+e);Debug.LogException(e);}};return;}
            if(action=="BUILD_BENCH"){EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/integration_status.txt","RUNNING BUILD_BENCH");BuildMergedBenchmark();File.WriteAllText(Work+"/integration_status.txt","COMPLETE BUILD_BENCH");}catch(Exception e){File.WriteAllText(Work+"/integration_status.txt","FAILED "+e);Debug.LogException(e);}};return;}
            if(action=="FIX"){EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/integration_status.txt","RUNNING FIX");FixInteractions();File.WriteAllText(Work+"/integration_status.txt","COMPLETE FIX");}catch(Exception e){File.WriteAllText(Work+"/integration_status.txt","FAILED "+e);Debug.LogException(e);}};return;}
            if(action=="FINISH"){EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/integration_status.txt","RUNNING FINISH");FinishReview();File.WriteAllText(Work+"/integration_status.txt","COMPLETE FINISH");}catch(Exception e){File.WriteAllText(Work+"/integration_status.txt","FAILED "+e);Debug.LogException(e);}};return;}
            if(action=="BUILD_GAME"){EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/integration_status.txt","RUNNING BUILD_GAME");BuildMergedGame();File.WriteAllText(Work+"/integration_status.txt","COMPLETE BUILD_GAME");}catch(Exception e){File.WriteAllText(Work+"/integration_status.txt","FAILED "+e);Debug.LogException(e);}};return;}
            EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/integration_status.txt","RUNNING "+action);if(action=="DUMP")Ready();else if(action=="DETAIL")Detail();else if(action=="ADAPT")Adapt();else if(action=="NAV"){if(SceneManager.GetActiveScene().path!=Stage)throw new Exception("Open integration stage first");try{BakeAndValidate(Object.FindFirstObjectByType<Map01OperationsLayout>());}finally{EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();}}else if(action=="PUBLISH")Publish();else if(action.StartsWith("TEST "))RunTests(action.Substring(5));else throw new Exception("Unknown integration request "+action);File.WriteAllText(Work+"/integration_status.txt","COMPLETE "+action);}catch(Exception e){File.WriteAllText(Work+"/integration_status.txt","FAILED "+e);Debug.LogException(e);}};
        }
        public static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode only");
            EditorSceneManager.OpenScene(Map);
            Dump();File.WriteAllText(Work+"/integration_status.txt","READY; merged scripts compiled; world adaptation pending");
        }
        static string PathOf(Transform t){if(t==null)return "null";string path=t.name;while(t.parent!=null){t=t.parent;path=t.name+"/"+path;}return path;}
        static string V(Vector3 p)=>string.Format(System.Globalization.CultureInfo.InvariantCulture,"({0:F3},{1:F3},{2:F3})",p.x,p.y,p.z);
        static string T(Transform t)=>t==null?"null":PathOf(t)+" position="+V(t.position)+" forward="+V(t.forward);
        public static void Dump()
        {
            var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var l=Object.FindFirstObjectByType<Map01OperationsLayout>();var rows=new List<string>{"Scene "+SceneManager.GetActiveScene().path,"Layout "+(l!=null)};
            foreach(var name in new[]{"C1 / Mission art","C2 / Mission art","C3 / Mission art"})
            {
                var camp=all.Single(t=>t.name==name);rows.Add("CAMP "+T(camp));
                rows.AddRange(camp.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Door")||t.name.Contains("Hinge")||t.name.Contains("radio")||t.name.Contains("Desk")).Select(t=>"PART "+T(t)));
            }
            if(l!=null)
            {
                foreach(var pair in new[]{("supply",l.supply),("supplyApproach",l.supplyApproach),("radio",l.radio),("radioApproach",l.radioApproach),("workbench",l.radioWorkbench),("commander",l.commanderPost),("door",l.warehouseDoor),("doorApproach",l.doorApproach),("chestApproach",l.chestApproach),("handoff",l.handoff)})rows.Add(pair.Item1+" "+T(pair.Item2));
                rows.AddRange(l.GetComponentsInChildren<Collider>(true).Select(c=>"OP COLLIDER "+PathOf(c.transform)+" "+V(c.bounds.center)+" size "+V(c.bounds.size)+" active "+c.gameObject.activeInHierarchy));
            }
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Meshes/C1_ApprovedArt.fbx");
            if(source!=null)rows.AddRange(source.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Door")||t.name.Contains("Hinge")).Select(t=>"FBX "+PathOf(t)+" local="+V(t.localPosition)+" rotation="+V(t.localEulerAngles)+" renderer="+(t.GetComponent<MeshRenderer>()!=null)));
            rows.AddRange(Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Select(a=>"AGENT "+a.name+" "+V(a.transform.position)+" enabled="+a.enabled));
            File.WriteAllLines(Work+"/world_before_adaptation.txt",rows);
        }
    }
}

