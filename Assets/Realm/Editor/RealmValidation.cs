using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TheRealm.Editor
{
    public static class RealmValidation
    {
        static TestRunnerApi api;
        [MenuItem("The Realm/Run Rules and Persistence Tests")]
        public static void RunTests()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode before running EditMode tests.");
            Directory.CreateDirectory("Logs/Realm");
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,assemblyNames=new[]{"Realm.Tests"}}));
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,"Logs/Realm/editmode-results.xml");
                File.WriteAllText("Logs/Realm/editmode-summary.txt","Passed="+result.PassCount+" Failed="+result.FailCount+" Skipped="+result.SkipCount);
                Debug.Log("[Realm Tests] Passed="+result.PassCount+" Failed="+result.FailCount);
                Object.DestroyImmediate(api);
            }
        }
    }
}
