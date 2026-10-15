using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Fsm;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies.Boss;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// M8 in Map 1: the commander comes with his boss profile from Map01Content.json, relocates over the
    /// tactical graph, his grenade hurts Nam behind cover, and his call brings three men.
    /// </summary>
    public sealed class BossIntegrationTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator TheCommanderRelocatesThrowsGrenadesAndCallsReinforcements()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator InPlayMode()
        {
            yield return null;
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            // As in the real quest line: every camp has fallen before the commander shows up.
            foreach (var guard in mission.Enemies) guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            yield return null;
            quest.RestoreStage(Map01Quest.BossStage); // Spawns the commander, as a checkpoint at this stage does.
            var boss = mission.Enemies.Single(e => e.IsBoss);
            var behaviour = boss.GetComponent<BossBehaviour>();
            Assert.IsNotNull(behaviour, "The commander runs the shared boss behaviour.");
            Assert.AreEqual(3, behaviour.Profile.ReinforceCount, "Profile read from Map01Content.json (boss_commander.boss).");
            Assert.AreEqual(.5f, behaviour.Profile.ReinforceHpThreshold, 1e-6);
            Assert.AreEqual("boss", boss.SquadId);
            float archetypeHp = mission.Bundle.enemy_archetypes.First(a => a.is_boss).max_hp;
            Assert.AreEqual(archetypeHp, boss.GetComponent<Health>().Max, 1e-3f,
                "Health from boss_commander.max_hp: at a guard's ×3 he died about a second after calling for help.");

            var relocated = new List<BossRelocatedEvent>();
            var blasts = new List<GrenadeExplodedEvent>();
            var calls = new List<BossReinforcementsEvent>();
            Action<BossRelocatedEvent> onRelocate = relocated.Add;
            Action<GrenadeExplodedEvent> onBlast = blasts.Add;
            Action<BossReinforcementsEvent> onCall = calls.Add;
            EventBus.Subscribe(onRelocate); EventBus.Subscribe(onBlast); EventBus.Subscribe(onCall);
            try
            {
                boss.EngageFor(60f);
                boss.Brain.Memory.LastKnownPlayerPosition = mission.player.position;

                Assert.IsTrue(behaviour.Relocate(mission.player.position, "test"), "A cover node within reach of his camp.");
                var m = boss.Brain.Memory;
                Assert.AreEqual("relocate", m.AssignedRole);
                float moved = Vector3.Distance(boss.transform.position, m.AssignedPosition);
                Assert.That(moved, Is.InRange(behaviour.Profile.RelocateMinDistance - .5f, behaviour.Profile.RelocateMaxDistance + .5f));
                yield return null;
                yield return null;
                Assert.AreEqual(EnemyStateId.TakeCover, boss.State, "He runs to the new spot — the shared TakeCover state, no seventh state.");
                Assert.AreEqual(1, relocated.Count);

                var nam = mission.player.GetComponent<Health>();
                float before = nam.Current;
                var grenade = behaviour.ThrowGrenade(mission.player.position);
                float dealt = grenade.Explode();
                Assert.Greater(dealt, 0f, "Right on top of him.");
                Assert.Less(nam.Current, before);
                Assert.AreEqual(1, blasts.Count);

                int sent = behaviour.CallReinforcements(mission.player.position, Time.time);
                Assert.AreEqual(3, sent, "Nobody idle is left near him: three fresh men.");
                Assert.AreEqual(3, calls.Single().Arrived);
                StringAssert.Contains("Tiếp viện", mission.Dialogue, "The call is heard.");
                foreach (var id in calls[0].Agents.Split('+'))
                {
                    var guard = mission.Enemies.Single(e => e.LegacyId == id);
                    Assert.IsTrue(guard.Alive, id);
                    Assert.IsTrue(guard.Engaged, id + " is in the fight.");
                    Assert.IsFalse(guard.IsBoss);
                    StringAssert.StartsWith("Viện binh", guard.name);
                    Assert.AreEqual("reinforce", guard.SquadId);
                    Assert.LessOrEqual(Vector3.Distance(guard.transform.position, boss.transform.position), Map01Reinforcements.SpawnDistance + 7f,
                        "They arrive near the commander, not somewhere across the map.");
                }

                // While he fights, the squad coordinator plans his men as a pincer (Boss.Command)...
                var orders = new List<BossOrderEvent>();
                Action<BossOrderEvent> onOrder = orders.Add;
                EventBus.Subscribe(onOrder);
                try
                {
                    string bossId = ((ShadowVale.AI.Agents.IEnemyAgent)boss).Id;
                    for (float until = Time.realtimeSinceStartup + 5f; !orders.Any(o => o.Order == "pincer") && Time.realtimeSinceStartup < until;)
                        yield return null;
                    Assert.IsTrue(orders.Any(o => o.Order == "pincer" && o.BossId == bossId), "The reinforcements are planned under his command.");

                    // ...and when he falls, they lose heart.
                    boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
                    yield return null;
                    yield return null;
                    var broken = orders.FirstOrDefault(o => o.Order == "morale_break");
                    Assert.AreEqual("morale_break", broken.Order, "His fall breaks his men's nerve.");
                    foreach (var id in broken.Detail.Split('+'))
                        Assert.GreaterOrEqual(mission.Enemies.Single(e => e.LegacyId == id).Brain.Tuning.RetreatHealthFraction, Command.MoraleBreakRetreat, id);
                }
                finally { EventBus.Unsubscribe(onOrder); }
            }
            finally
            {
                EventBus.Unsubscribe(onRelocate); EventBus.Unsubscribe(onBlast); EventBus.Unsubscribe(onCall);
            }
        }
    }
}
