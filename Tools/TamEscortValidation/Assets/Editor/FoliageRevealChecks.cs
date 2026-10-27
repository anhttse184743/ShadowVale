using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad] public static class FoliageRevealChecks
{
    const string Key="Map2FoliageRevealChecks";
    static readonly List<string> report=new List<string>();
    static FoliageRevealChecks(){EditorApplication.update+=Tick;}
    public static void BeginLive(){SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;}
    static void Tick()
    {
        int phase=SessionState.GetInt(Key,0);if(phase==0)return;
        if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(Key,0);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;}
        if(phase!=1||!EditorApplication.isPlaying)return;
        SessionState.SetInt(Key,2);
        try{Run();SessionState.SetInt(Key+"Exit",0);}catch(Exception error){report.Add("FAIL "+error);Debug.LogException(error);SessionState.SetInt(Key+"Exit",1);}
        File.WriteAllLines("foliage-reveal-checks.txt",report);SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;
    }
    static void Check(bool value,string description){if(!value)throw new Exception(description);report.Add("PASS "+description);}
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    static MeshRenderer Cube(Transform root,string name,Vector3 position,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root);go.transform.position=position+Vector3.up;
        go.transform.localScale=new Vector3(2,2,2);var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;return r;
    }
    static float Alpha(Renderer r){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block,0);return block.GetColor("_BaseColor").a;}
    static void Step(Map01FoliageReveal reveal,int frames=180){for(int i=0;i<frames;i++)Call(reveal,"LateUpdate");}
    static void Run()
    {
        var root=new GameObject("Map 2 foliage reveal probes");Material material=null;
        try
        {
            material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/FoliageRevealProbe.shader"));material.SetColor("_BaseColor",Color.green);
            var bush=Cube(root.transform,"BushB",Vector3.zero,material);
            var crate=Cube(root.transform,"Crate",new Vector3(4,0,0),material);
            var quest=Cube(root.transform,"Bụi cây quan sát đường ruộng",new Vector3(16,0,0),material);quest.gameObject.SetActive(false);
            var hedge=new GameObject("Blender Hedge");hedge.transform.SetParent(root.transform);
            var lod0=Cube(hedge.transform,"Hedge LOD0",new Vector3(8,0,0),material);var lod1=Cube(hedge.transform,"Hedge LOD1",new Vector3(8,0,0),material);
            hedge.AddComponent<LODGroup>().SetLODs(new[]{new LOD(.2f,new Renderer[]{lod0}),new LOD(.01f,new Renderer[]{lod1})});
            var block=new MaterialPropertyBlock();block.SetFloat("_InstanceTone",.7f);bush.SetPropertyBlock(block);
            var indexed=new MaterialPropertyBlock();indexed.SetFloat("_InstanceTone",.9f);indexed.SetColor("_BaseColor",new Color(.2f,.8f,.3f,.8f));bush.SetPropertyBlock(indexed,0);
            var pawn=new GameObject("Nam");pawn.transform.SetParent(root.transform);var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;
            var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);
            var reveal=root.AddComponent<Map01FoliageReveal>();reveal.enabled=false;Call(reveal,"Start");
            pawn.transform.position=Vector3.zero;
            Call(reveal,"LateUpdate");Check(Alpha(bush)>.8f*.28f&&Alpha(bush)<.8f,"fade starts gradually instead of popping fully transparent");
            Step(reveal);Check(Mathf.Abs(Alpha(bush)-.8f*.28f)<.001f,"inside bush reaches 28 percent of original opacity");
            Check(bush.sharedMaterial!=material&&bush.sharedMaterial.renderQueue==3000&&bush.sharedMaterial.GetFloat("_ZWrite")==0,"only this renderer uses transparent blend and no depth write");
            Check(material.GetColor("_BaseColor")==Color.green&&crate.sharedMaterial==material,"shared source material and unrelated crate stay unchanged");
            Check(bush.GetComponent<Collider>().enabled,"foliage collider remains enabled");
            var tinted=new MaterialPropertyBlock();bush.GetPropertyBlock(tinted,0);Check(Mathf.Abs(tinted.GetFloat("_InstanceTone")-.9f)<.001f&&Mathf.Abs(tinted.GetColor("_BaseColor").r-.2f)<.001f,"indexed tone and tint are preserved during fade");
            pawn.transform.position=new Vector3(4,0,0);Step(reveal);
            Check(bush.sharedMaterial==material&&reveal.ActivePlants==0,"leaving bush restores opaque original materials");
            bush.GetPropertyBlock(tinted,0);Check(Mathf.Abs(tinted.GetColor("_BaseColor").a-.8f)<.001f&&Mathf.Abs(tinted.GetFloat("_InstanceTone")-.9f)<.001f,"original indexed property block restored exactly");
            bush.GetPropertyBlock(tinted);Check(Mathf.Abs(tinted.GetFloat("_InstanceTone")-.7f)<.001f,"renderer-level property block restored exactly");
            pawn.transform.position=new Vector3(8,0,0);Step(reveal);
            Check(lod0.sharedMaterial!=material&&lod1.sharedMaterial==lod0.sharedMaterial&&Mathf.Abs(Alpha(lod1)-.28f)<.001f,"both hedge LODs fade together without an opaque LOD pop");
            quest.gameObject.SetActive(true);pawn.transform.position=new Vector3(16,0,0);Step(reveal);
            Check(quest.sharedMaterial!=material&&Mathf.Abs(Alpha(quest)-.28f)<.001f,"initially inactive night quest bush fades after activation");
            Call(reveal,"OnDisable");Check(quest.sharedMaterial==material&&reveal.ActivePlants==0,"disable restores faded foliage immediately");
        }
        finally
        {
            if(root!=null){var mission=root.GetComponent<Map02Mission>();if(mission!=null)Object.DestroyImmediate(mission);Object.DestroyImmediate(root);}
            if(material!=null)Object.DestroyImmediate(material);
        }
    }
}
