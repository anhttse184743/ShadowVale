using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class ForestResearchSetup
    {
        const string Request="Tools/Map01Research.request", Reports="Tools/Map01OptimizedReports";
        static ForestResearchSetup(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);
            try{if(command=="apply")Apply();else if(command=="benchmark")ForestResearchValidation.Benchmark();else if(command=="playcheck")ForestResearchValidation.PlayCheck();}
            catch(Exception e){File.WriteAllText(Reports+"/research-status.txt","FAIL "+e);Debug.LogException(e);}
        }
        [MenuItem("ShadowVale/Research/Apply Map 1 enemy squads")]
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=OptimizedMapBuilder.ScenePath||scene.isDirty||EditorApplication.isPlaying)throw new Exception("Save Map 1 and exit Play Mode first.");
            File.Copy(scene.path,Reports+"/Map1_before_research.unity",true);
            var mission=Object.FindFirstObjectByType<ForestMission>();
            var coordinator=mission.GetComponent<ForestSquadCoordinator>();if(coordinator==null)coordinator=mission.gameObject.AddComponent<ForestSquadCoordinator>();
            coordinator.researchConfig=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Map01/Map01Research.json");
            if(coordinator.researchConfig==null)throw new Exception("Missing Map01Research.json");
            var guards=Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);
            var prototype=guards.Single(g=>g.id=="patrol_0");
            const string prefabPath="Assets/_Project/Map01/Prefabs/RBL_RifleGuard.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            var temp=Object.Instantiate(prototype.gameObject);temp.name="RBL Rifle Guard";temp.transform.SetParent(null);temp.transform.position=Vector3.zero;
            var template=temp.GetComponent<ForestGuard>();template.id="";template.patrol=Array.Empty<Vector3>();template.squadId="";template.isCommander=false;template.state=ForestGuardState.Patrol;
            var prefab=PrefabUtility.SaveAsPrefabAsset(temp,prefabPath);Object.DestroyImmediate(temp);
            Add("rbl_patrol_3",prototype,2,new Vector3(1.1f,0,0));
            var east=guards.Single(g=>g.id=="expansion_guard_0");
            Add("rbl_east_2",east,1,new Vector3(0,0,1.2f));Add("rbl_east_3",east,3,new Vector3(0,0,-1.2f));
            void Add(string id,ForestGuard source,int phase,Vector3 offset)
            {
                if(Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None).Any(g=>g.id==id))return;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,source.transform.parent);go.name=id;var guard=go.GetComponent<ForestGuard>();guard.id=id;
                var points=new List<Vector3>();
                for(int i=0;i<source.patrol.Length;i++){var p=source.patrol[(phase+i)%source.patrol.Length]+offset;if(!NavMesh.SamplePosition(p,out var hit,2,NavMesh.AllAreas))throw new Exception("Spawn off NavMesh: "+id);points.Add(hit.position);}
                guard.patrol=points.ToArray();go.transform.position=points[0];PrefabUtility.RecordPrefabInstancePropertyModifications(guard);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            guards=Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);
            foreach(var guard in guards)
            {
                guard.squadId=guard.id.StartsWith("patrol_")||guard.id=="rbl_patrol_3"?"Bridge patrol":guard.id=="expansion_guard_0"||guard.id=="expansion_guard_1"||guard.id.StartsWith("rbl_east_")?"East camp":guard.id=="expansion_guard_2"||guard.id=="expansion_guard_3"?"Northwest post":"Northeast post";
                guard.isCommander=guard.id=="patrol_0"||guard.id=="expansion_guard_0";
                if(PrefabUtility.IsPartOfPrefabInstance(guard))PrefabUtility.RecordPrefabInstancePropertyModifications(guard);
                foreach(var collider in guard.GetComponentsInChildren<Collider>())collider.gameObject.layer=LayerMask.NameToLayer("Enemy");
                for(int i=0;i<guard.patrol.Length;i++)
                {var path=new NavMeshPath();if(!NavMesh.CalculatePath(guard.patrol[i],guard.patrol[(i+1)%guard.patrol.Length],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Disconnected guard route: "+guard.id);}
            }
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Reports+"/research-status.txt",$"PASS: {guards.Length} guards, {guards.Count(g=>g.isCommander)} commanders; all patrol loops connected; research config assigned; Map 1 saved.");
        }
    }
}
