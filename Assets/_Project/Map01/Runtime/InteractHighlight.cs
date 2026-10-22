using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowVale.Map01
{
    /// <summary>
    /// A faint warm outline round whatever [E] would use right now. The interaction code names its focus every
    /// frame; a frame without a call (a panel open, a conversation, nothing in reach) draws nothing. The outline
    /// is each mesh of the target drawn again by Resources/Effects/InteractOutline, so no scene object changes.
    /// </summary>
    public sealed class InteractHighlight : MonoBehaviour
    {
        const float MaxSize=3.5f; // Never outline a whole house or tree that a point happens to sit in.
        static InteractHighlight instance;static Material material;
        Transform focus;int focusFrame=-1;
        readonly List<(Mesh mesh,int submesh,MeshRenderer renderer)> parts=new List<(Mesh,int,MeshRenderer)>();

        /// <summary>What is outlined this frame, or null.</summary>
        public static Transform Current=>instance!=null&&instance.focusFrame==Time.frameCount?instance.focus:null;
        /// <summary>How many mesh parts the current outline draws.</summary>
        public static int PartCount=>Current!=null?instance.parts.Count:0;

        public static void Focus(Transform target)
        {
            if(instance==null)
            {
                var go=new GameObject("Interact highlight"){hideFlags=HideFlags.HideAndDontSave};
                DontDestroyOnLoad(go);instance=go.AddComponent<InteractHighlight>();
            }
            instance.Set(target);
        }
        void Set(Transform target)
        {
            focusFrame=Time.frameCount;
            if(target==focus)return;
            focus=target;parts.Clear();
            if(target==null)return;
            foreach(var r in target.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled||r.bounds.size.magnitude>MaxSize)continue;
                var filter=r.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;if(mesh==null)continue;
                // A statically batched part lives in the combined mesh, already in world space, from its start submesh.
                int first=r.isPartOfStaticBatch?r.subMeshStartIndex:0,count=r.isPartOfStaticBatch?r.sharedMaterials.Length:mesh.subMeshCount;
                for(int i=0;i<count&&first+i<mesh.subMeshCount;i++)parts.Add((mesh,first+i,r));
            }
        }
        void LateUpdate()
        {
            if(focus==null||focusFrame!=Time.frameCount||parts.Count==0)return;
            if(material==null)
            {
                var shader=Resources.Load<Shader>("Effects/InteractOutline");if(shader==null||!shader.isSupported)return;
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            }
            var draw=new RenderParams(material){shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false};
            foreach(var (mesh,submesh,r) in parts)
            {
                if(r==null||!r.enabled||!r.gameObject.activeInHierarchy)continue;
                draw.layer=r.gameObject.layer;
                Graphics.RenderMesh(draw,mesh,submesh,r.isPartOfStaticBatch?Matrix4x4.identity:r.localToWorldMatrix);
            }
        }
    }
}
