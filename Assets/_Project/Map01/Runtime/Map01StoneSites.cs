using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Stones to pick up. Nam's pockets hold a limited number (Map01Inventory.StoneCapacity), so the map
    /// keeps handing them back: gravel bars along the river that refill, a couple of stones at the foot
    /// of boulders near each camp, and a few beside the paths in between (Map01StoneSiteData).
    /// <para>
    /// Each site is an ordinary <see cref="ForestPoint"/>, so [E], the prompt and the save (which sites
    /// are used up) work as for any loot. A site shows exactly the stones it still has; a full bag
    /// takes only what fits and leaves the rest lying there.
    /// </para>
    /// </summary>
    public sealed class Map01StoneSites : MonoBehaviour
    {
        public const string IdPrefix = "stones_";

        private sealed class Site
        {
            public ForestPoint point;
            public Map01StoneSiteData.Source source;
            public int full, shown = -1;
            public bool wasUsed;
            public GameObject[] stones;
        }

        private readonly List<Site> sites = new List<Site>();
        private readonly Dictionary<ForestPoint, Site> byPoint = new Dictionary<ForestPoint, Site>();
        private Map01Mission mission;
        private Map01Inventory inventory;

        public int Count => sites.Count;

        /// <summary>The site's point, in the order of the data asset.</summary>
        public ForestPoint Point(int index) => sites[index].point;
        public Map01StoneSiteData.Source SourceOf(ForestPoint point) => byPoint[point].source;
        /// <summary>How many stones are lying at the site right now.</summary>
        public int Remaining(ForestPoint point) => point.used ? 0 : point.items[0].count;
        /// <summary>How many of the site's stone models are showing.</summary>
        public int Showing(ForestPoint point)
        {
            int showing = 0;
            foreach (var stone in byPoint[point].stones) if (stone.activeSelf) showing++;
            return showing;
        }

        /// <summary>
        /// Lays the stones out. Called while the mission sets itself up, before anything asks for its
        /// points: a save restored a moment later finds the sites already there to mark as used.
        /// </summary>
        public static Map01StoneSites Attach(Map01Mission owner)
        {
            var existing = owner.GetComponent<Map01StoneSites>();
            if (existing != null) return existing;
            var data = Resources.Load<Map01StoneSiteData>(Map01StoneSiteData.ResourcePath);
            if (data == null || data.sites.Length == 0) return null;
            var added = owner.gameObject.AddComponent<Map01StoneSites>();
            added.mission = owner;
            added.inventory = owner.GetComponent<Map01Inventory>();
            added.Spawn(data);
            return added;
        }

        private void Spawn(Map01StoneSiteData data)
        {
            var root = new GameObject("Stone sites").transform;
            for (int i = 0; i < data.sites.Length; i++)
            {
                var entry = data.sites[i];
                var random = new System.Random(7919 * (i + 1));
                var site = new GameObject($"Stone site {i:00} ({entry.source})");
                site.transform.SetParent(root, false);
                site.transform.position = OnGround(entry.position, out Vector3 normal);

                var point = site.AddComponent<ForestPoint>();
                point.id = IdPrefix + i.ToString("00");
                point.kind = ForestPointKind.Stones;
                point.label = "Nhặt đá";
                point.radius = 2f;
                point.items = new[] { new ForestIngredient { item_id = "stone", count = entry.stones } };
                point.restockSeconds = entry.source == Map01StoneSiteData.Source.Riverbank ? data.riverbankRestockSeconds : 0f;

                var record = new Site { point = point, source = entry.source, full = entry.stones, stones = new GameObject[entry.stones] };
                if (data.pebbles.Length > 0)
                {
                    // The gravel that stays: half sunk, bigger than what he can throw, so the place reads as
                    // somewhere stones lie even once the loose ones are gone (and from further off).
                    int bed = entry.source == Map01StoneSiteData.Source.Riverbank ? 7 : 4;
                    for (int k = 0; k < bed; k++)
                        Pebble(data, random, site.transform, normal, Range(random, .35f, .85f), Range(random, .75f, 1.25f), -.35f);
                    for (int k = 0; k < entry.stones; k++)
                        record.stones[k] = Pebble(data, random, site.transform, normal, Range(random, .05f, .3f), Range(random, .42f, .5f), .02f);
                }
                else for (int k = 0; k < entry.stones; k++)
                {
                    // No models assigned: the same grey ball that is thrown.
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(ball.GetComponent<Collider>());
                    ball.transform.SetParent(site.transform, false);
                    ball.transform.localPosition = Quaternion.Euler(0, Range(random, 0, 360), 0) * Vector3.forward * Range(random, .05f, .3f) + Vector3.up * .07f;
                    ball.transform.localScale = Vector3.one * .15f;
                    ball.GetComponent<Renderer>().material.color = new Color(.46f, .44f, .39f);
                    record.stones[k] = ball;
                }
                sites.Add(record);
                byPoint[point] = record;
                mission.RegisterPoint(point);
            }
        }

        private static float Range(System.Random random, float low, float high) => low + (high - low) * (float)random.NextDouble();

        /// <summary>One pebble model <paramref name="away"/> metres from the site's centre, <paramref name="sink"/> of its height into the ground.</summary>
        private static GameObject Pebble(Map01StoneSiteData data, System.Random random, Transform site, Vector3 normal, float away, float scale, float sink)
        {
            var prefab = data.pebbles[random.Next(data.pebbles.Length)];
            Vector3 at = site.position + Quaternion.Euler(0, Range(random, 0, 360), 0) * Vector3.forward * away;
            at = OnGround(at, out Vector3 here);
            var pebble = Instantiate(prefab, at, Quaternion.FromToRotation(Vector3.up, here) * Quaternion.Euler(0, Range(random, 0, 360), 0), site);
            pebble.transform.localScale = Vector3.one * scale;
            var bounds = new Bounds(at, Vector3.zero);
            foreach (var renderer in pebble.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            pebble.transform.position += Vector3.up * (at.y - bounds.min.y + bounds.size.y * sink);
            return pebble;
        }

        /// <summary>The ground under a point (not a guard or a prop standing on it), and which way it faces.</summary>
        private static Vector3 OnGround(Vector3 point, out Vector3 normal)
        {
            normal = Vector3.up;
            if (!Physics.Raycast(point + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return point;
            if (hit.collider.GetComponentInParent<Animator>() != null || hit.collider.attachedRigidbody != null) return point;
            if (hit.normal.y > .5f) normal = hit.normal;
            return hit.point;
        }

        /// <summary>What [E] offers at this site: how many of its stones still fit in the bag.</summary>
        public string Prompt(ForestPoint point)
        {
            int room = inventory.StoneRoom;
            return room <= 0
                ? $"Nhặt đá — túi đã đầy ({inventory.Stones}/{inventory.StoneCapacity})"
                : $"Nhặt đá (+{Mathf.Min(room, Remaining(point))})";
        }

        /// <summary>[E] at a site: as many stones as fit go into the bag. Returns how many were taken.</summary>
        public int PickUp(ForestPoint point)
        {
            if (point.used || !byPoint.ContainsKey(point)) return 0;
            int room = inventory.StoneRoom;
            if (room <= 0)
            {
                mission.Say($"Túi đá đã đầy ({inventory.Stones}/{inventory.StoneCapacity}).", 3);
                return 0;
            }
            int taken = Mathf.Min(room, point.items[0].count);
            inventory.Receive("stone", taken);
            point.items[0].count -= taken;
            int left = point.items[0].count;
            if (left <= 0) point.MarkLooted();
            if (left > 0) mission.Say($"Túi đá đã đầy ({inventory.Stones}/{inventory.StoneCapacity}), còn {left} viên ở đây.", 3);
            return taken;
        }

        /// <summary>The nearest site that still has stones, within <paramref name="within"/> metres of a point; null if none.</summary>
        public ForestPoint Nearest(Vector3 from, float within)
        {
            ForestPoint nearest = null;
            float best = within;
            foreach (var site in sites)
            {
                if (site.point.used) continue;
                float distance = Vector3.Distance(from, site.point.transform.position);
                if (distance < best) { best = distance; nearest = site.point; }
            }
            return nearest;
        }

        private void Update()
        {
            foreach (var site in sites)
            {
                var point = site.point;
                if (point == null) continue; // The map is being unloaded.
                // Back in use — a gravel bar the river refilled, or a save from before it was emptied.
                if (site.wasUsed && !point.used) point.items[0].count = site.full;
                site.wasUsed = point.used;
                int lying = point.used ? 0 : point.items[0].count;
                if (lying == site.shown) continue;
                site.shown = lying;
                for (int k = 0; k < site.stones.Length; k++) site.stones[k].SetActive(k < lying);
            }
        }
    }
}
