using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// T052 (Claude 27/11, director: the T049 stab "vung ra tay không tự nhiên còn xoắn"; the mocap retake T051 was rejected,
    /// "bản 49 đẹp hơn"). Same T049 body-mechanics thrust (same keys for shoulder, elbow, torso, timing), rebuilt with the
    /// wrist kept in a relaxed range: the hand search may roll the forearm only ±40° and pays steeply past 25° (T049 allowed
    /// ±75° and paid only past 50°, so the stab wrung the wrist to the end of its range to aim the blade). Variant B also
    /// lowers the blade toward the horizontal (T049 aimed it 38-48° up), which needs less roll still. Only knife_attack is
    /// rebuilt; the result goes to T052/NamMotionV3_T052_{A,B}.asset; the game asset is untouched until installed.
    /// </summary>
    public static partial class T049NamArmFix
    {
        public const string ThrustDir = "Assets/_Project/Art/Characters/NamAoBaBa_20261111/T052";
        public static string ThrustOutput(string v) => ThrustDir + "/NamMotionV3_T052_" + v + ".asset";

        public static void BuildThrustVariantsBatch()
        {
            var log = new StringBuilder(); int code = 0;
            try
            {
                Directory.CreateDirectory(ThrustDir);
                log.AppendLine("T049 (game): " + ThrustReport(AssetDatabase.LoadAssetAtPath<NamMotionData>(Source)));
                foreach (var (v, el) in new[] { ("A", (float[])null), ("B", new float[] { 32, 30, 14, 12, 22, 32, 32 }) })
                {
                    rollLimit = 40; rollKnee = 25; rollStrain = .6f; thrustElevation = el;
                    var built = RebuildThrust(log);
                    AssetDatabase.DeleteAsset(ThrustOutput(v)); AssetDatabase.CreateAsset(built, ThrustOutput(v)); AssetDatabase.SaveAssets();
                    log.AppendLine($"T052 {v}{(el == null ? " (wrist only)" : " (wrist + lower blade)")}: " + ThrustReport(built));
                }
            }
            catch (Exception e) { log.AppendLine("FAILED " + e); code = 1; }
            finally { rollLimit = ForearmRoll; rollKnee = 50; rollStrain = .2f; thrustElevation = null; }
            Directory.CreateDirectory(ThrustDir);
            File.WriteAllText(ThrustDir + "/T052_build_log.txt", log.ToString());
            Debug.Log("[T052]\n" + log);
            EditorApplication.Exit(code);
        }

        /// <summary>27/11, user: "Dùng bản B". Copies variant B over the game NamMotionV3 (only knife_attack differs: both come
        /// from the same game asset) and removes the T052 staging folder. Rebuilding is safe at any time: the thrust is made
        /// from knife_idle, never from the knife_attack already in the asset.</summary>
        public static void InstallBBatch()
        {
            var log = new StringBuilder(); int code = 0;
            try
            {
                var b = AssetDatabase.LoadAssetAtPath<NamMotionData>(ThrustOutput("B")) ?? throw new Exception("Build the variants first: " + ThrustOutput("B"));
                var game = AssetDatabase.LoadAssetAtPath<NamMotionData>(Source);
                if (b.frames != game.frames || b.clips.Length != game.clips.Length || b.bones.Length != game.bones.Length) throw new Exception("Variant B does not match the game asset layout");
                log.AppendLine("before install: " + ThrustReport(game));
                EditorUtility.CopySerialized(b, game); game.name = "NamMotionV3"; EditorUtility.SetDirty(game); AssetDatabase.SaveAssets();
                log.AppendLine("after install: " + ThrustReport(game));
                AssetDatabase.DeleteAsset(ThrustDir);
            }
            catch (Exception e) { log.AppendLine("FAILED " + e); code = 1; }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/T052_install.txt", log.ToString()); Debug.Log("[T052 install]\n" + log);
            EditorApplication.Exit(code);
        }

        static NamMotionData RebuildThrust(StringBuilder log)
        {
            var source = AssetDatabase.LoadAssetAtPath<NamMotionData>(Source);
            data = Object.Instantiate(source); data.name = "NamMotionV3_T052";
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            try
            {
                Setup(instance, log);
                BuildThrust(new StringBuilder());
                return data;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static void Setup(GameObject instance, StringBuilder log)
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = instance.GetComponentInChildren<Animator>();
            root = instance.transform;
            bone = animator.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            track = new Dictionary<string, int>();
            for (int i = 0; i < data.bones.Length; i++) track[data.bones[i].name] = i;
            twist = animator.GetComponent<ForearmTwist>() ?? animator.GetComponentInChildren<ForearmTwist>();
            var rest = BindRotations(instance);
            side = Mathf.Sign(rest.pos["LeftShoulder"].x - rest.pos["RightShoulder"].x);
            restLocal.Clear();
            foreach (var kv in rest.rot)
                if (bone.TryGetValue(kv.Key, out var bt) && bt.parent != null && rest.rot.TryGetValue(bt.parent.name, out var pr)) restLocal[bt] = Quaternion.Inverse(pr) * kv.Value;
            grip = MirroredGrip(rest, data.Find("knife_idle").start + 35, new StringBuilder());
            SetupArms(rest, new StringBuilder());
            SetupProps(new StringBuilder());
        }

        /// <summary>knife_attack of the given data, on a fresh Nam: worst forearm roll and wrist bend (human terms), how far the
        /// knife hand and elbow swing out sideways from the shoulder, furthest reach, peak hand speed.</summary>
        static string ThrustReport(NamMotionData d)
        {
            var keep = data; data = d;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            try
            {
                Setup(instance, new StringBuilder());
                var c = d.Find("knife_attack");
                float roll = 0, flex = 0, rad = 0, handOut = float.MinValue, elbowOut = float.MinValue, reach = float.MinValue, speed = 0; Vector3 prev = Vector3.zero;
                Vector3 outward = root.right * -side; // toward his right, the knife side
                for (int f = c.start; f <= c.end; f++)
                {
                    Pose(f);
                    var w = Wrist(armR, false);
                    if (Mathf.Abs(w.z) > Mathf.Abs(roll)) roll = w.z;
                    if (Mathf.Abs(w.x) > Mathf.Abs(flex)) flex = w.x;
                    if (Mathf.Abs(w.y) > Mathf.Abs(rad)) rad = w.y;
                    Vector3 sh = armR.up.position;
                    handOut = Mathf.Max(handOut, Vector3.Dot(armR.hand.position - sh, outward));
                    elbowOut = Mathf.Max(elbowOut, Vector3.Dot(armR.lo.position - sh, outward));
                    reach = Mathf.Max(reach, Vector3.Dot(armR.hand.position - sh, root.forward));
                    var h = root.InverseTransformPoint(armR.hand.position);
                    if (f > c.start) speed = Mathf.Max(speed, (h - prev).magnitude * 60); prev = h;
                }
                return $"forearm roll worst {roll:F0}°, wrist flex {flex:F0}° radial {rad:F0}°; hand out to the side {handOut * 100:F0} cm, elbow out {elbowOut * 100:F0} cm (from the shoulder); reach {reach * 100:F0} cm; peak hand speed {speed:F1} m/s";
            }
            finally { Object.DestroyImmediate(instance); data = keep; }
        }
    }
}
