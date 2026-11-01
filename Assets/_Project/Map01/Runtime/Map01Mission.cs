using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Perception;
using ShadowVale.AI.Tactical;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Telemetry;
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
    public sealed partial class Map01Mission : MonoBehaviour
    {
        /// <summary>The map's id in telemetry and research logs.</summary>
        public const string MapId = "map01";

        public Transform player, hung;
        [Tooltip("Authored allied commander position and facing in the shelter.")]
        public Transform alliedCommanderPost;
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
        public void SetPaused(bool value) { paused = value; RefreshTimeScale(); }
        public void RefreshTimeScale() { var clock = GetComponent<TutorialTimeControl>(); if (clock != null) clock.Apply(); else Time.timeScale = paused ? 0 : 1; }
        public IMapObjectives Objectives { get; private set; }
        public IBinoculars Binoculars { get; private set; }
        public System.Action RetryRequested { get; set; }
        public Func<bool> StopRequested { get; set; }
        public PlayerController ModernPlayer { get; private set; }
        public PlayerCombat ModernCombat { get; private set; }
        public Health ModernHealth { get; private set; }
        public ShadowVale.Gameplay.Skills.PlayerSkills Skills { get; private set; }
        public Map01EnemyController[] Enemies { get; private set; } = Array.Empty<Map01EnemyController>();
        /// <summary>Every guard registers here when bound; noises reach them through it, not through <see cref="Enemies"/>.</summary>
        public NoiseSystem Noise { get; } = new NoiseSystem();
        /// <summary>The player as the guards' sensors see him: crouch and hiding come from this mission.</summary>
        public PlayerStealthAdapter Stealth { get; private set; }
        /// <summary>Foliage the guards cannot see through.</summary>
        public Map01SightCover SightCover { get; private set; }
        public bool Stopped => (ModernHealth != null ? ModernHealth.IsDead : hp <= 0) || (Objectives != null && Objectives.IsComplete) || (StopRequested?.Invoke() ?? false) || paused || Cinematic || Map01SaveSystem.IsRestoring
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
        private readonly List<ForestPoint> coverPoints = new List<ForestPoint>();
        /// <summary>Placed cover positions a guard can run to on first contact.</summary>
        public IReadOnlyList<ForestPoint> CoverPoints => coverPoints;
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
            Objectives = GetComponents<MonoBehaviour>().OfType<IMapObjectives>().FirstOrDefault();
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
                Bundle = PublishedContent.Apply(JsonUtility.FromJson<ForestBundle>(contentBundle.text));
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
            if (player == null || (quest != null && hung == null) || gameCamera == null)
            {
                FailInitialization("Assign player, hung and gameCamera in the Inspector.");
                return;
            }
            Stealth = PlayerStealthAdapter.For(player.gameObject);
            Stealth.CrouchProbe = () => Crouched;
            Stealth.HiddenProbe = () => Hidden;
            // Shared services the map-agnostic AI tools (debug console, coordinator) resolve.
            ServiceLocator.Register(Noise);
            ServiceLocator.Register<IPlayerObservable>(Stealth);
            foreach (var point in FindObjectsByType<ForestPoint>(FindObjectsSortMode.None)) RegisterPoint(point);
            // Laid out now, not baked into Map 1.unity: a save restored a moment later finds them among the points.
            Map01StoneSites.Attach(this);
            Map01Logistics.For(this);     // The run's loot seed and the base store, before anything rolls loot.
            Map01CampChests.Attach(this); // An iron chest in each enemy camp (Map01ItemCatalog).
            // Point 3's command post (sets up in Start); Map 1's story only, not the tutorial.
            if (quest != null && GetComponent<Map01C3Mission>() == null) gameObject.AddComponent<Map01C3Mission>();
            if (quest != null && GetComponent<Map01FieldSupplies>() == null) gameObject.AddComponent<Map01FieldSupplies>();
            hp = Settings.playerHP; stamina = Settings.stamina;
            Skills = player.GetComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();
            if (Skills == null) Skills = player.gameObject.AddComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();
            Skills.LevelChanged += OnSkillLevelChanged;
            inventory.SeedStartingLoadout(Settings.startingStones);
            foreach (var item in Settings.startingItems ?? Array.Empty<ForestIngredient>()) inventory.Add(item.item_id, item.count);
            // Added at runtime like the scouting order; guards ask it from their first Update.
            SightCover = GetComponent<Map01SightCover>();
            if (SightCover == null) SightCover = gameObject.AddComponent<Map01SightCover>();
            // The squad coordinator's map: loads Resources/TacticalGraphs/Map 1, visibility through the same foliage.
            TacticalGraphHost.Attach(gameObject, ObstructionMask, SightCover);
            // Squad coordination (QUBO over that graph), configured by the content bundle's ai_settings
            // and this map's own coordination entry, if it has one.
            SquadCoordinator.Attach(gameObject, CoordinationSettings.ForScene(gameObject.scene.name));
            // The commander's grenades go off like Nam's F1 (Map01Blast), sized to their blast.
            ShadowVale.Gameplay.Enemies.Boss.Grenade.BlastEffect = CommanderBlast;
            // ...and are marked from the moment they leave his hand: the ring on the ground, an F1 in the air, the HUD pointer.
            if (GetComponent<Map01GrenadeWarning>() == null) gameObject.AddComponent<Map01GrenadeWarning>();
            // Anonymous session telemetry: listens to the shared events, ends when this map unloads.
            // A sandbox copy (Test Map 1 AI) reports apart, so test sessions never mix with Map 1's numbers.
            var telemetry = TelemetryService.Attach(gameObject, gameObject.scene.name == TutorialDirector.SceneName ? "map00_tutorial" : gameObject.scene.name == Map01SaveSystem.MainScene ? MapId : MapId + "_test");
            if (telemetry != null) telemetry.WeaponCode = WeaponCode;
            IsInitialized = true;
        }

        private void Start()
        {
            if (!IsInitialized) return;
            rescue = GetComponent<Map01Rescue>(); // Added by Map01Quest.Awake, like the scouting order.
            scouting = GetComponent<Map01Scouting>();
            Binoculars = GetComponents<MonoBehaviour>().OfType<IBinoculars>().FirstOrDefault();
            ModernPlayer = player.GetComponent<PlayerController>();
            ModernCombat = player.GetComponent<PlayerCombat>();
            inventory.ApplyPrimaryRifle(false);
            ModernHealth = player.GetComponent<Health>();
            Enemies = FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
            if (ModernPlayer != null)
            {
                // The bench works on through its queue while Nam walks off, so crafting does not hold him.
                ModernPlayer.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible && !InputFocus.OverlayOpen;
                ModernPlayer.SprintAllowed = () => stamina > 2;
            }
            if (ModernHealth != null)
            {
                ModernHealth.KeepCheckpointCorpse();
                ModernHealth.onDied.RemoveListener(OnPlayerDied);
                ModernHealth.onDied.AddListener(OnPlayerDied);
            }
            if (ModernCombat != null)
            {
                ModernCombat.DrawsOwnWeaponHud = true; // Map01Hud.Combat shows the slots on keys 1-4.
                ModernCombat.SlotPressed = inventory.OnSlotKey; // 1 long gun, 2 pistol, 3 knife, 4 stones (Map01Inventory.Bag).
                ModernCombat.GunKilled += OnGunKilled;
                ModernCombat.ShotPassesThrough = RoundPassesThrough;
                var stones = GetComponent<Map01StoneThrow>(); // Added by Map01PlayerInteraction.Awake.
                ModernCombat.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible && !inventory.SuppressFire && !InputFocus.OverlayOpen
                    && !inventory.ThrowableEquipped && !(Binoculars?.Binoculars ?? false) && (stones == null || !stones.PresentingThrow); // Hands are on the binoculars or a throwable, not the rifle.
                ModernCombat.TryConsumeRound = () =>
                {
                    if (inventory.Count(inventory.HeldAmmoId) <= 0) { Say("Hết đạn — nhặt đạn ở thùng vật tư gần điểm xuất phát hoặc lục xác lính [E]. Đổi sang dao: phím 3.", 3); return false; }
                    inventory.Spend(inventory.HeldAmmoId, 1);
                    EmitNoise(player.position, Weapon.noise_radius, NoiseKind.Gunshot);
                    return true;
                };
                // With a magazine in the weapon the pack is a reserve, drawn once per reload
                // rather than once per shot, so the two hooks below replace the one above.
                ModernCombat.ReserveRounds = () => inventory.Count(inventory.HeldAmmoId);
                ModernCombat.DrawRounds = wanted =>
                {
                    string ammo = inventory.HeldAmmoId;
                    int granted = Mathf.Min(wanted, inventory.Count(ammo));
                    if (granted > 0) inventory.Spend(ammo, granted);
                    else Say("Hết đạn — nhặt thêm đạn hoặc đổi vũ khí bằng phím 1 – 3.", 2);
                    return granted;
                };
                // The shot's noise used to ride along with spending the round. It now hangs off
                // the attack itself, so a reload does not announce the player thirty times over.
                ModernCombat.Attacked -= OnPlayerAttacked;
                ModernCombat.Attacked += OnPlayerAttacked;
                // A gun worn to 0% does not fire (Map01Inventory.Condition): the trigger says why, once a second or so.
                ModernCombat.GunUsable = () =>
                {
                    if (!inventory.HeldGunBroken()) return true;
                    if (Time.time >= brokenGunSaidAt + 1.5f)
                    {
                        brokenGunSaidAt = Time.time;
                        Say(ForestInventory.Name(inventory.SlotItem(inventory.HeldGun().Value)) + " đã hỏng (0%) — không bắn được. Sửa bằng bộ sửa chữa (Tab, túi đồ) hoặc ở bàn chế tạo căn cứ A; đổi sang dao: phím 3.", 3);
                    }
                    return false;
                };
                inventory.GunBroke -= OnGunBroke;
                inventory.GunBroke += OnGunBroke;
                Map01Pistol.Install(this);
                Map01Mosin.Install(this);
                NamMotionRefresh.Install(this);
            }
            if (gameCamera != null && gameCamera.TryGetComponent<ThirdPersonCamera>(out var cameraRig))
                {
                cameraRig.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible && !InputFocus.OverlayOpen;
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
            // Map 1's story pieces; the tutorial (no Hùng to rescue, no quest) has none of them.
            if (hung != null && quest != null) Map01OpeningCutscene.Attach(this);
            if (gameObject.scene.name == "Map 1") Map01Ambience.Attach(this);
            GetComponent<Map01Story>()?.Initialize();
            GetComponent<Map01Quest>()?.EnsureCommander();
            if (hung != null && quest != null) Map01HungVisual.Attach(this);
            if(GetComponent<Map01FoliageReveal>()==null)gameObject.AddComponent<Map01FoliageReveal>();
            if(GetComponent<Map01BushTakedown>()==null)gameObject.AddComponent<Map01BushTakedown>();
        }

        private void FailInitialization(string reason)
        {
            Debug.LogError("Map01Mission could not initialize. " + reason, this);
            enabled = false;
        }

        public Material trailMaterial;
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
            // Foreground conversations own their voice gate until Close. A delayed
            // gameplay notice (including a clear) must not pause or replace that queue.
            if(GetComponent<Map01StoryDialogue>()?.IsPlaying==true){
                if(string.IsNullOrEmpty(text)){Dialogue=null;DialogueUntil=0;}
                return;
            }
            var speech=DialogueVoice.For(gameObject);
            speech.CanSpeak=()=>!Paused&&(!Cinematic||GetComponent<TutorialCaptureCutscene>()?.Playing==true)&&!ForestMenu.Visible;
            speech.LineStarted=line=>{
                Dialogue=GameplayCaption(line.caption);DialogueUntil=Time.time+line.clip.length+.2f;
                if(line.caption.StartsWith("Hùng") && hung!=null)hung.GetComponentInChildren<Map01HungVisual>()?.Speak(line.clip.length+.2f);
            };
            if(string.IsNullOrEmpty(text))speech.Stop();
            float spoken=string.IsNullOrEmpty(text)?0:speech.SpeakText(text);
            // Unrecorded calls still need a readable cue; recorded ambient lines keep their audio-only policy.
            Dialogue=spoken>0?GameplayCaption(text):text;DialogueUntil=Time.time+seconds;
            if(!string.IsNullOrEmpty(text))DialogueUntil=Time.time+Mathf.Max(seconds,spoken+.25f);
        }
        public static string GameplayCaption(string text)
        {
            if(string.IsNullOrEmpty(text))return text;
            // Keep objective/interaction notices; ambient character speech is audio only.
            return text.StartsWith("Lính")||text.StartsWith("Sĩ quan")||text.StartsWith("Hùng:")||text.StartsWith("Hùng (")||text.StartsWith("Nam:")?null:text;
        }
        public void ReleaseForNextMap() {
            GetComponent<Map01Ambience>()?.StopForTransition();
            // The carried actor outlives Map 1; its scene-specific arm/scope drivers must not.
            if(player.TryGetComponent(out Map01Pistol pistolMotion))pistolMotion.enabled=false;
            if(player.TryGetComponent(out Map01Mosin mosinMotion))mosinMotion.enabled=false;
            ModernCombat.GunPresentationReady=true;
            GetComponent<DialogueVoice>()?.Stop();
            ModernCombat.Attacked-=OnPlayerAttacked;
            if(player.TryGetComponent(out PlayerFootsteps footsteps)) {
                footsteps.Stepped-=NotifyFootstep;footsteps.SurfaceProbe=null;
            }
        }
        /// <summary>
        /// The content item code of a weapon kind (rifle, pistol, sniper, knife), as the web's weapon stats count it;
        /// the rifle is whichever long gun is in the primary slot (AK or M16). Item codes pass through.
        /// </summary>
        public string WeaponCode(string kind)
        {
            switch (kind)
            {
                case "rifle": return inventory != null && inventory.SlotItem(Map01Inventory.Slot.Primary) == Map01M16.Id ? Map01M16.Id : "rifle_standard";
                case "pistol": return "pistol_k54";
                case "sniper": return "rifle_mosin";
                case "knife": return "knife";
                default: return kind;
            }
        }

        private void OnPlayerAttacked(WeaponKind kind, Vector3 position)
        {
            EventBus.Publish(new PlayerAttackEvent(WeaponCode(kind.ToString().ToLowerInvariant()), position));
            if (kind != WeaponKind.Rifle && kind != WeaponKind.Pistol && kind != WeaponKind.Sniper) return;
            var profile = kind == WeaponKind.Pistol ? Bundle.weapons.FirstOrDefault(w => w.id == "pistol_k54") : kind==WeaponKind.Sniper ? Bundle.weapons.FirstOrDefault(w=>w.id=="rifle_mosin") : Weapon;
            float loud = profile != null ? profile.noise_radius : Weapon.noise_radius;
            if (kind == WeaponKind.Sniper)
            {
                // Heard to noise_radius, placed only from close by: within ShotPinpointMeters it is a gunshot like any
                // other (Nam found, a hostage's guard sets off the execution); further off, a guard searches roughly
                // that way without knowing where Nam is (Map01EnemyController.HearDistantShot).
                EmitNoise(position, Mathf.Min(loud, ShotPinpointMeters), NoiseKind.Gunshot);
                foreach (var guard in Enemies)
                    if (guard != null && Vector3.Distance(guard.transform.position, position) > ShotPinpointMeters) guard.HearDistantShot(position, loud);
            }
            else EmitNoise(position, loud, NoiseKind.Gunshot);
            // Every shot wears the gun in hand by its content's durability_per_shot.
            var held = inventory.HeldGun();
            if (held != null) inventory.Wear(held.Value, inventory.WearPerShot(inventory.SlotItem(held.Value)));
        }

        private float brokenGunSaidAt = -10f;
        private void OnGunBroke(Map01Inventory.Gun gun, string id)
        {
            brokenGunSaidAt = Time.time;
            Say(ForestInventory.Name(id) + " vừa hỏng hẳn (0%) — kẹt cò. Sửa bằng bộ sửa chữa hoặc về bàn chế tạo; đổi sang dao: phím 3.", 5);
        }

        private void OnPlayerDied() => EventBus.Publish(new PlayerDiedEvent(player != null ? player.position : Vector3.zero));
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
            if (RetryRequested != null) { RetryRequested(); return; } // The tutorial retries one lesson.
            var save = GetComponent<Map01SaveSystem>();
            if (save != null && save.RestartCurrentMission()) return;
            Time.timeScale = 1; ReloadScene(gameObject.scene);
        }

        /// <summary>
        /// The guard the knife badge marks: the nearest one within <paramref name="range"/> in clear line,
        /// preferring one Nam can take down now; <paramref name="ready"/> when the knife would do it this frame.
        /// </summary>
        public bool GetTakedownHint(float range, out Map01EnemyController target, out bool ready)
        {
            target = null; ready = false;
            if (!IsInitialized || Stopped || InventoryOpen || MapOpen || ModernCombat == null || ModernCombat.EquippedKind != WeaponKind.Knife
                || (ModernCombat.InputAllowed != null && !ModernCombat.InputAllowed())) return false;
            foreach (var guard in Enemies.Where(e => e != null && e.Alive && !e.IsBoss
                && Vector3.Distance(e.transform.position, player.position) <= range)
                .OrderByDescending(e => e.CanSilentTakedown()).ThenBy(e => (e.transform.position - player.position).sqrMagnitude))
            {
                var origin = player.position + Vector3.up;
                if (Physics.Linecast(origin, guard.transform.position + Vector3.up, out var hit, ObstructionMask, QueryTriggerInteraction.Ignore)
                    && hit.transform != guard.transform && !hit.transform.IsChildOf(guard.transform)) continue;
                target = guard; ready = guard.CanSilentTakedown() && ModernCombat.AttackCooldownRemaining <= .01f && !ModernCombat.IsReloading;
                return true;
            }
            return false;
        }

        public void RegisterEnemy(Map01EnemyController enemy)
        {
            if (enemy == null) return;
            if (!Enemies.Contains(enemy)) Enemies = Enemies.Where(e => e != null).Append(enemy).ToArray();
            enemy.BindMission(this);
        }

        /// <summary>
        /// Starts <paramref name="scene"/> over. In the editor a scene outside Build Settings (the
        /// "Test Map 1 AI" sandbox, kept local) is loaded by path; everything else by name as before.
        /// </summary>
        public static void ReloadScene(UnityEngine.SceneManagement.Scene scene)
        {
#if UNITY_EDITOR
            if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(scene.path) < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                return;
            }
