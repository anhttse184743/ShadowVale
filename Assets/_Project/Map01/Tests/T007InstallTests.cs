#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public class T007InstallTests
    {
        const string Controller = "Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";
        const string Dir = "Assets/_Project/Art/Characters/NamAoBaBa_20261111/Animations/T007_ApprovedV18A";
        const string Move = "T007_Sneak_Rifle_Move", Tree = "T007_Rifle_8_huong";
        static readonly string[] Names = { "Idle", "Fwd", "FwdL", "FwdR", "L", "R", "Back", "BackL", "BackR" };
        static AnimatorController Load() => AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        static void Install()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("T007InstallApproved")).Single(t => t != null);
            type.GetMethod("Install").Invoke(null, null);
        }
        [Test] public void ApprovedClipsAndControllerAreIdempotent()
        {
            var before = Load(); Assert.NotNull(before);
            int count = before.layers[0].stateMachine.states.Length;
            var others = before.layers[0].stateMachine.states.Select(x => x.state).Where(s => s.name != "Sneak_Rifle" && s.name != Move)
                .ToDictionary(s => s.name, s => s.motion ? AssetDatabase.GetAssetPath(s.motion) + ":" + s.motion.name : "<none>");
            Install(); Install();
            var controller = Load(); var machine = controller.layers[0].stateMachine;
            Assert.AreEqual(count, machine.states.Length, "Running an already installed controller twice must preserve the state count.");
            foreach (var item in others)
            {
                var state = machine.states.Select(x => x.state).Single(s => s.name == item.Key);
                Assert.AreEqual(item.Value, state.motion ? AssetDatabase.GetAssetPath(state.motion) + ":" + state.motion.name : "<none>", item.Key);
            }
            Assert.AreEqual(1, machine.states.Count(s => s.state.name == Move));
            var trees = AssetDatabase.LoadAllAssetsAtPath(Controller).OfType<BlendTree>().Where(t => t.name == Tree).ToArray();
            Assert.AreEqual(1, trees.Length);
            Assert.AreEqual(BlendTreeType.FreeformDirectional2D, trees[0].blendType);
            Assert.AreEqual("MoveX", trees[0].blendParameter); Assert.AreEqual("MoveZ", trees[0].blendParameterY);
            Assert.AreEqual(8, trees[0].children.Length);
            var idle = machine.states.Select(s => s.state).Single(s => s.name == "Sneak_Rifle");
            var move = machine.states.Select(s => s.state).Single(s => s.name == Move);
            Assert.AreSame(trees[0], move.motion);
            Assert.AreEqual(1, idle.transitions.Count(t => t.destinationState == move));
            Assert.AreEqual(1, move.transitions.Count(t => t.destinationState == idle));
            for (int i = 0; i < Names.Length; i++)
            {
                string path = Dir + "/Nam_Rifle_Crouch_Aim_" + Names[i] + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.NotNull(importer, path); Assert.AreEqual(ModelImporterAnimationType.Human, importer.animationType);
                Assert.AreEqual(ModelImporterAvatarSetup.CopyFromOther, importer.avatarSetup);
                Assert.AreEqual(1, importer.clipAnimations.Length); Assert.IsTrue(importer.clipAnimations[0].loopTime); Assert.IsTrue(importer.clipAnimations[0].lockRootPositionXZ);
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
                Assert.AreEqual(1, clips.Length, path); Assert.IsTrue(clips[0].isHumanMotion, path);
                if (i == 0) Assert.AreSame(clips[0], idle.motion);
                else Assert.AreSame(clips[0], trees[0].children[i-1].motion);
            }
        }
    }
}
#endif
