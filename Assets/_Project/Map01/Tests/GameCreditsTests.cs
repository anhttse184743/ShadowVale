using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
    public sealed class GameCreditsTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(120000)]public IEnumerator CreditsNameTheTeamScrollAndEscSkipsThem()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator InPlayMode()
        {
            var routing=RouteInputToGame();GameCredits.AllowInBatchMode=true;var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            try
            {
                Assert.IsFalse(GameCredits.Rolling);
                var credits=GameCredits.Roll(0,false);Assert.NotNull(credits);Assert.IsTrue(GameCredits.Rolling);
                Assert.AreSame(credits,GameCredits.Roll(),"A second call never starts a second outro.");
                var texts=credits.Texts.ToArray();
                StringAssert.Contains("SHADOWVALE",string.Join("|",texts));
                foreach(var member in new[]{"Trần Tuấn Anh","Nguyễn Đăng Khoa","Nguyễn Đình Tài","Nguyễn Công Thịnh"})Assert.Contains(member,texts,member+" is in the credits");
                System.IO.Directory.CreateDirectory("Logs/Credits");
                float until=Time.realtimeSinceStartup+6;while(Time.realtimeSinceStartup<until)yield return null;
                Assert.Greater(credits.Scroll,10,"It scrolls on its own after the fade.");Assert.IsFalse(credits.Finished);
                ScreenCapture.CaptureScreenshot("Logs/Credits/1-verse.png");for(int f=0;f<4;f++)yield return null;
                // Jump to the team and film it.
                typeof(GameCredits).GetField("scroll",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(credits,1500f*Screen.height/1080f);
                yield return null;yield return null;ScreenCapture.CaptureScreenshot("Logs/Credits/2-team.png");for(int f=0;f<4;f++)yield return null;
                typeof(GameCredits).GetField("scroll",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(credits,2350f*Screen.height/1080f);
                yield return null;yield return null;ScreenCapture.CaptureScreenshot("Logs/Credits/3-end.png");for(int f=0;f<4;f++)yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                until=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<until&&!credits.Finished)yield return null;
                Assert.IsTrue(credits.Finished,"Esc skips to the end.");
                Object.Destroy(credits.gameObject);yield return null;Assert.IsFalse(GameCredits.Rolling);
            }
            finally{GameCredits.AllowInBatchMode=false;InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
        }
    }
}
