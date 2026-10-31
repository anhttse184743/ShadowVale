using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The sampan's look for the arrival: its renderers and meshes prepared for the village scene, the village cameras muted, and the landing steps.
    /// </summary>
    public sealed partial class Map02Arrival
    {
        void PrepareBoatRendering(bool destinationScene=false) {
            if(!boatVisualsPrepared) {
                foreach(var mesh in GetComponentsInChildren<MeshFilter>(true)) {
                    if(mesh.name!="Moored wooden sampan"&&mesh.name!="Boat bench"&&!mesh.name.StartsWith("Oar "))continue;
                    var old=mesh.GetComponent<MeshRenderer>();if(old==null||mesh.sharedMesh==null)continue;
                    // The authored hull stores Map 1 world coordinates in its
                    // vertices. Center its mesh and pivot together so moving
                    // between scenes does not leave the pivot ~87 m away.
                    var center=mesh.sharedMesh.bounds.center;
                    var copy=Instantiate(mesh.sharedMesh);copy.name=mesh.sharedMesh.name+" (arrival)";boatMeshes.Add(copy);
                    var subMeshes=Enumerable.Range(0,copy.subMeshCount).Select(copy.GetSubMesh).ToArray();
                    var vertices=copy.vertices;
                    for(int i=0;i<vertices.Length;i++)vertices[i]-=center;
                    copy.vertices=vertices;copy.RecalculateBounds();
                    for(int i=0;i<copy.subMeshCount;i++) {
                        var sub=subMeshes[i];var bounds=sub.bounds;bounds.center-=center;sub.bounds=bounds;
                        copy.SetSubMesh(i,sub,UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                    }
                    var visual=new GameObject(mesh.name);visual.SetActive(false);
                    // Independent of the old scene object's active/culling state.
                    visual.transform.SetParent(mesh.transform.parent,false);
                    visual.transform.localPosition=mesh.transform.localPosition+mesh.transform.localRotation*Vector3.Scale(mesh.transform.localScale,center);
                    visual.transform.localRotation=mesh.transform.localRotation;visual.transform.localScale=mesh.transform.localScale;
                    visual.layer=mesh.gameObject.layer;
                    visual.AddComponent<MeshFilter>().sharedMesh=copy;
                    var fresh=visual.AddComponent<MeshRenderer>();
                    fresh.sharedMaterials=old.sharedMaterials.Select(m=>{
                        if(m==null)return null;
                        var material=new Material(m);material.enableInstancing=false;partyMaterials.Add(material);return material;
                    }).ToArray();
                    fresh.shadowCastingMode=old.shadowCastingMode;fresh.receiveShadows=old.receiveShadows;
                    // Only this small cinematic prop bypasses GPU resident
                    // instances. Do not inherit Map 1 culling/property blocks.
                    SetBoatProperties(fresh);boatRenderers.Add(fresh);
                    old.enabled=false;mesh.name="Map 1 source "+mesh.name;
                }
                if(!boatRenderers.Any(r=>r.name=="Moored wooden sampan"))
                    throw new InvalidOperationException("The carried boat has no hull mesh; arrival cannot start.");
                boatVisualsPrepared=true;
            }
            if(destinationScene)for(int i=0;i<boatRenderers.Count;i++) {
                var previous=boatRenderers[i];
                var visual=new GameObject(previous.name);visual.SetActive(false);visual.layer=previous.gameObject.layer;
                visual.transform.SetParent(previous.transform.parent,false);
                visual.transform.SetLocalPositionAndRotation(previous.transform.localPosition,previous.transform.localRotation);
                visual.transform.localScale=previous.transform.localScale;
                visual.AddComponent<MeshFilter>().sharedMesh=previous.GetComponent<MeshFilter>().sharedMesh;
                var fresh=visual.AddComponent<MeshRenderer>();fresh.sharedMaterials=previous.sharedMaterials;
                fresh.shadowCastingMode=previous.shadowCastingMode;fresh.receiveShadows=previous.receiveShadows;
                SetBoatProperties(fresh);
                previous.name="Retired arrival geometry";previous.gameObject.SetActive(false);Destroy(previous.gameObject);boatRenderers[i]=fresh;
            }
            foreach(var renderer in boatRenderers)if(renderer!=null) {
                renderer.gameObject.SetActive(true);renderer.enabled=true;renderer.forceRenderingOff=false;
            }
        }
        static void SetBoatProperties(MeshRenderer renderer) {
            Map01MovingPropRendering.Prepare(renderer);
            // A per-renderer property block opts this prop out of GPU Resident
            // Drawer using the public API. Never copy stale source overrides.
            var properties=new MaterialPropertyBlock();
            var material=renderer.sharedMaterial;
            properties.SetColor("_BaseColor",material!=null&&material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white);
            properties.SetFloat("_InstanceTone",1);
            renderer.SetPropertyBlock(properties);
            // A moving hull must not inherit visibility from baked village cells.
            renderer.allowOcclusionWhenDynamic=false;
        }
        void MuteVillageCameras(Scene scene,LoadSceneMode mode) {
            foreach(var root in scene.GetRootGameObjects())foreach(var other in root.GetComponentsInChildren<Camera>(true))if(other!=camera) {
                other.enabled=false;other.tag="Untagged";
                var unusedRig=other.GetComponent<ThirdPersonCamera>();if(unusedRig!=null)unusedRig.enabled=false;
                var listener=other.GetComponent<AudioListener>();if(listener!=null)listener.enabled=false;
            }
        }
        void BuildLandingSteps() {
            landingSteps=new GameObject("Map 2 arrival timber stairs");
            landingSteps.transform.SetParent(transform.parent,false);
            for(int i=0;i<5;i++) {
                var tread=GameObject.CreatePrimitive(PrimitiveType.Cube);tread.name="Arrival tread "+(i+1);
                tread.transform.SetParent(landingSteps.transform,false);
                float top=.41f+i*.21f;
                tread.transform.position=DockPoint(new Vector3(-.95f-i*.26f,top-.08f-WaterlineRoot,0));
                tread.transform.localScale=new Vector3(.28f,.16f,2.5f);
                tread.transform.rotation=DockRotation;
                tread.GetComponent<Renderer>().sharedMaterial=wood;
            }
        }
    }
}
