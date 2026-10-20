using System;
using System.Collections.Generic;
using UnityEngine;
namespace ShadowVale.Map01
{
    public sealed class Map01SupplyPresentation : ScriptableObject
    {
        public GameObject namModel,hungModel;
        public AnimationClip collect,handoff,hungRest;
        public TextAsset collectCloth,handoffCloth;
        public Vector3 basisX,basisY,basisZ;
        public float namFloorOffset;
    }
    /// <summary>The reviewed identical rig and props share one clock; gameplay actor stays untouched.</summary>
    public sealed class Map01SupplyPerformance : IDisposable
    {
        [Serializable] public sealed class ClothData { public int fps,frames; public Strap[] straps; }
        [Serializable] public sealed class Strap { public string name; public int vertices; public float[] xyz; public int[] triangles; }
        static Map01SupplyPresentation data;
        static ClothData collectData,handoffData;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetCache(){data=null;collectData=handoffData=null;}
        readonly GameObject root;readonly GameObject model;readonly Transform player;
        readonly Renderer[] originals;readonly bool[] hidden;
        readonly AnimationClip clip;readonly ClothData cloth;
        readonly List<(Mesh mesh,Transform node,Vector3[] points,Strap strap)> strips=new();
        readonly Transform originalPack,packBody,packFlap,cargo;
        readonly bool delivering;
        public float Duration=>clip.length;
        public static Map01SupplyPresentation Data=>data!=null?data:data=Resources.Load<Map01SupplyPresentation>("OperationsAnimation/ShelterSupplies/Presentation");
        public static void Warm(){if(Data==null)return;collectData??=JsonUtility.FromJson<ClothData>(Data.collectCloth.text);handoffData??=JsonUtility.FromJson<ClothData>(Data.handoffCloth.text);foreach(var animation in new[]{collectData,handoffData})foreach(var strap in animation.straps)if(strap.vertices<=0||strap.xyz.Length!=animation.frames*strap.vertices*3||strap.triangles==null)throw new InvalidOperationException("Incomplete supply cloth animation: "+strap.name);}
        static Transform Find(Transform root,string name)=>Array.Find(root.GetComponentsInChildren<Transform>(true),t=>t.name==name);
        public static Map01SupplyPerformance Create(Animator actor,Transform player,bool delivering)
        {if(Data==null||Data.namModel==null)return null;Warm();return new Map01SupplyPerformance(actor,player,delivering);}
        Map01SupplyPerformance(Animator actor,Transform player,bool delivering)
        {
            this.player=player;this.delivering=delivering;clip=delivering?Data.handoff:Data.collect;cloth=delivering?handoffData:collectData;
            originals=actor.GetComponentsInChildren<Renderer>(true);hidden=new bool[originals.Length];
            for(int i=0;i<originals.Length;i++){hidden[i]=originals[i].forceRenderingOff;originals[i].forceRenderingOff=true;}
            root=UnityEngine.Object.Instantiate(Data.namModel);root.name="Nam supply interaction";model=root.transform.GetChild(0).gameObject;
            // Keep the live character's materials, including camouflage on the detachable pack.
            var livePack=Array.Find(originals,r=>r.name.IndexOf("Balo",StringComparison.OrdinalIgnoreCase)>=0);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)){
                var source=Array.Find(originals,r=>r.name==renderer.name);
                if(source!=null)renderer.sharedMaterials=source.sharedMaterials;
                if(livePack!=null&&(renderer.name=="Interaction_Backpack_Body"||renderer.name=="Interaction_Backpack_Flap"))renderer.sharedMaterials=livePack.sharedMaterials;
            }
            originalPack=Find(root.transform,"Nam_BaloRanRi");packBody=Find(root.transform,"Interaction_Backpack_Body");packFlap=Find(root.transform,"Interaction_Backpack_Flap");cargo=Find(root.transform,"SupplyBag_Interaction");
            foreach(var strap in cloth.straps){var node=Find(root.transform,strap.name+"_Baked");if(node==null)continue;var filter=node.GetComponent<MeshFilter>();var mesh=new Mesh{name=strap.name+" runtime cloth"};mesh.MarkDynamic();var points=new Vector3[strap.vertices];mesh.vertices=points;var indices=(int[])strap.triangles.Clone();if(Vector3.Dot(Data.basisX,Vector3.Cross(Data.basisY,Data.basisZ))<0)for(int i=0;i<indices.Length;i+=3)(indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]);mesh.triangles=indices;filter.sharedMesh=mesh;strips.Add((mesh,node,points,strap));}
            Sample(0);
        }
        public void Sample(float seconds)
        {
            root.transform.SetPositionAndRotation(player.position+Vector3.up*Data.namFloorOffset,player.rotation);
            clip.SampleAnimation(model,Mathf.Clamp(seconds,0,Duration));
            bool unpacked=seconds>=1.3f&&seconds<=15;
            if(originalPack!=null)originalPack.gameObject.SetActive(!unpacked);
            if(packBody!=null)packBody.gameObject.SetActive(unpacked);if(packFlap!=null)packFlap.gameObject.SetActive(unpacked);
            bool bagVisible=delivering?seconds>=6.25f:seconds<10.65f;
            if(cargo!=null)cargo.gameObject.SetActive(bagVisible);
            float frame=Mathf.Clamp(seconds*cloth.fps,0,cloth.frames-1);int a=Mathf.FloorToInt(frame),b=Mathf.Min(a+1,cloth.frames-1);float blend=frame-a;
            foreach(var strip in strips){
                strip.node.gameObject.SetActive(strip.strap.name.StartsWith("SupplyBag")?bagVisible:unpacked);
                for(int i=0;i<strip.points.Length;i++){
                    int ai=(a*strip.points.Length+i)*3,bi=(b*strip.points.Length+i)*3;
                    var p=new Vector3(Mathf.Lerp(strip.strap.xyz[ai],strip.strap.xyz[bi],blend),Mathf.Lerp(strip.strap.xyz[ai+1],strip.strap.xyz[bi+1],blend),Mathf.Lerp(strip.strap.xyz[ai+2],strip.strap.xyz[bi+2],blend));
                    var local=Data.basisX*p.x+Data.basisY*p.y+Data.basisZ*p.z;
                    strip.points[i]=strip.node.InverseTransformPoint(model.transform.TransformPoint(local));
                }
                strip.mesh.vertices=strip.points;strip.mesh.RecalculateNormals();strip.mesh.RecalculateBounds();
            }
        }
        public void Dispose(){foreach(var strip in strips)UnityEngine.Object.Destroy(strip.mesh);UnityEngine.Object.Destroy(root);for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].forceRenderingOff=hidden[i];}
    }
}
