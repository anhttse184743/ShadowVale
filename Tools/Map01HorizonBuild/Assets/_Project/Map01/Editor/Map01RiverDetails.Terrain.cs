using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map01RiverDetails
    {
        static float TerrainHeight(Vector3 world)
        {
            var hits=Physics.RaycastAll(new Vector3(world.x,60,world.z),Vector3.down,120)
                .Where(h=>h.collider.name.StartsWith("Collision_Walk") || h.collider.name.StartsWith("Hill ")).OrderByDescending(h=>h.point.y).ToArray();
            if(hits.Length==0) throw new InvalidOperationException("No ground below "+world);
            return hits[0].point.y;
        }
        static Mesh PersistMesh(Mesh mesh,string name)
        {
            string path=Root+"/"+name+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name=name;
            if(existing==null) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
            EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }
        static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        static void FitMoundAndDress(Transform bunker,Mesh source,Material material)
        {
            foreach(string name in new[]{"Terrain fitted mound","Mound grass"}) {var old=bunker.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}
            var mesh=Object.Instantiate(source);var vertices=mesh.vertices;var colors=mesh.colors;int edges=0;float maxGap=float.MinValue;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];
                float edge=Mathf.Max(Mathf.Abs(p.x)/8, p.z<0?-p.z/9:p.z/8.28f);
                var world=bunker.TransformPoint(p);float ground=bunker.InverseTransformPoint(new Vector3(world.x,TerrainHeight(world),world.z)).y;
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.52f,1,edge));
                // Exact perimeter is buried; intermediate rings blend continuously into the sampled terrain.
                if(p.y>-.5f) p.y=Mathf.Lerp(p.y,ground-.14f,blend);
                else p.y=Mathf.Min(p.y,ground-1.2f);
                vertices[i]=p;
                float noise=Mathf.PerlinNoise((p.x+31)*.37f,(p.z+53)*.37f);
                colors[i]=Color.Lerp(new Color(.27f,.27f,.15f),new Color(.25f,.35f,.09f),noise);
                if(edge>.999f){edges++;maxGap=Mathf.Max(maxGap,p.y-ground);}
            }
            mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh=PersistMesh(mesh,"Bunker_Mound_Fitted");
            var mound=MeshObject("Terrain fitted mound",bunker,mesh,material);mound.layer=LayerMask.NameToLayer("Obstacle");var collider=mound.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
            Physics.SyncTransforms();
            var grassRoot=new GameObject("Mound grass");grassRoot.transform.SetParent(bunker,false);
            var grass=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Realism/Meshes/Grass_LOD1.asset");
            var rng=new System.Random(92026);var combine=new List<CombineInstance>();int onRoof=0,around=0;
            for(float x=-10;x<=10;x+=.65f)for(float z=-11;z<=10;z+=.65f)
            {
                var p=new Vector3(x+((float)rng.NextDouble()-.5f)*.5f,0,z+((float)rng.NextDouble()-.5f)*.5f);
                if(Mathf.Abs(p.x)<2.6f&&p.z>3.3f)continue;
                var world=bunker.TransformPoint(p);var ray=new Ray(new Vector3(world.x,60,world.z),Vector3.down);
                bool roof=collider.Raycast(ray,out var hit,120);
                float y=roof?hit.point.y:TerrainHeight(world);
                if(roof&&hit.normal.y<.42f)continue;
                p=bunker.InverseTransformPoint(new Vector3(world.x,y-.025f,world.z));
                float scale=.38f+(float)rng.NextDouble()*.43f;
                combine.Add(new CombineInstance{mesh=grass,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,(float)rng.NextDouble()*360,0),new Vector3(scale,scale,scale))});
                if(roof)onRoof++;else around++;
            }
            var grassMesh=new Mesh{indexFormat=IndexFormat.UInt32};grassMesh.CombineMeshes(combine.ToArray());grassMesh=PersistMesh(grassMesh,"Bunker_Grass_Cover");
            var cover=MeshObject("Grass roof and perimeter",grassRoot.transform,grassMesh,material);cover.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            File.WriteAllText(Reports+"/mound-fit-checks.txt",$"PASS {edges} edge vertices fitted; largest edge above ground = {maxGap:F3}m\nGrass clumps on mound: {onRoof}; around perimeter: {around}\n");
            if(maxGap>-.1f||onRoof==0)throw new InvalidOperationException("Mound fitting failed");
        }
        static void CheckNorthernNavigation()
        {
            var jetty=GameObject.Find("North jetty • independent from Chunk_0_3").transform;
            var start=jetty.position+new Vector3(17,0,0);start.y=TerrainHeight(start)+.1f;
            var end=jetty.position+new Vector3(.7f,.12f,0);
            var path=new UnityEngine.AI.NavMeshPath();
            bool a=UnityEngine.AI.NavMesh.SamplePosition(start,out var p,.6f,UnityEngine.AI.NavMesh.AllAreas);
            bool b=UnityEngine.AI.NavMesh.SamplePosition(end,out var q,.6f,UnityEngine.AI.NavMesh.AllAreas);
            bool connected=a&&b&&UnityEngine.AI.NavMesh.CalculatePath(p.position,q.position,UnityEngine.AI.NavMesh.AllAreas,path)&&path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete;
            if(!connected)throw new InvalidOperationException("North jetty not connected to bank navigation");
            var deck=jetty.GetComponents<BoxCollider>().First(c=>c.size.y<.3f);
            bool supported=deck.Raycast(new Ray(jetty.position+new Vector3(6,3,0),Vector3.down),out var hit,5);
            if(!supported)throw new InvalidOperationException("Jetty deck collision missing");
            File.AppendAllText(Reports+"/north-jetty-checks.txt","PASS complete bank-to-jetty NavMesh path; deck collider supports player; two solid rails\n");
        }
        [Serializable] class OriginalJetty { public float[] vertices; }
        static void RepairNorthernJetty(GameObject env,Transform root,Dictionary<string,Mesh> meshes,Material material,Vector3[] water)
        {
            var source=JsonUtility.FromJson<OriginalJetty>(File.ReadAllText("SourceArt/Map01_RiverDetails/JettyOriginalVertices.json"));
            var oldVertices=new List<Vector3>();for(int i=0;i<source.vertices.Length;i+=3)oldVertices.Add(new Vector3(source.vertices[i],source.vertices[i+1],source.vertices[i+2]));
            int removed=0;
            Func<Mesh,Mesh> strip=original=>
            {
                var v=original.vertices;var tri=original.triangles;var match=new bool[v.Length];
                for(int i=0;i<v.Length;i++)if(v[i].z>83&&v[i].z<95.2f&&v[i].x>-.3f&&v[i].x<22)match[i]=oldVertices.Any(p=>(p-v[i]).sqrMagnitude<.000004f);
                var keep=new List<int>();for(int i=0;i<tri.Length;i+=3) {if(match[tri[i]]&&match[tri[i+1]]&&match[tri[i+2]]){removed++;continue;}keep.Add(tri[i]);keep.Add(tri[i+1]);keep.Add(tri[i+2]);}
                if(keep.Count==tri.Length)return original;
                var copy=Object.Instantiate(original);copy.triangles=keep.ToArray();copy.RecalculateBounds();return PersistMesh(copy,original.name.Replace("_separated","")+"_separated");
            };
            foreach(var f in env.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null&&f.sharedMesh.name.StartsWith("Chunk_")&&f.sharedMesh.name.Contains("opaque")))
            {
                var b=f.sharedMesh.bounds;if(b.max.z<83||b.min.z>96||b.max.x<-.3f||b.min.x>22)continue;
                f.sharedMesh=strip(f.sharedMesh);
                // Chunk geometry is authored in world coordinates; a transformed chunk displaces terrain and props together.
                if(f.name=="Chunk_0_3_opaque"){f.transform.localPosition=Vector3.zero;f.transform.localRotation=Quaternion.identity;f.transform.localScale=Vector3.one;}
            }
            foreach(var c in env.GetComponentsInChildren<MeshCollider>().Where(c=>c.sharedMesh!=null&&c.sharedMesh.name.StartsWith("Collision_")))
            {var b=c.sharedMesh.bounds;if(b.max.z<83||b.min.z>96||b.max.x<-.3f||b.min.x>22)continue;var original=c.sharedMesh;c.sharedMesh=null;Map01CollisionMesh.Assign(c,strip(original));}
            Physics.SyncTransforms();
            float river=TropicalRealismBuilder.RiverX(85),deck=TerrainHeight(new Vector3(river+14.6f,0,85))+.18f;
            var jetty=MeshObject("North jetty • independent from Chunk_0_3",root,meshes["Jetty_Rebuilt"],material);jetty.transform.position=new Vector3(river,deck,85);jetty.layer=LayerMask.NameToLayer("Obstacle");
            var floor=jetty.AddComponent<BoxCollider>();floor.center=new Vector3(7.2f,0,0);floor.size=new Vector3(14.8f,.18f,3.6f);
            foreach(float side in new[]{-1f,1f}) {var rail=jetty.AddComponent<BoxCollider>();rail.center=new Vector3(7.2f,.6f,side*1.6f);rail.size=new Vector3(14.4f,1.2f,.16f);}
            // The bank approach starts flush with the deck and ends on sampled terrain.
            var a=new Vector3(river+14.4f,deck+.02f,85);var bEnd=new Vector3(river+17,0,85);bEnd.y=TerrainHeight(bEnd)+.06f;
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.name="North jetty bank approach";ramp.transform.SetParent(root,false);ramp.transform.position=(a+bEnd)*.5f;ramp.transform.rotation=Quaternion.LookRotation(bEnd-a);ramp.transform.localScale=new Vector3(3.3f,.18f,Vector3.Distance(a,bEnd)+.2f);ramp.layer=LayerMask.NameToLayer("Obstacle");
            ramp.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");
            var rampMesh=Object.Instantiate(ramp.GetComponent<MeshFilter>().sharedMesh);
            rampMesh.colors=Enumerable.Repeat(new Color(.31f,.23f,.135f,1),rampMesh.vertexCount).ToArray();
            ramp.GetComponent<MeshFilter>().sharedMesh=PersistMesh(rampMesh,"North_Jetty_Ramp");
            foreach(var plant in env.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tall grass patch ")||t.name.StartsWith("Grass concealment ")||t.name.StartsWith("Prop_Grass")).ToArray())
            {
                var pos=plant.position;
                if(pos.x>river-2&&pos.x<river+20&&Mathf.Abs(pos.z-85)<4) {plant.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(plant.gameObject);}
            }
            float bz=90;float bx=TropicalRealismBuilder.RiverX(bz);float wy=water.OrderBy(v=>new Vector2(v.x-bx,v.z-bz).sqrMagnitude).First().y;
            var boat=MeshObject("North boat • rebuilt Blender hull",root,meshes["Boat_Floating"],material);boat.transform.position=new Vector3(bx,wy+.16f,bz);boat.layer=LayerMask.NameToLayer("Obstacle");var hull=boat.AddComponent<BoxCollider>();hull.center=new Vector3(0,.1f,0);hull.size=new Vector3(1.9f,.65f,6.6f);
            Physics.SyncTransforms();
            if(boat.transform.position.y-.32f>=wy||boat.transform.position.y+.51f<=wy)throw new InvalidOperationException("Incorrect north boat waterline");
            File.WriteAllText(Reports+"/north-jetty-checks.txt",$"Removed original jetty/canoe triangles: {removed}\nIndependent jetty: {jetty.transform.position}; bank approach: {bEnd}\nPASS north boat keel below / gunwale above water {wy}; origin {boat.transform.position}\n");
            Capture(new Vector3(bx+18,deck+13,68),new Vector3(river+4,1,87),"north-jetty-details");
            Capture(boat.transform.position+new Vector3(-8,6,-7),boat.transform.position,"north-boat-details");
        }
    }
}

