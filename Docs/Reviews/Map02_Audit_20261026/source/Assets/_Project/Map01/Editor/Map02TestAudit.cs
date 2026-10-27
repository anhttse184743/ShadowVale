using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    // The CLI's incremental result collector loses earlier leaves over Play Mode reloads.
    // Save the authoritative root tree so a regression failure is never hidden by its summary.
    [InitializeOnLoad] public static class Map02TestAudit
    {
        static readonly TestRunnerApi api;
        static Map02TestAudit(){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.hideFlags=HideFlags.HideAndDontSave;api.RegisterCallbacks(new Writer());}
        sealed class Writer:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                var leaves=Leaves(result).ToArray();if(!leaves.Any(r=>r.Test.FullName.Contains("Map02")))return;
                Directory.CreateDirectory("Logs/Map02Chapter");TestRunnerApi.SaveResultToFile(result,"Logs/Map02Chapter/latest-full-results.xml");
                File.WriteAllLines("Logs/Map02Chapter/latest-failures.txt",leaves.Where(r=>r.ResultState.StartsWith("Failed")).Select(r=>r.Test.FullName+"\n"+r.Message+"\n"+r.StackTrace));
            }
            static IEnumerable<ITestResultAdaptor> Leaves(ITestResultAdaptor root)
            {if(root.Test.IsSuite){foreach(var child in root.Children)foreach(var leaf in Leaves(child))yield return leaf;}else yield return root;}
        }
    }
}
