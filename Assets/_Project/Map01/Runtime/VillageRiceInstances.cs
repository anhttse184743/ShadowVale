using System;
using System.Collections.Generic;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.Rendering;
namespace ShadowVale.Map01
{
    /// <summary>Shared Blender rice meshes, submitted in instanced batches in edit mode and play mode.</summary>
    [ExecuteAlways]
    public sealed class VillageRiceInstances : MonoBehaviour
    {
        public Mesh detailedMesh;
        public Mesh distantMesh;
        public Material material;
        public Vector4[] plants = Array.Empty<Vector4>(); // Local xyz, yaw in degrees.
        public float[] sizes = Array.Empty<float>();
        [NonSerialized] Matrix4x4[][] batches;
        [NonSerialized] Matrix4x4 cachedTransform;
        static readonly HashSet<VillageRiceInstances> active = new();
        readonly Dictionary<Vector2Int,List<Vector3>> coverCells = new();
        void OnEnable(){active.Add(this);Rebuild();RenderPipelineManager.beginCameraRendering+=Render;}
        void OnDisable(){active.Remove(this);RenderPipelineManager.beginCameraRendering-=Render;}
        void OnValidate(){batches=null;}
        public void Rebuild()
        {
            cachedTransform=transform.localToWorldMatrix;
            coverCells.Clear();
            // This renderer also draws roadside grass, which is not a rice hiding spot.
            if(detailedMesh!=null && detailedMesh.name.IndexOf("Rice_",StringComparison.OrdinalIgnoreCase)>=0)
                foreach(var plant in plants) {
                    var point=transform.TransformPoint(new Vector3(plant.x,plant.y,plant.z));
                    var cell=new Vector2Int(Mathf.FloorToInt(point.x),Mathf.FloorToInt(point.z));
                    if(!coverCells.TryGetValue(cell,out var points))coverCells[cell]=points=new List<Vector3>();
                    points.Add(point);
                }
            batches=new Matrix4x4[(plants.Length+1022)/1023][];
            for(int b=0;b<batches.Length;b++)
            {
                int count=Mathf.Min(1023,plants.Length-b*1023);batches[b]=new Matrix4x4[count];
                for(int j=0;j<count;j++){int i=b*1023+j;var p=plants[i];float scale=i<sizes.Length?sizes[i]:1;batches[b][j]=cachedTransform*Matrix4x4.TRS(new Vector3(p.x,p.y,p.z),Quaternion.Euler(0,p.w,0),Vector3.one*scale);}
            }
        }
        public static bool Conceals(PlayerController player)
            => player!=null && player.IsProne && ContainsRice(player.transform.position);

        public static bool ContainsRice(Vector3 position)
        {
            foreach(var field in active) {
                if(field==null || !field.isActiveAndEnabled)continue;
                if(field.batches==null || field.cachedTransform!=field.transform.localToWorldMatrix)field.Rebuild();
                // Match actual planted clumps, not the rectangular bounds of the whole field.
                int x=Mathf.FloorToInt(position.x),z=Mathf.FloorToInt(position.z);
                for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                    if(field.coverCells.TryGetValue(new Vector2Int(x+dx,z+dz),out var points))
                        foreach(var point in points)
                            if(Mathf.Abs(position.y-point.y)<.6f && new Vector2(position.x-point.x,position.z-point.z).sqrMagnitude<=.45f*.45f)return true;
            }
            return false;
        }
        public int ClearAccess(Vector3 center,Vector3 approach,float radius,float pathHalfWidth)
        {
            var kept=new System.Collections.Generic.List<Vector4>();var scales=new System.Collections.Generic.List<float>();
            var segment=Vector3.ProjectOnPlane(approach-center,Vector3.up);float length=segment.sqrMagnitude;
            for(int i=0;i<plants.Length;i++)
            {
                var p=plants[i];var world=transform.TransformPoint(new Vector3(p.x,p.y,p.z));var delta=Vector3.ProjectOnPlane(world-center,Vector3.up);
                float t=length>.001f?Mathf.Clamp01(Vector3.Dot(delta,segment)/length):0;
                if(delta.sqrMagnitude<radius*radius||(delta-segment*t).sqrMagnitude<pathHalfWidth*pathHalfWidth)continue;
                kept.Add(p);scales.Add(i<sizes.Length?sizes[i]:1);
            }
            int removed=plants.Length-kept.Count;if(removed==0)return 0;
            plants=kept.ToArray();sizes=scales.ToArray();Rebuild();return removed;
        }
        void Render(ScriptableRenderContext context,Camera camera)
        {
            if(!isActiveAndEnabled||material==null||detailedMesh==null||!SystemInfo.supportsInstancing)return;
            if((camera.cullingMask & (1<<gameObject.layer))==0)return;
            if(batches==null||cachedTransform!=transform.localToWorldMatrix)Rebuild();
            Mesh mesh=distantMesh!=null&&Vector3.Distance(camera.transform.position,transform.position)>48?distantMesh:detailedMesh;
            foreach(var batch in batches)Graphics.DrawMeshInstanced(mesh,0,material,batch,batch.Length,null,ShadowCastingMode.Off,true,gameObject.layer,camera,LightProbeUsage.Off);
        }
    }
}
