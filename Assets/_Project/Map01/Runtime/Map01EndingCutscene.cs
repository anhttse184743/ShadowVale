using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>Reuses the boss's existing death take; only its playback is slowed.
    /// Game time, audio pitch and other enemies' animator assets are never modified.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class Map01EndingCutscene : MonoBehaviour
    {
        private const float DeathSpeed = .55f;
        private Map01Mission mission;
        private Map01Quest quest;
        private Animator actor;
        private ThirdPersonCamera cameraRig;
        private PlayableGraph graph;
        private AnimationClipPlayable death;
        private Transform chest;
        private Vector3 feet, approach, initialCameraPosition;
        private Quaternion initialCameraRotation;
        private float initialFov, elapsed, duration, skipHeld, clipLength;
        private bool rootMotion, playing;
        private AnimatorCullingMode culling;
        public bool IsPlaying => playing;
        public float Duration => duration;
        public AnimationClip ReusedDeathClip { get; private set; }

        public bool Begin(Map01Mission owner, Map01EnemyController boss)
        {
            if (playing || owner == null || boss == null || owner.Cinematic) return false;
            actor = boss.GetComponentInChildren<Animator>();
            cameraRig = owner.gameCamera != null ? owner.gameCamera.GetComponent<ThirdPersonCamera>() : null;
            var controller = actor != null ? actor.runtimeAnimatorController : null;
            var clip = controller != null ? controller.animationClips.FirstOrDefault(c =>
                c.name.Equals("Die", System.StringComparison.OrdinalIgnoreCase)
                || c.name.IndexOf("Dying", System.StringComparison.OrdinalIgnoreCase) >= 0) : null;
            if (clip == null || cameraRig == null) {
                Debug.LogWarning("[Map01] Finale unavailable: existing death clip or camera missing.", this);
                return false;
            }
            mission = owner; quest = owner.GetComponent<Map01Quest>();
            ReusedDeathClip = clip;
            clipLength = clip.length; duration = clipLength / DeathSpeed + 1.1f;
            feet = boss.transform.position;
            chest = actor.isHuman ? actor.GetBoneTransform(HumanBodyBones.Chest) : null;
            initialCameraPosition = owner.gameCamera.transform.position;
            initialCameraRotation = owner.gameCamera.transform.rotation;
            initialFov = owner.gameCamera.fieldOfView;
            approach = ChooseApproach(boss.transform.forward);
            rootMotion = actor.applyRootMotion; culling = actor.cullingMode;
            actor.applyRootMotion = false; actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Map 1 commander final collapse");
            death = AnimationClipPlayable.Create(graph, clip);
            death.SetApplyFootIK(true); death.SetSpeed(DeathSpeed);
            AnimationPlayableOutput.Create(graph, "Existing death animation", actor).SetSourcePlayable(death);
            graph.Play();
            var agent = boss.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            mission.CloseGameplayPanel();
            cameraRig.SetBinoculars(false, 1.6f);
            mission.Cinematic = playing = true;
            elapsed = skipHeld = 0;
            return true;
        }

        private Vector3 ChooseApproach(Vector3 facing)
        {
            Vector3 focus = feet + Vector3.up * 1.1f;
            Vector3 best = facing; float bestScore = float.NegativeInfinity;
            foreach (float angle in new[] { 35f, -35f, 70f, -70f, 110f, -110f, 160f }) {
                var direction = Quaternion.Euler(0, angle, 0) * facing;
                direction.y = 0; direction.Normalize();
                var offset = direction * 3.1f + Vector3.up * .3f;
                float clearance = offset.magnitude;
                if (Physics.SphereCast(focus, .18f, offset.normalized, out var hit, clearance,
                    mission.ObstructionMask, QueryTriggerInteraction.Ignore)) clearance = hit.distance;
                float score = clearance - Mathf.Abs(angle) * .002f;
                if (score > bestScore) { bestScore = score; best = direction; }
            }
            return best;
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;
            skipHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed
                ? skipHeld + Time.unscaledDeltaTime : 0;
            if (skipHeld >= 1 || elapsed >= duration) { Finish(); return; }
            // Hold the final frame during the last beat instead of looping the death.
            if (death.IsValid() && death.GetTime() >= clipLength) {
                death.SetTime(clipLength); death.SetSpeed(0);
            }
        }

        private void LateUpdate()
        {
            if (!playing || actor == null) return;
            float progress = Mathf.Clamp01(elapsed / duration);
            var body = chest != null ? chest.position : feet + Vector3.up * Mathf.Lerp(1.3f, .25f, progress);
            var focus = Vector3.Lerp(feet + Vector3.up * .75f, body, .7f);
            focus.y = Mathf.Max(feet.y + .3f, focus.y);
            var orbit = Quaternion.AngleAxis(Mathf.SmoothStep(0, 26, progress), Vector3.up) * approach;
            var position = feet + orbit * Mathf.Lerp(3.1f, 2.35f, progress)
                + Vector3.up * Mathf.Lerp(1.5f, .85f, progress);
            var offset = position - focus;
            if (Physics.SphereCast(focus, .18f, offset.normalized, out var hit, offset.magnitude,
                mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                position = focus + offset.normalized * Mathf.Max(.35f, hit.distance - .15f);
            float entry = Vector3.Distance(initialCameraPosition, feet) > 8 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .35f));
            cameraRig.SetCinematicView(Vector3.Lerp(initialCameraPosition, position, entry),
                Quaternion.Slerp(initialCameraRotation, Quaternion.LookRotation(focus - position), entry),
                Mathf.Lerp(initialFov, Mathf.Lerp(50, 43, progress), entry), 1);
        }

        public void Skip() { if (playing) Finish(); }

        private void Finish()
        {
            if (!playing) return;
            // Render the settled pose even if the user skips while the boss is still upright.
            elapsed = duration;
            death.SetTime(clipLength); death.SetSpeed(0); graph.Evaluate(0);
            LateUpdate();
            playing = false;
            ReleaseAnimation();
            mission.Cinematic = false;
            quest.CompleteBossDefeat();
            ForestMenu.SuppressKeysAfterCutscene();
            // Keep the final composition behind the existing completion panel.
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private void ReleaseAnimation()
        {
            if (graph.IsValid()) graph.Destroy();
            if (actor == null) return;
            actor.applyRootMotion = rootMotion;
            actor.cullingMode = culling;
            actor.ResetTrigger(PlayerCombat.AnimatorParams.Die);
            if (actor.HasState(0, Animator.StringToHash("Die"))) {
                actor.Play("Die", 0, 1); actor.Update(0);
            }
        }

        private void OnGUI()
        {
            if (!playing) return;
            var color = GUI.color; int depth = GUI.depth;
            GUI.depth = -90; GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * .09f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height * .91f, Screen.width, Screen.height * .09f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 245, 14, 235, 30), "Giữ Esc 1 giây để bỏ qua");
            GUI.color = color; GUI.depth = depth;
        }

        private void OnDisable()
        {
            if (playing) { playing = false; ReleaseAnimation(); }
            if (mission != null) mission.Cinematic = false;
            if (cameraRig != null) cameraRig.ClearCinematicView();
        }
        private void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}

