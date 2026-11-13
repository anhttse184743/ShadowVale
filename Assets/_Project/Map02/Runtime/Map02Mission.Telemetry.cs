using System;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Telemetry;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 2's side of the telemetry contract (docs/game-integration.md of ShadowVale_web), the same events Map 1
    /// publishes: the player for position samples (heatmap), shots and gun kills by item code, takedowns, the
    /// player's death, each chapter task finished (objective_completed, index = its place in Map02Chapter.json, so the
    /// funnel follows the chapter) and the result (completed, or failed when the story fails). The squads' AI events
    /// come from the shared coordinator and enemy brains.
    /// </summary>
    public sealed partial class Map02Mission
    {
        public const string MapCode = "map02";

        bool telemetryPrimed, wasDead, wasFailed, wasFinished;

        void InstallTelemetry()
        {
            TelemetryService.Attach(gameObject, MapCode);
            // The session samples the player through this; Map 1 registers its own player the same way
            if (Player != null) ServiceLocator.Register<IPlayerObservable>(PlayerStealthAdapter.For(Player.gameObject));
            if (combat != null) { combat.Attacked += TelemetryShot; combat.GunKilled += TelemetryGunKill; }
            if (Chapter != null) Chapter.TaskCompleted += TelemetryTaskCompleted;
        }

        void RemoveTelemetry()
        {
            if (combat != null) { combat.Attacked -= TelemetryShot; combat.GunKilled -= TelemetryGunKill; }
            if (Chapter != null) Chapter.TaskCompleted -= TelemetryTaskCompleted;
        }

        /// <summary>Every frame: death, failure and the end of the chapter, each reported once when it happens.</summary>
        void TickTelemetry()
        {
            if (!Ready || Chapter == null) return;
            bool dead = health != null && health.IsDead, failed = World.story.failed, finished = Chapter.State.finished;
            // The first frame only learns the state a checkpoint restored: nothing happened in this session yet
            if (telemetryPrimed)
            {
                if (dead && !wasDead) EventBus.Publish(new PlayerDiedEvent(Player != null ? Player.position : Vector3.zero));
                if (failed && !wasFailed) EventBus.Publish(new MissionEndedEvent(MapCode, "failed"));
                if (finished && !wasFinished) EventBus.Publish(new MissionEndedEvent(MapCode, "completed"));
            }
            telemetryPrimed = true;
            wasDead = dead; wasFailed = failed; wasFinished = finished;
        }

        /// <summary>The bundle item code of what is in hand (rifle_standard, rifle_m16, pistol_k54, rifle_mosin, knife).</summary>
        string HeldWeaponCode(WeaponKind kind)
        {
            if (kind == WeaponKind.Knife) return "knife";
            var held = Bag != null ? Bag.HeldGun() : null;
            return held != null ? Bag.IdOf(Map01Inventory.Gun.InSlot(held.Value)) : kind.ToString().ToLowerInvariant();
        }

        void TelemetryShot(WeaponKind kind, Vector3 at) => EventBus.Publish(new PlayerAttackEvent(HeldWeaponCode(kind), at));

        void TelemetryGunKill(GameObject target)
        {
            var enemy = target != null ? target.GetComponentInParent<Map02ChapterEnemy>() : null;
            if (enemy == null || !enemy.Dead) return;
            EventBus.Publish(new EnemyKilledEvent(enemy.id, "weapon", HeldWeaponCode(combat.EquippedKind), enemy.transform.position));
        }

        void TelemetryTakedown(Map02ChapterEnemy enemy)
        {
            EventBus.Publish(new TakedownEvent(enemy.id, enemy.transform.position));
            EventBus.Publish(new EnemyKilledEvent(enemy.id, "takedown", "knife", enemy.transform.position));
        }

        void TelemetryTaskCompleted(string taskId)
        {
            var tasks = Map02ChapterDefinition.Load().tasks;
            EventBus.Publish(new ObjectiveCompletedEvent(MapCode, taskId, Array.FindIndex(tasks, t => t.id == taskId)));
        }
    }
}
