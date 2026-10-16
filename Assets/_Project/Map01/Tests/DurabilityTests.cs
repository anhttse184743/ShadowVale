using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Skills;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Gun = ShadowVale.Map01.Map01Inventory.Gun;
using Slot = ShadowVale.Map01.Map01Inventory.Slot;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Gun condition in play (Map01Inventory.Condition.cs, Map01Logistics): every shot wears the AK by its content's
    /// durability_per_shot; at 0% it does not fire and keeps its rounds; a repair kit brings it up 35% at a time to the
    /// field cap (80% at Engineering 0) and no further; the bench takes it to 100% for parts and counts for
    /// Engineering; a gun taken apart gives its parts (one more from a gun in good condition, the Mosin its lens); the
    /// bag as saved keeps every gun's condition; a fallen guard's pack and gun are the same after a load, and gone
    /// when the camp is reinforced.
    /// </summary>
    public sealed class DurabilityTests : ForestSceneTestBase
    {
        // Lambdas stay in static helpers or capture only static fields (the domain reload of EnterPlayMode).
        private static Map01Mission mission;
        private static Map01Inventory inventory;
        private static PlayerCombat combat;

        private static void OpenMap()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
        }

        private static void Settle()
        {
            mission = Object.FindFirstObjectByType<Map01Mission>();
            inventory = mission.GetComponent<Map01Inventory>();
            combat = mission.player.GetComponent<PlayerCombat>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
        }

        private static Gun Primary => Gun.InSlot(Slot.Primary);
        private static float Fraction(Gun gun) => inventory.FractionOf(gun);
        private static int Rounds() => combat.RoundsInMagazine + inventory.Count("ammo_rifle");
        private static Gun FirstInBag(string id) => inventory.Guns().First(g => g.Slot == null && inventory.IdOf(g) == id);
        private static string[] Yield(Gun gun) => inventory.SalvageYield(gun).Select(y => y.item + "×" + y.count).ToArray();
        private static ForestPoint LaidGun(Map01EnemyController guard) =>
            Map01Drops.For(mission).LaidGuns.FirstOrDefault(p => p.id == Map01Logistics.GunPointPrefix + guard.LegacyId);
        private static int PointsWithId(string id) => mission.Points.Count(p => p != null && p.id == id);
        private static string Describe(ForestIngredient[] items) => string.Join(",", items.Select(i => i.item_id + "×" + i.count));

        private static void StandAtBench()
        {
            var b = mission.Interactables.First(p => p.kind == ForestPointKind.Workbench).transform.position;
            NavMesh.SamplePosition(b, out var near, 3, NavMesh.AllAreas);
            var side = Vector3.ProjectOnPlane(near.position - b, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            NavMesh.SamplePosition(b + side.normalized * 1.1f, out var ground, 2, NavMesh.AllAreas);
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false;
            mission.player.SetPositionAndRotation(ground.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(b - ground.position, Vector3.up)));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        /// <summary>Holds the left button for some frames through PlayerCombat's own input (the AK is automatic).</summary>
        private static IEnumerator Fire(Mouse mouse, int frames)
        {
            var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }.WithButton(MouseButton.Left)); InputSystem.Update();
            for (int i = 0; i < frames; i++) yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }); InputSystem.Update();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShotsWearTheRifleABrokenOneDoesNotFireAndRepairsBringItBack()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            float max = inventory.MaxCondition("rifle_standard"), wear = inventory.WearPerShot("rifle_standard");
            Assert.AreEqual(100f, max, 1e-4f, "Map01Content.json rifle_standard durability_max");
            Assert.AreEqual(.35f, wear, 1e-4f, "Map01Content.json rifle_standard durability_per_shot");
            Assert.AreEqual(1f, Fraction(Primary), "Nam's own rifle starts as new.");

            var routing = RouteInputToGame();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();   // Map01PlayerInteraction releases SuppressFire only with one
            try
            {
                inventory.Add("ammo_rifle", 60);
                Assert.IsTrue(inventory.EquipItem("rifle_standard"));
                yield return null; yield return null;
                int rounds = Rounds();
                yield return Fire(mouse, 6);
                int shots = rounds - Rounds();
                Assert.Greater(shots, 0, "The rifle fires.");
                Assert.AreEqual(max - shots * wear, inventory.ConditionOf(Primary), 1e-3f, "Each shot wears the rifle by durability_per_shot.");

                // Worn to nothing: it says so, and the trigger does nothing; the rounds stay.
                inventory.Wear(Slot.Primary, max);
                Assert.IsTrue(inventory.IsBroken(Primary));
                Assert.IsTrue(inventory.HeldGunBroken());
                StringAssert.Contains("hỏng", mission.Dialogue);
                rounds = Rounds();
                yield return Fire(mouse, 6);
                Assert.AreEqual(rounds, Rounds(), "A broken rifle does not fire.");
            }
            finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }

            // A repair kit: +35% each, to the 80% field cap at Engineering 0, and no further.
            Assert.AreEqual(0, mission.Skills.Level(SkillKind.Engineering));
            Assert.IsNotNull(inventory.FieldRepairRefusal(Primary), "No kit, no field repair.");
            inventory.Add("repair_kit", 4);
            Assert.IsTrue(inventory.FieldRepair(Primary)); Assert.AreEqual(.35f, Fraction(Primary), 1e-3f);
            Assert.IsTrue(inventory.FieldRepair(Primary)); Assert.AreEqual(.70f, Fraction(Primary), 1e-3f);
            Assert.IsTrue(inventory.FieldRepair(Primary)); Assert.AreEqual(.80f, Fraction(Primary), 1e-3f, "The field cap.");
            Assert.IsFalse(inventory.FieldRepair(Primary), "At the cap a kit does nothing.");
            Assert.AreEqual(1, inventory.Count("repair_kit"), "Refused: the kit is kept.");
            Assert.AreEqual(0, mission.Skills.Xp(SkillKind.Engineering), "A repair short of 100% does not count.");

            // The bench: to 100% for one part per started quarter missing; a paid repair to full counts for Engineering.
            Assert.IsNotNull(inventory.BenchRepairRefusal(Primary), "Away from the bench.");
            StandAtBench(); yield return null;
            Assert.AreEqual((1, 0), inventory.BenchCost(Primary));
            Assert.IsNotNull(inventory.BenchRepairRefusal(Primary), "No parts.");
            inventory.Add("weapon_parts", 1);
            Assert.IsTrue(inventory.BenchRepair(Primary));
            Assert.AreEqual(1f, Fraction(Primary), 1e-4f);
            Assert.AreEqual(0, inventory.Count("weapon_parts"));
            Assert.AreEqual(1, mission.Skills.Xp(SkillKind.Engineering), "Engineering counts the full repair.");
            inventory.Wear(Slot.Primary, max * .6f);
            Assert.AreEqual((3, 1), inventory.BenchCost(Primary), "60% missing: three quarters' parts and a gun oil.");

            // Taking guns apart: a captured AK at 25% gives its scrap and a part; one at 60% a part more; the Mosin its lens.
            inventory.Receive("rifle_standard", 1, .25f * max);
            var worn = FirstInBag("rifle_standard");
            CollectionAssert.AreEquivalent(new[] { "scrap_metal×2", "weapon_parts×1" }, Yield(worn));
            int scrap = inventory.Count("scrap_metal"), parts = inventory.Count("weapon_parts"), rifles = inventory.Count("rifle_standard");
            Assert.IsTrue(inventory.Salvage(worn));
            Assert.AreEqual(rifles - 1, inventory.Count("rifle_standard"));
            Assert.AreEqual(scrap + 2, inventory.Count("scrap_metal"));
            Assert.AreEqual(parts + 1, inventory.Count("weapon_parts"));
            inventory.Receive("rifle_standard", 1, .6f * max);
            CollectionAssert.AreEquivalent(new[] { "scrap_metal×2", "weapon_parts×2" }, Yield(FirstInBag("rifle_standard")));
            inventory.Receive("rifle_mosin", 1, .3f * inventory.MaxCondition("rifle_mosin"));
            CollectionAssert.Contains(Yield(FirstInBag("rifle_mosin")), "scope_lens×1");

            // The bag as saved keeps each gun's condition.
            var layout = inventory.CaptureLayout();
            float inHand = inventory.ConditionOf(Primary), inBag = inventory.ConditionOf(FirstInBag("rifle_mosin"));
            inventory.Wear(Slot.Primary, 10);
            inventory.RestoreLayout(layout);
            Assert.AreEqual(inHand, inventory.ConditionOf(Primary), 1e-3f);
            Assert.AreEqual(inBag, inventory.ConditionOf(FirstInBag("rifle_mosin")), 1e-3f);
            // A save from before guns wore: everything in it comes back as new.
            layout.conditions = null; layout.primaryCondition = 0;
            inventory.RestoreLayout(layout);
            Assert.AreEqual(1f, Fraction(Primary));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AFallenGuardsPackAndGunAreTheSameAfterALoadAndGoWhenTheCampIsReinforced()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var logistics = Map01Logistics.For(mission);
            var guard = mission.Enemies.First(e => e.Alive && !e.IsSupplyKeyKeeper);
            guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            yield return null;
            var pack = guard.GetComponent<ForestPoint>();
            Assert.IsNotNull(pack);
            Assert.AreEqual(logistics.PackName(guard), pack.label);
            string packed = Describe(pack.items);
            Assert.IsNotNull(LaidGun(guard), "His rifle lies by him.");
            float condition = LaidGun(guard).items[0].condition;
            StringAssert.Contains("độ bền", LaidGun(guard).label, "The prompt says how worn it is.");

            // Another run's rolls, then the save's again: the same pack, the same rifle.
            var saved = logistics.Capture();
            logistics.Restore(new Map01Logistics.State { seed = saved.seed == int.MaxValue ? 1 : saved.seed + 1 });
            guard.FillLoot();
            logistics.Restore(saved);
            guard.FillLoot();
            Assert.AreEqual(packed, Describe(pack.items));
            Assert.AreEqual(condition, LaidGun(guard).items[0].condition, 1e-4f);
            Assert.AreEqual(1, PointsWithId(LaidGun(guard).id), "Laid again, not twice.");

            // Taken, it stays a used point (so the save remembers it) and is no longer shown; a load that gives it back shows it.
            var gun = LaidGun(guard);
            mission.GetComponent<Map01PlayerInteraction>().Interact(gun);
            Assert.IsTrue(gun.used);
            yield return null;
            Assert.IsTrue(mission.Points.Contains(gun));
            Assert.IsFalse(gun.gameObject.activeSelf, "A taken gun is not shown.");
            gun.used = false; yield return null;
            Assert.IsTrue(gun.gameObject.activeSelf);

            // A failed scouting run reinforces the camp: the guard is back, the dropped rifle and his pack gone.
            guard.ReturnToPost();
            Assert.IsNull(LaidGun(guard));
            Assert.AreEqual(0, PointsWithId(gun.id));
            Assert.AreEqual(0, PointsWithId("loot_" + guard.LegacyId));
            yield return new ExitPlayMode();
        }
    }
}
