using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01.Editor {
public static class Map02ArrivalSurvey {
[Serializable] public class Part {public string name;public Vector3 position,center,size;public Vector3[] vertices;public int[] triangles;}
[Serializable] public class Report {public Part[] parts;public Vector3[] nav;}
public static void Run() {
EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
Directory.CreateDirectory("SourceArt/Map02_Arrival");
var selected=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(m=>m.sharedMesh!=null && (m.name.ToLowerInvariant().Contains("landing") || m.name.ToLowerInvariant().Contains("canal") || m.name.ToLowerInvariant().Contains("boat"))).ToArray();
var parts=selected.Select(m=>new Part{name=m.name,position=m.transform.position,center=m.GetComponent<Renderer>().bounds.center,size=m.GetComponent<Renderer>().bounds.size,vertices=m.sharedMesh.vertices.Select(v=>m.transform.TransformPoint(v)).ToArray(),triangles=m.sharedMesh.triangles}).ToArray();
var nav=new List<Vector3>();
foreach(var part in parts.Where(p=>p.name.ToLowerInvariant().Contains("landing"))) {
for(float x=-7;x<=7;x+=1)for(float z=-4;z<=4;z+=1)if(NavMesh.SamplePosition(part.center+new Vector3(x,0,z),out var hit,2,NavMesh.AllAreas))nav.Add(hit.position);
var go=new GameObject("Survey camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=50;camera.farClipPlane=350;camera.transform.position=part.center+new Vector3(10,9,-10);camera.transform.LookAt(part.center);
var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes("SourceArt/Map02_Arrival/dock-"+(part.center.z<0?"south":"north")+".png",tex.EncodeToPNG());RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);
}
File.WriteAllText("SourceArt/Map02_Arrival/terrain-survey.json",JsonUtility.ToJson(new Report{parts=parts,nav=nav.ToArray()},true));
File.WriteAllLines("SourceArt/Map02_Arrival/terrain-summary.txt",parts.Select(p=>p.name+" center="+p.center+" size="+p.size));
Debug.Log("Map 2 arrival survey complete.");
}}
}
