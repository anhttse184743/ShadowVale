using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using ShadowVale.Map01;
namespace ShadowVale.EnvironmentReview
{
    public static partial class Map01MergeIntegration
    {
        const string TestKey="ShadowVale.MergeDev.Tests";
        static TestRunnerApi testApi;
        [InitializeOnLoadMethod]static void ResumeTests(){if(SessionState.GetBool(TestKey,false))EditorApplication.delayCall+=RegisterTests;else if(!string.IsNullOrEmpty(SessionState.GetString(ForestSaveSlots.EditorTestRootKey,"")))EditorApplication.delayCall+=ReleaseTestRoot;}
        static void ReleaseTestRoot(){if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=ReleaseTestRoot;return;}SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);}
        static void RegisterTests(){if(testApi!=null)return;testApi=ScriptableObject.CreateInstance<TestRunnerApi>();testApi.RegisterCallbacks(new MergeResults());}
        static void RunTests(string selection)
        {
            RegisterTests();SessionState.SetBool(TestKey,true);SessionState.SetString(TestKey+".name",selection);
            var saveRoot=Path.GetFullPath(Work+"/IsolatedSaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(saveRoot);SessionState.SetString(ForestSaveSlots.EditorTestRootKey,saveRoot);
            var names=File.ReadAllLines(Work+"/tests_"+selection+".txt");File.WriteAllText(Work+"/test_status.txt","RUNNING "+selection);
            testApi.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=names}));
        }
        sealed class MergeResults:ICallbacks
        {
            public void RunStarted(ITestAdaptor t){}
            public void TestStarted(ITestAdaptor t){File.WriteAllText(Work+"/test_progress.txt",t.FullName);}
            public void TestFinished(ITestResultAdaptor r){if(!SessionState.GetBool(TestKey,false))return;File.AppendAllText(Work+"/test_cases_"+SessionState.GetString(TestKey+".name","")+".txt",r.Test.FullName+" "+r.ResultState+"\n"+r.Message+"\n"+r.StackTrace+"\n");}
            public void RunFinished(ITestResultAdaptor r)
            {if(!SessionState.GetBool(TestKey,false))return;var name=SessionState.GetString(TestKey+".name","");TestRunnerApi.SaveResultToFile(r,Path.GetFullPath(Work+"/tests_"+name+".xml"));File.WriteAllText(Work+"/test_status.txt",name+" "+r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount+" skipped="+r.SkipCount);SessionState.EraseBool(TestKey);EditorApplication.delayCall+=ReleaseTestRoot;}
        }
    }
}
