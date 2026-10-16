using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public enum TutorialStep { Move, Sprint, Crouch, Weapons, StoneKnife, Shoot, Heal, Squad, Scout, Return, Capture }

    /// <summary>In-session checkpoints, one per lesson. World results, never input alone, finish a lesson.</summary>
    public sealed partial class TutorialDirector : MonoBehaviour, IMapObjectives
    {
        public const string SceneName = "Map00_Tutorial";
        public Transform[] lessonStarts = Array.Empty<Transform>();
        public Map01EnemyController knifeGuard, shootingGuard;
        [Tooltip("The bush the knife lesson hides in first; the knife guard stands just in front of it, his back to it.")]
        public Transform knifeBush;
        public Map01EnemyController[] squad = Array.Empty<Map01EnemyController>(), patrol = Array.Empty<Map01EnemyController>();
        public TutorialNoiseTrap noiseTrap;
        public TutorialPromptUI promptUI;
        public TutorialStep Step { get; private set; }
        public bool IsComplete => false; // Scene transition owns the ending; Map 1's complete-stage gate is unrelated.
        public bool Failed { get; private set; }
        public string FailureReason { get; private set; }
        public bool AwaitingArrival { get; private set; }
        public string KeyLabel { get; private set; } = "W A S D";
        public string Instruction { get; private set; } = "Giữ phím để di chuyển";
        /// <summary>An optional move for the current lesson, shown on its own line (null when there is none).</summary>
        public string Tip { get; private set; }
        public float Progress { get; private set; }
        public bool CuePending { get; private set; }
        public bool Transitioning { get; private set; }
        public int RetryCount { get; private set; }
        public string LessonTitle => Titles[(int)Step];
        public string ObjectiveText => AwaitingArrival ? "Đi tới điểm hướng dẫn tiếp theo — " + LessonTitle : LessonTitle;
        private static readonly string[] Titles = { "Di chuyển và quan sát", "Chạy nhanh", "Đi khom để giảm tiếng động", "Đổi giữa súng và dao", "Ẩn vào bụi rồi ám sát bằng dao", "Ngắm, bắn và nạp đạn", "Dùng băng hồi máu", "Hạ tổ lính gần cầu gỗ", "Quét hàng trên cầu gỗ", "Quay về căn cứ", "Hùng bị phát hiện" };
        private Map01Mission mission;
        private Map01Inventory inventory;
        private TutorialTimeControl clock;
        private TutorialScouting scouting;
        private Vector3 lastPosition;
        private Quaternion cameraStart;
        private float distance, beganAt, advanceAt = -1;
        private bool moved, looked, rifle, knife, stoneHeard, hid, aimed, shot, reloading, reloaded, healed;
        private bool killedWithRifle;
        private int reloadReserveBefore;
        private string cue;
        private Checkpoint checkpoint;

        private sealed class Checkpoint
        {
            public Vector3 position; public Quaternion rotation, camera;
            public float hp, stamina; public ForestIngredient[] items; public int stones, magazine;
            public WeaponKind weapon;
            public bool crouched, awaitingArrival;
            public readonly Dictionary<Map01EnemyController, Map01EnemyController.Snapshot> enemies = new();
        }

        private IEnumerator Start()
        {
            mission = GetComponent<Map01Mission>(); inventory = GetComponent<Map01Inventory>();
            clock = GetComponent<TutorialTimeControl>(); scouting = GetComponent<TutorialScouting>();
            yield return null; // Mission.Start binds player/combat and creates the magazine first.
            if (!mission.IsInitialized) yield break;
            mission.RetryRequested = RetryStep;
            mission.StopRequested = () => Failed;
            inventory.ItemUsed += OnItemUsed;
            mission.ModernCombat.Attacked += OnAttack;
            shootingGuard.GetComponent<Health>().onDied.AddListener(OnShootingGuardDied);
            mission.ModernCombat.TrySpecialMelee = TryKnife;
            foreach (var enemy in squad) enemy.SquadId = "T3";
            knifeGuard.HoldsPost = true; // The knife lesson's sentry looks round at a sound but never walks into Hùng's bush.
            foreach (var enemy in AllEnemies()) { enemy.gameObject.SetActive(true); enemy.BindMission(mission); mission.Noise.Unregister(enemy); enemy.gameObject.SetActive(false); }
            BeginStep(TutorialStep.Move);
        }
        private IEnumerable<Map01EnemyController> AllEnemies() => new[] { knifeGuard, shootingGuard }.Concat(squad).Concat(patrol).Where(e => e != null).Distinct();
        private IEnumerable<Map01EnemyController> CurrentEnemies() => Step == TutorialStep.StoneKnife ? new[] { knifeGuard }
            : Step == TutorialStep.Shoot ? new[] { shootingGuard } : Step == TutorialStep.Squad ? squad : Array.Empty<Map01EnemyController>();
        public bool TryGetObjectiveTarget(out Vector3 target, out string label)
        {
            label = Titles[(int)Step];
            target = lessonStarts.Length > (int)Step && lessonStarts[(int)Step] != null ? lessonStarts[(int)Step].position : transform.position;
            if (Step == TutorialStep.Scout && !AwaitingArrival && scouting != null && scouting.supplyCache != null) target = scouting.supplyCache.position;
            if (Step == TutorialStep.StoneKnife && !AwaitingArrival && !hid && knifeBush != null) { target = knifeBush.position; label = "BỤI CÂY — ẨN NẤP"; }
            return !Failed && !Transitioning && Step != TutorialStep.Capture;
        }
        private void BeginStep(TutorialStep step)
        {
            CancelTargetReveal();
            Step = step; Failed = false; FailureReason = null; checkpoint = null; advanceAt = -1;
            distance = Progress = 0; moved = looked = rifle = knife = stoneHeard = hid = aimed = shot = reloading = reloaded = healed = false;
            killedWithRifle = false;
            cue = null; Tip = null; beganAt = Time.time; scouting.ResetScan(); clock.SetSlow(false);
            noiseTrap.gameObject.SetActive(step == TutorialStep.Return);
            foreach (var enemy in AllEnemies()) { mission.Noise.Unregister(enemy); enemy.gameObject.SetActive(false); }
            lastPosition = mission.player.position; cameraStart = mission.gameCamera.transform.rotation;
            AwaitingArrival = Vector3.Distance(mission.player.position, lessonStarts[(int)step].position) > 5f && step != TutorialStep.Return;
            if (!AwaitingArrival) PrepareLesson(); else CaptureCheckpoint();
        }
        private void PrepareLesson()
        {
            AwaitingArrival = false;
            foreach (var enemy in CurrentEnemies().Where(e => e != null)) {
                enemy.gameObject.SetActive(true); mission.RegisterEnemy(enemy); enemy.ReturnToPost();
            }
            // Each combat lesson has enough resources regardless of what was spent before it.
            if (Step == TutorialStep.StoneKnife && inventory.Stones < mission.Settings.startingStones)
                inventory.Add("stone", mission.Settings.startingStones - inventory.Stones);
            if (Step == TutorialStep.Shoot || Step == TutorialStep.Squad) {
                inventory.Add("ammo_rifle", Mathf.Max(0, StartingCount("ammo_rifle") - inventory.Count("ammo_rifle")));
                mission.ModernCombat.RestoreMagazine(mission.Settings.tutorialMagazine);
            }
            if (Step == TutorialStep.Heal) {
                inventory.Add("medkit_small", Mathf.Max(0, Mathf.Max(1, StartingCount("medkit_small")) - inventory.Count("medkit_small")));
                if (mission.PlayerHealth >= mission.Settings.playerHP)
                    mission.RestoreHealthFromSave(mission.Settings.playerHP - mission.Settings.tutorialPracticeDamage);
                mission.Say("Hùng: Vừa bị đạn sượt qua. Phải băng lại trước khi đi tiếp.", 4);
            }
            lastPosition = mission.player.position; cameraStart = mission.gameCamera.transform.rotation; beganAt = Time.time;
            CaptureCheckpoint();
            StartTargetReveal();
        }
        private void CaptureCheckpoint()
        {
            checkpoint = new Checkpoint { position = mission.player.position, rotation = mission.player.rotation,
                camera = mission.gameCamera.transform.rotation, hp = mission.PlayerHealth, stamina = mission.Stamina,
                items = inventory.CaptureItems(), stones = inventory.Stones, weapon = mission.ModernCombat.EquippedKind,
                magazine = mission.ModernCombat.RoundsInMagazine, crouched = mission.Crouched, awaitingArrival = AwaitingArrival };
            foreach (var enemy in CurrentEnemies().Where(e => e != null && e.gameObject.activeInHierarchy)) checkpoint.enemies[enemy] = enemy.Capture();
        }
        private int StartingCount(string id) => mission.Settings.startingItems?.FirstOrDefault(i => i.item_id == id)?.count ?? 0;
        public void RetryStep()
        {
            if (checkpoint == null || Transitioning || Step == TutorialStep.Capture) return;
            CancelTargetReveal();
            RetryCount++; clock.SetSlow(false); scouting.ResetScan();
            Map01NamActions.For(mission)?.CancelAction(); GetComponent<Map01StoneThrow>()?.Cancel();
            mission.CloseGameplayPanel(); mission.Alarmed = false;
            var body = mission.player.GetComponent<CharacterController>(); body.enabled = false;
            mission.player.SetPositionAndRotation(checkpoint.position, checkpoint.rotation); body.enabled = true;
            mission.ModernPlayer.enabled = true; mission.ModernPlayer.RestoreMotion(checkpoint.crouched); mission.Crouched = checkpoint.crouched;
            mission.ModernCombat.enabled = true; mission.ModernCombat.RestoreAfterCinematic();
            mission.RestoreHealthFromSave(checkpoint.hp); mission.SetStamina(checkpoint.stamina);
            inventory.RestoreFromSave(checkpoint.items, checkpoint.stones, null, 0);
            inventory.SuppressFire = true;
            mission.ModernCombat.Equip(checkpoint.weapon); mission.ModernCombat.RestoreMagazine(checkpoint.magazine);
            mission.ModernCombat.RestoreAttackCooldown(0);
            mission.gameCamera.GetComponent<ThirdPersonCamera>()?.RestoreLook(checkpoint.camera);
            foreach (var pair in checkpoint.enemies) { pair.Key.ReturnToPost(); pair.Key.RestoreSnapshot(pair.Value); }
            Failed = false; FailureReason = null; CuePending = false; cue = null; Tip = null;
            AwaitingArrival = checkpoint.awaitingArrival;
            advanceAt = -1; Progress = distance = 0; moved = looked = rifle = knife = stoneHeard = hid = aimed = shot = reloading = reloaded = healed = false;
            killedWithRifle = false;
            lastPosition = checkpoint.position; cameraStart = checkpoint.camera; beganAt = Time.time;
            clock.Apply();
            if (!AwaitingArrival) StartTargetReveal();
        }
        public void Fail(string reason)
        {
            if (Failed || Transitioning) return;
            CancelTargetReveal();
            Failed = true; FailureReason = reason; CuePending = false; clock.SetSlow(false);
        }
        /// <param name="slow">Slow time while the cue waits. Off where timing matters (sneaking up on a guard).</param>
        private void Cue(string key, string text, bool accepted, bool slow = true)
        {
            if (cue != key + text) { cue = key + text; KeyLabel = key; Instruction = text; CuePending = true; clock.SetSlow(slow); }
            if (accepted && CuePending) { CuePending = false; clock.SetSlow(false); }
        }
        private void OnItemUsed(string id) { if (Step == TutorialStep.Heal && id == "medkit_small") healed = true; }
        private void OnAttack(WeaponKind kind, Vector3 point)
        {
            if (Step == TutorialStep.Shoot && kind == WeaponKind.Rifle && mission.ModernCombat.IsAiming)
                aimed = shot = true; // Aim and fire can arrive in the same frame, before this component's Update.
        }
        private void OnShootingGuardDied() { if (Step == TutorialStep.Shoot) killedWithRifle = mission.ModernCombat.EquippedKind == WeaponKind.Rifle && !shootingGuard.TakenDownSilently; }
        private bool TryKnife() => Step == TutorialStep.StoneKnife && knifeGuard != null && knifeGuard.TrySilentTakedown();
        private void Update()
        {
            if (mission == null || !mission.IsInitialized || Transitioning) return;
            if (Failed) { if (!ForestMenu.Visible && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) RetryStep(); return; }
            if (mission.ModernHealth.IsDead) { Fail("Hùng đã gục. Thử lại hướng dẫn hiện tại."); return; }
            if (ForestMenu.Visible || mission.Paused || mission.Cinematic || mission.InventoryOpen || mission.MapOpen) return;
            if (advanceAt >= 0) { if (Time.unscaledTime >= advanceAt && !(Map01NamActions.For(mission)?.Busy ?? false)) BeginStep((TutorialStep)((int)Step + 1)); return; }
            if (AwaitingArrival) {
                CuePending = false; clock.SetSlow(false);
                // The knife guard hears a walking step from 9 m: tell the player to sneak before he is in earshot.
                if (Step == TutorialStep.StoneKnife) { KeyLabel = "CTRL"; Instruction = "Khom người rồi đi lén tới — lính nghe được tiếng chân"; }
                else { KeyLabel = "W A S D"; Instruction = "Đi tới dấu chỉ dẫn để học thao tác tiếp theo"; }
                if (Vector3.Distance(mission.player.position, lessonStarts[(int)Step].position) <= 5f) PrepareLesson();
                return;
            }
            float travel = Vector3.ProjectOnPlane(mission.player.position - lastPosition, Vector3.up).magnitude;
            lastPosition = mission.player.position;
            if (travel > 2f) travel = 0; // Teleports never count as movement practice.
            var combat = mission.ModernCombat; var player = mission.ModernPlayer;
            bool done = false; var mouse = Mouse.current;
            switch (Step) {
                case TutorialStep.Move:
                    distance += travel; moved |= distance > 2; looked |= mouse != null && mouse.delta.ReadValue().sqrMagnitude > .1f && Quaternion.Angle(cameraStart, mission.gameCamera.transform.rotation) > 12;
                    Cue(moved ? "CHUỘT" : "W A S D", moved ? "Di chuyển chuột để nhìn xung quanh" : "Giữ W/A/S/D để di chuyển", moved ? looked : player.IsMoving);
                    Progress = (moved ? .5f : Mathf.Min(.5f, distance / 4)) + (looked ? .5f : 0); done = moved && looked; break;
                case TutorialStep.Sprint:
                    Cue("SHIFT", "Nhấn Shift để bật chạy nhanh, rồi di chuyển", player.IsSprinting);
                    if (player.IsSprinting) distance += travel;
                    Progress = distance / mission.Settings.tutorialSprintDistance; done = Progress >= 1; break;
                case TutorialStep.Crouch:
                    Cue("CTRL / C", "Nhấn Ctrl hoặc C để đi khom. Nhấn lại để đứng lên.", player.IsSneaking);
                    if (player.IsSneaking) distance += travel;
                    Progress = distance / mission.Settings.tutorialCrouchDistance; done = Progress >= 1; break;
                case TutorialStep.Weapons:
                    rifle |= combat.EquippedKind == WeaponKind.Rifle; knife |= combat.EquippedKind == WeaponKind.Knife;
                    Cue(rifle ? "2" : "1", rifle ? "Nhấn 2 để cầm dao" : "Nhấn 1 để cầm súng", rifle && knife);
                    Progress = (rifle ? .5f : 0) + (knife ? .5f : 0); done = rifle && knife; break;
                case TutorialStep.StoneKnife:
                    // Hide in the bush first, then out behind the guard. The stone is optional; no slow motion here.
                    stoneHeard |= knifeGuard.LastStoneHeardAt >= beganAt;
                    // Only the bush behind the guard counts: the way in passes other bushes.
                    hid |= mission.Hidden && (knifeBush == null
                        || Vector3.ProjectOnPlane(mission.player.position - knifeBush.position, Vector3.up).magnitude < 2.5f);
                    Tip = "Tuỳ chọn: giữ Q rồi thả để ném đá ra xa — lính sẽ quay đi xem";
                    if (!player.IsSneaking) Cue("CTRL", "Nhấn Ctrl để khom — đi thẳng người sẽ gây tiếng động", false, slow: false);
                    else if (!hid) Cue("BỤI CÂY", "Khom người đi vào bụi cây phía trước để ẩn nấp", false, slow: false);
                    else if (combat.EquippedKind != WeaponKind.Knife) Cue("2", "Nhấn 2 để cầm dao", false, slow: false);
                    else if (knifeGuard.CanSilentTakedown()) Cue("CHUỘT TRÁI", "Nhấn chuột trái để ám sát", false, slow: false);
                    else Cue("SAU LƯNG", "Lẻn ra sau lưng lính, tới thật gần để ám sát", false, slow: false);
                    if (!knifeGuard.Alive && !knifeGuard.TakenDownSilently) Fail("Phải ám sát bằng dao từ sau lưng. Thử lại bước này.");
                    if (knifeGuard.Engaged && knifeGuard.Alive) Fail("Lính đã phát hiện Hùng. Đi khom (Ctrl) để không gây tiếng động, núp trong bụi chờ lính quay đi rồi lẻn ra sau lưng.");
                    done = knifeGuard.TakenDownSilently; Progress = done ? 1 : hid ? .5f : 0; break;
                case TutorialStep.Shoot:
                    aimed |= combat.IsAiming;
                    if (combat.IsReloading && !reloading) { reloadReserveBefore = inventory.Count("ammo_rifle"); reloading = true; }
                    if (reloading && !combat.IsReloading) { reloaded |= inventory.Count("ammo_rifle") < reloadReserveBefore; reloading = false; }
                    if (combat.EquippedKind != WeaponKind.Rifle) Cue("1", "Nhấn 1 để cầm súng", false);
                    else if (!aimed) Cue("CHUỘT PHẢI", "Giữ chuột phải để ngắm vào lính", combat.IsAiming);
                    else if (shootingGuard.Alive) Cue("CHUỘT TRÁI", "Nhấn chuột trái để bắn hạ lính", mouse != null && mouse.leftButton.isPressed);
                    else Cue("R", "Nhấn R để nạp đạn", reloading);
                    if (!shootingGuard.Alive && (!shot || !killedWithRifle)) Fail("Hạ mục tiêu bằng súng sau khi ngắm. Thử lại bước này.");
                    if (inventory.Count("ammo_rifle") == 0 && combat.RoundsInMagazine == 0) Fail("Hết đạn. Thử lại bước này để được tiếp tế.");
                    done = shot && killedWithRifle && !shootingGuard.Alive && reloaded; Progress = (shot ? .3f : 0) + (killedWithRifle ? .4f : 0) + (reloaded ? .3f : 0); break;
                case TutorialStep.Heal:
                    Cue("H", "Nhấn H để dùng băng hồi máu", healed); done = healed; Progress = healed ? 1 : 0; break;
                case TutorialStep.Squad:
                    KeyLabel = "THỰC HÀNH"; Instruction = "Dùng chỗ nấp, súng hoặc dao để hạ tổ 3 lính"; CuePending = false; clock.SetSlow(false);
                    int alive = squad.Count(e => e.Alive); Progress = 1f - alive / (float)Mathf.Max(1, squad.Length); done = alive == 0;
                    if (inventory.Count("ammo_rifle") == 0 && combat.RoundsInMagazine == 0) Instruction = "Hết đạn: dùng dao hoặc Thử lại bước này để được tiếp tế"; break;
                case TutorialStep.Scout:
                    Cue("F", "Giữ F để quét kiện hàng đặt ngay trên cầu gỗ", scouting.Binoculars);
                    Progress = scouting.Progress; done = Progress >= 1; break;
                case TutorialStep.Return:
                    KeyLabel = "QUAY VỀ"; Instruction = "Đã ghi nhận hàng tiếp tế. Rời bến theo cầu gỗ để quay về báo cáo."; clock.SetSlow(false); break;
            }
            if (done && !Failed) { Progress = 1; CuePending = false; clock.SetSlow(false); Instruction = "Đã hoàn thành"; advanceAt = Time.unscaledTime + .7f; }
        }
        public void BeginCapture()
        {
            if (Step != TutorialStep.Return || Transitioning || Failed) return;
            foreach (var enemy in patrol) { enemy.gameObject.SetActive(true); mission.RegisterEnemy(enemy); enemy.ReturnToPost(); }
            mission.EmitNoise(mission.player.position, mission.Settings.tutorialCaptureNoise, ShadowVale.AI.Perception.NoiseKind.Other);
            Step = TutorialStep.Capture; clock.SetSlow(false); scouting.ResetScan();
            GetComponent<TutorialCaptureCutscene>().Begin();
        }
        public void SkipAll()
        {
            if (Transitioning) return;
            CancelTargetReveal();
            Transitioning = true; clock.SetSlow(false); StopAllCoroutines(); scouting.ResetScan();
            GetComponent<TutorialCaptureCutscene>()?.Cancel();
            Map01NamActions.For(mission)?.CancelAction(); GetComponent<Map01StoneThrow>()?.Cancel();
            checkpoint = null; mission.SetPaused(false); mission.Cinematic = false;
            Map01OpeningCutscene.RequestNewGame(); Map01SaveSystem.BeginGame();
        }
        private void OnDestroy()
        {
            CancelTargetReveal();
            if (inventory != null) inventory.ItemUsed -= OnItemUsed;
            if (shootingGuard != null) shootingGuard.GetComponent<Health>().onDied.RemoveListener(OnShootingGuardDied);
            if (mission != null) { mission.RetryRequested = null; mission.StopRequested = null; if (mission.ModernCombat != null) { mission.ModernCombat.Attacked -= OnAttack; mission.ModernCombat.TrySpecialMelee = null; } }
        }
    }
}
