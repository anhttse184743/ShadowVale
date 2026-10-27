using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Visual tree geometry supplements small/missing trunk colliders for flashlight sight only.</summary>
    public sealed class Map02SightCover:MonoBehaviour
    {
        sealed class Geometry {public Vector3[] vertices;public int[] triangles;}
        sealed class Tree {public MeshFilter filter;public Renderer renderer;public Geometry geometry;}
        readonly List<Tree> trees=new List<Tree>();
        readonly Dictionary<Mesh,Geometry> meshes=new Dictionary<Mesh,Geometry>();
        void Awake()=>Build();
        static bool TreeName(string name)
        {
            name=name.ToLowerInvariant();
            return name.Contains("tree")||name.Contains("banyan")||name.Contains("flamboyant")||
                name.Contains("poinciana")||name.Contains("coconut")||name.Contains("palm")||name.Contains("bamboo");
        }
        static bool IsTree(MeshFilter filter)
        {
            if(filter.sharedMesh!=null&&TreeName(filter.sharedMesh.name))return true;
            var t=filter.transform;
            for(int i=0;i<4&&t!=null;i++,t=t.parent)if(TreeName(t.name))return true;
            return false;
        }
        public void Build()
        {
            trees.Clear();meshes.Clear();var grouped=new HashSet<Renderer>();
            foreach(var group in FindObjectsByType<LODGroup>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var lods=group.GetLODs();if(lods.Length==0)continue;
                var filters=lods[0].renderers.Where(r=>r!=null).Select(r=>r.GetComponent<MeshFilter>()).Where(f=>f!=null&&IsTree(f)).ToArray();
                if(filters.Length==0)continue;
                foreach(var lod in lods)foreach(var r in lod.renderers)if(r!=null)grouped.Add(r);
                foreach(var filter in filters)Add(filter);
            }
            foreach(var filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var renderer=filter.GetComponent<Renderer>();
                if(renderer!=null&&!grouped.Contains(renderer)&&IsTree(filter))Add(filter);
            }
        }
        void Add(MeshFilter filter)
        {
            var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();if(mesh==null||renderer==null)return;
            if(!meshes.TryGetValue(mesh,out var geometry))
            {
                geometry=new Geometry();
                if(mesh.isReadable){geometry.vertices=mesh.vertices;geometry.triangles=mesh.triangles;}
                meshes.Add(mesh,geometry);
            }
            trees.Add(new Tree{filter=filter,renderer=renderer,geometry=geometry});
        }
        public bool Blocks(Vector3 from,Vector3 target)
        {
            var delta=target-from;float distance=delta.magnitude;if(distance<.001f)return false;
            var ray=new Ray(from,delta/distance);
            foreach(var tree in trees)
            {
                if(tree.filter==null||tree.renderer==null||!tree.filter.gameObject.activeInHierarchy)continue;
                if(!tree.renderer.bounds.IntersectRay(ray,out float hit)||hit>=distance)continue;
                var vertices=tree.geometry.vertices;
                if(vertices==null)
                {
                    // Unreadable imported trees: retain a small trunk, never turn the entire crown's bounding box into a wall.
                    var origin=tree.filter.transform.position;var bounds=tree.renderer.bounds;
                    var trunk=new Bounds(new Vector3(origin.x,bounds.min.y+1.25f,origin.z),new Vector3(.6f,2.5f,.6f));
                    if(trunk.IntersectRay(ray,out hit)&&hit<distance)return true;
                    continue;
                }
                var localFrom=tree.filter.transform.InverseTransformPoint(from);
                var localDelta=tree.filter.transform.InverseTransformPoint(target)-localFrom;
                var triangles=tree.geometry.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                    if(Intersects(localFrom,localDelta,vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]))return true;
            }
            return false;
        }
        static bool Intersects(Vector3 origin,Vector3 delta,Vector3 a,Vector3 b,Vector3 c)
        {
            var e1=b-a;var e2=c-a;var cross=Vector3.Cross(delta,e2);float determinant=Vector3.Dot(e1,cross);
            if(Mathf.Abs(determinant)<.000001f)return false;
            float inverse=1/determinant;var offset=origin-a;float u=Vector3.Dot(offset,cross)*inverse;
            if(u<0||u>1)return false;
            var q=Vector3.Cross(offset,e1);float v=Vector3.Dot(delta,q)*inverse;if(v<0||u+v>1)return false;
            float t=Vector3.Dot(e2,q)*inverse;return t>.0001f&&t<.9999f;
        }
    }
}
