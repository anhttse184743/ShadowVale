using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class TutorialGuidanceTests : ForestSceneTestBase
    {
        private const string Scene = "Assets/_Project/Scenes/Maps/Map00_Tutorial.unity";
        private static IEnumerator Ready()
        {
            double until = Time.realtimeSinceStartupAsDouble + 12;
            while (Time.realtimeSinceStartupAsDouble < until) {
                var d = Object.FindFirstObjectByType<TutorialDirector>();
                if (d != null && d.GetComponent<Map01Mission>().RetryRequested != null && d.GetComponent<TutorialRouteGuide>() != null) break;
                yield return null;
            }
            for (int i = 0; i < 8; i++) yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<TutorialRouteGuide>());
        }
        private static void Lesson(TutorialDirector d, TutorialStep step)
        {
            var mission = d.GetComponent<Map01Mission>(); var body = mission.player.GetComponent<CharacterController>();
            body.enabled = false; mission.player.position = d.lessonStarts[(int)step].position; body.enabled = true;
            typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, new object[] { step });
        }
        private static IEnumerator Frames(int count = 5) { for (int i = 0; i < count; i++) yield return null; }
        private static IEnumerator FinishReveal(TutorialDirector d)
        {
            double until = Time.realtimeSinceStartupAsDouble + 6;
            while (d.RevealingTargets && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.IsFalse(d.RevealingTargets); yield return Frames();
        }
        private static IEnumerator Photo(string name)
        {
            Directory.CreateDirectory("Temp/TutorialGuidance"); ScreenCapture.CaptureScreenshot("Temp/TutorialGuidance/" + name + ".png"); yield return Frames();
        }
        private static void AssertInside(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var corner in corners) {
                var p = parent.InverseTransformPoint(corner);
                Assert.That(p.x, Is.InRange(parent.rect.xMin - 1, parent.rect.xMax + 1), child.name);
                Assert.That(p.y, Is.InRange(parent.rect.yMin - 1, parent.rect.yMax + 1), child.name);
            }
        }
        [UnityTest]
        public IEnumerator KeycapsMouseAndNotesStayReadableAtCommonResolutions()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return Ready();
            var d = Object.FindFirstObjectByType<TutorialDirector>(); var mission = d.GetComponent<Map01Mission>(); var ui = Object.FindFirstObjectByType<TutorialPromptUI>();
            var card = (RectTransform)ui.transform.Find("Hint"); var plate = card.Find("Key plate");
            Assert.IsFalse(ShaderUtil.ShaderHasError(card.GetComponent<UnityEngine.UI.Image>().material.shader), "Glass shader compiles for the current graphics device.");
            Assert.AreEqual(4, plate.GetComponentsInChildren<UnityEngine.UI.Text>().Count(t => t.name == "Glyph"));
            foreach (var graphic in card.GetComponentsInChildren<UnityEngine.UI.Graphic>()) Assert.IsFalse(graphic.raycastTarget, graphic.name);
            foreach (var text in card.GetComponentsInChildren<UnityEngine.UI.Text>()) {
                if (!text.enabled) continue;
                AssertInside(text.name == "Next caption" ? (RectTransform)ui.transform : card, text.rectTransform);
                Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 2, text.name + ": " + text.text);
            }
            yield return Photo("01-move-and-route");
            Lesson(d, TutorialStep.Sprint); yield return Frames();
            StringAssert.Contains("một lần", card.Find("Progress text").GetComponent<UnityEngine.UI.Text>().text);
            Assert.AreEqual("NHẤN", plate.Find("Control mode").GetComponent<UnityEngine.UI.Text>().text);
            Lesson(d, TutorialStep.Shoot); mission.ModernCombat.Equip(WeaponKind.Rifle); yield return FinishReveal(d);
            Assert.AreEqual("GIỮ", plate.Find("Control mode").GetComponent<UnityEngine.UI.Text>().text);
            Assert.AreEqual(2, plate.GetComponentInChildren<TutorialMouseGlyph>().Button);
            Canvas.ForceUpdateCanvases();
            var mouseMesh = plate.GetComponentInChildren<TutorialMouseGlyph>().canvasRenderer.GetMesh();
            Assert.IsNotNull(mouseMesh); Assert.Greater(mouseMesh.vertexCount, 0, "The mouse icon must actually render, not just have a label.");
            yield return Photo("02-aim-mouse");
            Lesson(d, TutorialStep.StoneKnife); yield return FinishReveal(d);
            Assert.IsNotEmpty(d.Tip); yield return Photo("03-stealth-note");
            SetPreviewResolution(1280, 720); yield return Frames(); Canvas.ForceUpdateCanvases();
            AssertInside((RectTransform)ui.transform, card);
            foreach (string name in new[] { "Instruction", "Lesson", "Progress text" }) {
                var text = card.Find(name).GetComponent<UnityEngine.UI.Text>();
                AssertInside(card, text.rectTransform); Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 2, text.name + ": " + text.text);
            }
            yield return Photo("04-1280x720");
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ChevronsFollowWalkableGroundPulseInOrderAndReverseOffTheBridge()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return Ready();
            var d = Object.FindFirstObjectByType<TutorialDirector>(); var mission = d.GetComponent<Map01Mission>(); var guide = d.GetComponent<TutorialRouteGuide>();
            Assert.IsTrue(guide.HasPath); Assert.Greater(guide.VisibleCount, 2);
            var root = d.transform.Find("Tutorial route chevrons"); var renderers = root.GetComponentsInChildren<MeshRenderer>();
            Assert.IsFalse(ShaderUtil.ShaderHasError(renderers[0].sharedMaterial.shader), "The route's glow and frame shader must compile on the current graphics device.");
            var property = new MaterialPropertyBlock(); float min = 2, max = 0;
            foreach (var arrow in renderers) {
                Assert.IsNull(arrow.GetComponent<Collider>()); arrow.GetPropertyBlock(property);
                float brightness = property.GetColor("_Tint").r; min = Mathf.Min(min, brightness); max = Mathf.Max(max, brightness);
                Assert.Greater(Vector3.Dot(arrow.transform.forward, guide.Target - arrow.transform.position), 0);
                Assert.IsTrue(Physics.Raycast(arrow.transform.position + arrow.transform.up * .1f, -arrow.transform.up, .25f, ~0, QueryTriggerInteraction.Ignore), arrow.name + " sits on the ground");
            }
            Assert.Greater(max - min, .1f, "The route has a bright travelling pulse, not a single solid colour.");
            var first = renderers[0]; first.GetPropertyBlock(property); var before = property.GetColor("_Tint");
            double until = Time.realtimeSinceStartupAsDouble + .4;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
            first.GetPropertyBlock(property); Assert.AreNotEqual(before, property.GetColor("_Tint"), "Pulse continues during tutorial slow motion.");
            yield return Photo("08-golden-chevrons");
            // Approach the on-bridge cargo from the land lesson before it: the navigation path must use the ramp.
            Lesson(d, TutorialStep.Squad);
            typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, new object[] { TutorialStep.Scout });
            yield return Frames(); Assert.IsTrue(guide.HasPath); Assert.Greater(guide.VisibleCount, 5);
            yield return Photo("05-route-to-bridge");
            Lesson(d, TutorialStep.Scout); yield return Frames(); yield return Photo("06-bridge-scan");
            // Start Return from the cargo location rather than teleporting back to its checkpoint.
            typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, new object[] { TutorialStep.Return });
            yield return Frames(); Assert.IsTrue(guide.HasPath); Assert.Greater(guide.VisibleCount, 0);
            foreach (var arrow in root.GetComponentsInChildren<MeshRenderer>())
                Assert.Greater(Vector3.Dot(arrow.transform.forward, guide.Target - arrow.transform.position), -.1f, "Return arrows point towards the bank/base.");
            yield return Photo("07-return-route");
            mission.SetPaused(true); yield return Frames(); Assert.AreEqual(0, guide.VisibleCount); Assert.IsFalse(root.gameObject.activeSelf);
            mission.SetPaused(false); yield return Frames(); Assert.Greater(guide.VisibleCount, 0);
            d.Fail("Guidance checkpoint check"); yield return Frames(); Assert.AreEqual(0, guide.VisibleCount);
            d.RetryStep(); yield return Frames(); Assert.Greater(guide.VisibleCount, 0);
            mission.Cinematic = true; yield return Frames(); Assert.AreEqual(0, guide.VisibleCount);
            mission.Cinematic = false; mission.SetMapOpen(true); yield return Frames(); Assert.AreEqual(0, guide.VisibleCount);
            yield return new ExitPlayMode();
        }
    }
}
