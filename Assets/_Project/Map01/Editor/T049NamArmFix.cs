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
    /// T049 (Claude 26/11, approved step 2 of the Nam arm diagnosis; director: Claude does it himself). Builds a SEPARATE copy of
    /// NamMotionV3 with the free arm fixed in the one-handed K-54 and knife takes: the elbow brought in to the body, the idle arm
    /// relaxed, the palm turned to the thigh and the fingers closed in the grip of Nam's own approved knife hand, mirrored. The knife
    /// arm swings its elbow in about the shoulder-wrist line so the blade stays where it was. The game's asset is not touched; the
    /// copy is swapped in only after the director approves.
    /// </summary>
    public static partial class T049NamArmFix
    {
        // Installed 26/11/2026 (T049T050Install): the game asset now IS this build's output. To rebuild, first put the pre-T049
        // original back here from chuyen_dong\GiaoViec_Codex\T049_Nam_TayK54Dao_v3\claude\sao_luu_truoc_khi_cai (or git e64ccb2a),
        // otherwise the fix is applied twice.
        public const string Source = "Assets/_Project/Map01/Resources/Characters/NamMotionV3.asset";
        public const string OutDir = "Assets/_Project/Art/Characters/NamAoBaBa_20261111/T049";
        public const string Output = OutDir + "/NamMotionV3_T049.asset";
        const string Prefab = "Assets/_Project/Prefabs/Player/Player.prefab";
        const float ElbowTarget = .23f, IdleBend = 20f, TorsoClear = .20f, HandTorsoClear = .17f, HandThighClear = .10f;
        static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        enum Kind { Idle, Move, Crouch, Jump }
        sealed class Range { public string id; public Kind kind; public bool knife; }
        static readonly Range[] Ranges =
        {
            // Run and crouch are rebuilt whole from body mechanics (T049NamArmFix.Mechanics.cs), not patched here.
            new Range { id = "pistol_carry", kind = Kind.Idle }, new Range { id = "pistol_walk", kind = Kind.Move },
            new Range { id = "jump_pistol", kind = Kind.Jump },
            new Range { id = "knife_idle", kind = Kind.Idle, knife = true }, new Range { id = "knife_walk", kind = Kind.Move, knife = true },
            new Range { id = "knife_draw", kind = Kind.Idle, knife = true }, new Range { id = "knife_holster", kind = Kind.Idle, knife = true },
            new Range { id = "jump_knife", kind = Kind.Jump, knife = true },
        };

        static NamMotionData data;
        static Dictionary<string, Transform> bone;
        static Dictionary<string, int> track;
        static Transform root;
        static ForearmTwist twist;
        static float side; // world x of the left side (-1 or +1)
        static Dictionary<string, Quaternion> grip; // left finger world rotations relative to the left hand, mirrored from the knife hand

        [MenuItem("ShadowVale/T049/Build Nam arm fix (separate asset)")]
        public static void Build()
        {
            var log = new StringBuilder();
            var source = AssetDatabase.LoadAssetAtPath<NamMotionData>(Source);
            if (source == null) throw new Exception("Missing " + Source);
            data = Object.Instantiate(source); data.name = "NamMotionV3_T049";
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponentInChildren<Animator>();
                root = instance.transform;
                bone = animator.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                track = new Dictionary<string, int>();
                for (int i = 0; i < data.bones.Length; i++) track[data.bones[i].name] = i;
                twist = animator.GetComponent<ForearmTwist>() ?? animator.GetComponentInChildren<ForearmTwist>();
                if (twist == null) throw new Exception("No ForearmTwist on Nam");

                // Rest (bind) pose, for the mirror between the hands.
                var rest = BindRotations(instance);
                side = Mathf.Sign(rest.pos["LeftShoulder"].x - rest.pos["RightShoulder"].x);
                restLocal.Clear();
                foreach (var kv in rest.rot)
                    if (bone.TryGetValue(kv.Key, out var bt) && bt.parent != null && rest.rot.TryGetValue(bt.parent.name, out var pr)) restLocal[bt] = Quaternion.Inverse(pr) * kv.Value;
                grip = MirroredGrip(rest, data.Find("knife_idle").start + 35, log);
                SetupArms(rest, log);
                SetupProps(log);

                foreach (var r in Ranges)
                {
                    var clip = data.Find(r.id);
                    if (clip == null) throw new Exception("Missing clip " + r.id);
                    float theta = SolveAdduction(clip, r, out string why);
                    var before = Measure(clip, r, 0f, false);
                    for (int f = clip.start; f <= clip.end; f++) { Fix(f, r, theta, true); Store(f); }
                    var after = MeasureStored(clip, r);
                    log.AppendLine($"{r.id} {clip.start}-{clip.end}: adduction {theta:F1} deg ({why}); before {before}; after {after}");
                }
                Directory.CreateDirectory(OutDir);
                BuildCrouch("pistol_crouch", false, log); BuildCrouch("knife_crouch", true, log);
                BuildRun("pistol_run", false, log); BuildRun("knife_run", true, log);
                BuildThrust(log);
                BuildDrawHolster(animator, log);
                AssetDatabase.CreateAsset(data, Output);
                AssetDatabase.SaveAssets();
                File.WriteAllText(OutDir + "/T049_build_log.txt", log.ToString());
                Debug.Log("[T049] built " + Output + "\n" + log);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        sealed class Rest { public Dictionary<string, Quaternion> rot = new(); public Dictionary<string, Vector3> pos = new(); }
        static Rest BindRotations(GameObject go)
        {
            var rest = new Rest();
            var matrices = new Dictionary<Transform, Matrix4x4>();
            foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                for (int i = 0; i < skin.bones.Length; i++)
                    if (skin.bones[i] != null && !matrices.ContainsKey(skin.bones[i]))
                        matrices[skin.bones[i]] = skin.transform.localToWorldMatrix * skin.sharedMesh.bindposes[i].inverse;
            foreach (var x in matrices) { rest.rot[x.Key.name] = x.Value.rotation; rest.pos[x.Key.name] = x.Value.GetColumn(3); }
            return rest;
        }

        static void Pose(int frame)
        {
            for (int i = 0; i < data.bones.Length; i++)
                if (bone.TryGetValue(data.bones[i].name, out var t)) data.bones[i].poses[frame - 1].Apply(t);
        }
        static void Store(int frame)
        {
            foreach (var n in ArmBones())
                if (track.TryGetValue(n, out int i) && bone.TryGetValue(n, out var t))
                {
                    var p = data.bones[i].poses[frame - 1]; p.rotation = t.localRotation; data.bones[i].poses[frame - 1] = p;
                }
        }
        static IEnumerable<string> ArmBones()
        {
            foreach (var s in new[] { "Left", "Right" })
            {
                yield return s + "Arm"; yield return s + "ForeArm"; yield return s + "ForeArmTwist1"; yield return s + "ForeArmTwist2"; yield return s + "Hand";
                foreach (var f in Fingers) for (int k = 1; k <= 3; k++) yield return s + "Hand" + f + k;
            }
        }

        // Finger rotations of the knife hand relative to it, mirrored onto the left hand: L = (T r r0^T T^T) l0 with T = H0L^T S H0R.
        static Dictionary<string, Quaternion> MirroredGrip(Rest rest, int frame, StringBuilder log)
        {
            Pose(frame);
            var S = Matrix4x4.Scale(new Vector3(-1, 1, 1));
            var H0R = Matrix4x4.Rotate(rest.rot["RightHand"]); var H0L = Matrix4x4.Rotate(rest.rot["LeftHand"]);
            var T = H0L.transpose * S * H0R;
            var HR = Matrix4x4.Rotate(bone["RightHand"].rotation);
            var result = new Dictionary<string, Quaternion>();
            foreach (var f in Fingers) for (int k = 1; k <= 3; k++)
            {
                string rn = "RightHand" + f + k, ln = "LeftHand" + f + k;
                if (!bone.ContainsKey(rn) || !bone.ContainsKey(ln) || !rest.rot.ContainsKey(rn) || !rest.rot.ContainsKey(ln)) continue;
                var r = HR.transpose * Matrix4x4.Rotate(bone[rn].rotation);
                var r0 = H0R.transpose * Matrix4x4.Rotate(rest.rot[rn]);
                var l0 = H0L.transpose * Matrix4x4.Rotate(rest.rot[ln]);
                var delta = T * r * r0.transpose * T.transpose;
                result[ln] = (delta * l0).rotation;
            }
            log.AppendLine($"grip mirrored from knife_idle frame {frame}: {result.Count} finger bones");
            return result;
        }

        static float SolveAdduction(NamMotionData.Clip clip, Range r, out string why)
        {
            if (r.kind == Kind.Idle) { why = "idle hang pose"; return 0; }
            float sign = TrySign(clip, r);
            float best = 0; why = "already in";
            if (Measure(clip, r, 0, true).elbow <= ElbowTarget) return 0;
            for (float a = 1; a <= 45; a += 1)
            {
                var m = Measure(clip, r, a * sign, true);
                if (m.clearFail) { why = "stopped at body clearance"; break; }
                best = a * sign;
                if (m.elbow <= ElbowTarget) { why = "elbow target"; break; }
                why = "max 45";
            }
            return best;
        }
        static float TrySign(NamMotionData.Clip clip, Range r)
            => Measure(clip, r, 5, true).elbow < Measure(clip, r, -5, true).elbow ? 1 : -1;

        struct M { public float elbow, curl, bend, handTorso, handThigh, elbowTorso; public bool clearFail; public float knifeElbow;
            public override string ToString() => $"elbowL max {elbow * 100:F0} cm, bendL mean {bend:F0}, curlL {curl:F0}, handL-torso min {handTorso * 100:F0} cm, handL-thigh min {handThigh * 100:F0} cm, elbowL-torso min {elbowTorso * 100:F0} cm, elbowR max {knifeElbow * 100:F0} cm"; }
        static M Measure(NamMotionData.Clip clip, Range r, float theta, bool fix)
        {
            var m = new M { handTorso = 9, handThigh = 9, elbowTorso = 9 }; float bendSum = 0, curlSum = 0; int n = 0;
            for (int f = clip.start; f <= clip.end; f += fix ? 2 : 1)
            {
                if (fix) Fix(f, r, theta, false); else Pose(f);
                Accumulate(ref m, ref bendSum, ref curlSum); n++;
            }
            m.bend = bendSum / n; m.curl = curlSum / n;
            m.clearFail = m.handTorso < HandTorsoClear || m.handThigh < HandThighClear || m.elbowTorso < TorsoClear;
            return m;
        }
        static M MeasureStored(NamMotionData.Clip clip, Range r)
        {
            var m = new M { handTorso = 9, handThigh = 9, elbowTorso = 9 }; float bendSum = 0, curlSum = 0; int n = 0;
            for (int f = clip.start; f <= clip.end; f++) { Pose(f); Accumulate(ref m, ref bendSum, ref curlSum); n++; }
            m.bend = bendSum / n; m.curl = curlSum / n; return m;
        }
        static void Accumulate(ref M m, ref float bendSum, ref float curlSum)
        {
            Vector3 chest = bone["Spine2"].position, right = root.right;
            Vector3 hips = bone["Hips"].position, neck = bone["Neck"].position;
            var e = bone["LeftForeArm"].position; var h = bone["LeftHand"].position;
            m.elbow = Mathf.Max(m.elbow, Vector3.Dot(e - chest, right) * side);
            m.knifeElbow = Mathf.Max(m.knifeElbow, Vector3.Dot(bone["RightForeArm"].position - chest, right) * -side);
            m.elbowTorso = Mathf.Min(m.elbowTorso, Seg(e, hips, neck));
            m.handTorso = Mathf.Min(m.handTorso, Seg(h, hips, neck));
            m.handThigh = Mathf.Min(m.handThigh, Seg(h, bone["LeftUpLeg"].position, bone["LeftLeg"].position));
            bendSum += Vector3.Angle(e - bone["LeftArm"].position, h - e);
            float c = 0; int k = 0;
            foreach (var f in new[] { "Index", "Middle", "Ring", "Pinky" }) for (int j = 1; j <= 2; j++)
                if (bone.TryGetValue("LeftHand" + f + j, out var t)) { c += Quaternion.Angle(Quaternion.identity, t.localRotation * Quaternion.Inverse(RestLocal(t))); k++; }
            curlSum += k > 0 ? c / k : 0;
        }
        static readonly Dictionary<Transform, Quaternion> restLocal = new();
        static Quaternion RestLocal(Transform t) => restLocal.TryGetValue(t, out var q) ? q : t.localRotation;
        static float Seg(Vector3 p, Vector3 a, Vector3 b) { var ab = b - a; float s = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude); return Vector3.Distance(p, a + ab * s); }

        static void Fix(int frame, Range r, float theta, bool final)
        {
            Pose(frame);
            Transform up = bone["LeftArm"], lo = bone["LeftForeArm"], hand = bone["LeftHand"];
            if (r.kind == Kind.Idle) HangIdle();
            // Elbow in: the whole arm turns about the body's forward axis through the shoulder joint.
            else if (Mathf.Abs(theta) > .01f) up.rotation = Quaternion.AngleAxis(theta, root.forward) * up.rotation;
            // Closed hand: Nam's own knife grip, mirrored.
            foreach (var kv in grip) if (bone.TryGetValue(kv.Key, out var t)) t.rotation = hand.rotation * kv.Value;
            // Idle: palm to the thigh, turning the hand about the forearm.
            if (r.kind == Kind.Idle)
            {
                Vector3 axis = (hand.position - lo.position).normalized;
                Vector3 palm = PalmNormal();
                Vector3 want = Vector3.ProjectOnPlane(-root.right * side, axis).normalized;
                Vector3 have = Vector3.ProjectOnPlane(palm, axis).normalized;
                if (want.sqrMagnitude > .5f && have.sqrMagnitude > .5f)
                {
                    float roll = Vector3.SignedAngle(have, want, axis);
                    var saved = grip.Keys.Where(bone.ContainsKey).ToDictionary(n => n, n => Quaternion.Inverse(hand.rotation) * bone[n].rotation);
                    hand.rotation = Quaternion.AngleAxis(roll, axis) * hand.rotation;
                    foreach (var kv in saved) bone[kv.Key].rotation = hand.rotation * kv.Value;
                }
            }
            // Knife arm: elbow in about the shoulder-wrist line, the hand and blade held where they were.
            if (r.knife) SwingKnifeElbow();
            twist.Apply();
        }
        // Relaxed hang for the idle takes: upper arm down, a little out and forward; the elbow bends 18 degrees forward as a hinge
        // (the forearm keeps the rest twist relative to the upper arm, so the elbow crease faces forward and nothing wrings).
        const float HangOut = .14f, HangForward = .06f, HangBend = 18f;
        static void HangIdle()
        {
            Transform up = bone["LeftArm"], lo = bone["LeftForeArm"], hand = bone["LeftHand"];
            Quaternion handLocal = hand.localRotation;
            Vector3 outward = root.right * side;
            Vector3 d = (-root.up + outward * HangOut + root.forward * HangForward).normalized;
            // Hinge of the elbow in the upper arm's rest frame.
            Quaternion upRest = RestLocal(up), loRest = RestLocal(lo);
            Vector3 foreRestDir = loRest * Vector3.up; // forearm direction in the upper arm's frame at rest
            Vector3 hingeLocal = Vector3.Cross(Vector3.up, foreRestDir);
            if (hingeLocal.sqrMagnitude < 1e-6f) hingeLocal = Vector3.right;
            hingeLocal.Normalize();
            float restBend = Vector3.Angle(Vector3.up, foreRestDir);
            // World hinge: horizontal, across the body, signed so the forearm bends forward.
            Vector3 h = Vector3.Cross(d, root.forward).normalized;
            if (Vector3.Dot(Quaternion.AngleAxis(10, h) * d, root.forward) < Vector3.Dot(d, root.forward)) h = -h;
            // Upper arm world rotation with local Y on d and the local hinge on h.
            Vector3 hx = Vector3.ProjectOnPlane(h, d).normalized;
            var target = Matrix4x4.identity; var local = Matrix4x4.identity;
            Vector3 z1 = Vector3.Cross(hx, d); target.SetColumn(0, hx); target.SetColumn(1, d); target.SetColumn(2, z1);
            Vector3 lz = Vector3.Cross(hingeLocal, Vector3.up); local.SetColumn(0, hingeLocal); local.SetColumn(1, Vector3.up); local.SetColumn(2, lz);
            up.rotation = (target * local.transpose).rotation;
            // Forearm: rest relation to the upper arm, then bend about the hinge to HangBend.
            lo.localRotation = loRest;
            float bendNow = Vector3.Angle(lo.position - up.position, hand.position - lo.position);
            lo.rotation = Quaternion.AngleAxis(HangBend - restBend, h) * lo.rotation;
            if (Vector3.Angle(lo.position - up.position, hand.position - lo.position) > HangBend + 1f) lo.rotation = Quaternion.AngleAxis(-2f * (HangBend - restBend), h) * lo.rotation;
            hand.localRotation = handLocal;
        }

        static Vector3 BladeAxis(GameObject prefab)
        {
            var pts = new List<Vector3>(); var rootInv = prefab.transform.worldToLocalMatrix;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue; var b = mf.sharedMesh.bounds; var m = rootInv * mf.transform.localToWorldMatrix;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    pts.Add(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            }
            if (pts.Count == 0) return Vector3.up;
            Vector3 min = pts[0], max = pts[0]; foreach (var q in pts) { min = Vector3.Min(min, q); max = Vector3.Max(max, q); }
            Vector3 size = max - min; int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var dir = Vector3.zero; dir[axis] = Mathf.Abs(max[axis]) >= Mathf.Abs(min[axis]) ? 1 : -1; return dir;
        }
        static float RollOf(Transform t)
        {
            var q = Quaternion.Inverse(RestLocal(t)) * t.localRotation;
            return Mathf.DeltaAngle(0, 2f * Mathf.Atan2(q.y, q.w) * Mathf.Rad2Deg);
        }
        static void StoreList(int frame, IEnumerable<string> names)
        {
            foreach (var n in names)
                if (track.TryGetValue(n, out int i) && bone.TryGetValue(n, out var t))
                {
                    var p = data.bones[i].poses[frame - 1]; p.rotation = t.localRotation; data.bones[i].poses[frame - 1] = p;
                }
        }

        // ---- K-54 draw / holster (step 3): two takes appended after frame 3208, from Mixamo "Pistol Aim To Holster Idle" ----
        const string HolsterSource = "D:/DO_AN_GAME/chuyen_dong/Mixamo/tai_2026-11-26/Pistol_AimToHolster.fbx";
        static readonly string[] UpperBones = { "Spine", "Spine1", "Spine2", "Neck", "LeftShoulder", "RightShoulder", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "LeftHand", "RightHand" };
        static AnimationClip HumanoidClip(string source, string fileName)
        {
            string dir = OutDir + "/Mixamo", dst = dir + "/" + fileName;
            Directory.CreateDirectory(dir);
            if (!File.Exists(dst)) File.Copy(source, dst);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(dst);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(dst).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        }
        static void BuildDrawHolster(Animator animator, StringBuilder log)
        {
            var clip = HumanoidClip(HolsterSource, "Pistol_AimToHolster.fbx");
            var go = animator.gameObject; Vector3 modelPos = go.transform.localPosition; Quaternion modelRot = go.transform.localRotation;
            var carry = data.Find("pistol_carry"); var knifeIdle = data.Find("knife_idle");
            int samples = Mathf.Max(2, Mathf.RoundToInt(clip.length * 60));
            // The holster moment: the gun hand closest to the right hip.
            int insert = 0; float best = float.MaxValue;
            for (int s = 0; s <= samples; s++)
            {
                clip.SampleAnimation(go, s / 60f); go.transform.SetLocalPositionAndRotation(modelPos, modelRot);
                Vector3 holster = bone["RightUpLeg"].position + root.right * -side * .10f;
                float d = Vector3.Distance(bone["RightHand"].position, holster);
                if (d < best) { best = d; insert = s; }
            }
            // The clip ends with the hand resting on the holstered gun: the insert is the first frame the hand gets there.
            for (int s = 0; s <= samples; s++)
            {
                clip.SampleAnimation(go, s / 60f); go.transform.SetLocalPositionAndRotation(modelPos, modelRot);
                Vector3 holster = bone["RightUpLeg"].position + root.right * -side * .10f;
                if (Vector3.Distance(bone["RightHand"].position, holster) <= best + .02f) { insert = s; break; }
            }
            int s0 = Mathf.Max(0, insert - 36), s1 = Mathf.Min(samples, insert + 15), n = s1 - s0 + 1;
            log.AppendLine($"holster: Mixamo {samples} samples, insert at {insert} ({best * 100:F0} cm from hip), take {s0}-{s1} ({n} frames)");

            // Poses used as anchors.
            Pose(carry.start); var carryUpper = UpperBones.ToDictionary(b => b, b => bone[b].localRotation);
            var gunFingers = AllFingers("Right").ToDictionary(b => b, b => bone[b].localRotation);
            Pose(knifeIdle.start); var emptyFingers = AllFingers("Right").ToDictionary(b => b, b => bone[b].localRotation);
            var holsterFrames = new List<Map01PistolAnimationData.Pose[]>(); var drawFrames = new List<Map01PistolAnimationData.Pose[]>();
            var sampled = new List<Dictionary<string, Quaternion>>();
            for (int s = s0; s <= s1; s++)
            {
                clip.SampleAnimation(go, s / 60f); go.transform.SetLocalPositionAndRotation(modelPos, modelRot);
                sampled.Add(UpperBones.ToDictionary(b => b, b => bone[b].localRotation));
            }
            const int Blend = 12;
            for (int k = 0; k < n; k++)
            {
                // Holster: carry -> Mixamo over the first frames; the hand is empty after the insert.
                holsterFrames.Add(Frame(carry.start + k % carry.Frames, sampled[k], carryUpper, Mathf.SmoothStep(0, 1, k / (float)Blend), s0 + k <= insert ? gunFingers : emptyFingers));
                // Draw: the same take backwards, ending in the carry.
                int r = n - 1 - k;
                drawFrames.Add(Frame(carry.start + k % carry.Frames, sampled[r], carryUpper, Mathf.SmoothStep(0, 1, (n - 1 - k) / (float)Blend), s0 + r <= insert ? gunFingers : emptyFingers));
            }
            int holsterStart = data.frames + 1, drawStart = holsterStart + n;
            Append(holsterFrames.Concat(drawFrames).ToList(), carry.start);
            int insertFrame = holsterStart + (insert - s0), grabFrame = drawStart + (n - 1 - (insert - s0));
            var clips = data.clips.ToList();
            clips.Add(new NamMotionData.Clip { id = "pistol_holster", label = "T049 K54 / cat sung|event=" + insertFrame, prop = "Pistol", start = holsterStart, end = holsterStart + n - 1 });
            clips.Add(new NamMotionData.Clip { id = "pistol_draw", label = "T049 K54 / rut sung|event=" + grabFrame, prop = "Pistol", start = drawStart, end = drawStart + n - 1 });
            data.clips = clips.ToArray();
            log.AppendLine($"pistol_holster {holsterStart}-{holsterStart + n - 1} (gun leaves the hand at {insertFrame}); pistol_draw {drawStart}-{drawStart + n - 1} (gun in hand from {grabFrame}); frames now {data.frames}");
        }
        static IEnumerable<string> AllFingers(string s) { foreach (var f in Fingers) for (int k = 1; k <= 3; k++) if (bone.ContainsKey(s + "Hand" + f + k)) yield return s + "Hand" + f + k; }
        static Map01PistolAnimationData.Pose[] Frame(int legFrame, Dictionary<string, Quaternion> taken, Dictionary<string, Quaternion> carryUpper, float w, Dictionary<string, Quaternion> rightFingers)
        {
            Pose(legFrame);
            foreach (var b in UpperBones) bone[b].localRotation = b.StartsWith("Left") ? carryUpper[b] : Quaternion.Slerp(carryUpper[b], taken[b], w);
            foreach (var kv in rightFingers) bone[kv.Key].localRotation = kv.Value;
            foreach (var kv in grip) if (bone.TryGetValue(kv.Key, out var t)) t.rotation = bone["LeftHand"].rotation * kv.Value;
            twist.Apply();
            var poses = new Map01PistolAnimationData.Pose[data.bones.Length];
            for (int i = 0; i < data.bones.Length; i++)
                poses[i] = bone.TryGetValue(data.bones[i].name, out var t) ? Map01PistolAnimationData.Pose.Read(t) : data.bones[i].poses[legFrame - 1];
            return poses;
        }
        static void Append(List<Map01PistolAnimationData.Pose[]> frames, int propFrame)
        {
            int add = frames.Count;
            for (int i = 0; i < data.bones.Length; i++)
            {
                var list = data.bones[i].poses.ToList(); foreach (var f in frames) list.Add(f[i]); data.bones[i].poses = list.ToArray();
            }
            foreach (var p in data.props)
            {
                p.root.poses = Extend(p.root.poses, p.root.poses[propFrame - 1], add);
                foreach (var part in p.parts)
                {
                    part.track.poses = Extend(part.track.poses, part.track.poses[propFrame - 1], add);
                    if (part.visible != null && part.visible.Length > 0) part.visible = part.visible.Concat(Enumerable.Repeat(part.visible[Mathf.Clamp(propFrame - 1, 0, part.visible.Length - 1)], add)).ToArray();
                }
            }
            data.frames += add;
        }
        static T[] Extend<T>(T[] a, T fill, int add) => a.Concat(Enumerable.Repeat(fill, add)).ToArray();

        static Vector3 PalmNormal()
        {
            var h = bone["LeftHand"]; Vector3 i1 = bone["LeftHandIndex1"].position, p1 = bone["LeftHandPinky1"].position;
            Vector3 tips = (bone["LeftHandIndex3"].position + bone["LeftHandMiddle3"].position) * .5f, knuckles = (i1 + bone["LeftHandMiddle1"].position) * .5f;
            Vector3 axis = (knuckles - h.position).normalized;
            return Vector3.ProjectOnPlane(tips - knuckles, axis).normalized; // the curled fingers point into the palm
        }
        static void SwingKnifeElbow()
        {
            Transform up = bone["RightArm"], lo = bone["RightForeArm"], hand = bone["RightHand"];
            Vector3 chest = bone["Spine2"].position;
            float lateral = Vector3.Dot(lo.position - chest, root.right) * -side;
            if (lateral <= ElbowTarget) return;
            Quaternion handWorld = hand.rotation; Vector3 shoulder = up.position, axis = (hand.position - shoulder).normalized;
            float best = 0, bestScore = float.MaxValue;
            for (float a = -60; a <= 60; a += .5f)
            {
                var q = Quaternion.AngleAxis(a, axis);
                Vector3 e = shoulder + q * (lo.position - shoulder);
                if (Seg(e, bone["Hips"].position, bone["Neck"].position) < TorsoClear) continue;
                float lat = Vector3.Dot(e - chest, root.right) * -side;
                float score = Mathf.Max(lat, ElbowTarget) + .0005f * Mathf.Abs(a);
                if (score < bestScore) { bestScore = score; best = a; }
            }
            if (Mathf.Abs(best) < .01f) return;
            up.rotation = Quaternion.AngleAxis(best, axis) * up.rotation;
            hand.rotation = handWorld;
        }

        public static void BuildBatch()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
