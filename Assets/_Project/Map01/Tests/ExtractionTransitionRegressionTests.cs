using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests {
public sealed class ExtractionTransitionRegressionTests:ForestSceneTestBase {
 [UnityTest,Timeout(420000)]public IEnumerator CompleteCheckpointWithExistingGripChannelsReachesMap2Gameplay(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
  yield return new EnterPlayMode();yield return Run();yield return new ExitPlayMode();
 }
 static IEnumerator Run(){
  Map01Mission m=null;Animator commander=null;float end=Time.realtimeSinceStartup+90;
  while(Time.realtimeSinceStartup<end){m=Object.FindFirstObjectByType<Map01Mission>();commander=Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).FirstOrDefault(a=>a.name==Map01OpeningCutscene.CommanderVisualName);if(m!=null&&m.IsInitialized&&commander!=null)break;yield return null;}
  Assert.NotNull(commander);Assert.True(m.IsInitialized);
  var readable=Resources.Load<Mesh>("Cutscenes/Commander_Paddle_Readable");var skin=commander.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.sharedMesh.vertexCount==readable.vertexCount);
  var seed=Object.Instantiate(readable);seed.ClearBlendShapes();var zero=new Vector3[seed.vertexCount];
  seed.AddBlendShapeFrame("PaddleGripLeft",100,zero,zero,zero);seed.AddBlendShapeFrame("PaddleGripRight",100,zero,zero,zero);skin.sharedMesh=seed;
  var source=Map01Extraction.Get(m);Assert.NotNull(source);m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.CompleteStage);
  source.ContinueToMap2();Assert.True(source.Prepared);
  Map02Arrival arrival=null;end=Time.realtimeSinceStartup+270;
  while(Time.realtimeSinceStartup<end){arrival=Object.FindFirstObjectByType<Map02Arrival>();if(arrival!=null&&arrival.CurrentPhase==Map02Arrival.Phase.Gameplay)break;yield return null;}
  Assert.NotNull(arrival);Assert.AreEqual(Map02Arrival.Phase.Gameplay,arrival.CurrentPhase);
  Assert.True(arrival.Passengers[0].GetComponent<ShadowVale.Gameplay.Player.PlayerController>().enabled);
  yield return null;yield return null; // Expected info logs are allowed; Unity still fails on unexpected errors/exceptions.
  Directory.CreateDirectory("Logs/PaddleGripFix");var pipeline=GraphicsSettings.currentRenderPipeline;var mode=pipeline?.GetType().GetProperty("gpuResidentDrawerMode")?.GetValue(pipeline);
  File.WriteAllText("Logs/PaddleGripFix/transition.txt",$"device={SystemInfo.graphicsDeviceType}\ngpuResidentDrawer={mode}\narrival={arrival.CurrentPhase}\nsourceReady={source.Prepared}\nexisting grip channels rebuilt without duplicate name; gameplay controls returned\n");
 }
}
}
