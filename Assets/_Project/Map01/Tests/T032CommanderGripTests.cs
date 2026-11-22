using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class T032CommanderGripTests : ForestSceneTestBase
    {
        static readonly string[] RifleClips =
        {
            "Nam_Rifle_Idle", "Nam_Rifle_Walk_F", "Nam_Rifle_Run",
            "Nam_Rifle_AimIdle", "Nam_Rifle_Fire"
        };

        [UnityTest, Timeout(180000)]
        public IEnumerator C3CommanderUsesNamClipsAndReachesForegrip()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var commander = mission != null ? mission.GetComponent<Map01Quest>()?.CommanderEnemy : null;
            Assert.IsNotNull(commander, "The C3 scene must spawn the actual commander.");
            var animator = commander.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator != null && animator.isHuman, "The C3 commander requires a Humanoid.");
            var controller = animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.IsNotNull(controller, "The actual commander must receive the rifle override.");
            Assert.AreEqual("EnemyCommander_NamRifle", controller.name);

            var grip = animator.GetComponent<Map01EnemyGrip>();
            var rifle = animator.GetComponent<Map01Rifle>();
            Assert.IsTrue(grip != null && grip.Calibrated && rifle != null && rifle.weapon != null);
            Assert.AreSame(animator.GetBoneTransform(HumanBodyBones.RightHand), rifle.weapon.transform.parent);
            var output = System.Environment.GetEnvironmentVariable("SHADOWVALE_COMMANDER_QA_DIR");
            var found = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(found);
            var failures = new List<string>();
            using (var pose = new Map01CinematicPose(animator, skinGrounding: false))
            {
                foreach (string name in RifleClips)
                {
                    var clip = found.Select(pair => pair.Value).SingleOrDefault(value => value != null && value.name == name);
                    Assert.IsTrue(clip != null && clip.isHumanMotion, "Missing installed clip " + name);
                    bool patrol = name == "Nam_Rifle_Idle" || name == "Nam_Rifle_Walk_F";
                    int lastFrame = Mathf.CeilToInt(clip.length * 30f);
                    float worst = 0;
                    int worstFrame = 0;
                    for (int frame = 0; frame <= lastFrame; frame++)
                    {
                        pose.Sample(clip, frame / 30f);
                        grip.Apply(patrol);
                        Assert.IsFalse(float.IsNaN(grip.PalmError) || float.IsInfinity(grip.PalmError), name);
                        if (grip.PalmError <= worst) continue;
                        worst = grip.PalmError;
                        worstFrame = frame;
                    }
                    string measurement = string.Format(CultureInfo.InvariantCulture,
                        "{0}: foregrip offset {1:F2} cm, frame {2}/{3} at 30 fps", name, worst * 100f, worstFrame, lastFrame);
                    Debug.Log(measurement);
                    if (worst > .02f) failures.Add(measurement);
                    if (!string.IsNullOrEmpty(output))
                    {
                        pose.Sample(clip, worstFrame / 30f);
                        grip.Apply(patrol);
                        Capture(animator, mission.gameCamera, output, name);
                    }
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
            yield return new ExitPlayMode();
        }

        static void Capture(Animator actor, Camera source, string output, string clip)
        {
            if (source == null) return;
            var holder = new GameObject("T032 QA camera");
            var camera = holder.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.useOcclusionCulling = false;
            camera.fieldOfView = 36f;
            var previous = RenderTexture.active;
            var target = new RenderTexture(720, 540, 24);
            var image = new Texture2D(720, 540, TextureFormat.RGB24, false);
            var bakedViews = new List<(SkinnedMeshRenderer source, GameObject view, Mesh mesh)>();
            try
            {
                // Render the sampled skin and gun with transforms from the same test frame.
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled) continue;
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    var view = new GameObject("T032 baked skin");
                    view.layer = skin.gameObject.layer;
                    view.transform.SetParent(skin.transform, false);
                    view.AddComponent<MeshFilter>().sharedMesh = mesh;
                    view.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    bakedViews.Add((skin, view, mesh));
                    skin.enabled = false;
                }
                var chest = actor.GetBoneTransform(HumanBodyBones.Chest).position;
                var focus = chest + actor.transform.forward * .20f;
                foreach (var view in new[] { "side", "oblique" })
                {
                    var direction = view == "side" ? actor.transform.right :
                        (actor.transform.right + actor.transform.forward).normalized;
                    var eye = focus + direction * 1.85f + Vector3.up * .05f;
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 720, 540), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, clip + "_" + view + ".png"), image.EncodeToPNG());
                }
            }
            finally
            {
                foreach (var baked in bakedViews)
                {
                    baked.source.enabled = true;
                    baked.view.SetActive(false);
                    Object.Destroy(baked.view);
                    Object.Destroy(baked.mesh);
                }
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
                Object.Destroy(holder);
            }
        }
    }
}
