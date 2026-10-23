using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DayOneCompletionTests : ForestSceneTestBase
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        const string Review = "Docs/Reviews/ClaudeContinuation_20261023";

        [UnityTest, Timeout(240000)]
        public IEnumerator SupperFinishesItsTimelineAndLoadingKeepsItComplete()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            yield return new EnterPlayMode();
            yield return CheckSupperTimeline();
            yield return new ExitPlayMode();
        }

        static IEnumerator CheckSupperTimeline()
        {
            Map02Mission mission = null;
            float deadline = Time.realtimeSinceStartup + 100;
            while (Time.realtimeSinceStartup < deadline)
            {
                mission = Object.FindFirstObjectByType<Map02Mission>();
                if (mission?.Ready == true && mission.Chapter != null) break;
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                yield return null;
            }
            Assert.NotNull(mission?.Chapter);
            var director = mission.Chapter;
            director.Cancel(); director.enabled = false;
            object Call(string method, params object[] args) => typeof(Map02ChapterDirector).GetMethod(method, Private).Invoke(director, args);
            var save = mission.Capture(); var state = new Map02ChapterState();
            foreach (var task in Map02ChapterDefinition.Load().tasks)
            {
                if (task.id == "N1_01") break;
                state.Complete(task.id);
            }
            state.dayOne.arrivalSeen = true; save.chapter = state; mission.Restore(save);
            try
            {
                Call("BeginScene", Map02ChapterDefinition.Load().Find("N1_01"), 0, 0);
                Assert.AreEqual("N1_01", director.CurrentScene?.id);
                float approach = (float)typeof(Map02ChapterDirector).GetField("dayApproachDuration", Private).GetValue(director);
                var lines = (Map02DialogueCatalog.Entry[])typeof(Map02ChapterDirector).GetField("lines", Private).GetValue(director);
                float elapsed = 0, lastStarted = -1;
                bool heardCrickets = false;
                // Deterministic game-clock driver: a separate test plays every WAV to completion in real time.
                // No E/escape input is used here, so the scene must finish through its automatic timing path.
                for (int step = 0; step < 800 && director.CurrentScene != null; step++)
                {
                    DialogueVoice.For(mission.gameObject).Stop();
                    if ((int)typeof(Map02ChapterDirector).GetField("lineIndex", Private).GetValue(director) == lines.Length - 1 && lastStarted < 0)
                        lastStarted = elapsed;
                    Call("TickScene", .25f); elapsed += .25f;
                    if (director.CurrentScene != null) Call("LateUpdate");
                    var crickets = (AudioSource)typeof(Map02ChapterDirector).GetField("mealCrickets", Private).GetValue(director);
                    if (crickets != null && crickets.isPlaying && crickets.volume > .01f) heardCrickets = true;
                    if (step % 40 == 0) yield return null;
                }
                Assert.True(director.State.Done("N1_01"), "The automatic supper must complete the objective.");
                Assert.That(elapsed - approach, Is.InRange(90f, 100f), "The staged supper keeps its approximately 90-second pacing.");
                Assert.GreaterOrEqual(elapsed - lastStarted, DialogueVoice.Length(lines.Last().id) + 3f, "The last spoken line is followed by at least three seconds of hold.");
                Assert.True(heardCrickets, "The final hold must actually play the cricket ambience.");
                var finalCrickets = (AudioSource)typeof(Map02ChapterDirector).GetField("mealCrickets", Private).GetValue(director);
                Assert.False(finalCrickets.isPlaying, "Completing the scene stops the ambience.");
                Assert.IsNull(director.CurrentScene); Assert.False(mission.InputBlocked);
                save = mission.Capture(); mission.Restore(save);
                Assert.True(director.State.Done("N1_01")); Assert.IsNull(director.CurrentScene);
                Directory.CreateDirectory(Review);
                Assert.False(finalCrickets.isPlaying, "Loading the completed supper must not restart its ambience.");
                WriteReviewFile(Review + "/supper-timeline.txt", $"approach={approach:F3}\nsupper={elapsed - approach:F3}\nlastLineAndHold={elapsed - lastStarted:F3}\ncricketsPlayed={heardCrickets}\ncompletedAfterLoad=True\n");
            }
            finally { director.Cancel(); director.enabled = true; }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SupperCricketsWaitForTheVoiceAndStopOnCancelOrLoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            yield return new EnterPlayMode();
            yield return CheckCrickets();
            yield return new ExitPlayMode();
        }

        static IEnumerator CheckCrickets()
        {
            Map02Mission mission = null;
            float deadline = Time.realtimeSinceStartup + 100;
            while (Time.realtimeSinceStartup < deadline)
            {
                mission = Object.FindFirstObjectByType<Map02Mission>();
                if (mission?.Ready == true && mission.Chapter != null) break;
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                yield return null;
            }
            Assert.NotNull(mission?.Chapter);
            var director = mission.Chapter; director.Cancel(); director.enabled = false;
            object Call(string method, params object[] args) => typeof(Map02ChapterDirector).GetMethod(method, Private).Invoke(director, args);
            void Set(string name, object value) => typeof(Map02ChapterDirector).GetField(name, Private).SetValue(director, value);
            AudioSource Crickets() => (AudioSource)typeof(Map02ChapterDirector).GetField("mealCrickets", Private).GetValue(director);
            var save = mission.Capture(); var state = new Map02ChapterState();
            foreach (var task in Map02ChapterDefinition.Load().tasks)
            {
                if (task.id == "N1_01") break;
                state.Complete(task.id);
            }
            state.dayOne.arrivalSeen = true; save.chapter = state; mission.Restore(save);
            try
            {
                Call("BeginScene", Map02ChapterDefinition.Load().Find("N1_01"), 0, 0);
                Call("TickMealCrickets");
                Assert.IsNull(Crickets(), "No ambience during the approach or earlier dialogue.");
                var lines = (Map02DialogueCatalog.Entry[])typeof(Map02ChapterDirector).GetField("lines", Private).GetValue(director);
                var last = lines.Last(); var voice = DialogueVoice.For(mission.gameObject);
                Set("lineIndex", lines.Length - 1); Set("mealBegan", true);
                Set("lineClock", DialogueVoice.Length(last.RecordingId) + .75f);
                voice.Play(last.RecordingId);
                Assert.True(voice.HasSpeech); Call("TickMealCrickets");
                Assert.IsNull(Crickets(), "A running final voice still takes priority, even if the scene clock is ahead.");
                voice.Stop(); Call("TickMealCrickets"); yield return null;
                var source = Crickets(); Assert.NotNull(source);
                Assert.True(source.isPlaying); Assert.That(source.volume, Is.InRange(.01f, .18f));
                Assert.True(source.loop); Assert.False(source.playOnAwake); Assert.AreEqual(0, source.spatialBlend);
                Assert.That(source.clip.length, Is.InRange(10f, 13f));
                var samples = new float[source.clip.samples * source.clip.channels];
                Assert.True(source.clip.GetData(samples, 0)); Assert.Greater(samples.Max(sample => Mathf.Abs(sample)), .01f, "The imported recording contains audible samples.");
                mission.SetPaused(true); Call("Update"); yield return null;
                Assert.False(source.isPlaying, "The ambience follows the game's pause state.");
                mission.SetPaused(false); Call("Update");
                Assert.True(source.isPlaying, "Unpausing resumes the same ambience.");
                director.Cancel(); Assert.False(source.isPlaying);
                Call("BeginScene", Map02ChapterDefinition.Load().Find("N1_01"), 0, 0);
                Set("lineIndex", lines.Length - 1); Set("mealBegan", true);
                Set("lineClock", DialogueVoice.Length(last.RecordingId) + .75f);
                voice.Stop(); Call("TickMealCrickets"); Assert.True(source.isPlaying);
                Assert.AreEqual(1, director.GetComponentsInChildren<AudioSource>().Count(audio => audio.clip == source.clip), "Replaying reuses one source.");
                mission.Restore(save); Assert.False(source.isPlaying, "Loading another state stops the old scene ambience.");
            }
            finally { mission.SetPaused(false); director.Cancel(); director.enabled = true; }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RadioClearsTheActualShirtAndFollowsTheSignaller()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            yield return new EnterPlayMode();
            yield return CheckRadio();
            yield return new ExitPlayMode();
        }

        static IEnumerator CheckRadio()
        {
            Map02Mission mission = null;
            float deadline = Time.realtimeSinceStartup + 100;
            while (Time.realtimeSinceStartup < deadline)
            {
                mission = Object.FindFirstObjectByType<Map02Mission>();
                if (mission?.Ready == true && mission.Chapter != null) break;
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                yield return null;
            }
            Assert.NotNull(mission?.Chapter);
            var director = mission.Chapter;
            director.Cancel();
            director.enabled = false;
            var actor = director.Actor("Hùng");
            var animator = actor.visual.GetComponentInChildren<Animator>();
            var radio = (Transform)typeof(Map02ChapterDirector).GetField("hungRadio", Private).GetValue(director);
            void UpdateRadio() => typeof(Map02ChapterDirector).GetMethod("UpdateHungRadio", Private).Invoke(director, null);
            void LoadBefore(string id)
            {
                var save = mission.Capture();
                var state = new Map02ChapterState();
                foreach (var task in Map02ChapterDefinition.Load().tasks)
                {
                    if (task.id == id) break;
                    state.Complete(task.id);
                }
                state.dayOne.arrivalSeen = true;
                save.chapter = state;
                mission.Restore(save);
            }
            Directory.CreateDirectory(Review);
            var rows = new List<string>();
            float worstGap = float.PositiveInfinity;
            int totalHits = 0;
            var origin = actor.transform.position;
            var rotation = actor.transform.rotation;
            try
            {
                LoadBefore("D1_03");
                // A clear part of the real map avoids a wall occluding the diagnostic camera.
                actor.StopAt(director.Sites["dayOneFamily"].position + Vector3.back * 6);
                actor.enabled = false;
                var art = Resources.Load<Map02DayOneArt>("Story/Map02DayOneArt");
                foreach (int yaw in new[] { 0, 90, 180, 270 })
                {
                    actor.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    foreach (var pose in new[] { ("InjuredIdle", .35f), ("InjuredWalk", .3f), ("InjuredWalk", .7f) })
                    {
                        actor.SamplePerformance(art.Find(pose.Item1), pose.Item2, true);
                        actor.GroundPerformance(actor.transform.position.y);
                        UpdateRadio();
                        // Let the render loop register newly enabled/moved primitive renderers.
                        yield return null;
                        yield return null;
                        UpdateRadio();
                        Assert.True(radio.gameObject.activeInHierarchy);
                        var facing = Map02ChapterDirector.BodyForward(animator, actor.transform);
                        var measured = ShirtClearance(actor.visual, radio, facing);
                        worstGap = Mathf.Min(worstGap, measured.gap);
                        totalHits += measured.hits;
                        rows.Add($"yaw={yaw} clip={pose.Item1} time={pose.Item2:F2} gap={measured.gap:F5} hits={measured.hits} radio={radio.position:F3} chest={animator.GetBoneTransform(HumanBodyBones.Chest).position:F3}");
                        if (yaw == 0 && pose.Item1 == "InjuredIdle")
                        {
                            Capture(radio.position, -facing * 2.2f, "radio-back");
                            Capture(radio.position + Vector3.up * .25f, -facing * 3f, "radio-overview");
                            Capture(radio.position, Vector3.Cross(Vector3.up, facing) * 1.8f - facing, "radio-side");
                            // Isolate the prop to distinguish an occluded mesh from an object that never renders.
                            var skins = actor.visual.GetComponentsInChildren<Renderer>();
                            var enabled = skins.Select(s => s.enabled).ToArray();
                            var usedLayers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Select(r => r.gameObject.layer).ToHashSet();
                            int isolatedLayer = Enumerable.Range(8, 24).Last(layer => !usedLayers.Contains(layer));
                            var propParts = radio.GetComponentsInChildren<Transform>();
                            var layers = propParts.Select(t => t.gameObject.layer).ToArray();
                            try
                            {
                                foreach (var skin in skins) skin.enabled = false;
                                foreach (var part in propParts) part.gameObject.layer = isolatedLayer;
                                yield return null;
                                yield return null;
                                var visible = Capture(radio.position, -facing * 2.2f, "radio-isolated", 1 << isolatedLayer);
                                radio.gameObject.SetActive(false);
                                yield return null;
                                var hidden = Capture(radio.position, -facing * 2.2f, "radio-hidden-reference", 1 << isolatedLayer);
                                int changed = visible.Where((pixel, i) =>
                                    Mathf.Abs(pixel.r - hidden[i].r) + Mathf.Abs(pixel.g - hidden[i].g) + Mathf.Abs(pixel.b - hidden[i].b) > 35).Count();
                                rows.Add("renderedPropPixels=" + changed);
                                Assert.Greater(changed, 500, "The active radio must actually render, not only have an enabled renderer.");
                            }
                            finally
                            {
                                radio.gameObject.SetActive(true);
                                for (int i = 0; i < propParts.Length; i++) propParts[i].gameObject.layer = layers[i];
                                for (int i = 0; i < skins.Length; i++) skins[i].enabled = enabled[i];
                            }
                        }
                    }
                }
                File.WriteAllLines(Review + "/radio-clearance.txt", rows);
                Assert.Greater(totalHits, 50, "The measurement must actually intersect the shirt, rather than pass with no geometry.");
                Assert.GreaterOrEqual(worstGap, .004f, "The front of the radio must clear the rendered shirt by at least 4 mm.");
                LoadBefore("N1_02"); UpdateRadio(); Assert.True(radio.gameObject.activeSelf, "Radio is present on the night patrol.");
                var snapshot = mission.Capture(); mission.Restore(snapshot); UpdateRadio();
                Assert.True(radio.gameObject.activeSelf, "Loading the patrol retains the radio.");
                LoadBefore("D2_01"); UpdateRadio(); Assert.False(radio.gameObject.activeSelf, "Day-one radio must not duplicate the later chapter prop.");
            }
            finally
            {
                File.WriteAllLines(Review + "/radio-clearance.txt", rows);
                actor.ReleasePerformance(); actor.enabled = true;
                actor.transform.SetPositionAndRotation(origin, rotation);
                director.Cancel(); director.enabled = true;
            }
        }

        // Independently raycast the evaluated shirt at a grid of points across the prop's front face.
        static (float gap, int hits) ShirtClearance(Transform model, Transform radio, Vector3 facing)
        {
            float gap = float.PositiveInfinity; int hits = 0;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh, false);
                mesh.vertices = mesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                mesh.RecalculateBounds();
                var probe = new GameObject("Test only: evaluated shirt collider");
                var collider = probe.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                try
                {
                    for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++)
                    {
                        var front = radio.position + radio.right * (x * .055f) + Vector3.up * (y * .055f) + facing * .055f;
                        if (!collider.Raycast(new Ray(front - facing, facing), out var hit, 1.8f)) continue;
                        gap = Mathf.Min(gap, Vector3.Dot(hit.point - front, facing)); hits++;
                    }
                }
                finally { Object.DestroyImmediate(probe); Object.DestroyImmediate(mesh); }
            }
            return (gap, hits);
        }

        static Color32[] Capture(Vector3 focus, Vector3 offset, string name, int isolatedMask = 0)
        {
            var source = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.enabled && c.targetTexture == null);
            var camera = new GameObject("Test only: radio review camera").AddComponent<Camera>();
            camera.CopyFrom(source); camera.enabled = false;
            if (isolatedMask != 0)
            {
                camera.cullingMask = isolatedMask;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.25f, .3f, .35f);
            }
            camera.transform.SetPositionAndRotation(focus + offset, Quaternion.LookRotation(-offset));
            camera.fieldOfView = 42;
            var target = new RenderTexture(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                WriteReviewFile(Review + "/" + name + ".png", image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
