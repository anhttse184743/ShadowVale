using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DayOneUiTutorialTests : ForestSceneTestBase
    {
        const string Evidence = "Logs/DayOneUiTutorial20261121";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        Map02ChapterDirector chapter;
        Keyboard keyboard;
        T Field<T>(string name) => (T)typeof(Map02ChapterDirector).GetField(name, Private).GetValue(chapter);
                object Call(string name, params object[] args)
        {
            var result=typeof(Map02ChapterDirector).GetMethod(name,Private).Invoke(chapter,args);
            if(name=="OpenDay")Assert.True(Field<bool>("dayHelpVisible"),"Tutorial must open synchronously, before the first gameplay frame");
            return result;
        }
        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }
        static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(Evidence);
            ScreenCapture.CaptureScreenshot(Evidence + "/" + name + ".png");
            yield return null;
            yield return null;
        }
        IEnumerator Click(float x, float y)
        {
            var queue = typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var position = new Vector2(x * Screen.width / 1600f, y * Screen.height / 900f);
            queue.Invoke(null, new object[] { new Event { type = EventType.MouseDown, mousePosition = position, button = 0 } });
            yield return null;
            queue.Invoke(null, new object[] { new Event { type = EventType.MouseUp, mousePosition = position, button = 0 } });
            yield return null;
            yield return null;
        }
        IEnumerator ReadTutorial(string name)
        {
            yield return WaitGameSeconds(.2f);
            Assert.True(Field<bool>("dayHelpVisible"), name + " must show its first-use instructions");
            Assert.True(Cursor.visible);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            yield return Capture(name + "-tutorial-1");
            yield return Tap(Key.Space);
            Assert.True(Field<bool>("dayHelpVisible"));
            yield return Click(1175, 729);
            Assert.AreEqual(1, Field<int>("dayHelpPage"));
            yield return Capture(name + "-tutorial-2");
            yield return Tap(Key.Enter);
            Assert.AreEqual(2, Field<int>("dayHelpPage"));
            yield return Tap(Key.Enter);
            Assert.False(Field<bool>("dayHelpVisible"));
            yield return WaitGameSeconds(.15f);
        }
        static bool Solve(Map02DayOneState state, int index = 0)
        {
            if (index == 3) return state.Packed;
            for (int rotation = 0; rotation < 2; rotation++)
            for (int x = 2; x < 6; x++)
            for (int y = 0; y < 4; y++)
            {
                if (!state.PlaceCrate(index, x, y, rotation == 1, out _)) continue;
                if (Solve(state, index + 1)) return true;
                state.crates[index] = new Map02DayOneState.Placement();
            }
            return false;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HelpBlocksActionsAndMouseKeyboardCanFinishDayAndNightGames()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            SetPreviewResolution(1280, 720);
            yield return new EnterPlayMode();
            Map02Mission mission = null;
            float deadline = Time.realtimeSinceStartup + 90;
            while (Time.realtimeSinceStartup < deadline)
            {
                mission = Object.FindFirstObjectByType<Map02Mission>();
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                if (mission?.Ready == true && mission.Chapter != null) break;
                yield return null;
            }
            Assert.NotNull(mission?.Chapter);
            chapter = mission.Chapter;
            var snapshot = mission.Capture();
            snapshot.chapter = new Map02ChapterState();
            foreach (var task in Map02ChapterDefinition.Load().tasks)
            {
                if (task.id == "D1_04") break;
                snapshot.chapter.Complete(task.id);
            }
            mission.Restore(snapshot);
            var routing = RouteInputToGame();
            keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.NotNull(Resources.Load<Texture2D>("Map02DayOne/UI/DayOneAtlas"));
                var activity = typeof(Map02ChapterDirector).GetNestedType("DayActivity", BindingFlags.NonPublic);
                Call("OpenDay", Enum.Parse(activity, "Packing"), Map02ChapterDefinition.Load().Find("D1_04"));
                yield return ReadTutorial("packing");
                Assert.False(chapter.State.Done("D1_04"));
                Assert.True(chapter.State.dayOne.crates.All(c => c.x < 0));
                yield return Capture("packing-board");
                yield return Tap(Key.H);
                yield return null;
                Assert.True(Field<bool>("dayHelpVisible"));
                yield return Tap(Key.Escape);
                Assert.True(chapter.DayActivityActive, "Escape on help returns to the puzzle");
                var plan = new Map02DayOneState();
                plan.Initialize(chapter.State.dayOne.seed);
                Assert.True(Solve(plan));
                Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                for (int i = 0; i < 3; i++)
                {
                    if (i == 0) yield return Click(1150, 294);
                    else yield return Tap(digits[i]);
                    if (plan.crates[i].rotated) yield return Tap(Key.R);
                    if (i == 0)
                    {
                        var size = Map02DayOneState.Size(i, plan.crates[i].rotated);
                        yield return Click(160 + plan.crates[i].x * 105 + 10,
                            262 + (4 - plan.crates[i].y - size.y) * 92 + 10);
                        Assert.AreEqual(plan.crates[i].x, chapter.State.dayOne.crates[i].x);
                        continue;
                    }
                    var cursor = Field<Vector2Int>("packingCursor");
                    while (cursor.x != plan.crates[i].x)
                    {
                        yield return Tap(cursor.x < plan.crates[i].x ? Key.RightArrow : Key.LeftArrow);
                        cursor = Field<Vector2Int>("packingCursor");
                    }
                    while (cursor.y != plan.crates[i].y)
                    {
                        yield return Tap(cursor.y < plan.crates[i].y ? Key.UpArrow : Key.DownArrow);
                        cursor = Field<Vector2Int>("packingCursor");
                    }
                    yield return Tap(Key.E);
                }
                yield return Capture("packing-complete");
                Assert.True(chapter.State.dayOne.Packed);
                yield return Click(1160, 690);
                Assert.True(chapter.State.Done("D1_04"));
                Call("OpenDay", Enum.Parse(activity, "Dock"), Map02ChapterDefinition.Load().Find("D1_05"));
                yield return ReadTutorial("hammer");
                Assert.AreEqual(0, chapter.State.dayOne.stakeHits, "Tutorial Space and Enter cannot hit a stake");
                yield return Capture("hammer-play");
                deadline = Time.realtimeSinceStartup + 70;
                while (chapter.State.dayOne.stakeHits < 6)
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, "Hammer must remain playable after tutorial");
                    float needle = Mathf.PingPong(Field<float>("dayClock") / Map02DayOneState.SweepSeconds(chapter.State.dayOne.stakeHits / 3), 1);
                    if (needle > .47f && needle < .53f) yield return Tap(Key.Space);
                    else yield return null;
                }
                yield return WaitGameSeconds(1);
                yield return ReadTutorial("boards");
                Assert.AreEqual(0, chapter.State.dayOne.BoardCount, "Tutorial Enter cannot place boards");
                yield return Capture("boards-play");
                for (int i = 0; i < 3; i++)
                {
                    int card = Array.IndexOf(chapter.State.dayOne.boardOrder, i);
                    yield return Click(290, 290 + card * 90);
                    Assert.AreEqual(i, Field<int>("selectedBoard"));
                    while (Field<float>("dockOffset") > .02f)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                        yield return null;
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    yield return Capture("boards-selected-" + i);
                    yield return Click(1340, 627);
                    Assert.AreEqual(i + 1, chapter.State.dayOne.BoardCount);
                }
                yield return Capture("boards-complete");
                yield return Click(1340, 627);
                Assert.False(chapter.DayActivityActive);
                Assert.NotNull(chapter.CurrentScene, "The physical dock test scene must start");
                Call("EndScene");
                Assert.True(chapter.State.Done("D1_05"));
                snapshot = mission.Capture();
                snapshot.chapter = new Map02ChapterState();
                foreach (var task in Map02ChapterDefinition.Load().tasks)
                {
                    if (task.id == "N1_02") break;
                    snapshot.chapter.Complete(task.id);
                }
                mission.Restore(snapshot);
                var nightTask = Map02ChapterDefinition.Load().Find("N1_02");
                Call("StartPost", nightTask);
                var post = chapter.State.dayOne.posts[0];
                post.lampAngle = 0;
                post.gateClosed = post.latchSeated = post.bundleStowed = false;
                Assert.NotNull(Resources.Load<Texture2D>("Map02DayOne/UI/NightOneAtlas"));
                Call("OpenDay", Enum.Parse(activity, "Lamp"), nightTask);
                yield return ReadTutorial("lamp");
                Assert.AreEqual(0, post.lampAngle, "Help input cannot rotate the lamp");
                yield return Capture("lamp-before");
                for (int turn = 0; turn < 12; turn++) yield return Click(1350, 445);
                Assert.True(post.LampSafe);
                yield return Capture("lamp-ready");
                yield return Click(1220, 641);
                Assert.False(chapter.DayActivityActive);
                Call("OpenDay", Enum.Parse(activity, "Gate"), nightTask);
                yield return ReadTutorial("gate");
                Assert.False(post.gateClosed || post.latchSeated, "Help cannot fix the gate");
                yield return Capture("gate-before");
                yield return Click(1220, 516);
                Assert.False(post.latchSeated, "The latch button must wait for the gate to close");
                yield return Click(1220, 445);
                yield return Click(1220, 516);
                Assert.True(post.gateClosed && post.latchSeated);
                yield return Capture("gate-ready");
                yield return Click(1220, 641);
                Assert.False(chapter.DayActivityActive);
                Call("OpenDay", Enum.Parse(activity, "Path"), nightTask);
                yield return ReadTutorial("path");
                Assert.False(post.bundleStowed, "Help cannot move the firewood");
                yield return Capture("path-before");
                yield return Click(1220, 445);
                Assert.False(post.bundleStowed, "Leaving wood by the door is unsafe");
                yield return Click(1220, 516);
                Assert.True(post.bundleStowed);
                yield return Capture("path-ready");
                yield return Click(1220, 641);
                Assert.False(chapter.DayActivityActive);
                Assert.True(post.Safe);
                Assert.False(chapter.State.Done("N1_02"), "Repairing props must still require reporting to the guard");
                WriteReviewFile(Evidence + "/result.txt", "PASS at 1280x720: all six tutorials, mouse Next and keyboard Enter, H reopening, Esc returning to play, no gameplay actions behind help; 3 crates packed; 6 stake hits; 3 boards; dock completion; lamp rotated inward; gate closed before latching; wood stowed off path; confirmation returns controls; report to guard remains required.");
            }
            finally
            {
                chapter.Cancel();
                InputSystem.RemoveDevice(keyboard);
                RestoreInputRouting(routing);
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(240000)]
        public IEnumerator HammerHandsStayOnHandleAndCycleReturnsToReadyPose()
        {
            Directory.CreateDirectory(Evidence);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            SetPreviewResolution(1280,720);
            yield return new EnterPlayMode();
            Map02Mission mission=null;float deadline=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<deadline){
                mission=Object.FindFirstObjectByType<Map02Mission>();
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                if(mission?.Ready==true&&mission.Chapter!=null)break;
                yield return null;
            }
            Assert.NotNull(mission?.Chapter);chapter=mission.Chapter;
            var snapshot=mission.Capture();snapshot.chapter=new Map02ChapterState();
            foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id=="D1_05")break;snapshot.chapter.Complete(task.id);}
            snapshot.chapter.dayOne.arrivalSeen=true;mission.Restore(snapshot);
            var activity=typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic);
            var bay=Field<System.Collections.Generic.Dictionary<string,Map02ChapterActor>>("roles")["Chú Bảy"];var bayOrigin=bay.transform.position;
            try{
                Call("OpenDay",Enum.Parse(activity,"Dock"),Map02ChapterDefinition.Load().Find("D1_05"));
                float clock=Field<float>("strikeClock");
                yield return WaitGameSeconds(.25f);
                Assert.AreEqual(clock,Field<float>("strikeClock"),"Tutorial freezes the hammer cycle");
                Call("FinishDayHelp");
                yield return WaitGameSeconds(.2f);
                chapter.enabled=false;
                Call("StrikeStake",true);
                var body=Field<Animator>("dayNamAnimator");
                var mallet=Field<Transform>("twoHandMallet");
                var readyLeft=Vector3.zero;var readyRight=Vector3.zero;
                var report=new System.Text.StringBuilder();float maxError=0;
                foreach(int completedHits in new[]{0,2,5}){
                chapter.State.dayOne.stakeHits=completedHits;Call("SyncDayProps");
                Call("ResetStakeStrike");Call("SampleStake",0f);Call("StrikeStake",true);
                foreach(float t in new[]{0f,.04f,.08f,.12f,.2f,.4f,.65f,.8f}){
                    typeof(Map02ChapterDirector).GetField("strikeClock",Private).SetValue(chapter,t);
                    Call("SampleStake",0f);
                    var activeStake=(Transform)typeof(Map02ChapterDirector).GetProperty("Stake",Private).GetValue(chapter);
                    Assert.Greater(Vector3.ProjectOnPlane(bay.Animator.GetBoneTransform(HumanBodyBones.Head).position-activeStake.position,Vector3.up).magnitude,1.6f,"The supervisor must stay clear of both the post and the full hammer swing");
                    foreach(bool left in new[]{true,false}){
                        var hand=body.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                        var middle=(Transform)typeof(Map02ChapterDirector).GetMethod("SceneMiddle",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{body,left});
                        var palm=hand.position+(middle.position-hand.position).normalized*.05f;
                        var grip=mallet.position+mallet.up*(left?.19f:.04f);
                        float error=Vector3.Distance(palm,grip);
                        var upper=body.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                        var lower=body.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                        report.AppendLine($"hits={completedHits} t={t:F2} left={left} palmError={error:F4} upper={upper.position} grip={grip} reach={Vector3.Distance(upper.position,grip):F3} length={Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position):F3}");
                        maxError=Mathf.Max(error,maxError);File.WriteAllText(Evidence+"/hammer-contact.txt",report.ToString());

                    }
                    if(t==0){readyLeft=body.GetBoneTransform(HumanBodyBones.LeftHand).position;readyRight=body.GetBoneTransform(HumanBodyBones.RightHand).position;}
                    if(t==.12f){
                        var stake=(Transform)typeof(Map02ChapterDirector).GetProperty("Stake",Private).GetValue(chapter);
                        Assert.Less(Vector3.Distance(mallet.position+mallet.up*.6f,stake.position+Vector3.up*1.14f),.005f,"Hammer head meets the post at contact");
                    }
                    if(t==.8f){
                        Assert.Less(Vector3.Distance(readyLeft,body.GetBoneTransform(HumanBodyBones.LeftHand).position),.005f);
                        Assert.Less(Vector3.Distance(readyRight,body.GetBoneTransform(HumanBodyBones.RightHand).position),.005f);
                    }
                    yield return Capture("hammer-"+completedHits+"-pose-"+Mathf.RoundToInt(t*100));
                }
                File.WriteAllText(Evidence+"/hammer-contact.txt",report.ToString());
                }
                Assert.Less(maxError,.035f,"Both palms must stay on the handle for both posts, including sunk posts: "+report);
                chapter.State.dayOne.stakeHits=6;Call("SyncDayProps");Call("ReleaseStake");Call("TickDockObserver");
                for(int frame=0;frame<30;frame++){Call("TickWorkView");yield return null;}
                yield return Capture("dock-boards-clear-workspace");
                Assert.Greater(Vector3.ProjectOnPlane(bay.transform.position-chapter.Sites["dock"].position,Vector3.up).magnitude,2,"Keep the observer outside the board workspace too");
                Call("CloseDay",false);
                Assert.Less(Vector3.Distance(bayOrigin,bay.transform.position),.01f,"Closing the work restores the quest NPC");
                Call("OpenDay",Enum.Parse(activity,"Dock"),Map02ChapterDefinition.Load().Find("D1_05"));
                Assert.True(Field<bool>("dayHelpVisible"),"Re-entering unfinished work shows instructions before gameplay");
            }finally{chapter.enabled=true;chapter.Cancel();}
            yield return new ExitPlayMode();
        }
    }
}
