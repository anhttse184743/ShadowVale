using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The pickup feed: what Nam just took, as its bag icon, "+count" and its name, down the right edge. The
    /// newest is on top; the same item taken again within two seconds adds to its row instead of stacking a
    /// new one; at most seven rows (a chest holds six); each stays four seconds, then fades. Out of the way of the centre, where
    /// players complain loot pop-ups get in the way, and never over the open bag or map.
    /// </summary>
    public sealed partial class Map01Hud
    {
        private sealed class Pickup { public string id; public int count; public float at; }

        private readonly List<Pickup> pickups = new List<Pickup>();
        private const float PickupShown = 4f, PickupFade = .6f, PickupMerge = 2f;
        private const int PickupRows = 7;   // a whole chest (six things) at once
        private bool pickupHooked;
        private GUIStyle pickupCount, pickupName;

        /// <summary>Rows on screen now: (item, count), newest first.</summary>
        public IEnumerable<(string id, int count)> PickupFeed => pickups.Where(p => Age(p) < PickupShown + PickupFade).Select(p => (p.id, p.count));

        private static float Age(Pickup p) => Time.unscaledTime - p.at;

        private void HookPickups()
        {
            if (pickupHooked || inventory == null) return;
            inventory.Received += OnReceived;
            pickupHooked = true;
        }

        private void OnReceived(string id, int count)
        {
            var same = pickups.FirstOrDefault(p => p.id == id && Age(p) < PickupMerge);
            if (same != null) { same.count += count; same.at = Time.unscaledTime; pickups.Remove(same); pickups.Insert(0, same); return; }
            pickups.Insert(0, new Pickup { id = id, count = count, at = Time.unscaledTime });
            if (pickups.Count > PickupRows) pickups.RemoveAt(pickups.Count - 1);
        }

        private void DrawPickups(float width)
        {
            pickups.RemoveAll(p => Age(p) > PickupShown + PickupFade);
            float y = 120;
            if (inventory.IsCrafting) { DrawCraftingChip(width); y += 62; } // the bench works on while Nam is away
            if (pickups.Count == 0) return;
            if (pickupCount == null)
            {
                pickupCount = new GUIStyle(hudKey) { fontSize = 22, alignment = TextAnchor.MiddleLeft, wordWrap = false, normal = { textColor = HudGold } };
                pickupName = new GUIStyle(hudSmall) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            }
            foreach (var p in pickups)
            {
                float age = Age(p);
                float alpha = age < PickupShown ? 1 : Mathf.Clamp01(1 - (age - PickupShown) / PickupFade);
                float slide = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .2f));           // slides in from the edge
                var row = new Rect(width - 372 + (1 - slide) * 60, y, 340, 50);
                var old = GUI.color;
                HudFill(row, new Color(.035f, .045f, .03f, .82f * alpha));
                HudFill(new Rect(row.x, row.y, 3, row.height), new Color(HudGold.r, HudGold.g, HudGold.b, alpha));
                GUI.color = new Color(1, 1, 1, alpha);
                HudIcon(new Rect(row.x + 9, row.y + 3, 44, 44), p.id);
                GUI.color = new Color(1, 1, 1, alpha);
                string plus = "+" + p.count;
                float countWidth = Mathf.Max(50f, pickupCount.CalcSize(new GUIContent(plus)).x + 8f);   // a big haul (+1315) pushes the name over
                GUI.Label(new Rect(row.x + 62, row.y + 3, countWidth, 44), plus, pickupCount);
                GUI.Label(new Rect(row.x + 62 + countWidth, row.y + 3, row.width - 70 - countWidth, 44), ForestInventory.Name(p.id), pickupName);
                GUI.color = old;
                y += 56;
            }
        }

        private GUIStyle chipTitle, chipLine;

        /// <summary>What the bench is making while Nam is away from it: the item, the seconds left, how many wait
        /// behind it, and a progress bar — the first row of the right edge, above the pickup feed.</summary>
        private void DrawCraftingChip(float width)
        {
            if (chipTitle == null)
            {
                chipTitle = new GUIStyle(hudSmall) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false };
                chipLine = new GUIStyle(hudSmall) { fontSize = 14, wordWrap = false, normal = { textColor = new Color(.66f, .64f, .51f) } };
            }
            var job = inventory.Queue[0];
            var row = new Rect(width - 372, 120, 340, 54);
            HudFill(row, new Color(.035f, .045f, .03f, .82f));
            HudFill(new Rect(row.x, row.y, 3, row.height), HudGold);
            HudIcon(new Rect(row.x + 9, row.y + 5, 44, 44), job.recipe.output_item_id);
            GUI.Label(new Rect(row.x + 62, row.y + 5, 270, 22), "Đang chế tạo: " + ForestInventory.Name(job.recipe.output_item_id), chipTitle);
            GUI.Label(new Rect(row.x + 62, row.y + 26, 270, 20), $"còn {inventory.CraftRemaining:0.0} giây"
                + (inventory.Queue.Count > 1 ? $" · {inventory.Queue.Count - 1} món chờ" : ""), chipLine);
            HudFill(new Rect(row.x + 62, row.y + 47, 266, 4), new Color(.17f, .18f, .11f));
            HudFill(new Rect(row.x + 62, row.y + 47, 266 * inventory.CraftProgress, 4), HudGold);
        }
    }
}
