using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class EndingCutsceneTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator ClearedPointThreeCanReportWithEWhileBesideAWorkbench()
        {
            UnityEditor.SessionState.SetString(ForestSaveSlots.EditorTestRootKey,
                Path.GetFullPath("Logs/Point3Report/saves/" + System.Guid.NewGuid().ToString("N")));
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            foreach(var enemy in mission.Enemies)enemy.enabled=false;
            var quest=mission.GetComponent<Map01Quest>();var story=mission.GetComponent<Map01Story>();
            quest.RestoreStoryStage(Map01Quest.BossStage,OfficerReported());
            var bench=mission.Interactables.First(p=>p.kind==ForestPointKind.Workbench);
            PlaceNam(mission,bench.transform.position);yield return null;
            var interaction=mission.GetComponent<Map01PlayerInteraction>();
            var interact=typeof(Map01PlayerInteraction).GetMethod("HandleInteractKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var nearby=typeof(Map01PlayerInteraction).GetField("<Nearby>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            nearby.SetValue(interaction,bench); // No physical keyboard is installed in the batch Editor.
            Assert.AreSame(bench,interaction.Nearby);
            interact.Invoke(interaction,null);
            Assert.IsTrue(mission.BenchOpen,"Before a report is pending E should still open the workbench.");
            mission.CloseGameplayPanel();
            foreach(var enemy in mission.Enemies.ToArray())if(enemy.Alive)enemy.GetComponent<Health>().TakeDamage(999999,enemy.transform.position,null);
            var ending=mission.GetComponent<Map01EndingCutscene>();Assert.NotNull(ending);ending.Skip();yield return null;
            Assert.AreEqual(Map01Quest.ReportBossStage,quest.Stage);
            Assert.IsTrue(story.SafeToReport);Assert.IsFalse(mission.Enemies.Any(e=>e.Alive));
            nearby.SetValue(interaction,bench);
            Assert.AreSame(bench,interaction.Nearby);
            interact.Invoke(interaction,null);
            var dialogue=mission.GetComponent<Map01StoryDialogue>();
            Assert.IsTrue(dialogue.IsPlaying,"A nearby workbench must not consume E while the final commander report is pending.");
            Assert.IsFalse(mission.BenchOpen);
            Assert.AreEqual("m1_ops_retreat_01",dialogue.Current.voice);
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.ExtractionStage,quest.Stage);
            Assert.IsTrue(story.Progress.retreatReported);
            mission.GetComponentInChildren<Map01Extraction>().autoContinueToMap2=false;
            yield return new ExitPlayMode();
            UnityEditor.SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
        }

        [UnityTest]
        public IEnumerator ReplacementBossFinaleEndsInItsGroundedDeathState()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            var quest = mission.GetComponent<Map01Quest>();
            quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
            var boss = quest.CommanderEnemy;
            var actor = boss.GetComponentInChildren<Animator>();
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            var ending = mission.GetComponent<Map01EndingCutscene>();
            Assert.IsTrue(ending.IsPlaying);
            string[] deaths = { "Enemy_Death_Backward", "Enemy_Death_Forward", "Enemy_Death_Shot", "Enemy_Death_Knees", "Enemy_Death_Stomach" };
            Assert.AreEqual(deaths[actor.GetInteger("DeathIndex")], ending.ReusedDeathClip.name);
            yield return WaitGameSeconds(ending.Duration + .2f);
            Assert.IsFalse(ending.IsPlaying);
            Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Die_" + actor.GetInteger("DeathIndex")));
            Assert.Less(actor.GetBoneTransform(HumanBodyBones.Head).position.y, boss.transform.position.y + .9f);
            Assert.Less(Mathf.Abs(boss.GetComponent<Map01EnemyGrounding>().LastCorrection), .02f);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ReplacementBossReceivesDeathOnItsOwnAnimator()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            var boss = mission.GetComponent<Map01Quest>().CommanderEnemy;
            var actor = boss.GetComponentInChildren<Animator>();
            var health = boss.GetComponent<Health>();
            var binding = new UnityEditor.SerializedObject(health).FindProperty("animator").objectReferenceValue;
            Assert.AreSame(actor, binding, "Health must target the replacement model rather than the destroyed guard model.");
            int index = actor.GetInteger("DeathIndex");
            health.TakeDamage(999999, boss.transform.position, null);
            yield return WaitGameSeconds(.5f);
            Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Die_" + index), "Ordinary death must leave the aiming state.");
            yield return WaitGameSeconds(actor.GetCurrentAnimatorStateInfo(0).length + .2f);
            Assert.Less(actor.GetBoneTransform(HumanBodyBones.Head).position.y, boss.transform.position.y + .9f, "The defeated boss must collapse rather than remain upright.");
            Assert.Less(Mathf.Abs(boss.GetComponent<Map01EnemyGrounding>().LastCorrection), .02f, "Settled corpse must stay grounded.");
            yield return new ExitPlayMode();
        }
        /// <summary>
        /// 21/10/2026: the officer killed before Nam watched the camp still completes point 3 (his body is the
        /// confirmation); while men still hunt Nam the objective counts them; once clear, Nam radios for the way out
        /// by himself.
        /// </summary>
        [UnityTest]
        public IEnumerator AnEarlyKillCountsAndTheRadioCallComesByItselfOnceClear()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var story = mission.GetComponent<Map01Story>();
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            quest.RestoreStoryStage(Map01Quest.BossStage, RadioSilenced()); // Never watched the camp.
            Assert.IsFalse(story.Progress.commanderConfirmed);
            var boss = quest.CommanderEnemy;
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage, "His body is the confirmation: point 3 is done.");
            var ending = mission.GetComponent<Map01EndingCutscene>();
            double deadline = Time.realtimeSinceStartupAsDouble + (ending != null ? ending.Duration : 0) + 3;
            while (ending != null && ending.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;

            var hunter = mission.Enemies.First(e => e != null && e.Alive && !e.IsBoss);
            hunter.EngageFor(30f);
            Assert.IsFalse(story.SafeToReport);
            StringAssert.Contains("1 lính đang truy lùng", story.Objective, "Who still hunts him, by count.");
            yield return WaitGameSeconds(AutoWait);
            Assert.IsFalse(mission.GetComponent<Map01StoryDialogue>().IsPlaying, "No call while still hunted.");

            hunter.Brain.ResetToPost(Time.time);
            Assert.IsTrue(story.SafeToReport);
            yield return WaitGameSeconds(AutoWait);
            Assert.IsTrue(mission.GetComponent<Map01StoryDialogue>().IsPlaying, "Clear of the fight: the radio call starts without E.");
            story.TryInteract();
            Assert.IsTrue(mission.GetComponent<Map01StoryDialogue>().IsPlaying, "E during the call does not end or restart it.");
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.ExtractionStage, quest.Stage);
            yield return new ExitPlayMode();
        }

        private const float AutoWait = Map01Story.AutoRetreatReportSeconds + .8f;

        /// <summary>
        /// The officer of point 3, seen and reported, falls: his own death clip plays as the finale (world time
        /// untouched, no saving meanwhile); after it Nam reports by radio for the way out, and that starts the extraction.
        /// </summary>
        [UnityTest]
        public IEnumerator DefeatUsesExistingDeathThenTheRetreatReportStartsExtraction()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var story = mission.GetComponent<Map01Story>();
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            // He holds his headquarters from the map's start (Map01C3Mission), never spawned at the boss stage.
            var boss = quest.CommanderEnemy;
            Assert.IsNotNull(boss, "The officer is at point 3 from the start.");
            quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
            Assert.AreSame(boss, quest.CommanderEnemy, "The boss stage brings no second officer.");
            Assert.AreEqual(1, mission.Enemies.Count(e => e.IsBoss));
            Assert.IsNotNull(boss.GetComponent<Health>(), "Boss health");

            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            var ending = mission.GetComponent<Map01EndingCutscene>();
            Assert.IsNotNull(ending, "Ending created after lethal damage");
            Assert.IsTrue(ending.IsPlaying);
            Assert.IsTrue(mission.Cinematic);
            Assert.IsFalse(mission.CameraInputEnabled);
            Assert.IsFalse(mission.GetComponent<Map01SaveSystem>().CanSave());
            Assert.AreEqual(1, Time.timeScale, "Slow only the existing death clip, not world time.");
            CollectionAssert.Contains(boss.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips,
                ending.ReusedDeathClip);
            yield return WaitGameSeconds(.9f);
            Capture(mission.gameCamera, "collapse-start");
            yield return WaitGameSeconds(1.5f);
            Capture(mission.gameCamera, "collapse-middle");
            double deadline = Time.realtimeSinceStartupAsDouble + ending.Duration + 3;
            while (ending.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(ending.IsPlaying);
            Assert.IsFalse(mission.Cinematic);
            Assert.IsTrue(boss.GetComponent<Health>().IsDead);
            var fallen = boss.GetComponentInChildren<Animator>();
            Assert.IsTrue(fallen.GetCurrentAnimatorStateInfo(0).IsName("Die_" + fallen.GetInteger("DeathIndex")), "The finale must hand off to the matching death state.");
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage, "The finale leaves the retreat report to Nam.");
            var early = mission.GetComponentInChildren<Map01Extraction>();
            Assert.IsTrue(early == null || early.CurrentPhase == Map01Extraction.Phase.Dormant, "No extraction before the report.");
            Capture(mission.gameCamera, "collapse-final");

            // The radio report from where he stands gives the way out.
            Assert.IsTrue(story.SafeToReport);
            Assert.IsTrue(story.TryInteract());
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.ExtractionStage, quest.Stage);
            Assert.AreEqual(Map01Extraction.Phase.Extraction, mission.GetComponentInChildren<Map01Extraction>().CurrentPhase,
                "The report was the radio call: straight on to the boat.");
            Assert.IsFalse(mission.Cinematic);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(ForestMenu.Visible);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SkipIsIdempotentAndLegacyVictoryDoesNotReplay()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
            var boss = quest.CommanderEnemy;
            Assert.IsNotNull(boss, "The officer is at point 3.");
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            var ending = mission.GetComponent<Map01EndingCutscene>();
            Assert.IsNotNull(ending, "Ending created after lethal damage");
            Assert.IsTrue(ending.IsPlaying);
            ending.Skip(); ending.Skip();
            var fallen = boss.GetComponentInChildren<Animator>();
            Assert.IsTrue(fallen.GetCurrentAnimatorStateInfo(0).IsName("Die_" + fallen.GetInteger("DeathIndex")), "Skipping must retain the corpse pose.");
            Assert.IsFalse(ending.IsPlaying);
            Assert.IsFalse(mission.Cinematic);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage, "Skipping the finale does not skip the retreat report.");
            quest.TryCompleteCommanderObjective(true);
            Assert.IsFalse(ending.IsPlaying, "The objective completes once: no second finale.");

            // A save that was already won before the reports existed (no story progress kept): it is done, no replay.
            quest.RestoreStage(Map01Quest.ReportBossStage);
            Assert.AreEqual(Map01Quest.CompleteStage, quest.Stage);
            Assert.IsFalse(ending.IsPlaying, "An old victorious save must not replay the death.");
            yield return new ExitPlayMode();
        }

        private static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Logs/Ending");
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280,720,24);
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                File.WriteAllBytes("Logs/Ending/" + name + ".png", image.EncodeToPNG());
            } finally {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }
    }
}
