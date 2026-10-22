using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Color originalAmbientSky,originalAmbientEquator,originalAmbientGround;
        float originalAmbientIntensity,originalReflectionIntensity,originalShadowStrength;
        sealed class NightSurface {public Renderer renderer;public Material[] original,rough;}
        readonly List<NightSurface> nightSurfaces=new();
        bool nightSurfacesActive;
        void SetNightSurfaces(bool enabled)
        {
            if(nightSurfaces.Count==0)foreach(var renderer in Mission.Player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Concat(roles.Values.Where(a=>a.visual!=null).SelectMany(a=>a.visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))).Distinct())
            {
                var original=renderer.sharedMaterials;var rough=original.Select(source=>{if(source==null)return null;var mat=new Material(source){name=source.name+" (Map2 matte night instance)"};mat.DisableKeyword("_EMISSION");if(mat.HasProperty("_EmissionColor"))mat.SetColor("_EmissionColor",Color.black);mat.DisableKeyword("_METALLICSPECGLOSSMAP");if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0);if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.04f);if(mat.HasProperty("_SpecularHighlights"))mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");if(mat.HasProperty("_EnvironmentReflections"))mat.SetFloat("_EnvironmentReflections",0);mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");if(mat.HasProperty("_BaseColor")){var color=mat.GetColor("_BaseColor");mat.SetColor("_BaseColor",new Color(color.r*.8f,color.g*.8f,color.b*.8f,color.a));}return mat;}).ToArray();
                nightSurfaces.Add(new NightSurface{renderer=renderer,original=original,rough=rough});
            }
            if(nightSurfacesActive==enabled)return;nightSurfacesActive=enabled;foreach(var surface in nightSurfaces)if(surface.renderer!=null)surface.renderer.sharedMaterials=enabled?surface.rough:surface.original;
        }
        Volume dayToneVolume;VolumeProfile dayToneProfile;bool originalCameraPost;
        void CaptureNaturalLighting()
        {
            originalAmbientSky=RenderSettings.ambientSkyColor;originalAmbientEquator=RenderSettings.ambientEquatorColor;originalAmbientGround=RenderSettings.ambientGroundColor;originalAmbientIntensity=RenderSettings.ambientIntensity;originalReflectionIntensity=RenderSettings.reflectionIntensity;
            if(sun!=null)originalShadowStrength=sun.shadowStrength;
            var camera=rig.GetComponent<UniversalAdditionalCameraData>();if(camera!=null){originalCameraPost=camera.renderPostProcessing;camera.renderPostProcessing=true;}
            var go=new GameObject("Day 1–Night 1 natural highlight rolloff");go.transform.SetParent(root.transform);dayToneVolume=go.AddComponent<Volume>();dayToneVolume.isGlobal=true;dayToneVolume.priority=100;
            dayToneProfile=ScriptableObject.CreateInstance<VolumeProfile>();var tone=dayToneProfile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);dayToneVolume.sharedProfile=dayToneProfile;
        }
        bool TickNaturalLighting(float dt)
        {
            if(dayLampFlame!=null)dayLampFlame.gameObject.SetActive(State.phase==1);
            bool scoped=State.phase<=1;SetNightSurfaces(State.phase==1);if(dayToneVolume!=null)dayToneVolume.weight=scoped?1:0;
            var camera=rig.GetComponent<UniversalAdditionalCameraData>();if(camera!=null)camera.renderPostProcessing=scoped||originalCameraPost;
            if(!scoped){RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.reflectionIntensity=originalReflectionIntensity;if(sun!=null)sun.shadowStrength=originalShadowStrength;return false;}
            bool night=State.phase==1;bool afternoon=!night&&State.Available(Map02ChapterDefinition.Load().Find("D1_06"));float blend=1-Mathf.Exp(-Mathf.Max(0,dt)*1.5f);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=1;
            RenderSettings.ambientSkyColor=Color.Lerp(RenderSettings.ambientSkyColor,night?new Color(.19f,.23f,.29f):new Color(.48f,.53f,.58f),blend);
            RenderSettings.ambientEquatorColor=Color.Lerp(RenderSettings.ambientEquatorColor,night?new Color(.12f,.14f,.17f):new Color(.33f,.35f,.31f),blend);
            RenderSettings.ambientGroundColor=Color.Lerp(RenderSettings.ambientGroundColor,night?new Color(.065f,.075f,.09f):new Color(.17f,.18f,.15f),blend);
            RenderSettings.reflectionIntensity=Mathf.Lerp(RenderSettings.reflectionIntensity,night?.15f:.38f,blend);
            if(sun!=null)
            {
                sun.intensity=Mathf.Lerp(sun.intensity,night?.12f:afternoon?.62f:(Layout!=null?Layout.dayOneSunIntensity:.82f),blend);
                sun.color=Color.Lerp(sun.color,night?new Color(.66f,.74f,.91f):afternoon?new Color(1,.83f,.65f):new Color(1,.96f,.89f),blend);sun.shadows=LightShadows.Soft;sun.shadowStrength=.82f;
                sun.transform.rotation=Quaternion.Slerp(sun.transform.rotation,night?Quaternion.Euler(25,190,0):afternoon?Quaternion.Euler(18,220,0):sunRotation,blend);
            }
            if(chapterSky!=null&&chapterSky.HasProperty("_Exposure"))chapterSky.SetFloat("_Exposure",Mathf.Lerp(chapterSky.GetFloat("_Exposure"),night?.22f:.8f,blend));
            return true;
        }
        void RestoreNaturalLighting()
        {
            if(nightSurfaces.Count>0)SetNightSurfaces(false);foreach(var surface in nightSurfaces)foreach(var material in surface.rough)if(material!=null)Destroy(material);nightSurfaces.Clear();
            RenderSettings.ambientSkyColor=originalAmbientSky;RenderSettings.ambientEquatorColor=originalAmbientEquator;RenderSettings.ambientGroundColor=originalAmbientGround;RenderSettings.ambientIntensity=originalAmbientIntensity;RenderSettings.reflectionIntensity=originalReflectionIntensity;
            if(sun!=null)sun.shadowStrength=originalShadowStrength;
            if(rig!=null){var camera=rig.GetComponent<UniversalAdditionalCameraData>();if(camera!=null)camera.renderPostProcessing=originalCameraPost;}
            if(dayToneProfile!=null){foreach(var component in dayToneProfile.components)if(component!=null)Destroy(component);Destroy(dayToneProfile);}
        }
    }
}
