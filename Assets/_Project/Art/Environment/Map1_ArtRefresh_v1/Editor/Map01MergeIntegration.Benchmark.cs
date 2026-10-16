using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace ShadowVale.EnvironmentReview
{
    public static partial class Map01MergeIntegration
    {
        static void BuildMergedBenchmark()
        {
            var scene=EditorSceneManager.OpenScene(Map);var path=Root+"/MergeReview/Benchmark/Map 1.unity";Directory.CreateDirectory(Path.GetDirectoryName(path));EditorSceneManager.SaveScene(scene,path,true);scene=EditorSceneManager.OpenScene(path);
            var bench=new GameObject("Merged game local performance benchmark").AddComponent<Map01RenderBench>();bench.measureResponse=true;bench.outputRoot=Path.GetFullPath(Work+"/Benchmark/Measurements");Directory.CreateDirectory(bench.outputRoot);EditorSceneManager.SaveScene(scene);
            var executable=Path.GetFullPath("SourceArt/MergeDev_20261016_Benchmark/ShadowVale_MergeBench.exe");Directory.CreateDirectory(Path.GetDirectoryName(executable));
            var property=typeof(PlayerSettings).GetProperty("enableFrameTimingStats");var previous=property?.GetValue(null);property?.SetValue(null,true);
            UnityEditor.Build.Reporting.BuildReport report;try{report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},locationPathName=executable,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});}finally{if(property!=null&&previous!=null)property.SetValue(null,previous);}
            File.WriteAllText(Work+"/benchmark_build.txt",report.summary.result+" errors="+report.summary.totalErrors+" size="+report.summary.totalSize+" path="+executable);
            EditorSceneManager.OpenScene(Map);if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Benchmark build failed");
        }
        static void BuildMergedGame()
        {
            EditorSceneManager.OpenScene(Map);var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            foreach(var required in new[]{"Assets/_Project/Scenes/00_Boot.unity","Assets/_Project/Scenes/01_MainMenu.unity",Map,"Assets/_Project/Scenes/Maps/Map 2.unity"})if(!scenes.Contains(required))throw new Exception("Game build missing scene "+required);
            var executable=Path.GetFullPath("SourceArt/MergeDev_20261016_Build/ShadowVale.exe");Directory.CreateDirectory(Path.GetDirectoryName(executable));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=executable,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Work+"/game_build.txt",report.summary.result+" errors="+report.summary.totalErrors+" size="+report.summary.totalSize+" path="+executable+"\nScenes: "+string.Join("; ",scenes));
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Merged game build failed");
        }
    }
}
