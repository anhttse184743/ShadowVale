using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>Nam's and the guards' Blender clips (StoryAnimationSetup) in the story.</summary>
    public sealed class StoryAnimationTests : ForestSceneTestBase
    {
        private const string Controllers = "Assets/_Project/Art/Characters/Animations/Controllers/";

        [Test]
        public void ControllersCarryTheBlenderClips()
        {
            var player = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controllers + "AC_Player.controller");
            var playerStates = player.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state.name).ToList();
            foreach (var state in new[] { "Locomotion_Rifle", "Sneak_Rifle", "Aim_Rifle", "Reload_Rifle", "ReloadEmpty_Rifle",
                "Hit", "Act_Takedown", "Act_ThrowAim", "Act_Throw", "Act_Binoculars", "Act_PickUp", "Act_OpenChest", "Act_Bandage", "Act_Untie" })
                Assert.Contains(state, playerStates);
            Assert.IsTrue(player.layers[0].stateMachine.states.Any(s => s.state.name == "Act_Takedown"),
                "Full-body moves are base-layer states: only the base layer places a humanoid's body.");
            Assert.IsTrue(player.layers.Any(l => l.name == Map01NamActions.UpperLayer && l.avatarMask != null));
            Assert.IsTrue(player.layers.Any(l => l.name == Map01NamActions.UpperLayer && l.defaultWeight == 0), "The stone and binoculars layer is only raised while one plays.");
            var nam = player.animationClips.Where(c => c.name.StartsWith("Nam_")).ToList();
            Assert.GreaterOrEqual(nam.Count, 25);
            foreach (var clip in nam) { Assert.IsTrue(clip.humanMotion, clip.name); Assert.AreEqual(30, clip.frameRate, clip.name); }
            Assert.That(nam.First(c => c.name == "Nam_Takedown").length, Is.EqualTo(Map01NamActions.TakedownSeconds).Within(.05f));

            var enemy = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controllers + "AC_Enemy.controller");
            var enemyStates = enemy.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state.name).ToList();
            foreach (var state in new[] { "Locomotion", "Aim", "Fire", "Search", "Startled", "Hit", "TakedownVictim",
                "Die_0", "Die_1", "Die_2", "Die_3", "Die_4" })
                Assert.Contains(state, enemyStates);
            foreach (var clip in enemy.animationClips) Assert.IsTrue(clip.humanMotion, clip.name);
            Assert.That(enemy.animationClips.First(c => c.name == "Enemy_Takedown_Victim").length,
                Is.EqualTo(Map01NamActions.TakedownSeconds).Within(.05f), "Both halves of the takedown run the same length.");
        }

        /// <summary>
        /// Through the real hit: knife in hand, behind an unaware guard. Nam steps into place, the two clips
        /// run together — his left hand ends on the guard's mouth, the knife at the guard's neck — and his
        /// controls come back when it is over.
        /// </summary>
        [UnityTest]
        public IEnumerator KnifeTakedownPlaysBothHalvesTogether()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null;
            IsolateSaves();
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ScoutStage);
            yield return WaitGameSeconds(.5f);
            var guard = NearestGuard(mission);   // static: an iterator lambda's closure does not survive EnterPlayMode
            var agent = guard.GetComponent<NavMeshAgent>();
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            mission.ModernCombat.Equip(WeaponKind.Knife);
            { var na = mission.player.GetComponentInChildren<Animator>(); var ga = guard.GetComponentInChildren<Animator>();
              Directory.CreateDirectory("Logs/Story");
              File.WriteAllText("Logs/Story/heights.txt", "nam head above feet " + (na.GetBoneTransform(HumanBodyBones.Head).position.y - mission.player.position.y).ToString("F3") + " guard head above feet " + (ga.GetBoneTransform(HumanBodyBones.Head).position.y - guard.transform.position.y).ToString("F3") + " nam humanScale " + na.humanScale.ToString("F3") + " guard humanScale " + ga.humanScale.ToString("F3") + " guard model scale " + ga.transform.lossyScale.y.ToString("F3")); }
            var body = mission.player.GetComponent<CharacterController>();
            body.enabled = false;
            mission.player.SetPositionAndRotation(guard.transform.position - guard.transform.forward * 1.6f, guard.transform.rotation);
            body.enabled = true;
            yield return null;
            guard.GetComponent<Health>().TakeDamage(5, guard.transform.position + Vector3.up, mission.player.gameObject);
            yield return null; yield return null;
            var actions = mission.player.GetComponent<Map01NamActions>();
            Assert.IsNotNull(actions, "The takedown runs through Nam's story moves.");
            Assert.IsTrue(actions.Busy, "Nam is in the takedown.");
            Assert.IsTrue(guard.TakenDownSilently);
            Assert.IsFalse(mission.ModernPlayer.enabled, "His own controls wait while it plays.");
            var guardRifle = guard.GetComponentInChildren<Map01Rifle>(true);
            Assert.IsTrue(guardRifle == null || guardRifle.weapon == null || !guardRifle.weapon.gameObject.activeSelf,
                "Grabbed from behind, the guard's rifle drops out of the scene instead of sticking through Nam.");
            var namActor = mission.player.GetComponentInChildren<Animator>();
            var guardActor = guard.GetComponentInChildren<Animator>();
            Assert.That(Vector3.ProjectOnPlane(guard.transform.position - mission.player.position, Vector3.up).magnitude, Is.LessThan(.05f),
                "Both halves share one origin, as baked in Blender: Nam steps onto it, the guard's body stays put.");

            var camera = mission.gameCamera;
            yield return WaitGameSeconds(1f);
            Look(camera, guard.transform, guard.transform.right, "takedown-grab");
            // frame 76 of 141 (the thrust): the hand over his mouth, the blade in his neck
            for (float until = Time.time + 4; Time.time < until
                && !(namActor.GetCurrentAnimatorStateInfo(0).IsName("Act_Takedown") && namActor.GetCurrentAnimatorStateInfo(0).normalizedTime >= 76f / 141f);)
                yield return null;
            Assert.IsTrue(guardActor.GetCurrentAnimatorStateInfo(0).IsName("TakedownVictim"), "The guard plays his half, not a generic death.");
            Vector3 mouth = guardActor.GetBoneTransform(HumanBodyBones.Head).position + guard.transform.forward * .1f;
            float hand = Vector3.Distance(namActor.GetBoneTransform(HumanBodyBones.LeftHand).position, mouth);
            float knife = Vector3.Distance(namActor.GetBoneTransform(HumanBodyBones.RightHand).position,
                guardActor.GetBoneTransform(HumanBodyBones.Neck).position);
            File.WriteAllText("Logs/Story/takedown-metrics.txt", $"left hand to mouth {hand:F3} m, right hand to neck {knife:F3} m\n"
                + Diagnose(mission, namActor, guard, guardActor));
            Look(camera, guard.transform, guard.transform.right, "takedown-cut-right");
            Look(camera, guard.transform, -guard.transform.right, "takedown-cut-left");
            Look(camera, guard.transform, guard.transform.forward, "takedown-cut-front");
            Assert.Less(hand, .25f, "Nam's left hand is on the guard's mouth.");
            Assert.Less(knife, .35f, "At the thrust the knife hand is at the guard's neck.");
            yield return WaitGameSeconds(2.6f);
            Look(camera, guard.transform, guard.transform.right, "takedown-end");
            Assert.IsFalse(actions.Busy);
            Assert.IsTrue(mission.ModernPlayer.enabled && mission.ModernCombat.enabled, "Controls are back.");
            Assert.IsFalse(guard.Alive);
            yield return new ExitPlayMode();
        }

        /// <summary>With the AK in his hands Nam carries it his own way (Blender), and the guards hold theirs.</summary>
        [UnityTest]
        public IEnumerator RifleCarryUsesNamsOwnClips()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null;
            IsolateSaves();
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ScoutStage);
            mission.ModernCombat.Equip(WeaponKind.Rifle);
            yield return WaitGameSeconds(1f);
            var nam = mission.player.GetComponentInChildren<Animator>();
            Assert.IsTrue(nam.GetCurrentAnimatorStateInfo(0).IsName("Locomotion_Rifle"), "Nam's own AK carry.");
            int upper = nam.GetLayerIndex(PlayerCombat.UpperBodyLayer);
            Assert.Less(nam.GetLayerWeight(upper), .05f, "Carrying, his arms come from the carry take itself.");
            var camera = mission.gameCamera;
            Transform hips = nam.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 focus = hips.position + Vector3.up * .35f;
            Vector3 side = (mission.player.forward + mission.player.right * .8f).normalized;
            camera.transform.SetPositionAndRotation(focus + side * 1.6f + Vector3.up * .2f, Quaternion.LookRotation(-side - Vector3.up * .1f));
            Capture(camera, "nam-rifle-idle");
            side = (-mission.player.right + mission.player.forward * .3f).normalized;
            camera.transform.SetPositionAndRotation(focus + side * 1.6f + Vector3.up * .2f, Quaternion.LookRotation(-side - Vector3.up * .1f));
            Capture(camera, "nam-rifle-idle-left");
            var guard = NearestGuard(mission);
            var guardActor = guard.GetComponentInChildren<Animator>();
            Assert.IsTrue(guardActor.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            focus = guardActor.GetBoneTransform(HumanBodyBones.Hips).position + Vector3.up * .35f;
            side = (guard.transform.forward + guard.transform.right * .8f).normalized;
            camera.transform.SetPositionAndRotation(focus + side * 2f + Vector3.up * .2f, Quaternion.LookRotation(-side - Vector3.up * .1f));
            Capture(camera, "guard-patrol");
            yield return new ExitPlayMode();
        }

        private static string Diagnose(Map01Mission mission, Animator nam, Map01EnemyController guard, Animator guardActor)
        {
            Vector3 fwd = guard.transform.forward;
            var ns = nam.GetCurrentAnimatorStateInfo(0); var gs = guardActor.GetCurrentAnimatorStateInfo(0);
            float namHips = Vector3.Dot(nam.GetBoneTransform(HumanBodyBones.Hips).position - mission.player.position, fwd);
            float guardHips = Vector3.Dot(guardActor.GetBoneTransform(HumanBodyBones.Hips).position - guard.transform.position, fwd);
            return $"nam state takedown={ns.IsName("Act_Takedown")} t={ns.normalizedTime:F2} len={ns.length:F2} trans={nam.IsInTransition(0)} rootMotion={nam.applyRootMotion} modelLocal={nam.transform.localPosition:F3}\n" +
                $"guard state victim={gs.IsName("TakedownVictim")} t={gs.normalizedTime:F2} len={gs.length:F2}\n" +
                $"nam hips fwd of player {namHips:F2}, guard hips fwd of guard {guardHips:F2}, player->guard fwd {Vector3.Dot(guard.transform.position - mission.player.position, fwd):F2}\n" +
                Decompose(mission, nam, guard, guardActor) + "\n" +
                $"nam fwd vs guard fwd {Vector3.Angle(mission.player.forward, fwd):F0} deg, model fwd vs guard fwd {Vector3.Angle(nam.transform.forward, fwd):F0} deg, nam hips fwd axis vs guard {Vector3.Angle(nam.GetBoneTransform(HumanBodyBones.Hips).forward, fwd):F0} deg";
        }

        private static string Decompose(Map01Mission mission, Animator nam, Map01EnemyController guard, Animator guardActor)
        {
            Vector3 f = guard.transform.forward, r = guard.transform.right;
            Vector3 v = nam.GetBoneTransform(HumanBodyBones.RightHand).position - guardActor.GetBoneTransform(HumanBodyBones.Neck).position;
            Vector3 l = nam.GetBoneTransform(HumanBodyBones.LeftHand).position - guardActor.GetBoneTransform(HumanBodyBones.Head).position;
            float ground = guard.transform.position.y;
            return $"wrist-neck fwd {Vector3.Dot(v, f):F3} right {Vector3.Dot(v, r):F3} up {v.y:F3} | lefthand-head fwd {Vector3.Dot(l, f):F3} right {Vector3.Dot(l, r):F3} up {l.y:F3} | " +
                $"guard neck h {guardActor.GetBoneTransform(HumanBodyBones.Neck).position.y - ground:F3}, nam wrist h {nam.GetBoneTransform(HumanBodyBones.RightHand).position.y - ground:F3}, guard hips h {guardActor.GetBoneTransform(HumanBodyBones.Hips).position.y - ground:F3}, nam hips h {nam.GetBoneTransform(HumanBodyBones.Hips).position.y - ground:F3}, nam ground {mission.player.position.y - ground:F3}";
        }

        private static Map01EnemyController NearestGuard(Map01Mission mission)
        {
            Map01EnemyController best = null; float bestDistance = float.MaxValue;
            foreach (var e in mission.Enemies) {
                if (e == null || !e.Alive || !e.name.StartsWith("Outpost guard ")) continue;
                float d = Vector3.Distance(e.transform.position, mission.player.position);
                if (d < bestDistance) { bestDistance = d; best = e; }
            }
            return best;
        }

        private static void Look(Camera camera, Transform guard, Vector3 side, string name)
        {
            Vector3 focus = guard.position + Vector3.up * 1.3f - guard.forward * .5f;
            camera.transform.SetPositionAndRotation(focus + side * 2.6f + Vector3.up * .3f, Quaternion.LookRotation(-side - Vector3.up * .1f));
            Capture(camera, name);
        }

        private static void IsolateSaves()
        {
            ShaderUtil.allowAsyncCompilation = false;
            string root = "Logs/Story/TestSaves/" + System.Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
            typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, Path.GetFullPath(root));
        }

        private static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Logs/Story");
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes("Logs/Story/" + name + ".png", image.EncodeToPNG());
            } finally {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }
    }
}
