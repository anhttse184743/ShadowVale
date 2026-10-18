using System;
using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    /// <summary>Joint-limited contact poses for Hùng's tutorial and its capture film.</summary>
    internal static class TutorialCapturePoses
    {
        public static Transform Bone(Animator actor, HumanBodyBones id) => actor != null && actor.isHuman ? actor.GetBoneTransform(id) : null;
        public static Vector3 Point(Animator actor, HumanBodyBones id, Vector3 fallback) => Bone(actor, id) is Transform bone ? bone.position : fallback;

        public static void Limb(Animator actor, HumanBodyBones upperId, HumanBodyBones lowerId, HumanBodyBones tipId,
            Vector3 target, Vector3 bend, float weight = 1, Quaternion? tipRotation = null)
        {
            var upper = Bone(actor, upperId); var lower = Bone(actor, lowerId); var tip = Bone(actor, tipId);
            if (upper == null || lower == null || tip == null) return;
            Vector3 origin = upper.position, axis = target - origin;
            float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, tip.position);
            if (axis.sqrMagnitude < .00001f || a < .001f || b < .001f) return;
            // A hand cannot fold right back onto the shoulder. Keep elbows/knees inside
            // their useful range rather than stretching or forcing an impossible contact.
            float min = Mathf.Sqrt(a * a + b * b + 2 * a * b * Mathf.Cos(140 * Mathf.Deg2Rad));
            float d = Mathf.Clamp(axis.magnitude, min, (a + b) * .975f); axis.Normalize();
            target = origin + axis * d;
            bend = Vector3.ProjectOnPlane(bend, axis).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(actor.transform.forward, axis).normalized;
            float along = (a * a + d * d - b * b) / (2 * d);
            Vector3 elbow = origin + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3 oldUpper = lower.position - origin, oldLower = tip.position - lower.position;
            Vector3 normal = Vector3.Cross(oldUpper, oldLower).normalized;
            Vector3 nextNormal = Vector3.Cross(elbow - origin, target - elbow).normalized;
            Quaternion upperRotation = upper.rotation, lowerRotation = lower.rotation;
            if (normal.sqrMagnitude > .5f && nextNormal.sqrMagnitude > .5f) {
                // Resolve the hinge plane as well as the direction. A shortest-arc swing
                // alone leaves arbitrary roll in the forearm and corkscrews the wrist.
                var upperFrame = Quaternion.LookRotation(elbow - origin, nextNormal) * Quaternion.Inverse(Quaternion.LookRotation(oldUpper, normal));
                var lowerFrame = Quaternion.LookRotation(target - elbow, nextNormal) * Quaternion.Inverse(Quaternion.LookRotation(oldLower, normal));
                upper.rotation = Quaternion.Slerp(upperRotation, upperFrame * upperRotation, weight);
                lower.rotation = Quaternion.Slerp(lower.rotation, lowerFrame * lowerRotation, weight);
            } else {
                upper.rotation = Quaternion.Slerp(upper.rotation, Quaternion.FromToRotation(oldUpper, elbow - origin) * upper.rotation, weight);
                lower.rotation = Quaternion.Slerp(lower.rotation, Quaternion.FromToRotation(tip.position - lower.position, target - lower.position) * lower.rotation, weight);
            }
            if (tipRotation.HasValue) tip.rotation = Quaternion.Slerp(tip.rotation, tipRotation.Value, weight);
        }
        public static void Arm(Animator actor, bool right, Vector3 target, float weight = 1, Quaternion? wrist = null)
        {
            Limb(actor, right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm,
                right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
                right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand, target,
                actor.transform.right * (right ? .35f : -.2f) + actor.transform.forward * .35f - Vector3.up, weight, wrist);
        }
        private static void Tilt(Animator actor, HumanBodyBones id, float pitch, float roll = 0)
        {
            var bone = Bone(actor, id);
            if (bone != null) bone.rotation = Quaternion.AngleAxis(roll, actor.transform.forward) * Quaternion.AngleAxis(pitch, actor.transform.right) * bone.rotation;
        }
        public static void GroundStanding(Animator actor, Vector3 root)
        {
            var left = Bone(actor, HumanBodyBones.LeftFoot); var right = Bone(actor, HumanBodyBones.RightFoot); var hips = Bone(actor, HumanBodyBones.Hips);
            if (left == null || right == null || hips == null) return;
            // Some rifle clips retain a vertical body offset; remove it only for a planted actor.
            hips.position -= Vector3.up * Mathf.Max(0, Mathf.Min(left.position.y, right.position.y) - root.y - .08f);
        }
        public static void Brace(Animator actor, float lowering = .08f, float pitch = 8)
        {
            var left = Bone(actor, HumanBodyBones.LeftFoot); var right = Bone(actor, HumanBodyBones.RightFoot); var hips = Bone(actor, HumanBodyBones.Hips);
            if (left == null || right == null || hips == null) return;
            Vector3 l = left.position, r = right.position;
            hips.position -= Vector3.up * lowering; Tilt(actor, HumanBodyBones.Spine, pitch);
            Limb(actor, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, l, actor.transform.forward + Vector3.up);
            Limb(actor, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, r, actor.transform.forward + Vector3.up);
        }
        public sealed class Feet
        {
            private sealed class Contact
            {
                public Vector3 measured;
                public readonly Transform[] bones = new Transform[4];
                public readonly Vector3[] local = new Vector3[4];
                public Vector4 weights;
                public Vector3 Point()
                {
                    Vector3 point = default;
                    for (int i = 0; i < 4; i++) if (weights[i] > 0) point += bones[i].TransformPoint(local[i]) * weights[i];
                    return point;
                }
            }
            private sealed class Sole
            {
                public Transform foot;
                public Quaternion neutral;
                public Contact[] contacts;
            }
            private readonly Animator actor;
            private readonly TutorialCaptureAssets assets;
            private readonly Func<Vector3, Vector3> ground;
            private readonly Func<Vector3, Vector3> groundNormal;
            private readonly Sole left, right;
            public Feet(Animator actor, TutorialCaptureAssets assets, Func<Vector3, Vector3> ground, Func<Vector3, Vector3> groundNormal)
            {
                this.actor = actor; this.assets = assets; this.ground = ground; this.groundNormal = groundNormal;
                left = Measure(HumanBodyBones.LeftFoot); right = Measure(HumanBodyBones.RightFoot);
            }
            private Sole Measure(HumanBodyBones id)
            {
                var foot = Bone(actor, id); var root = actor.transform;
                var verticesOnSole = new System.Collections.Generic.List<Contact>();
                // Mesh CPU data can be discarded in a player. Bind-space contacts are
                // prepared in the Editor; runtime only reads transforms and this asset.
                foreach (var data in assets.footMeshes ?? Array.Empty<TutorialCaptureAssets.FootMesh>()) {
                    var node = string.IsNullOrEmpty(data.rendererPath) ? root : root.Find(data.rendererPath);
                    var skin = node != null ? node.GetComponent<SkinnedMeshRenderer>() : null;
                    if (skin == null || skin.sharedMesh == null || skin.sharedMesh.vertexCount != data.vertexCount) continue;
                    var bones = skin.bones;
                    foreach (var sample in (id == HumanBodyBones.RightFoot ? data.right : data.left) ?? Array.Empty<TutorialCaptureAssets.FootVertex>()) {
                        if (sample.boneIndices == null || sample.boneIndices.Length != 4 || sample.boneLocalPoints == null || sample.boneLocalPoints.Length != 4) continue;
                        var contact = new Contact { weights = sample.weights }; bool valid = true;
                        for (int j = 0; j < 4; j++) if (contact.weights[j] > 0) {
                            int index = sample.boneIndices[j];
                            if (index < 0 || index >= bones.Length || bones[index] == null) { valid = false; break; }
                            contact.bones[j] = bones[index]; contact.local[j] = sample.boneLocalPoints[j];
                        }
                        if (!valid) continue;
                        contact.measured = contact.Point() - foot.position;
                        if (contact.measured.y <= .01f) verticesOnSole.Add(contact);
                    }
                }
                if (verticesOnSole.Count == 0) {
                    var fallback = new Contact { measured = -Vector3.up * .11f, weights = new Vector4(1, 0, 0, 0) };
                    fallback.bones[0] = foot; fallback.local[0] = foot.InverseTransformPoint(foot.position + fallback.measured); verticesOnSole.Add(fallback);
                }
                var contacts = new System.Collections.Generic.List<Contact>();
                // Retain actual support vertices across ankle angles. A rectangle below
                // the rounded heel overestimates its depth and makes the foot hover.
                for (int pitch = -60; pitch <= 60; pitch += 20)
                for (int roll = -25; roll <= 25; roll += 25) {
                    var normal = Quaternion.AngleAxis(pitch, root.right) * Quaternion.AngleAxis(roll, root.forward) * Vector3.up;
                    float lowest = float.PositiveInfinity; Contact support = null;
                    foreach (var at in verticesOnSole) if (Vector3.Dot(at.measured, normal) < lowest) { lowest = Vector3.Dot(at.measured, normal); support = at; }
                    if (!contacts.Contains(support)) contacts.Add(support);
                }
                return new Sole { foot = foot, neutral = Quaternion.Inverse(root.rotation) * foot.rotation, contacts = contacts.ToArray() };
            }
            public void Apply(float seated, bool dragged)
            {
                Place(left, false, seated, dragged); Place(right, true, seated, dragged);
            }
            private void Place(Sole sole, bool right, float seated, bool dragged)
            {
                var foot = sole.foot; var root = actor.transform; var rotation = foot.rotation;
                if (seated > 0) {
                    var normal = groundNormal(foot.position);
                    if (normal.y < .55f) normal = Vector3.up;
                    // Hip rotation must not pitch the sandals through the floor. Keep
                    // a relaxed, toes-up ankle with the heel supported by the slope.
                    var resting = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.AngleAxis(dragged ? -18 : -8, root.right) * root.rotation * sole.neutral;
                    rotation = Quaternion.Slerp(rotation, resting, seated);
                }
                foot.rotation = rotation;
                for (int pass = 0; pass < 2; pass++) {
                    // A seated/dragged heel is planted; running feet retain their lift.
                    float lift = seated >= .999f ? float.NegativeInfinity : 0;
                    foreach (var contact in sole.contacts) {
                        // Footwear near the ankle blends with the shin too. Use its
                        // actual skinned position, not a rigid offset from the foot.
                        var at = contact.Point();
                        // OnGround includes 3.5 cm of root clearance. The actual sole
                        // needs only an 8 mm gap, sampled at its heel, toe and edges.
                        lift = Mathf.Max(lift, ground(at).y - .035f + .008f - at.y);
                    }
                    if (float.IsNegativeInfinity(lift) || Mathf.Abs(lift) < .001f) break;
                    Limb(actor, right ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg,
                        right ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg,
                        right ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot,
                        foot.position + Vector3.up * lift, root.forward + Vector3.up * .5f, 1, rotation);
                }
            }
        }
        public sealed class Trip
        {
            private readonly Animator actor;
            private readonly Vector3 forward, right, leftStart, leftLanding, rightStart, rightContact, rightLanding;
            private readonly Quaternion leftRotation, rightRotation;
            private static float Ease(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
            public Trip(Animator actor, Vector3 start, Vector3 branch, Func<Vector3, Vector3> ground)
            {
                this.actor = actor; forward = actor.transform.forward; right = actor.transform.right;
                leftRotation = Bone(actor, HumanBodyBones.LeftFoot).rotation; rightRotation = Bone(actor, HumanBodyBones.RightFoot).rotation;
                leftStart = ground(start - forward * .12f - right * .14f) + Vector3.up * .10f;
                rightStart = ground(start + forward * .05f + right * .14f) + Vector3.up * .10f;
                // Catch the front of the sandal against the near side of the branch,
                // with the heel at deck height, rather than placing the ankle on it.
                rightContact = branch - forward * .20f + Vector3.up * .04f;
                leftLanding = ground(start + forward * .85f - right * .18f) + Vector3.up * .10f;
                rightLanding = ground(start + forward * 1.05f + right * .14f) + Vector3.up * .10f;
            }
            public static float Travel(float t)
            {
                if (t < .34f) return Mathf.Lerp(0, .18f, Ease(0, .34f, t));
                if (t < .85f) return Mathf.Lerp(.18f, .52f, Mathf.InverseLerp(.34f, .85f, t));
                if (t < 1.4f) return Mathf.Lerp(.52f, .76f, Mathf.InverseLerp(.85f, 1.4f, t));
                return Mathf.Lerp(.76f, .92f, Ease(1.4f, 2.05f, t));
            }
            public void Apply(float t)
            {
                float fall = Ease(.30f, .66f, t) * (1 - Ease(1.05f, 1.95f, t));
                var hips = Bone(actor, HumanBodyBones.Hips);
                hips.position += forward * (.04f * fall) - Vector3.up * (.14f * fall);
                hips.rotation = Quaternion.AngleAxis(12 * fall, right) * hips.rotation;
                Tilt(actor, HumanBodyBones.Spine, 18 * fall); Tilt(actor, HumanBodyBones.Chest, 10 * fall); Tilt(actor, HumanBodyBones.Head, 10 * fall);
                float leftStep = Ease(.45f, 1.12f, t), rightStep = Ease(.85f, 1.75f, t);
                var left = Vector3.Lerp(leftStart, leftLanding, leftStep) + Vector3.up * (.26f * Mathf.Sin(leftStep * Mathf.PI));
                var caught = Vector3.Lerp(rightStart, rightContact, Ease(0, .34f, t));
                var rightFoot = Vector3.Lerp(caught, rightLanding, rightStep) + Vector3.up * (.20f * Mathf.Sin(rightStep * Mathf.PI));
                Limb(actor, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, left, forward + Vector3.up * .45f, 1, leftRotation);
                Limb(actor, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, rightFoot, forward + Vector3.up * .45f, 1,
                    Quaternion.AngleAxis(-12 * fall * (1 - rightStep), right) * rightRotation);
                var chest = Point(actor, HumanBodyBones.Chest, actor.transform.position + Vector3.up * 1.2f);
                Arm(actor, true, chest + forward * .48f + right * .28f - Vector3.up * .12f, fall);
                Arm(actor, false, chest + forward * .45f - right * .38f + Vector3.up * .05f, fall);
            }
        }
        public static void Collapse(Animator actor, float weight)
        {
            Tilt(actor, HumanBodyBones.Spine, 10 * weight, -8 * weight);
            Tilt(actor, HumanBodyBones.Neck, 10 * weight, -8 * weight);
            Tilt(actor, HumanBodyBones.Head, 25 * weight, -20 * weight);
            var head = Bone(actor, HumanBodyBones.Head);
            if (head != null) head.rotation = Quaternion.AngleAxis(24 * weight, Vector3.up) * head.rotation;
        }
        public static void HoldWound(Animator actor, float weight)
        {
            var arm = Bone(actor, HumanBodyBones.RightUpperArm); if (arm == null) return;
            Tilt(actor, HumanBodyBones.Spine, 6 * weight, -3 * weight);
            var chest = Point(actor, HumanBodyBones.Chest, actor.transform.position + Vector3.up * 1.15f);
            Arm(actor, true, chest - Vector3.up * .23f + actor.transform.forward * .25f + actor.transform.right * .1f, weight);
            var elbow = Bone(actor, HumanBodyBones.RightLowerArm);
            var wound = elbow != null ? Vector3.Lerp(arm.position, elbow.position, .48f) : arm.position - Vector3.up * .13f;
            Arm(actor, false, wound + actor.transform.forward * .055f, weight);
            Tilt(actor, HumanBodyBones.Head, 5 * weight, -3 * weight);
        }
        public static void Sit(Animator actor, float weight, bool dragged = false)
        {
            var root = actor.transform; var hips = Bone(actor, HumanBodyBones.Hips);
            if (hips != null) {
                var position = hips.position;
                position.y = Mathf.Lerp(position.y, root.position.y + (dragged ? .25f : .31f), weight);
                hips.position = position;
                hips.rotation = Quaternion.AngleAxis((dragged ? -20 : -8) * weight, root.right) * hips.rotation;
            }
            Tilt(actor, HumanBodyBones.Spine, (dragged ? -15 : 5) * weight);
            foreach (bool right in new[] { false, true }) {
                var foot = root.position + root.forward * (dragged ? .78f : .59f) + root.right * (right ? .19f : -.19f) + Vector3.up * .09f;
                var footBone = Bone(actor, right ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
                var footRotation = footBone != null ? footBone.rotation : Quaternion.identity;
                Limb(actor, right ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg,
                    right ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg,
                    right ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot, foot, root.forward + Vector3.up * .35f, weight, footRotation);
            }
            if (!dragged) HoldWound(actor, 1);
            else {
                Arm(actor, false, root.position + Vector3.up * 1.02f - root.forward * .31f - root.right * .46f, weight);
                Arm(actor, true, root.position + Vector3.up * 1.02f - root.forward * .31f + root.right * .46f, weight);
                Tilt(actor, HumanBodyBones.Head, 14, -5);
            }
        }
        public static Vector3 StockLocal(Map01Rifle gun)
        {
            if (gun == null || gun.weapon == null) return Vector3.zero;
            return StockLocal(gun.weapon);
        }
        private static Vector3 StockLocal(Weapon gun)
        {
            var muzzle = gun.transform.InverseTransformPoint(gun.Muzzle.position);
            return muzzle.sqrMagnitude > .001f ? -muzzle.normalized * .34f + new Vector3(-.038f, .007f, 0) : new Vector3(-.038f, .007f, -.34f);
        }
        private static Vector3 Palm(Transform hand)
        {
            foreach (Transform child in hand)
                if (child.name.EndsWith("End", System.StringComparison.OrdinalIgnoreCase)) return Vector3.Lerp(hand.position, child.position, .45f);
            return hand.position;
        }
        public static void Rifle(Animator actor, Map01Rifle gun, Vector3 aim, Vector3? stockTarget = null)
        {
            if (gun == null || gun.weapon == null) return;
            Rifle(actor, gun.weapon, aim, stockTarget);
        }
        public static void Rifle(Animator actor, Weapon gun, Vector3 aim, Vector3? stockTarget = null, float raised = 0)
        {
            if (gun == null) return;
            var hand = Bone(actor, HumanBodyBones.RightHand); var shoulder = Bone(actor, HumanBodyBones.RightUpperArm);
            if (hand == null || shoulder == null) return;
            Vector3 direction = (aim - shoulder.position).normalized;
            Quaternion weaponRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0, 0, -90);
            Vector3 grip = new(.011f, .007f, -.004f);
            Vector3 target = shoulder.position + direction * Mathf.Lerp(.20f, .28f, raised)
                - actor.transform.right * Mathf.Lerp(.14f, .08f, raised) - Vector3.up * Mathf.Lerp(.16f, .06f, raised);
            if (stockTarget.HasValue) {
                var origin = stockTarget.Value - weaponRotation * Vector3.Scale(StockLocal(gun), gun.transform.lossyScale);
                var palmTarget = origin + weaponRotation * Vector3.Scale(grip, gun.transform.lossyScale);
                // The palm offset turns with the forearm. Re-evaluate after each solve so
                // the stock arrives at the face instead of swinging below the camera.
                for (int i = 0; i < 4; i++) Arm(actor, true, palmTarget - (Palm(hand) - hand.position));
            }
            else Arm(actor, true, target);
            // Seat the prop in the actual palm. Twisting the wrist to a mount copied from
            // another rig is what folds the glove/forearm and leaves the stock in the chest.
            gun.transform.rotation = weaponRotation;
            gun.transform.position = Palm(hand) - weaponRotation * Vector3.Scale(grip, gun.transform.lossyScale);
            var left = Bone(actor, HumanBodyBones.LeftHand);
            if (left != null) {
                var support = gun.transform.TransformPoint(new Vector3(-.04f, .007f, Mathf.Lerp(.14f, .22f, raised)));
                for (int i = 0; i < 2; i++) Arm(actor, false, support - (Palm(left) - left.position));
            }
        }
    }
}
