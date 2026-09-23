using System;
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
        void OnEnable(){Rebuild();RenderPipelineManager.beginCameraRendering+=Render;}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=Render;}
        void OnValidate(){batches=null;}
        public void Rebuild()
        {
            cachedTransform=transform.localToWorldMatrix;
            batches=new Matrix4x4[(plants.Length+1022)/1023][];
            for(int b=0;b<batches.Length;b++)
            {
                int count=Mathf.Min(1023,plants.Length-b*1023);batches[b]=new Matrix4x4[count];
                for(int j=0;j<count;j++){int i=b*1023+j;var p=plants[i];float scale=i<sizes.Length?sizes[i]:1;batches[b][j]=cachedTransform*Matrix4x4.TRS(new Vector3(p.x,p.y,p.z),Quaternion.Euler(0,p.w,0),Vector3.one*scale);}
            }
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
