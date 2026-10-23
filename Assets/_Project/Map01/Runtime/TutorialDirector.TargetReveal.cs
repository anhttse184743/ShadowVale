using System.Collections;
using System.Linq;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class TutorialDirector
    {
        public bool RevealingTargets { get; private set; }
        public float RevealProgress { get; private set; }
        public string RevealCaption { get; private set; }
        private Coroutine targetReveal;
        private ThirdPersonCamera revealRig;
        private Vector3 revealStartPosition;
        private Quaternion revealStartRotation;
        private float revealStartFov;
        private bool revealRigEnabled, cinematicBeforeReveal;
        private const float RevealTravel = .9f, RevealHold = 1f;

        private void StartTargetReveal()
        {
            if (Step != TutorialStep.StoneKnife && Step != TutorialStep.Shoot && Step != TutorialStep.Squad) return;
            var targets = CurrentEnemies().Where(e => e != null && e.Alive).Select(e => e.transform).ToArray();
            if (targets.Length == 0) return;
            var camera = mission.gameCamera;
            revealRig = camera.GetComponent<ThirdPersonCamera>(); revealRigEnabled = revealRig != null && revealRig.enabled;
            revealStartPosition = camera.transform.position; revealStartRotation = camera.transform.rotation; revealStartFov = camera.fieldOfView;
            cinematicBeforeReveal = mission.Cinematic;
            RevealingTargets = true; RevealProgress = 0; CuePending = false; mission.Cinematic = true; clock.SetSlow(false);
            RevealCaption = Step == TutorialStep.StoneKnife ? "Lính gác phía trước — khom người, vòng ra sau để ám sát."
                : Step == TutorialStep.Shoot ? "Mục tiêu tập bắn — ngắm và hạ tên lính này."
                : "Tổ lính gần cầu — tận dụng chỗ nấp và các thao tác vừa học.";
            targetReveal = StartCoroutine(RevealTargets(targets));
        }
        private IEnumerator RevealTargets(Transform[] targets)
        {
            Vector3 focus = Vector3.zero;
            foreach (var target in targets) focus += target.position + Vector3.up * 1.15f;
            focus /= targets.Length;
            float radius = targets.Max(t => Vector3.ProjectOnPlane(t.position - focus, Vector3.up).magnitude);
            var towardPlayer = Vector3.ProjectOnPlane(mission.player.position - focus, Vector3.up).normalized;
            if (towardPlayer.sqrMagnitude < .1f) towardPlayer = -mission.player.forward;
            float fov = targets.Length == 1 ? 42 : 48;
            var eye = RevealVantage(targets, focus, towardPlayer, radius);
            var rotation = Quaternion.LookRotation(focus - eye);
            const float duration = RevealTravel * 2 + RevealHold;
            float elapsed = 0;
            while (elapsed < duration) {
                if (!mission.Paused && !ForestMenu.Visible) {
                    elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime); RevealProgress = elapsed / duration;
                    float blend = elapsed < RevealTravel ? Mathf.SmoothStep(0, 1, elapsed / RevealTravel)
                        : elapsed <= RevealTravel + RevealHold ? 1 : 1 - Mathf.SmoothStep(0, 1, (elapsed - RevealTravel - RevealHold) / RevealTravel);
                    if (revealRigEnabled) {
                        // The normal arm keeps settling behind this override, so fading
                        // its weight to zero returns smoothly to the live player view.
                        revealRig.SetCinematicView(eye + Vector3.up * (Mathf.Sin(blend * Mathf.PI) * .7f), rotation, fov, blend);
                    } else {
                        mission.gameCamera.transform.SetPositionAndRotation(Vector3.Lerp(revealStartPosition, eye, blend), Quaternion.Slerp(revealStartRotation, rotation, blend));
                        mission.gameCamera.fieldOfView = Mathf.Lerp(revealStartFov, fov, blend);
                    }
                }
                yield return null;
            }
            FinishTargetReveal();
        }
        private Vector3 RevealVantage(Transform[] targets, Vector3 focus, Vector3 towardPlayer, float radius)
        {
            float distance = Mathf.Max(4.6f, radius * 2 + 3);
            var best = focus - towardPlayer * distance + Vector3.up * 2;
            int bestVisible = -1;
            foreach (float height in new[] { 1.1f, 2f, 3.2f })
            foreach (float angle in new[] { 150f, -150f, 110f, -110f, 70f, -70f, 0f, 180f }) {
                var eye = focus + Quaternion.AngleAxis(angle, Vector3.up) * towardPlayer * distance + Vector3.up * (height + radius * .25f);
                int visible = 0;
                foreach (var target in targets) {
                    var chest = target.position + Vector3.up * 1.15f; var ray = eye - chest;
                    bool blocked = Physics.SphereCastAll(chest, .12f, ray.normalized, ray.magnitude, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                        .Any(h => h.distance > .25f && !h.transform.IsChildOf(mission.player) && !targets.Any(t => h.transform.IsChildOf(t)));
                    // Foliage has no solid collider. Check both directions so a camera
                    // inside the player's hiding bush is also rejected.
                    if (!blocked && (mission.SightCover == null || (!mission.SightCover.Blocks(eye, chest) && !mission.SightCover.Blocks(chest, eye)))) visible++;
                }
                if (visible > bestVisible) { bestVisible = visible; best = eye; }
                if (visible == targets.Length) return eye;
            }
            return best;
        }
        private void FinishTargetReveal()
        {
            if (!RevealingTargets) return;
            RevealingTargets = false; RevealProgress = 1; targetReveal = null;
            if (mission != null) {
                if (!revealRigEnabled && mission.gameCamera != null) {
                    mission.gameCamera.transform.SetPositionAndRotation(revealStartPosition, revealStartRotation); mission.gameCamera.fieldOfView = revealStartFov;
                }
                mission.Cinematic = cinematicBeforeReveal;
            }
            if (revealRig != null) revealRig.ClearCinematicView();
            if (mission != null && mission.CameraInputEnabled && !ForestMenu.Visible) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            clock?.Apply();
        }
        private void CancelTargetReveal()
        {
            if (targetReveal != null) { StopCoroutine(targetReveal); targetReveal = null; }
            FinishTargetReveal();
        }
        private void OnDisable() => CancelTargetReveal();
    }
}
