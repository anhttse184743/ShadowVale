using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class CompactEnemyCamps
    {
        const string Scene="Assets/_Project/Scenes/Maps/Map 1.unity";
        const string Output="Assets/_Project/Art/Environment/CompactCamps";
        const string Models="Assets/_Project/Art/Environment/FieldCamp/Prefabs/";
        static GameObject Put(string asset,Transform parent,Vector3 position,Vector3 scale)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
            if(!prefab)throw new Exception("Missing asset: "+asset);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(parent,false);
            g.transform.localPosition=position;g.transform.localScale=scale;return g;
        }
        static void Box(Transform parent,string name,Vector3 at,Vector3 size,Material mat,bool solid)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
            g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        public static void Build()
        {
            if(Directory.Exists(Output))throw new Exception("Compact camps already installed; preserving edits.");
            var scene=EditorSceneManager.OpenScene(Scene);
            var guards=Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).Where(g=>g.name.StartsWith("Outpost guard ")).ToArray();
            if(guards.Length!=6)throw new Exception("Expected six outpost guards; inspect changed scene before editing.");
            var camps=Enumerable.Range(1,3).Select(i=>GameObject.Find("Enemy outpost "+i).transform).ToArray();
            var assignments=guards.ToDictionary(g=>g,g=>camps.OrderBy(c=>(c.position-g.transform.position).sqrMagnitude).First());
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/FieldCamp/Materials/Worn timber.mat");
            var cloth=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/FieldCamp/Materials/Weathered olive canvas.mat");
            foreach(var camp in camps)
            {
                var old=camp.Find("Field camp models");if(!old)throw new Exception("Missing original camp content: "+camp.name);
                Object.DestroyImmediate(old.gameObject);
                var content=new GameObject("Compact field camp — 12 x 10 m").transform;content.SetParent(camp,false);
                Put(Models+"CommandTent.prefab",content,new Vector3(-2,0,2),new Vector3(.65f,.9f,.57f));
                Put(Models+"PlanningTable.prefab",content,new Vector3(-2,0,2),Vector3.one*.72f);
                Put(Models+"RadioStation.prefab",content,new Vector3(-2,0,3.15f),Vector3.one*.65f);
                Put(Models+"SupplyStack.prefab",content,new Vector3(3,0,2.5f),Vector3.one*.65f);
                foreach(float x in new[]{2f,4f})foreach(float z in new[]{1.5f,3.5f})Box(content,"Supply tarp post",new Vector3(x,1.1f,z),new Vector3(.09f,2.2f,.09f),wood,true);
                Box(content,"Supply tarp",new Vector3(3,2.23f,2.5f),new Vector3(2.3f,.06f,2.3f),cloth,false);
                Put(Models+"CampfireKitchen.prefab",content,new Vector3(-2.2f,0,-2.25f),Vector3.one*.72f);
                foreach(float x in new[]{-3.6f,3.6f})Put(Models+"SandbagWall.prefab",content,new Vector3(x,0,-4.7f),Vector3.one);
                // Reclaim the old courtyard edges, leaving south, east and west approach corridors open.
                foreach(var p in new[]{new Vector3(-6.3f,0,3),new Vector3(6.3f,0,3),new Vector3(-4,0,5.3f),new Vector3(0,0,5.5f),new Vector3(4.5f,0,5.4f),new Vector3(-6.3f,0,-3),new Vector3(6.3f,0,-3)})
                    Put("Assets/_Project/Art/Environment/ForestV2/Prefabs/CoverShrub_A.prefab",content,p,Vector3.one*.9f);
                var localGuards=guards.Where(g=>assignments[g]==camp).OrderBy(g=>g.name).ToArray();
                if(localGuards.Length!=2)throw new Exception("Expected two guards at "+camp.name);
                for(int i=0;i<2;i++)
                {
                    var g=localGuards[i];var points=new[]{new Vector3(1,0,-2.5f),new Vector3(3.5f,0,-2.5f),new Vector3(3.5f,0,-.25f),new Vector3(1,0,-.25f)};
                    var loop=Enumerable.Range(0,4).Select(j=>camp.TransformPoint(points[(j+i*2)%4])).ToArray();
                    g.transform.position=loop[0];g.Configure(loop);
                    var serialized=new SerializedObject(g);serialized.FindProperty("visionRange").floatValue=14;
                    serialized.FindProperty("attackRange").floatValue=12;serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var surfaces=Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            if(surfaces.Length!=1)throw new Exception("Expected one navigation surface.");
            surfaces[0].RemoveData();surfaces[0].BuildNavMesh();AssetDatabase.CreateAsset(surfaces[0].navMeshData,Output+"/Navigation.asset");
            foreach(var g in guards){
                if(!NavMesh.SamplePosition(g.transform.position,out var hit,1,NavMesh.AllAreas))throw new Exception("Guard off navmesh "+g.name);
                g.transform.position=hit.position;
                foreach(var p in g.PatrolPoints){var path=new NavMeshPath();if(!NavMesh.CalculatePath(hit.position,p,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Blocked patrol "+g.name);}
            }
            var anchors=new[]{"A_BaseReport","B_HungCaptive","C1_MissionAnchor","C2_MissionAnchor","C3_MissionAnchor"};
            if(!NavMesh.SamplePosition(GameObject.Find(anchors[0]).transform.position,out var start,3,NavMesh.AllAreas))throw new Exception("Base off navmesh");
            foreach(string name in anchors.Skip(1)){
                if(!NavMesh.SamplePosition(GameObject.Find(name).transform.position,out var end,3,NavMesh.AllAreas))throw new Exception("Unreachable "+name);
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Broken story route "+name);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Render();Debug.Log("COMPACT CAMPS PASS: six guards, all patrol paths and five story anchors connected.");
        }
        public static void Render()
        {
            EditorSceneManager.OpenScene(Scene);Directory.CreateDirectory("Logs/CompactCamps");
            foreach(var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)){ps.Simulate(2,true,true);ps.Pause();}
            var center=GameObject.Find("Enemy outpost 1").transform.position;var cam=Camera.main;
            cam.transform.position=center+new Vector3(8,9,-10);cam.transform.LookAt(center+Vector3.up);cam.fieldOfView=60;cam.orthographic=false;
            var rt=RenderTexture.GetTemporary(1600,1000,24);var previous=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("Logs/CompactCamps/overview.png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
        }
    }
}
