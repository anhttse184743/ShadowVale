using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Bakes the map-one shelter against the surrounding ground without runtime meshes.</summary>
    public static class ShelterSurfaceSetup
    {
        private const string Folder = "Assets/_Project/Art/Environment/LayoutTest/";
        private const string PrefabPath = Folder + "Prefabs/ShelterShell.prefab";

        [MenuItem("ShadowVale/Environment/Seal shelter and match ground")]
        public static void Bake()
        {
            var instance = GameObject.Find("ShelterShell");
            var ground = GameObject.Find("Continuous hills — 220 x 200");
            if (instance == null || ground == null || Application.isPlaying)
                throw new System.InvalidOperationException("Open Map 1 outside Play mode before baking its shelter.");
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // The original earth batch contains five retaining-wall boxes, then
                // the old roof box and sloping banks. Keep only the wall faces.
                var original = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "Meshes/ShelterShell_1.asset");
                var walls = Object.Instantiate(original); walls.name = "Shelter retaining walls";
                var triangles = new List<int>();
                var oldTriangles = original.triangles;
                for (int i = 0; i < oldTriangles.Length; i += 3)
                    if (oldTriangles[i] < 120 && oldTriangles[i + 1] < 120 && oldTriangles[i + 2] < 120)
                        triangles.AddRange(new[] {oldTriangles[i],oldTriangles[i+1],oldTriangles[i+2]});
                walls.triangles = triangles.ToArray();
                prefab.transform.Find("Geometry 1").GetComponent<MeshFilter>().sharedMesh = Store(walls,"ShelterRetainingWalls");
                prefab.transform.Find("Sloping earth banks").GetComponent<Collider>().enabled = false;

                var source = ground.GetComponent<MeshFilter>().sharedMesh;
                var sourceVertices = source.vertices; var sourceUv = source.uv; var sourceColors = source.colors;
                var lookup = new Dictionary<Vector2Int,int>();
                for(int i=0;i<sourceVertices.Length;i++) {
                    var world=ground.transform.TransformPoint(sourceVertices[i]);
                    lookup[new Vector2Int(Mathf.RoundToInt(world.x),Mathf.RoundToInt(world.z))]=i;
                }
                // Match the existing 1 m ground grid exactly at the hole boundary.
                // UVs/colors come from that same grid, avoiding a rectangular material patch.
                const int width=17,depth=15;
                var vertices=new Vector3[width*depth];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];
                triangles.Clear();
                for(int z=0;z<depth;z++)for(int x=0;x<width;x++) {
                    int i=z*width+x;var local=new Vector3(x-8,4.2f-instance.transform.position.y,z-7);
                    var world=instance.transform.TransformPoint(local);
                    int sourceIndex=lookup[new Vector2Int(Mathf.RoundToInt(world.x),Mathf.RoundToInt(world.z))];
                    vertices[i]=local;uv[i]=sourceUv[sourceIndex];colors[i]=sourceColors.Length==sourceVertices.Length?sourceColors[sourceIndex]:Color.white;
                    if(x==width-1||z==depth-1)continue;
                    triangles.AddRange(new[]{i,i+width,i+1,i+1,i+width,i+width+1});
                }
                var roof=new Mesh {name="Shelter seamless ground roof",vertices=vertices,uv=uv,colors=colors,triangles=triangles.ToArray()};
                roof.RecalculateNormals();roof.RecalculateBounds();roof.RecalculateTangents();
                var cap=Child(prefab.transform,"Ground roof surface");
                cap.AddComponent<MeshFilter>().sharedMesh=Store(roof,"ShelterGroundRoof");
                cap.AddComponent<MeshRenderer>().sharedMaterial=ground.GetComponent<Renderer>().sharedMaterial;

                var earth=prefab.transform.Find("Geometry 1").GetComponent<Renderer>().sharedMaterial;
                // Overlap the wall tops by 5 cm; the cap itself supplies the visible top.
                float top=4.2f-instance.transform.position.y;
                var slab=Child(prefab.transform,"Sealed roof slab");
                Box(slab.transform,new Vector3(0,(3.35f+top-.02f)*.5f,0),new Vector3(16,top-.02f-3.35f,14),earth);
                var roofCollider=prefab.transform.Find("Earth covered roof");
                roofCollider.localPosition=new Vector3(0,(3.35f+top)*.5f,0);
                roofCollider.localScale=new Vector3(16,top-3.35f,14);
                // Bridge the 40 cm separation from the front wall to the door frame,
                // leaving the existing 2.92 m passage and 2.7 m clear doorway height.
                var timber=prefab.transform.Find("Geometry 2").GetComponent<Renderer>().sharedMaterial;
                var portal=Child(prefab.transform,"Sealed entrance returns");
                Box(portal.transform,new Vector3(-4.73f,1.7f,-6.45f),new Vector3(6.54f,3.4f,.9f),timber,true);
                Box(portal.transform,new Vector3(4.73f,1.7f,-6.45f),new Vector3(6.54f,3.4f,.9f),timber,true);
                Box(portal.transform,new Vector3(0,(2.98f+top-.02f)*.5f,-6.45f),new Vector3(16,top-.02f-2.98f,.9f),timber,true);
                PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            Physics.SyncTransforms();
        }

        private static Mesh Store(Mesh mesh,string name)
        {
            string path=Folder+"Meshes/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;
        }
        private static GameObject Child(Transform parent,string name)
        {
            var previous=parent.Find(name);if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var child=new GameObject(name);child.transform.SetParent(parent,false);return child;
        }
        private static void Box(Transform parent,Vector3 position,Vector3 size,Material material,bool physical=false)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name="Timber closure";
            box.transform.SetParent(parent,false);box.transform.localPosition=position;box.transform.localScale=size;
            box.GetComponent<Renderer>().sharedMaterial=material;
            if(!physical)Object.DestroyImmediate(box.GetComponent<Collider>());
        }
    }
}
