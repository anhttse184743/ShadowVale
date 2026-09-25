using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        const int JoinMaskSize=2048;
        [InitializeOnLoadMethod] static void RegisterJoinedSoil(){EditorApplication.update+=PollJoinedSoil;}
        static void PollJoinedSoil()
        {
            const string request="Tools/Map02JoinedSoil.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            try{JoinMap02Soil();File.WriteAllText(Reports+"/joined-soil.status","PASS "+DateTime.Now.ToString("O"));}
            catch(Exception e){File.WriteAllText(Reports+"/joined-soil.status","FAIL "+e);Debug.LogException(e);}
        }
        static Vector2 MaskPoint(Vector3 p)=>new Vector2((p.x+110)/220*(JoinMaskSize-1),(p.z+100)/200*(JoinMaskSize-1));
        static float Cross2(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static void RasterSurface(MeshFilter f,float[] mask)
        {
            var v=f.sharedMesh.vertices.Select(p=>MaskPoint(f.transform.TransformPoint(p))).ToArray();var tri=f.sharedMesh.triangles;
            for(int t=0;t<tri.Length;t+=3)
            {
                var a=v[tri[t]];var b=v[tri[t+1]];var c=v[tri[t+2]];float area=Cross2(b-a,c-a);if(Mathf.Abs(area)<.00001f)continue;
                int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x)))),maxX=Mathf.Min(JoinMaskSize-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
                int minY=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y)))),maxY=Mathf.Min(JoinMaskSize-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
                for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){var p=new Vector2(x,y);float u=Cross2(b-a,p-a)/area,w=Cross2(p-a,c-a)/area;if(u>=-.001f&&w>=-.001f&&u+w<=1.001f)mask[y*JoinMaskSize+x]=1;}
            }
        }
        static float[] FeatherMask(float[] input)
        {
            // Two separable box passes: about 35 cm of natural soil/grass transition.
            int n=JoinMaskSize;var a=input;var b=new float[a.Length];
            for(int pass=0;pass<2;pass++)
            {
                for(int y=0;y<n;y++)for(int x=0;x<n;x++){float s=0;for(int k=-2;k<=2;k++)s+=a[y*n+Mathf.Clamp(x+k,0,n-1)];b[y*n+x]=s/5;}
                for(int y=0;y<n;y++)for(int x=0;x<n;x++){float s=0;for(int k=-2;k<=2;k++)s+=b[Mathf.Clamp(y+k,0,n-1)*n+x];a[y*n+x]=s/5;}
            }
            return a;
        }
        static bool JoinedOverlay(MeshFilter f)=>f.name.StartsWith("Winding ")&&f.name!="Winding canal water"||f.name=="Uneven planted bund"||f.name=="Irregular rice parcel"||f.name=="Soft-edged homestead yard";
        [MenuItem("ShadowVale/Map 2/Join road and paddy banks")]
        public static void JoinMap02Soil()
        {
            var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath)throw new Exception("Open Map 2 before applying joined soil.");
            string texturePath=Root+"/Ricefield_Gravel_Albedo.png";AssetDatabase.ImportAsset(texturePath);var gravel=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(gravel==null)throw new Exception("Missing fine gravel texture.");
            var shader=Shader.Find("ShadowVale/Map 2 Joined Soil");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Joined soil shader unavailable or has compilation errors.");
            env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            if(!File.Exists(Reports+"/Map2_before_joined_soil.unity"))EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_joined_soil.unity",true);
            reliefHomes=env.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();
            reliefPlots=env.Find("Rice fields surrounding individual homes").Cast<Transform>().Where(t=>t.name.StartsWith("Paddy ")).ToArray();
            var ground=env.Find("Continuous ground with meandering canal");var gf=ground.GetComponent<MeshFilter>();var overlays=env.GetComponentsInChildren<MeshFilter>().Where(JoinedOverlay).ToArray();
            var roadMask=new float[JoinMaskSize*JoinMaskSize];var yardMask=new float[roadMask.Length];
            foreach(var f in overlays){if(f.name.StartsWith("Winding "))RasterSurface(f,roadMask);else if(f.name=="Soft-edged homestead yard")RasterSurface(f,yardMask);}
            FeatherMask(roadMask);FeatherMask(yardMask);var maskTex=new Texture2D(JoinMaskSize,JoinMaskSize,TextureFormat.RGB24,false,true);var pixels=new Color32[roadMask.Length];
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32((byte)(roadMask[i]*255),(byte)(yardMask[i]*255),0,255);
            maskTex.SetPixels32(pixels);maskTex.Apply();string maskPath=Root+"/Joined_Soil_Mask.png";File.WriteAllBytes(maskPath,maskTex.EncodeToPNG());Object.DestroyImmediate(maskTex);AssetDatabase.ImportAsset(maskPath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(maskPath);importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            var gi=(TextureImporter)AssetImporter.GetAtPath(texturePath);gi.wrapMode=TextureWrapMode.Repeat;gi.mipmapEnabled=true;gi.anisoLevel=8;gi.maxTextureSize=2048;gi.SaveAndReimport();
            // One physical surface owns roads, grassy bund crests and the depressed paddy floor.
            // Old ribbons only supply paint masks; none remain as raised render/collision layers.
            var mesh=Object.Instantiate(gf.sharedMesh);var vertices=mesh.vertices;var colors=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=ground.TransformPoint(vertices[i]);var q=Unwarp(p);float inset=FieldInset(q);float wet=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,2.9f,inset))*HomeMask(p);float noise=Mathf.PerlinNoise(p.x*.29f+300,p.z*.29f+300);
                var grassy=Color.Lerp(new Color(.18f,.29f,.065f),new Color(.34f,.41f,.135f),noise);colors[i]=Color.Lerp(grassy,new Color(.255f,.235f,.115f),wet);
            }
            mesh.colors=colors;mesh=SaveMesh(mesh,"Joined_Continuous_Soil");gf.sharedMesh=mesh;var gc=ground.GetComponent<MeshCollider>();gc.sharedMesh=null;gc.sharedMesh=mesh;
            string matPath=Root+"/Joined ricefield soil.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}material.shader=shader;material.SetTexture("_RoadTex",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.SetTexture("_SurfaceMask",AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));material.SetFloat("_GravelScale",.5f);EditorUtility.SetDirty(material);ground.GetComponent<MeshRenderer>().sharedMaterial=material;
            foreach(var f in overlays){f.GetComponent<MeshRenderer>().enabled=false;foreach(var c in f.GetComponents<Collider>())c.enabled=false;}
            Physics.SyncTransforms();int grounded=0;float maxGroundingError=0;
            foreach(var instance in env.GetComponentsInChildren<VillageRiceInstances>())
            {
                for(int i=0;i<instance.plants.Length;i++){var p=instance.plants[i];var w=instance.transform.TransformPoint(new Vector3(p.x,p.y,p.z));if(!gc.Raycast(new Ray(new Vector3(w.x,5,w.z),Vector3.down),out var hit,10))throw new Exception("Plant outside joined ground: "+w);w.y=hit.point.y+.008f;var local=instance.transform.InverseTransformPoint(w);instance.plants[i]=new Vector4(local.x,local.y,local.z,p.w);maxGroundingError=Mathf.Max(maxGroundingError,Mathf.Abs(w.y-hit.point.y-.008f));grounded++;}instance.Rebuild();EditorUtility.SetDirty(instance);
            }
            // Raycast both sides of every old ribbon edge against the actual shared collider.
            int seamSamples=0;float largestStep=0;
            foreach(var f in overlays.Where(f=>f.name.StartsWith("Winding ")||f.name=="Uneven planted bund"))
            {
                var v=f.sharedMesh.vertices;for(int i=0;i<v.Length;i+=Mathf.Max(1,v.Length/40)){var p=f.transform.TransformPoint(v[i]);if(!gc.Raycast(new Ray(new Vector3(p.x,5,p.z),Vector3.down),out var a,10))continue;foreach(var d in new[]{new Vector3(.025f,0,0),new Vector3(0,0,.025f)}){if(!gc.Raycast(new Ray(new Vector3(p.x+d.x,5,p.z+d.z),Vector3.down),out var b,10))continue;largestStep=Mathf.Max(largestStep,Mathf.Abs(a.point.y-b.point.y));seamSamples++;}}
            }
            foreach(var n in env.GetComponents<NavMeshSurface>()){n.RemoveData();Object.DestroyImmediate(n);}var nav=env.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.2f;nav.BuildNavMesh();var navPath=Root+"/Joined_Soil_NavMesh.asset";var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);if(previous==null)AssetDatabase.CreateAsset(nav.navMeshData,navPath);else{EditorUtility.CopySerialized(nav.navMeshData,previous);nav.navMeshData=previous;EditorUtility.SetDirty(previous);}
            var report=new List<string>{"One continuous render mesh and identical collision mesh; no overlapping road/bund/parcel layers.","Disabled floating overlays: "+overlays.Length,"Grounded grass and rice: "+grounded,"Plant grounding error: "+maxGroundingError,"Seam samples: "+seamSamples+"; maximum height change over 2.5 cm: "+largestStep+" m"};
            if(largestStep>.035f)report.Add("FAIL excessive local step at former ribbon edge");
            foreach(var target in new[]{Rural(new Vector3(32,1,-4)),Rural(new Vector3(64,1,-68)),Rural(new Vector3(-56,1,60))}.Concat(reliefHomes.Select(h=>h.position-h.forward*12)))
            {var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(Rural(new Vector3(-100,1,-4)),out var a,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;report.Add((ok?"PASS":"FAIL")+" route "+target);}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);scene=EditorSceneManager.OpenScene(ScenePath);var savedGround=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).First(f=>f.name=="Continuous ground with meandering canal");if(savedGround.sharedMesh!=savedGround.GetComponent<MeshCollider>().sharedMesh)throw new Exception("Saved collider mismatch");report.Add("PASS scene reload: shared render/collision surface");File.WriteAllLines(Reports+"/joined-soil-checks.txt",report);
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();Capture(camera,Rural(new Vector3(-96,6,-41)),Rural(new Vector3(-87,.7f,-34)),0,"map02-joined-junction");Capture(camera,Rural(new Vector3(77,3,-57)),Rural(new Vector3(79,1,-39)),0,"map02-joined-path");Capture(camera,new Vector3(-109,35,-67),new Vector3(-58,2,0),0,"map02-joined-village");EditorSceneManager.SaveScene(scene);
            if(report.Any(s=>s.StartsWith("FAIL")))throw new Exception("Joined soil validation failed; inspect report.");
        }
    }
}

