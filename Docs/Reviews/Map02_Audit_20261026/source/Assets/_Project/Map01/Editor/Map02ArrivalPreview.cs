using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
[InitializeOnLoad] public static class Map02ArrivalPreview {
const string Pending="ShadowVale.Map02ArrivalPreview";
static Map02ArrivalPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Restore;}
static void Restore(PlayModeStateChange state){
if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending+"Active",false))return;
var field=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
var before=SessionState.GetString(Pending+"Storage","");field.SetValue(null,string.IsNullOrEmpty(before)?null:before);
var scene=SessionState.GetString(Pending+"Scene","Assets/_Project/Scenes/00_Boot.unity");
EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
SessionState.SetInt(Pending,0);SessionState.SetBool(Pending+"Active",false);
}
[MenuItem("ShadowVale/Cutscene/Preview Map 2 arrival")]
public static void Run(){
if(EditorApplication.isPlaying)return;
if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");
SessionState.SetBool(Pending+"Active",true);SessionState.SetString(Pending+"Scene","Assets/_Project/Scenes/00_Boot.unity");
SessionState.SetInt(Pending,1);EditorApplication.isPlaying=true;
}
static void Tick(){
int pending=SessionState.GetInt(Pending,0);if(pending==0 || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
var mission=Object.FindFirstObjectByType<Map01Mission>();if(mission==null || !mission.IsInitialized || mission.player==null)return;
if(pending==1){
string folder=Path.GetFullPath("Temp/Map02ArrivalPreviewSaves");Directory.CreateDirectory(folder);
var storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
SessionState.SetString(Pending+"Storage",storage.GetValue(null) as string??"");storage.SetValue(null,folder);
mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);
var extraction=mission.GetComponentInChildren<Map01Extraction>();extraction.autoContinueToMap2=false;
var cc=mission.player.GetComponent<CharacterController>();cc.enabled=false;mission.player.position=extraction.approach;cc.enabled=true;
SessionState.SetInt(Pending,2);
} else {
var extraction=mission.GetComponentInChildren<Map01Extraction>();
if(extraction.CurrentPhase<Map01Extraction.Phase.Approach)return;
extraction.Skip();SessionState.SetInt(Pending,0);extraction.ContinueToMap2();
}
}
}}

