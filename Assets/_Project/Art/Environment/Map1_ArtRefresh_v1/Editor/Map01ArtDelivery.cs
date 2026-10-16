using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.EnvironmentReview
{
    [InitializeOnLoad]
    public static class Map01ArtDelivery
    {
        const string Running="ShadowVale.ArtRefreshRegression";
        const string ResultName="ShadowVale.ArtRefreshRegression.Result";
        static TestRunnerApi api;
        static Map01ArtDelivery(){if(SessionState.GetBool(Running,false))EditorApplication.delayCall+=Register;}
        static void Register(){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}
        public static void Integrate()
        {
            var scene=EditorSceneManager.GetActiveScene();if(scene.path!=Map01ArtStage.Stage||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Integrate only the validated art stage in Edit mode.");
            var report=JsonUtility.FromJson<Map01ArtStageValidation.Report>(File.ReadAllText(Map01ArtStage.Work+"/Audit/navigation_validation.json"));
            if(report.failed!=0)throw new Exception("Stage navigation still has failing checks.");
            string path=Map01EnvironmentReview.MapPath;
            using(var sha=SHA256.Create())
            {var hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();if(hash!="82e097e24ab5fda359149e13630875030f57247f27a1ba93c22c5e91dc720dc9")throw new Exception("Original Map 1 changed since backup; inspect before integrating.");}
            File.Copy(path,Map01ArtStage.Work+"/Backup/Map 1.preintegration.unity",true);
            var cameras=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).ToArray();
            foreach(var camera in cameras)
            {if(camera.name=="Art stage camera")Object.DestroyImmediate(camera.gameObject);else camera.enabled=camera.name=="Third person shoulder camera";}
            var gameCamera=cameras.FirstOrDefault(c=>c!=null&&c.name=="Third person shoulder camera");if(gameCamera==null)throw new Exception("Gameplay camera missing.");
            if(!EditorSceneManager.SaveScene(scene,path,true))throw new Exception("Could not save Map 1 integration.");
            File.WriteAllText(Map01ArtStage.Work+"/Audit/integration.txt","Integrated environment, private terrain/NavMesh and walkable entrances into original Map 1 at "+DateTime.UtcNow.ToString("O")+". Gameplay source scripts, mission anchor transforms, enemy counts and rules unchanged. Original scene backup retained.");
            EditorSceneManager.OpenScene(path);
        }
        public static void RunTests(bool chestOnly=false)
        {
            SessionState.SetBool(Running,true);Register();
            string prefix="ShadowVale.Map01.Tests.";
            var filter=new Filter{testMode=TestMode.EditMode,testNames=new[]{
                prefix+"CampChestTests.EachCampHasOneChestThatStandsClearAndCanBeReached",
                prefix+"RescueTests.FourGuardsHaveThreeSeparatedConnectedRoutesAndApproaches",
                prefix+"RescueMotionTests.UntieAndRiseKeepTheRenderedSkinOnTheJetty",
                prefix+"ExtractionTests.NaturalBoardingDepartsBeforeCompletion",
                prefix+"ExtractionTests.GroundedMatchingActorsAndBoardingWithAllEnemiesAlive"}};
            if(chestOnly)filter.testNames=filter.testNames.Take(1).ToArray();
            SessionState.SetString(ResultName,chestOnly?"camp_chest_regression.xml":"gameplay_regression.xml");
            api.Execute(new ExecutionSettings(filter));
        }
        public static void SyncExistingGrenadeArt()
        {
            const string modelPath="Assets/_Project/Art/Environment/Map01Items/Meshes/Item_grenade_f1.fbx";
            const string catalogPath="Assets/_Project/Map01/Resources/Items/Map01ItemCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<ShadowVale.Map01.Map01ItemCatalog>(catalogPath);
            if(catalog==null||AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)==null)throw new Exception("Existing grenade model or catalog missing.");
            File.Copy(catalogPath,Map01ArtStage.Work+"/Backup/Map01ItemCatalog.before_art_sync.asset",true);
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            foreach(var material in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
            {
                var existing=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01Items/Materials/"+material.name+".mat")
                    ??AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/FieldCamp/Materials/"+material.name+".mat");
                if(existing!=null)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),existing);
            }
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
            var entries=catalog.items.ToList();int index=entries.FindIndex(e=>e.id=="grenade_f1");
            var entry=new ShadowVale.Map01.Map01ItemCatalog.Entry{id="grenade_f1",model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),scale=1};
            if(index<0)entries.Add(entry);else entries[index]=entry;
            catalog.items=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            File.WriteAllText(Map01ArtStage.Work+"/Audit/catalog_art_sync.txt","Linked existing Item_grenade_f1.fbx into existing catalog; reused existing materials. Gameplay rules unchanged.");
        }
        public static void BuildBenchmark()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play mode before building.");
            var scene=EditorSceneManager.OpenScene(Map01ArtStage.Stage);string folder=Map01ArtStage.Root+"/StageData/Benchmark";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            string benchmarkScene=folder+"/Map 1.unity";EditorSceneManager.SaveScene(scene,benchmarkScene,true);scene=EditorSceneManager.OpenScene(benchmarkScene);
            foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).ToArray())
            {if(c.name=="Art stage camera")Object.DestroyImmediate(c.gameObject);else c.enabled=c.name=="Third person shoulder camera";}
            var harness=new GameObject("Local standalone rendering benchmark").AddComponent<Map01RenderBench>();harness.outputRoot=Path.GetFullPath(Map01ArtStage.Work+"/Benchmark/Measurements");
            Directory.CreateDirectory(harness.outputRoot);EditorSceneManager.SaveScene(scene);
            string executable=Path.GetFullPath(Map01ArtStage.Work+"/Benchmark/Player/ShadowVale_ArtBench.exe");Directory.CreateDirectory(Path.GetDirectoryName(executable));
            var timingProperty=typeof(PlayerSettings).GetProperty("enableFrameTimingStats");var previousTiming=timingProperty?.GetValue(null);if(timingProperty!=null)timingProperty.SetValue(null,true);
            UnityEditor.Build.Reporting.BuildReport report;try{report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{benchmarkScene},locationPathName=executable,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});}
            finally{if(timingProperty!=null)timingProperty.SetValue(null,previousTiming);}
            File.WriteAllText(Map01ArtStage.Work+"/Audit/benchmark_build.txt",report.summary.result+"; "+report.summary.totalSize+" bytes; "+report.summary.totalTime+"; "+report.summary.totalErrors+" errors.");
            EditorSceneManager.OpenScene(Map01ArtStage.Stage);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Benchmark player build failed.");
        }
        class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor test)=>File.WriteAllText(Map01ArtStage.Work+"/Audit/regression_status.txt","RUNNING "+test.FullName);
            public void TestStarted(ITestAdaptor test)=>File.AppendAllText(Map01ArtStage.Work+"/Audit/regression_progress.txt","START "+test.FullName+Environment.NewLine);
            public void TestFinished(ITestResultAdaptor result)=>File.AppendAllText(Map01ArtStage.Work+"/Audit/regression_progress.txt",result.ResultState+" "+result.FullName+" "+result.Message+Environment.NewLine);
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,Path.GetFullPath(Map01ArtStage.Work+"/Audit/"+SessionState.GetString(ResultName,"gameplay_regression.xml")));
                File.WriteAllText(Map01ArtStage.Work+"/Audit/regression_status.txt",$"COMPLETE {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped. {result.Message}");
                SessionState.EraseBool(Running);
            }
        }
    }
}
