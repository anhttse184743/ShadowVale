using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        private Texture2D combatIcons, healthDroplet;
        private GUIStyle combatNumber, combatCaption;

        private void DrawCombatHud(float width, float height)
        {
            if (healthDroplet == null)
            {
                // A white alpha silhouette, filled dynamically from bottom to top.
                // Its lower half is circular; the upper half tapers to a sharp tip.
                healthDroplet = MakeTexture(192, (u, v) => {
                    float y = (v - .05f) / .9f;
                    float radius = y < .35f
                        ? .44f * Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((y - .35f) / .35f, 2)))
                        : .44f * (1 - Mathf.Pow((y - .35f) / .65f, 1.15f));
                    return y >= 0 && y <= 1 && Mathf.Abs(u - .5f) <= radius ? Color.white : Color.clear;
                });
                combatIcons = Resources.Load<Texture2D>("Hud/CombatIcons");
                combatNumber = new GUIStyle(hudHeading) { fontSize = 38, alignment = TextAnchor.MiddleRight, wordWrap = false };
                combatCaption = new GUIStyle(hudSmall) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }
            float x = width - 370, y = height - 292;
            var kind = mission.ModernCombat != null ? mission.ModernCombat.EquippedKind : WeaponKind.Unarmed;
            CombatIcon(new Rect(x + 10, y + 10, 146, 118), new Rect(672, 107, 536, 438), kind == WeaponKind.Rifle);
            CombatIcon(new Rect(x + 249, y + 29, 82, 81), new Rect(96, 695, 491, 476), kind == WeaponKind.Knife);
            CombatIcon(new Rect(x + 286, y + 145, 54, 69), new Rect(783, 724, 335, 429), kind == WeaponKind.Unarmed);
            CombatLabel(new Rect(x + 46, y + 123, 60, 24), "6", combatCaption);
            CombatLabel(new Rect(x + 260, y + 109, 60, 24), "7", combatCaption);
            CombatLabel(new Rect(x + 285, y + 215, 60, 24), "8", combatCaption);

            var drop = new Rect(x + 166, y + 79, 108, 150);
            float hp = Mathf.Clamp01(mission.PlayerHealth / Mathf.Max(1, mission.Settings.playerHP));
            var tint = GUI.color;
            GUI.color = new Color(.14f, .15f, .14f, .85f);
            GUI.DrawTexture(drop, healthDroplet);
            GUI.color = new Color(.97f, .96f, .91f, 1);
            // Texture has 5% padding on both ends. Clip inside that padding so
            // even very small HP changes correspond to the visible droplet.
            if (hp > 0)
            {
                float fill = .05f + .9f * hp;
                GUI.DrawTextureWithTexCoords(new Rect(drop.x, drop.yMax - drop.height * fill, drop.width, drop.height * fill),
                    healthDroplet, new Rect(0, 0, 1, fill));
            }
            GUI.color = tint;
            CombatLabel(new Rect(x + 149, y + 230, 145, 27), $"{mission.PlayerHealth:0} / {mission.Settings.playerHP:0} HP", combatCaption);
            // Rounds in the magazine over the reserve in the pack: the magazine decides whether to
            // push on or break contact, so it reads first. A reload shows its progress instead —
            // for those seconds the pack total is not what the player needs to know.
            var combat = mission.ModernCombat;
            bool reloading = kind == WeaponKind.Rifle && combat != null && combat.IsReloading;
            string ammo = kind != WeaponKind.Rifle ? "—"
                : combat == null ? inventory.Count("ammo_rifle").ToString()
                : reloading ? $"{combat.ReloadProgress * 100f:0}%"
                : $"{combat.RoundsInMagazine}/{inventory.Count("ammo_rifle")}";
            CombatLabel(new Rect(x - 20, y + 158, 163, 49), ammo, combatNumber);
            CombatLabel(new Rect(x + 5, y + 205, 143, 26), kind != WeaponKind.Rifle ? "CẬN CHIẾN" : reloading ? "ĐANG NẠP ĐẠN" : "BĂNG / DỰ TRỮ", combatCaption);
            CombatLabel(new Rect(x, y + 261, 346, 25), $"Sức bền {mission.Stamina:0}  ·  {(mission.Hidden ? "Ẩn nấp" : mission.Crouched ? "Đi khom" : "Sẵn sàng")}", combatCaption);
        }

        private void CombatIcon(Rect rect, Rect pixels, bool selected)
        {
            if (combatIcons == null) return;
            var old = GUI.color;
            GUI.color = selected ? Color.white : new Color(.59f, .61f, .59f, .8f);
            GUI.DrawTextureWithTexCoords(rect, combatIcons, new Rect(pixels.x / 1280, 1 - pixels.yMax / 1280, pixels.width / 1280, pixels.height / 1280));
            GUI.color = old;
        }

        private static void CombatLabel(Rect rect, string text, GUIStyle style)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, .9f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 2, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
            GUI.color = old;
        }

        private void OnDestroy()
        {
            if (healthDroplet != null) Destroy(healthDroplet);
            if (compassArrow != null) Destroy(compassArrow);
            if (compassBadge != null) Destroy(compassBadge);
        }
    }
}
