using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1's shared state: content data, player/companion/camera references, the
    /// pause/stopped gate, world points (loot/workbench/hide/cover/...), and noise/damage
    /// dispatch. Everything else — quest, inventory, HUD, save, input — is its own sibling
    /// component on this GameObject and reads this one rather than each other directly.
    /// Replaces the old ForestMission god-class; this piece only keeps state that genuinely has
    /// to be shared.
    /// </summary>
    public sealed class Map01Mission : MonoBehaviour
    {
        public Transform player, hung;
        public Camera gameCamera;
        public Vector2 mapMin = new Vector2(-45, -60), mapMax = new Vector2(45, 88);
        public TextAsset balanceJson, contentBundle;

        public ForestSettings Settings { get; private set; }
        public ForestWeapon Weapon { get; private set; }
        public ForestArchetype GuardData { get; private set; }
        public ForestBundle Bundle { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool Alarmed { get; set; }
        public bool Hidden { get; private set; }
        public bool InventoryOpen { get; private set; }
        public bool MapOpen { get; private set; }
        public bool Crouched { get; set; }
        public float PlaySeconds { get; private set; }
        public string Dialogue { get; private set; }
        public float DialogueUntil { get; private set; }
        public bool Cinematic { get; set; }
        public bool Paused => paused;
        public void SetPaused(bool value) { paused = value; Time.timeScale = value ? 0 : 1; }
        public PlayerController ModernPlayer { get; private set; }
        public PlayerCombat ModernCombat { get; private set; }
        public Health ModernHealth { get; private set; }
        public ShadowVale.Gameplay.Skills.PlayerSkills Skills { get; private set; }
        public Map01EnemyController[] Enemies { get; private set; } = Array.Empty<Map01EnemyController>();
        /// <summary>Foliage the guards cannot see through.</summary>
        public Map01SightCover SightCover { get; private set; }
        public bool Stopped => (ModernHealth != null ? ModernHealth.IsDead : hp <= 0) || Stage == Map01Quest.CompleteStage || paused || Cinematic || Map01SaveSystem.IsRestoring
            || (rescue != null && rescue.HungDown) || (scouting != null && scouting.FailedRun);
        /// <summary>What blocks sight. Cached: guards ask for it every frame, and GetMask allocates.</summary>
        public int ObstructionMask => obstructionMask != 0 ? obstructionMask
            : obstructionMask = LayerMask.GetMask("Default", "Obstacle", "Cover", "VisionBlocker");
        private int obstructionMask;
        public int Stage => quest != null ? quest.Stage : 0;
        public readonly List<ForestPoint> Points = new List<ForestPoint>();
        /// <summary>The points Nam can use — all but hiding spots and cover: a dozen or so, against the
        /// thousands of bushes in Points, for the checks that run every frame.</summary>
        public readonly List<ForestPoint> Interactables = new List<ForestPoint>();
        private readonly List<ForestPoint> hideSpots = new List<ForestPoint>();
        private const float HideCell=4;
        private readonly Dictionary<Vector2Int,List<ForestPoint>> hideGrid=new();
        private float hp, stamina;
        private bool paused;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private Map01Rescue rescue;
        private Map01Scouting scouting;

        [Tooltip("The river's centre line, x = offset + a1·sin(f1·z) + a2·sin(f2·z). Within halfWidth of it " +
                 "and with his feet below wadeBelowY, Nam is wading. Set by the map's layout tool.")]
        public float riverOffset = 8, riverAmplitude1 = 12, riverFrequency1 = .041f, riverAmplitude2 = 4, riverFrequency2 = .105f;
        public float riverHalfWidth = 5, wadeBelowY = .12f;

        public bool IsWading => player != null && player.position.y < wadeBelowY &&
            Mathf.Abs(player.position.x - (riverOffset + riverAmplitude1 * Mathf.Sin(player.position.z * riverFrequency1)
                + riverAmplitude2 * Mathf.Sin(player.position.z * riverFrequency2))) < riverHalfWidth;
        public float MovementSurfaceMultiplier => IsWading ? .68f : 1f;
        public bool CameraInputEnabled => IsInitialized && !Stopped && !InventoryOpen && !MapOpen;
        public float PlayerHealth => hp;
        public float Stamina => stamina;

        private void Awake()
        {
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
            if (player == null)
                player = FindFirstObjectByType<PlayerController>()?.transform;
            // Unity objects can retain a managed wrapper after the asset was deleted.
            // Use Unity's null check before accessing TextAsset.text.
            if (balanceJson == null || contentBundle == null)
            {
                FailInitialization("Assign valid balanceJson and contentBundle TextAssets in the Inspector.");
                return;
            }
            try
            {
                Settings = JsonUtility.FromJson<ForestSettings>(balanceJson.text);
                Bundle = JsonUtility.FromJson<ForestBundle>(contentBundle.text);
            }
            catch (ArgumentException exception)
            {
                FailInitialization("Invalid mission JSON: " + exception.Message);
                return;
            }
            Weapon = Bundle?.weapons?.FirstOrDefault(w => w != null && w.id == "rifle_standard");
            GuardData = Bundle?.enemy_archetypes?.FirstOrDefault(e => e != null && e.id == "grunt");
            if (Settings == null || Weapon == null || GuardData == null)
            {
                FailInitialization("Mission JSON must contain settings, rifle_standard and grunt.");
                return;
            }
            if (player == null || hung == null || gameCamera == null)
            {
                FailInitialization("Assign player, hung and gameCamera in the Inspector.");
                return;
            }
            foreach (var point in FindObjectsByType<ForestPoint>(FindObjectsSortMode.None)) RegisterPoint(point);
            // Laid out now, not baked into Map 1.unity: a save restored a moment later finds them among the points.
            Map01StoneSites.Attach(this);
            Map01CampChests.Attach(this); // An iron chest in each enemy camp (Map01ItemCatalog).
            hp = Settings.playerHP; stamina = Settings.stamina;
            Skills = player.GetComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();
            if (Skills == null) Skills = player.gameObject.AddComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();
            Skills.LevelChanged += OnSkillLevelChanged;
            inventory.SeedStartingLoadout(Settings.startingStones);
            // Added at runtime like the scouting order; guards ask it from their first Update.
            SightCover = GetComponent<Map01SightCover>();
            if (SightCover == null) SightCover = gameObject.AddComponent<Map01SightCover>();
            IsInitialized = true;
        }

        private void Start()
        {
            if (!IsInitialized) return;
            rescue = GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake, like the scouting order.
            scouting = GetComponent<Map01Scouting>();
            ModernPlayer = player.GetComponent<PlayerController>();
            ModernCombat = player.GetComponent<PlayerCombat>();
            ModernHealth = player.GetComponent<Health>();
            Enemies = FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
            if (ModernPlayer != null)
            {
                // The bench works on through its queue while Nam walks off, so crafting does not hold him.
                ModernPlayer.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible;
                ModernPlayer.SprintAllowed = () => stamina > 2;
            }
            if (ModernHealth != null) ModernHealth.KeepCheckpointCorpse();
            if (ModernCombat != null)
            {
                ModernCombat.DrawsOwnWeaponHud = true; // Map01Hud.Combat shows the slots on keys 1-4.
                ModernCombat.SlotPressed = inventory.OnSlotKey; // 1 long gun, 2 pistol, 3 knife, 4 next throwable (Map01Inventory.Bag).
                ModernCombat.GunKilled += OnGunKilled;
                var stones = GetComponent<Map01StoneThrow>(); // Added by Map01PlayerInteraction.Awake.
                ModernCombat.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible && !inventory.SuppressFire
                    && !scouting.Binoculars && !stones.Aiming; // Hands are on the binoculars or a stone, not the rifle.
                ModernCombat.TryConsumeRound = () =>
                {
                    if (inventory.Count("ammo_rifle") <= 0) { Say("Hết đạn — nhặt đạn ở thùng vật tư gần điểm xuất phát hoặc lục xác lính [E]. Đổi sang dao: phím 3.", 3); return false; }
                    inventory.Spend("ammo_rifle", 1);
                    EmitNoise(player.position, Weapon.noise_radius, Map01NoiseKind.Gunshot);
                    return true;
                };
                // With a magazine in the weapon the pack is a reserve, drawn once per reload
                // rather than once per shot, so the two hooks below replace the one above.
                ModernCombat.ReserveRounds = () => inventory.Count("ammo_rifle");
                ModernCombat.DrawRounds = wanted =>
                {
                    int granted = Mathf.Min(wanted, inventory.Count("ammo_rifle"));
                    if (granted > 0) inventory.Spend("ammo_rifle", granted);
                    else Say("Hết đạn — nhặt thêm đạn hoặc đổi vũ khí bằng phím 1 – 3.", 2);
                    return granted;
                };
                // The shot's noise used to ride along with spending the round. It now hangs off
                // the attack itself, so a reload does not announce the player thirty times over.
                ModernCombat.Attacked -= OnPlayerAttacked;
                ModernCombat.Attacked += OnPlayerAttacked;
            }
            if (gameCamera != null && gameCamera.TryGetComponent<ThirdPersonCamera>(out var cameraRig))
                {
                cameraRig.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible;
                var riverSurface = GameObject.Find("Winding river")?.GetComponent<Renderer>();
                float waterHeight = riverSurface != null ? riverSurface.bounds.max.y : wadeBelowY;
                cameraRig.SurfaceCameraFloor = () => IsWading ? waterHeight + .35f : (float?)null;
            }
            if (player.TryGetComponent(out PlayerFootsteps footsteps))
            {
                // The river is already tracked for the movement slowdown, so the footsteps ask
                // the same question rather than raycasting for a surface underneath.
                footsteps.SurfaceProbe = () => IsWading ? FootSurface.Water : FootSurface.Grass;
                footsteps.Stepped -= NotifyFootstep;
                footsteps.Stepped += NotifyFootstep;
            }
            foreach (var enemy in Enemies) enemy.BindMission(this);
            Map01OpeningCutscene.Attach(this);
            GetComponent<Map01Story>()?.Initialize();
            GetComponent<Map01Quest>()?.EnsureCommander();
            Map01HungVisual.Attach(this);
            if(GetComponent<Map01FoliageReveal>()==null)gameObject.AddComponent<Map01FoliageReveal>();
            if(GetComponent<Map01BushTakedown>()==null)gameObject.AddComponent<Map01BushTakedown>();
        }

        private void FailInitialization(string reason)
        {
            Debug.LogError("Map01Mission could not initialize. " + reason, this);
            enabled = false;
        }

        public Material trailMaterial;
        public void RegisterPoint(ForestPoint point)
        {
            Points.Add(point);
            if (point.kind == ForestPointKind.Hide) {
                hideSpots.Add(point);
                var p=point.transform.position;
                for(int x=Mathf.FloorToInt((p.x-point.radius)/HideCell);x<=Mathf.FloorToInt((p.x+point.radius)/HideCell);x++)
                for(int z=Mathf.FloorToInt((p.z-point.radius)/HideCell);z<=Mathf.FloorToInt((p.z+point.radius)/HideCell);z++){
                    var key=new Vector2Int(x,z);if(!hideGrid.TryGetValue(key,out var list))hideGrid[key]=list=new();list.Add(point);
                }
            }
            else if (point.kind != ForestPointKind.Cover) Interactables.Add(point);
        }
        public void UnregisterPoint(ForestPoint point)
        {
            Points.Remove(point); hideSpots.Remove(point); Interactables.Remove(point);
            if(point.kind==ForestPointKind.Hide)foreach(var list in hideGrid.Values)list.Remove(point);
        }
        public void AddEnemy(Map01EnemyController enemy) { if (enemy != null && !Enemies.Contains(enemy)) Enemies = Enemies.Append(enemy).ToArray(); }
        public void RemoveEnemy(Map01EnemyController enemy) => Enemies=Enemies.Where(e=>e!=null&&e!=enemy).ToArray();
        /// <summary>A brief visible streak — thrown stones, tracers — using the shared trail material.</summary>
        public void Trace(Vector3 start, Vector3 end, Color color)
        {
            var trail = new GameObject("Transient trail");
            var line = trail.AddComponent<LineRenderer>();
            line.sharedMaterial = trailMaterial;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = .055f;
            line.endWidth = .015f;
            line.startColor = color;
            line.endColor = color;
            Destroy(trail, .12f);
        }
        public void Say(string text, float seconds = 7) {
            var speech=DialogueVoice.For(gameObject);
            speech.CanSpeak=()=>!Cinematic&&!ForestMenu.Visible;
            speech.LineStarted=line=>{
                Dialogue=GameplayCaption(line.caption);DialogueUntil=Time.time+line.clip.length+.2f;
                if(line.caption.StartsWith("Hùng") && hung!=null)hung.GetComponentInChildren<Map01HungVisual>()?.Speak(line.clip.length+.2f);
            };
            Dialogue=GameplayCaption(text);DialogueUntil=Time.time+seconds;
            if(string.IsNullOrEmpty(text))speech.Stop();
            float spoken=string.IsNullOrEmpty(text)?0:speech.SpeakText(text);
            if(!string.IsNullOrEmpty(text))DialogueUntil=Time.time+Mathf.Max(seconds,spoken+.25f);
        }
        public static string GameplayCaption(string text)
        {
            if(string.IsNullOrEmpty(text))return text;
            // Keep objective/interaction notices; ambient character speech is audio only.
            return text.StartsWith("Lính")||text.StartsWith("Sĩ quan")||text.StartsWith("Hùng:")||text.StartsWith("Hùng (")||text.StartsWith("Nam:")?null:text;
        }
        public void ReleaseForNextMap() {
            GetComponent<DialogueVoice>()?.Stop();
            ModernCombat.Attacked-=OnPlayerAttacked;
            if(player.TryGetComponent(out PlayerFootsteps footsteps)) {
                footsteps.Stepped-=NotifyFootstep;footsteps.SurfaceProbe=null;
            }
        }
        private void OnPlayerAttacked(WeaponKind kind, Vector3 position)
        {
            if (kind == WeaponKind.Rifle) EmitNoise(position, Weapon.noise_radius, Map01NoiseKind.Gunshot);
        }

        /// <summary>A noise every guard within <paramref name="radius"/> goes to check; returns how many heard it.</summary>
        public int EmitNoise(Vector3 position, float radius, Map01NoiseKind kind = Map01NoiseKind.Other)
        {
            int heard = 0;
            foreach (var enemy in Enemies) if (enemy != null && enemy.Hear(position, radius, kind)) heard++;
            return heard;
        }
        public void Damage(float amount)
        {
            if (Stopped) return;
            if (ModernHealth != null) { ModernHealth.TakeDamage(amount, player.position, gameObject); hp = ModernHealth.Current; }
            else hp = Mathf.Max(0, hp - amount);
        }
        public void SetInventoryOpen(bool open) { InventoryOpen = open; MapOpen = false; BenchOpen = false; }
        /// <summary>The workbench screen (Map01Hud.Bench). It is the bag's panel slot showing the bench, so
        /// everything that stops play while the bag is open stops it here too.</summary>
        public bool BenchOpen { get; private set; }
        public void SetBenchOpen(bool open) { InventoryOpen = open; BenchOpen = open; MapOpen = false; }
        public void SetMapOpen(bool open) { MapOpen = open; InventoryOpen = false; BenchOpen = false; }
        public bool CloseGameplayPanel()
        {
            if (!InventoryOpen && !MapOpen) return false;
            InventoryOpen = MapOpen = BenchOpen = false; return true;
        }
        public void UpdateHiddenState()
        {
            Hidden = false;
            if (!Crouched) return;
            Vector3 at = player.position;
            Hidden=TryGetHideSpot(at,out _);
        }
        public bool TryGetHideSpot(Vector3 at,out ForestPoint found) {
            found=null;float best=float.PositiveInfinity;
            var key=new Vector2Int(Mathf.FloorToInt(at.x/HideCell),Mathf.FloorToInt(at.z/HideCell));
            if(!hideGrid.TryGetValue(key,out var nearby))return false;
            foreach(var spot in nearby)if(spot!=null){float distance=(spot.transform.position-at).sqrMagnitude;
                if(distance<spot.radius*spot.radius&&distance<best){best=distance;found=spot;}}
            return found!=null;
        }
        /// <summary>
        /// How far a footstep carries, by how the player is moving. Sneaking, walking and
        /// running have to be three distinct choices rather than "running is loud, everything
        /// else is free": before this, only a sprint made any sound at all, so walking through
        /// an outpost in plain view of nobody was silent and the crouch key bought nothing.
        /// </summary>
        private static float FootstepNoiseRadius(MoveStance stance) => stance switch
        {
            MoveStance.Sneak => 3f,
            MoveStance.Sprint => 16f,
            _ => 9f,
        };

        /// <summary>Wading splashes. Multiplies whichever stance the player is in.</summary>
        private const float WadingNoiseScale = 1.45f;

        /// <summary>Landing from a jump is a single heavy footfall, loud whatever the stance.</summary>
        private const float LandingNoiseRadius = 12f;

        /// <summary>
        /// Turns one footfall into a noise the guards can hear.
        /// <para>
        /// The rate limit is deliberately per-step rather than per-second: the step itself is
        /// already paced by distance travelled, so a sprint emits more often than a walk without
        /// a timer needing to know how fast anyone is going.
        /// </para>
        /// </summary>
        public void NotifyFootstep(PlayerFootsteps.Footstep step)
        {
            if (Stopped || Hidden) return;

            float radius = step.Landing ? LandingNoiseRadius : FootstepNoiseRadius(step.Stance);
            if (step.Surface == FootSurface.Water) radius *= WadingNoiseScale;
            EmitNoise(step.Position, radius, step.Landing ? Map01NoiseKind.Landing : Map01NoiseKind.Footstep);
        }
        public void AdvancePlaySeconds() { if (!Stopped) PlaySeconds += Time.deltaTime; }
        public void SetPlaySeconds(float value) => PlaySeconds = value;
        public void SetHealth(float value) => hp = value;
        public void SetStamina(float value) => stamina = Mathf.Clamp(value, 0, Settings.stamina);
        public void RestoreHealthFromSave(float value)
        {
            hp = value;
            ModernHealth?.RestoreHealth(hp);
        }

        private void Restart()
        {
            if (GetComponent<Map01SaveSystem>().RestartCurrentMission()) return;
            Time.timeScale = 1; UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void Update()
        {
            if (ModernHealth != null) hp = ModernHealth.Current;
            AdvancePlaySeconds();
            // restockAt is not saved, so a checkpoint load refills restockable points straight away.
            foreach (var point in Interactables) // Only these restock; the bushes need no look.
                if (point.used && point.restockSeconds > 0 && Time.time >= point.restockAt) point.used = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (ForestMenu.Visible) return;
            if (kb.enterKey.wasPressedThisFrame && rescue != null && Stage <= Map01Quest.EscortStage && (rescue.Failed || hp<=0)) rescue.Retry();
            else if (kb.enterKey.wasPressedThisFrame && hp <= 0) Restart();
            else if(kb.enterKey.wasPressedThisFrame && Stage==Map01Quest.CompleteStage) Map01Extraction.Get(this)?.ContinueToMap2();
            else if (kb.enterKey.wasPressedThisFrame && rescue != null && rescue.HungDown) rescue.Retry();
            else if (kb.enterKey.wasPressedThisFrame && scouting != null && scouting.FailedRun) scouting.Restart();
        }

        private void OnGunKilled(GameObject target)
        {
            // A lethal boss hit can start a cinematic inside Health.TakeDamage, before this callback.
            if (Map01SaveSystem.IsRestoring || Skills.MissionFailed || (ModernHealth != null && ModernHealth.IsDead)
                || target.GetComponent<Map01EnemyController>() == null) return;
            Skills.Award(ShadowVale.Gameplay.Skills.SkillKind.Shooting);
        }
        private void OnSkillLevelChanged(ShadowVale.Gameplay.Skills.SkillKind kind, int level)
        {
            string name = kind == ShadowVale.Gameplay.Skills.SkillKind.Shooting ? "Bắn súng"
                : kind == ShadowVale.Gameplay.Skills.SkillKind.Stealth ? "Lén lút" : "Kỹ thuật";
            Say($"{name} đạt cấp {level}! Xem thông số trong Túi đồ → Kĩ Năng.", 4);
        }
        private void OnDestroy()
        {
            if (Skills != null) Skills.LevelChanged -= OnSkillLevelChanged;
            if (ModernCombat != null) {
                ModernCombat.GunKilled -= OnGunKilled;
                if (ModernCombat.SlotPressed?.Target is Map01Inventory) ModernCombat.SlotPressed = null;
            }
        }
        private void OnDisable() { Time.timeScale = 1; }
    }
}
