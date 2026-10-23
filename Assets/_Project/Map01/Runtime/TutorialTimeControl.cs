using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed class TutorialTimeControl : MonoBehaviour
    {
        private Map01Mission mission;
        private float fixedStep;
        public bool Slow { get; private set; }
        private void Awake() { mission = GetComponent<Map01Mission>(); fixedStep = Time.fixedDeltaTime; }
        public void SetSlow(bool value) { Slow = value; Apply(); }
        public void Apply()
        {
            if (mission == null) return;
            float scale = Slow && !mission.Cinematic && mission.PlayerHealth > 0
                ? Mathf.Clamp(mission.Settings?.tutorialSlowScale ?? .2f, .05f, 1f) : 1f;
            Time.timeScale = mission.Paused ? 0 : scale;
            Time.fixedDeltaTime = fixedStep * scale;
        }
        private void OnDisable() { Slow = false; Time.timeScale = 1; Time.fixedDeltaTime = fixedStep; }
    }
}
