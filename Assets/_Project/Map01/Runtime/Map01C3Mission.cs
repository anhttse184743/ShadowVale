using ShadowVale.AI.Perception;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
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
    public sealed partial class Map01C3Mission : MonoBehaviour
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
        [Serializable] public sealed class Sleeper { public string name, barracks; public float[] door, inside; public float floorY, bedYaw; }
        [Serializable] public sealed class Runners { public int count; public float[] from; public float delaySeconds; }
        [Serializable] public sealed class Kieng { public float[] frame, use; }
        [Serializable] public sealed class Phone { public float[] pole, use, hqEnd, far; }

        /// <summary>What the save keeps of the post.</summary>
        [Serializable] public sealed class State
        {
            public bool kiengRemoved, phoneCut, wary, kiengRung, called, ambushed, runnersSent;
            public float checkInRemaining = -1f, runnersIn = -1f, sleeperIn = -1f;
            public bool[] sleepersAwake, sleepersKilled, sleepersExecuted;
            /// <summary>The guard running to strike the kẻng over a body he found (his SaveId; empty: nobody), and where it lies.</summary>
            public string bodyRunner = "";
            public float[] bodyAt;
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
            var animation = Map01BedAnimationData.Load();
            foreach (var s in Data.sleepers)
            {
                var g = CopyGuard(s.name, At(s.inside[0], s.inside[1]), false);
                if (g == null) continue;
                int index = sleepers.Count; sleepers.Add(g);
                if (animation != null)
                {
                    var floor = new Vector3(s.inside[0], s.floorY != 0 ? s.floorY : At(s.inside[0], s.inside[1]).y, s.inside[1]);
                    g.gameObject.AddComponent<Map01C3Sleeper>().Bind(mission, this, index, floor, Quaternion.Euler(0,s.bedYaw,0), animation);
                }
            }
            if (animation != null && GetComponent<Map01C3BedTakedown>() == null) gameObject.AddComponent<Map01C3BedTakedown>();
            for (int i = 0; i < Data.runners.count; i++)
            {
                var g = CopyGuard("C3 runner " + (i + 1), At(Data.runners.from[0] + i * 1.5f, Data.runners.from[1]), false);
                if (g != null) runners.Add(g);
            }
            Current.sleepersAwake = new bool[sleepers.Count];
            Current.sleepersKilled = new bool[sleepers.Count];
            Current.sleepersExecuted = new bool[sleepers.Count];
        }

        // ---- play ------------------------------------------------------------------------------------------------------

        /// <summary>Every man of the command post on the map now: the posts, the sleepers and runners called out, the commander.</summary>
        internal IEnumerable<Map01EnemyController> Garrison()
        {
            foreach (var g in guards.Values) yield return g;
            foreach (var s in sleepers) if (s.gameObject.activeSelf && s.GetComponent<Map01C3Sleeper>()?.BedOwned != true) yield return s;
            foreach (var r in runners) if (r.gameObject.activeSelf) yield return r;
            if (Commander != null) yield return Commander;
        }
        private bool Near => mission.player != null && Vector3.Distance(mission.player.position, At(Data.anchor[0], Data.anchor[1])) < Data.nearMeters;
        /// <summary>Inside the command post's fence (the commander's cover positions stay there). False before the layout loads.</summary>
        public bool InsideFence(Vector3 p) => Data != null && OutsideFence(p) <= 0f;
        /// <summary>The nearest walkable point inside the fence (a metre in from it) to <paramref name="p"/>; p itself when inside.</summary>
        public Vector3 ClampIntoFence(Vector3 p)
        {
            if (Data == null || OutsideFence(p) <= 0f) return p;
            const float inset = 1f;
            var c = new Vector3(Mathf.Clamp(p.x, Data.fenceMin[0] + inset, Data.fenceMax[0] - inset), p.y,
                                Mathf.Clamp(p.z, Data.fenceMin[1] + inset, Data.fenceMax[1] - inset));
            c = At(c.x, c.z);
            return NavMesh.SamplePosition(c, out var hit, 3f, NavMesh.AllAreas) && OutsideFence(hit.position) <= 0f ? hit.position : c;
        }
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

            BodyRunner();
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
            ApplyWary(walk: true);
        }
        /// <summary>On guard over a body: the same posts, but the men walk to them (it happens in front of Nam).</summary>
        private void GoWaryOverBody()
        {
            if (Current.wary) return;
            Current.wary = true;
            ApplyWary(true);
        }
        private void ApplyWary(bool walk = false)
        {
            foreach (var w in Data.waryPosts)
            {
                var g = Guard(w.role);
                if (g != null && g.Alive && !g.Engaged) g.Repost(At(w.post[0], w.post[1]), w.yaw, walk: walk);
            }
            var boss = Commander;
            if (boss != null && boss.Alive && !boss.Engaged && Data.commander.points.Length >= 4)
                boss.Repost(At(Data.commander.points[2], Data.commander.points[3]), Data.commander.yaws[1], walk: walk);
        }

        /// <param name="fight">The sleepers come out to fight Nam where he is (an alarm); false: to search round
        /// <paramref name="search"/> (a body found).</param>
        private void RingKieng(bool fight = true, Vector3? search = null)
        {
            if (Current.kiengRung) return;
            Current.kiengRung = true;
            // The approved clip contains all three strikes. Full 3D placement retains the pool's distance rolloff.
            Map01OperationAudio.Play("c3_kieng", kiengSoundOrigin != null ? kiengSoundOrigin.position : At(Data.kieng.frame[0], Data.kieng.frame[1]), spatialBlend: 1f);
            mission.Say("\"Keng! Keng! Keng!\" — kẻng báo động. Lính trong dãy nhà bật dậy!", 4);
            var kieng = At(Data.kieng.frame[0], Data.kieng.frame[1]);
            if (fight)
            {
                mission.EmitNoise(kieng, 45f, NoiseKind.Other);
                for (int i = 0; i < sleepers.Count; i++) Wake(i, true);
                return;
            }
            // Up before the noise: a sleeper woken by it (OnNoise) would otherwise come out fighting.
            for (int i = 0; i < sleepers.Count; i++) Wake(i, false);
            mission.EmitNoise(kieng, 45f, NoiseKind.Other);
            if (search.HasValue)
            {
                int k = 0;
                foreach (var s in sleepers) if (s.gameObject.activeSelf && s.Alive && !s.Engaged)
                {
                    var target=Map01BodyDiscovery.Around(search.Value,k++);
                    var bed=s.GetComponent<Map01C3Sleeper>();
                    if(bed!=null)bed.SearchAfterWake(target);else s.InspectBody(target);
                }
            }
        }
    }
    /// <summary>
    /// Who answers the C3 commander's call (BossBehaviour, at its reinforce_hp_threshold). The men of the command post
    /// not yet in the fight come first (never the guards of points 1 and 2, nor the jetty: the call is shouted, not
    /// radioed); the post's own reserve follows on its own terms (<see cref="Map01C3Mission.CallReinforcements"/>):
    /// the kẻng wakes the sleepers in the barracks (only one, and late, if Nam took its striker) and the runners come
    /// down the telephone line unless Nam cut it. No one is cloned out of thin air.
    /// </summary>
    public sealed class Map01C3Reinforcements : IReinforcementProvider
    {
        private readonly Map01C3Mission c3;
        public Map01C3Reinforcements(Map01C3Mission c3) { this.c3 = c3; }

        public IReadOnlyList<IEnemyAgent> Summon(IEnemyAgent boss, Vector3 playerPosition, int count, float radius, float now)
        {
            float r2 = radius * radius;
            var sent = c3.Garrison().Where(g => g != null && !ReferenceEquals(g, boss) && g.Alive && !g.IsBoss
                                              && !g.Brain.Memory.Engaged(now) && (g.Position - boss.Position).sqrMagnitude <= r2)
                .OrderBy(g => (g.Position - boss.Position).sqrMagnitude).ThenBy(g => ((IEnemyAgent)g).Id, StringComparer.Ordinal)
                .Take(Mathf.Max(0, count)).Cast<IEnemyAgent>().ToList();
            foreach (var g in sent) NearbyAgentsReinforcements.Send(g, playerPosition, now);
            c3.CallReinforcements(); // the reserve arrives on the kẻng's and the phone line's own timing
            return sent;
        }
    }
}
