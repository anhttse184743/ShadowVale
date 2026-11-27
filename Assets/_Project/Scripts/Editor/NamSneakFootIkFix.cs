using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// 27/11: holding the K-54 in Map 2 and crouching, Nam's legs folded up and he hung 45-70 cm above the ground.
    /// The plain crouch state (Sneak: Nam_Crouch_Idle_V21 / Nam_Crouch_Walk_New) was the only crouch with Foot IK on,
    /// and the generated V21 idle carries no usable foot goals, so the IK pulled both feet up toward the hips. Map 1
    /// hid it (T005fApprovedPose and NamMotionRefresh write the legs over it); Map 2 has no NamMotionRefresh for the
    /// K-54. The other crouches (Sneak_Rifle, Sneak_Knife, T007_Sneak_Rifle_Move) already run without Foot IK.
    /// Idempotent: touches only that one flag.
    /// </summary>
    public static class NamSneakFootIkFix
    {
        const string Controller = "Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";

        [MenuItem("ShadowVale/Nam/Tat Foot IK cua trang thai Sneak")]
        public static void Run()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            var sneak = controller.layers[0].stateMachine.states.Select(s => s.state).Single(s => s.name == "Sneak");
            string report = $"Sneak footIK was {sneak.iKOnFeet}";
            if (sneak.iKOnFeet)
            {
                sneak.iKOnFeet = false;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
            report += $", now {sneak.iKOnFeet}";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/NamSneakFootIkFix.txt", report);
            Debug.Log("[NamSneakFootIkFix] " + report);
        }

        public static void RunBatch() { Run(); EditorApplication.Exit(0); }
    }
}
