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

        [Test]
        public void MagazineLeavesTheRifleRidesTheHandAndGoesBack()
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
                carrier.Bind(rifle.transform);
                Transform magazine = carrier.Magazine;
                Assert.IsNotNull(magazine, "The game's AK has separate magazine pieces to carry.");
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
    }
}
