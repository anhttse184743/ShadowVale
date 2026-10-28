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

        [Test]
        public void ArrowTurnsInPlaceAtAnyHudScale()
        {
            // The HUD is drawn under a scale matrix (1 only at 1600×900). Turning Nam's arrow must
            // leave it on its spot whatever that scale; GUIUtility.RotateAroundPivot did not, and
            // on the player's screen the arrow swung outside the minimap.
            var pivot = new Vector2(170, 166);
            foreach (float scale in new[] { .8f, 1f, 1.2f })
            {
                var hud = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                foreach (float yaw in new[] { 0f, 90f, 215f })
                {
                    var turned = Map01Hud.RotateAbout(hud, pivot, yaw);
                    Assert.Less(Vector3.Distance(turned.MultiplyPoint(pivot), hud.MultiplyPoint(pivot)), .01f, $"scale {scale}, yaw {yaw}");
                    // And it still turns: a point off the pivot moves.
                    if (yaw != 0) Assert.Greater(Vector3.Distance(turned.MultiplyPoint(pivot + Vector2.up * 10), hud.MultiplyPoint(pivot + Vector2.up * 10)), 1f);
                }
            }
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
            Vector3 start = mission.player.position;
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

            // Not the reference 1600×900, and Nam turned sideways: the arrow must still sit in
            // the middle of the corner map.
            SetPreviewResolution(1280, 720);
            controller.enabled = false;
            mission.player.SetPositionAndRotation(start, Quaternion.Euler(0, 120, 0));
            controller.enabled = true;
            for (int i = 0; i < 6; i++) yield return null;
            ScreenCapture.CaptureScreenshot("Logs/MinimapPreview/turned-720p.png");
            for (int i = 0; i < 6; i++) yield return null;
            SetPreviewResolution(1600, 900);
            yield return new ExitPlayMode();
        }
    }
}
