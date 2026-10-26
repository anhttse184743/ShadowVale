using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]public static class Map02BridgeRailSetup
    {
        const string Path="Assets/_Project/Scenes/Maps/Map 2.unity",Request="Temp/Map02BridgeRails.request";
        static Map02BridgeRailSetup(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Request);
            try{Build();}catch(Exception e){Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllText("Logs/Map02Chapter/bridge-rails-error.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("ShadowVale/Map 2/Sửa collider lan can cầu")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before saving bridge colliders.");
            var scene=SceneManager.GetSceneByPath(Path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new InvalidOperationException("Map 2 has unsaved edits; save them before applying bridge colliders.");
            if(opened)scene=EditorSceneManager.OpenScene(Path,OpenSceneMode.Additive);
            try
            {
                var rails=Map02BridgeRails.Install(scene);if(rails.Count<4)throw new InvalidOperationException("Expected at least two village bridges.");
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllText("Logs/Map02Chapter/bridge-rails-setup.txt","Saved "+rails.Count+" solid side colliders. Bridge entrances remain open.\n");
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
