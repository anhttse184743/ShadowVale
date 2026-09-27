using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class SightCoverTests : ForestSceneTestBase
    {
        /// <summary>A plant as the scene has them: a LOD group over a renderer, no solid collider.</summary>
        private static GameObject Plant(Vector3 feet, Vector3 size)
        {
            var root = new GameObject("Test plant");
            root.transform.position = feet;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<Collider>()); // Leaves stop no ray in the game either.
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * size.y / 2;
            body.transform.localScale = size;
            root.AddComponent<LODGroup>().SetLODs(new[] { new LOD(.01f, new[] { body.GetComponent<Renderer>() }) });
            return root;
        }

        [Test]
        public void BushesAndTreeCrownsHideWhatIsBehindThem()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Plant(Vector3.zero, new Vector3(2.4f, 1.8f, 2.4f));              // A bush.
            Plant(new Vector3(0, 0, 20), new Vector3(3, 8, 3));              // A tree: only its crown hides.
            Plant(new Vector3(0, 0, -20), new Vector3(2, .5f, 2));           // Ground grass: hides nothing.
            var cover = new GameObject("Sight cover").AddComponent<Map01SightCover>();
            cover.Build();
            Assert.AreEqual(2, cover.Count, "The bush and the tree count; the grass is too low.");

            Assert.IsTrue(cover.Blocks(new Vector3(-10, 1.3f, 0), new Vector3(10, 1.1f, 0)), "Behind a bush.");
            Assert.IsFalse(cover.Blocks(new Vector3(-10, 1.3f, 5), new Vector3(10, 1.1f, 5)), "Beside it.");
            Assert.IsTrue(cover.Blocks(new Vector3(-8, 1.3f, 0), new Vector3(0, .75f, 0)), "Crouched in the bush.");
            Assert.IsFalse(cover.Blocks(new Vector3(-2, 1.3f, 0), new Vector3(0, .75f, 0)), "Right next to it a guard sees in.");
            Assert.IsFalse(cover.Blocks(new Vector3(0, 1.3f, 0), new Vector3(10, 1.1f, 0)), "A guard standing in it sees out.");
            Assert.IsFalse(cover.Blocks(new Vector3(-10, 1.3f, 20), new Vector3(10, 1.1f, 20)), "Under a crown: the trunk's collider decides.");
            Assert.IsTrue(cover.Blocks(new Vector3(-10, 6f, 20), new Vector3(10, 5f, 20)), "Through the crown, from up a hill.");
            Assert.IsFalse(cover.Blocks(new Vector3(-10, .4f, -20), new Vector3(10, .4f, -20)), "Ground grass.");
        }

        private static Map01EnemyController OpenGuard(Map01Mission mission, out Vector3 stand)
        {
            // A guard with open ground ten metres in front of him, nothing in the way yet.
            foreach (var guard in mission.Enemies.Where(e => e.name.StartsWith("Outpost guard ")))
            {
                var eye = guard.transform.position + Vector3.up * 1.3f;
                if (!NavMesh.SamplePosition(guard.transform.position + guard.transform.forward * 10, out var hit, 2, NavMesh.AllAreas)) continue;
                var target = hit.position + Vector3.up * 1.1f;
                if (Physics.Linecast(eye, target, mission.ObstructionMask, QueryTriggerInteraction.Ignore) || mission.SightCover.Blocks(eye, target)) continue;
                stand = hit.position;
                return guard;
            }
            stand = default;
            Assert.Fail("No guard with a clear view in front of him.");
            return null;
        }

        [UnityTest]
        public IEnumerator AGuardDoesNotSeeNamBehindABushUntilItIsGone()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.IsNotNull(mission.SightCover, "Map01Mission brings the sight cover along.");
            Assert.Greater(mission.SightCover.Count, 1000, "Map 1's bushes and trees are cover.");
            var guard = OpenGuard(mission, out var stand);
            foreach (var e in mission.Enemies) e.enabled = e == guard;
            guard.Configure(System.Array.Empty<Vector3>());
            var agent = guard.GetComponent<NavMeshAgent>();
            if (agent.isOnNavMesh) agent.ResetPath();
            if (mission.player.TryGetComponent(out PlayerFootsteps steps)) steps.enabled = false; // No teleport thud for him to hear.
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = stand; controller.enabled = true;
            mission.Crouched = false;

            // A bush halfway between them.
            var middle = Vector3.Lerp(guard.transform.position, stand, .5f);
            var bush = Plant(new Vector3(middle.x, Mathf.Min(guard.transform.position.y, stand.y) - .3f, middle.z), new Vector3(2.6f, 2.6f, 2.6f));
            mission.SightCover.Build();
            yield return WaitGameSeconds(2f);
            Assert.AreEqual(0f, guard.Suspicion, "Behind the bush Nam is not seen.");
            Assert.IsFalse(guard.Engaged);

            Object.Destroy(bush);
            yield return null;
            mission.SightCover.Build();
            for (float until = Time.time + 4; guard.Suspicion < .3f && !guard.Engaged && Time.time < until;) yield return null;
            Assert.IsTrue(guard.Suspicion >= .3f || guard.Engaged, "In the open, he is seen.");
            Assert.IsTrue(Map01Hud.Saw(guard), "The HUD shows the eye, not the question mark.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AGuardWhoOnlyHeardNamShowsAQuestionMarkNotAnEye()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var guard = mission.Enemies.First(e => e.name.StartsWith("Outpost guard "));
            foreach (var e in mission.Enemies) e.enabled = e == guard;
            // A noise behind him, with Nam far away: he goes to check without having seen anything.
            Assert.IsTrue(guard.Hear(guard.transform.position - guard.transform.forward * 5, 10));
            yield return null;
            Assert.IsTrue(guard.Alerted);
            Assert.IsFalse(Map01Hud.Saw(guard), "Heard, not seen.");
            yield return new ExitPlayMode();
        }
    }
}
