using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed class TutorialScouting : MonoBehaviour, IBinoculars
    {
        public Transform supplyCache;
        public bool Binoculars { get; private set; }
        public float Progress { get; private set; }
        public Vector3 ScanTarget => supplyCache.position + Vector3.up * .25f;
        private bool held;
        private Map01Mission mission;
        private TutorialDirector director;
        private ThirdPersonCamera rig;
        private void Start()
        {
            mission = GetComponent<Map01Mission>(); director = GetComponent<TutorialDirector>();
            rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
        }
        public void HoldBinoculars(bool value) => held = value;
        public void ResetScan() { held = false; Progress = 0; SetView(false); }
        private void SetView(bool value)
        {
            if (Binoculars == value) return;
            Binoculars = value;
            var actions = Map01NamActions.For(mission);
            actions?.Hold(Map01NamActions.Binoculars, value);
            rig?.SetBinoculars(value, mission.Crouched ? 1.15f : 1.6f, 45);
        }
        private void Update()
        {
            if (mission == null || !mission.IsInitialized) return;
            bool allowed = director.Step == TutorialStep.Scout && !director.AwaitingArrival && !mission.Stopped
                && !mission.InventoryOpen && !mission.MapOpen && !ForestMenu.Visible;
            SetView(allowed && held);
            if (!allowed || supplyCache == null || Progress >= 1) return;
            Vector3 eye = mission.player.position + Vector3.up * (mission.Crouched ? 1.15f : 1.6f);
            Vector3 target = ScanTarget;
            bool clear = !Physics.Linecast(eye, target, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == supplyCache || hit.transform.IsChildOf(supplyCache) || Vector3.Distance(hit.point, target) < 1.5f;
            bool seen = Binoculars && Vector3.Distance(eye, target) <= mission.Settings.tutorialScanRange
                && Vector3.Angle(mission.gameCamera.transform.forward, target - mission.gameCamera.transform.position) < mission.Settings.tutorialScanAngle && clear;
            Progress = Mathf.Clamp01(Progress + (seen ? 1 : -1) * Time.deltaTime / Mathf.Max(.1f, mission.Settings.tutorialScanSeconds));
        }
        private void OnDisable() { if (mission != null) ResetScan(); }
    }
}
