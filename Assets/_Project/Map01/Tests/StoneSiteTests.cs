using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Stones to pick up (Map01StoneSites). The places are worked out by an editor tool and saved; the
    /// first test holds the saved places to the tool's rules against the map as it is now, so moving a
    /// camp or a bridge without running "ShadowVale > Map 1 > Rải đá nhặt được" again shows up here.
    /// </summary>
    public sealed class StoneSiteTests : ForestSceneTestBase
    {
        // Lambdas stay in static helpers: one capturing an iterator local breaks after the domain
        // reload EnterPlayMode triggers (see the note at the top of ForestFlowTests).

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static Vector3[] Route(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var a, 6, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 6, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return new Vector3[0];
            return path.corners;
        }

        /// <summary>Every post and beat a guard watches from, with how far he sees plus the 2 m margin.</summary>
        private static List<(Vector3 at, float sight)> Watched()
        {
            var watched = new List<(Vector3, float)>();
            foreach (var guard in Object.FindObjectsByType<Map01EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                watched.Add((guard.transform.position, guard.VisionRange + 2));
                foreach (var point in guard.PatrolPoints) watched.Add((point, guard.VisionRange + 2));
            }
            var layout = Resources.Load<Map01RescueLayout>("Rescue/Map01RescueLayout");
            float squad = Mathf.Max(layout.patrolVision, Map01Rescue.SquadNames.Select(GameObject.Find).Where(g => g != null)
                .Max(g => g.GetComponent<Map01EnemyController>().VisionRange)) + 2;
            foreach (var post in layout.guardPosts) watched.Add((post.position, squad));
            foreach (var route in layout.patrolRoutes) if (route != null) foreach (Transform point in route) watched.Add((point.position, squad));
            return watched;
        }

        private static bool InSight(List<(Vector3 at, float sight)> watched, Vector3 point) => watched.Any(w => Flat(w.at, point) <= w.sight);

        private static float LongestStretchWithoutStones(Map01StoneSiteData data, List<(Vector3 at, float sight)> watched, Vector3[] corners)
        {
            float since = 0, longest = 0;
            for (int i = 1; i < corners.Length; i++)
            {
                float length = Vector3.Distance(corners[i - 1], corners[i]);
                for (float d = 0; d < length; d += 2)
                {
                    Vector3 at = Vector3.Lerp(corners[i - 1], corners[i], d / length);
                    since = InSight(watched, at) || data.sites.Any(s => Flat(s.position, at) <= 8f) ? 0 : since + 2;
                    longest = Mathf.Max(longest, since);
                }
            }
            return longest;
        }

        /// <summary>Where the rescue lays out its own piles (Map01RescueStones, on the layout's offsets round Hùng).</summary>
        private static List<Vector3> RescuePiles(Vector3 hung)
        {
            var piles = new List<Vector3>();
            foreach (var offset in Resources.Load<Map01RescueLayout>("Rescue/Map01RescueLayout").stoneOffsets)
                if (NavMesh.SamplePosition(hung + offset, out var pile, 4, NavMesh.AllAreas)) piles.Add(pile.position);
            return piles;
        }

        private static int PilesInRing(List<Vector3> piles, Vector3 centre, float inner, float outer) =>
            piles.Count(p => Flat(p, centre) >= inner && Flat(p, centre) <= outer);

        private static int InRing(Map01StoneSiteData data, Vector3 centre, float inner, float outer) =>
            data.sites.Count(s => Flat(s.position, centre) >= inner && Flat(s.position, centre) <= outer);

        [Test]
        public void SavedSitesKeepToThePlacementRulesOnTheMapAsItIs()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var data = Resources.Load<Map01StoneSiteData>(Map01StoneSiteData.ResourcePath);
            Assert.IsNotNull(data, "No stone sites saved: run ShadowVale > Map 1 > Rải đá nhặt được.");
            Assert.That(data.sites.Length, Is.InRange(26, 40), "About thirty sites.");
            Assert.GreaterOrEqual(data.pebbles.Count(p => p != null), 1, "The stones need a model.");
            Assert.Greater(data.riverbankRestockSeconds, 0f, "Gravel bars refill.");
            Assert.GreaterOrEqual(data.sites.Count(s => s.source == Map01StoneSiteData.Source.Riverbank), 6, "Gravel bars along the river are what nobody can run out of.");

            var river = GameObject.Find("Winding river").GetComponent<MeshFilter>();
            var water = river.sharedMesh.vertices.Select(v => river.transform.TransformPoint(v)).ToArray();
            float level = water.Average(v => v.y);
            var start = Object.FindFirstObjectByType<CharacterController>().transform.position;
            var watched = Watched();
            var piles = RescuePiles(Object.FindFirstObjectByType<Map01Mission>().hung.position);

            for (int i = 0; i < data.sites.Length; i++)
            {
                var site = data.sites[i];
                string name = $"Site {i} ({site.source}) at {site.position}";
                Assert.That(site.stones, Is.InRange(1, 4), name);
                Assert.IsTrue(NavMesh.SamplePosition(site.position, out var ground, 1f, NavMesh.AllAreas), name + " is not on walkable ground.");
                Assert.Greater(ground.position.y, level + .15f, name + " is in the water.");
                Assert.IsFalse(InSight(watched, site.position), name + " is in a guard's sight.");
                Assert.Greater(Route(start, site.position).Length, 1, name + " cannot be walked to from the base.");
                for (int other = 0; other < i; other++)
                    Assert.GreaterOrEqual(Flat(site.position, data.sites[other].position), 11.5f, name + $" crowds site {other}.");
                foreach (var pile in piles) Assert.GreaterOrEqual(Flat(site.position, pile), 11.5f, name + " crowds one of the rescue's own stone piles.");
                if (site.source == Map01StoneSiteData.Source.Riverbank)
                    Assert.LessOrEqual(water.Min(v => Flat(v, site.position)), 8f, name + " is not at the water's edge.");
            }

            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var camps = Enumerable.Range(1, 3).Select(n => GameObject.Find("Enemy outpost " + n).transform.position).ToArray();
            Assert.GreaterOrEqual(InRing(data, mission.hung.position, 28, 45) + PilesInRing(piles, mission.hung.position, 28, 45), 3, "Stones within reach of the jetty: ours outside the squad's sight, and the rescue's own piles.");
            for (int camp = 0; camp < camps.Length; camp++)
                Assert.GreaterOrEqual(InRing(data, camps[camp], 18, 40), 3, $"Stones round camp C{camp + 1}, outside its guards' sight.");

            var legs = new (string name, Vector3 from, Vector3 to)[]
            {
                ("base to jetty", start, mission.hung.position), ("base to C1", start, camps[0]),
                ("C1 to C2", camps[0], camps[1]), ("C2 to C3", camps[1], camps[2]), ("C3 to base", camps[2], start),
            };
            foreach (var leg in legs)
            {
                var corners = Route(leg.from, leg.to);
                Assert.Greater(corners.Length, 1, leg.name + " has no way through.");
                Assert.LessOrEqual(LongestStretchWithoutStones(data, watched, corners), 46f, $"From {leg.name}: too long a walk with no stones within 8 m.");
            }
        }

        private static Map01Mission mission;
        private static Map01Inventory inventory;
        private static Map01StoneSites sites;
        private static Map01PlayerInteraction interaction;

        private static ForestPoint First(Map01StoneSiteData.Source source, int skip = 0)
        {
            for (int i = 0; i < sites.Count; i++)
                if (sites.SourceOf(sites.Point(i)) == source && skip-- == 0) return sites.Point(i);
            return null;
        }

        private static int PointsWithId(string id) => mission.Points.Count(p => p.id == id);

        private static void StandAt(ForestPoint point)
        {
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = point.transform.position + new Vector3(.8f, .1f, 0); controller.enabled = true;
        }

        [UnityTest]
        public IEnumerator StonesArePickedUpToTheBagsLimitAndTheRiverBringsMore()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            mission = Object.FindFirstObjectByType<Map01Mission>();
            inventory = mission.GetComponent<Map01Inventory>();
            sites = mission.GetComponent<Map01StoneSites>();
            interaction = mission.GetComponent<Map01PlayerInteraction>();
            foreach (var enemy in mission.Enemies) { enemy.enabled = false; enemy.GetComponent<NavMeshAgent>().enabled = false; }
            mission.ModernHealth.CinematicInvulnerable = true;

            var data = Resources.Load<Map01StoneSiteData>(Map01StoneSiteData.ResourcePath);
            Assert.IsNotNull(sites, "The mission lays the stones out.");
            Assert.AreEqual(data.sites.Length, sites.Count);
            Assert.AreEqual(mission.Settings.startingStones, inventory.Stones);
            Assert.AreEqual(15, inventory.StoneCapacity);
            // The save records used points by id: every site has to be one of the mission's points, under its own id.
            for (int i = 0; i < sites.Count; i++)
            {
                Assert.Contains(sites.Point(i), mission.Points, "Site " + i);
                Assert.AreEqual(1, PointsWithId(sites.Point(i).id), "Id " + sites.Point(i).id);
                Assert.AreEqual(data.sites[i].stones, sites.Showing(sites.Point(i)), $"Site {i} shows the stones it has.");
            }

            // By the real key: walk up to the stones at a boulder, read the prompt, press [E].
            var routing = RouteInputToGame(); var keyboard = InputSystem.AddDevice<Keyboard>();
            var boulder = First(Map01StoneSiteData.Source.Boulder);
            StandAt(boulder);
            yield return null; yield return null;
            Assert.AreSame(boulder, interaction.Nearby, "Standing at the stones, they are what [E] offers.");
            Assert.AreEqual("Nhặt đá (+2)", boulder.label);
            // Small stones are lost in the undergrowth, so the ones a few steps away are dotted on screen.
            var hud = mission.GetComponent<Map01Hud>();
            Assert.AreEqual(1f, hud.StoneMarker(boulder), "Stones a step away are pointed out.");
            Assert.AreEqual(0f, hud.StoneMarker(First(Map01StoneSiteData.Source.Riverbank)), "Not the ones further than a short walk.");
            int before = inventory.Stones;
            mission.Say(null, 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.AreEqual(before + 2, inventory.Stones, "[E] puts the stones in the bag.");
            Assert.IsTrue(boulder.used);
            yield return null;
            Assert.AreEqual(0, sites.Showing(boulder), "What was picked up no longer lies there.");
            Assert.AreEqual(0f, hud.StoneMarker(boulder), "Nor is it pointed out any more.");
            Assert.IsTrue(string.IsNullOrEmpty(mission.Dialogue), "Successful stone pickups use the item feed only.");
            Assert.IsTrue(hud.PickupFeed.Any(p => p.id == "stone" && p.count >= 2));

            // Up to the limit: the last site gives only what fits and keeps the rest.
            var bars = new[] { First(Map01StoneSiteData.Source.Riverbank), First(Map01StoneSiteData.Source.Riverbank, 1), First(Map01StoneSiteData.Source.Riverbank, 2) };
            interaction.Interact(bars[0]); interaction.Interact(bars[1]);
            Assert.AreEqual(before + 8, inventory.Stones);
            Assert.AreEqual(1, inventory.StoneRoom, "One short of full with the usual start of six.");
            interaction.Interact(bars[2]);
            Assert.AreEqual(15, inventory.Stones);
            Assert.IsFalse(bars[2].used, "Two stones are still there.");
            Assert.AreEqual(2, sites.Remaining(bars[2]));
            yield return null; yield return null;
            Assert.AreEqual(2, sites.Showing(bars[2]));
            StringAssert.Contains("còn 2 viên", mission.Dialogue);
            interaction.Interact(bars[2]);
            Assert.AreEqual(15, inventory.Stones, "A full bag takes nothing.");
            Assert.AreEqual(2, sites.Remaining(bars[2]));
            StringAssert.Contains("đã đầy", sites.Prompt(bars[2]));

            // The river refills its bars; stones at a boulder are gone for good.
            bars[0].restockAt = Time.time; boulder.restockAt = Time.time;
            yield return null; yield return null;
            Assert.IsFalse(bars[0].used, "A gravel bar has stones again once its time is up.");
            Assert.AreEqual(3, sites.Remaining(bars[0]));
            Assert.AreEqual(3, sites.Showing(bars[0]));
            Assert.IsTrue(boulder.used, "Boulder stones do not come back.");

            // What a restored save does: it sets which points are used. The stones follow either way.
            boulder.used = false;
            yield return null; yield return null;
            Assert.AreEqual(2, sites.Remaining(boulder)); Assert.AreEqual(2, sites.Showing(boulder));
            boulder.used = true;
            yield return null; yield return null;
            Assert.AreEqual(0, sites.Showing(boulder));

            // Nearly out: the map points at the nearest stones, and only then.
            var minimap = mission.GetComponent<Map01Minimap>();
            StandAt(bars[2]);
            yield return null; yield return null;
            Assert.IsNull(minimap.StoneHint, "With a full bag the map shows no stones.");
            Assert.AreEqual(0f, hud.StoneMarker(bars[2]), "Nor does the screen: there is no room for them.");
            inventory.Spend("stone", inventory.Stones - Map01Minimap.LowOnStones);
            Assert.IsNotNull(minimap.StoneHint, "Down to the last two, it shows where the nearest lie.");
            Assert.AreEqual(1f, hud.StoneMarker(bars[2]));
            Assert.Less(Vector3.Distance(minimap.StoneHint.transform.position, mission.player.position), 30f);

            // The rescue's own piles (Map01RescueStones) go through ordinary loot: they too stop at the limit and keep the rest.
            var pile = mission.GetComponent<Map01RescueStones>().Pickups[0];
            inventory.Spend("stone", inventory.Stones); inventory.Add("stone", 14);
            interaction.Interact(pile);
            Assert.AreEqual(15, inventory.Stones);
            Assert.IsFalse(pile.used, "The stones that did not fit are still there.");
            Assert.AreEqual(2, pile.items.Single(i => i.item_id == "stone").count);
            Assert.AreEqual("Đá ném — 2 viên", pile.label);
            interaction.Interact(pile);
            Assert.AreEqual(15, inventory.Stones, "A full bag takes nothing from the pile either.");
            Assert.AreEqual(2, pile.items.Single(i => i.item_id == "stone").count);
            StringAssert.Contains("đã đầy", mission.Dialogue);

            // Out altogether: reaching for one says where to look.
            inventory.Spend("stone", inventory.Stones);
            mission.GetComponent<Map01StoneThrow>().BeginAim();
            StringAssert.Contains("bờ sông", mission.Dialogue);
            Assert.IsFalse(mission.GetComponent<Map01StoneThrow>().Aiming);

            InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing);
            yield return new ExitPlayMode();
        }
    }
}
