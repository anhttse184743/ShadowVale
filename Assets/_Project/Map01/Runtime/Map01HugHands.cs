using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01 {
    // A temporary close-up renderer shares existing bones/materials. The original
    // renderer's mesh/GPU buffer never changes size during gameplay or handoff.
    public sealed class Map01HugHands : IDisposable {
        readonly List<(SkinnedMeshRenderer original,bool enabled,GameObject variant)> held=new();
        public Map01HugHands(Animator actor){
            foreach(string side in new[]{"Left","Right"}){
                var original=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name=="Player_"+side+"Hand");
                var mesh=Resources.Load<Mesh>("Rescue/Story/Nam_Hug_"+side+"Hand");
                if(original==null||mesh==null)continue;
                var obj=new GameObject("Hug_"+side+"Hand");obj.SetActive(false);obj.layer=original.gameObject.layer;
                obj.transform.SetParent(original.transform.parent,false);
                obj.transform.SetLocalPositionAndRotation(original.transform.localPosition,original.transform.localRotation);obj.transform.localScale=original.transform.localScale;
                var renderer=obj.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=original.bones;renderer.rootBone=original.rootBone;
                renderer.sharedMaterials=original.sharedMaterials;renderer.localBounds=original.localBounds;renderer.quality=SkinQuality.Bone4;
                renderer.shadowCastingMode=original.shadowCastingMode;renderer.receiveShadows=original.receiveShadows;
                renderer.updateWhenOffscreen=true;renderer.forceMatrixRecalculationPerRender=true;
                held.Add((original,original.enabled,obj));original.enabled=false;obj.SetActive(true);
            }
        }
        public void Dispose(){foreach(var item in held){if(item.variant!=null){item.variant.SetActive(false);UnityEngine.Object.Destroy(item.variant);}if(item.original!=null)item.original.enabled=item.enabled;}held.Clear();}
    }
}
