using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The iron chest in each enemy camp (Map01CampChests): where it stands, that it stays locked while the
    /// camp has a guard alive, that [E] opens it to show its goods and [E] again takes them (with the pickup
    /// feed), and what a loaded save makes of it. An [Explicit] pass photographs it all at 1920x1080.
    /// </summary>
    public sealed class CampChestTests : ForestSceneTestBase
    {
        // Lambdas stay in static helpers or capture only static fields: one capturing an iterator local
        // breaks after the domain reload EnterPlayMode triggers (see the note at the top of ForestFlowTests).

        private static float SegmentToBox(Vector3 a, Vector3 b, Bounds box)
        {
            float best = float.MaxValue;
            for (int i = 0; i <= 20; i++) best = Mathf.Min(best, Mathf.Sqrt(box.SqrDistance(Vector3.Lerp(a, b, i / 20f))));
            return best;
        }

        [Test]
        public void EachCampHasOneChestThatStandsClearAndCanBeReached()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var catalog = Map01ItemCatalog.Load();
            Assert.IsNotNull(catalog, "No item catalog: run ShadowVale > Map 1 > Nhập mô hình vật phẩm.");
            Assert.IsNotNull(catalog.chest, "No chest model.");
            foreach (var id in ForestInventory.Order.Where(i => i != "ammo_smg" && i != "ammo_shotgun"))
                Assert.IsNotNull(catalog.Model(id), $"No world model for {id}.");

            var start = Object.FindFirstObjectByType<CharacterController>().transform.position;
            NavMesh.SamplePosition(start, out var from, 6, NavMesh.AllAreas);
            var others = Object.FindObjectsByType<ForestPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => p.kind != ForestPointKind.Hide && p.kind != ForestPointKind.Cover).Select(p => p.transform.position).ToList();
            var stones = Resources.Load<Map01StoneSiteData>(Map01StoneSiteData.ResourcePath);
            if (stones != null) others.AddRange(stones.sites.Select(s => s.position));
            Physics.SyncTransforms();
            for (int n = 1; n <= 3; n++)
            {
                Assert.IsTrue(Map01CampChests.PlaceFor(n, out var at, out var facing), $"Camp C{n} not found.");
                var chest = Object.Instantiate(catalog.chest, at, facing);
                try
                {
                    var bounds = new Bounds(at, Vector3.zero);
                    foreach (var r in chest.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                    string name = $"C{n} chest at {at}";
                    // Nothing it would stand in: tent posts, the supply stack, sandbags, a tree.
                    var inside = Physics.OverlapBox(bounds.center + Vector3.up * .05f, bounds.extents * .95f - Vector3.up * .05f, facing, ~0, QueryTriggerInteraction.Ignore)
                        .Where(c => !(c is TerrainCollider) && !c.transform.IsChildOf(chest.transform) && c.GetComponentInParent<Map01EnemyController>() == null).ToArray();
                    Assert.IsEmpty(inside.Select(c => c.name), name + " stands in something.");
                    // Its front, where Nam opens it, is walkable, reachable from the start and faces the courtyard.
                    var front = at + facing * Vector3.forward * (bounds.extents.z + .7f);
                    Assert.IsTrue(NavMesh.SamplePosition(front, out var stand, .8f, NavMesh.AllAreas), name + ": nowhere to stand in front of it.");
                    var path = new NavMeshPath();
                    Assert.IsTrue(NavMesh.CalculatePath(from.position, stand.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, name + " cannot be walked to.");
                    var camp = GameObject.Find("Enemy outpost " + n).transform;
                    Assert.Greater(Vector3.Dot(facing * Vector3.forward, camp.TransformPoint(new Vector3(1.5f, 0, -1.5f)) - at), 0, name + " turns its back on the courtyard.");
                    // Its guards walk past it, not through it.
                    var guards = Map01CampChests.GuardsOf(n);
                    Assert.AreEqual(2, guards.Length, $"C{n} guards");
                    foreach (var guard in guards)
                    {
                        var beat = guard.PatrolPoints;
                        for (int i = 0; i < beat.Count; i++)
                            Assert.GreaterOrEqual(SegmentToBox(beat[i], beat[(i + 1) % beat.Count], bounds), .55f, name + " is on " + guard.name + "'s beat.");
                    }
                    foreach (var other in others)
                        Assert.GreaterOrEqual(new Vector2(other.x - at.x, other.z - at.z).magnitude, 3.9f, name + " crowds something else [E] reaches at " + other);
                }
                finally { Object.DestroyImmediate(chest); }
            }
        }

        // ---- in play --------------------------------------------------------------------------------

        private static Map01Mission mission;
        private static Map01Inventory inventory;
        private static Map01CampChests chests;
        private static Map01PlayerInteraction interaction;
        private static Map01Hud hud;
        private static Keyboard keyboard;

        private static void OpenMap()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
        }

        private static void Settle()
        {
            mission = Object.FindFirstObjectByType<Map01Mission>();
            inventory = mission.GetComponent<Map01Inventory>();
            chests = mission.GetComponent<Map01CampChests>();
            interaction = mission.GetComponent<Map01PlayerInteraction>();
            hud = mission.GetComponent<Map01Hud>();
            foreach (var enemy in mission.Enemies) { enemy.enabled = false; enemy.GetComponent<NavMeshAgent>().enabled = false; }
            // Jetty B's overseer scene starts within 40 m of Hùng; none of this is near it, but be sure.
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
        }

        private static ForestPoint ChestOfCamp(int n)
        {
            for (int i = 0; i < chests.Count; i++) if (chests.CampOf(chests.Point(i)) == n) return chests.Point(i);
            return null;
        }

        private static void StandAt(Vector3 at, Vector3 facing)
        {
            var controller = mission.player.GetComponent<CharacterController>();
            NavMesh.SamplePosition(at, out var ground, 2, NavMesh.AllAreas);
            controller.enabled = false;
            mission.player.SetPositionAndRotation(ground.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(facing - ground.position, Vector3.up)));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        /// <summary>Nam in front of the chest, facing it, where [E] reaches it.</summary>
        private static void StandBefore(ForestPoint chest)
        {
            var t = chests.ChestOf(chest);
            StandAt(t.position + t.forward * 1.1f, t.position);
        }

        private static void KillCamp(int n)
        {
            foreach (var guard in Map01CampChests.GuardsOf(n))
                guard.GetComponent<Health>().TakeDamage(99999, guard.transform.position, null);
        }

        private static IEnumerator PressE()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static int Holds(ForestPoint point, string item) => point.items.Where(i => i.item_id == item).Sum(i => i.count);
        private static (string item, int count)[] Contents(ForestPoint point) => point.items.Select(i => (i.item_id, i.count)).ToArray();

        /// <summary>Every gun in a chest lies within its item's found range (Map01Loot.json foundMin..foundMax).</summary>
        private static void AssertFoundCondition(ForestPoint point)
        {
            var data = Map01Loot.Load();
            foreach (var gun in point.items.Where(i => Map01Inventory.HasCondition(i.item_id)))
            {
                var item = data.ItemOf(gun.item_id);
                Assert.That(inventory.Fraction(gun.item_id, gun.condition), Is.InRange(item.foundMin - 1e-4f, item.foundMax + 1e-4f), gun.item_id);
            }
        }

        private static Dictionary<string, int> Holding() => ForestInventory.Order.ToDictionary(i => i, i => inventory.Count(i));
        private static bool FeedHas(string id) => hud.PickupFeed.Any(p => p.id == id);
        private static int PointsWithId(string id) => mission.Points.Count(p => p.id == id);

        [UnityTest]
        public IEnumerator ChestStaysLockedUntilItsCampFallsThenOpensToShowAndTakesInTwoPresses()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            Assert.IsNotNull(chests, "The mission lays the chests out.");
            Assert.AreEqual(3, chests.Count);
            for (int n = 1; n <= 3; n++)
            {
                var c = ChestOfCamp(n);
                Assert.AreEqual(ForestPointKind.Chest, c.kind);
                Assert.AreEqual(Map01CampChests.IdPrefix + n, c.id);
                Assert.AreEqual(1, PointsWithId(c.id), "The save tells the chests apart by id.");
                // Its signature things every run, rolls on top; any gun in it used but sound.
                foreach (var (item, count) in Map01CampChests.Guaranteed(n))
                    Assert.GreaterOrEqual(Holds(c, item), count, "C" + n + "'s chest always holds " + count + " " + item);
                AssertFoundCondition(c);
            }
            var routing = RouteInputToGame(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WaitGameSeconds(3.2f);   // the camp-cleared line is held back over the first seconds of a load

            // Locked while C1's two guards live: [E] says why and gives nothing.
            var chest = ChestOfCamp(1);
            StandBefore(chest);
            yield return null; yield return null;
            Assert.AreSame(chest, interaction.Nearby, "In front of the chest, it is what [E] offers.");
            Assert.AreEqual(Map01CampChests.State.Locked, chests.StateOf(chest));
            Assert.AreEqual("Hòm sắt khóa — còn 2 lính trại C1", chest.label);
            var before = Holding();
            yield return PressE();
            StringAssert.Contains("khóa chặt", mission.Dialogue);
            CollectionAssert.AreEqual(before, Holding(), "A locked chest gives nothing.");
            Assert.IsTrue(chests.LockShown(chest)); Assert.IsFalse(chests.LidOpen(chest));
            Assert.IsEmpty(chests.Waiting().ToArray(), "No camp is clear: nothing on the minimap.");

            // One guard down: still locked, one left.
            Map01CampChests.GuardsOf(1)[0].GetComponent<Health>().TakeDamage(99999, Vector3.zero, null);
            yield return null;
            Assert.AreEqual("Hòm sắt khóa — còn 1 lính trại C1", chest.label);
            KillCamp(1);
            yield return null; yield return null;
            Assert.AreEqual(Map01CampChests.State.Ready, chests.StateOf(chest));
            StringAssert.Contains("Trại C1 đã sạch lính", mission.Dialogue);
            Assert.AreEqual("Mở hòm sắt", chest.label);
            Assert.AreEqual(1, chests.Waiting().Count(), "The minimap marks the chest waiting in the taken camp.");
            Assert.AreEqual(Map01CampChests.State.Locked, chests.StateOf(ChestOfCamp(2)), "The other camps stay locked.");

            // First [E]: the lock comes off, the lid swings up, the goods lie on the tray; nothing taken yet.
            yield return PressE();
            Assert.AreEqual(Map01CampChests.State.Open, chests.StateOf(chest));
            Assert.IsFalse(chests.LockShown(chest));
            Assert.AreEqual(chest.items.Length, chests.ContentsShown(chest), "Every item lies on the tray.");
            StringAssert.Contains("Trong hòm: ", mission.Dialogue);   // C1 no longer holds the K-54: Nam starts with it
            StringAssert.Contains("đạn k-54", mission.Dialogue);
            var inChest = Contents(chest);
            CollectionAssert.AreEqual(before, Holding(), "Opening shows; it does not take.");
            yield return WaitGameSeconds(.9f);
            Assert.IsTrue(chests.LidOpen(chest), "The lid has swung open.");
            Assert.AreEqual("Lấy hết đồ trong hòm", chest.label);

            // Second [E]: everything into the bag, each in the pickup feed.
            yield return WaitGameSeconds(3.5f);    // Nam's opening take
            yield return PressE();
            foreach (var (item, count) in inChest)
            {
                Assert.AreEqual(before[item] + count, inventory.Count(item), item);
                Assert.IsTrue(FeedHas(item), item + " shows in the pickup feed.");
            }
            Assert.IsTrue(string.IsNullOrEmpty(mission.Dialogue), "Taking the chest clears its preview and uses the pickup feed only.");
            Assert.AreEqual(Map01CampChests.State.Taken, chests.StateOf(chest));
            Assert.IsTrue(chest.used);
            Assert.AreEqual(0, chests.ContentsShown(chest));
            Assert.IsEmpty(chests.Waiting().ToArray(), "Emptied: off the minimap.");
            var after = Holding();
            yield return WaitGameSeconds(3.5f);
            yield return PressE();
            CollectionAssert.AreEqual(after, Holding(), "An empty chest gives nothing twice.");

            // A loaded save: from before it was taken (shut, unlocked: the camp is still clear), and after.
            chest.used = false; yield return null; yield return null;
            Assert.AreEqual(Map01CampChests.State.Ready, chests.StateOf(chest));
            Assert.IsTrue(chests.LockShown(chest)); Assert.IsFalse(chests.LidOpen(chest));
            chest.used = true; yield return null; yield return null;
            Assert.IsTrue(chests.LidOpen(chest)); Assert.IsFalse(chests.LockShown(chest)); Assert.AreEqual(0, chests.ContentsShown(chest));

            // What came out has a name and an icon of its own in the bag; the same pickup again merges its row.
            foreach (var item in ForestInventory.RenderedIcons)
            {
                Assert.IsNotNull(hud.ItemIcon(item), item + " has no icon.");
                Assert.AreNotEqual(item, ForestInventory.Name(item), item + " has no name.");
            }
            inventory.Receive("cloth", 1); inventory.Receive("cloth", 2);
            Assert.AreEqual(3, hud.PickupFeed.First().count, "Two quick pickups of cloth share one row.");

            // What is made at the bench shows in the feed as well: a smoke bomb from what C2's chest holds.
            var bench = mission.Interactables.First(p => p.kind == ForestPointKind.Workbench);
            StandAt(bench.transform.position + Vector3.right, bench.transform.position);
            yield return null;
            inventory.Add("smoke_module", 1);
            int bombs = inventory.Count("smoke_bomb");
            Assert.IsTrue(inventory.TryCraft("craft_smoke_bomb"));
            yield return WaitGameSeconds(4.3f);
            Assert.AreEqual(bombs + 1, inventory.Count("smoke_bomb"));
            Assert.IsTrue(FeedHas("smoke_bomb"), "A crafted smoke bomb shows in the pickup feed.");
            InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing);
            yield return new ExitPlayMode();
        }

        // ---- a look at it all, at 1920x1080 --------------------------------------------------------------

        private const string Out = "Docs/Previews/Map01Items/";
        private static Vector3 eye, aim;

        private static IEnumerator Photo(string name)
        {
            string path = Out + name + ".png";
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
        }

        private static IEnumerator Reframe()
        {
            mission.gameCamera.GetComponent<ThirdPersonCamera>().SetCinematicView(eye, Quaternion.LookRotation(aim - eye), 50, 1);
            for (int i = 0; i < 4; i++) yield return null;
        }

        private static void FreeCamera() => mission.gameCamera.GetComponent<ThirdPersonCamera>().ClearCinematicView();

        /// <summary>Nam at one end of the chest, facing it (in reach of [E], out of the camera's way); the
        /// camera in front of it and above, looking down onto the tray.</summary>
        private static IEnumerator AtChest(ForestPoint chest, float height = 1.5f)
        {
            var t = chests.ChestOf(chest);
            var end = t.position + t.right * 1.35f;
            if (!NavMesh.SamplePosition(end, out var hit, .4f, NavMesh.AllAreas)) end = t.position - t.right * 1.35f;
            StandAt(end, t.position);
            aim = t.position + Vector3.up * .32f;
            eye = t.position + t.forward * 1.55f + Vector3.up * height - t.right * .25f;
            yield return Reframe();
        }

        private static void SelectInBag(string id)
        {
            typeof(Map01Hud).GetField("selectedStack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(hud, inventory.InventoryStacks().FindIndex(s => s.Id == id));
        }

        private static void ClearFeed()
        {
            var list = (System.Collections.IList)typeof(Map01Hud).GetField("pickups", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(hud);
            list.Clear();
        }

        private static Bounds BoundsOf(GameObject what)
        {
            var b = new Bounds(what.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in what.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>A world model to look at, without the colliders and scripts the game's own prefabs carry,
        /// at its catalog size, lying on its flattest side (the rifle and knife prefabs are posed for the hand),
        /// its longest side along the camp's X.</summary>
        private static GameObject Showpiece(string id, float yaw)
        {
            var catalog = Map01ItemCatalog.Load();
            var shown = Object.Instantiate(catalog.Model(id));
            shown.name = "Showpiece " + id;
            foreach (var c in shown.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var b in shown.GetComponentsInChildren<MonoBehaviour>()) Object.DestroyImmediate(b);
            foreach (var a in shown.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
            shown.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            shown.transform.localScale *= catalog.Scale(id);
            if (id == "rifle_standard" || id == "knife")
            {
                var e = BoundsOf(shown).extents;
                if (e.x <= e.y && e.x <= e.z) shown.transform.rotation = Quaternion.Euler(0, 0, 90);
                else if (e.z <= e.x && e.z <= e.y) shown.transform.rotation = Quaternion.Euler(90, 0, 0);
                e = BoundsOf(shown).extents;
                if (e.z > e.x) shown.transform.rotation = Quaternion.Euler(0, 90, 0) * shown.transform.rotation;
            }
            shown.transform.rotation = Quaternion.Euler(0, yaw, 0) * shown.transform.rotation;
            return shown;
        }

        /// <summary>Sets a showpiece down with its bottom centre on <paramref name="at"/>.</summary>
        private static void SetDown(GameObject shown, Vector3 at)
        {
            var b = BoundsOf(shown);
            shown.transform.position += at - new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static Vector3 Ground(Vector3 near)
        {
            foreach (var hit in Physics.RaycastAll(near + Vector3.up * 3, Vector3.down, 8, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.collider is TerrainCollider || hit.normal.y > .8f) return hit.point;
            return near;
        }

        private static void HideCampGuards(int n)
        {
            foreach (var g in Map01CampChests.GuardsOf(n)) g.gameObject.SetActive(false);
        }

        private static IEnumerator OpenAndTake(int camp, string prefix)
        {
            var chest = ChestOfCamp(camp);
            KillCamp(camp); yield return WaitGameSeconds(1.2f); HideCampGuards(camp);
            yield return AtChest(chest);
            yield return PressE();
            yield return WaitGameSeconds(1.2f);
            yield return AtChest(chest);
            ClearFeed();
            yield return Photo(prefix + "-chest-open");
            yield return WaitGameSeconds(2.6f);
            ClearFeed();
            yield return PressE();
            yield return WaitGameSeconds(.6f);
            yield return Photo(prefix + "-taken-pickup-feed");
        }

        [UnityTest, Explicit("Photographs for review; writes Docs/Previews/Map01Items.")]
        public IEnumerator PhotographTheChestsAndEveryItemInTheWorldAndInTheBag()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            SetPreviewResolution(1920, 1080);
            Directory.CreateDirectory(Out);
            foreach (var old in Directory.GetFiles(Out, "*.png")) File.Delete(old);
            var routing = RouteInputToGame(); keyboard = InputSystem.AddDevice<Keyboard>();
            typeof(Map01Rescue).GetField("nextChatter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(mission.GetComponent<Map01Rescue>(), float.MaxValue);
            yield return WaitGameSeconds(3.2f);

            // C1: locked with its guards about; [E] says why; the camp falls; open; taken.
            var chest = ChestOfCamp(1);
            yield return AtChest(chest, 1.25f);
            mission.Say(null, -1f);
            yield return Photo("01-C1-chest-locked");
            yield return PressE();
            yield return WaitGameSeconds(.3f);
            yield return Photo("02-C1-locked-message");
            KillCamp(1);
            yield return WaitGameSeconds(1.5f);
            HideCampGuards(1);
            FreeCamera();
            var t = chests.ChestOf(chest);
            StandAt(t.position + t.forward * 3.4f - t.right * .8f, t.position);
            yield return WaitGameSeconds(.8f);
            yield return Photo("03-C1-camp-cleared");
            yield return AtChest(chest);
            yield return PressE();
            yield return WaitGameSeconds(1.2f);
            yield return AtChest(chest);
            ClearFeed();
            yield return Photo("04-C1-chest-open");
            yield return WaitGameSeconds(2.6f);
            ClearFeed();
            yield return PressE();
            yield return WaitGameSeconds(.6f);
            yield return Photo("05-C1-taken-pickup-feed");
            yield return OpenAndTake(2, "06-C2");
            yield return OpenAndTake(3, "08-C3");

            // The bag with the new things picked out one by one.
            FreeCamera();
            ClearFeed();
            int shot = 10;
            foreach (var id in new[] { "ammo_pistol", "ammo_sniper" })
            {
                mission.Say(null, -1f);
                mission.SetPaused(true); mission.SetInventoryOpen(true);
                SelectInBag(id);
                for (int i = 0; i < 6; i++) yield return null;
                yield return Photo($"{shot++:00}-bag-{id}");
                mission.SetInventoryOpen(false); mission.SetPaused(false);
            }

            // Every item in the world, laid out in rows on the ground of camp C2's courtyard, then each up close.
            var camp = GameObject.Find("Enemy outpost 2").transform;
            StandAt(camp.TransformPoint(new Vector3(-3.6f, 0, -3.6f)), camp.position);
            var ids = ForestInventory.Order.Where(i => i != "ammo_smg" && i != "ammo_shotgun").ToArray();
            // Measured as built (unturned), so a row is as long as its items really are; then turned with the camp.
            var pieces = ids.Select(id => Showpiece(id, 0)).ToArray();
            float x = 0, z = 0, rowDepth = 0; const float rowWidth = 3.3f, gap = .22f;
            var local = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var size = BoundsOf(pieces[i]).size;
                float w = size.x, d = size.z;
                if (x + w > rowWidth && x > 0) { x = 0; z -= rowDepth + gap; rowDepth = 0; }
                local[i] = new Vector3(-.9f + x + w / 2, 0, -.2f + z - d / 2);
                x += w + gap; rowDepth = Mathf.Max(rowDepth, d);
            }
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].transform.rotation = Quaternion.Euler(0, camp.eulerAngles.y, 0) * pieces[i].transform.rotation;
                SetDown(pieces[i], Ground(camp.TransformPoint(local[i])));
            }
            var all = new Bounds(pieces[0].transform.position, Vector3.zero);
            foreach (var p in pieces) all.Encapsulate(BoundsOf(p));
            // Steeply from above, the lay-out up and to the right of centre: clear of the panels on the left and the weapons at the bottom right.
            float r = all.extents.magnitude;
            aim = all.center - camp.right * r * .18f - camp.forward * r * .12f;
            eye = aim - camp.forward * (r * .45f + .3f) + Vector3.up * (r * 1.45f + .6f);
            yield return Reframe();
            mission.Say(null, -1f); ClearFeed();
            yield return WaitGameSeconds(.3f);
            yield return Photo($"{shot:00}-all-items-in-camp");

            for (int i = 0; i < pieces.Length; i++)
            {
                for (int k = 0; k < pieces.Length; k++) pieces[k].SetActive(k == i);   // each on its own
                var b = BoundsOf(pieces[i]);
                float size = Mathf.Max(.12f, b.size.magnitude);
                aim = b.center;
                eye = b.center - camp.forward * (size * .85f + .12f) + Vector3.up * (size * .75f + .12f) + camp.right * size * .2f;
                yield return Reframe();
                mission.Say(null, -1f);
                yield return WaitGameSeconds(.15f);
                yield return Photo($"item-{ids[i]}");
            }
            foreach (var p in pieces) Object.Destroy(p);

            InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing);
            yield return new ExitPlayMode();
        }

        /// <summary>The workbench in base A without the HUD, as the ground for designing the bench's crafting
        /// screen: Nam at the bench from the game's own camera, and the bench close up.</summary>
        [UnityTest, Explicit("Photographs for review; writes Docs/Previews/Map01Items/bench-*.png.")]
        public IEnumerator PhotographTheWorkbenchWithoutTheHud()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            SetPreviewResolution(1920, 1080);
            Directory.CreateDirectory(Out);
            hud.enabled = false;
            var bench = mission.Interactables.First(p => p.kind == ForestPointKind.Workbench);
            var b = bench.transform.position;
            NavMesh.SamplePosition(b, out var near, 3, NavMesh.AllAreas);
            var side = Vector3.ProjectOnPlane(near.position - b, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            StandAt(b + side.normalized * 1.1f, b);
            mission.Say(null, -1f);
            yield return WaitGameSeconds(1.5f);
            yield return Photo("bench-third-person");
            aim = b + Vector3.up * .8f;
            eye = b + side.normalized * 2.4f + Vector3.up * 1.7f + Vector3.Cross(Vector3.up, side.normalized) * .8f;
            yield return Reframe();
            yield return WaitGameSeconds(.4f);
            yield return Photo("bench-close");
            yield return new ExitPlayMode();
        }
    }
}
