using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    /// <summary>Art inspection and review tooling. Gameplay edits require a separate approved scope.</summary>
    public static class Map01EnvironmentReview
    {
        public const string MapPath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        public const string Root = "Assets/_Project/Art/Environment/Map1_ArtRefresh_v1";
        public const string Work = "SourceArt/Map1_ArtRefresh_20261016";
        [Serializable] public class Item
        {
            public string name, path, prefab;
            public Vector3 position, rotation, scale, boundsCenter, boundsSize;
            public int renderers, colliders, triangles;
            public string[] materials, children;
        }
        [Serializable] public class LightInfo
        {
            public string name, type; public Color color; public float intensity;
            public Vector3 rotation; public bool shadows;
        }
        [Serializable] public class Audit
        {
            public string unityVersion, gpu, scene; public int graphicsMemoryMB;
            public int renderers, lodGroups, colliders;
            public Item[] landmarks, plants;
            public LightInfo[] lights;
            public Color ambient, fogColor; public float ambientIntensity, fogStart, fogEnd;
            public bool fog; public string fogMode;
            public string[] navMeshAssets, groundMaterials;
            public Vector3[] groundVertices;
            public Vector2[] groundUV;
        }

        static string Hierarchy(Transform t)
        {
            return t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
        }
        static Item Inspect(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(t.position, Vector3.zero);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            int tris = 0;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null)
                    for (int i=0;i<mf.sharedMesh.subMeshCount;i++) tris += (int)mf.sharedMesh.GetIndexCount(i)/3;
            return new Item
            {
                name=t.name, path=Hierarchy(t), prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),
                position=t.position, rotation=t.eulerAngles, scale=t.lossyScale,
                boundsCenter=bounds.center, boundsSize=bounds.size, renderers=renderers.Length,
                colliders=t.GetComponentsInChildren<Collider>(true).Length, triangles=tris,
                materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null)
                    .Select(AssetDatabase.GetAssetPath).Distinct().ToArray(),
                children=t.Cast<Transform>().Select(c=>c.name).ToArray()
            };
        }

        [MenuItem("ShadowVale/Art Review/Inspect Map 1 (read only)")]
        public static void AuditMap()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene(MapPath);
            Physics.SyncTransforms();
            var roots=scene.GetRootGameObjects();
            var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var wanted=new HashSet<string>{"Enemy outpost 1","Enemy outpost 2","Enemy outpost 3",
                "A — underground friendly shelter","B — captive jetty","B_HungCaptive","A_BaseReport",
                "C1_MissionAnchor","C2_MissionAnchor","C3_MissionAnchor","Winding river",
                "Continuous hills — 220 x 200"};
            var ground=all.First(t=>t.name=="Continuous hills — 220 x 200").GetComponent<MeshFilter>();
            var report=new Audit
            {
                unityVersion=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
                graphicsMemoryMB=SystemInfo.graphicsMemorySize,scene=scene.path,
                renderers=all.Sum(t=>t.GetComponents<Renderer>().Length),
                lodGroups=all.Sum(t=>t.GetComponents<LODGroup>().Length),colliders=all.Sum(t=>t.GetComponents<Collider>().Length),
                landmarks=all.Where(t=>wanted.Contains(t.name)||t.name.StartsWith("Crossing ")).Select(Inspect).ToArray(),
                plants=all.Where(t=>t.GetComponent<LODGroup>()!=null).Select(Inspect).ToArray(),
                lights=all.SelectMany(t=>t.GetComponents<Light>()).Select(l=>new LightInfo
                {name=l.name,type=l.type.ToString(),color=l.color,intensity=l.intensity,rotation=l.transform.eulerAngles,shadows=l.shadows!=LightShadows.None}).ToArray(),
                ambient=RenderSettings.ambientLight,ambientIntensity=RenderSettings.ambientIntensity,
                fog=RenderSettings.fog,fogMode=RenderSettings.fogMode.ToString(),fogColor=RenderSettings.fogColor,
                fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,
                navMeshAssets=all.SelectMany(t=>t.GetComponents<NavMeshSurface>()).Select(s=>AssetDatabase.GetAssetPath(s.navMeshData)).ToArray(),
                groundMaterials=ground.GetComponent<Renderer>().sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray(),
                groundVertices=ground.sharedMesh.vertices.Select(v=>ground.transform.TransformPoint(v)).ToArray(),
                groundUV=ground.sharedMesh.uv
            };
            Directory.CreateDirectory(Work+"/Audit");
            File.WriteAllText(Work+"/Audit/map_baseline.json",JsonUtility.ToJson(report,true));
            Debug.Log($"MAP ART AUDIT COMPLETE: {report.renderers} renderers, {report.lodGroups} LOD groups. Scene was not saved.");
        }
    }
}
