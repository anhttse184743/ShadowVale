using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// T049 round 2 (Claude 26/11, director: "research how the human body moves and simulate it", on the real-human skeleton of
    /// the T047 audit). The knife thrust, the crouched K-54/knife holds and the running arms are rebuilt from body mechanics instead
    /// of patched clips: the elbow is a pure hinge, forearm roll goes to the twist bones, the wrist stays inside human range
    /// (elliptic limit), and each hand target comes from a pose described in the body's own frame. Notes and sources:
    /// chuyen_dong\GiaoViec_Codex\T049_Nam_TayK54Dao_v3\claude\NGHIEN_CUU_CO_SINH_HOC.md.
    /// </summary>
    public static partial class T049NamArmFix
    {
        // Comfortable share (~85 %) of normal human range (AAOS).
        const float WristFlex = 65, WristExt = 55, WristRadial = 18, WristUlnar = 28, ForearmRoll = 75, ElbowMinBend = 12, ElbowMaxBend = 145;

        sealed class Arm
        {
            public string s; public Transform clav, up, lo, hand; public float L1, L2;
            public Vector3 palmH, thumbH;   // hand-local unit vectors: palm-facing normal and thumb side, both across the hand's length (local Y)
            public Vector3 hingeUp;         // elbow hinge in the upper arm's local frame; + = flexion
        }
        static Arm armL, armR;
        static readonly StringBuilder mlog = new();

        // ---------------------------------------------------------------- set-up
        static void SetupArms(Rest rest, StringBuilder log)
        {
            int gripFrame = data.Find("knife_idle").start + 35;
            Pose(data.Find("knife_idle").start); chestFwdLocal = Quaternion.Inverse(bone["Spine2"].rotation) * root.forward;
            armR = MakeArm("Right", rest, gripFrame, log);
            armL = MakeArm("Left", rest, gripFrame, log);
        }
        static Arm MakeArm(string s, Rest rest, int gripFrame, StringBuilder log)
        {
            var a = new Arm { s = s, clav = bone[s + "Shoulder"], up = bone[s + "Arm"], lo = bone[s + "ForeArm"], hand = bone[s + "Hand"] };
            if (a.lo.parent != a.up || a.hand.parent != a.lo) throw new Exception($"{s}: arm chain is not Arm>ForeArm>Hand ({a.lo.parent.name}, {a.hand.parent.name})");
            Pose(gripFrame);
            if (s == "Left") foreach (var kv in grip) if (bone.TryGetValue(kv.Key, out var t)) t.rotation = a.hand.rotation * kv.Value;
            a.L1 = Vector3.Distance(a.up.position, a.lo.position); a.L2 = Vector3.Distance(a.lo.position, a.hand.position);
            Vector3 Y = a.hand.up;
            Vector3 knuckles = (bone[s + "HandIndex1"].position + bone[s + "HandMiddle1"].position) * .5f;
            Vector3 tips = (bone[s + "HandIndex3"].position + bone[s + "HandMiddle3"].position) * .5f;
            Vector3 palm = Vector3.ProjectOnPlane(tips - knuckles, Y).normalized;           // curled fingers point into the palm
            Vector3 thumb = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(bone[s + "HandThumb1"].position - a.hand.position, Y), palm).normalized;
            a.palmH = Quaternion.Inverse(a.hand.rotation) * palm; a.thumbH = Quaternion.Inverse(a.hand.rotation) * thumb;

            // Elbow hinge, anatomical: at the bind pose the forearm is mid-prone, so flexion carries it toward the thumb side.
            Vector3 aB = (rest.pos[s + "Hand"] - rest.pos[s + "ForeArm"]).normalized;
            Vector3 thumbB = Vector3.ProjectOnPlane(rest.rot[s + "Hand"] * a.thumbH, aB).normalized;
            Vector3 hingeAnat = Quaternion.Inverse(rest.rot[s + "Arm"]) * Vector3.Cross(aB, thumbB).normalized;
            // The hinge the approved clips actually use (same skeleton): axis of the forearm's turn from rest wherever it is bent.
            Vector3 sum = Vector3.zero; int n = 0; var axes = new List<Vector3>();
            foreach (var id in new[] { "knife_idle", "pistol_walk", "pistol_aim", "knife_walk", "pistol_carry" })
            {
                var c = data.Find(id); if (c == null) continue;
                for (int f = c.start; f <= c.end; f += 5)
                {
                    Pose(f);
                    (a.lo.localRotation * Quaternion.Inverse(RestLocal(a.lo))).ToAngleAxis(out float ang, out Vector3 ax);
                    if (ang > 180) { ang = 360 - ang; ax = -ax; }
                    if (ang < 25) continue;
                    if (Vector3.Dot(ax, hingeAnat) < 0) ax = -ax;
                    axes.Add(ax); sum += ax; n++;
                }
            }
            Vector3 hingeData = n > 0 ? sum.normalized : hingeAnat;
            float spread = n > 0 ? axes.Average(x => Vector3.Angle(x, hingeData)) : 0;
            float gap = Vector3.Angle(hingeAnat, hingeData);
            a.hingeUp = n > 5 && spread < 12 ? hingeData : hingeAnat;
            log.AppendLine($"{s} arm: upper {a.L1 * 100:F1} cm, fore {a.L2 * 100:F1} cm; hinge anatomical vs clips {gap:F1} deg (clip spread {spread:F1} over {n}) -> using {(a.hingeUp == hingeData && n > 5 && spread < 12 ? "clips" : "anatomical")}; palm {a.palmH}, thumb {a.thumbH}");
            return a;
        }

        // ---------------------------------------------------------------- joints
        static float Bend(Arm a) => Vector3.Angle(a.lo.position - a.up.position, a.hand.position - a.lo.position);
        static void SetBend(Arm a, float deg)
        {
            Quaternion rest = RestLocal(a.lo);
            float Eval(float x) { a.lo.localRotation = Quaternion.AngleAxis(x, a.hingeUp) * rest; return Bend(a); }
            float x0 = 0, f0 = Eval(x0) - deg, x1 = deg, f1 = Eval(x1) - deg;
            for (int i = 0; i < 8 && Mathf.Abs(f1) > .05f; i++)
            {
                float d = f1 - f0; if (Mathf.Abs(d) < 1e-4f) break;
                float x2 = Mathf.Clamp(x1 - f1 * (x1 - x0) / d, -30, 175); x0 = x1; f0 = f1; x1 = x2; f1 = Eval(x1) - deg;
            }
            Eval(x1);
        }
        /// <summary>Two-bone IK: hand bone origin to P, elbow in the plane of the pole direction. Returns the elbow bend.</summary>
        static float SolveArm(Arm a, Vector3 P, Vector3 pole)
        {
            Vector3 S = a.up.position, d = P - S; float L1 = a.L1, L2 = a.L2;
            float dMax = Mathf.Sqrt(L1 * L1 + L2 * L2 + 2 * L1 * L2 * Mathf.Cos(ElbowMinBend * Mathf.Deg2Rad));
            float dMin = Mathf.Sqrt(L1 * L1 + L2 * L2 + 2 * L1 * L2 * Mathf.Cos(ElbowMaxBend * Mathf.Deg2Rad));
            float dist = Mathf.Clamp(d.magnitude, dMin, dMax); Vector3 dn = d.normalized; P = S + dn * dist;
            float bend = 180 - Mathf.Acos(Mathf.Clamp((L1 * L1 + L2 * L2 - dist * dist) / (2 * L1 * L2), -1, 1)) * Mathf.Rad2Deg;
            float alpha = Mathf.Acos(Mathf.Clamp((L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist), -1, 1));
            Vector3 p = Vector3.ProjectOnPlane(pole, dn); if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(Vector3.down, dn); p.Normalize();
            Vector3 E = S + L1 * (Mathf.Cos(alpha) * dn + Mathf.Sin(alpha) * p);
            SetBend(a, bend);
            Vector3 u1 = a.lo.position - S, u2 = a.hand.position - S;
            a.up.rotation = Quaternion.LookRotation(P - S, E - S) * Quaternion.Inverse(Quaternion.LookRotation(u2, u1)) * a.up.rotation;
            return bend;
        }
        /// <summary>Wrist in human terms: flexion(+)/extension(-), radial(+)/ulnar(-) deviation, forearm roll from the bind (mid-prone).</summary>
        static Vector3 Wrist(Arm a, bool clamp)
        {
            Quaternion rest = RestLocal(a.hand);
            Quaternion r = Quaternion.Inverse(rest) * a.hand.localRotation;
            float roll = Mathf.DeltaAngle(0, 2f * Mathf.Atan2(r.y, r.w) * Mathf.Rad2Deg);
            Quaternion sw = Quaternion.AngleAxis(-roll, Vector3.up) * r;
            sw.ToAngleAxis(out float ang, out Vector3 ax); if (ang > 180) ang -= 360;
            Vector3 v = ax * ang;
            Vector3 fAx = Vector3.Cross(Vector3.up, a.palmH).normalized, rAx = Vector3.Cross(Vector3.up, a.thumbH).normalized;
            float flex = Vector3.Dot(v, fAx), rad = Vector3.Dot(v, rAx);
            if (!clamp) return new Vector3(flex, rad, roll);
            float lf = flex >= 0 ? WristFlex : WristExt, lr = rad >= 0 ? WristRadial : WristUlnar;
            float e = (flex / lf) * (flex / lf) + (rad / lr) * (rad / lr);
            if (e > 1) { float k = 1 / Mathf.Sqrt(e); flex *= k; rad *= k; }
            roll = Mathf.Clamp(roll, -ForearmRoll, ForearmRoll);
            Vector3 v2 = fAx * flex + rAx * rad;
            var swing = v2.sqrMagnitude > 1e-8f ? Quaternion.AngleAxis(v2.magnitude, v2.normalized) : Quaternion.identity;
            a.hand.localRotation = rest * Quaternion.AngleAxis(roll, Vector3.up) * swing;
            return new Vector3(flex, rad, roll);
        }
        /// <summary>Points the hand-held axis (blade, barrel, or the fist's own length) along dir, thumb toward the hint: searches
        /// forearm roll and wrist flexion/deviation inside human range (coarse to fine) for the least miss plus a little joint strain.
        /// Returns the leftover angle between the axis and dir.</summary>
        static float OrientHand(Arm a, Vector3 axisH, Vector3 dir, Vector3 thumbHint, Vector3 upH = default, float upWeight = .15f)
        {
            if (dir.sqrMagnitude < 1e-6f) dir = (a.hand.position - a.lo.position).normalized; // empty fist: wrist straight on the forearm
            dir.Normalize();
            Quaternion rest = RestLocal(a.hand), parent = a.lo.rotation;
            Vector3 fAx = Vector3.Cross(Vector3.up, a.palmH).normalized, rAx = Vector3.Cross(Vector3.up, a.thumbH).normalized;
            Vector3 hint = Vector3.ProjectOnPlane(thumbHint, dir);
            if (upH.sqrMagnitude < 1e-6f) upH = a.thumbH;
            Quaternion Local(float roll, float flex, float dev)
            {
                Vector3 v = fAx * flex + rAx * dev;
                return rest * Quaternion.AngleAxis(roll, Vector3.up) * (v.sqrMagnitude > 1e-8f ? Quaternion.AngleAxis(v.magnitude, v.normalized) : Quaternion.identity);
            }
            float Cost(float roll, float flex, float dev)
            {
                if (Mathf.Abs(roll) > ForearmRoll) return float.MaxValue;
                float lf = flex >= 0 ? WristFlex : WristExt, lr = dev >= 0 ? WristRadial : WristUlnar;
                if ((flex / lf) * (flex / lf) + (dev / lr) * (dev / lr) > 1) return float.MaxValue;
                Quaternion w = parent * Local(roll, flex, dev);
                float c = Vector3.Angle(w * axisH, dir);
                Vector3 th = Vector3.ProjectOnPlane(w * upH, dir);
                if (hint.sqrMagnitude > 1e-6f && th.sqrMagnitude > 1e-6f) c += upWeight * Vector3.Angle(th, hint);
                // Strain: cheap near neutral, steep past a relaxed wrist (25 deg bend, 15 deg tilt) and past half the forearm roll.
                return c + .06f * (Mathf.Abs(flex) + Mathf.Abs(dev)) + .03f * Mathf.Abs(roll)
                         + .35f * (Mathf.Max(0, Mathf.Abs(flex) - 25) + Mathf.Max(0, Mathf.Abs(dev) - 15)) + .2f * Mathf.Max(0, Mathf.Abs(roll) - 50);
            }
            float bR = 0, bF = 0, bD = 0, bC = Cost(0, 0, 0);
            for (float r = -75; r <= 75.1f; r += 15) for (float fl = -55; fl <= 65.1f; fl += 10) for (float dv = -28; dv <= 18.1f; dv += 6)
            { float c = Cost(r, fl, dv); if (c < bC) { bC = c; bR = r; bF = fl; bD = dv; } }
            foreach (var (sr, sf, sd) in new[] { (7.5f, 5f, 3f), (3f, 2f, 1.5f), (1f, 1f, .5f) })
            {
                float r0 = bR, f0 = bF, d0 = bD;
                for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) for (int k = -1; k <= 1; k++)
                { float c = Cost(r0 + i * sr, f0 + j * sf, d0 + k * sd); if (c < bC) { bC = c; bR = r0 + i * sr; bF = f0 + j * sf; bD = d0 + k * sd; } }
            }
            a.hand.localRotation = Local(bR, bF, bD);
            return Vector3.Angle(a.hand.rotation * axisH, dir);
        }
        /// <summary>Full arm: IK with the elbow turned about the shoulder-hand line within ±swivel of the pole, choosing the turn that
        /// best points the held axis while keeping the elbow off the body.</summary>
        static void PlaceArm(Arm a, Vector3 P, Vector3 pole, Vector3 axisH, Vector3 dir, Vector3 thumbHint, float swivel = 30, Vector3 upH = default, float upWeight = .15f)
        {
            Vector3 dn = (P - a.up.position).normalized; float bestCost = float.MaxValue, best = 0;
            for (float phi = -swivel; phi <= swivel + .01f; phi += 5)
            {
                SolveArm(a, P, Quaternion.AngleAxis(phi, dn) * pole);
                float err = OrientHand(a, axisH, dir, thumbHint, upH, upWeight);
                float elbowBody = Seg(a.lo.position, bone["Hips"].position, bone["Neck"].position);
                float cost = err + .12f * Mathf.Abs(phi) + 400 * Mathf.Max(0, .17f - elbowBody);
                if (cost < bestCost) { bestCost = cost; best = phi; }
            }
            SolveArm(a, P, Quaternion.AngleAxis(best, dn) * pole);
            OrientHand(a, axisH, dir, thumbHint, upH, upWeight);
        }
        static void LeftFist() { foreach (var kv in grip) if (bone.TryGetValue(kv.Key, out var t)) t.rotation = armL.hand.rotation * kv.Value; }
        /// <summary>Left fingers between straight (0) and Nam's closed knife fist, mirrored (1): a lead or support hand is half open.</summary>
        static void LeftHand(float closed)
        {
            LeftFist(); if (closed >= .999f) return;
            var fist = grip.Keys.Where(bone.ContainsKey).ToDictionary(n => n, n => bone[n].localRotation);
            foreach (var kv in fist) bone[kv.Key].localRotation = Quaternion.Slerp(RestLocal(bone[kv.Key]), kv.Value, closed);
        }

        // ---------------------------------------------------------------- body frame
        static Vector3 bodyFwd = Vector3.forward, chestFwdLocal = Vector3.forward;
        static Vector3 Fwd => bodyFwd;
        static Vector3 Right => Vector3.Cross(Vector3.up, bodyFwd).normalized;
        static Vector3 OutL => Right * side;
        static Vector3 OutR => -Right * side;
        static void UseRootFrame() => bodyFwd = root.forward;
        /// <summary>Arms swing and hold in the frame of the shoulder girdle, which turns against the hips in a stride.</summary>
        static void UseChestFrame() { var v = bone["Spine2"].rotation * chestFwdLocal; v.y = 0; bodyFwd = v.sqrMagnitude > 1e-4f ? v.normalized : root.forward; }
        /// <summary>Direction that leans the forward axis down (neg) / up (pos) by elev degrees and toward the midline by inward degrees.</summary>
        static Vector3 Aim(float elev, float inward, Vector3 outSide)
            => Quaternion.AngleAxis(elev, Vector3.Cross(Vector3.up, Fwd) * -1) * (Quaternion.AngleAxis(inward * Mathf.Sign(Vector3.Dot(outSide, Right)) * -1, Vector3.up) * Fwd);

        static Vector3 propAxisKnife, propAxisGun, gunUpLocal = Vector3.up; static int gunMain;
        static void SetupProps(StringBuilder log)
        {
            UseRootFrame();
            var knife = data.FindProp("Knife"); var gun = data.FindProp("Pistol");
            propAxisKnife = knife != null && knife.prefab != null ? BladeAxis(knife.prefab) : Vector3.forward;
            if (gun == null) return;
            // The gun's biggest part (frame/slide) carries the axes: the barrel is what points forward, the top what points up, in the aim take.
            float biggest = -1;
            for (int i = 0; i < gun.parts.Length; i++)
            {
                var t = gun.prefab.transform.Find(gun.parts[i].track.name); if (t == null) continue;
                float size = t.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).Select(m => m.sharedMesh.bounds.size.sqrMagnitude).DefaultIfEmpty(0).Max()
                           + t.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(m => m.sharedMesh != null).Select(m => m.sharedMesh.bounds.size.sqrMagnitude).DefaultIfEmpty(0).Max();
                if (size > biggest) { biggest = size; gunMain = i; }
            }
            var aim = data.Find("pistol_aim"); int f = aim.start + 20; Pose(f);
            var R = armR.hand.rotation * gun.parts[gunMain].track.poses[f - 1].rotation;
            Vector3 Snap(Vector3 local, out float off)
            {
                Vector3 best = local; float bd = -2;
                foreach (var ax in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                    if (Vector3.Dot(ax, local) > bd) { bd = Vector3.Dot(ax, local); best = ax; }
                off = Mathf.Acos(Mathf.Clamp(bd, -1, 1)) * Mathf.Rad2Deg; return off < 15 ? best : local.normalized;
            }
            propAxisGun = Snap(Quaternion.Inverse(R) * Fwd, out float offB);
            gunUpLocal = Snap(Vector3.ProjectOnPlane(Quaternion.Inverse(R) * Vector3.up, propAxisGun).normalized, out float offU);
            log.AppendLine($"gun main part {gun.parts[gunMain].track.name}: barrel {propAxisGun} ({offB:F0} deg from an axis), top {gunUpLocal} ({offU:F0}); knife blade measured on its parts");
        }
        static Vector3 HandAxis(string prop, int frame)
        {
            var p = data.FindProp(prop); if (p == null) return Vector3.up;
            return prop == "Knife" ? KnifeBladeInHand(frame) : p.parts[gunMain].track.poses[frame - 1].rotation * propAxisGun;
        }
        static Vector3 GunUpInHand(int frame) => data.FindProp("Pistol").parts[gunMain].track.poses[frame - 1].rotation * gunUpLocal;
        // The game parents the knife at the hand with an identity root and places its parts by their hand-space tracks, so the
        // blade direction is measured on the parts as the game places them: from the knife's middle to the point furthest away.
        static GameObject knifeProbe; static Transform[] knifeParts;
        static Vector3 KnifeBladeInHand(int frame)
        {
            var k = data.FindProp("Knife");
            if (knifeProbe == null)
            {
                knifeProbe = UnityEngine.Object.Instantiate(k.prefab); knifeProbe.hideFlags = HideFlags.HideAndDontSave;
                knifeProbe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); knifeProbe.transform.localScale = Vector3.one;
                knifeParts = k.parts.Select(x => knifeProbe.transform.Find(x.track.name)).ToArray();
            }
            for (int i = 0; i < knifeParts.Length; i++) if (knifeParts[i] != null) k.parts[i].track.poses[frame - 1].Apply(knifeParts[i]);
            var pts = new List<Vector3>();
            foreach (var mf in knifeProbe.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.transform.localToWorldMatrix; var v = mf.sharedMesh.vertices;
                for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 400)) pts.Add(m.MultiplyPoint3x4(v[i]));
            }
            if (pts.Count == 0) return Vector3.forward;
            Vector3 c = Vector3.zero; foreach (var q in pts) c += q; c /= pts.Count;
            Vector3 tip = pts.OrderByDescending(q => (q - c).sqrMagnitude).First();
            // The far end may be the pommel: the blade is the longer side, so pick the end further from the hand (origin).
            Vector3 far = pts.OrderByDescending(q => Vector3.Dot(q - c, (tip - c).normalized)).First(), near = pts.OrderBy(q => Vector3.Dot(q - c, (tip - c).normalized)).First();
            Vector3 dir = (far - c).normalized;
            if (far.magnitude < near.magnitude) dir = -dir;
            return dir;
        }

        static IEnumerable<string> UpperArm(string s)
        {
            yield return s + "Arm"; yield return s + "ForeArm"; yield return s + "ForeArmTwist1"; yield return s + "ForeArmTwist2"; yield return s + "Hand";
            foreach (var f in Fingers) for (int k = 1; k <= 3; k++) yield return s + "Hand" + f + k;
        }

        // Step phase of a loop: +1 when the left foot is furthest forward, -1 for the right; delayed by lag frames (arms trail the legs).
        static float[] StepPhase(NamMotionData.Clip c, int lag, int smooth)
        {
            // The loop's fundamental: the first Fourier term of (left foot - right foot) along the body, delayed by lag frames.
            // A pure cosine is continuous across the loop seam by construction (the raw foot signal is not at every seam).
            int n = c.Frames; double A = 0, B = 0;
            for (int i = 0; i < n; i++)
            {
                Pose(c.start + i); float x = Vector3.Dot(bone["LeftFoot"].position - bone["RightFoot"].position, Fwd);
                double w = 2 * Math.PI * i / n; A += x * Math.Cos(w); B += x * Math.Sin(w);
            }
            double phi = Math.Atan2(B, A); var y = new float[n];
            for (int i = 0; i < n; i++) y[i] = (float)Math.Cos(2 * Math.PI * (i - lag) / n - phi);
            return y;
        }

        // ---------------------------------------------------------------- crouch (one hand K-54 low ready / knife stalk)
        static void BuildCrouch(string id, bool knife, StringBuilder log)
        {
            UseRootFrame(); var c = data.Find(id); var phase = StepPhase(c, 3, 4);
            var stats = new ArmStats();
            for (int f = c.start; f <= c.end; f++)
            {
                Pose(f); UseChestFrame(); float s = phase[f - c.start];
                Vector3 SR = armR.up.position, SL = armL.up.position;
                Vector3 axis = HandAxis(knife ? "Knife" : "Pistol", f);
                if (!knife)
                {
                    // Gun: upper arm hanging by the ribs (12 deg forward), elbow ~88 deg, forearm level and turned in so the gun sits in
                    // front of the belly; muzzle 30 deg down and a little in, thumb up. Steadied: it barely moves with the steps.
                    PumpArm(armR, OutR, 12 + 1.5f * s, 76, .30f, out Vector3 P, out Vector3 E);
                    PlaceArm(armR, P, E - SR, axis, Aim(-30, 8, OutR), Vector3.up, 30, GunUpInHand(f), .4f);
                    // Free hand: support-ready in front of the stomach beside the gun, elbow tucked, sways a little with the steps.
                    PumpArm(armL, OutL, 8 - 3 * s, 98, .55f, out Vector3 PL, out Vector3 EL);
                    PlaceArm(armL, PL, EL - SL, Vector3.up, Vector3.zero, Vector3.up, 20); LeftHand(.8f);
                }
                else
                {
                    // Knife: held at the front of the hip, elbow ~86 deg by the side, blade forward and 30 deg up, thumb up.
                    PumpArm(armR, OutR, 8 + 2 * s, 74, .10f, out Vector3 P, out Vector3 E);
                    PlaceArm(armR, P, E - SR, axis, Aim(30, 10, OutR), Vector3.up + Fwd * .3f);
                    // Lead hand: forward at chest height to check or seize, elbow ~95 deg, fist upright.
                    PumpArm(armL, OutL, 24 - 4 * s, 66, .40f, out Vector3 PL, out Vector3 EL, .04f);
                    PlaceArm(armL, PL, EL - SL, Vector3.up, Vector3.zero, Vector3.up + Fwd * .3f, 20); LeftHand(.75f);
                }
                twist.Apply();
                StoreList(f, UpperArm("Right").Concat(UpperArm("Left")));
                stats.Add(f);
            }
            float jump = SmoothLoop(c);
            log.AppendLine($"{id} {c.start}-{c.end} (body mechanics): {stats}; worst hand step before smoothing {jump * 100:F1} cm/frame, after {HandStep(c) * 100:F1}{stepInfo}");
        }

        // ---------------------------------------------------------------- run (arm pump about the shoulder, opposite the leg)
        /// <summary>The source run loops end on a frame that does not continue the stride (hips turned / feet off): measured,
        /// and if it is a lone outlier against both neighbours across the seam, rebuilt as their in-between.</summary>
        static void RepairSeam(NamMotionData.Clip c, StringBuilder log)
        {
            float Yaw(int f) { Pose(f); return Vector3.SignedAngle(root.forward, Vector3.ProjectOnPlane(bone["Hips"].forward, Vector3.up), Vector3.up); }
            Vector3 Foot(int f) { Pose(f); return root.InverseTransformPoint(bone["LeftFoot"].position) - root.InverseTransformPoint(bone["RightFoot"].position); }
            int a = c.end - 1, z = c.end, b = c.start;
            float yA = Yaw(a), yZ = Yaw(z), yB = Yaw(b), dIn = Mathf.Abs(Mathf.DeltaAngle(yA, yZ)), dOut = Mathf.Abs(Mathf.DeltaAngle(yZ, yB)), dSkip = Mathf.Abs(Mathf.DeltaAngle(yA, yB));
            float fIn = (Foot(z) - Foot(a)).magnitude, fOut = (Foot(b) - Foot(z)).magnitude, fSkip = (Foot(b) - Foot(a)).magnitude;
            log.AppendLine($"{c.id} seam: hips yaw {yA:F1} -> {yZ:F1} -> {yB:F1} (first); feet gap step {fIn * 100:F1} / {fOut * 100:F1} cm, skipping the last frame {fSkip * 100:F1} cm");
            // The last frame repeats the first; the real break is end-1 -> start. Spread that break over the last K frames (each bone
            // eased toward where the first frame continues), so the loop runs on without a one-frame jump.
            if (fOut < .005f && fIn > .06f)
            {
                const int K = 8; int last = c.end - 1;
                for (int i = 0; i < data.bones.Length; i++)
                {
                    var pe = data.bones[i].poses[last - 1]; var ps = data.bones[i].poses[c.start - 1];
                    // Where frame 'last' should be: one step before the start, the step taken from the start's own motion.
                    var p1 = data.bones[i].poses[c.start];
                    Quaternion qWant = ps.rotation * Quaternion.Inverse(p1.rotation) * ps.rotation; Vector3 vWant = ps.position * 2 - p1.position;
                    Quaternion dq = qWant * Quaternion.Inverse(pe.rotation); Vector3 dv = vWant - pe.position;
                    for (int k = 0; k < K; k++)
                    {
                        int f = last - K + 1 + k; float w = Mathf.SmoothStep(0, 1, (k + 1) / (float)K);
                        var p = data.bones[i].poses[f - 1];
                        p.rotation = Quaternion.Slerp(Quaternion.identity, dq, w) * p.rotation; p.position += dv * w;
                        data.bones[i].poses[f - 1] = p;
                    }
                }
                float fNew = (Foot(c.start) - Foot(last)).magnitude, fPrev = (Foot(last) - Foot(last - 1)).magnitude;
                log.AppendLine($"{c.id} seam: break spread over frames {last - K + 1}-{last}; now last->first feet step {fNew * 100:F1} cm, last-1->last {fPrev * 100:F1} cm");
            }
        }
        static void BuildRun(string id, bool knife, StringBuilder log)
        {
            UseRootFrame(); var c = data.Find(id); RepairSeam(c, log);
            // The source run turns the shoulder line by ~15 deg in one frame mid-loop (chest centre still): spread over 9 frames.
            SmoothCyclic(c, new[] { "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "RightShoulder" }, 4);
            var phase = StepPhase(c, 4, 3);
            var stats = new ArmStats();
            for (int f = c.start; f <= c.end; f++)
            {
                Pose(f); UseChestFrame(); float s = phase[f - c.start]; // +1: left foot forward -> right arm forward, left arm back
                // Free arm: shoulder -30..+40 deg, elbow 105..75 deg (more bent in front), hand never past the midline.
                PumpArm(armL, OutL, 5 - 35 * s, 90 + 15 * s, .10f, out Vector3 PL, out Vector3 EL);
                PlaceArm(armL, PL, EL - armL.up.position, Vector3.up, Vector3.zero, Vector3.up + Fwd * .5f, 15); LeftHand(.9f);
                Vector3 axis = HandAxis(knife ? "Knife" : "Pistol", f);
                if (!knife)
                {
                    PumpArm(armR, OutR, 8 + 14 * s, 72 - 6 * s, .08f, out Vector3 P, out Vector3 E);
                    PlaceArm(armR, P, E - armR.up.position, axis, Aim(-45, 5, OutR), Vector3.up, 30, GunUpInHand(f), .3f);
                }
                else
                {
                    PumpArm(armR, OutR, 6 + 22 * s, 74 - 8 * s, .10f, out Vector3 P, out Vector3 E);
                    PlaceArm(armR, P, E - armR.up.position, axis, Aim(-10, 8, OutR), Vector3.up + Fwd * .4f);
                }
                twist.Apply();
                StoreList(f, UpperArm("Right").Concat(UpperArm("Left")));
                stats.Add(f);
            }
            float jump = SmoothLoop(c);
            log.AppendLine($"{id} {c.start}-{c.end} (body mechanics): {stats}; worst hand step before smoothing {jump * 100:F1} cm/frame, after {HandStep(c) * 100:F1}{stepInfo}");
        }
        /// <summary>Elbow and hand from joint angles in the sagittal plane: shoulder flexion (+ forward, - back), elbow bend, the
        /// forearm turned in toward the midline by inward (share of its length), the elbow held out by elbowOut metres.</summary>
        static void PumpArm(Arm a, Vector3 outSide, float shoulderDeg, float elbowDeg, float inward, out Vector3 P, out Vector3 E, float elbowOut = 0)
        {
            float th = shoulderDeg * Mathf.Deg2Rad, ph = (shoulderDeg + elbowDeg) * Mathf.Deg2Rad;
            Vector3 S = a.up.position;
            E = S + a.L1 * (Vector3.down * Mathf.Cos(th) + Fwd * Mathf.Sin(th) + outSide * elbowOut).normalized;
            P = E + a.L2 * (Vector3.down * Mathf.Cos(ph) + Fwd * Mathf.Sin(ph) - outSide * inward).normalized;
        }

        // ---------------------------------------------------------------- knife thrust (forward grip)
        static void BuildThrust(StringBuilder log)
        {
            var at = data.Find("knife_attack"); var idle = data.Find("knife_idle"); var knife = data.FindProp("Knife");
            int n = at.Frames; var idleGrip = knife.root.poses[idle.start - 1];
            var upperNames = new[] { "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "RightShoulder" }.Concat(UpperArm("Right")).Concat(UpperArm("Left")).Where(track.ContainsKey).ToArray();
            // The whole body starts from the knife idle (its loop running on): the old overhead stab's lunge and hip twist are dropped.
            void Source(int f)
            {
                int g = idle.start + (f - at.start) % idle.Frames;
                for (int b = 0; b < data.bones.Length; b++) if (bone.TryGetValue(data.bones[b].name, out var bt)) data.bones[b].poses[g - 1].Apply(bt);
            }
            Vector3 Anchor() => Vector3.ProjectOnPlane(bone["Hips"].position, Vector3.up);
            // Keys: frame offset from the start; right hand (lateral to his right, height above chest, forward of the hips),
            // blade elevation, torso yaw (+ = right shoulder back), torso lean (+ = forward), right clavicle (+ = shoulder forward),
            // left hand (lateral to his left, height above chest, forward).
            UseRootFrame(); Source(at.start);
            float chest0 = bone["Spine2"].position.y; Vector3 a0 = Anchor();
            float[] T = { 0, 10, 21, 27, 40, 60, n - 1 };
            // Right arm keys as joints: shoulder flexion (- back), elbow bend, forearm turned toward the midline.
            Vector3 u0 = armR.lo.position - armR.up.position, f0 = armR.hand.position - armR.lo.position;
            float th0 = Mathf.Atan2(Vector3.Dot(u0, Fwd), -u0.y) * Mathf.Rad2Deg, ep0 = Mathf.Atan2(Vector3.Dot(f0, Fwd), -f0.y) * Mathf.Rad2Deg - th0;
            float[] TH = { th0, -22, 50, 52, 5, th0, th0 };
            float[] EPS = { ep0, 100, 20, 18, 80, ep0, ep0 };
            float[] IN = { .05f, .02f, .12f, .14f, .08f, .05f, .05f };
            float[] EL = { 45, 48, 38, 38, 45, 45, 45 };       // blade elevation above horizontal (forward grip: blade leads upward)
            float[] YAW = { 0, 12, -16, -18, 4, 0, 0 };
            float[] LEAN = { 0, -2, 10, 11, 3, 0, 0 };
            float[] CLAV = { 0, -6, 12, 12, 0, 0, 0 };
            float[] HIPS = { 0, 6, -8, -9, 2, 0, 0 };      // pelvis turn: the chain starts at the hips
            // Left arm as joints too: guard forward at the chamber, pulled back to the ribs as the knife goes in.
            Vector3 lu0 = armL.lo.position - armL.up.position, lf0 = armL.hand.position - armL.lo.position;
            float lth0 = Mathf.Atan2(Vector3.Dot(lu0, Fwd), -lu0.y) * Mathf.Rad2Deg, lep0 = Mathf.Atan2(Vector3.Dot(lf0, Fwd), -lf0.y) * Mathf.Rad2Deg - lth0;
            float[] LTH = { lth0, 28, -20, -20, 5, lth0, lth0 };
            float[] LEPS = { lep0, 85, 115, 115, 75, lep0, lep0 };
            float[] LIN = { .05f, .40f, .10f, .10f, .30f, .05f, .05f };
            float[] WT = { 0, 1, 1, 1, 1, 0, 0 }; float[] WTt = { 0, 8, 21, 27, 46, 66, n - 1 };

            float clavSign = ClavicleSign();
            var stats = new ArmStats(); var speeds = new List<string>(); Vector3 prev = Vector3.zero; float peakSpeed = 0; int peakAt = 0;
            for (int i = 0; i < n; i++)
            {
                int f = at.start + i; float t = i;
                Source(f);
                var src = upperNames.ToDictionary(x => x, x => bone[x].localRotation);
                float w = Pchip(WTt, WT, t);
                float yaw = Pchip(T, YAW, t) * w, lean = Pchip(T, LEAN, t) * w, clav = Pchip(T, CLAV, t) * w;
                // Pelvis turns first (thighs kept where they point, so the feet stay planted), then the torso over the three spine
                // bones, head kept on the target.
                float hy = Pchip(T, HIPS, t) * w;
                bone["Hips"].rotation = Quaternion.AngleAxis(hy, Vector3.up) * bone["Hips"].rotation;
                foreach (var leg in new[] { "LeftUpLeg", "RightUpLeg" }) bone[leg].rotation = Quaternion.AngleAxis(-hy, Vector3.up) * bone[leg].rotation;
                Vector3 latAxis = Vector3.Cross(Vector3.up, Fwd);
                foreach (var (b, k) in new[] { ("Spine", .3f), ("Spine1", .35f), ("Spine2", .35f) })
                    bone[b].rotation = Quaternion.AngleAxis(yaw * k, Vector3.up) * Quaternion.AngleAxis(lean * k, latAxis) * bone[b].rotation;
                bone["Neck"].rotation = Quaternion.AngleAxis(-(yaw + hy) * .55f, Vector3.up) * Quaternion.AngleAxis(-lean * .4f, latAxis) * bone["Neck"].rotation;
                bone["Head"].rotation = Quaternion.AngleAxis(-(yaw + hy) * .45f, Vector3.up) * Quaternion.AngleAxis(-lean * .3f, latAxis) * bone["Head"].rotation;
                bone["RightShoulder"].rotation = Quaternion.AngleAxis(clav * clavSign, Vector3.up) * bone["RightShoulder"].rotation;
                bone["LeftShoulder"].rotation = Quaternion.AngleAxis(clav * -.35f * clavSign, Vector3.up) * bone["LeftShoulder"].rotation;
                float chest = bone["Spine2"].position.y; Vector3 an = Anchor();
                PumpArm(armR, OutR, Pchip(T, TH, t), Pchip(T, EPS, t), Pchip(T, IN, t), out Vector3 P, out Vector3 E);
                PumpArm(armL, OutL, Pchip(T, LTH, t), Pchip(T, LEPS, t), Pchip(T, LIN, t), out Vector3 PL, out Vector3 ELb, .03f);
                knife.root.poses[f - 1] = idleGrip;
                foreach (var part in knife.parts) part.track.poses[f - 1] = part.track.poses[idle.start - 1];
                Vector3 blade = Aim(Pchip(T, EL, t), 4, OutR);
                PlaceArm(armR, P, E - armR.up.position, KnifeBladeInHand(f), blade, Vector3.up + Fwd * .6f);
                PlaceArm(armL, PL, ELb - armL.up.position, Vector3.up, Vector3.zero, Vector3.up, 20);
                LeftHand(.75f);
                twist.Apply();
                // Ease in and out of the idle the take starts and ends on.
                foreach (var nme in UpperArm("Right").Concat(UpperArm("Left")))
                    if (bone.TryGetValue(nme, out var bt) && src.TryGetValue(nme, out var q)) bt.localRotation = Quaternion.Slerp(q, bt.localRotation, w);
                twist.Apply();
                StoreAll(f);
                var hp = root.InverseTransformPoint(armR.hand.position);
                if (i % 4 == 0) speeds.Add($"  dbg t={i} P={root.InverseTransformPoint(P)} E={root.InverseTransformPoint(E)} S={root.InverseTransformPoint(armR.up.position)} elbow={root.InverseTransformPoint(armR.lo.position)} |P-S|={(P - armR.up.position).magnitude:F3} |H-S|={(armR.hand.position - armR.up.position).magnitude:F3} L1={armR.L1:F3} L2={armR.L2:F3}");
                float sp = i > 0 ? (hp - prev).magnitude * 60 : 0; prev = hp;
                if (sp > peakSpeed) { peakSpeed = sp; peakAt = f; }
                stats.Add(f);
                var wr = Wrist(armR, false);
                Vector3 bw = armR.hand.rotation * KnifeBladeInHand(f);
                speeds.Add($"  {f} t={i,2} w={w:F2} hand=({hp.x:F2},{hp.y:F2},{hp.z:F2}) v={sp:F1} m/s bendR={Bend(armR):F0} wristR flex={wr.x:F0} rad={wr.y:F0} roll={wr.z:F0} bladeElev={90 - Vector3.Angle(bw, Vector3.up):F0} bendL={Bend(armL):F0} yaw={yaw:F0}");
            }
            log.AppendLine($"knife_attack {at.start}-{at.end} (body mechanics thrust): peak hand speed {peakSpeed:F1} m/s at {peakAt}; {stats}");
            foreach (var s in speeds) log.AppendLine(s);
        }
        static void StoreAll(int frame)
        {
            for (int i = 0; i < data.bones.Length; i++)
                if (bone.TryGetValue(data.bones[i].name, out var t)) data.bones[i].poses[frame - 1] = Map01PistolAnimationData.Pose.Read(t);
        }
        static void SmoothCyclic(NamMotionData.Clip c, string[] names, int half)
        {
            int n = c.Frames;
            foreach (var name in names)
            {
                if (!track.TryGetValue(name, out int ti)) continue;
                var src = Enumerable.Range(0, n).Select(i => data.bones[ti].poses[c.start - 1 + i].rotation).ToArray();
                for (int i = 0; i < n; i++)
                {
                    Vector4 acc = Vector4.zero; Quaternion q0 = src[i];
                    for (int j = -half; j <= half; j++)
                    {
                        Quaternion q = src[((i + j) % n + n) % n]; float w = half + 1 - Mathf.Abs(j);
                        if (Quaternion.Dot(q, q0) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        acc += new Vector4(q.x, q.y, q.z, q.w) * w;
                    }
                    acc.Normalize();
                    var p = data.bones[ti].poses[c.start - 1 + i]; p.rotation = new Quaternion(acc.x, acc.y, acc.z, acc.w); data.bones[ti].poses[c.start - 1 + i] = p;
                }
            }
        }
        static readonly string[] SmoothBones = { "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand" };
        /// <summary>Cyclic smoothing (weights 1-2-3-2-1) of the arm rotations over a loop; twist bones re-derived. Returns the worst
        /// per-frame hand step (cm/frame in the body frame) before smoothing.</summary>
        static float SmoothLoop(NamMotionData.Clip c)
        {
            float before = HandStep(c); int n = c.Frames; float[] k = { 1, 2, 3, 2, 1 };
            foreach (var name in SmoothBones)
            {
                if (!track.TryGetValue(name, out int ti)) continue;
                var src = Enumerable.Range(0, n).Select(i => data.bones[ti].poses[c.start - 1 + i].rotation).ToArray();
                for (int i = 0; i < n; i++)
                {
                    Vector4 acc = Vector4.zero; Quaternion q0 = src[i];
                    for (int j = -2; j <= 2; j++)
                    {
                        Quaternion q = src[((i + j) % n + n) % n]; float w = k[j + 2];
                        if (Quaternion.Dot(q, q0) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        acc += new Vector4(q.x, q.y, q.z, q.w) * w;
                    }
                    acc.Normalize();
                    var p = data.bones[ti].poses[c.start - 1 + i]; p.rotation = new Quaternion(acc.x, acc.y, acc.z, acc.w); data.bones[ti].poses[c.start - 1 + i] = p;
                }
            }
            for (int f = c.start; f <= c.end; f++) { Pose(f); twist.Apply(); StoreList(f, new[] { "LeftForeArmTwist1", "LeftForeArmTwist2", "RightForeArmTwist1", "RightForeArmTwist2" }); }
            return before;
        }
        static string stepInfo = "";
        static float HandStep(NamMotionData.Clip c)
        {
            float worst = 0; Vector3 pl = default, pr = default, pc = default; int at = 0; float chestAt = 0;
            for (int i = 0; i <= c.Frames; i++)
            {
                Pose(c.start + i % c.Frames);
                var h = root; Vector3 l = h.InverseTransformPoint(armL.hand.position), r = h.InverseTransformPoint(armR.hand.position), ch = h.InverseTransformPoint(bone["Spine2"].position);
                if (i > 0) { float st = Mathf.Max((l - pl).magnitude, (r - pr).magnitude); if (st > worst) { worst = st; at = c.start + i % c.Frames; chestAt = (ch - pc).magnitude; } }
                pl = l; pr = r; pc = ch;
            }
            stepInfo = $" (worst at frame {at}, chest moved {chestAt * 100:F1} cm there)";
            if (at > c.start)
            {
                var sb = new StringBuilder();
                for (int f = Mathf.Max(c.start, at - 2); f <= Mathf.Min(c.end, at + 1); f++)
                {
                    Pose(f);
                    string V(Transform t) { var v = root.InverseTransformPoint(t.position); return $"({v.x:F3},{v.y:F3},{v.z:F3})"; }
                    sb.Append($" | f{f} shR{V(armR.up)} elR{V(armR.lo)} hR{V(armR.hand)} shL{V(armL.up)} elL{V(armL.lo)} hL{V(armL.hand)}");
                }
                stepInfo += sb.ToString();
            }
            return worst;
        }
        static float ClavicleSign()
        {
            var c = bone["RightShoulder"]; var q = c.rotation; Vector3 p0 = armR.up.position;
            c.rotation = Quaternion.AngleAxis(5, Vector3.up) * q; float d = Vector3.Dot(armR.up.position - p0, Fwd); c.rotation = q;
            return d >= 0 ? 1 : -1;
        }

        // ---------------------------------------------------------------- interpolation (monotone cubic: no overshoot, continuous speed)
        static float Pchip(float[] t, float[] y, float x)
        {
            int n = t.Length; if (x <= t[0]) return y[0]; if (x >= t[n - 1]) return y[n - 1];
            int k = 0; while (x > t[k + 1]) k++;
            float D(int i) => (y[i + 1] - y[i]) / (t[i + 1] - t[i]);
            float M(int i)
            {
                if (i == 0 || i == n - 1) return 0;
                float d0 = D(i - 1), d1 = D(i); if (d0 * d1 <= 0) return 0;
                float h0 = t[i] - t[i - 1], h1 = t[i + 1] - t[i], w1 = 2 * h1 + h0, w2 = h1 + 2 * h0;
                return (w1 + w2) / (w1 / d0 + w2 / d1);
            }
            float h = t[k + 1] - t[k], s = (x - t[k]) / h, s2 = s * s, s3 = s2 * s;
            return (2 * s3 - 3 * s2 + 1) * y[k] + (s3 - 2 * s2 + s) * h * M(k) + (-2 * s3 + 3 * s2) * y[k + 1] + (s3 - s2) * h * M(k + 1);
        }
        static Vector3 PchipV(float[] t, Vector3[] v, float x)
            => new(Pchip(t, v.Select(a => a.x).ToArray(), x), Pchip(t, v.Select(a => a.y).ToArray(), x), Pchip(t, v.Select(a => a.z).ToArray(), x));

        // ---------------------------------------------------------------- measures
        sealed class ArmStats
        {
            float eLmax, eRmax, bLmin = 999, bLmax, bRmin = 999, bRmax, wf, wd, wr, clear = 9, thigh = 9; int n;
            public void Add(int f)
            {
                Vector3 chest = bone["Spine2"].position;
                eLmax = Mathf.Max(eLmax, Vector3.Dot(armL.lo.position - chest, OutL));
                eRmax = Mathf.Max(eRmax, Vector3.Dot(armR.lo.position - chest, OutR));
                float bl = Bend(armL), br = Bend(armR);
                bLmin = Mathf.Min(bLmin, bl); bLmax = Mathf.Max(bLmax, bl); bRmin = Mathf.Min(bRmin, br); bRmax = Mathf.Max(bRmax, br);
                foreach (var a in new[] { armL, armR })
                {
                    var w = Wrist(a, false);
                    wf = Mathf.Max(wf, Mathf.Abs(w.x)); wd = Mathf.Max(wd, Mathf.Abs(w.y)); wr = Mathf.Max(wr, Mathf.Abs(w.z));
                    clear = Mathf.Min(clear, Seg(a.hand.position, bone["Hips"].position, bone["Neck"].position));
                    clear = Mathf.Min(clear, Seg(a.lo.position, bone["Hips"].position, bone["Neck"].position));
                    thigh = Mathf.Min(thigh, Mathf.Min(Seg(a.hand.position, bone["LeftUpLeg"].position, bone["LeftLeg"].position), Seg(a.hand.position, bone["RightUpLeg"].position, bone["RightLeg"].position)));
                }
                n++;
            }
            public override string ToString() => $"{n} frames; elbow out of chest L max {eLmax * 100:F0} cm, R max {eRmax * 100:F0} cm; bend L {bLmin:F0}-{bLmax:F0}, R {bRmin:F0}-{bRmax:F0}; wrist max flex/ext {wf:F0}, deviation {wd:F0}, roll {wr:F0}; hand/elbow to spine min {clear * 100:F0} cm, hand to thigh min {thigh * 100:F0} cm";
        }
    }
}
