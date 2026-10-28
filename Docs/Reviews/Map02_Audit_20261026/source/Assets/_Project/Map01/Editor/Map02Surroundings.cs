using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    // Scenery outside the existing playable footprint. No colliders or navigation changes.
    public static class Map02Surroundings
    {
        const string ScenePath = "Assets/_Project/Scenes/Maps/Map 2.unity";
        const string Folder = "Assets/_Project/Art/Environment/Map02_Village/Boundary";
        const string RootName = "03 Surroundings • Map 2 landscape";
        const string Reports = "Tools/Map02Reports";
        static readonly Dictionary<(int,int), float> edge = new Dictionary<(int,int), float>();
        static Material palette;
        static Transform root;
        static System.Random random;
        static float Random(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
        static float Canal(float z) => -8 + 10 * Mathf.Sin((z + 12) * .027f);
        static float Outside(float x, float z) => Vector2.Distance(new Vector2(x,z), new Vector2(Mathf.Clamp(x,-110,110),Mathf.Clamp(z,-100,100)));

        [MenuItem("ShadowVale/Map 2/Build surrounding landscape and sky")]
        public static void Build()
        {
            random = new System.Random(200210);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty) throw new Exception("Save the current scene before building surroundings.");
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Reports);
            if (!File.Exists(Reports + "/Map2_before_surroundings.unity"))
                EditorSceneManager.SaveScene(scene, Reports + "/Map2_before_surroundings.unity", true);
            var env = scene.GetRootGameObjects().First(g => g.name.StartsWith("01 Environment"));
            var terrain = env.transform.Find("Continuous ground with meandering canal").GetComponent<MeshFilter>();
            edge.Clear();
            foreach (var p in terrain.sharedMesh.vertices)
                if (Mathf.Abs(Mathf.Abs(p.x)-110)<.001f || Mathf.Abs(Mathf.Abs(p.z)-100)<.001f)
                    edge[(Mathf.RoundToInt(p.x*2),Mathf.RoundToInt(p.z*2))] = p.y;
            if (edge.Count != 1680) throw new Exception("Expected complete half-metre map perimeter, got " + edge.Count);
            foreach (var old in scene.GetRootGameObjects().Where(g=>g.name==RootName)) Object.DestroyImmediate(old);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            root = new GameObject(RootName).transform;
            palette = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat"));
            palette.SetColor("_BaseColor", Color.white); palette = Save(palette,"Landscape palette.mat");
            var ground = new MeshData(); var water = new MeshData();
            var xs = Lines(-110,110); var zs = Lines(-100,100);
            var ids = new Dictionary<(int,int),int>();
            int Vertex(int x,int z)
            {
                if (ids.TryGetValue((x,z),out int id)) return id;
                float xx=xs[x],zz=zs[z],y=Height(xx,zz);
                id=ground.vertices.Count; ground.vertices.Add(new Vector3(xx,y,zz));
                ground.colors.Add(GroundColor(xx,zz,y)); ground.uv.Add(new Vector2(xx,zz)*.1f);
                ids[(x,z)]=id; return id;
            }
            for(int z=0;z<zs.Count-1;z++) for(int x=0;x<xs.Count-1;x++)
            {
                if(xs[x]>=-110 && xs[x+1]<=110 && zs[z]>=-100 && zs[z+1]<=100) continue;
                int a=Vertex(x,z),b=Vertex(x+1,z),c=Vertex(x,z+1),d=Vertex(x+1,z+1);
                ground.triangles.AddRange(new[]{a,c,b,b,c,d});
                if(Mathf.Min(ground.vertices[a].y,ground.vertices[b].y,ground.vertices[c].y,ground.vertices[d].y)<.4f)
                {
                    int n=water.vertices.Count;
                    foreach(int i in new[]{a,b,c,d}) { var p=ground.vertices[i];p.y=.045f;water.vertices.Add(p);water.colors.Add(Color.white);water.uv.Add(new Vector2(Mathf.Clamp((p.x-Canal(p.z))/(p.z < -180 ? 1200 : 17)+.5f,.1f,.9f),p.z)); }
                    water.triangles.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});
                }
            }
            Model("Continuous land • hills north and fields east west",Save(ground.Mesh(),"Surrounding terrain.asset"),palette);
            var waterMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map02_Village/PhysicalRiver.mat");
            var waterObject=Model("River extensions • north to Map 1 • broad southern river",Save(water.Mesh(),"Surrounding river.asset"),waterMaterial);
            var simulation=waterObject.AddComponent<RiverWater>();simulation.waterRenderer=waterObject.GetComponent<Renderer>();
            int trees=PlantTrees(); int crops=PlantFields();
            Marker("North river valley • visual connection toward Map 1",new Vector3(Canal(145),1,145));
            Marker("South canal mouth • opens into the broad river",new Vector3(Canal(-155),1,-155));
            var sky = new Material(Shader.Find("ShadowVale/Tropical Sky"));
            sky.SetColor("_Zenith",new Color(.18f,.43f,.70f));sky.SetColor("_Horizon",new Color(.70f,.81f,.83f));sky.SetFloat("_CloudCover",.55f);
            sky=Save(sky,"Map 2 cloud sky.mat");RenderSettings.skybox=sky;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=240;RenderSettings.fogEndDistance=1000;RenderSettings.fogColor=new Color(.67f,.77f,.78f);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.65f,.73f);RenderSettings.ambientEquatorColor=new Color(.38f,.43f,.31f);RenderSettings.ambientGroundColor=new Color(.19f,.23f,.14f);
            foreach(var camera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>())) {camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1800;}
            var report=new List<string>();float maxError=0;
            foreach(var pair in edge) maxError=Mathf.Max(maxError,Mathf.Abs(Height(pair.Key.Item1*.5f,pair.Key.Item2*.5f)-pair.Value));
            if(maxError>.0001f)throw new Exception("Terrain perimeter seam " + maxError);
            if(root.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Scenery must not alter navigation");
            if(Height(180,400)<35 || Height(200,0)>4 || Height(-200,0)>4 || Height(150,-260)>-.5f)throw new Exception("Landscape zoning check failed");
            if(ShaderUtil.ShaderHasError(sky.shader))throw new Exception("Sky shader failed");
            report.Add("PASS all 1680 perimeter vertices match existing Map 2 ground; max error " + maxError);
            report.Add("PASS north hills / east and west fields / south broad river");
            report.Add("PASS river valley to Map 1 at north; canal mouth at south; scenery only");
            report.Add("PASS no scenery colliders; existing playable environment and navigation retained");
            report.Add("Scenery trees: "+trees+"; distant rice clumps: "+crops);
            report.Add("Terrain vertices: "+ground.vertices.Count+"; water vertices: "+water.vertices.Count);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Capture("map02-boundary-overview",new Vector3(340,310,-440),new Vector3(0,0,45),false);
            Capture("map02-boundary-north",new Vector3(-60,22,-65),new Vector3(5,32,310),false);
            Capture("map02-boundary-south",new Vector3(55,28,20),new Vector3(-15,0,-275),false);
            File.WriteAllLines(Reports+"/surroundings-checks.txt",report);
        }

        public static void BuildAndExit(){try{Build();EditorApplication.Exit(0);}catch(Exception e){Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/surroundings-checks.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}}
        static void Marker(string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root,false);t.position=p;}
        static float Edge(float x,float z)
        {
            float xx=Mathf.Clamp(x,-110,110)*2,zz=Mathf.Clamp(z,-100,100)*2;
            int x0=Mathf.FloorToInt(xx),x1=Mathf.CeilToInt(xx),z0=Mathf.FloorToInt(zz),z1=Mathf.CeilToInt(zz);
            return Mathf.Lerp(Mathf.Lerp(edge[(x0,z0)],edge[(x1,z0)],xx-x0),Mathf.Lerp(edge[(x0,z1)],edge[(x1,z1)],xx-x0),zz-z0);
        }
        static float Height(float x,float z)
        {
            float d=Outside(x,z),e=Edge(x,z);
            if(d<.001f)return e;
            float undulation=(Mathf.PerlinNoise(x*.009f+17,z*.009f+71)-.5f)*1.2f;
            float h=1+undulation;
            if(z>100)
            {
                float rise=Smooth(110,440,z);
                float peaks=38+100*Mathf.PerlinNoise(x*.006f+15,z*.009f+51)+27*Mathf.Sin(x*.008f+z*.016f)*Mathf.Sin(x*.008f+z*.016f);
                h+=rise*peaks;
                float valley=Smooth(18,120,Mathf.Abs(x-Canal(z)));
                h=Mathf.Lerp(1+undulation,h,valley);
                h=Mathf.Lerp(-.8f,h,Smooth(5.25f,8.5f,Mathf.Abs(x-Canal(z))));
            }
            if(z < -100)
            {
                // A funnel widens from the existing canal into a river running east-west.
                float widening=Smooth(100,180,-z);
                float half=5.25f+620*widening*widening;
                float funnel=Smooth(half,half+5,Mathf.Abs(x-Canal(z)));
                h=Mathf.Lerp(-2.2f,h,funnel);
                float southBank=-380+12*Mathf.Sin(x*.011f)+8*Mathf.Sin(x*.024f);
                h=Mathf.Lerp(1+undulation,h,Smooth(southBank-7,southBank+8,z));
            }
            return Mathf.Lerp(e,h,Smooth(0,12,d));
        }
        static Color GroundColor(float x,float z,float y)
        {
            float grain=Mathf.PerlinNoise(x*.09f+10,z*.09f+20);
            if(y<.4f)return Color.Lerp(new Color(.22f,.26f,.13f),new Color(.35f,.34f,.20f),grain);
            if(z>135) return Color.Lerp(new Color(.14f,.25f,.12f),new Color(.30f,.37f,.20f),grain)*Mathf.Lerp(1,.8f,Smooth(30,150,y));
            var q=FieldCoordinates(x,z);int ix=Mathf.FloorToInt(q.x/30),iz=Mathf.FloorToInt(q.y/44);
            float bx=Mathf.Repeat(q.x,30),bz=Mathf.Repeat(q.y,44);
            if(Mathf.Min(bx,30-bx,bz,44-bz)<.65f)return new Color(.30f,.37f,.14f);
            var tone=((ix*17+iz*31)&7)<5?new Color(.61f,.58f,.17f):new Color(.30f,.47f,.10f);
            return tone*Mathf.Lerp(.82f,1.06f,grain);
        }
        static Vector2 FieldCoordinates(float x,float z)=>new Vector2(x+5*Mathf.Sin(z*.014f),z+8*Mathf.Sin(x*.012f));
        static List<float> Lines(float min,float max)
        {
            var outer=new List<float>();float d=0;
            while(d<700){d+=d<4?.5f:d<16?1:d<55?3:d<140?6:d<300?12:20;outer.Add(d);}
            var result=outer.Select(v=>min-v).Reverse().ToList();
            for(float x=min;x<=max+.001f;x+=.5f)result.Add(x);
            result.AddRange(outer.Select(v=>max+v));return result;
        }
        static T Save<T>(T value,string name) where T:Object
        {
            string path=Folder+"/"+name;var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
        }
        static GameObject Model(string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return go;
        }
        static int PlantTrees()
        {
            int count=0;var chunks=new Dictionary<(int,int),MeshData>();
            for(int i=0;i<2200;i++)
            {
                float x=Random(-560,560),z=Random(-460,650),d=Outside(x,z);
                if(d<9||d>570)continue;float h=Height(x,z);if(h<.65f)continue;
                var q=FieldCoordinates(x,z);float bund=Mathf.Min(Mathf.Repeat(q.x,30),30-Mathf.Repeat(q.x,30),Mathf.Repeat(q.y,44),44-Mathf.Repeat(q.y,44));
                if(z<130 && bund>3.5f)continue;
                var key=(Mathf.FloorToInt(x/100),Mathf.FloorToInt(z/100));
                if(!chunks.TryGetValue(key,out var mesh))chunks[key]=mesh=new MeshData();
                float size=Random(5,10),c=Random(.8f,1.15f);var p=new Vector3(x,h,z);
                mesh.Ellipsoid(p+Vector3.up*size*.32f,new Vector3(.25f,size*.38f,.25f),new Color(.27f,.23f,.13f),5,3);
                mesh.Ellipsoid(p+Vector3.up*size*.8f,new Vector3(size*.57f,size*.40f,size*.48f),new Color(.17f,.31f,.095f)*c,8,4);
                mesh.Ellipsoid(p+new Vector3(-size*.26f,size*.72f,size*.1f),new Vector3(size*.39f,size*.30f,size*.39f),new Color(.23f,.36f,.12f)*c,7,3);count++;
            }
            foreach(var c in chunks)Model("Distant tree belt "+c.Key,Save(c.Value.Mesh(),"Trees_"+c.Key.Item1+"_"+c.Key.Item2+".asset"),palette);
            return count;
        }
        static int PlantFields()
        {
            int count=0;var chunks=new Dictionary<(int,int),MeshData>();
            for(float z=-125;z<125;z+=2.2f)for(float x=-345;x<345;x+=2.2f)
            {
                if(Outside(x,z)<3||Height(x,z)<.6f)continue;var q=FieldCoordinates(x,z);
                float bund=Mathf.Min(Mathf.Repeat(q.x,30),30-Mathf.Repeat(q.x,30),Mathf.Repeat(q.y,44),44-Mathf.Repeat(q.y,44));if(bund<1.3f)continue;
                var key=(Mathf.FloorToInt(x/80),Mathf.FloorToInt(z/80));if(!chunks.TryGetValue(key,out var mesh))chunks[key]=mesh=new MeshData();
                var p=new Vector3(x+Random(-.25f,.25f),Height(x,z),z+Random(-.25f,.25f));var color=GroundColor(x,z,p.y)*1.15f;
                for(int k=0;k<3;k++){float a=k*2.094f;var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.35f;int n=mesh.vertices.Count;mesh.vertices.AddRange(new[]{p-side,p+Vector3.up*Random(.55f,.95f)+side*.4f,p+side});mesh.colors.AddRange(new[]{color*.65f,color,color});mesh.uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.right});mesh.triangles.AddRange(new[]{n,n+1,n+2});}count++;
            }
            foreach(var c in chunks)Model("Outer rice parcel rows "+c.Key,Save(c.Value.Mesh(),"Rice_"+c.Key.Item1+"_"+c.Key.Item2+".asset"),palette);
            return count;
        }
        static void Capture(string name,Vector3 position,Vector3 target,bool orthographic)
        {
            var go=new GameObject("Temporary surroundings camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=position;camera.transform.LookAt(target);camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1900;camera.nearClipPlane=.3f;camera.fieldOfView=62;camera.orthographic=orthographic;camera.orthographicSize=400;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
            var rt=new RenderTexture(1600,1000,24);var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(Reports+"/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(go);Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
        sealed class MeshData
        {
            public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<Color> colors=new List<Color>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> triangles=new List<int>();
            public Mesh Mesh(){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(vertices);m.SetColors(colors);m.SetUVs(0,uv);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(.3f);m.bounds=b;return m;}
            public void Ellipsoid(Vector3 center,Vector3 size,Color color,int sides,int rings)
            {
                int n=vertices.Count;for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides,b=j*Mathf.PI/rings;vertices.Add(center+Vector3.Scale(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a)),size));colors.Add(color*Mathf.Lerp(.75f,1.08f,(Mathf.Cos(b)+1)*.5f));uv.Add(Vector2.zero);if(i>0&&j>0){int k=n+j*(sides+1)+i;triangles.AddRange(new[]{k-sides-2,k-1,k,k-sides-2,k,k-sides-1});}}
            }
        }
    }
}
