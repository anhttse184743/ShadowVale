using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>Capture film on the existing jetty approach. Owns actors and camera, never map geometry.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class TutorialCaptureCutscene : MonoBehaviour
    {
        public enum Shot { None, Stumble, Discovery, ChaseWide, ChaseFront, Gunman, ShoulderHit, WoundedRun, Collision, SeatedFall, VictimView, Blackout, DragReveal, Dragging, FadeOut, Complete }
        public AnimationClip runClip, hitClip, fallClip, enemyRunClip;
        public Transform endPoint;
        public float Fade { get; private set; }
        public bool Playing { get; private set; }
        public bool Running { get; private set; }
        public Vector3 EscapeDirection { get; private set; }
        public Shot CurrentShot { get; private set; }
        public float ShotElapsed { get; private set; }
        public bool ShotFired { get; private set; }
        public int ButtImpacts { get; private set; }
        public Vector3 BloodPosition { get; private set; }
        public bool IsFirstPerson => CurrentShot == Shot.VictimView || CurrentShot == Shot.Blackout;
        public Transform Pursuer => shooter;
        public Transform BlockingSoldier => blocker;
        public float VisionBlur => vision?.Amount ?? 0;
        public Vector3 CameraShakeEuler { get; private set; }
        private TutorialDirector director;
        private Map01Mission mission;
        private ThirdPersonCamera rig;
        private TutorialCaptureAssets clips;
        private Animator hung, gunman, blockingActor;
        private Map01Extraction.PosePlayer hungPose, gunmanPose, blockerPose;
        private Transform shooter, blocker;
        private Map01Rifle shooterRifle, blockerRifle;
        private TutorialCapturePoses.Trip trip;
        private TutorialCapturePoses.Feet feet;
        private TutorialCaptureVision vision;
        private Vector3 branch;
        private bool branchCracked;
        private Vector3 origin, right, forward, bank, landForward, collision, dragStart, dragEnd;
        private float bridgeLength;
        private Vector3[] pursuitApproach = Array.Empty<Vector3>();
        private readonly Dictionary<Behaviour, bool> behaviours = new();
        private readonly Dictionary<Collider, bool> colliders = new();
        private readonly Dictionary<GameObject, bool> weapons = new();
        private readonly Dictionary<Renderer, bool> hidden = new();
        private readonly Dictionary<Transform, Pose> originalPoses = new();
        private readonly Dictionary<Transform, Pose> weaponMounts = new();
        private readonly Dictionary<SkinnedMeshRenderer, bool> offscreenUpdates = new();
        private readonly List<GameObject> effects = new();
        private AudioSource sound;
        private AudioClip thud;
        private float skipHeld, shotDuration, hurtAt;
        private bool wasInvulnerable;
        private bool previousOcclusionCulling;
        private const float StrikeContact = 1.85f;
        private const float VictimSeconds = 3.35f;
        private static float StrikeProgress(float seconds) { float t = Mathf.InverseLerp(1.55f, StrikeContact, seconds); return t * t; }

        public void Begin()
        {
            if (Playing) return;
            director = GetComponent<TutorialDirector>(); mission = GetComponent<Map01Mission>();
            if (director.patrol.Length < 2) throw new InvalidOperationException("Capture requires two soldiers.");
            clips = TutorialCaptureAssets.Load();
            if (clips == null) throw new InvalidOperationException("Build TutorialCapture assets before playing the tutorial film.");
            rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            previousOcclusionCulling = mission.gameCamera.useOcclusionCulling; mission.gameCamera.useOcclusionCulling = false;
            hung = mission.player.GetComponentsInChildren<Animator>().First(a => a.isHuman);
            blocker = director.patrol[0].transform; shooter = director.patrol[1].transform;
            blockingActor = blocker.GetComponentInChildren<Animator>(); gunman = shooter.GetComponentInChildren<Animator>();
            origin = mission.player.position;
            EscapeDirection = Vector3.ProjectOnPlane((endPoint != null ? endPoint.position : origin + Vector3.left * 5) - origin, Vector3.up).normalized;
            forward = EscapeDirection; right = Vector3.Cross(Vector3.up, forward);
            var branchObject = director.noiseTrap.transform.Find("Dry branch");
            // The existing stick runs diagonally along the escape route. Catch its
            // approaching end, so the sandal cannot pass through the middle of it.
            branch = branchObject != null
                ? branchObject.position - branchObject.up * (Mathf.Sign(Vector3.Dot(branchObject.up, forward)) * branchObject.lossyScale.y)
                : origin + Vector3.up * .05f;
            origin = OnGround(branch - right * .14f);
            EscapeDirection = Vector3.ProjectOnPlane((endPoint != null ? endPoint.position : origin + forward * 5) - origin, Vector3.up).normalized;
            forward = EscapeDirection; right = Vector3.Cross(Vector3.up, forward);
            bank = endPoint != null ? endPoint.position : origin + forward * 5;
            bridgeLength = Vector3.ProjectOnPlane(bank - origin, Vector3.up).magnitude;
            landForward = Vector3.ProjectOnPlane(director.lessonStarts[0].position - bank, Vector3.up).normalized;
            BuildPursuitApproach();
            collision = OnGround(PathPoint(14.6f)); dragEnd = bank;
            Playing = true; Fade = 0; ShotFired = false; ButtImpacts = 0; skipHeld = 0; branchCracked = false;
            mission.Cinematic = true; GetComponent<TutorialTimeControl>().SetSlow(false);
            var actions = Map01NamActions.For(mission); actions?.CancelAction(); Disable(actions);
            wasInvulnerable = mission.ModernHealth.CinematicInvulnerable; mission.ModernHealth.CinematicInvulnerable = true;
            Disable(mission.ModernPlayer); Disable(mission.ModernCombat);
            foreach (var canvas in mission.GetComponentsInChildren<Canvas>(true)) Disable(canvas);
            foreach (var actor in new[] { mission.player, blocker, shooter })
            {
                originalPoses[actor] = new Pose(actor.position, actor.rotation);
                foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) { colliders[collider] = collider.enabled; collider.enabled = false; }
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { offscreenUpdates[skin] = skin.updateWhenOffscreen; skin.updateWhenOffscreen = true; }
            }
            foreach (var enemy in director.patrol) { Disable(enemy.GetComponent<NavMeshAgent>()); Disable(enemy); }
            hungPose = new Map01Extraction.PosePlayer(hung); gunmanPose = new Map01Extraction.PosePlayer(gunman); blockerPose = new Map01Extraction.PosePlayer(blockingActor);
            shooterRifle = Map01Rifle.Attach(gunman); blockerRifle = Map01Rifle.Attach(blockingActor);
            foreach (var rifle in new[] { shooterRifle, blockerRifle }) if (rifle != null && rifle.weapon != null) {
                var mount = rifle.weapon.transform; weaponMounts[mount] = new Pose(mount.localPosition, mount.localRotation);
            }
            foreach (var weapon in mission.player.GetComponentsInChildren<Weapon>(true)) HideWeapon(weapon.gameObject);
            PlaceHung(-.47f); hungPose.Play(clips.idle); hungPose.Settle();
            feet = new TutorialCapturePoses.Feet(hung, clips, SoleGround, GroundNormal);
            trip = new TutorialCapturePoses.Trip(hung, mission.player.position, branch, OnGround);
            PlacePursuer(.45f - 4.5f);
            Place(blocker, collision + landForward * .58f, -landForward); blockerPose.Play(clips.idle);
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0; thud = MakeThud();
            StartCoroutine(Sequence());
        }
        private void Disable(Behaviour behaviour) { if (behaviour != null) { behaviours[behaviour] = behaviour.enabled; behaviour.enabled = false; } }
        private void HideWeapon(GameObject weapon) { if (weapon != null && !weapons.ContainsKey(weapon)) { weapons[weapon] = weapon.activeSelf; weapon.SetActive(false); } }
        private IEnumerator Beat(Shot shot, float duration, Action<float> frame = null)
        {
            CurrentShot = shot; ShotElapsed = 0; shotDuration = duration;
            while (ShotElapsed < duration) {
                if (!mission.Paused && !ForestMenu.Visible) { frame?.Invoke(Mathf.Clamp01(ShotElapsed / duration)); ShotElapsed += Time.deltaTime; }
                yield return null;
            }
            frame?.Invoke(1);
        }
        private IEnumerator Sequence()
        {
            hungPose.Play(clips.unarmedWalk, true); hungPose.Rate(.9f); gunmanPose.Play(clips.startled);
            yield return Beat(Shot.Stumble, 2.05f, p => {
                PlaceHung(-.47f + TutorialCapturePoses.Trip.Travel(ShotElapsed));
                if (!branchCracked && ShotElapsed >= .34f) { branchCracked = true; director.noiseTrap.Crack(branch); mission.Say("Rắc!", .8f); }
            });
            hungPose.Play(clips.idle); gunmanPose.Play(clips.startled, true); mission.Say("Địch: Có người! Đứng lại!", 2.2f);
            yield return Beat(Shot.Discovery, 1.1f);
            hungPose.Play(clips.run ?? runClip); gunmanPose.Play(clips.enemyRun ?? enemyRunClip); Running = true;
            yield return Beat(Shot.ChaseWide, 1.8f, p => Chase(Mathf.Lerp(.45f, 5.3f, p)));
            mission.Say("Địch: Đừng để hắn chạy thoát!", 2.5f);
            yield return Beat(Shot.ChaseFront, 2.15f, p => Chase(Mathf.Lerp(5.3f, 10.2f, p)));
            gunmanPose.Play(clips.idle); hungPose.Rate(.45f);
            yield return Beat(Shot.Gunman, 1f, p => PlaceHung(Mathf.Lerp(10.2f, 10.8f, p)));
            FireAtArm(); hungPose.Play(clips.hit ?? hitClip, true); gunmanPose.Play(clips.idle);
            mission.Say("Hùng: A! Tay mình…", 2.2f); yield return Beat(Shot.ShoulderHit, .65f);
            hungPose.Play(clips.run ?? runClip); hungPose.Rate(.72f); gunmanPose.Play(clips.enemyRun);
            yield return Beat(Shot.WoundedRun, 1.9f, p => Chase(Mathf.Lerp(10.8f, 14f, p), Mathf.Lerp(5.1f, 4.1f, Mathf.SmoothStep(0, 1, p))));
            yield return Beat(Shot.Collision, .75f, p => Chase(Mathf.Lerp(14f, 14.6f, Mathf.SmoothStep(0, 1, p)), 4.1f));
            Running = false; sound.PlayOneShot(thud, .5f); hungPose.Play(clips.idle); blockerPose.Play(clips.idle);
            yield return Beat(Shot.SeatedFall, 1.15f, p => PlaceHung(14.6f - .42f * Mathf.SmoothStep(0, 1, p)));
            SetFirstPerson(true); mission.Say("Địch: Mày chạy đi đâu?", 2.1f);
            yield return Beat(Shot.VictimView, VictimSeconds, p => {
                // Keep enough distance to read the whole rifle before the short, fast thrust.
                float thrust = StrikeProgress(ShotElapsed);
                Place(blocker, collision + forward * Mathf.Lerp(.58f, .24f, thrust), -forward);
                if (ShotElapsed >= StrikeContact && ButtImpacts == 0) { impactEye = Eye(); ButtImpacts++; hurtAt = ShotElapsed; sound.PlayOneShot(thud, .9f); }
                // The strike is visible first; loss of consciousness follows the contact.
                Fade = ButtImpacts > 0 ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(StrikeContact + .60f, 3.25f, ShotElapsed)) : 0;
            });
            Fade = 1; yield return Beat(Shot.Blackout, 1.1f);
            SetFirstPerson(false); HideWeapon(shooterRifle != null ? shooterRifle.weapon.gameObject : null); HideWeapon(blockerRifle != null ? blockerRifle.weapon.gameObject : null);
            dragStart = mission.player.position; hungPose.Play(clips.idle); gunmanPose.Play(clips.idle); blockerPose.Play(clips.idle);
            Drag(0); mission.Say("Địch: Kéo nó về cầu! Trói lại!", 3);
            yield return Beat(Shot.DragReveal, 1f, p => { Fade = 1 - Mathf.SmoothStep(0, 1, p); Drag(0); });
            gunmanPose.Play(clips.unarmedWalk ?? clips.enemyWalk); blockerPose.Play(clips.unarmedWalk ?? clips.enemyWalk);
            yield return Beat(Shot.Dragging, 5.8f, Drag);
            gunmanPose.Play(clips.idle); blockerPose.Play(clips.idle);
            yield return Beat(Shot.FadeOut, .85f, p => Fade = Mathf.SmoothStep(0, 1, p));
            CurrentShot = Shot.Complete; Fade = 1; director.SkipAll();
        }
        private Vector3 PathPoint(float distance) => distance <= bridgeLength ? origin + EscapeDirection * distance : bank + landForward * (distance - bridgeLength);
        private Vector3 PathDirection(float distance) => Vector3.Slerp(EscapeDirection, landForward,
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(bridgeLength - .6f, bridgeLength + .6f, distance))).normalized;
        private void BuildPursuitApproach()
        {
            // Extending the escape line backwards cuts outside the waiting deck.
            // Follow its existing walkable connection to the ramp instead.
            var start = origin + Vector3.Cross(Vector3.up, EscapeDirection) * .25f;
            pursuitApproach = new[] { start };
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(start, out var from, .75f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(start - EscapeDirection * 5, out var to, 1, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                pursuitApproach = new[] { start }.Concat(path.corners.Skip(1)).ToArray();
        }
        private Vector3 PursuitPoint(float distance)
        {
            if (distance >= 0) return PathPoint(distance) + Vector3.Cross(Vector3.up, PathDirection(distance)) * .25f;
            float remaining = -distance;
            for (int i = 1; i < pursuitApproach.Length; i++) {
                float length = Vector3.ProjectOnPlane(pursuitApproach[i] - pursuitApproach[i - 1], Vector3.up).magnitude;
                if (remaining <= length && length > .001f) return Vector3.Lerp(pursuitApproach[i - 1], pursuitApproach[i], remaining / length);
                remaining -= length;
            }
            return pursuitApproach[pursuitApproach.Length - 1];
        }
        private void PlacePursuer(float distance)
        {
            var facing = Vector3.ProjectOnPlane(PursuitPoint(distance + .35f) - PursuitPoint(distance - .35f), Vector3.up);
            Place(shooter, PursuitPoint(distance), facing.sqrMagnitude > .001f ? facing.normalized : EscapeDirection);
        }
        private void PlaceHung(float distance)
        {
            forward = PathDirection(distance); right = Vector3.Cross(Vector3.up, forward);
            mission.player.SetPositionAndRotation(OnGround(PathPoint(distance)), Quaternion.LookRotation(forward));
        }
        private void Chase(float distance, float gap = 4.5f)
        {
            PlaceHung(distance); PlacePursuer(distance - gap);
        }
        private void Drag(float progress)
        {
            var center = OnGround(Vector3.Lerp(dragStart, dragEnd, progress));
            mission.player.SetPositionAndRotation(center, Quaternion.LookRotation(forward));
            Place(blocker, center - right * .58f - forward * .45f, -forward);
            Place(shooter, center + right * .58f - forward * .45f, -forward);
            float rate = 1.3f * Mathf.Min(Mathf.SmoothStep(0, 1, progress / .12f), Mathf.SmoothStep(0, 1, (1 - progress) / .12f));
            if (CurrentShot == Shot.Dragging) { gunmanPose.Rate(rate); blockerPose.Rate(rate); }
        }
        private static void Place(Transform actor, Vector3 position, Vector3 forward) => actor.SetPositionAndRotation(OnGround(position), Quaternion.LookRotation(forward));
        private static Vector3 OnGround(Vector3 point)
        {
            if (Floor(point, out var hit)) return new Vector3(point.x, hit.point.y + .035f, point.z);
            if (NavMesh.SamplePosition(point, out var nav, 3, NavMesh.AllAreas)) return new Vector3(point.x, nav.position.y + .035f, point.z);
            return point;
        }
        private static Vector3 GroundNormal(Vector3 point) => Floor(point, out var hit) ? hit.normal : Vector3.up;
        private static Vector3 SoleGround(Vector3 point) => Floor(point, out var hit)
            ? new Vector3(point.x, hit.point.y + .035f, point.z)
            : new Vector3(point.x, float.NegativeInfinity, point.z);
        private static bool Floor(Vector3 point, out RaycastHit floor)
        {
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 12, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                // The ground itself: hills, ramps, decks and the shelter's earth entrance (ShelterEntranceSetup), not props on it.
                if (hit.normal.y > .55f && (hit.transform.name == "Natural entrance" || hit.transform.root.name.StartsWith("Detailed layout") &&
                    (hit.transform.name.StartsWith("Continuous hills") || hit.transform.name.IndexOf("ramp", StringComparison.OrdinalIgnoreCase) >= 0 || hit.transform.name.IndexOf("deck", StringComparison.OrdinalIgnoreCase) >= 0)))
                    { floor = hit; return true; }
            floor = default; return false;
        }
        private void FireAtArm()
        {
            BloodPosition = TutorialCapturePoses.Point(hung, HumanBodyBones.RightUpperArm, mission.player.position + Vector3.up * 1.35f) - Vector3.up * .12f;
            var feedback = EnemyFeedback.Attach(shooter.gameObject, mission.ModernPlayer);
            if (!feedback.Fire(BloodPosition, mission.player, mission.ObstructionMask, out _)) Debug.LogWarning("[Tutorial capture] Scenery obstructed the cinematic shot.");
            var muzzle = shooterRifle != null ? shooterRifle.weapon.Muzzle.position : shooter.position + Vector3.up * 1.4f;
            BloodImpact.Play(BloodPosition, BloodPosition - muzzle); ShotFired = true;
            var flash = new GameObject("Tutorial capture muzzle flash"); effects.Add(flash); flash.transform.position = muzzle;
            var light = flash.AddComponent<Light>(); light.color = new Color(1, .68f, .24f); light.range = 1.4f; light.intensity = 2; Destroy(flash, .055f);
        }
        private void SetFirstPerson(bool value)
        {
            if (value && mission != null) {
                vision ??= new TutorialCaptureVision(mission.gameCamera, clips.concussionProfile);
                foreach (var renderer in mission.player.GetComponentsInChildren<Renderer>(true)) { hidden[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
            } else {
                vision?.Dispose(); vision = null;
                foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value; hidden.Clear();
            }
        }
        private void LateUpdate()
        {
            if (!Playing || mission == null) return;
            float dt = mission.Paused || ForestMenu.Visible ? 0 : Time.deltaTime;
            hungPose?.Tick(dt); gunmanPose?.Tick(dt); blockerPose?.Tick(dt);
            float p = Mathf.Clamp01(ShotElapsed / Mathf.Max(.01f, shotDuration));
            bool dragging = CurrentShot == Shot.DragReveal || CurrentShot == Shot.Dragging || CurrentShot == Shot.FadeOut;
            if (!dragging) TutorialCapturePoses.GroundStanding(blockingActor, blocker.position);
            if (CurrentShot == Shot.Discovery || CurrentShot == Shot.Gunman || CurrentShot == Shot.ShoulderHit) TutorialCapturePoses.GroundStanding(gunman, shooter.position);
            if (CurrentShot == Shot.Stumble) trip?.Apply(ShotElapsed);
            if (CurrentShot == Shot.ShoulderHit || CurrentShot == Shot.WoundedRun || CurrentShot == Shot.Collision)
                TutorialCapturePoses.HoldWound(hung, CurrentShot == Shot.ShoulderHit ? Mathf.SmoothStep(0, 1, p * 3) : 1);
            if (CurrentShot == Shot.SeatedFall || IsFirstPerson) TutorialCapturePoses.Sit(hung, CurrentShot == Shot.SeatedFall ? Mathf.SmoothStep(0, 1, p) : 1);
            if (IsFirstPerson && ButtImpacts > 0) {
                float sinceHit = CurrentShot == Shot.Blackout ? VictimSeconds - hurtAt + ShotElapsed : Mathf.Max(0, ShotElapsed - hurtAt);
                TutorialCapturePoses.Collapse(hung, Mathf.SmoothStep(0, 1, sinceHit / .85f));
                vision?.Set(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .85f, sinceHit)), Mathf.Exp(-sinceHit * 6));
            }
            if (!dragging && !IsFirstPerson) {
                TutorialCapturePoses.Rifle(gunman, shooterRifle, CurrentShot == Shot.Gunman || CurrentShot == Shot.ShoulderHit
                    ? TutorialCapturePoses.Point(hung, HumanBodyBones.RightUpperArm, mission.player.position + Vector3.up * 1.4f)
                    : shooter.position + gunman.transform.forward * 5 + Vector3.up * 1.25f);
                TutorialCapturePoses.Rifle(blockingActor, blockerRifle, blocker.position + blockingActor.transform.forward * 5 + Vector3.up * 1.25f);
            }
            if (CurrentShot == Shot.VictimView) {
                float windup = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 1.15f, ShotElapsed));
                float thrust = StrikeProgress(ShotElapsed);
                TutorialCapturePoses.Brace(blockingActor, Mathf.Lerp(.01f, .08f, thrust), Mathf.Lerp(-3, 25, thrust));
                var eye = Eye();
                var restingStock = eye + forward * .44f - right * .38f + Vector3.up * .18f;
                var raisedStock = eye + forward * .66f - right * .50f + Vector3.up * .65f;
                // Once struck, the weapon stays at the impact point while Hùng's head falls.
                var contactEye = ButtImpacts > 0 ? impactEye : eye;
                var contact = contactEye + (VictimFocus() - contactEye).normalized * .11f;
                var stock = Vector3.Lerp(Vector3.Lerp(restingStock, raisedStock, windup), contact, thrust);
                // Finish with the barrel above the hands, not pointing through the chest.
                // The old backward barrel forced both grip targets inside/behind the torso.
                var direction = Vector3.Slerp(Vector3.Slerp(right * .85f + forward * .55f, right * .95f + forward * .05f + Vector3.up * .25f, windup), Vector3.up * .96f + forward * .25f - right * .12f, thrust).normalized;
                var shoulder = TutorialCapturePoses.Point(blockingActor, HumanBodyBones.RightUpperArm, blocker.position + Vector3.up * 1.4f);
                TutorialCapturePoses.Rifle(blockingActor, blockerRifle, shoulder + direction * 3, stock);
            }
            if (dragging) {
                TutorialCapturePoses.Sit(hung, 1, true);
                TutorialCapturePoses.GroundStanding(blockingActor, blocker.position); TutorialCapturePoses.GroundStanding(gunman, shooter.position);
                TutorialCapturePoses.Brace(blockingActor); TutorialCapturePoses.Brace(gunman);
                TutorialCapturePoses.Arm(blockingActor, false, TutorialCapturePoses.Point(hung, HumanBodyBones.LeftHand, mission.player.position + Vector3.up));
                TutorialCapturePoses.Arm(gunman, true, TutorialCapturePoses.Point(hung, HumanBodyBones.RightHand, mission.player.position + Vector3.up));
            }
            if (CurrentShot != Shot.Stumble) feet?.Apply(CurrentShot == Shot.SeatedFall ? Mathf.SmoothStep(0, 1, p) : IsFirstPerson || dragging ? 1 : 0, dragging);
            Compose(p);
        }
        private Vector3 Eye() => TutorialCapturePoses.Point(hung, HumanBodyBones.Head, mission.player.position + Vector3.up * .8f) + forward * .12f + Vector3.up * .045f;
        private Vector3 impactEye;
        private Vector3 VictimFocus() => Vector3.Lerp(blockingActor.GetBoneTransform(HumanBodyBones.Head).position, blockingActor.GetBoneTransform(HumanBodyBones.Chest).position, .42f);
        private void Compose(float p)
        {
            var at = mission.player.position; var f = forward; var right = this.right;
            var chest = TutorialCapturePoses.Point(hung, HumanBodyBones.Chest, at + Vector3.up * 1.2f);
            Vector3 eye, focus; float fov = 45, roll = 0; CameraShakeEuler = Vector3.zero;
            switch (CurrentShot) {
                case Shot.Stumble:
                    float reveal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, 1.25f, ShotElapsed));
                    focus = Vector3.Lerp(branch + Vector3.up * .08f, chest, reveal);
                    eye = Vector3.Lerp(branch + right * 1.2f + f * .5f + Vector3.up * .34f, at + right * 2.1f + f * .9f + Vector3.up * 1.25f, reveal);
                    fov = Mathf.Lerp(38, 48, reveal); roll = -3 * Mathf.Sin(p * Mathf.PI); break;
                case Shot.Discovery:
                    focus = TutorialCapturePoses.Point(gunman, HumanBodyBones.Head, shooter.position + Vector3.up * 1.6f);
                    eye = shooter.position + f * 2.4f + right * 1.35f + Vector3.up * 1.55f; fov = Mathf.Lerp(44, 32, p); break;
                case Shot.ChaseWide:
                    float angle = Mathf.Lerp(145, 42, Mathf.SmoothStep(0, 1, p)) * Mathf.Deg2Rad;
                    focus = Vector3.Lerp(chest, TutorialCapturePoses.Point(gunman, HumanBodyBones.Chest, shooter.position + Vector3.up * 1.2f), .5f);
                    eye = focus + f * (Mathf.Cos(angle) * 6.2f) + right * (Mathf.Sin(angle) * 6.2f) + Vector3.up * 1.8f;
                    fov = 54; break;
                case Shot.ChaseFront:
                    // Hùng foreground/right, pursuing soldier background/left.
                    f = Vector3.ProjectOnPlane(at - shooter.position, Vector3.up).normalized; right = Vector3.Cross(Vector3.up, f);
                    eye = at + f * 4.2f + right * 1.8f + Vector3.up * 1.65f;
                    focus = at - f * 1.45f + Vector3.up * 1.15f; fov = Mathf.Lerp(44, 39, p); roll = -2; break;
                case Shot.Gunman:
                    eye = shooter.position + f * 1.5f + right * 1.15f + Vector3.up * 1.5f;
                    focus = shooter.position + Vector3.up * 1.35f + f * .3f; fov = Mathf.Lerp(39, 29, p); break;
                case Shot.ShoulderHit:
                    focus = TutorialCapturePoses.Point(hung, HumanBodyBones.RightUpperArm, chest) - Vector3.up * .08f;
                    eye = at + right * 1.55f + f * .8f + Vector3.up * 1.4f; fov = 31; roll = Mathf.Sin(p * 27) * 3.5f * (1 - p); break;
                case Shot.WoundedRun:
                    eye = at + f * 1.7f + right * 1.25f + Vector3.up * 1.5f; focus = chest + right * .1f; fov = Mathf.Lerp(48, 42, p); break;
                case Shot.Collision:
                    eye = collision + right * 3.1f + f * 1.15f + Vector3.up * 1.4f;
                    focus = Vector3.Lerp(chest, blocker.position + Vector3.up * 1.3f, .35f); fov = 44; break;
                case Shot.SeatedFall:
                    eye = at + right * 1.9f - f * .6f + Vector3.up * Mathf.Lerp(1.25f, .82f, p);
                    focus = TutorialCapturePoses.Point(hung, HumanBodyBones.Head, chest); fov = Mathf.Lerp(50, 44, p); break;
                case Shot.VictimView:
                case Shot.Blackout:
                    eye = Eye(); focus = VictimFocus();
                    float afterHit = ButtImpacts > 0 ? (CurrentShot == Shot.Blackout ? VictimSeconds - hurtAt + ShotElapsed : Mathf.Max(0, ShotElapsed - hurtAt)) : 0;
                    float impact = ButtImpacts > 0 ? Mathf.Sin(Mathf.Clamp01(afterHit / .035f) * Mathf.PI * .5f) * Mathf.Exp(-afterHit * 5) : 0;
                    float collapse = ButtImpacts > 0 ? Mathf.SmoothStep(0, 1, afterHit / .85f) : 0;
                    if (ButtImpacts > 0) {
                        var viewRight = Vector3.Cross(Vector3.up, (focus - eye).normalized).normalized;
                        eye += viewRight * (Mathf.Sin(afterHit * 87) * .025f * impact + .06f * collapse) - Vector3.up * (.025f * impact + .10f * collapse) - (focus - eye).normalized * (.08f * impact);
                        CameraShakeEuler = new Vector3(-7 + Mathf.Sin(afterHit * 74) * 3, Mathf.Sin(afterHit * 83 + .7f) * 3, 8 + Mathf.Sin(afterHit * 95) * 5) * impact;
                    }
                    roll = -2 - 30 * collapse; fov = 72 + 4 * impact;
                    CameraShakeEuler += new Vector3(40 * collapse, 28 * collapse, 0); break;
                case Shot.DragReveal:
                case Shot.Dragging:
                case Shot.FadeOut:
                    eye = at + f * 3.8f + right * .6f + Vector3.up * 3.5f;
                    focus = at - f * .4f + Vector3.up * .85f; fov = CurrentShot == Shot.Dragging ? Mathf.Lerp(49, 56, p) : 49; break;
                default: return;
            }
            if (!IsFirstPerson) {
                var ray = eye - focus; float nearest = ray.magnitude;
                foreach (var obstruction in Physics.SphereCastAll(focus, .13f, ray.normalized, nearest, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                    if (obstruction.distance > .25f && !obstruction.transform.IsChildOf(mission.player) && !obstruction.transform.IsChildOf(shooter) && !obstruction.transform.IsChildOf(blocker))
                        nearest = Mathf.Min(nearest, Mathf.Max(.55f, obstruction.distance - .18f));
                if (nearest < ray.magnitude) { eye = focus + ray.normalized * nearest; fov = Mathf.Min(76, fov * Mathf.Sqrt(ray.magnitude / nearest)); }
            }
            if ((focus - eye).sqrMagnitude < .001f) return;
            var rotation = Quaternion.LookRotation(focus - eye) * Quaternion.Euler(CameraShakeEuler.x, CameraShakeEuler.y, roll + CameraShakeEuler.z);
            rig.SetCinematicView(eye, rotation, fov, 1); mission.gameCamera.transform.SetPositionAndRotation(eye, rotation); mission.gameCamera.fieldOfView = fov;
        }
        private void Update()
        {
            if (!Playing || ForestMenu.Visible) return;
            skipHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed ? skipHeld + Time.unscaledDeltaTime : 0;
            if (skipHeld >= 1) director.SkipAll();
        }
        private static AudioClip MakeThud()
        {
            const int rate = 22050; var data = new float[rate / 4]; var random = new System.Random(73);
            for (int i = 0; i < data.Length; i++) { float t = i / (float)rate; data[i] = (Mathf.Sin(t * 2 * Mathf.PI * 75) * .65f + (float)(random.NextDouble() * 2 - 1) * .2f) * Mathf.Exp(-t * 32); }
            var clip = AudioClip.Create("Tutorial body and rifle stock impact", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        public void Cancel() => Cancel(true);
        private void Cancel(bool restoreActors)
        {
            bool wasPlaying = Playing;
            if(wasPlaying&&mission!=null)mission.GetComponent<DialogueVoice>()?.Stop();
            StopAllCoroutines(); hungPose?.Dispose(); gunmanPose?.Dispose(); blockerPose?.Dispose(); hungPose = gunmanPose = blockerPose = null;
            foreach (var mount in weaponMounts) if (mount.Key != null) mount.Key.SetLocalPositionAndRotation(mount.Value.position, mount.Value.rotation);
            weaponMounts.Clear();
            foreach (var pair in offscreenUpdates) if (pair.Key != null) pair.Key.updateWhenOffscreen = pair.Value;
            offscreenUpdates.Clear();
            if (wasPlaying && mission != null && mission.gameCamera != null) mission.gameCamera.useOcclusionCulling = previousOcclusionCulling;
            SetFirstPerson(false);
            if (restoreActors) {
                foreach (var pair in originalPoses) if (pair.Key != null) pair.Key.SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
                foreach (var pair in behaviours) if (pair.Key != null) pair.Key.enabled = pair.Value;
                foreach (var pair in colliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
                foreach (var pair in weapons) if (pair.Key != null) pair.Key.SetActive(pair.Value);
            }
            originalPoses.Clear();
            behaviours.Clear(); colliders.Clear(); weapons.Clear();
            foreach (var effect in effects) if (effect != null) Destroy(effect); effects.Clear();
            if (sound != null) Destroy(sound); if (thud != null) Destroy(thud);
            if (mission != null && mission.ModernHealth != null) mission.ModernHealth.CinematicInvulnerable = wasInvulnerable;
            if (wasPlaying && mission != null) mission.Cinematic = false;
            Playing = Running = false; Fade = 0; CameraShakeEuler = Vector3.zero; trip = null; feet = null; rig?.ClearCinematicView();
        }
        private void OnDisable() => Cancel(false);
    }
}
