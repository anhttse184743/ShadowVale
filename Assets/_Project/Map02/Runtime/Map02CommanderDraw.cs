using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>
    /// G009 (approved 22/11): anh Thành draws the AK over his right shoulder before Night 3's fight and slings it back
    /// after. The Mixamo clips play unchanged on the upper body (legs keep the current controller); the rifle rides the
    /// back socket until the right hand reaches it, then is carried by the hand and settles into both palms (pistol grip in
    /// the right palm, handguard on the left palm). Rifle axes come from the W_AK47 parts, not from its transform.
    /// </summary>
    public sealed class Map02CommanderDraw : MonoBehaviour
    {
        const string DrawClip = "Characters/G009/Rifle_Grab_From_Shoulder", HolsterClip = "Characters/G009/Rifle_Put_Behind_Shoulder";
        const float DrawContact = .44f, HolsterRelease = .375f; // fraction of the clip where the right hand meets the slung rifle

        Animator anim; Map01Rifle rifle; Action done; bool draw;
        PlayableGraph graph; AnimationClipPlayable clipPlayable; float length, contact;
        Transform rHand, chest; Vector3 gripL, supL, downL, grabL, stockL, muzzleL, conPos; Quaternion conRot; bool contacted; Vector3 startPos; Quaternion startRot;
        Vector3 relPos; Quaternion relRot; // holster: chest-relative pose when the hand lets go
        Vector3 backPos, holdPos, startLocalPos; Quaternion backRot, holdRot, startLocalRot; bool started;
        Vector3 SlungPos; Quaternion SlungRot; // chest-local sling pose (muzzle up behind the right shoulder)

        public bool Running => graph.IsValid();

        static AnimationClip Load(string path) =>
            Resources.LoadAll<AnimationClip>(path).FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));

        /// <summary>Starts the draw (true) or holster (false); returns false when it cannot run (then the caller does the old instant switch).</summary>
        public static bool Begin(Animator animator, bool drawRifle, Action finished)
        {
            if (animator == null || !animator.isHuman) return false;
            var rifle = animator.GetComponent<Map01Rifle>(); if (rifle == null) rifle = Map01Rifle.Attach(animator);
            var clip = Load(drawRifle ? DrawClip : HolsterClip);
            if (rifle == null || rifle.weapon == null || clip == null) return false;
            var self = animator.GetComponent<Map02CommanderDraw>(); if (self == null) self = animator.gameObject.AddComponent<Map02CommanderDraw>();
            if (self.Running) return true;
            return self.Play(animator, rifle, clip, drawRifle, finished);
        }

        static Vector3 Part(Transform w, string[] keys)
        {
            foreach (var k in keys)
            {
                var r = w.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                if (r) return w.InverseTransformPoint(r.bounds.center);
                var t = w.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                if (t) return w.InverseTransformPoint(t.position);
            }
            return Vector3.zero;
        }

        static void Frame(Transform w, out Vector3 stock, out Vector3 muzzle, out Vector3 down)
        {
            Vector3 grip = Part(w, new[] { "bl_grip" });
            muzzle = Part(w, new[] { "Muzzle", "barrelEnd" }); stock = Part(w, new[] { "l_stockMetal", "l_stockBase", "stock" });
            Vector3 mag = Part(w, new[] { "bm_magazine_low", "magazine" });
            down = Vector3.ProjectOnPlane(mag - grip, (muzzle - stock).normalized).normalized;
        }

        /// <summary>Chest-local sling pose: muzzle up behind the right shoulder, magazine facing away from the back — where the
        /// Mixamo over-the-shoulder draw expects it (the approved G009 video). The rifle is not moved.</summary>
        public static bool SlingLocal(Animator animator, Transform w, out Vector3 localPos, out Quaternion localRot)
        {
            localPos = Vector3.zero; localRot = Quaternion.identity;
            var chest = animator != null ? animator.GetBoneTransform(HumanBodyBones.Chest) : null; if (chest == null || w == null) return false;
            Frame(w, out var stock, out var muzzle, out var down);
            var body = animator.transform;
            Quaternion rot = Quaternion.LookRotation((Vector3.up * .9f + body.right * .3f).normalized, body.forward)
                * Quaternion.Inverse(Quaternion.LookRotation((muzzle - stock).normalized, -down));
            Vector3 mid = chest.position - body.forward * .17f + body.right * .06f - Vector3.up * .05f;
            Vector3 pos = mid - rot * Vector3.Scale(w.lossyScale, (stock + muzzle) * .5f);
            localPos = chest.InverseTransformPoint(pos); localRot = Quaternion.Inverse(chest.rotation) * rot;
            return true;
        }

        /// <summary>Puts the rifle on the chest socket in the sling pose (used for the new commander's quiet carry).</summary>
        public static void Sling(Animator animator, Transform w)
        {
            if (!SlingLocal(animator, w, out var lp, out var lr)) return;
            w.SetParent(animator.GetBoneTransform(HumanBodyBones.Chest), false); w.localPosition = lp; w.localRotation = lr;
        }

        bool Play(Animator animator, Map01Rifle r, AnimationClip clip, bool drawRifle, Action finished)
        {
            anim = animator; rifle = r; draw = drawRifle; done = finished; contacted = false;
            rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            chest = anim.GetBoneTransform(HumanBodyBones.Chest); // the game slings the rifle on Chest (SetQuietRifle)
            var w = rifle.weapon.transform;
            if (rHand == null || chest == null) return false;
            gripL = Part(w, new[] { "bl_grip" });
            Vector3 muzzle = Part(w, new[] { "Muzzle", "barrelEnd" }), stock = Part(w, new[] { "l_stockMetal", "l_stockBase", "stock" }), mag = Part(w, new[] { "bm_magazine_low", "magazine" });
            Vector3 barrel = (muzzle - stock).normalized;
            downL = Vector3.ProjectOnPlane(mag - gripL, barrel).normalized;
            supL = stock + (muzzle - stock) * .60f + downL * .025f;
            grabL = stock + (muzzle - stock) * .88f; stockL = stock; muzzleL = muzzle;
            startPos = w.position; startRot = w.rotation;
            if (!SlingLocal(anim, w, out SlungPos, out SlungRot)) return false;

            length = clip.length; contact = length * (draw ? DrawContact : HolsterRelease);
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var p in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                mask.SetHumanoidBodyPartActive(p, true);
            graph = PlayableGraph.Create("G009 commander draw");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var mixer = AnimationLayerMixerPlayable.Create(graph, 2);
            if (anim.runtimeAnimatorController != null)
            {
                var ctrl = AnimatorControllerPlayable.Create(graph, anim.runtimeAnimatorController);
                graph.Connect(ctrl, 0, mixer, 0); mixer.SetInputWeight(0, 1);
            }
            clipPlayable = AnimationClipPlayable.Create(graph, clip); clipPlayable.SetApplyFootIK(false); clipPlayable.SetDuration(length);
            graph.Connect(clipPlayable, 0, mixer, 1); mixer.SetInputWeight(1, 1);
            // Without a controller nothing would drive the legs (the body sinks to the root): play the clip whole then.
            if (anim.runtimeAnimatorController != null) mixer.SetLayerMaskFromAvatarMask(1, mask);
            AnimationPlayableOutput.Create(graph, "G009 draw", anim).SetSourcePlayable(mixer);
            // Pre-sample the clip like the approved sandbox video: the shoulder pose at the contact frame (chest-relative on
            // the back, hand-relative in the hand) and the two-hand hold at the clip's held end, relative to the right hand.
            // The rifle then follows the hand between them instead of being re-fitted to mid-swing hands.
            Sample(contact); ShoulderPose(out var sp, out var sr);
            backPos = chest.InverseTransformPoint(sp); backRot = Quaternion.Inverse(chest.rotation) * sr;
            conPos = rHand.InverseTransformPoint(sp); conRot = Quaternion.Inverse(rHand.rotation) * sr;
            Sample(draw ? length - .001f : 0f); FitPose(out var fp, out var fr);
            holdPos = rHand.InverseTransformPoint(fp); holdRot = Quaternion.Inverse(rHand.rotation) * fr;
            Sample(0f);
            started = false;
            graph.Play();
            return true;
        }

        void Sample(float time) { clipPlayable.SetTime(time); graph.Evaluate(0f); }

        Vector3 Palm(HumanBodyBones hand, HumanBodyBones middle)
        {
            var h = anim.GetBoneTransform(hand); var m = anim.GetBoneTransform(middle);
            return m ? Vector3.Lerp(h.position, m.position, .55f) : h.position;
        }

        // Rifle pose that sits in both palms of the current frame.
        void FitPose(out Vector3 pos, out Quaternion rot)
        {
            var w = rifle.weapon.transform;
            Vector3 rp = Palm(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal), lp = Palm(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal) + Vector3.up * .035f;
            Quaternion gripToFore = Quaternion.LookRotation((supL - gripL).normalized, -downL);
            rot = Quaternion.LookRotation((lp - rp).normalized, Vector3.up) * Quaternion.Inverse(gripToFore);
            Quaternion keep = w.rotation; Vector3 keepP = w.position;
            w.rotation = rot; pos = w.position + (rp - w.TransformPoint(gripL));
            w.SetPositionAndRotation(keepP, keep);
        }

        // Rifle up over the right shoulder, muzzle up, the hand's grab point (88 % towards the muzzle) at the right palm,
        // kept behind the chest — where the Mixamo hand meets it (same as the approved sandbox video).
        void ShoulderPose(out Vector3 pos, out Quaternion rot)
        {
            var w = rifle.weapon.transform; var body = anim.transform;
            Vector3 dir = (Vector3.up * .85f + body.right * .5f).normalized;
            Quaternion local = Quaternion.LookRotation((muzzleL - stockL).normalized, -downL);
            rot = Quaternion.LookRotation(dir, body.forward) * Quaternion.Inverse(local);
            Vector3 palm = Palm(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal);
            Quaternion keep = w.rotation; Vector3 keepP = w.position;
            w.rotation = rot; pos = w.position + (palm - w.TransformPoint(grabL));
            w.position = pos;
            float behind = Vector3.Dot(w.TransformPoint((stockL + muzzleL) * .5f) - chest.position, body.forward);
            if (behind > -.14f) pos -= body.forward * (behind + .14f);
            w.SetPositionAndRotation(keepP, keep);
        }

        void LateUpdate()
        {
            if (!graph.IsValid()) return;
            float t = (float)clipPlayable.GetTime();
            var w = rifle.weapon.transform;
            if (draw)
            {
                if (t < contact)
                {
                    if (!started) { started = true; startLocalPos = chest.InverseTransformPoint(w.position); startLocalRot = Quaternion.Inverse(chest.rotation) * w.rotation; }
                    // The hand reaches back: the rifle rides up the back into the shoulder pose where the hand meets it.
                    float s = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - contact * .45f) / (contact * .55f)));
                    w.SetPositionAndRotation(chest.TransformPoint(Vector3.Lerp(startLocalPos, backPos, s)), chest.rotation * Quaternion.Slerp(startLocalRot, backRot, s));
                }
                else
                {
                    if (!contacted) { contacted = true; w.SetParent(rHand, true); }
                    float s = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - contact) / Mathf.Max(.01f, (length - contact) * .7f)));
                    w.localPosition = Vector3.Lerp(conPos, holdPos, s); w.localRotation = Quaternion.Slerp(conRot, holdRot, s);
                }
            }
            else
            {
                if (t < contact)
                {
                    if (!started) { started = true; w.SetParent(rHand, true); startLocalPos = w.localPosition; startLocalRot = w.localRotation; }
                    // Combat mount -> the clip's two-hand hold, then up over the right shoulder where the hand lets go.
                    float a0 = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / Mathf.Max(.01f, contact * .3f)));
                    float s = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - contact * .3f) / Mathf.Max(.01f, contact * .7f)));
                    Vector3 hp = Vector3.Lerp(startLocalPos, holdPos, a0); Quaternion hr = Quaternion.Slerp(startLocalRot, holdRot, a0);
                    w.localPosition = Vector3.Lerp(hp, conPos, s); w.localRotation = Quaternion.Slerp(hr, conRot, s);
                }
                else
                {
                    if (!contacted)
                    {
                        contacted = true; w.SetParent(chest, true);
                        relPos = w.localPosition; relRot = w.localRotation;
                    }
                    // Released: the rifle settles down the back into the sling.
                    float s = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - contact) / Mathf.Max(.01f, (length - contact) * .4f)));
                    w.localPosition = Vector3.Lerp(relPos, SlungPos, s); w.localRotation = Quaternion.Slerp(relRot, SlungRot, s);
                }
            }
            if (t >= length - .001f) Finish();
        }

        void Finish()
        {
            if (graph.IsValid()) graph.Destroy();
            var w = rifle != null && rifle.weapon != null ? rifle.weapon.transform : null;
            if (!draw && w != null && chest != null) { w.SetParent(chest, false); w.localPosition = SlungPos; w.localRotation = SlungRot; }
            if (draw && w != null && rHand != null)
            {
                // Hand over to the game's authored hand mount, as the combat switch expects (it happens on the same frame).
                var assets = Map01ActorAssets.Load();
                if (assets != null) { w.SetParent(rHand, false); w.localPosition = assets.riflePosition; w.localRotation = Quaternion.Euler(assets.rifleEuler); }
            }
            var cb = done; done = null; cb?.Invoke();
        }

        void OnDisable() { if (graph.IsValid()) graph.Destroy(); }
    }
}
