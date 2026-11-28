using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// T047 (director 26/11 "Đưa vào game"): Hùng V119 and the enemy commander V119 re-weighted by Claude (thighs no
    /// longer drag the belt, at most 4 bones per vertex, Hùng's left forearm gets the same three twist bones as the right,
    /// the commander's ankles 3.5 cm lower). Exported with Codex's original settings (FBX_SCALE_ALL).
    /// DryRun: imports the old and new FBX side by side in a temp folder and compares every transform of the default pose.
    /// Run (idempotent; the runner copies the FBX in first): refreshes only the commander's foot entries of the stored
    /// Humanoid skeleton, and gives Hùng's ForearmTwist a left limb mirroring its right one.
    /// </summary>
    public static class T047TrongSoInstall
    {
        const string HungFbx = "Assets/_Project/Art/Characters/NPCs/Hung_Meshy/Hung_V119_model_va_clip.fbx";
        const string ChiHuyFbx = "Assets/_Project/Art/Characters/ChiHuyDich_V119/ChiHuyDich_V119.fbx";
        static readonly string[] ChiHuyFeet = { "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase" };
        static readonly string[] HungPrefabs =
        {
            "Assets/_Project/Art/Map02DayOne/hung.prefab",
            "Assets/_Project/Map01/Resources/Characters/HungVisual.prefab",
            "Assets/_Project/Prefabs/Player/PlayerHung.prefab",
        };
        const string Tmp = "Assets/_T047Tmp";

        static void Finish(List<string> log, bool ok, string file)
        {
            log.Add(ok ? "T047 OK" : "T047 LOI");
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/" + file, log);
            Debug.Log(string.Join("\n", log));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Args after -t047: oldHung newHung oldChiHuy newChiHuy (absolute paths).</summary>
        public static void DryRun()
        {
            var a = System.Environment.GetCommandLineArgs(); int k = System.Array.IndexOf(a, "-t047");
            var log = new List<string>(); bool ok = true;
            Directory.CreateDirectory(Tmp);
            string[] names = { "old_hung", "new_hung", "old_chihuy", "new_chihuy" };
            for (int i = 0; i < 4; i++) File.Copy(a[k + 1 + i], $"{Tmp}/{names[i]}.fbx", true);
            AssetDatabase.Refresh();
            try
            {
                ok &= Compare("Hung", $"{Tmp}/old_hung.fbx", $"{Tmp}/new_hung.fbx", new string[0], log);
                ok &= Compare("ChiHuy", $"{Tmp}/old_chihuy.fbx", $"{Tmp}/new_chihuy.fbx", ChiHuyFeet, log);
            }
            finally { AssetDatabase.DeleteAsset(Tmp); }
            Finish(log, ok, "T047DryRun.txt");
        }

        static bool Compare(string label, string oldPath, string newPath, string[] expected, List<string> log)
        {
            var o = AssetDatabase.LoadAssetAtPath<GameObject>(oldPath); var n = AssetDatabase.LoadAssetAtPath<GameObject>(newPath);
            var ot = o.GetComponentsInChildren<Transform>(true).Where(t => t != o.transform).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var nt = n.GetComponentsInChildren<Transform>(true).Where(t => t != n.transform).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            float worstP = 0, worstR = 0, worstS = 0; string wp = "", wr = "";
            foreach (var kv in ot)
            {
                if (!nt.TryGetValue(kv.Key, out var t)) { log.Add($"{label}: MAT {kv.Key}"); return false; }
                if (expected.Contains(kv.Key)) continue;
                float dp = (kv.Value.localPosition - t.localPosition).magnitude, dr = Quaternion.Angle(kv.Value.localRotation, t.localRotation);
                float ds = (kv.Value.localScale - t.localScale).magnitude;
                if (dp > worstP) { worstP = dp; wp = kv.Key; }
                if (dr > worstR) { worstR = dr; wr = kv.Key; }
                worstS = Mathf.Max(worstS, ds);
            }
            var added = nt.Keys.Except(ot.Keys).ToArray();
            foreach (var e in expected)
                log.Add($"{label}: {e} pos {ot[e].localPosition:F4} -> {nt[e].localPosition:F4}, rot {Quaternion.Angle(ot[e].localRotation, nt[e].localRotation):F2} deg");
            var oc = AssetDatabase.LoadAllAssetsAtPath(oldPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            var nc = AssetDatabase.LoadAllAssetsAtPath(newPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            log.Add($"{label}: other transforms max pos {worstP * 1000:F3} mm ({wp}), rot {worstR:F3} deg ({wr}), scale {worstS:F5}; added [{string.Join(", ", added)}]");
            log.Add($"{label}: clips old [{string.Join(", ", oc.Select(c => $"{c.name} {c.length:F3}s"))}] new [{string.Join(", ", nc.Select(c => $"{c.name} {c.length:F3}s"))}]");
            var om = o.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.sharedMesh.vertexCount);
            var nm = n.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.sharedMesh.vertexCount);
            log.Add($"{label}: skinned vertices {om} -> {nm}");
            bool clipsOk = oc.Length == nc.Length && oc.Zip(nc, (x, y) => x.name == y.name && Mathf.Abs(x.length - y.length) < .002f).All(b => b);
            return worstP < .0005f && worstR < .05f && worstS < .001f && clipsOk;
        }

        [MenuItem("ShadowVale/T047/Cai trong so Hung + chi huy dich")]
        public static void Run()
        {
            var log = new List<string>();
            bool ok = Avatar(HungFbx, null, log) & Avatar(ChiHuyFbx, ChiHuyFeet, log);
            int added = 0;
            foreach (string p in HungPrefabs) ok &= AddLeftTwist(p, log, ref added);
            ok &= added > 0;
            AssetDatabase.SaveAssets();
            Finish(log, ok, "T047TrongSoInstall.txt");
        }

        /// <summary>Refreshes only the listed skeleton entries from the model's rest pose, then checks the avatar.</summary>
        static bool Avatar(string path, string[] refresh, List<string> log)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (imp == null || model == null) { log.Add("THIEU " + path); return false; }
            if (refresh != null)
            {
                var byName = model.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var hd = imp.humanDescription; var sk = hd.skeleton; int changed = 0;
                for (int i = 0; i < sk.Length; i++)
                {
                    if (!refresh.Contains(sk[i].name) || !byName.TryGetValue(sk[i].name, out var t)) continue;
                    if ((sk[i].position - t.localPosition).sqrMagnitude < 1e-12f && Quaternion.Angle(sk[i].rotation, t.localRotation) < .001f) continue;
                    log.Add($"{Path.GetFileName(path)}: skeleton {sk[i].name} pos {sk[i].position:F4} -> {t.localPosition:F4}");
                    sk[i].position = t.localPosition; sk[i].rotation = t.localRotation; changed++;
                }
                if (changed > 0) { hd.skeleton = sk; imp.humanDescription = hd; imp.SaveAndReimport(); }
            }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            bool good = avatar != null && avatar.isValid && avatar.isHuman;
            log.Add($"{Path.GetFileName(path)}: avatar valid={good}");
            return good;
        }

        static bool AddLeftTwist(string path, List<string> log, ref int added)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool dirty = false;
                foreach (var drv in root.GetComponentsInChildren<ForearmTwist>(true))
                {
                    var so = new SerializedObject(drv); var limbs = so.FindProperty("limbs");
                    SerializedProperty right = null; bool hasLeft = false;
                    for (int i = 0; i < limbs.arraySize; i++)
                    {
                        var el = limbs.GetArrayElementAtIndex(i);
                        var h = el.FindPropertyRelative("hand").objectReferenceValue as Transform;
                        var b0 = el.FindPropertyRelative("bones").arraySize > 0 ? el.FindPropertyRelative("bones").GetArrayElementAtIndex(0).objectReferenceValue as Transform : null;
                        if (h == null || b0 == null || !b0.name.Contains("ForeArmTwist_V")) continue;   // only Hùng's V1xx twist bones
                        if (h.name == "LeftHand") hasLeft = true;
                        if (h.name == "RightHand") right = el;
                    }
                    if (right == null) { log.Add($"{path}: {drv.name} skipped (no Hùng right limb)"); continue; }
                    if (hasLeft) { log.Add($"{path}: left limb already there"); added++; continue; }
                    var rHand = (Transform)right.FindPropertyRelative("hand").objectReferenceValue;
                    var rig = rHand.GetComponentInParent<Animator>(true);
                    var all = (rig != null ? rig.transform : drv.transform).GetComponentsInChildren<Transform>(true);
                    var lHand = all.FirstOrDefault(t => t.name == "LeftHand");
                    var rBones = right.FindPropertyRelative("bones"); var rShares = right.FindPropertyRelative("shares");
                    var lBones = new List<Transform>();
                    for (int b = 0; b < rBones.arraySize; b++)
                    {
                        var rb = (Transform)rBones.GetArrayElementAtIndex(b).objectReferenceValue;
                        var lb = rb == null ? null : all.FirstOrDefault(t => t.name == rb.name.Replace("Right", "Left"));
                        if (lb == null) { log.Add($"{path}: missing left twist for {(rb ? rb.name : "null")}"); return false; }
                        lBones.Add(lb);
                    }
                    if (lHand == null) { log.Add(path + ": no LeftHand"); return false; }
                    limbs.arraySize++;
                    var ne = limbs.GetArrayElementAtIndex(limbs.arraySize - 1);
                    ne.FindPropertyRelative("hand").objectReferenceValue = lHand;
                    ne.FindPropertyRelative("handRest").quaternionValue = lHand.localRotation;
                    ne.FindPropertyRelative("centre").floatValue = -right.FindPropertyRelative("centre").floatValue;
                    var bp = ne.FindPropertyRelative("bones"); var rp = ne.FindPropertyRelative("bonesRest"); var sp = ne.FindPropertyRelative("shares");
                    bp.arraySize = rp.arraySize = sp.arraySize = lBones.Count;
                    for (int b = 0; b < lBones.Count; b++)
                    {
                        bp.GetArrayElementAtIndex(b).objectReferenceValue = lBones[b];
                        rp.GetArrayElementAtIndex(b).quaternionValue = lBones[b].localRotation;
                        sp.GetArrayElementAtIndex(b).floatValue = rShares.GetArrayElementAtIndex(b).floatValue;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo(); dirty = true; added++;
                    log.Add($"{path}: left limb added ({string.Join(", ", lBones.Select(t => $"{t.name} rest {t.localRotation.eulerAngles:F1}"))}), hand rest {lHand.localRotation.eulerAngles:F1}");
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
