using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02ArrivalMovieTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(300000),Explicit("Manual video recording; excluded from routine regression runs.")]public IEnumerator RecordNaturalArrivalForFlowVideo()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return null;
            Map02FlowMovie movie=null;
            try
            {
                Time.captureFramerate=15;
                var m=Object.FindFirstObjectByType<Map01Mission>();m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);
                var extraction=m.GetComponentInChildren<Map01Extraction>();extraction.autoContinueToMap2=false;
                var body=m.player.GetComponent<CharacterController>();body.enabled=false;m.player.position=extraction.approach;body.enabled=true;
                yield return WaitGameSeconds(.3f);extraction.Skip();extraction.ContinueToMap2();
                float until=Time.realtimeSinceStartup+150;
                while(SceneManager.GetActiveScene().name!="Map 2"&&Time.realtimeSinceStartup<until)yield return null;
                Assert.AreEqual("Map 2",SceneManager.GetActiveScene().name);
                var arrival=Object.FindFirstObjectByType<Map02Arrival>();Assert.NotNull(arrival);
                while(arrival.CurrentPhase==Map02Arrival.Phase.Loading&&Time.realtimeSinceStartup<until)yield return null;
                Assert.IsTrue(arrival.IsPlaying);
                movie=new GameObject("Natural arrival movie").AddComponent<Map02FlowMovie>();movie.Begin("Logs/Map02Chapter/FlowVideo/Map2_CapBen.mp4");
                movie.Section("MỞ ĐẦU · Cập bến làng", "Cutscene hiện có · Chuyển tự nhiên từ Map 1; tiếp theo là toàn bộ tuyến nhiệm vụ Map 2.");
                while(arrival.IsPlaying&&Time.realtimeSinceStartup<until)yield return null;
                Assert.IsFalse(arrival.IsPlaying);yield return WaitGameSeconds(1);movie.End();
            }
            finally{movie?.End();Time.captureFramerate=0;}
            yield return new ExitPlayMode();
        }
    }
}
