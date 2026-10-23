using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using ShadowVale.Map01;
using Object=UnityEngine.Object;
namespace ShadowVale.EnvironmentReview
{
    public static partial class Map01MergeIntegration
    {
        static void FixInteractions()
        {
            EditorSceneManager.OpenScene(Stage);var l=Object.FindFirstObjectByType<Map01OperationsLayout>();var c=Camp(2);var front=c.forward;var radio=l.radio.position;
            var stand=radio+front*.70f;stand.y=c.GetComponentsInChildren<BoxCollider>().First(b=>b.name.StartsWith("COLLIDER_Floor")).bounds.max.y;
            l.radioApproach.SetPositionAndRotation(stand,Quaternion.LookRotation(-front));
            l.radioRightContact.position=radio+front*.25f-c.right*.12f+Vector3.up*.285f;
            l.radioLeftContact.position=radio+front*.25f+c.right*.27f+Vector3.up*.30f;
            var catalog=Map01ItemCatalog.Load();var entries=catalog.items.ToList();
            foreach(var pair in new[]{(Map01Story.KeyItem,"WarehouseKey"),(Map01Story.CargoItem,"SupplyBag")}){
                var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01Operations/"+pair.Item2+".fbx");if(model==null)throw new Exception("Missing mission model "+pair.Item2);
                var entry=new Map01ItemCatalog.Entry{id=pair.Item1,model=model,scale=1};int index=entries.FindIndex(e=>e.id==pair.Item1);if(index<0)entries.Add(entry);else entries[index]=entry;
            }
            catalog.items=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            // Observation markers must offer a readable view of the main gate, rather than the rear wall.
            var notes=new List<string>();var sites=new List<Vector3>();Physics.SyncTransforms();
            foreach(float angle in new[]{0f,-4f,4f,-8f,8f,-16f,16f,-24f,24f}){
                var desired=l.commanderPost.position+Quaternion.Euler(0,angle,0)*Vector3.back*32;
                if(!NavMesh.SamplePosition(desired,out var hit,12,NavMesh.AllAreas)||Vector2.Distance(new Vector2(hit.position.x,hit.position.z),new Vector2(desired.x,desired.z))>1)continue;
                if(Physics.Linecast(hit.position+Vector3.up*1.6f,l.commanderPost.position+Vector3.up*1.5f,out var obstruction,~0,QueryTriggerInteraction.Ignore)){notes.Add("BLOCKED "+V(hit.position)+" "+PathOf(obstruction.transform));continue;}
                if(BodyCollisions(hit.position).Length>0)continue;sites.Add(hit.position);if(sites.Count==l.observationSites.Length)break;
            }
            // This only chooses scene markers; the approved dev observation rules remain unchanged.
            if(sites.Count<l.observationSites.Length)throw new Exception("Could not find three readable southern observation sites: "+string.Join("; ",notes));
            for(int i=0;i<sites.Count;i++){l.observationSites[i].position=sites[i];notes.Add("PASS observation "+i+" "+V(sites[i]));}
            File.WriteAllLines(Work+"/observation_review.txt",notes);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();ReviewRoster();
        }
    }
}
