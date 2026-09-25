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
        public bool Paused => paused;
        public void SetPaused(bool value) { paused = value; Time.timeScale = value ? 0 : 1; }
        public PlayerController ModernPlayer { get; private set; }
        public PlayerCombat ModernCombat { get; private set; }
        public Health ModernHealth { get; private set; }
        public Map01EnemyController[] Enemies { get; private set; } = Array.Empty<Map01EnemyController>();
        public bool Stopped => (ModernHealth != null ? ModernHealth.IsDead : hp <= 0) || Stage == Map01Quest.CompleteStage || paused || Map01SaveSystem.IsRestoring
            || (rescue != null && rescue.HungDown);
        public int ObstructionMask => LayerMask.GetMask("Default", "Obstacle", "Cover", "VisionBlocker");
        public int Stage => quest != null ? quest.Stage : 0;
        public readonly List<ForestPoint> Points = new List<ForestPoint>();
        private float hp, stamina;
        private bool paused;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private Map01Rescue rescue;

        public bool IsWading => player != null && player.position.y < .12f &&
            Mathf.Abs(player.position.x - (8 + 12 * Mathf.Sin(player.position.z * .041f) + 4 * Mathf.Sin(player.position.z * .105f))) < 5f;
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
            Points.AddRange(FindObjectsByType<ForestPoint>(FindObjectsSortMode.None));
            hp = Settings.playerHP; stamina = Settings.stamina;
            inventory.SeedStartingLoadout(Settings.startingStones);
            IsInitialized = true;
        }

        private void Start()
        {
            if (!IsInitialized) return;
            rescue = GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake.
            ModernPlayer = player.GetComponent<PlayerController>();
            ModernCombat = player.GetComponent<PlayerCombat>();
            ModernHealth = player.GetComponent<Health>();
            Enemies = FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
            if (ModernPlayer != null)
            {
                ModernPlayer.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible && !inventory.IsCrafting;
                ModernPlayer.SprintAllowed = () => stamina > 2;
            }
            if (ModernHealth != null) ModernHealth.KeepCheckpointCorpse();
            if (ModernCombat != null)
            {
                ModernCombat.DrawsOwnWeaponHud = true; // Map01Hud.Combat shows the weapons on keys 1/2/3.
                var scouting = GetComponent<Map01Scouting>(); // Added by Map01Quest.Awake.
                var stones = GetComponent<Map01StoneThrow>(); // Added by Map01PlayerInteraction.Awake.
                ModernCombat.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible && !inventory.IsCrafting && !inventory.SuppressFire
                    && !scouting.Binoculars && !stones.Aiming; // Hands are on the binoculars or a stone, not the rifle.
                ModernCombat.TryConsumeRound = () =>
                {
                    if (inventory.Count("ammo_rifle") <= 0) { Say("Hết đạn — nhặt đạn ở thùng vật tư gần điểm xuất phát hoặc lục xác lính [E]. Đổi sang dao: phím 2.", 3); return false; }
                    inventory.Spend("ammo_rifle", 1);
                    EmitNoise(player.position, Weapon.noise_radius);
                    return true;
                };
                // With a magazine in the weapon the pack is a reserve, drawn once per reload
                // rather than once per shot, so the two hooks below replace the one above.
                ModernCombat.ReserveRounds = () => inventory.Count("ammo_rifle");
                ModernCombat.DrawRounds = wanted =>
                {
                    int granted = Mathf.Min(wanted, inventory.Count("ammo_rifle"));
                    if (granted > 0) inventory.Spend("ammo_rifle", granted);
                    else Say("Hết đạn — nhặt thêm đạn hoặc đổi vũ khí bằng 1 / 2 / 3.", 2);
                    return granted;
                };
                // The shot's noise used to ride along with spending the round. It now hangs off
                // the attack itself, so a reload does not announce the player thirty times over.
                ModernCombat.Attacked -= OnPlayerAttacked;
                ModernCombat.Attacked += OnPlayerAttacked;
            }
            if (gameCamera != null && gameCamera.TryGetComponent<ThirdPersonCamera>(out var cameraRig))
                cameraRig.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible;
            if (player.TryGetComponent(out PlayerFootsteps footsteps))
            {
                // The river is already tracked for the movement slowdown, so the footsteps ask
                // the same question rather than raycasting for a surface underneath.
                footsteps.SurfaceProbe = () => IsWading ? FootSurface.Water : FootSurface.Grass;
                footsteps.Stepped -= NotifyFootstep;
                footsteps.Stepped += NotifyFootstep;
            }
            foreach (var enemy in Enemies) enemy.BindMission(this);
        }

        private void FailInitialization(string reason)
        {
            Debug.LogError("Map01Mission could not initialize. " + reason, this);
            enabled = false;
        }

        public Material trailMaterial;
        public void RegisterPoint(ForestPoint point) => Points.Add(point);
        public void UnregisterPoint(ForestPoint point) => Points.Remove(point);
        public void AddEnemy(Map01EnemyController enemy) => Enemies = Enemies.Append(enemy).ToArray();
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
        public void Say(string text, float seconds = 7) { Dialogue = text; DialogueUntil = Time.time + seconds; }
        private void OnPlayerAttacked(WeaponKind kind, Vector3 position)
        {
            if (kind == WeaponKind.Rifle) EmitNoise(position, Weapon.noise_radius);
        }

        /// <summary>A noise every guard within <paramref name="radius"/> goes to check; returns how many heard it.</summary>
        public int EmitNoise(Vector3 position, float radius)
        {
            int heard = 0;
            foreach (var enemy in Enemies) if (enemy.Hear(position, radius)) heard++;
            return heard;
        }
        public void Damage(float amount)
        {
            if (Stopped) return;
            if (ModernHealth != null) { ModernHealth.TakeDamage(amount, player.position, gameObject); hp = ModernHealth.Current; }
            else hp = Mathf.Max(0, hp - amount);
        }
        public void SetInventoryOpen(bool open) { InventoryOpen = open; MapOpen = false; }
        public void SetMapOpen(bool open) { MapOpen = open; InventoryOpen = false; }
        public bool CloseGameplayPanel()
        {
            if (!InventoryOpen && !MapOpen) return false;
            InventoryOpen = MapOpen = false; return true;
        }
        public void UpdateHiddenState()
        {
            Hidden = Crouched && Points.Any(p => p.kind == ForestPointKind.Hide && Vector3.Distance(player.position, p.transform.position) < p.radius);
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
            EmitNoise(step.Position, radius);
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

        private void Restart() { Time.timeScale = 1; UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); }

        private void Update()
        {
            if (ModernHealth != null) hp = ModernHealth.Current;
            AdvancePlaySeconds();
            // restockAt is not saved, so a checkpoint load refills restockable points straight away.
            foreach (var point in Points)
                if (point.used && point.restockSeconds > 0 && Time.time >= point.restockAt) point.used = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (ForestMenu.Visible) return;
            if (kb.enterKey.wasPressedThisFrame && (hp <= 0 || Stage == Map01Quest.CompleteStage)) Restart();
            else if (kb.enterKey.wasPressedThisFrame && rescue != null && rescue.HungDown) rescue.Retry();
        }

        private void OnDisable() { Time.timeScale = 1; }
    }
}
