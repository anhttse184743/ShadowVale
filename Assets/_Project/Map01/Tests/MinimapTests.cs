using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class MinimapTests : ForestSceneTestBase
    {
        [Test]
        public void ProjectionKeepsNorthUpAndWorldCenterAligned()
        {
            var world = new Rect(-100, -100, 200, 200);
            var area = new Rect(20, 30, 400, 400);
            Assert.AreEqual(area.center, Map01Minimap.Project(Vector3.zero, world, area));
            Assert.AreEqual(new Vector2(20, 30), Map01Minimap.Project(new Vector3(-100, 0, 100), world, area));
            Assert.AreEqual(new Vector2(420, 430), Map01Minimap.Project(new Vector3(100, 0, -100), world, area));
        }

        [UnityTest]
        public IEnumerator SceneSurveyAndBothMapLayoutsRender()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            for (int i = 0; i < 6; i++) yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var minimap = mission.GetComponent<Map01Minimap>();
            Assert.IsNotNull(minimap);
            Assert.IsTrue(minimap.Ready);
            Assert.IsFalse(ForestMenu.Visible);
            Directory.CreateDirectory("Logs/MinimapPreview");
            ScreenCapture.CaptureScreenshot("Logs/MinimapPreview/small.png");
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsNotNull(Resources.Load<Texture2D>("Hud/CombatIcons"));
            mission.RestoreHealthFromSave(mission.Settings.playerHP * .5f);
            for (int i = 0; i < 3; i++) yield return null;
            ScreenCapture.CaptureScreenshot("Logs/MinimapPreview/half-health.png");
            for (int i = 0; i < 6; i++) yield return null;
            Assert.AreEqual(mission.Settings.playerHP * .5f, mission.PlayerHealth, .01f);
            mission.SetMapOpen(true);
            for (int i = 0; i < 3; i++) yield return null;
            ScreenCapture.CaptureScreenshot("Logs/MinimapPreview/expanded.png");
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsTrue(mission.CloseGameplayPanel());
            Assert.IsFalse(mission.MapOpen);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NamStaysCentredOnTheCornerMapAtTheEdge()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            for (int i = 0; i < 6; i++) yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var minimap = mission.GetComponent<Map01Minimap>();
            var area = new Rect(0, 0, 200, 200);
            var controller = mission.player.GetComponent<CharacterController>();
            foreach (var corner in new[] { mission.mapMin, mission.mapMax, new Vector2(mission.mapMin.x, mission.mapMax.y) })
            {
                controller.enabled = false;
                mission.player.position = new Vector3(corner.x, mission.player.position.y, corner.y);
                controller.enabled = true;
                yield return null; yield return null;
                var p = Map01Minimap.Project(mission.player.position, minimap.View(false), area);
                Assert.Less(Vector2.Distance(area.center, p), .5f, $"Nam's arrow must stay centred at {corner}.");
            }
            Directory.CreateDirectory("Logs/MinimapPreview");
            ScreenCapture.CaptureScreenshot("Logs/MinimapPreview/edge.png");
            for (int i = 0; i < 6; i++) yield return null;
            yield return new ExitPlayMode();
        }
    }
}
