using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// One iron chest in each enemy camp, the reward for taking the camp (as an outpost is liberated in Far
    /// Cry 6 only once every soldier is down). It stays padlocked while any guard of its camp is alive;
    /// once the camp is clear, [E] opens it — the lock comes off, the lid swings up and what it holds lies on
    /// its packing tray — and [E] again takes everything into the bag.
    /// <para>
    /// Each chest is a <see cref="ForestPoint"/> of kind Chest, so the prompt and the save work as for any
    /// loot: a taken chest is saved as used and comes back open and empty. An opened chest not yet emptied
    /// is not saved; after a load it is shut again (and unlocked, if its camp is still clear).
    /// </para>
    /// The chest model and every item's world model come from <see cref="Map01ItemCatalog"/>; Map 1.unity is
    /// not touched. What each chest holds is in Resources/Loot/Map01Loot.json (Map01Logistics rolls it).
    /// </summary>
    public sealed class Map01CampChests : MonoBehaviour
    {
        public const string IdPrefix = "chest_c";

        /// <summary>Where the chest stands in a compact camp (CompactEnemyCamps, camp-local): just in front of
        /// the supply tarp, between its posts, its front (and padlock) towards the courtyard.</summary>
        public static readonly Vector3 ChestLocal = new Vector3(3f, 0f, 1.05f);

        /// <summary>Height of the packing tray the contents lie on, above the chest's base (build_map01_items.py TRAY_TOP).</summary>
        public const float TrayTop = .36f;

        /// <summary>The camps with a chest. What each holds comes from its container tag's loot table (Map01Loot.json:
        /// camp_chest_c1..c3): the signature things every time (C1 the K-54, C3 the Mosin, C2 rounds and parts) and a few
        /// rolls of the camp's table, rolled from the run's seed (Map01Logistics.ContainerLoot).</summary>
        public static readonly int[] Camps = { 1, 2, 3 };
        public static string Tag(int camp) => "camp_chest_c" + camp;
        /// <summary>What camp <paramref name="camp"/>'s chest always holds (its tier-5 entries), whatever the rolls.</summary>
        public static (string item, int count)[] Guaranteed(int camp) =>
            (Map01Loot.Load().ContainerTable(Tag(camp))?.entries ?? System.Array.Empty<Map01Loot.Entry>())
            .Where(e => e.tier == 5).Select(e => (e.item, e.min)).ToArray();

        /// <summary>The long slot along the back of the tray (a rifle), then five along the front; chest-local, +Z is the front.</summary>
        private static readonly Vector3 LongSlot = new Vector3(0f, TrayTop, -.11f);
        private static readonly Vector3[] Slots =
        {
            new Vector3(-.47f, TrayTop, .10f), new Vector3(-.24f, TrayTop, .10f), new Vector3(0f, TrayTop, .10f),
            new Vector3(.24f, TrayTop, .10f), new Vector3(.47f, TrayTop, .10f), new Vector3(.36f, TrayTop, -.13f),
            new Vector3(-.36f, TrayTop, -.13f),
        };
        private const float OpenSeconds = .7f, LidAngle = 100f;

        public enum State { Locked, Ready, Open, Taken }

        private sealed class Site
        {
            public int camp;
            public ForestPoint point;
            public Transform chest, lid, lockPart, contents;
            public Quaternion lidShut, lidOpen;
            public Map01EnemyController[] guards;
            public bool open, shownTaken, wasClear, known;
            public float openedAt = -1;
        }

        private readonly List<Site> sites = new List<Site>();
        private readonly Dictionary<ForestPoint, Site> byPoint = new Dictionary<ForestPoint, Site>();
        private Map01Mission mission;
        private Map01Inventory inventory;
        private Map01ItemCatalog catalog;

        public int Count => sites.Count;
        public ForestPoint Point(int index) => sites[index].point;
        public int CampOf(ForestPoint point) => byPoint[point].camp;
        public Transform ChestOf(ForestPoint point) => byPoint[point].chest;
        public bool LockShown(ForestPoint point) => byPoint[point].lockPart != null && byPoint[point].lockPart.gameObject.activeSelf;
        public bool LidOpen(ForestPoint point) { var s = byPoint[point]; return s.lid != null && Quaternion.Angle(s.lid.localRotation, s.lidOpen) < 1f; }
        /// <summary>Models of what is still lying in the opened chest.</summary>
        public int ContentsShown(ForestPoint point) { var s = byPoint[point]; return s.contents == null ? 0 : s.contents.childCount; }

        /// <summary>Lays the chests out. Called while the mission sets itself up, so a save restored a moment
        /// later finds them among the points.</summary>
        public static Map01CampChests Attach(Map01Mission owner)
        {
            var existing = owner.GetComponent<Map01CampChests>();
            if (existing != null) return existing;
            var catalog = Map01ItemCatalog.Load();
            if (catalog == null || catalog.chest == null) return null;
            var added = owner.gameObject.AddComponent<Map01CampChests>();
            added.mission = owner;
            added.inventory = owner.GetComponent<Map01Inventory>();
            added.catalog = catalog;
            added.Spawn();
            return added;
        }

        /// <summary>Where camp <paramref name="number"/>'s chest stands, and which way its front faces.</summary>
        public static bool PlaceFor(int number, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            var camp = GameObject.Find("Enemy outpost " + number);
            if (camp == null) return false;
            position = OnGround(camp.transform.TransformPoint(ChestLocal));
            // The model's front is its +Z; the courtyard is the camp's -Z.
            rotation = Quaternion.Euler(0, camp.transform.eulerAngles.y + 180f, 0);
            return true;
        }

        private void Spawn()
        {
            var root = new GameObject("Camp chests").transform;
            foreach (var number in Camps)
            {
                if (!PlaceFor(number, out var position, out var rotation)) continue;
                var chest = Instantiate(catalog.chest, position, rotation, root);
                chest.name = $"Iron chest C{number}";
                var site = new Site { camp = number, chest = chest.transform };
                site.lid = Find(chest.transform, "Lid");
                site.lockPart = Find(chest.transform, "Lock");
                if (site.lid != null) PrepareLid(site);
                var body = Find(chest.transform, "Body");
                if (body != null && body.TryGetComponent<Renderer>(out var shell))
                {
                    var box = chest.AddComponent<BoxCollider>();
                    box.center = chest.transform.InverseTransformPoint(shell.bounds.center);
                    box.size = Vector3.Scale(shell.localBounds.size, body.lossyScale);
                }
                var point = chest.AddComponent<ForestPoint>();
                point.id = IdPrefix + number;
                point.kind = ForestPointKind.Chest;
                point.radius = 2f;
                point.restockSeconds = 0f;
                point.items = Map01Logistics.For(mission).ContainerLoot(Tag(number));
                site.point = point;
                sites.Add(site);
                byPoint[point] = site;
                mission.RegisterPoint(point);
                point.label = Prompt(point);
            }
        }

        private static Transform Find(Transform under, string name)
        {
            // All the models share one .blend, so Blender numbers repeated names: "Lid.001".
            foreach (var t in under.GetComponentsInChildren<Transform>(true))
                if (t.name == name || t.name.StartsWith(name + ".")) return t;
            return null;
        }

        /// <summary>Which way about its hinge the lid opens: the way that lifts its front edge.</summary>
        private static void PrepareLid(Site site)
        {
            site.lidShut = site.lid.localRotation;
            float best = float.NegativeInfinity;
            foreach (float angle in new[] { LidAngle, -LidAngle })
            {
                site.lid.localRotation = site.lidShut * Quaternion.AngleAxis(angle, Vector3.right);
                float height = site.lid.GetComponent<Renderer>() is Renderer r ? r.bounds.center.y : 0f;
                if (height > best) { best = height; site.lidOpen = site.lid.localRotation; }
            }
            site.lid.localRotation = site.lidShut;
        }

        private static Vector3 OnGround(Vector3 point)
        {
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.collider is TerrainCollider || hit.collider.GetComponentInParent<Animator>() == null && hit.normal.y > .7f) return hit.point;
            return point;
        }

        // ---- the camp ----------------------------------------------------------------------------------

        /// <summary>The guards of a camp: its outpost guards, nearest camp first (as Map01Scouting counts them).</summary>
        public static Map01EnemyController[] GuardsOf(int number)
        {
            var camps = Enumerable.Range(1, 3).Select(n => GameObject.Find("Enemy outpost " + n)).ToArray();
            var mine = camps[number - 1];
            if (mine == null) return System.Array.Empty<Map01EnemyController>();
            return FindObjectsByType<Map01EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(g => g.name.StartsWith("Outpost guard "))
                .Where(g => camps.Where(c => c != null).OrderBy(c => (c.transform.position - g.transform.position).sqrMagnitude).First() == mine)
                .OrderBy(g => g.name, System.StringComparer.Ordinal).ToArray();
        }

        /// <summary>Who keeps a chest locked: its camp's guards; C3's (the command post, Map01C3Mission) is the commander's.</summary>
        private Map01EnemyController[] Guards(Site site)
        {
            if (site.camp == 3 && GetComponent<Map01C3Mission>() != null && GetComponent<Map01Quest>()?.CommanderEnemy is Map01EnemyController boss)
                return new[] { boss };
            return site.guards ??= GuardsOf(site.camp);
        }
        private bool CommanderKeeps(Site site) => site.camp == 3 && GetComponent<Map01C3Mission>() != null;
        public int GuardsLeft(ForestPoint point) => Guards(byPoint[point]).Count(g => g != null && g.Alive);

        public State StateOf(ForestPoint point)
        {
            var site = byPoint[point];
            if (point.used) return State.Taken;
            if (GuardsLeft(point) > 0) return State.Locked;
            return site.open ? State.Open : State.Ready;
        }

        /// <summary>What [E] offers at the chest, worded as Nam stands there.</summary>
        public string Prompt(ForestPoint point)
        {
            switch (StateOf(point))
            {
                case State.Locked: return CommanderKeeps(byPoint[point]) ? "Hòm sắt khóa — chìa ở chỉ huy địch" : $"Hòm sắt khóa — còn {GuardsLeft(point)} lính trại C{CampOf(point)}";
                case State.Ready: return "Mở hòm sắt";
                case State.Open: return "Lấy hết đồ trong hòm";
                default: return "Hòm trống";
            }
        }

        /// <summary>The chest's contents, as a list: "1 súng lục K-54, 16 đạn K-54".</summary>
        public string Listing(ForestPoint point) =>
            string.Join(", ", point.items.Select(i => i.count + " " + ForestInventory.Name(i.item_id).ToLowerInvariant() + Map01Drops.ConditionTag(mission, i.item_id, i.condition)));

        /// <summary>The run's seed was restored from a save: chests not yet emptied hold what this run rolls.</summary>
        public void Reroll()
        {
            foreach (var site in sites)
                if (site.point != null && !site.point.used) { site.point.items = Map01Logistics.For(mission).ContainerLoot(Tag(site.camp)); if (site.open) ShowContents(site); }
        }

        /// <summary>[E] at a chest. Locked: says why. Ready: opens it. Open: takes everything. Returns whether anything happened.</summary>
        public bool Use(ForestPoint point)
        {
            if (point == null || !byPoint.TryGetValue(point, out var site)) return false;
            switch (StateOf(point))
            {
                case State.Locked:
                    mission.Say(CommanderKeeps(site) ? "Hòm sắt khóa chặt. Chìa nằm ở chỉ huy địch trong nhà chỉ huy." : $"Hòm sắt khóa chặt. Hạ hết lính trại C{site.camp} (còn {GuardsLeft(point)}) rồi quay lại mở.", 4);
                    return false;
                case State.Ready:
                    site.open = true; site.openedAt = Time.time;
                    if (site.lockPart != null) site.lockPart.gameObject.SetActive(false);
                    ShowContents(site);
                    mission.Say($"Trong hòm: {Listing(point)}. [E] để lấy hết.", 7);
                    Map01NamActions.For(mission)?.Play(Map01NamActions.OpenChest, 269f / 30f / 2.6f, true);
                    point.label = Prompt(point);
                    return true;
                case State.Open:
                    // Dismiss the contents preview before receiving; keep any new full-bag warning.
                    if (mission.Dialogue != null && mission.Dialogue.StartsWith("Trong hòm: ")) mission.Say(null, 0);
                    foreach (var item in point.items) inventory.Receive(item.item_id, item.count, item.condition);
                    point.MarkLooted();
                    Show(site);
                    Map01NamActions.For(mission)?.Play(Map01NamActions.PickUp, 288f / 30f / 2.4f, true);
                    point.label = Prompt(point);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Lays what the chest holds on its tray: a long gun along the back, the rest along the front.</summary>
        private void ShowContents(Site site)
        {
            if (site.contents != null) Destroy(site.contents.gameObject);
            site.contents = new GameObject("Contents").transform;
            site.contents.SetParent(site.chest, false);
            var random = new System.Random(31 * site.camp);
            int slot = 0;
            foreach (var item in site.point.items)
            {
                var model = catalog.Model(item.item_id);
                if (model == null) continue;
                var shown = Instantiate(model, site.contents, false);
                shown.name = item.item_id;
                shown.transform.localScale *= catalog.Scale(item.item_id);
                foreach (var c in shown.GetComponentsInChildren<Collider>()) Destroy(c);
                bool longGun = item.item_id == "rifle_m16" || item.item_id == "rifle_mosin" || item.item_id == "rifle_standard";
                if (longGun) shown.transform.localPosition = LongSlot;
                else
                {
                    // More than the tray has slots (a lucky roll): the rest lie on top of the first ones.
                    shown.transform.localPosition = Slots[slot % Slots.Length] + Vector3.up * (.06f * (slot / Slots.Length));
                    slot++;
                    shown.transform.localRotation = Quaternion.Euler(0, (float)(random.NextDouble() * 40 - 20), 0);
                }
            }
        }

        /// <summary>Brings the chest's look in line with its state: shut (locked or not), open with its goods, or open and empty.</summary>
        private void Show(Site site)
        {
            bool taken = site.point.used;
            site.shownTaken = taken;
            if (taken) site.open = true;
            if (site.lockPart != null) site.lockPart.gameObject.SetActive(!site.open);
            if (site.contents != null && (taken || !site.open)) { Destroy(site.contents.gameObject); site.contents = null; }
            if (site.lid != null && site.openedAt < 0) site.lid.localRotation = site.open ? site.lidOpen : site.lidShut;
        }

        private void Update()
        {
            foreach (var site in sites)
            {
                if (site.point == null) continue; // The map is being unloaded.
                // A save being loaded: an opened chest not yet emptied shuts again; a taken one comes back
                // open and empty, one given back (not used in that save) shut and locked.
                if (Map01SaveSystem.IsRestoring && site.open && !site.point.used) { site.open = false; site.openedAt = -1; Show(site); }
                if (site.point.used != site.shownTaken) { if (!site.point.used) site.open = false; site.openedAt = -1; Show(site); }
                if (site.openedAt >= 0 && site.lid != null)
                {
                    float t = Mathf.Clamp01((Time.time - site.openedAt) / OpenSeconds);
                    site.lid.localRotation = Quaternion.Slerp(site.lidShut, site.lidOpen, Mathf.SmoothStep(0, 1, t));
                    if (t >= 1) site.openedAt = -1;
                }
                bool clear = GuardsLeft(site.point) == 0;
                // The last guard of a camp falls: tell the player once. Not for a camp found clear on load.
                if (site.known && clear && !site.wasClear && !site.point.used && !Map01SaveSystem.IsRestoring && Time.timeSinceLevelLoad > 3f)
                    mission.Say(CommanderKeeps(site) ? "Đã có chìa của chỉ huy. Hòm sắt C3 mở được — đến lấy đồ." : $"Trại C{site.camp} đã sạch lính. Hòm sắt cạnh bạt tiếp tế đã mở được — đến lấy đồ.", 7);
                site.wasClear = clear; site.known = true;
                site.point.label = Prompt(site.point);
            }
        }

        /// <summary>An unlocked chest still holding something: the minimap marks it.</summary>
        public IEnumerable<Vector3> Waiting() =>
            sites.Where(s => s.point != null && !s.point.used && GuardsLeft(s.point) == 0).Select(s => s.chest.position);
    }
}
