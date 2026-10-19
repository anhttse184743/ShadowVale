using ShadowVale.AI.Perception;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Point 3 of Map 1, the C3 command post, as the approved mission design "Đột kích Sở chỉ huy C3" (17/10/2026) lays
    /// it out on the approved C3_MainGarrison_Art: Nam alone takes the camp apart layer by layer and kills its commander.
    /// <list type="bullet">
    /// <item>Six guards with a job each (Resources/Missions/Map01C3Layout.json): a sentry on the observation tower who
    /// watches the south slope, the gate guard by the kẻng, a guard walking the courtyard, one on the headquarters'
    /// porch who steps round the back to the generator for a smoke, and two patrols outside the fence whose beats pass
    /// through bushes Nam can pull them into.</item>
    /// <item>The commander is one actor from the start (never cloned at a stage change), walking a 75 s round inside
    /// the headquarters: at the map table with his back to the door, at the side desk, out on the porch.</item>
    /// <item>Every 90 s the gate guard calls the patrols on the radio; one gone silent puts the camp on guard.</item>
    /// <item>When the camp is alarmed the nearest guard strikes the kẻng (the hanging iron bar of the gate) and the
    /// three men asleep in the barracks come out. The commander's call at half health strikes it too. Nam can take the
    /// striker beforehand: then only one sleeper, the nearest, hears the commander shout, eight seconds later. While the
    /// field telephone line behind the camp is intact, two more men run in from the jetty posts 25 s after the call.</item>
    /// <item>A knife from behind does not kill the commander: he takes 35% and breaks free into the fight.</item>
    /// <item>Nobody chases Nam more than 40 m beyond the fence (the story plan: no pursuit deep into the forest).</item>
    /// <item>From the commander order on, the scouts' Mosin with five rounds lies at the south-east shoulder of the
    /// hill, the one place that sees the tower's platform beyond the Mosin's 30 m report.</item>
    /// </list>
    /// The commander's own fighting (cover, grenades, falling back) is not here: <see cref="CallReinforcements"/> is the
    /// hook his behaviour calls, and <see cref="CoverPoints"/> the positions it may use.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class Map01C3Mission : MonoBehaviour
    {
        // ---- data ------------------------------------------------------------------------------------------------
        [Serializable] public sealed class Layout
        {
            public float[] fenceMin, fenceMax, anchor, nestA, mosinCache, cover;
            public Role[] roles; public Route commander; public WaryPost[] waryPosts; public Sleeper[] sleepers;
            public Runners runners; public Kieng kieng; public Phone phone;
            public float checkInSeconds, kiengDelaySeconds, sleeperDelayWithoutKieng, ambushDamage, leashMeters, sleeperHearMeters, nearMeters;
            public int sleepersWithoutKieng;
        }
        /// <summary>A post of the garrison and the guard who holds it. <see cref="copy"/>: no guard of Map 1 is free for it
        /// (rbl_east_3, the old camp's gate sentry, was switched off when the approved C3 art came in), so one is made.</summary>
        [Serializable] public sealed class Role { public string role, guard; public float[] post, points, stops, yaws; public float yaw, vision, deck; public bool copy; }
        [Serializable] public sealed class Route { public float[] points, stops, yaws; }
        [Serializable] public sealed class WaryPost { public string role; public float[] post; public float yaw; }
        [Serializable] public sealed class Sleeper { public string name, barracks; public float[] door, inside; }
        [Serializable] public sealed class Runners { public int count; public float[] from; public float delaySeconds; }
        [Serializable] public sealed class Kieng { public float[] frame, use; }
        [Serializable] public sealed class Phone { public float[] pole, use, hqEnd, far; }

        /// <summary>What the save keeps of the post.</summary>
        [Serializable] public sealed class State
        {
            public bool kiengRemoved, phoneCut, wary, kiengRung, called, ambushed, runnersSent;
            public float checkInRemaining = -1f, runnersIn = -1f, sleeperIn = -1f;
            public bool[] sleepersAwake;
        }

        public const string ResourcePath = "Missions/Map01C3Layout";
        public const string KiengId = "c3_kieng", PhoneId = "c3_phone", CacheId = "c3_mosin_cache";
        public const string ReserveRoot = "C3 / mission reserve";

        public Layout Data { get; private set; }
        public State Current { get; private set; } = new State();
        /// <summary>Where the commander may take cover (for his fighting behaviour), on the ground.</summary>
        public IReadOnlyList<Vector3> CoverPoints => cover;
        /// <summary>True: this component calls <see cref="CallReinforcements"/> itself when the commander falls to half
        /// health. A commander behaviour that makes the call on its own turns this off.</summary>
        public bool AutoCallAtHalfHealth { get; set; } = true;
        public bool Alarmed { get; private set; }
        public bool Wary => Current.wary;
        public bool KiengRemoved => Current.kiengRemoved;
        public bool PhoneCut => Current.phoneCut;
        public float CheckInRemaining => Current.checkInRemaining;
        public Map01EnemyController Guard(string role) => guards.TryGetValue(role, out var g) ? g : null;
        public Map01EnemyController Commander => quest != null ? quest.CommanderEnemy : null;
        public IReadOnlyList<Map01EnemyController> Sleepers => sleepers;
        public IReadOnlyList<Map01EnemyController> RunnerGuards => runners;
        public ForestPoint KiengPoint => kiengPoint;
        public ForestPoint PhonePoint => phonePoint;
        public ForestPoint CachePoint => cachePoint;

        private Map01Mission mission;
        private Map01Quest quest;
        private readonly Dictionary<string, Map01EnemyController> guards = new Dictionary<string, Map01EnemyController>();
        private readonly List<Map01EnemyController> sleepers = new List<Map01EnemyController>(), runners = new List<Map01EnemyController>();
        private readonly List<Vector3> cover = new List<Vector3>();
        private ForestPoint kiengPoint, phonePoint, cachePoint;
        private GameObject striker, cacheRoot;
        private Transform kiengSoundOrigin;
        private LineRenderer hqWire;
        private Vector3 poleTop;
        private Collider ground;
        private float kiengAt = -1f;
        private bool ready;

        public static Map01C3Mission For(Map01Mission mission) => mission != null ? mission.GetComponent<Map01C3Mission>() : null;

        // ---- set-up --------------------------------------------------------------------------------------------------

        private void Start()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            if (mission == null || !mission.IsInitialized || quest == null) { enabled = false; return; }
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) { enabled = false; return; }
            Data = JsonUtility.FromJson<Layout>(asset.text);
            ground = GameObject.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).FirstOrDefault(c => c.name.StartsWith("Continuous hills", StringComparison.Ordinal));
            Current.checkInRemaining = Data.checkInSeconds;
            Current.sleepersAwake = Array.Empty<bool>();
            PostGuards();
            PostCommander();
            MakeReserve();
            BuildKieng();
            BuildPhone();
            BuildCache();
            for (int i = 0; i + 1 < Data.cover.Length; i += 2) cover.Add(At(Data.cover[i], Data.cover[i + 1]));
            mission.NoiseEmitted += OnNoise;
            ready = true;
        }

        private void OnDestroy() { if (mission != null) mission.NoiseEmitted -= OnNoise; }

        /// <summary>The ground at (x, z): the terrain mesh, or the highest walkable surface found there.</summary>
        public Vector3 At(float x, float z)
        {
            var from = new Vector3(x, 120f, z);
            if (ground != null && ground.Raycast(new Ray(from, Vector3.down), out var hit, 200f)) return hit.point;
            return Physics.Raycast(from, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : new Vector3(x, 8.25f, z);
        }
        private static float Yaw(float yaw) => yaw <= -900f ? float.NaN : yaw;
        private Vector3[] Points(float[] flat)
        {
            var list = new List<Vector3>();
            for (int i = 0; flat != null && i + 1 < flat.Length; i += 2) list.Add(At(flat[i], flat[i + 1]));
            return list.ToArray();
        }

        private void PostGuards()
        {
            foreach (var role in Data.roles)
            {
                var guard = role.copy ? CopyGuard(role.guard, At(role.post[0], role.post[1]), true)
                    : mission.Enemies.FirstOrDefault(e => e != null && e.name == role.guard);
                if (guard == null) continue;
                guards[role.role] = guard;
                bool tower = role.deck > 0;
                var post = tower ? new Vector3(role.post[0], role.deck, role.post[1]) : At(role.post[0], role.post[1]);
                guard.Repost(post, role.yaw, tower ? null : Points(role.points), role.stops, role.yaws?.Select(Yaw).ToArray(), role.vision, tower);
            }
        }

        private void PostCommander()
        {
            quest.EnsureCommander();
            var boss = quest.CommanderEnemy;
            if (boss == null) return;
            var route = Points(Data.commander.points);
            if (route.Length == 0) return;
            boss.Repost(route[0], Data.commander.yaws[0], route, Data.commander.stops, Data.commander.yaws.Select(Yaw).ToArray());
        }

        private Map01EnemyController template;
        /// <summary>
        /// A copy of a camp guard under <see cref="ReserveRoot"/>, made at start so the save always finds it under the same
        /// name. On duty (<paramref name="onDuty"/>) it is one of the garrison from the first frame (a guard of the
        /// operation, as the other C3 guards are); otherwise it waits off the field until called.
        /// </summary>
        private Map01EnemyController CopyGuard(string name, Vector3 at, bool onDuty)
        {
            template ??= mission.Enemies.Where(e => e != null && e.name.StartsWith("Outpost guard ", StringComparison.Ordinal))
                .OrderBy(e => e.SaveId, StringComparer.Ordinal).FirstOrDefault();
            if (template == null) return null;
            var root = GameObject.Find(ReserveRoot)?.transform ?? new GameObject(ReserveRoot).transform;
            var go = Instantiate(template.gameObject, at, Quaternion.Euler(0, 180, 0), root);
            go.name = name;
            var guard = go.GetComponent<Map01EnemyController>();
            guard.Configure(Array.Empty<Vector3>());
            guard.carriesSupplyKey = false; guard.operationIndex = onDuty ? 3 : 0; guard.suppressOperationLoot = false;
            guard.BindMission(mission);
            if (onDuty) mission.AddEnemy(guard); else go.SetActive(false);
            return guard;
        }

        /// <summary>The sleepers and the runners, kept off the field until they are called.</summary>
        private void MakeReserve()
        {
            foreach (var s in Data.sleepers) { var g = CopyGuard(s.name, At(s.inside[0], s.inside[1]), false); if (g != null) sleepers.Add(g); }
            for (int i = 0; i < Data.runners.count; i++)
            {
                var g = CopyGuard("C3 runner " + (i + 1), At(Data.runners.from[0] + i * 1.5f, Data.runners.from[1]), false);
                if (g != null) runners.Add(g);
            }
            Current.sleepersAwake = new bool[sleepers.Count];
        }

        // ---- props -----------------------------------------------------------------------------------------------------

        private Material[] materials;
        /// <summary>A material of the approved C3 art by name (weathered timber, black steel, canvas), so the props match it.</summary>
        private Material Mat(string name, Color fallback)
        {
            materials ??= GameObject.Find("C3 / Mission art")?.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null).Distinct().ToArray() ?? Array.Empty<Material>();
            var found = materials.FirstOrDefault(m => m.name.StartsWith(name, StringComparison.Ordinal));
            if (found != null) return found;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = fallback, name = name + " (stand-in)" };
        }
        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, Quaternion? rotation = null, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition; go.transform.localScale = scale; go.transform.localRotation = rotation ?? Quaternion.identity;
            if (!solid) Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        private ForestPoint Point(GameObject host, string id, ForestPointKind kind, string label, float radius)
        {
            var point = host.AddComponent<ForestPoint>();
            point.id = id; point.kind = kind; point.label = label; point.radius = radius; point.restockSeconds = 0f;
            point.items = Array.Empty<ForestIngredient>();
            mission.RegisterPoint(point);
            return point;
        }

        /// <summary>The kẻng: an iron bar hung from a timber frame inside the gate, the striker hanging beside it.</summary>
        private void BuildKieng()
        {
            var timber = Mat("WeatheredTimber", new Color(.36f, .27f, .18f));
            var steel = Mat("BlackSteel", new Color(.12f, .12f, .12f));
            var rope = Mat("Linen", new Color(.62f, .56f, .42f));
            var root = new GameObject("C3 kẻng").transform;
            root.position = At(Data.kieng.frame[0], Data.kieng.frame[1]);
            root.rotation = Quaternion.Euler(0, 0, 0);
            Part(PrimitiveType.Cube, "Kẻng post", root, new Vector3(-.5f, .95f, 0), new Vector3(.11f, 1.9f, .11f), timber, solid: true);
            Part(PrimitiveType.Cube, "Kẻng post", root, new Vector3(.5f, .95f, 0), new Vector3(.11f, 1.9f, .11f), timber, solid: true);
            Part(PrimitiveType.Cube, "Kẻng beam", root, new Vector3(0, 1.86f, 0), new Vector3(1.2f, .1f, .1f), timber);
            Part(PrimitiveType.Cylinder, "Kẻng rope", root, new Vector3(0, 1.66f, 0), new Vector3(.015f, .1f, .015f), rope);
            kiengSoundOrigin = Part(PrimitiveType.Cube, "Kẻng (iron bar)", root, new Vector3(0, 1.28f, 0), new Vector3(.07f, .56f, .07f), steel).transform;
            striker = Part(PrimitiveType.Cylinder, "Kẻng striker", root, new Vector3(.44f, 1.12f, .09f), new Vector3(.03f, .2f, .03f), steel, Quaternion.Euler(0, 0, 12));
            var use = new GameObject("Kẻng striker point");
            use.transform.position = At(Data.kieng.use[0], Data.kieng.use[1]);
            kiengPoint = Point(use, KiengId, ForestPointKind.Sabotage, "Tháo dùi kẻng", 1.6f);
        }

        /// <summary>The field telephone line: a pole behind the rear fence, the wire to the headquarters' roof and on
        /// along the north path towards the jetty posts.</summary>
        private void BuildPhone()
        {
            var timber = Mat("WeatheredTimber", new Color(.36f, .27f, .18f));
            var wireMat = Mat("BlackSteel", new Color(.08f, .08f, .08f));
            var root = new GameObject("C3 telephone pole").transform;
            root.position = At(Data.phone.pole[0], Data.phone.pole[1]);
            Part(PrimitiveType.Cylinder, "Pole", root, new Vector3(0, 2.8f, 0), new Vector3(.18f, 2.8f, .18f), timber, solid: true);
            Part(PrimitiveType.Cube, "Cross arm", root, new Vector3(0, 5.35f, 0), new Vector3(.9f, .08f, .08f), timber);
            poleTop = root.position + new Vector3(.32f, 5.4f, 0);
            var hq = new Vector3(Data.phone.hqEnd[0], Data.phone.hqEnd[1], Data.phone.hqEnd[2]);
            hqWire = Wire("Wire to headquarters", poleTop, hq, wireMat);
            var far = At(Data.phone.far[0], Data.phone.far[1]) + Vector3.up * 5f;
            Wire("Wire to the jetty posts", poleTop + new Vector3(-.64f, 0, 0), far, wireMat);
            var use = new GameObject("Telephone line point");
            use.transform.position = At(Data.phone.use[0], Data.phone.use[1]);
            phonePoint = Point(use, PhoneId, ForestPointKind.Sabotage, "Cắt dây điện thoại", 1.6f);
        }
        private static LineRenderer Wire(string name, Vector3 a, Vector3 b, Material material)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.widthMultiplier = .025f; line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Sag(line, a, b);
            return line;
        }
        private static void Sag(LineRenderer line, Vector3 a, Vector3 b, float sag = .6f)
        {
            const int n = 12;
            line.positionCount = n;
            for (int i = 0; i < n; i++) { float t = i / (n - 1f); line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1 - t)); }
        }

        /// <summary>The scouts' Mosin and five rounds on a canvas sheet behind a rock at the south-east shoulder of the
        /// hill (nest A), shown from the commander order on.</summary>
        private void BuildCache()
        {
            cacheRoot = new GameObject("C3 scouts' Mosin cache");
            var spot = At(Data.mosinCache[0], Data.mosinCache[1]);
            cacheRoot.transform.position = spot;
            var nest = At(Data.nestA[0], Data.nestA[1]);
            var tower = Guard("tower");
            var facing = Vector3.ProjectOnPlane((tower != null ? tower.transform.position : At(Data.anchor[0], Data.anchor[1])) - nest, Vector3.up);
            if (facing.sqrMagnitude > .01f) cacheRoot.transform.rotation = Quaternion.LookRotation(facing);
            // A rock to rest the rifle on, copied from the map's own boulders.
            var boulder = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name.StartsWith("05_MossBoulder", StringComparison.Ordinal) || t.name.StartsWith("01_BroadBoulder", StringComparison.Ordinal));
            if (boulder != null)
            {
                var rock = Instantiate(boulder.gameObject, nest + facing.normalized * .9f, Quaternion.Euler(0, 37f, 0), cacheRoot.transform);
                rock.name = "Nest rock";
                var bounds = Bounds(rock);
                if (bounds.size.y > .01f) rock.transform.localScale *= .75f / bounds.size.y;
            }
            var canvas = Mat("OliveCanvas", new Color(.33f, .35f, .22f));
            Part(PrimitiveType.Cube, "Canvas sheet", cacheRoot.transform, new Vector3(0, .01f, 0), new Vector3(1.35f, .02f, .62f), canvas);
            var catalog = Map01ItemCatalog.Load();
            Lay(catalog, "rifle_mosin", new Vector3(0, .03f, -.08f));
            Lay(catalog, "ammo_sniper", new Vector3(.45f, .03f, .2f));
            cachePoint = Point(cacheRoot, CacheId, ForestPointKind.Loot, "Mosin trinh sát giấu — 5 viên", 1.6f);
            cachePoint.items = Map01Logistics.For(mission).ContainerLoot(CacheId);
            cacheRoot.SetActive(false);
        }
        private void Lay(Map01ItemCatalog catalog, string id, Vector3 local)
        {
            var model = catalog != null ? catalog.Model(id) : null;
            if (model == null) return;
            var shown = Instantiate(model, cacheRoot.transform, false);
            shown.name = id;
            shown.transform.localScale *= catalog.Scale(id);
            foreach (var c in shown.GetComponentsInChildren<Collider>()) Destroy(c);
            Map01Drops.LayFlat(shown, cacheRoot.transform.TransformPoint(local));
        }
        private static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }

        // ---- play ------------------------------------------------------------------------------------------------------

        private IEnumerable<Map01EnemyController> Garrison()
        {
            foreach (var g in guards.Values) yield return g;
            foreach (var s in sleepers) if (s.gameObject.activeSelf) yield return s;
            foreach (var r in runners) if (r.gameObject.activeSelf) yield return r;
            if (Commander != null) yield return Commander;
        }
        private bool Near => mission.player != null && Vector3.Distance(mission.player.position, At(Data.anchor[0], Data.anchor[1])) < Data.nearMeters;
        private float OutsideFence(Vector3 p)
        {
            float dx = Mathf.Max(Data.fenceMin[0] - p.x, 0f, p.x - Data.fenceMax[0]);
            float dz = Mathf.Max(Data.fenceMin[1] - p.z, 0f, p.z - Data.fenceMax[1]);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void Update()
        {
            if (!ready || !mission.IsInitialized || Map01SaveSystem.IsRestoring || mission.Cinematic) return;
            if (cacheRoot != null && !cacheRoot.activeSelf && !cachePoint.used && quest.Stage >= Map01Quest.BossStage) cacheRoot.SetActive(true);
            if (mission.Stopped) return;

            bool alarmed = Garrison().Any(g => g != null && g.Alive && g.Engaged);
            if (alarmed && !Alarmed && !Current.kiengRemoved && !Current.kiengRung && kiengAt < 0) kiengAt = Time.time + Data.kiengDelaySeconds;
            Alarmed = alarmed;
            if (kiengAt >= 0 && Time.time >= kiengAt) { kiengAt = -1f; if (!Current.kiengRemoved && StrikerAlive()) RingKieng(); }

            CheckIn();
            HalfHealthCall();
            Countdowns();
            Leash();
        }

        /// <summary>Someone of the camp is standing to strike the kẻng: the gate guard, the courtyard guard or the porch guard.</summary>
        private bool StrikerAlive() => new[] { "gate", "rover", "porch" }.Select(Guard).Any(g => g != null && g.Alive);

        private void CheckIn()
        {
            var caller = Guard("gate");
            if (Current.wary || Alarmed || caller == null || !caller.Alive || caller.Engaged) return;
            Current.checkInRemaining -= Time.deltaTime;
            if (Current.checkInRemaining > 0) return;
            Current.checkInRemaining = Data.checkInSeconds;
            var silent = new[] { "patrolSouth", "patrolNorth" }.Select(Guard).Where(g => g != null && !g.Alive).ToList();
            if (silent.Count > 0) GoWary();
            else if (Near) mission.Say(Map01C3Dialogue.CheckIn, 3);
        }

        /// <summary>A patrol did not answer: the camp stands on guard (the remaining patrols hold before the gate, the
        /// courtyard and porch guards hold the headquarters' door, the commander keeps to his desk).</summary>
        public void GoWary()
        {
            if (Current.wary) return;
            Current.wary = true;
            if (Near) mission.Say(Map01C3Dialogue.MissingPatrol, 5);
            ApplyWary();
        }
        private void ApplyWary()
        {
            foreach (var w in Data.waryPosts)
            {
                var g = Guard(w.role);
                if (g != null && g.Alive && !g.Engaged) g.Repost(At(w.post[0], w.post[1]), w.yaw);
            }
            var boss = Commander;
            if (boss != null && boss.Alive && !boss.Engaged && Data.commander.points.Length >= 4)
                boss.Repost(At(Data.commander.points[2], Data.commander.points[3]), Data.commander.yaws[1]);
        }

        private void RingKieng()
        {
            if (Current.kiengRung) return;
            Current.kiengRung = true;
            // The approved clip contains all three strikes. Full 3D placement retains the pool's distance rolloff.
            Map01OperationAudio.Play("c3_kieng", kiengSoundOrigin != null ? kiengSoundOrigin.position : At(Data.kieng.frame[0], Data.kieng.frame[1]), spatialBlend: 1f);
            mission.Say("\"Keng! Keng! Keng!\" — kẻng báo động. Lính trong dãy nhà bật dậy!", 4);
            mission.EmitNoise(At(Data.kieng.frame[0], Data.kieng.frame[1]), 45f, NoiseKind.Other);
            for (int i = 0; i < sleepers.Count; i++) Wake(i, true);
        }

        private void HalfHealthCall()
        {
            var boss = Commander;
            if (!AutoCallAtHalfHealth || Current.called || boss == null || !boss.Alive) return;
            var health = boss.GetComponent<Health>();
            if (health != null && health.Current <= health.Max * .5f) CallReinforcements();
        }

        /// <summary>
        /// The commander calls his men (once): the kẻng is struck if it still can be, and every sleeper comes out; if Nam
        /// took the striker, only the nearest sleeper hears the shout, a few seconds later. With the telephone line intact,
        /// runners from the jetty posts are on their way. Returns how many men answer now or soon.
        /// </summary>
        public int CallReinforcements()
        {
            if (Current.called) return 0;
            Current.called = true;
            var boss = Commander;
            if (boss != null && boss.Alive) mission.Say(Map01C3Dialogue.EnemyCall, 3);
            int answering = 0;
            if (!Current.kiengRemoved)
            {
                answering += Current.sleepersAwake.Count(a => !a);
                if (!Current.kiengRung) RingKieng();
            }
            else if (Current.sleepersAwake.Any(a => !a)) { Current.sleeperIn = Data.sleeperDelayWithoutKieng; answering += Mathf.Min(Data.sleepersWithoutKieng, Current.sleepersAwake.Count(a => !a)); }
            if (!Current.phoneCut && !Current.runnersSent) { Current.runnersIn = Data.runners.delaySeconds; answering += runners.Count; }
            return answering;
        }

        private void Countdowns()
        {
            if (Current.sleeperIn >= 0 && (Current.sleeperIn -= Time.deltaTime) < 0)
            {
                // The one nearest the commander hears him.
                var boss = Commander;
                int best = -1; float bestDistance = float.MaxValue;
                for (int i = 0; i < sleepers.Count; i++)
                {
                    if (Current.sleepersAwake[i]) continue;
                    float d = boss != null ? Vector3.Distance(boss.transform.position, sleepers[i].transform.position) : i;
                    if (d < bestDistance) { bestDistance = d; best = i; }
                }
                for (int n = 0; n < Data.sleepersWithoutKieng && best >= 0; n++) { Wake(best, true); best = Array.IndexOf(Current.sleepersAwake, false); }
            }
            if (Current.runnersIn >= 0 && (Current.runnersIn -= Time.deltaTime) < 0) SendRunners();
        }

        private void Wake(int index, bool fight)
        {
            if (index < 0 || index >= sleepers.Count || Current.sleepersAwake[index]) return;
            Current.sleepersAwake[index] = true;
            var guard = sleepers[index];
            var door = Data.sleepers[index].door;
            guard.gameObject.SetActive(true);
            guard.Repost(At(door[0], door[1]), 180f);
            mission.AddEnemy(guard);
            if (fight && mission.player != null) guard.Alert(mission.player.position);
        }

        private void SendRunners()
        {
            if (Current.runnersSent) return;
            Current.runnersSent = true;
            foreach (var r in runners)
            {
                r.gameObject.SetActive(true);
                r.Repost(r.transform.position, 90f);
                mission.AddEnemy(r);
                if (mission.player != null) r.Alert(mission.player.position);
            }
            if (Near) mission.Say("Có thêm lính chạy tới từ đường mòn phía Bắc — dây điện thoại vẫn còn.", 4);
        }

        private void OnNoise(Vector3 position, float radius, NoiseKind kind)
        {
            if (!ready || Map01SaveSystem.IsRestoring || kind != NoiseKind.Gunshot && radius < 25f) return;
            for (int i = 0; i < sleepers.Count; i++)
            {
                if (Current.sleepersAwake[i]) continue;
                var inside = Data.sleepers[i].inside;
                if (Vector3.Distance(position, At(inside[0], inside[1])) <= Data.sleeperHearMeters) Wake(i, true);
            }
        }

        /// <summary>The story plan: no pursuit deep into the forest. Past the leash the garrison walks back to its posts.</summary>
        private void Leash()
        {
            if (mission.player == null || OutsideFence(mission.player.position) <= Data.leashMeters) return;
            foreach (var g in Garrison()) if (g != null && g.Alive && g.Engaged && !g.IsBoss) g.StandDown();
        }

        // ---- Nam's actions ---------------------------------------------------------------------------------------------

        /// <summary>[E] at the kẻng or the telephone pole.</summary>
        public bool Use(ForestPoint point)
        {
            if (point == null || point.used) return false;
            if (point == kiengPoint)
            {
                Current.kiengRemoved = true; point.MarkLooted();
                if (striker != null) striker.SetActive(false);
                mission.Say("Đã tháo dùi kẻng. Nếu báo động, trại không gõ kẻng gọi lính ngủ dậy được nữa.", 5);
                Map01NamActions.For(mission)?.Play(Map01NamActions.PickUp, 1.4f, true);
                StartCoroutine(SpeakAfterInteraction(Map01C3Dialogue.MalletRemoved));
                return true;
            }
            if (point == phonePoint)
            {
                Current.phoneCut = true; point.MarkLooted();
                DropWire();
                mission.Say("Đã cắt dây điện thoại. Chỉ huy không gọi được lính ở các chốt đường bến tới nữa.", 5);
                Map01NamActions.For(mission)?.Play(Map01NamActions.PickUp, 1.6f, true);
                StartCoroutine(SpeakAfterInteraction(Map01C3Dialogue.PhoneCut));
                return true;
            }
            return false;
        }
        private System.Collections.IEnumerator SpeakAfterInteraction(string caption)
        {
            var actions = Map01NamActions.For(mission);
            while (actions != null && actions.Busy) yield return null;
            // Do not cut a story line short, or queue a stale remark after a new cinematic.
            float deadline = Time.time + 5f;
            var voice = mission.GetComponent<DialogueVoice>();
            while (voice != null && voice.HasSpeech && Time.time < deadline)
            {
                if (mission.Stopped || mission.Cinematic) yield break;
                yield return null;
            }
            if (!mission.Stopped && !mission.Cinematic && (voice == null || !voice.HasSpeech))
                mission.Say(caption, 4);
        }
        private void DropWire()
        {
            if (hqWire == null) return;
            var below = poleTop; below.y = At(poleTop.x, poleTop.z + 1.2f).y + .05f;
            Sag(hqWire, poleTop, below + Vector3.forward * 1.2f, .1f);
        }

        /// <summary>Nam behind the commander with the knife, before he has seen him: an ambush can start.</summary>
        public bool CanAmbush()
        {
            var boss = Commander;
            if (boss == null || !boss.Alive || boss.Engaged || Current.ambushed || mission.ModernCombat == null
                || mission.ModernCombat.EquippedKind != WeaponKind.Knife || mission.player == null) return false;
            var delta = Vector3.ProjectOnPlane(mission.player.position - boss.transform.position, Vector3.up);
            float reach = mission.Skills != null ? mission.Skills.TakedownRange(1.6f) : 1.6f;
            if (delta.magnitude > reach || Vector3.Angle(boss.transform.forward, delta) < 120f) return false;
            var from = mission.player.position + Vector3.up;
            return !Physics.Linecast(from, boss.transform.position + Vector3.up, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform);
        }

        /// <summary>The knife from behind: the commander is no man to die quietly. He takes a heavy wound and breaks free.</summary>
        public bool TryAmbush()
        {
            if (!CanAmbush()) return false;
            var boss = Commander;
            if (!Map01NamActions.For(mission).PlayTakedown(boss.transform)) return false;
            Current.ambushed = true;
            var health = boss.GetComponent<Health>();
            health.TakeDamage(health.Max * Data.ambushDamage, boss.transform.position + Vector3.up * 1.2f, mission.player.gameObject);
            mission.Say("Chỉ huy địch vùng thoát khỏi lưỡi dao — hắn bị thương nặng và lao ra ngoài!", 4);
            return true;
        }

        /// <summary>The run's loot seed came back from a save: an unopened cache holds what this run rolls.</summary>
        public void RerollCache()
        {
            if (cachePoint != null && !cachePoint.used) cachePoint.items = Map01Logistics.For(mission).ContainerLoot(CacheId);
        }

        // ---- the save ------------------------------------------------------------------------------------------------

        public State Capture()
        {
            var s = JsonUtility.FromJson<State>(JsonUtility.ToJson(Current));
            s.sleepersAwake = (bool[])Current.sleepersAwake?.Clone();
            return s;
        }

        /// <summary>Back to a saved state, before the guards' own snapshots are restored: a woken sleeper is back on the
        /// field so his snapshot finds him.</summary>
        public void Restore(State saved)
        {
            if (!ready) return;
            var s = saved ?? new State { checkInRemaining = Data.checkInSeconds };
            if (s.checkInRemaining < 0) s.checkInRemaining = Data.checkInSeconds;
            var awake = new bool[sleepers.Count];
            for (int i = 0; i < Mathf.Min(awake.Length, s.sleepersAwake?.Length ?? 0); i++) awake[i] = s.sleepersAwake[i];
            bool runnersOut = s.runnersSent;
            s.sleepersAwake = new bool[sleepers.Count];
            s.runnersSent = false;
            Current = s;
            kiengAt = -1f;
            for (int i = 0; i < sleepers.Count; i++)
            {
                if (awake[i]) Wake(i, false);
                else if (sleepers[i].gameObject.activeSelf) sleepers[i].gameObject.SetActive(false);
            }
            if (runnersOut) { Current.runnersSent = true; foreach (var r in runners) { r.gameObject.SetActive(true); mission.AddEnemy(r); } }
            if (striker != null) striker.SetActive(!s.kiengRemoved);
            if (s.phoneCut) DropWire(); else if (hqWire != null) Sag(hqWire, poleTop, new Vector3(Data.phone.hqEnd[0], Data.phone.hqEnd[1], Data.phone.hqEnd[2]));
            if (s.wary) ApplyWary();
        }

        // ---- the HUD ---------------------------------------------------------------------------------------------------

        /// <summary>A line for the HUD while Nam is near the post, or null.</summary>
        public string StatusLine()
        {
            if (!ready || !Near || quest.Stage < Map01Quest.BossStage || quest.Stage > Map01Quest.ReportBossStage) return null;
            if (CanAmbush()) return "Sau lưng chỉ huy — CHUỘT TRÁI để đánh úp";
            if (Alarmed) return Current.kiengRung ? "Trại C3 báo động — kẻng đã gõ" : "Trại C3 báo động";
            if (Current.wary) return "Trại C3 đang cảnh giác — tuần tra mất liên lạc";
            var caller = Guard("gate");
            return caller != null && caller.Alive ? $"Gác cổng điểm danh tuần tra sau {Mathf.CeilToInt(Current.checkInRemaining)} giây" : null;
        }
        /// <summary>The optional objectives, as the HUD lists them.</summary>
        public IEnumerable<(string text, bool done)> OptionalObjectives()
        {
            var tower = Guard("tower");
            yield return ("Hạ lính tháp canh", tower != null && !tower.Alive);
            yield return ("Tháo dùi kẻng ở cổng", Current.kiengRemoved);
            yield return ("Cắt dây điện thoại sau trại", Current.phoneCut);
        }
    }
}