#endif
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
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

        /// <summary>
        /// Thatch and zinc stop no rifle round: the art stage's roof colliders ("ROOF / …", Map01ArtStage) let shots
        /// through. C3's watchtower eave hangs at the sentry's head (14.1-14.5 m, head 14.25 m), so from the ground every
        /// round aimed at the head he shows under it stopped in the roof (2026-10-22). Sight, the camera and thrown
        /// things still stop at roofs.
        /// </summary>
        public static bool RoundPassesThrough(Collider c) => c is MeshCollider && c.name.StartsWith("ROOF /");

        /// <summary>A guard who hears a Mosin shot from this close knows where it came from (Nam found); from further
        /// off, out to the rifle's noise_radius (30 m), he only searches roughly that way. Chosen 2026-10-22: sniping
        /// was found out by everyone within 30 m, as far as the PU's 3.5x reaches in C3's terraced ground.</summary>
        public const float ShotPinpointMeters = 12f;

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
        private void CommanderBlast(Vector3 at, float radius)
        {
            Vector3 up = Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var ground, 4f, ObstructionMask, QueryTriggerInteraction.Ignore)
                ? ground.normal : Vector3.up;
            Map01Blast.Spawn(at, up, gameCamera, radius);
            Map01Smoke.Spawn(at, 1.6f, 4f, visible: false); // Its dust hides a man behind it for a moment, as the F1's does.
        }

        private void OnDestroy()
        {
            if (ShadowVale.Gameplay.Enemies.Boss.Grenade.BlastEffect == (System.Action<Vector3, float>)CommanderBlast)
                ShadowVale.Gameplay.Enemies.Boss.Grenade.BlastEffect = null;
            if (Skills != null) Skills.LevelChanged -= OnSkillLevelChanged;
            if (ModernCombat != null) {
                ModernCombat.GunKilled -= OnGunKilled;
                if (ModernCombat.SlotPressed?.Target is Map01Inventory) ModernCombat.SlotPressed = null;
            }
            // The shared AI services this mission registered.
            if (ServiceLocator.TryResolve<NoiseSystem>(out var noise) && noise == Noise) ServiceLocator.Unregister<NoiseSystem>();
            if (ServiceLocator.TryResolve<IPlayerObservable>(out var stealth) && ReferenceEquals(stealth, Stealth)) ServiceLocator.Unregister<IPlayerObservable>();
        }
        private void OnDisable() { Time.timeScale = 1; }

    }
}
