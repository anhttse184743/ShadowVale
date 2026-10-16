using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        private Texture2D combatIcons, healthDroplet;
        private GUIStyle combatNumber, combatCaption, combatBroken;

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
            var kind = mission.ModernCombat != null ? mission.ModernCombat.EquippedKind : WeaponKind.Knife;
            // The four slots (Map01Inventory.Bag): 1 long gun, 2 pistol, 3 knife on the right, 4 the throwable [Q] uses.
            // What is in hand is drawn bright; the rest dimmed; an empty slot shows only its number.
            string primary = inventory.SlotItem(Map01Inventory.Slot.Primary), pistol = inventory.SlotItem(Map01Inventory.Slot.Pistol);
            if (primary == "rifle_standard") CombatIcon(new Rect(x + 10, y + 10, 146, 118), new Rect(672, 107, 536, 438), kind == WeaponKind.Rifle);
            else if (primary != null) HotbarIcon(new Rect(x + 4, y + 44, 156, 49), primary, false);
            CombatLabel(new Rect(x + 46, y + 123, 60, 24), "1", combatCaption);
            if (pistol != null) HotbarIcon(new Rect(x + 274, y + 8, 70, 52), pistol, false);
            CombatLabel(new Rect(x + 238, y + 22, 30, 24), "2", combatCaption);
            // Each gun's condition under its picture (Map01Hud.Gunsmith.cs).
            if (Map01Inventory.HasCondition(primary)) ConditionBar(new Rect(x + 22, y + 148, 122, 5), inventory.FractionOf(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary)));
            if (Map01Inventory.HasCondition(pistol)) ConditionBar(new Rect(x + 276, y + 62, 66, 4), inventory.FractionOf(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Pistol)));
            CombatIcon(new Rect(x + 270, y + 72, 66, 66), new Rect(96, 695, 491, 476), kind == WeaponKind.Knife);
            CombatLabel(new Rect(x + 238, y + 94, 30, 24), "3", combatCaption);
            string throwable = inventory.ReadyThrowable() ?? inventory.Throwable;
            int throwCount = inventory.Count(throwable);
            var throwRect = new Rect(x + 286, y + 148, 52, 46);
            if (Time.time - inventory.ThrowableChosenAt < 1.6f) HudBorder(new Rect(throwRect.x - 6, throwRect.y - 4, throwRect.width + 12, throwRect.height + 8), HudGold, 2);
            HotbarIcon(throwRect, throwable, throwCount > 0);
            CombatLabel(new Rect(x + 290, y + 186, 46, 20), "×" + throwCount, new GUIStyle(combatCaption) { fontSize = 15, alignment = TextAnchor.MiddleRight });
            for (int k = 0; k < Map01Inventory.Throwables.Length; k++)
            {
                bool on = Map01Inventory.Throwables[k] == throwable;
                HudFill(new Rect(x + 290 + k * 14, y + 206, 8, 8), on ? HudGold : new Color(.85f, .85f, .8f, inventory.Count(Map01Inventory.Throwables[k]) > 0 ? .6f : .2f));
            }
            CombatLabel(new Rect(x + 238, y + 160, 30, 24), "4", combatCaption);

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
            bool broken = kind == WeaponKind.Rifle && inventory.HeldGunBroken();
            var caption = broken ? combatBroken ??= new GUIStyle(combatCaption) { normal = { textColor = ConditionBroken } } : combatCaption;
            CombatLabel(new Rect(x + 5, y + 205, 143, 26), kind != WeaponKind.Rifle ? "CẬN CHIẾN" : broken ? "SÚNG HỎNG · SỬA" : reloading ? "ĐANG NẠP ĐẠN" : "BĂNG / DỰ TRỮ", caption);
            CombatLabel(new Rect(x, y + 261, 346, 25), $"Sức bền {mission.Stamina:0}  ·  {(mission.Hidden ? "Ẩn nấp" : mission.Crouched ? "Đi khom" : "Sẵn sàng")}", combatCaption);
        }

        private readonly System.Collections.Generic.Dictionary<string, Texture2D> hotbarIcons = new System.Collections.Generic.Dictionary<string, Texture2D>();
        /// <summary>A slot picture cut out like the painted board's (Resources/Hud/HotbarIcons, rendered in Blender):
        /// the Mosin, the K-54 and the three throwables. Bright when <paramref name="lit"/>.</summary>
        private void HotbarIcon(Rect rect, string id, bool lit)
        {
            if (id == null) return;
            if (!hotbarIcons.TryGetValue(id, out var icon)) hotbarIcons[id] = icon = Resources.Load<Texture2D>("Hud/HotbarIcons/" + id);
            if (icon == null) return;
            var old = GUI.color;
            GUI.color = lit ? Color.white : new Color(.59f, .61f, .59f, .8f);
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
            GUI.color = old;
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
            if(takedownBadge!=null)Destroy(takedownBadge);
            if(takedownKnife!=null)Destroy(takedownKnife);
            if(takedownLeaf!=null)Destroy(takedownLeaf);
            if (stoneDot != null) Destroy(stoneDot);
        }
    }
}
