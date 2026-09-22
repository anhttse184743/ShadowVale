using ShadowVale.Gameplay.Combat;
using ShadowVale.UI.HUD;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Builds the testbed HUD: a centre crosshair and a weapon hotbar across the bottom.
    /// Slot order and labels come from <see cref="PlayerCombat.SlotOrder"/>, so the HUD cannot
    /// drift out of sync with the number keys. Called by <see cref="GreyboxSandboxBuilder"/>.
    /// </summary>
    public static class HudBuilder
    {
        private const float SlotSize = 104f;
        private const float SlotSpacing = 12f;
        private const float BottomMargin = 42f;
        private const float BorderThickness = 3f;

        /// <summary>Display names, indexed the same way as <see cref="PlayerCombat.SlotOrder"/>.</summary>
        private static string LabelFor(WeaponKind kind) => kind switch
        {
            WeaponKind.Rifle => "SÚNG",
            WeaponKind.Knife => "DAO",
            WeaponKind.Unarmed => "TAY KHÔNG",
            _ => kind.ToString().ToUpperInvariant(),
        };

        public static void CreateHud(Transform parent, PlayerCombat combat)
        {
            var canvasGo = new GameObject("HUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateCrosshair(canvasGo.transform);

            var hotbar = canvasGo.AddComponent<WeaponHotbar>();
            WeaponHotbar.Slot[] slots = CreateHotbar(canvasGo.transform, out CanvasGroup hotbarGroup);

            var so = new SerializedObject(hotbar);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("hotbarGroup").objectReferenceValue = hotbarGroup;
            SerializedProperty array = so.FindProperty("slots");
            array.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("background").objectReferenceValue = slots[i].background;
                element.FindPropertyRelative("border").objectReferenceValue = slots[i].border;
                element.FindPropertyRelative("label").objectReferenceValue = slots[i].label;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // Paint the starting selection now, so the HUD reads correctly in the editor too.
            hotbar.SetEquipped(combat != null ? combat.StartingWeapon : PlayerCombat.SlotOrder[0]);
        }

        /// <summary>A small dot, not a full reticle — enough to show where a shot will land.</summary>
        private static void CreateCrosshair(Transform parent)
        {
            RectTransform dot = NewRect("Crosshair", parent, new Vector2(6f, 6f));
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = Vector2.zero;

            Image image = dot.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.75f);
            image.raycastTarget = false;
        }

        private static WeaponHotbar.Slot[] CreateHotbar(Transform parent, out CanvasGroup hotbarGroup)
        {
            WeaponKind[] order = PlayerCombat.SlotOrder;
            var slots = new WeaponHotbar.Slot[order.Length];

            float totalWidth = order.Length * SlotSize + (order.Length - 1) * SlotSpacing;

            RectTransform row = NewRect("Hotbar", parent, new Vector2(totalWidth, SlotSize));
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, BottomMargin);
            // Own CanvasGroup so Map 1's mission HUD can hide just this row — the crosshair is a
            // sibling under the same canvas and must not go down with it.
            hotbarGroup = row.gameObject.AddComponent<CanvasGroup>();

            for (int i = 0; i < order.Length; i++)
            {
                float x = -totalWidth / 2f + SlotSize / 2f + i * (SlotSize + SlotSpacing);
                slots[i] = CreateSlot(row, order[i], i + 1, x);
            }
            return slots;
        }

        private static WeaponHotbar.Slot CreateSlot(RectTransform row, WeaponKind kind, int number, float x)
        {
            RectTransform slot = NewRect($"Slot_{number}_{kind}", row, new Vector2(SlotSize, SlotSize));
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2(x, 0f);

            // The "border" is just a filled quad behind a slightly inset background.
            RectTransform borderRect = Stretch(NewRect("Border", slot, Vector2.zero), 0f);
            var border = borderRect.gameObject.AddComponent<Image>();
            border.raycastTarget = false;

            RectTransform bgRect = Stretch(NewRect("Background", slot, Vector2.zero), BorderThickness);
            var background = bgRect.gameObject.AddComponent<Image>();
            background.raycastTarget = false;

            Text key = CreateText(slot, "Key", number.ToString(), 30, TextAnchor.UpperLeft);
            key.rectTransform.anchorMin = new Vector2(0f, 1f);
            key.rectTransform.anchorMax = new Vector2(0f, 1f);
            key.rectTransform.pivot = new Vector2(0f, 1f);
            key.rectTransform.sizeDelta = new Vector2(40f, 40f);
            key.rectTransform.anchoredPosition = new Vector2(9f, -6f);
            key.color = new Color(1f, 1f, 1f, 0.8f);

            Text label = CreateText(slot, "Label", LabelFor(kind), 19, TextAnchor.LowerCenter);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(0f, 34f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 9f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            return new WeaponHotbar.Slot { background = background, border = border, label = label };
        }

        // ---- uGUI plumbing ---------------------------------------------------

        private static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        private static Text CreateText(Transform parent, string name, string content, int size,
            TextAnchor anchor)
        {
            RectTransform rect = NewRect(name, parent, new Vector2(100f, 30f));
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.raycastTarget = false;
            // Built-in font: no font asset to import, and it covers Vietnamese diacritics.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }
    }
}
