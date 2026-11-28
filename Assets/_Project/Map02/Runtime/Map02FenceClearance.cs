using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    // Capture the unfenced routes first. Decoration must yield to houses and existing access.
    public sealed class Map02FenceClearance
    {
        public const float RouteClearance=3f,BuildingClearance=.45f;
        public readonly List<Vector3[]> Routes=new();
        public readonly List<Bounds> Buildings=new();
        readonly List<Vector3[]> roads=new();
        public Map02FenceClearance(IEnumerable<Vector3> destinations)
        {
            var transforms=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach(var t in transforms){
                if(!t.name.StartsWith("Stilt house")&&!t.name.StartsWith("Bamboo thatch house"))continue;
                var meshes=t.GetComponentsInChildren<Renderer>();if(meshes.Length==0)continue;
                var bounds=meshes[0].bounds;foreach(var mesh in meshes)bounds.Encapsulate(mesh.bounds);
                bounds.Expand(new Vector3(BuildingClearance*2,0,BuildingClearance*2));Buildings.Add(bounds);
            }
            foreach(var wall in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)){
                string name=wall.name.ToLowerInvariant();
                if((name.Contains("wall")||name.Contains("tường"))&&wall.bounds.size.y>.7f){var bounds=wall.bounds;bounds.Expand(new Vector3(BuildingClearance*2,0,BuildingClearance*2));Buildings.Add(bounds);}
            }
            foreach(var mesh in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)){
                bool road=false;for(var t=mesh.transform;t!=null;t=t.parent)if(t.name=="Earth roads linking homes between paddies"){road=true;break;}
                if(!road||mesh.sharedMesh==null||!mesh.sharedMesh.isReadable)continue;
                var vertices=mesh.sharedMesh.vertices;var triangles=mesh.sharedMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)roads.Add(new[]{mesh.transform.TransformPoint(vertices[triangles[i]]),mesh.transform.TransformPoint(vertices[triangles[i+1]]),mesh.transform.TransformPoint(vertices[triangles[i+2]])});
            }
            var points=destinations.ToArray();var path=new NavMeshPath();
            for(int i=0;i<points.Length;i++)for(int j=i+1;j<points.Length;j++){
                if(!NavMesh.SamplePosition(points[i],out var a,2,NavMesh.AllAreas)||!NavMesh.SamplePosition(points[j],out var b,2,NavMesh.AllAreas))continue;
                if(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)Routes.Add(path.corners);
            }
        }
        public void Protect(Vector3[] route){if(route!=null&&route.Length>1)Routes.Add(route);}
        public bool Allows(Vector3 start,Vector3 end)
        {
            var min=Vector3.Min(start,end)-new Vector3(.12f,0,.12f);var max=Vector3.Max(start,end)+new Vector3(.12f,0,.12f);
            foreach(var building in Buildings)if(min.x<building.max.x&&max.x>building.min.x&&min.z<building.max.z&&max.z>building.min.z)return false;
            int samples=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,end)/.25f));
            for(int i=0;i<=samples;i++)if(!AllowsPoint(Vector3.Lerp(start,end,(float)i/samples)))return false;
            return true;
        }
        bool AllowsPoint(Vector3 p)
        {
            foreach(var building in Buildings)if(p.x>=building.min.x&&p.x<=building.max.x&&p.z>=building.min.z&&p.z<=building.max.z)return false;
            foreach(var route in Routes)for(int i=1;i<route.Length;i++)if(SegmentDistance(p,route[i-1],route[i])<RouteClearance)return false;
            foreach(var triangle in roads){
                var q=new Vector2(p.x,p.z);var a=new Vector2(triangle[0].x,triangle[0].z);var b=new Vector2(triangle[1].x,triangle[1].z);var c=new Vector2(triangle[2].x,triangle[2].z);
                float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;
                if(Mathf.Abs(Cross(b-a,c-a))<.0001f)continue;
                float x=Cross(b-a,q-a),y=Cross(c-b,q-b),z=Cross(a-c,q-c);
                if((x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0))return false;
                if(SegmentDistance(p,triangle[0],triangle[1])<.6f||SegmentDistance(p,triangle[1],triangle[2])<.6f||SegmentDistance(p,triangle[2],triangle[0])<.6f)return false;
            }
            return true;
        }
        public static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {p.y=a.y=b.y=0;var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude)));}
        public bool AllowsBounds(Bounds bounds)
        {
            foreach(var building in Buildings)if(bounds.min.x<building.max.x&&bounds.max.x>building.min.x&&bounds.min.z<building.max.z&&bounds.max.z>building.min.z)return false;
            var a=bounds.center;var b=a;
            if(bounds.size.x>=bounds.size.z){a.x=bounds.min.x;b.x=bounds.max.x;}else{a.z=bounds.min.z;b.z=bounds.max.z;}
            return Allows(a,b);
        }
    }
}
