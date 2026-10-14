using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class RescueRigCompatibilityTests
    {
        private const string PlayerModel = "Assets/_Project/Art/Characters/Player/Player.fbx";

        [Test]
        public void BushAvatarIncludesEveryBoneOfTheCurrentPlayerRig()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel);
            var gameplay = model.GetComponent<Animator>().avatar;
            var bush = Resources.Load<Avatar>("Rescue/Story/Nam_Bush_Avatar");
            Assert.IsTrue(bush != null && bush.isValid && bush.isHuman);
            var actual = bush.humanDescription.skeleton.Select(b => b.name).ToArray();
            foreach (var bone in gameplay.humanDescription.skeleton)
                CollectionAssert.Contains(actual, bone.name, "Bush handoff must include the current rig's twist bones.");
        }

        [Test]
        public void RefinedHugHandsUseTheCurrentPlayerBoneBindings()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel);
            foreach (var side in new[] { "Left", "Right" })
            {
                var original = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(r => r.name == "Player_" + side + "Hand");
                var refined = Resources.Load<Mesh>("Rescue/Story/Nam_Hug_" + side + "Hand");
                Assert.NotNull(refined);
                Assert.AreEqual(original.bones.Length, refined.bindposes.Length,
                    "A stale hand mesh must not bind to the wrong joints after a player-model update.");
                var expected = original.sharedMesh.bindposes;
                var actual = refined.bindposes;
                for (int i = 0; i < expected.Length; i++)
                    for (int element = 0; element < 16; element++)
                        Assert.AreEqual(expected[i][element], actual[i][element], 0.000001f,
                            side + " hand bind pose " + i);
                foreach (var weight in refined.boneWeights)
                {
                    Assert.Less(weight.boneIndex0, original.bones.Length);
                    Assert.Less(weight.boneIndex1, original.bones.Length);
                    Assert.Less(weight.boneIndex2, original.bones.Length);
                    Assert.Less(weight.boneIndex3, original.bones.Length);
                }
            }
        }
    }
}
