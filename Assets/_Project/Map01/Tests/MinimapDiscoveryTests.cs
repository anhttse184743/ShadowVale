using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class MinimapDiscoveryTests : ForestSceneTestBase
    {
        [UnityTest] public IEnumerator DiscoverySurvivesSaveLoadAndBothLayoutsShowRealTerrain()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();for(int i=0;i<8;i++)yield return null;
            var storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
            object oldRoot=storage.GetValue(null);storage.SetValue(null,Path.Combine(Application.temporaryCachePath,"minimap-tests-"+Guid.NewGuid().ToString("N")));
            try
            {
                SetPreviewResolution(1600,900);
                var mission=Object.FindFirstObjectByType<Map01Mission>();var map=mission.GetComponent<Map01Minimap>();
                Assert.IsTrue(map.Ready);Assert.AreEqual(new Rect(-110,-100,220,200),map.WorldBounds);
                Assert.IsTrue(map.IsExplored(mission.player.position));
                Assert.IsTrue(map.IsExplored(map.SafeCamp));
                var visited=new Vector3(-85,8,65);var unknown=new Vector3(90,8,85);
                var ground=GameObject.Find("Continuous hills — 220 x 200").GetComponent<Collider>();
                if(ground.Raycast(new Ray(new Vector3(visited.x,100,visited.z),Vector3.down),out var land,200))visited.y=land.point.y+.1f;
                Assert.IsFalse(map.IsExplored(visited));Assert.IsFalse(map.IsExplored(unknown));
                Directory.CreateDirectory("Logs/MinimapDiscovery");
                mission.SetMapOpen(true);for(int i=0;i<6;i++)yield return null;
                ScreenCapture.CaptureScreenshot("Logs/MinimapDiscovery/initial-expanded.png");for(int i=0;i<6;i++)yield return null;
                mission.SetMapOpen(false);
                var controller=mission.player.GetComponent<CharacterController>();controller.enabled=false;mission.player.position=visited;controller.enabled=true;
                yield return WaitGameSeconds(.4f);Assert.IsTrue(map.IsExplored(visited),"Walking into a region must reveal it automatically.");
                mission.Cinematic=false;
                var save=mission.GetComponent<Map01SaveSystem>();Assert.IsTrue(save.SaveSlot(1,out var error,true),error);
                Map01SaveSystem.BeginGame(1);
                for(int i=0;i<180;i++) {
                    yield return null;
                    mission=Object.FindFirstObjectByType<Map01Mission>();
                    if(mission!=null&&mission.IsInitialized&&!Map01SaveSystem.IsRestoring&&mission.GetComponent<Map01Minimap>()?.Ready==true)break;
                }
                Assert.IsFalse(Map01SaveSystem.IsRestoring);
                map=mission.GetComponent<Map01Minimap>();
                Assert.IsTrue(map.IsExplored(visited));Assert.IsTrue(map.IsExplored(map.SafeCamp));Assert.IsFalse(map.IsExplored(unknown));
                mission.SetMapOpen(true);for(int i=0;i<6;i++)yield return null;
                ScreenCapture.CaptureScreenshot("Logs/MinimapDiscovery/restored-expanded.png");for(int i=0;i<6;i++)yield return null;
                mission.SetMapOpen(false);for(int i=0;i<6;i++)yield return null;
                ScreenCapture.CaptureScreenshot("Logs/MinimapDiscovery/restored-small.png");for(int i=0;i<6;i++)yield return null;
                SetPreviewResolution(1280,720);mission.SetMapOpen(true);for(int i=0;i<6;i++)yield return null;
                ScreenCapture.CaptureScreenshot("Logs/MinimapDiscovery/restored-720p.png");for(int i=0;i<6;i++)yield return null;
                SetPreviewResolution(1600,900);mission.SetMapOpen(false);

                // For visual QA only, expose the baked landscape in one expanded screenshot.
                for(float z=-95;z<=95;z+=15)for(float x=-105;x<=105;x+=15)map.Reveal(new Vector3(x,0,z));
                mission.SetMapOpen(true);for(int i=0;i<6;i++)yield return null;
                ScreenCapture.CaptureScreenshot("Logs/MinimapDiscovery/survey-expanded.png");for(int i=0;i<6;i++)yield return null;
                mission.SetMapOpen(false);
                map.RestoreExploration(null);
                Assert.IsFalse(map.IsExplored(unknown),"Older saves without exploration metadata must begin locally, not reveal the whole map.");
            }
            finally {storage.SetValue(null,oldRoot);}
            yield return new ExitPlayMode();
        }
    }
}
