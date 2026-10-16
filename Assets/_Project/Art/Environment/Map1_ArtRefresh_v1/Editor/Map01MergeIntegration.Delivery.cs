using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace ShadowVale.EnvironmentReview
{
    public static partial class Map01MergeIntegration
    {
        [MenuItem("ShadowVale/Merged Map/Inspect C1 warehouse")]static void InspectC1()=>Inspect(1);
        [MenuItem("ShadowVale/Merged Map/Inspect C2 radio station")]static void InspectC2()=>Inspect(2);
        [MenuItem("ShadowVale/Merged Map/Inspect C3 main garrison")]static void InspectC3()=>Inspect(3);
        static void Inspect(int n){if(EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorSceneManager.GetActiveScene().path!=Map)EditorSceneManager.OpenScene(Map);Selection.activeGameObject=Camp(n).gameObject;SceneView.lastActiveSceneView?.FrameSelected();}
        static void FinishReview()
        {
            EditorSceneManager.OpenScene(Map);var deps=new HashSet<string>(AssetDatabase.GetDependencies(new[]{Map,Stage,Root+"/Map1_ArtStage.unity"},true));var removed=new List<string>();
            string boundary=Path.GetFullPath(Root+"/MergeReview")+Path.DirectorySeparatorChar;
            foreach(var guid in AssetDatabase.FindAssets("",new[]{Root+"/MergeReview"})){
                string path=AssetDatabase.GUIDToAssetPath(guid);if(AssetDatabase.IsValidFolder(path)||deps.Contains(path))continue;
                if(!Path.GetFullPath(path).StartsWith(boundary,StringComparison.OrdinalIgnoreCase))throw new Exception("Generated asset outside review directory");
                if(!AssetDatabase.DeleteAsset(path))throw new Exception("Could not remove unused generated asset "+path);removed.Add(path);
            }
            AssetDatabase.SaveAssets();File.WriteAllLines(Work+"/unused_generated_assets_removed.txt",removed);
            File.WriteAllLines(Work+"/final_scene_dependencies.txt",deps.OrderBy(p=>p,StringComparer.Ordinal));
            File.WriteAllText(Work+"/delivery_ready.txt",DateTime.UtcNow.ToString("O")+" merged map open, generated unused intermediates removed; final git review pending");Inspect(3);
        }
    }
}
