using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor {
 [InitializeOnLoad] public static class Map02RiverPlayCheck {
  const string Key="ShadowVale.RiverPlayCheck";
  static float start=-1;static GameObject probe;static Vector3 initial;
  static Map02RiverPlayCheck(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=RestoreStartScene;}
  static void RestoreStartScene(PlayModeStateChange change){if(change==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key+"Restore",false)){foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None)){menu.CancelInvoke();menu.enabled=false;}UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Maps/Map 2.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));return;}if(change!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Restore",false))return;SessionState.SetBool(Key+"Restore",false);var path=SessionState.GetString(Key+"StartScene","");UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);}
  static void StartMap2(){var previous=UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;SessionState.SetString(Key+"StartScene",previous==null?"":AssetDatabase.GetAssetPath(previous));SessionState.SetBool(Key+"Restore",true);UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=null;EditorApplication.EnterPlaymode();}
  [MenuItem("ShadowVale/Map 2/Play water preview (skip main menu)")]
  public static void PlayWaterPreview(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save scene before playing water preview.");if(scene.path!="Assets/_Project/Scenes/Maps/Map 2.unity")UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");StartMap2();}
  static void Tick(){
   const string req="Tools/Map02RiverPlayTestV3.request";
   if(!EditorApplication.isPlaying&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists(req)){
    File.Delete(req);var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
    if(scene.path!="Assets/_Project/Scenes/Maps/Map 2.unity"||scene.isDirty){File.WriteAllText("Tools/Map02Reports/river-play-checks.txt","FAIL requires saved Map 2");return;}
    SessionState.SetBool(Key,true);StartMap2();return;
   }
   if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
   try{
    var river=Object.FindFirstObjectByType<RiverWater>();if(river==null){if(Time.time<2)return;throw new Exception("River absent in play mode");}
    if(start<0){start=Time.time;var middle=(river.leftBank[125]+river.rightBank[125])*.5f;initial=middle+Vector3.up;probe=new GameObject("Temporary water interaction probe");probe.transform.position=initial;probe.AddComponent<BoxCollider>().size=Vector3.one*.5f;probe.AddComponent<Rigidbody>().mass=8;}
    if(Time.time-start<12)return;
    var body=probe.GetComponent<Rigidbody>();if(probe.GetComponent<RiverBuoyantBody>()==null)throw new Exception("Trigger failed to attach buoyancy");
    if(!river.TrySample(body.position,Time.time,out var sample)||Mathf.Abs(body.position.y-sample.height)>.8f)throw new Exception("Dropped body failed to float: "+body.position);
    var travel=Vector2.Distance(new Vector2(initial.x,initial.z),new Vector2(body.position.x,body.position.z));if(travel<1)throw new Exception("No current-driven drift");
    var boats=Object.FindObjectsByType<RiverBuoyantBody>(FindObjectsSortMode.None).Where(b=>b.moored).ToArray();if(boats.Length!=2)throw new Exception("Expected two floating boats");
    foreach(var boat in boats){if(Vector3.Distance(boat.transform.position,boat.mooringPoint)>3||!float.IsFinite(boat.transform.position.y))throw new Exception("Unstable moored boat");}
    File.WriteAllLines("Tools/Map02Reports/river-play-checks.txt",new[]{"PASS 12 seconds actual Play Mode","PASS trigger automatically attaches buoyancy to dropped Rigidbody","PASS dropped body floating; downstream travel "+travel.ToString("F2")+" m","PASS both existing boats remain near their moorings","PASS dynamic ripple events: "+river.RippleCount});
    if(river.RippleCount==0)throw new Exception("No ripple events");Finish();
   }catch(Exception e){File.WriteAllText("Tools/Map02Reports/river-play-checks.txt","FAIL "+e);Finish();}
  }
  static void Finish(){SessionState.SetBool(Key,false);start=-1;EditorApplication.ExitPlaymode();}
 }
}



