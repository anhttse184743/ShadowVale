using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The magazine in Nam's reloads (ReloadMagazineCarrier on Player.prefab, set up by StoryAnimationSetup): it
    /// leaves the rifle in his hand, is hidden while the old one is put away and a fresh one drawn, and is brought
    /// back to the well. Checked on the three reload clips sampled through his avatar with the game's rifle.
    /// </summary>
    public sealed class ReloadMagazineTests
    {
        private const string Player = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string Rifle = "Assets/_Project/Prefabs/Items/W_AK47.prefab";
        private const string Grip = "Assets/_Project/ScriptableObjects/GameSettings/WeaponGripConfig.asset";
        private const string Clips = "Assets/_Project/Art/Characters/Animations/Nam/";

        private static readonly (string state, string clip)[] Reloads = {
            ("ReloadEmpty_Rifle", "Nam_Rifle_Reload"), ("Reload_Rifle", "Nam_Rifle_ReloadTactical"), ("ReloadCrouch_Rifle", "Nam_Crouch_Reload"),
        };

        [TestCase(false)]
        [TestCase(true)]
        public void MagazineLeavesTheRifleRidesTheHandAndGoesBack(bool m16)
        {
            GameObject nam = null;
            try
            {
                nam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in nam.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var actor = nam.GetComponentInChildren<Animator>();
                var carrier = actor.GetComponent<ReloadMagazineCarrier>();
                Assert.IsNotNull(carrier, "Player.prefab's Model carries the reload magazine carrier.");
                Assert.AreEqual(Reloads.Length, carrier.Takes.Length, "One entry per reload state.");
                Transform anchor = actor.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponAnchor");
                // a plain copy, as PlayerCombat makes at runtime
                GameObject rifle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Rifle), anchor);
                AssetDatabase.LoadAssetAtPath<WeaponGripConfig>(Grip).Apply(rifle.transform, WeaponKind.Rifle);
                var weapon = rifle.GetComponent<Weapon>();
                if (m16) Map01RifleVisual.Apply(weapon, Map01M16.Id);
                carrier.Bind(weapon.ReloadModel);
                Transform magazine = carrier.Magazine;
                Assert.IsNotNull(magazine, "The equipped rifle has separate magazine pieces to carry.");
                int pieces = magazine.childCount;
                Assert.Greater(pieces, 0);
                Transform hand = actor.GetBoneTransform(HumanBodyBones.LeftHand);

                AnimationMode.StartAnimationMode();
                void Sample(AnimationClip clip, float t)
                {
                    carrier.Evaluate(null, 0);       // back in the rifle before the pose changes under it
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(actor.gameObject, clip, t * clip.length);
                    AnimationMode.EndSampling();
                }
                foreach (var (state, clipName) in Reloads)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(Clips + clipName + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                    ReloadMagazineCarrier.Take take = carrier.Takes.First(t => t.state == state);
                    Assert.Less(take.leaves, take.putAway); Assert.Less(take.putAway, take.fresh); Assert.Less(take.fresh, take.seated);
                    foreach (float t in new[] { 0f, take.leaves - .01f, take.seated + .005f, 1f })
                    {
                        Sample(clip, t); carrier.Evaluate(state, t);
                        Assert.Less(carrier.OutOfWell, 1e-4f, $"{state} at {t}: in the rifle");
                        Assert.IsTrue(carrier.Shown, $"{state} at {t}: visible");
                    }
                    float farthest = 0;
                    for (float t = take.leaves; t < take.seated - take.seatEase; t += .01f)
                    {
                        Sample(clip, t); carrier.Evaluate(state, t);
                        Assert.Less(Vector3.Distance(hand.InverseTransformPoint(magazine.position), take.inHandPosition), 1e-3f, $"{state} at {t}: fixed in the hand");
                        Assert.AreEqual(t < take.putAway || t >= take.fresh, carrier.Shown, $"{state} at {t}: shown");
                        farthest = Mathf.Max(farthest, carrier.OutOfWell);
                    }
                    Assert.Greater(farthest, .15f, $"{state}: the magazine clearly leaves the rifle");
                    // what the hand brings back at the moment of seating, before the two-frame ease
                    Sample(clip, take.seated - .0005f);
                    Vector3 well = magazine.position;
                    Assert.Less(Vector3.Distance(hand.TransformPoint(take.inHandPosition), well), .03f, $"{state}: the hand brings the magazine back to the well");
                    // a reload cut short (a hit, another weapon): no reload state, straight back in the rifle
                    float mid = (take.leaves + take.putAway) * .5f;
                    if (m16 && state == "Reload_Rifle")
                    {
                        Sample(clip, mid); carrier.Evaluate(state, mid);
                        Photograph(nam, actor, "Tools/M16Reports/m16-magazine-detached.png");
                    }
                    Sample(clip, mid); carrier.Evaluate(state, mid); carrier.Evaluate(null, 0);
                    Assert.Less(carrier.OutOfWell, 1e-4f, $"{state}: an interrupted reload re-seats the magazine");
                    Assert.IsTrue(carrier.Shown);
                    Assert.AreEqual(pieces, magazine.childCount, "One magazine, never a second copy.");
                }
                AnimationMode.StopAnimationMode();
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                if (nam != null) Object.DestroyImmediate(nam);
            }
        }

        private static void Photograph(GameObject actorObject, Animator actor, string path)
        {
            var preview = new PreviewRenderUtility();
            var copy = Object.Instantiate(actorObject);
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            copy.GetComponentInChildren<Animator>().enabled = false;
            preview.AddSingleGO(copy);
            var hand = actor.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 focus = Vector3.Lerp(hand.position, actor.GetBoneTransform(HumanBodyBones.Chest).position, .35f);
            var camera = preview.camera;
            camera.transform.position = focus + actor.transform.forward * 1.4f - actor.transform.right * 1.1f + Vector3.up * .15f;
            camera.transform.LookAt(focus);
            camera.fieldOfView = 35; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.13f,.12f);
            preview.lights[0].intensity = 1.5f; preview.lights[0].transform.rotation = Quaternion.Euler(35,30,0);
            preview.lights[1].intensity = 1; preview.ambientColor = new Color(.5f,.5f,.5f);
            var old = RenderTexture.active;
            var target = RenderTexture.GetTemporary(960,720,24);
            var pixels = new Texture2D(960,720,TextureFormat.RGB24,false);
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,960,720),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = old;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(pixels); preview.Cleanup();
                ShaderUtil.allowAsyncCompilation = async;
            }
        }
    }
}
