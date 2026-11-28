using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class ReviewDemoCaption:MonoBehaviour
    {
        public string text;
        void OnGUI(){if(string.IsNullOrEmpty(text))return;GUI.depth=-1100;GUI.color=new Color(0,0,0,.85f);GUI.DrawTexture(new Rect(20,Screen.height-112,Screen.width-40,44),Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(34,Screen.height-108,Screen.width-68,38),text,new GUIStyle(GUI.skin.label){fontSize=20,wordWrap=true,normal={textColor=new Color(1,.88f,.55f)}});}
    }
    public sealed class FenceAndBinocularDemoTests:ForestSceneTestBase
    {
        DockMovieCapture capture;ReviewDemoCaption caption;Keyboard keyboard;
        IEnumerator Film(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        IEnumerator Tap(Key key){Keys(key);yield return Film(.10f);Keys();yield return Film(.20f);}
        void Begin(string folder){keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();Time.captureFramerate=24;Time.timeScale=1;capture=new GameObject("Review fix recording").AddComponent<DockMovieCapture>();capture.Folder=folder;caption=capture.gameObject.AddComponent<ReviewDemoCaption>();capture.Begin();}
        void End(){capture?.End();if(capture!=null)Object.Destroy(capture.gameObject);if(keyboard!=null){Keys();InputSystem.RemoveDevice(keyboard);}Time.captureFramerate=0;Time.timeScale=1;}
        void Shot(ThirdPersonCamera rig,Vector3 eye,Vector3 focus,float fov=55)=>rig.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),fov,1);
        [UnityTest,Timeout(600000),Explicit("Record the corrected fence layout and real movement through a mission route.")]
        public IEnumerator RecordFenceLayoutAndMissionWalk()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();yield return FenceMovie();yield return new ExitPlayMode();
        }
        IEnumerator FenceMovie()
        {
            Map02Mission m=null;float until=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();Object.FindFirstObjectByType<Map02Arrival>()?.Skip();if(m?.Ready==true&&m.Chapter!=null)break;yield return null;}Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();
            var save=m.Capture();save.chapter=new Map02ChapterState();save.chapter.dayOne.arrivalSeen=true;m.Restore(save);c.Cancel();
            var rig=m.GameCamera.GetComponent<ThirdPersonCamera>();Assert.NotNull(rig);var routing=RouteInputToGame();
            try{
                Begin("Logs/FenceDemo_20261128");caption.text="MAP 2 · Hàng rào theo mép làng; chừa nhà, sân và đường nhiệm vụ";
                for(float t=0;t<7;t+=Time.deltaTime){var eye=Vector3.Lerp(new Vector3(-101,48,-23),new Vector3(-100,38,19),t/7);Shot(rig,eye,new Vector3(-74,1,5),62);yield return null;}
                var gate=Object.FindObjectsByType<Map02NightWatchVisual>(FindObjectsSortMode.None).Where(v=>v.gate).OrderBy(v=>Vector3.Distance(v.transform.position,new Vector3(-55.8f,1,9.4f))).First();
                caption.text="Cổng nằm ở lối đi · Không đặt rào xuyên nhà hoặc chắn đường";Shot(rig,gate.transform.position+gate.transform.rotation*new Vector3(3,3,-7),gate.transform.position+Vector3.up*.9f);yield return Film(5);
                var start=c.Sites["command"].position;var goal=c.Sites["dayOneFamily"].position;var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=start;cc.enabled=true;yield return Film(.2f);
                var path=new NavMeshPath();Assert.True(NavMesh.CalculatePath(m.Player.position,goal,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
                caption.text="Di chuyển bằng WASD: chốt đầu làng → nhà chị Lành · Tuyến nhiệm vụ thông thoáng";
                int corner=1;float deadline=Time.time+80;
                while(Vector3.ProjectOnPlane(m.Player.position-goal,Vector3.up).magnitude>1.1f){
                    Assert.Less(Time.time,deadline,"Recorded mission walk must reach the family house");
                    while(corner<path.corners.Length-1&&Vector3.ProjectOnPlane(path.corners[corner]-m.Player.position,Vector3.up).magnitude<.75f)corner++;
                    var delta=Vector3.ProjectOnPlane(path.corners[Mathf.Min(corner,path.corners.Length-1)]-m.Player.position,Vector3.up).normalized;
                    Shot(rig,m.Player.position-delta*6.5f+Vector3.up*3.6f,m.Player.position+delta*2+Vector3.up*1.0f,60);
                    var forward=Vector3.ProjectOnPlane(m.GameCamera.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);var keys=new List<Key>();
                    float f=Vector3.Dot(delta,forward),r=Vector3.Dot(delta,right);if(f>.3f)keys.Add(Key.W);if(f<-.3f)keys.Add(Key.S);if(r>.3f)keys.Add(Key.D);if(r<-.3f)keys.Add(Key.A);Keys(keys.ToArray());yield return null;
                }
                Keys();caption.text="Đã tới nhà chị Lành · Không bị hàng rào cản trên tuyến đi";yield return Film(3);
                capture.End();
            }finally{End();rig.ClearCinematicView();c.Cancel();RestoreInputRouting(routing);}
        }
        [UnityTest,Timeout(300000),Explicit("Record an overview and close shot of the actual perimeter fence.")]
        public IEnumerator RecordFenceBoundaryOverview()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();yield return BoundaryMovie();yield return new ExitPlayMode();
        }
        IEnumerator BoundaryMovie()
        {
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();Object.FindFirstObjectByType<Map02Arrival>()?.Skip();if(m?.Ready==true&&m.Chapter!=null)break;yield return null;}Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();
            var save=m.Capture();save.chapter=new Map02ChapterState();save.chapter.dayOne.arrivalSeen=true;m.Restore(save);c.Cancel();var rig=m.GameCamera.GetComponent<ThirdPersonCamera>();
            try{
                Shot(rig,new Vector3(-77,92,-18),new Vector3(-77,1,-1),65);yield return Film(.3f);Begin("Logs/FenceBoundaryDemo_20261128");
                caption.text="MAP 2 · Tổng quan: rào ở mép làng; đường và khu nhà được chừa trống";
                for(float t=0;t<5;t+=Time.deltaTime){Shot(rig,Vector3.Lerp(new Vector3(-77,92,-18),new Vector3(-82,88,-12),t/5),new Vector3(-77,1,-1),65);yield return null;}
                caption.text="Cận cảnh rào ngoài khu nhà · Không kéo rào cắt qua đường nhiệm vụ";
                for(float t=0;t<4;t+=Time.deltaTime){var eye=Vector3.Lerp(new Vector3(-111,3,-8),new Vector3(-111,3,3),t/4);Shot(rig,eye,new Vector3(-120,1,5+t*2),57);yield return null;}
                capture.End();
            }finally{End();rig.ClearCinematicView();c.Cancel();}
        }
        [UnityTest,Timeout(600000),Explicit("Record actual binocular input and completion of required observation.")]
        public IEnumerator RecordBinocularReminderAndRecovery()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();yield return BinocularMovie();yield return new ExitPlayMode();
        }
        IEnumerator BinocularMovie()
        {
            Map01Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map01Mission>();if(m?.IsInitialized==true)break;yield return null;}Assert.NotNull(m);foreach(var enemy in m.Enemies)enemy.enabled=false;
            var story=m.GetComponent<Map01Story>();var quest=m.GetComponent<Map01Quest>();quest.RestoreStoryStage(Map01Quest.BossStage,new Map01Story.Snapshot{rescueAccepted=true,supplies=true,suppliesDelivered=true,supplyReported=true,radioDisabled=true,powerDisabled=true});
            var target=story.Layout.commanderPost.position;Vector3 at=default;bool found=false;
            // Use a walkable point beside the camp, at the terrain's real height.
            for(int i=0;i<16&&!found;i++){
                var p=target+Quaternion.Euler(0,i*22.5f,0)*Vector3.back*32;
                if(NavMesh.SamplePosition(p,out var nav,80,NavMesh.AllAreas)&&Vector3.ProjectOnPlane(nav.position-target,Vector3.up).magnitude>=20&&Vector3.ProjectOnPlane(nav.position-target,Vector3.up).magnitude<=65){at=nav.position;found=true;}
            }
            Assert.True(found);PlaceNam(m,at+Vector3.up*.1f);var rig=m.gameCamera.GetComponent<ThirdPersonCamera>();Assert.NotNull(rig);var routing=RouteInputToGame();
            try{
                Shot(rig,m.player.position+Vector3.up*6, target+Vector3.up*1.5f,60);yield return Film(.5f);
                Begin("Logs/BinocularDemo_20261128");caption.text="MAP 1 · Điểm 3: thông báo dùng [F] hiện trước khi đưa ống nhòm lên";Assert.NotNull(story.RequiredObservationReminder);yield return Film(6);
                rig.ClearCinematicView();rig.RestoreLook(Quaternion.LookRotation(target+Vector3.up*1.5f-(m.player.position+Vector3.up*1.6f)));caption.enabled=false;caption.text="GIỮ F · Nhìn về doanh trại, đứng yên để xác nhận quan sát";capture.input="GIỮ PHÍM F";Keys(Key.F);
                float deadline=Time.time+20;while(!m.GetComponent<Map01StoryDialogue>().IsPlaying){Assert.Less(Time.time,deadline,"Actual F input must start the observation report");yield return null;}
                Keys();capture.input="";caption.enabled=false;
                deadline=Time.time+40;while(m.GetComponent<Map01StoryDialogue>().IsPlaying){Assert.Less(Time.time,deadline);yield return Film(1.5f);yield return Tap(Key.E);}
                Assert.True(story.Progress.commanderReported);Assert.IsNull(story.RequiredObservationReminder);caption.enabled=true;caption.text="Đã hoàn tất bước quan sát · Thông báo ống nhòm tự ẩn";yield return Film(4);capture.End();
            }finally{End();rig.ClearCinematicView();RestoreInputRouting(routing);}
        }
    }
}
