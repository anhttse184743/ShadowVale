using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class ForestFoliageUpdate
    {
        const string Root="Assets/_Project/Art/Environment/Map01_Optimized";
        const string Reports="Tools/Map01OptimizedReports";
        static ForestFoliageUpdate(){EditorApplication.update+=Poll;File.WriteAllText(Reports+"/foliage.ready","1");}
        static void Poll()
        {
            string request="Tools/Map01Foliage.request";
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
            File.Delete(request);
            try{Update();File.WriteAllText(Reports+"/foliage.status","PASS "+DateTime.UtcNow.ToString("O"));}
            catch(Exception e){File.WriteAllText(Reports+"/foliage.status","FAIL "+e);Debug.LogException(e);}
        }
        [MenuItem("ShadowVale/Map 1/Update Foliage Meshes Only")]
        public static void Update()
        {
            OptimizedMapBuilder.SourceData data;
            using(var stream=File.OpenRead("SourceArt/Map01_Optimized/Map01.meshdata.json.gz"))using(var gz=new GZipStream(stream,CompressionMode.Decompress))using(var reader=new StreamReader(gz))data=JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(reader.ReadToEnd());
            foreach(var src in data.meshes.Where(m=>m.kind=="tree"))
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+src.id+".asset");if(mesh==null)throw new Exception("Missing shared tree mesh "+src.id);
                mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;int n=src.vertices.Length/3;
                var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];
                for(int i=0;i<n;i++){v[i]=new Vector3(src.vertices[i*3],src.vertices[i*3+1],src.vertices[i*3+2]);normals[i]=new Vector3(src.normals[i*3],src.normals[i*3+1],src.normals[i*3+2]);colors[i]=new Color(src.colors[i*4],src.colors[i*4+1],src.colors[i*4+2],1);}
                mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.triangles=src.triangles;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();
            for(int i=0;i<5;i++)
            {
                string path=Root+"/Prefabs/Tree_"+i+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{var group=root.GetComponent<LODGroup>();var lods=group.GetLODs();lods[0].screenRelativeTransitionHeight=.13f;lods[1].screenRelativeTransitionHeight=.035f;group.SetLODs(lods);group.RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();SceneView.RepaintAll();OptimizedMapBuilder.Capture();
            File.WriteAllLines(Reports+"/foliage-meshes.txt",data.meshes.Where(m=>m.kind=="tree").Select(m=>m.id+": "+m.triangles.Length/3+" triangles"));
        }
    }
}
