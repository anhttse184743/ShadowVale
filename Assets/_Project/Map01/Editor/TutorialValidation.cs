using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Keep full NUnit results across the domain reloads used by scene tests.</summary>
    [InitializeOnLoad]
    public static class TutorialValidation
    {
        private const string Pending = "ShadowVale.Tutorial.TestResults";
        private static TestRunnerApi api;
        static TutorialValidation() { if (!string.IsNullOrEmpty(SessionState.GetString(Pending, ""))) Attach(); }
        private static void Attach() { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); }
        public static void RunTutorial() => Run("Temp/tutorial-tests.xml", "ShadowVale.Map01.Tests.TutorialSceneTests");
        public static void RunMap02Conversation() => Run("Temp/map02-conversation-tests.xml", "ShadowVale.Map01.Tests.Map02CastTests.ConversationFacesPartnerAtExactSpacingFromEveryApproach");
        public static void RunMap02Skills() => Run("Temp/map02-skills-tests.xml", "ShadowVale.Map01.Tests.Map02SkillsTests");
        public static void RunMap01() => Run("Temp/map01-regression.xml", null);
        public static void RunRegressionRecheck()
        {
            SessionState.SetString(Pending, "Temp/map01-recheck.xml"); Attach();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] {
                "ShadowVale.Map01.Tests.ForestMenuIntegrationTests",
                "ShadowVale.Map01.Tests.EndingCutsceneTests.DefeatUsesExistingDeathThenRadioAndExtraction",
                "ShadowVale.Map01.Tests.HungVisualTests.ModelAnimatesAndRescueNavigationSurvives",
                "ShadowVale.Map01.Tests.RescueCompatibilityTests.KnifeRejectsRangeFrontAndPhysicalObstructionThenKillsOverseerLast",
                "ShadowVale.Map01.Tests.ScoutingTests.AQuietKnifeTakedownAwayFromTheCampGoesUnnoticed"
            } }));
        }
        private static void Run(string path, string test)
        {
            SessionState.SetString(Pending, path); Attach();
            var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "ShadowVale.Map01.Tests" }, categoryNames = new[] { "!Replay" } };
            if (test != null) filter.testNames = new[] { test };
            api.Execute(new ExecutionSettings(filter));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = SessionState.GetString(Pending, "");
                if (string.IsNullOrEmpty(path)) return;
                TestRunnerApi.SaveResultToFile(result, path); SessionState.EraseString(Pending);
                Debug.Log($"[Tutorial validation] {path}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
            }
        }
    }
}
