using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    [InitializeOnLoad]
    public static class Map01OperationsRecording
    {
        public const string Key="ShadowVale.OperationRecording";
        static TestRunnerApi runner;
        static string Prefix=>SessionState.GetString(Key+".prefix","");
        static Map01OperationsRecording(){EditorApplication.delayCall+=Reconnect;}
        static void Reconnect(){if(SessionState.GetBool(Key,false))Register();}
        static void Register(){if(runner!=null)return;runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());}
        public static void Record()
        {
            SessionState.SetString(Key+".prefix","");
            Directory.CreateDirectory("Logs/Map01Operations/video");File.WriteAllText("Logs/Map01Operations/video/test-cases.txt","");
            SessionState.SetBool(Key,true);Register();
            runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{
                "ShadowVale.Map01.Tests.Map01OperationsTests.SupplyRequiresKeyPuzzleReportAndPhysicalHandoff",
                "ShadowVale.Map01.Tests.Map01OperationsTests.RadioFailureCallsOnlyOneSavedSquadAndSuccessDoesNotExtract",
                "ShadowVale.Map01.Tests.Map01OperationsTests.OfficerNeedsDistantBinocularsAndRestoresCombatBeforeDiscovery",
                "ShadowVale.Map01.Tests.Map01RevisionTests.EarlyOfficerDeathCannotSkipPointThree"}}));
        }
        public static void RecordRadio()
        {
            SessionState.SetString(Key+".prefix","radio-");Directory.CreateDirectory("Logs/Map01Operations/video");File.WriteAllText("Logs/Map01Operations/video/radio-test-cases.txt","");
            SessionState.SetBool(Key,true);Register();runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"ShadowVale.Map01.Tests.Map01OperationsTests.RadioFailureCallsOnlyOneSavedSquadAndSuccessDoesNotExtract"}}));
        }
        public static void RecordClarity()
        {
            SessionState.SetString(Key+".prefix","clarity-");Directory.CreateDirectory("Logs/Map01Operations/video");File.WriteAllText("Logs/Map01Operations/video/clarity-test-cases.txt","");
            SessionState.SetBool(Key,true);Register();runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{
                "ShadowVale.Map01.Tests.Map01OperationsTests.SupplyRequiresKeyPuzzleReportAndPhysicalHandoff",
                "ShadowVale.Map01.Tests.Map01OperationsTests.RadioFailureCallsOnlyOneSavedSquadAndSuccessDoesNotExtract",
                "ShadowVale.Map01.Tests.Map01OperationsTests.OfficerNeedsDistantBinocularsAndRestoresCombatBeforeDiscovery",
                "ShadowVale.Map01.Tests.Map01RevisionTests.EarlyOfficerDeathCannotSkipPointThree",
                "ShadowVale.Map01.Tests.Map01OperationsTests.AllOperationDialogueHasExactCaptionAndCorrectAudioChannel",
                "ShadowVale.Map01.Tests.Map01OperationsTests.CircuitRoutesToDummyLoadAndRejectsBrokenSource",
                "ShadowVale.Map01.Tests.Map01OperationsTests.CircuitMigratesOldBoardWithoutAcceptingItAsSolved",
                "ShadowVale.Map01.Tests.Map01OperationsTests.FailedLockCanRetryWithoutLosingCodeOrNotebook"}}));
        }
        public static void RecordInteractionReview()
        {
            SessionState.SetString(Key+".prefix","interaction-");Directory.CreateDirectory("Logs/Map01Operations/video");File.WriteAllText("Logs/Map01Operations/video/interaction-test-cases.txt","");
            SessionState.SetBool(Key,true);Register();runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{
                "ShadowVale.Map01.Tests.Map01InteractionReviewTests.WarmDialogueStartsWithoutBlockingAndKeepsAnklesPlanted",
                "ShadowVale.Map01.Tests.Map01OperationsTests.OfficerNeedsDistantBinocularsAndRestoresCombatBeforeDiscovery",
                "ShadowVale.Map01.Tests.Map01OperationsTests.SupplyRequiresKeyPuzzleReportAndPhysicalHandoff",
                "ShadowVale.Map01.Tests.RescueBushTests.BushSuccessAndEscapeKeepEnemyAliveUntilFinisherAndRestoreControls",
                "ShadowVale.Map01.Tests.RescueBushTests.IntroAndExecutionStayBoundAndPresentDeathBeforeFailure"}}));
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){Directory.CreateDirectory("Logs/Map01Operations/video");File.AppendAllText("Logs/Map01Operations/video/"+Prefix+"test-cases.txt",result.Test.FullName+" "+result.ResultState+"\n"+result.Message+"\n"+result.StackTrace+"\n");}
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory("Logs/Map01Operations/video");
                File.WriteAllText("Logs/Map01Operations/video/"+Prefix+"results.txt",result.ResultState+"\nPassed: "+result.PassCount+" Failed: "+result.FailCount+"\n"+result.Message);
                SessionState.EraseBool(Key);
                // Let Test Runner release its native capture/render resources before editor shutdown.
                double quitAt=EditorApplication.timeSinceStartup+2;int code=result.FailCount==0?0:2;
                EditorApplication.CallbackFunction quit=null;quit=()=>{if(EditorApplication.timeSinceStartup<quitAt)return;EditorApplication.update-=quit;EditorApplication.Exit(code);};EditorApplication.update+=quit;
            }
        }
    }
    public sealed class Map01OperationMovie:MonoBehaviour
    {
        MediaEncoder encoder;int frames;string path;
        public void Begin(string take){path="Logs/Map01Operations/video/"+take+".mp4";Directory.CreateDirectory(Path.GetDirectoryName(path));StartCoroutine(Capture());}
        IEnumerator Capture()
        {
            while(true){
                yield return new WaitForEndOfFrame();
                if(!Application.isPlaying)yield break;
                var frame=ScreenCapture.CaptureScreenshotAsTexture();
                if(frame==null)continue;
                try{
                    if(encoder==null)encoder=new MediaEncoder(path,new VideoTrackAttributes{frameRate=new MediaRational(15),width=(uint)frame.width,height=(uint)frame.height,includeAlpha=false});
                    encoder.AddFrame(frame);
                    if(frames%30==0)File.WriteAllBytes(Path.ChangeExtension(path,null)+"-"+frames.ToString("D4")+".png",frame.EncodeToPNG());
                    frames++;
                }finally{Object.Destroy(frame);}
            }
        }
        public void End(){StopAllCoroutines();encoder?.Dispose();encoder=null;Time.captureFramerate=0;if(path!=null)File.WriteAllText(path+".txt",frames+" frames at 15 fps; automated staged integration, no audio track.");}
        void OnDisable()=>End();
    }
}
