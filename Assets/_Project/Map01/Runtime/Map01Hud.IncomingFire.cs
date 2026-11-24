using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        private const float IncomingFireSeconds = 1.25f;
        private const float IncomingFireHold = .375f;
        private const float IncomingFireSize = 416f;

        private Health incomingFireTarget;
        private Texture2D incomingFireTexture;
        private Vector3 incomingFireOrigin;
        private float incomingFireAt = float.NegativeInfinity;

        private bool CanShowIncomingFire => mission?.IsInitialized == true && !ForestMenu.Visible &&
            !InputFocus.OverlayOpen && !mission.Stopped && (mission.MapTwo != null
                ? !mission.MapTwo.InputBlocked
                : !mission.InventoryOpen && !mission.MapOpen && !mission.BenchOpen && !mission.MapOne.StorageOpen);

        private void TickIncomingFire()
        {
            if (incomingFireTarget == null || incomingFireTarget.transform != mission.player)
            {
                UnbindIncomingFire();
                incomingFireTarget = mission.ModernHealth;
                if (incomingFireTarget != null)
                {
                    incomingFireTarget.IncomingFire += RecordIncomingFire;
                    if (incomingFireTexture == null) incomingFireTexture = MakeTexture(160, ShadeIncomingFire);
                }
            }
            if (!CanShowIncomingFire) ClearIncomingFire();
        }

        private void RecordIncomingFire(Vector3 origin)
        {
            if (!CanShowIncomingFire) return;
            incomingFireOrigin = origin;
            incomingFireAt = Time.unscaledTime;
        }

        public void ClearIncomingFire() => incomingFireAt = float.NegativeInfinity;

        private void UnbindIncomingFire()
        {
            if (incomingFireTarget != null) incomingFireTarget.IncomingFire -= RecordIncomingFire;
            incomingFireTarget = null;
            ClearIncomingFire();
        }

        private void OnDisable() => UnbindIncomingFire();

        private void DrawIncomingFire()
        {
            float opacity = IncomingFireOpacity(Time.unscaledTime - incomingFireAt);
            if (opacity <= 0f || !CanShowIncomingFire || mission.gameCamera == null || incomingFireTexture == null) return;
            if (!TryIncomingFireAngle(mission.gameCamera.transform.right, incomingFireOrigin - mission.player.position, out float angle)) return;

            float size = IncomingFireSize * HudScale;
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var matrix = GUI.matrix;
            var color = GUI.color;
            int depth = GUI.depth;
            GUI.matrix = RotateUi(Matrix4x4.identity, center, angle);
            GUI.color = new Color(1f, 1f, 1f, opacity);
            GUI.depth = -11;
            GUI.DrawTexture(new Rect(center.x - size * .5f, center.y - size * .5f, size, size), incomingFireTexture);
            GUI.matrix = matrix;
            GUI.color = color;
            GUI.depth = depth;
        }

        internal static bool TryIncomingFireAngle(Vector3 cameraRight, Vector3 towardAttacker, out float angle)
        {
            var right = Vector3.ProjectOnPlane(cameraRight, Vector3.up).normalized;
            var toward = Vector3.ProjectOnPlane(towardAttacker, Vector3.up);
            angle = 0f;
            if (right.sqrMagnitude <= .001f || toward.sqrMagnitude <= .001f) return false;
            // The horizontal camera right remains usable even when the camera looks straight down.
            var forward = Vector3.Cross(right, Vector3.up);
            angle = Mathf.Atan2(Vector3.Dot(toward, right), Vector3.Dot(toward, forward)) * Mathf.Rad2Deg;
            return true;
        }

        internal static float IncomingFireOpacity(float elapsed)
        {
            float remaining = Mathf.Clamp01((IncomingFireSeconds - elapsed) / (IncomingFireSeconds - IncomingFireHold));
            return remaining * remaining;
        }

        private static Color ShadeIncomingFire(float u, float v)
        {
            // Sample the approved tapered arc in its original 216 px design space.
            float x = u * 216f - 108f;
            float y = (1f - v) * 216f;
            if (Mathf.Abs(x) > 49.3f || y < 24f || y > 43f) return Color.clear;
            float along = Mathf.Clamp01((x + 48.2f) / 96.4f);
            float outer = 108f - Mathf.Sqrt(82f * 82f - x * x);
            float inner = 41.7f - 35.4f * along * (1f - along);
            float edge = Mathf.Min(48.2f - Mathf.Abs(x), Mathf.Min(y - outer, inner - y));
            if (edge < 0f) return new Color(.24f, .035f, .025f, Mathf.Clamp01(1f + edge / 1.1f) * .75f);
            var light = new Color32(255, 116, 101, 255);
            var red = new Color32(223, 49, 43, 255);
            return Color.Lerp(light, red, Mathf.InverseLerp(26f, 42f, y));
        }
    }
}
