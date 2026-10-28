using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace ShadowVale.Map01 {
 // Apply Unity's per-object GPU Resident Drawer exclusion to a moving cross-scene prop.
 // The official component is internal in the installed Core package, so AddComponent uses its exact type.
 public static class Map01MovingPropRendering {
  static readonly Type Exclusion=Type.GetType("UnityEngine.Rendering.DisallowGPUDrivenRendering, Unity.RenderPipelines.GPUDriven.Runtime",true);
  public static void Prepare(MeshRenderer renderer){
   if(renderer.GetComponent(Exclusion)==null)renderer.gameObject.AddComponent(Exclusion);
   renderer.allowOcclusionWhenDynamic=false;
  }
 }
}
