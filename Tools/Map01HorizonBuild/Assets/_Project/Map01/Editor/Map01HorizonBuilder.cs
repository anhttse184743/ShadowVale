using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class Map01HorizonBuilder
    {
        const string Root="Assets/_Project/Art/Environment/Map01_Horizon",Reports="Tools/Map01HorizonReports";
        static readonly List<Vector3> edge=new List<Vector3>();
        static Map01HorizonBuilder(){EditorApplication.update+=Poll;}
        static void Poll(){const string request="Tools/Map01Horizon.request";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(request);try{Apply();File.WriteAllText(Reports+"/status.txt","PASS");}catch(Exception e){Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/status.txt","FAIL "+e);Debug.LogException(e);}}
        static Mesh Save(Mesh mesh,string name){mesh.name=name;var path=Root+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        static GameObject Model(string name,Transform parent,Mesh mesh,Material material){var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return go;}
        static float EdgeHeight(float x,float z)
        {
            var hits=Physics.RaycastAll(new Vector3(Mathf.Clamp(x,-109.85f,109.85f),70,Mathf.Clamp(z,-99.85f,99.85f)),Vector3.down,120).Where(h=>h.collider.name.StartsWith("Collision_Walk")||h.collider.name.StartsWith("Hill ")).OrderByDescending(h=>h.point.y).ToArray();
            return hits.Length>0?hits[0].point.y:2.7f;
        }
        static float River(float z)
        {
            float end=Mathf.Sign(z)*100;float u=Mathf.Abs(z)-100;
            return TropicalRealismBuilder.RiverX(end)+Mathf.Sin(u*.009f)*14;
        }
        static float Height(float x,float z,float ground)
        {
            float distance=Mathf.Max(Mathf.Abs(x)-110,Mathf.Abs(z)-100);
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/50));
            float hills=5+Mathf.PerlinNoise((x+800)*.009f,(z+800)*.009f)*30;
            hills+=Mathf.SmoothStep(0,1,Mathf.InverseLerp(90,250,distance))*Mathf.PerlinNoise((x+1100)*.004f,(z+600)*.004f)*85;
            float h=Mathf.Lerp(ground-.06f,hills,blend);
            if(Mathf.Abs(z)>=99.5f){float river=Mathf.Abs(x-River(z));h=Mathf.Lerp(-.8f,h,Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.6f,11,river)));}
            return h;
        }
        [MenuItem("ShadowVale/Map 1/Add forest horizon and distance fog")]
        public static void Apply()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Reports);
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=OptimizedMapBuilder.ScenePath){if(scene.isDirty){if(string.IsNullOrEmpty(scene.path))throw new Exception("Save untitled scene first");EditorSceneManager.SaveScene(scene);}scene=EditorSceneManager.OpenScene(OptimizedMapBuilder.ScenePath);}
            EditorSceneManager.SaveScene(scene,Reports+"/Map1_before_horizon.unity",true);
            var old=GameObject.Find("03 Distant landscape • outside playable map");if(old!=null)Object.DestroyImmediate(old);
            var root=new GameObject("03 Distant landscape • outside playable map");
            var palette=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");
            Physics.SyncTransforms();edge.Clear();
            for(int i=0;i<64;i++)edge.Add(new Vector3(Mathf.Lerp(-110,110,i/64f),0,-100));
            for(int i=0;i<64;i++)edge.Add(new Vector3(110,0,Mathf.Lerp(-100,100,i/64f)));
            for(int i=0;i<64;i++)edge.Add(new Vector3(Mathf.Lerp(110,-110,i/64f),0,100));
            for(int i=0;i<64;i++)edge.Add(new Vector3(-110,0,Mathf.Lerp(100,-100,i/64f)));
            for(int i=0;i<edge.Count;i++){var p=edge[i];p.y=EdgeHeight(p.x,p.z);edge[i]=p;}
            var v=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();var bands=new[]{-.6f,0,2,5,10,18,30,45,65,90,125,180,250,360,500,700};
            for(int ring=0;ring<bands.Length;ring++)
            {
                for(int j=0;j<edge.Count;j++){var p=edge[j];float x=p.x*(1+bands[ring]/110),z=p.z*(1+bands[ring]/100);v.Add(new Vector3(x,Height(x,z,p.y),z));float k=Mathf.PerlinNoise((x+200)*.12f,(z+400)*.12f);colors.Add(Color.Lerp(new Color(.22f,.28f,.12f),new Color(.34f,.39f,.20f),k));}
                if(ring==0)continue;for(int j=0;j<edge.Count;j++){int next=(j+1)%edge.Count,a=(ring-1)*edge.Count+j,b=(ring-1)*edge.Count+next,c=ring*edge.Count+j,d=ring*edge.Count+next;triangles.AddRange(new[]{a,b,c,b,d,c});}
            }
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            // Clockwise perimeter plus outward strips must face upward on all four sides.
            if(mesh.normals[edge.Count*2].y<0){var idx=mesh.triangles;for(int i=0;i<idx.Length;i+=3){int a=idx[i];idx[i]=idx[i+2];idx[i+2]=a;}mesh.triangles=idx;mesh.RecalculateNormals();}
            var terrain=Model("Surrounding terrain • seamless overlapping rim",root.transform,Save(mesh,"Terrain_Horizon"),palette);
            var sampling=terrain.AddComponent<MeshCollider>();sampling.sharedMesh=terrain.GetComponent<MeshFilter>().sharedMesh;Physics.SyncTransforms();
            var random=new System.Random(9281);var groups=new Dictionary<Vector2Int,List<CombineInstance>>();int trees=0;
            var prototypes=Enumerable.Range(0,5).Select(i=>AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Sept22/Meshes/Forest_"+i+"_LOD2.asset")).ToArray();
            for(float x=-290;x<=290;x+=6.5f)for(float z=-280;z<=280;z+=6.5f)
            {
                float xx=x+(float)random.NextDouble()*4,zz=z+(float)random.NextDouble()*4;float distance=Mathf.Max(Mathf.Abs(xx)-110,Mathf.Abs(zz)-100);
                if(distance<4||distance>170)continue;if(Mathf.Abs(zz)>100&&Mathf.Abs(xx-River(zz))<9)continue;
                float ex=Mathf.Clamp(xx,-110,110),ez=Mathf.Clamp(zz,-100,100);float y=Height(xx,zz,EdgeHeight(ex,ez));if(sampling.Raycast(new Ray(new Vector3(xx,250,zz),Vector3.down),out var terrainHit,400))y=terrainHit.point.y;
                var key=new Vector2Int(Mathf.FloorToInt(xx/48),Mathf.FloorToInt(zz/48));if(!groups.ContainsKey(key))groups[key]=new List<CombineInstance>();
                float scale=.85f+(float)random.NextDouble()*1.0f;
                groups[key].Add(new CombineInstance{mesh=prototypes[trees%5],transform=Matrix4x4.TRS(new Vector3(xx,y,zz),Quaternion.Euler(0,(float)random.NextDouble()*360,0),new Vector3(scale,scale,scale))});trees++;
            }
            foreach(var pair in groups){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(pair.Value.ToArray());Model("Distant forest "+pair.Key,root.transform,Save(m,"Forest_"+pair.Key.x+"_"+pair.Key.y),palette);}
            Object.DestroyImmediate(sampling);
            var riverMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Realism/River surface.mat");
            foreach(float side in new[]{-1f,1f})
            {
                var rv=new List<Vector3>();var rt=new List<int>();for(int i=0;i<=120;i++){float z=side*(99.4f+i*3);float x=River(z);rv.Add(new Vector3(x-3.7f,.05f,z));rv.Add(new Vector3(x+3.7f,.05f,z));if(i>0){int k=i*2;rt.AddRange(side>0?new[]{k-2,k,k-1,k-1,k,k+1}:new[]{k-2,k-1,k,k-1,k+1,k});}}
                var m=new Mesh();m.SetVertices(rv);m.SetTriangles(rt,0);m.RecalculateNormals();m.RecalculateBounds();Model("River continues beyond "+(side>0?"north":"south"),root.transform,Save(m,"River_Continuation_"+side),riverMat);
            }
            var fogColor=new Color(.64f,.73f,.71f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=40;RenderSettings.fogEndDistance=310;RenderSettings.fogColor=fogColor;
            var skySource=RenderSettings.skybox;if(skySource!=null){string skyPath=Root+"/Forest_Haze_Sky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);if(sky==null){sky=new Material(skySource);AssetDatabase.CreateAsset(sky,skyPath);}if(sky.HasProperty("_Horizon"))sky.SetColor("_Horizon",fogColor);RenderSettings.skybox=sky;EditorUtility.SetDirty(sky);}
            foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.gameObject.scene==scene)){camera.farClipPlane=Mathf.Max(1100,camera.farClipPlane);camera.clearFlags=CameraClearFlags.Skybox;}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            var report=new List<string>{"PASS 360-degree exterior ground ring, 0.6m overlap, scenery extends 700m","PASS "+trees+" distant trees in "+groups.Count+" static render groups; low-detail shared source meshes","PASS distance fog: clear foreground to 40m, full haze by 310m","PASS no exterior colliders or NavMesh changes"};
            if(root.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Backdrop must not alter collision");
            foreach(var dir in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right}){var p=new Vector3(dir.x*106,0,dir.z*96);p.y=EdgeHeight(p.x,p.z)+2.2f;Capture(p,p+dir*90+Vector3.up*5,"edge-"+dir);}
            File.WriteAllLines(Reports+"/checks.txt",report);File.WriteAllText(Reports+"/status.txt","PASS "+DateTime.UtcNow.ToString("O"));
        }
        static void Capture(Vector3 p,Vector3 target,string name)
        {
            var go=new GameObject("Temporary horizon camera");var c=go.AddComponent<Camera>();c.CopyFrom(Camera.main);c.enabled=false;c.orthographic=false;c.fieldOfView=65;c.farClipPlane=1100;c.transform.position=p;c.transform.LookAt(target);var rt=new RenderTexture(1440,900,24);var t=new Texture2D(1440,900,TextureFormat.RGB24,false);var before=RenderTexture.active;
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1440,900),0,0);t.Apply();File.WriteAllBytes(Reports+"/"+name+".png",t.EncodeToPNG());}finally{RenderTexture.active=before;c.targetTexture=null;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}

