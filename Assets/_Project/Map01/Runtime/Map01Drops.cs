using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Things Nam put down (VỨT BỎ in the bag) or had no room for: each lies on the ground in front of him as its
    /// own 3D model (Map01ItemCatalog; a small sack for items without one), and [E] picks it up again — it is an
    /// ordinary one-time loot point. A save keeps every pile still lying there.
    /// </summary>
    public sealed class Map01Drops : MonoBehaviour
    {
        [System.Serializable] public sealed class Pile { public string id; public int count; public Vector3 position; public float yaw; }
        private sealed class Lying { public Pile pile; public ForestPoint point; public GameObject view; }

        public const string IdPrefix = "drop_";
        private readonly List<Lying> lying = new List<Lying>();
        private Map01Mission mission;
        private Transform root;


        public static Map01Drops For(Map01Mission mission)
        {
            if (mission == null) return null;
            var drops = mission.GetComponent<Map01Drops>();
            return drops != null ? drops : mission.gameObject.AddComponent<Map01Drops>();
        }

        private void Awake() => mission = GetComponent<Map01Mission>();

        /// <summary>Piles on the ground now (for the HUD, the tests and the save).</summary>
        public IEnumerable<Pile> Piles => lying.Where(l => l.point != null && !l.point.used).Select(l => l.pile);

        /// <summary>Puts <paramref name="count"/> of <paramref name="id"/> on the ground a step in front of Nam, a little to
        /// the side of what he put down before, so piles do not stack inside each other.</summary>
        public void Put(string id, int count)
        {
            if (mission == null || mission.player == null || count <= 0) return;
            var player = mission.player;
            int around = lying.Count(l => l.point != null && !l.point.used && Vector3.Distance(l.pile.position, player.position) < 2f);
            float angle = around * 47f;
            Vector3 spot = player.position + Quaternion.Euler(0, angle, 0) * (player.forward * .75f) + Vector3.up * .5f;
            Spawn(new Pile { id = id, count = count, position = Ground(spot), yaw = player.eulerAngles.y + 20f + angle });
        }

        private void Spawn(Pile pile)
        {
            if (root == null) root = new GameObject("Dropped items").transform;
            var go = new GameObject("Drop " + pile.id);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pile.position, Quaternion.Euler(0, pile.yaw, 0));
            var catalog = Map01ItemCatalog.Load();
            var model = catalog != null ? catalog.Model(pile.id) : null;
            GameObject view;
            if (model != null)
            {
                view = Instantiate(model, go.transform);
                view.transform.localScale = Vector3.one * catalog.Scale(pile.id);
            }
            else
            {
                // A small cloth sack: what the bag puts things in that have no model of their own.
                view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(view.GetComponent<Collider>());
                view.transform.SetParent(go.transform, false);
                view.transform.localScale = new Vector3(.22f, .16f, .2f);
                view.transform.localPosition = Vector3.up * .07f;
                var r = view.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(.34f, .31f, .22f);
            }
            foreach (var c in view.GetComponentsInChildren<Collider>()) Destroy(c);   // nothing to trip on
            var point = go.AddComponent<ForestPoint>();
            point.id = IdPrefix + System.Guid.NewGuid().ToString("N").Substring(0, 10);   // never one a save marked used
            point.kind = ForestPointKind.Loot;
            point.radius = 1.5f;
            point.restockSeconds = 0f;
            point.items = new[] { new ForestIngredient { item_id = pile.id, count = pile.count } };
            point.label = "Nhặt " + ForestInventory.Name(pile.id) + (pile.count > 1 ? " ×" + pile.count : "");
            mission.RegisterPoint(point);
            lying.Add(new Lying { pile = pile, point = point, view = go });
        }

        private void Update()
        {
            // Picked up (TakeItems marks the point looted): the pile goes. A pile only partly taken (stones that did
            // not all fit) keeps what is left.
            for (int i = lying.Count - 1; i >= 0; i--)
            {
                var l = lying[i];
                if (l.point == null) { lying.RemoveAt(i); continue; }
                if (!l.point.used)
                {
                    int left = l.point.items != null && l.point.items.Length > 0 ? l.point.items[0].count : 0;
                    if (left != l.pile.count && left > 0) { l.pile.count = left; l.point.label = "Nhặt " + ForestInventory.Name(l.pile.id) + (left > 1 ? " ×" + left : ""); }
                    continue;
                }
                mission.UnregisterPoint(l.point);
                Destroy(l.view);
                lying.RemoveAt(i);
            }
        }

        public Pile[] Capture() => Piles.Select(p => new Pile { id = p.id, count = p.count, position = p.position, yaw = p.yaw }).ToArray();

        public void Restore(Pile[] saved)
        {
            foreach (var l in lying) { if (l.point != null) mission.UnregisterPoint(l.point); if (l.view != null) Destroy(l.view); }
            lying.Clear();
            if (saved == null) return;
            foreach (var p in saved) if (p != null && !string.IsNullOrEmpty(p.id) && p.count > 0) Spawn(p);
        }

        private static Vector3 Ground(Vector3 point)
        {
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 1.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.collider is TerrainCollider || hit.collider.GetComponentInParent<Animator>() == null && hit.normal.y > .6f) return hit.point;
            return point - Vector3.up * .5f;
        }
    }
}
