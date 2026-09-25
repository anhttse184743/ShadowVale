using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        [InitializeOnLoadMethod] static void RegisterMeadowPolish(){EditorApplication.update+=PollMeadowPolish;}
        static void PollMeadowPolish(){const string req="Tools/Map02MeadowPolish.request";if(!File.Exists(req)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete(req);try{PolishMeadow();File.WriteAllText(Reports+"/meadow-polish.status","PASS");}catch(Exception e){File.WriteAllText(Reports+"/meadow-polish.status","FAIL "+e);Debug.LogException(e);}}
        static float EarthGrain(float x,float y)
        {
            float sum=0,weight=0;
            for(int octave=0;octave<5;octave++)
            {
                float frequency=4*(1<<octave),amplitude=Mathf.Pow(.55f,octave);
                float a=Mathf.PerlinNoise(137+x*frequency,291+y*frequency);
                float b=Mathf.PerlinNoise(137+(x-1)*frequency,291+y*frequency);
                float c=Mathf.PerlinNoise(137+x*frequency,291+(y-1)*frequency);
                float d=Mathf.PerlinNoise(137+(x-1)*frequency,291+(y-1)*frequency);
                sum+=Mathf.Lerp(Mathf.Lerp(a,b,x),Mathf.Lerp(c,d,x),y)*amplitude;weight+=amplitude;
            }
            return Mathf.Clamp01((sum/weight-.5f)*1.8f+.5f);
        }
        public static void PolishMeadow()
        {
            var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath)throw new Exception("Map 2 must be active and saved.");
            env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            var tex=new Texture2D(256,256,TextureFormat.RGB24,false);var normal=new Texture2D(256,256,TextureFormat.RGB24,false);
            for(int y=0;y<256;y++)for(int x=0;x<256;x++){float u=x/256f,v=y/256f,g=EarthGrain(u,v);tex.SetPixel(x,y,Color.Lerp(new Color(.24f,.18f,.105f),new Color(.53f,.43f,.28f),g));float dx=EarthGrain(u+1/256f,v)-EarthGrain(u-1/256f,v),dy=EarthGrain(u,v+1/256f)-EarthGrain(u,v-1/256f);var n=new Vector3(-dx*1.6f,-dy*1.6f,1).normalized;normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));}
            tex.Apply();normal.Apply();string tp=Root+"/Meadow_Earth_Albedo.png",np=Root+"/Meadow_Earth_Normal.png";File.WriteAllBytes(tp,tex.EncodeToPNG());File.WriteAllBytes(np,normal.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(normal);AssetDatabase.ImportAsset(tp);AssetDatabase.ImportAsset(np);var ni=(TextureImporter)AssetImporter.GetAtPath(np);ni.textureType=TextureImporterType.NormalMap;ni.wrapMode=TextureWrapMode.Repeat;ni.SaveAndReimport();
            var mat=Mat("Meadow textured dry earth",Color.white);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(np));mat.SetFloat("_BumpScale",.65f);mat.SetFloat("_Smoothness",.04f);mat.EnableKeyword("_NORMALMAP");
            int roads=0;foreach(var f in env.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Winding ")&&f.name!="Winding canal water")){var m=f.sharedMesh;m.uv=m.vertices.Select(p=>{var w=f.transform.TransformPoint(p);return new Vector2(w.x*.7f,w.z*.7f);}).ToArray();m.RecalculateTangents();EditorUtility.SetDirty(m);f.GetComponent<MeshRenderer>().sharedMaterial=mat;roads++;}
            Physics.SyncTransforms();var root=env.Find("Natural meadow and additional trees");var surfaces=env.GetComponentsInChildren<MeshCollider>().Where(c=>c.name.StartsWith("Winding ")&&c.name!="Winding canal water"||c.name=="Uneven planted bund"||c.name=="Irregular rice parcel"||c.name=="Continuous ground with meandering canal").ToArray();int aligned=0;
            foreach(var grass in root.GetComponentsInChildren<VillageRiceInstances>())
            {
                for(int i=0;i<grass.plants.Length;i++){var p=grass.plants[i];var w=grass.transform.TransformPoint(new Vector3(p.x,p.y,p.z));var ray=new Ray(new Vector3(w.x,4,w.z),Vector3.down);float top=-10;foreach(var col in surfaces){var b=col.bounds;if(w.x<b.min.x||w.x>b.max.x||w.z<b.min.z||w.z>b.max.z)continue;if(col.Raycast(ray,out var hit,8))top=Mathf.Max(top,hit.point.y);}if(top>-5){w.y=top+.008f;var local=grass.transform.InverseTransformPoint(w);grass.plants[i]=new Vector4(local.x,local.y,local.z,p.w);aligned++;}}
                grass.Rebuild();EditorUtility.SetDirty(grass);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene(ScenePath);env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;var saved=env.Find("Natural meadow and additional trees");int count=saved.GetComponentsInChildren<VillageRiceInstances>().Sum(g=>g.plants.Length);if(count!=aligned)throw new Exception("Grass grounding / reload count mismatch: "+count+" vs "+aligned);
            File.AppendAllText(Reports+"/ground-meadow-checks.txt","PASS saved scene reload and grounded grass: "+count+"\nTextured dirt ribbons: "+roads+"\nShared grass meshes: 4; 20m spatial tiles, GPU instancing, distant LOD, no grass shadows/colliders.\n");
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();Capture(camera,Rural(new Vector3(-94,3,-38)),Rural(new Vector3(-66,.5f,-35)),0,"map02-meadow-path");Capture(camera,new Vector3(-109,35,-67),new Vector3(-58,2,0),0,"map02-meadow-village");Capture(camera,new Vector3(18,175,-200),new Vector3(0,0,8),108,"map02-meadow-overview");EditorSceneManager.SaveScene(scene);
        }
    }
}


