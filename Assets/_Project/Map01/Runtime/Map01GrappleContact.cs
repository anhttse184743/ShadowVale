using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01 {
    // A local, disabled query shape. It never participates in gameplay physics.
    // Bake once at the start, then follow the animated head without skinning or
    // scanning the scene each frame. The envelope includes the face and helmet.
    public sealed class Map01GrappleContact : IDisposable {
        readonly Transform head;
        readonly Mesh hull;
        readonly MeshCollider headShape;
        readonly CapsuleCollider segment;
        readonly Vector3 mouthLocal;
        public Vector3 Mouth=>head.position+head.rotation*mouthLocal;
        public Map01GrappleContact(Animator victim) {
            head=victim.GetBoneTransform(HumanBodyBones.Head);
            var root=victim.transform;var points=new List<Vector3>();
            foreach(var skin in victim.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var baked=new Mesh();skin.BakeMesh(baked);
                foreach(var vertex in baked.vertices) {
                    var delta=skin.transform.TransformPoint(vertex)-head.position;
                    float height=Vector3.Dot(delta,root.up),side=Vector3.Dot(delta,root.right),front=Vector3.Dot(delta,root.forward);
                    if(height<-.035f||height>.34f||Mathf.Abs(side)>.20f||front<-.15f||front>.26f)continue;
                    points.Add(Quaternion.Inverse(head.rotation)*delta);
                }
                UnityEngine.Object.Destroy(baked);
            }
            if(points.Count<8)throw new InvalidOperationException("Cannot build the captive's head contact envelope.");
            var up=Quaternion.Inverse(head.rotation)*root.up;var forward=Quaternion.Inverse(head.rotation)*root.forward;var right=Quaternion.Inverse(head.rotation)*root.right;
            float face=points.Where(p=>Mathf.Abs(Vector3.Dot(p,right))<.05f&&Vector3.Dot(p,up)>.015f&&Vector3.Dot(p,up)<.045f)
                .Select(p=>Vector3.Dot(p,forward)).DefaultIfEmpty(.18f).Max();
            mouthLocal=up*.03f+forward*(face+.027f);
            var extremes=new List<Vector3>();
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++) {
                if(x==0&&y==0&&z==0)continue;
                var axis=new Vector3(x,y,z).normalized;
                var point=points.OrderByDescending(p=>Vector3.Dot(p,axis)).First();
                if(!extremes.Contains(point))extremes.Add(point);
            }
            hull=new Mesh{name="Temporary grapple head envelope"};hull.SetVertices(extremes);
            var triangles=new List<int>();for(int i=1;i<extremes.Count-1;i++){triangles.Add(0);triangles.Add(i);triangles.Add(i+1);}hull.SetTriangles(triangles,0);hull.RecalculateBounds();
            var query=new GameObject("Grapple contact queries"){hideFlags=HideFlags.HideAndDontSave};
            headShape=query.AddComponent<MeshCollider>();headShape.convex=true;headShape.sharedMesh=hull;headShape.enabled=false;
            segment=query.AddComponent<CapsuleCollider>();segment.direction=1;segment.enabled=false;
        }
        public float Penetration(Vector3 from,Vector3 to,float radius) {
            segment.radius=radius;segment.height=Vector3.Distance(from,to)+radius*2;
            var rotation=Quaternion.FromToRotation(Vector3.up,to-from);
            return Physics.ComputePenetration(segment,(from+to)*.5f,rotation,headShape,head.position,head.rotation,out _,out var depth)?depth:0;
        }
        public float ArmPenetration(Vector3 shoulder,Vector3 elbow,Vector3 wrist)=>
            Mathf.Max(Penetration(shoulder,elbow,.05f),Penetration(elbow,wrist,.035f));
        public void Dispose(){UnityEngine.Object.Destroy(headShape.gameObject);UnityEngine.Object.Destroy(hull);}
    }
}
