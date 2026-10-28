using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Fsm;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using ShadowVale.Gameplay.Enemies.Boss;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// M8 in Map 1, on the commander-led story: the C3 commander is at his headquarters from the start with his boss
    /// profile from Map01Content.json; he relocates over the tactical graph between the command post's own cover
    /// (never out of the fence), his grenade hurts Nam behind cover, and his call brings the post's men — the ones not
    /// yet fighting, then the barracks woken by the kẻng — never guards of the other points, nobody cloned.
    /// </summary>
    public sealed class BossIntegrationTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator TheCommanderFightsFromC3CoverThrowsGrenadesAndCallsThePost()
        {
            Map01OpeningCutscene.CancelPending();
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
            var c3 = mission.GetComponent<Map01C3Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            Assert.IsNotNull(c3.Data, "The C3 layout is loaded.");
            var boss = quest.CommanderEnemy;
            Assert.IsNotNull(boss, "The commander is at C3 from map start, not spawned at a stage change.");
            Assert.AreSame(boss, mission.Enemies.Single(e => e.IsBoss));
            Assert.IsTrue(c3.InsideFence(boss.transform.position), "He starts inside his command post.");
            var behaviour = boss.GetComponent<BossBehaviour>();
            Assert.IsNotNull(behaviour, "The commander runs the shared boss behaviour.");
            Assert.AreEqual(3, behaviour.Profile.ReinforceCount, "Profile read from Map01Content.json (boss_commander.boss).");
            Assert.AreEqual(45f, behaviour.Profile.GrenadeDamage, 1e-3f);
            Assert.AreEqual(8f, behaviour.Profile.GrenadeCooldownSeconds, 1e-3f);
            Assert.AreEqual(.5f, behaviour.Profile.ReinforceHpThreshold, 1e-6);
            Assert.AreEqual("boss", boss.SquadId);
            Assert.AreEqual(behaviour.Profile.BurstRounds, boss.BurstRounds);
            // 21/10/2026: body armour (aim for his head), and he shouts his orders within earshot.
            var armour = boss.GetComponent<Health>().BulletDamageModifier;
            Assert.IsNotNull(armour, "Body armour.");
            Vector3 feet = boss.transform.position;
            Assert.AreEqual(12f, armour(24f, feet + Vector3.up * 1f), 1e-3f, "A round to the body: half.");
            Assert.AreEqual(48f, armour(24f, new Vector3(feet.x, behaviour.HeadLine() + .1f, feet.z)), 1e-3f, "To the head: double.");
            Assert.That(behaviour.HeadLine() - feet.y, Is.InRange(1.2f, 1.9f), "His head, not his boots or the sky.");
            var callouts = boss.GetComponent<Map01CommanderCallouts>();
            Assert.IsNotNull(callouts, "He shouts his orders.");
            Vector3 namWas = mission.player.position;
            PlaceNam(mission, feet + boss.transform.forward * 8f);
            EventBus.Publish(new BossOrderEvent(((ShadowVale.AI.Agents.IEnemyAgent)boss).Id, "cover_fire", "test"));
            StringAssert.Contains("Yểm trợ", callouts.LastLine, "Within earshot: a subtitle.");
            PlaceNam(mission, namWas);
            // His own weapon and eyes (19/10/2026), not a cloned guard's times a multiplier.
            Assert.AreEqual(11.5f, boss.DamagePerShot, 1e-3f, "11.5 a round.");
            Assert.AreEqual(.3f, boss.FireInterval, 1e-3f, "A round every 0.3 s.");
            Assert.AreEqual(20f, boss.VisionRange, 1e-3f, "Sight from boss_commander.vision_range.");
            Assert.AreEqual(120f, boss.VisionAngle, 1e-3f);
            Assert.AreEqual(18f, boss.AttackRange, 1e-3f);
            Assert.AreEqual(18f, boss.Brain.Tuning.AttackRange, 1e-3f, "The FSM fights at his reach.");
            Assert.AreEqual(1f, boss.DetectionSeconds, 1e-3f);
            Assert.AreEqual(3.6f, boss.MoveSpeed, 1e-3f, "Walking speed from boss_commander.move_speed.");
            var bossRegen = boss.GetComponent<EnemyRegen>();
            Assert.IsTrue(bossRegen == null || !bossRegen.enabled, "His wounds stay: the fight against him wears him down.");
            float archetypeHp = mission.Bundle.enemy_archetypes.First(a => a.is_boss).max_hp;
            Assert.AreEqual(700f, archetypeHp, 1e-3f, "boss_commander.max_hp.");
            Assert.AreEqual(archetypeHp, boss.GetComponent<Health>().Max, 1e-3f,
                "Health from boss_commander.max_hp: at a guard's ×3 he died about a second after calling for help.");
            Assert.IsInstanceOf<Map01C3Reinforcements>(behaviour.Reinforcements, "His call is answered by the command post.");
            Assert.IsFalse(c3.AutoCallAtHalfHealth, "BossBehaviour makes the half-health call, not the post a second time.");

            // Nam just outside the south fence.
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = c3.At(68f, 36f) + Vector3.up * .1f; controller.enabled = true;
            Physics.SyncTransforms();
            var post = PostMen(c3);
            var elsewhere = mission.Enemies.Where(e => e != null && e.Alive && !e.IsBoss && !post.Contains(e)).ToList();

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

                Assert.IsTrue(behaviour.Relocate(mission.player.position, "test"), "A cover node of the command post within reach.");
                var m = boss.Brain.Memory;
                Assert.AreEqual("relocate", m.AssignedRole);
                Assert.IsTrue(c3.InsideFence(m.AssignedPosition), $"His cover {m.AssignedPosition} is inside the C3 fence.");
                // Measured flat, as BossRelocation picks it: Map 1's C3 stands on terraces.
                float moved = Vector3.ProjectOnPlane(m.AssignedPosition - boss.transform.position, Vector3.up).magnitude;
                Assert.That(moved, Is.InRange(behaviour.Profile.RelocateMinDistance - .5f, behaviour.Profile.RelocateMaxDistance + .5f));
                yield return null;
                yield return null;
                Assert.AreEqual(EnemyStateId.TakeCover, boss.State, "He runs to the new spot — the shared TakeCover state, no seventh state." +
                    $" (stopped={mission.Stopped}, cinematic={mission.Cinematic}, paused={mission.Paused}, enabled={boss.enabled}, engaged={boss.Engaged})");
                Assert.AreEqual(1, relocated.Count);

                // Whatever the FSM asks of him (closing in on Nam with no cover left), he walks no further than the fence.
                var agent = boss.GetComponent<NavMeshAgent>();
                boss.Brain.Context.Body.MoveTo(c3.At(68f, 10f));
                yield return null;
                Assert.IsTrue(c3.InsideFence(agent.destination), $"Sent after Nam 30 m out, he stops at the fence ({agent.destination}).");
                boss.Brain.Assign(m.AssignedPosition, m.AssignedNode, "relocate");

                var nam = mission.player.GetComponent<Health>();
                Assert.AreEqual(7.5f, behaviour.Profile.GrenadeRadius, .001f, "Map 1's commander uses the wider blast profile.");
                Assert.AreEqual(1.5f, behaviour.Profile.GrenadeFuseSeconds, .001f, "1.5 s to get out of the wider blast.");
                float before = nam.Current;
                var grenade = behaviour.ThrowGrenade(mission.player.position);
                float dealt = grenade.Explode();
                Assert.Greater(dealt, 0f, "Right on top of him.");
                Assert.Less(nam.Current, before);
                Assert.AreEqual(1, blasts.Count);

                int idleBefore = post.Count(g => g.Alive && g.gameObject.activeInHierarchy && !g.Engaged);
                int sent = behaviour.CallReinforcements(mission.player.position, Time.time);
                Assert.AreEqual(Mathf.Min(3, idleBefore), sent, "The post's men not yet fighting answer first, three at most.");
                Assert.AreEqual(sent, calls.Single().Arrived);
                foreach (var id in calls[0].Agents.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var guard = mission.Enemies.Single(e => e.LegacyId == id);
                    Assert.IsTrue(post.Contains(guard), id + " belongs to the command post.");
                    Assert.IsTrue(guard.Alive, id);
                    Assert.IsTrue(guard.Engaged, id + " is in the fight.");
                    Assert.IsFalse(guard.IsBoss);
                }
                Assert.IsTrue(c3.Current.called, "The post's own call is made with his.");
                Assert.IsTrue(c3.Current.kiengRung, "The kẻng is struck: the barracks wake.");
                Assert.IsTrue(c3.Sleepers.All(s => s.gameObject.activeSelf), "All three sleepers come out.");
                Assert.IsFalse(mission.Enemies.Any(e => e.name.StartsWith("Viện binh", StringComparison.Ordinal)), "Nobody is cloned out of thin air.");
                Assert.IsTrue(elsewhere.All(e => !e.Engaged), "The guards of the other points do not hear a shout at C3.");

                // While he fights, the squad coordinator plans his men as a pincer (Boss.Command)...
                var orders = new List<BossOrderEvent>();
                Action<BossOrderEvent> onOrder = orders.Add;
                EventBus.Subscribe(onOrder);
                try
                {
                    string bossId = ((ShadowVale.AI.Agents.IEnemyAgent)boss).Id;
                    // Moving to new cover, he has two of them fire to cover him.
                    var men = calls[0].Agents.Split('+').Select(id => (ShadowVale.AI.Agents.IEnemyAgent)mission.Enemies.Single(e => e.LegacyId == id)).ToList();
                    Assert.AreEqual(behaviour.Profile.CoverFireMen, behaviour.CoverFire(men, Time.time));
                    var cover = orders.Single(o => o.Order == "cover_fire");
                    Assert.AreEqual(behaviour.Profile.CoverFireMen, cover.Detail.Split('+').Length);
                    foreach (var id in cover.Detail.Split('+'))
                        Assert.Greater(mission.Enemies.Single(e => e.LegacyId == id).Brain.Memory.CoverFireUntil, Time.time, id + " is giving covering fire.");

                    // Once they have rallied to him (or given up on it), they are planned with the rest.
                    for (float until = Time.realtimeSinceStartup + 15f; !orders.Any(o => o.Order == "pincer") && Time.realtimeSinceStartup < until;)
                        yield return null;
                    Assert.IsTrue(orders.Any(o => o.Order == "pincer" && o.BossId == bossId), "The post's men are planned under his command.");

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

        /// <summary>Everyone of the command post but the commander: the posts, the barracks, the runners.</summary>
        private static HashSet<Map01EnemyController> PostMen(Map01C3Mission c3)
        {
            var men = new HashSet<Map01EnemyController>(c3.Data.roles.Select(r => c3.Guard(r.role)).Where(g => g != null));
            men.UnionWith(c3.Sleepers); men.UnionWith(c3.RunnerGuards);
            return men;
        }
    }
}
