using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    // Review capture only. Never included in a player build and never advances a retail save.
    public sealed class Map02FlowMovieTests:ForestSceneTestBase
    {
        const string Folder="Logs/Map02Chapter/FlowVideo";
        Map02Mission mission;Map02ChapterDirector chapter;Keyboard keyboard;Map02FlowMovie movie;
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        T Field<T>(string name)=>(T)typeof(Map02ChapterDirector).GetField(name,Private).GetValue(chapter);
        void Key(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
        [UnityTest,Timeout(1800000),Explicit("Manual video recording; excluded from routine regression runs.")]
        public IEnumerator RecordEveryTaskFromLandingToDawn()
        {
            Directory.CreateDirectory(Folder);
            SessionState.SetBool("Codex.Map02FlowMovie.Autostart",true);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);
            yield return new EnterPlayMode();
            var input=RouteInputToGame();keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                Time.captureFramerate=15;
                movie=Object.FindFirstObjectByType<Map02FlowMovie>();
                if(movie==null){movie=new GameObject("Map 2 review recorder").AddComponent<Map02FlowMovie>();movie.Begin(Folder+"/Map2_ToanBoFlow_Demo.mp4");movie.Section("00 · Cập bến", "BẢN TRÌNH DIỄN LOGIC · NPC/animation phụ đang chờ asset; chưa có thoại thu âm mới.");}
                float deadline=Time.realtimeSinceStartup+180;
                while(Time.realtimeSinceStartup<deadline)
                {mission=Object.FindFirstObjectByType<Map02Mission>();if(mission?.Chapter!=null&&mission.Ready)break;yield return null;}
                Assert.NotNull(mission?.Chapter,"Arrival must hand over to the chapter.");chapter=mission.Chapter;movie.mission=mission;
                Assert.AreEqual(0,chapter.State.completed.Count,"Record a fresh chapter, never an existing player's save.");
                var resume=SessionState.GetString("Codex.Map02FlowMovie.Resume","");
                if(!string.IsNullOrEmpty(resume))
                {
                    var snapshot=mission.Capture();snapshot.chapter=new Map02ChapterState();
                    foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==resume)break;Assert.IsTrue(snapshot.chapter.Complete(t.id));}
                    mission.Restore(snapshot);
                }
                foreach(var task in Map02ChapterDefinition.Load().tasks)
                {
                    if(chapter.State.Done(task.id))continue;
                    Assert.IsTrue(chapter.State.Available(task),task.id);
                    movie.Section(task.id+" · "+task.title,"ĐIỀU HƯỚNG TỰ ĐỘNG · Lược quãng đi lặp bằng chuyển cảnh; giao tranh mô phỏng.");
                    File.WriteAllText(Folder+"/progress.txt",task.id+" "+task.title);
                    if(chapter.CurrentScene!=null){yield return Dialogue();}
                    if(chapter.State.Done(task.id))continue;
                    if(task.kind=="binocular")
                    {
                        yield return Cut(chapter.Sites["bridge"].position);
                        var cam=Object.FindObjectsByType<ShadowVale.Gameplay.Player.ThirdPersonCamera>(FindObjectsSortMode.None).First(c=>c.GetComponent<Camera>().enabled);
                        var focus=chapter.Sites["scout"].position+Vector3.up;
                        cam.RestoreLook(Quaternion.LookRotation(focus-(mission.Player.position+Vector3.up*1.6f)));
                        cam.SetCinematicView(mission.Player.position+Vector3.up*1.6f,Quaternion.LookRotation(focus-(mission.Player.position+Vector3.up*1.6f)),50,1);
                        Key(UnityEngine.InputSystem.Key.F);yield return null;Key();
                        yield return WaitDone(task.id,12);cam.ClearCinematicView();continue;
                    }
                    yield return Cut(Mark(task.id).transform.position);
                    if(chapter.State.Done(task.id))continue;
                    if(task.kind=="reach"){yield return WaitDone(task.id,8);continue;}
                    // These happen to Nam: the tank shot, the last boat on reaching the bank, the sacrifice.
                    if(task.id=="N3_WOUND"||task.id=="N3_07"||task.id=="N3_09")
                    {
                        float start=Time.time+6;while(chapter.CurrentScene==null&&Time.time<start)yield return null;
                        Assert.NotNull(chapter.CurrentScene,task.id+" starts on its own");yield return Dialogue();yield return WaitDone(task.id,12);continue;
                    }
                    // A lane hold starts on entering the lane; there is nothing to press.
                    if(task.kind!="defend"){Assert.IsTrue(mission.Interact(Mark(task.id)),task.id+" interaction");yield return WaitGameSeconds(.7f);}
                    if(task.kind=="demolition"&&chapter.State.carry==task.id)
                    {yield return Cut(Mark(task.id).transform.position);Assert.IsTrue(mission.Interact(Mark(task.id)),"set the crate down at the bank");yield return WaitGameSeconds(.7f);}
                    if(task.kind=="carry")
                    {
                        for(int trip=0;trip<task.count;trip++)
                        {
                            if(trip>0){yield return Cut(Mark(task.id).transform.position);Assert.IsTrue(mission.Interact(Mark(task.id)));yield return WaitGameSeconds(.7f);}
                            yield return Cut(Mark(task.id).transform.position);Assert.IsTrue(mission.Interact(Mark(task.id)));yield return WaitGameSeconds(1.2f);
                        }
                    }
                    else if(task.kind=="escortHung"||task.kind=="escortScout"||task.kind=="patrolHung"||task.kind.StartsWith("rescue"))
                    {
                        yield return Dialogue();var who=Field<Map02ChapterActor>("escort");Assert.NotNull(who,task.id);
                        Vector3 destination=task.kind.StartsWith("rescue")?chapter.Sites["southStore"].position:chapter.Sites[task.site].position;
                        yield return Cut(destination);who.StopAt(destination+Vector3.right*1.5f);yield return WaitGameSeconds(1);
                        yield return Dialogue();
                    }
                    else if(task.kind=="families")
                    {
                        yield return Dialogue();
                        while(!chapter.State.Done(task.id)){yield return Cut(Mark(task.id).transform.position);Assert.IsTrue(mission.Interact(Mark(task.id)));yield return WaitGameSeconds(1.5f);}
                    }
                    else if(task.kind=="repair"||task.kind=="antenna"||task.kind=="clear"||task.kind=="demolition")
                    {
                        yield return Dialogue();
                        var limit=Time.time+100;Key();yield return null;
                        while(Field<bool>("doingWork")&&Time.time<limit)
                        {
                            if(task.kind=="repair")
                            {
                                float needle=Mathf.PingPong(Field<float>("workClock")*.65f,1);
                                if(needle>.44f&&needle<.55f){Key(UnityEngine.InputSystem.Key.Space);yield return null;Key();yield return WaitGameSeconds(.65f);}
                                else yield return null;
                            }
                            else if(task.kind=="antenna") {if(Field<float>("signal")<.71f)Key(UnityEngine.InputSystem.Key.D);else Key();yield return null;}
                            else {Key(UnityEngine.InputSystem.Key.Space);yield return null;}
                        }
                        Key();Assert.IsFalse(Field<bool>("doingWork"),task.id+" operation timeout");yield return Dialogue();
                    }
                    else if(task.kind=="defend"||task.kind=="coverFire")
                    {
                        movie.note="GIAO TRANH MÔ PHỎNG · Video kiểm tra thứ tự nhiệm vụ, không đo cân bằng chiến đấu.";
                        float limit=Time.time+(task.kind=="coverFire"?Map02ChapterDirector.CoverSlowest+20:150),fire=Time.time;
                        while(!chapter.State.Done(task.id)&&chapter.CurrentScene==null&&Time.time<limit)
                        {
                            if(Time.time>fire+4)
                            {
                                foreach(var enemy in chapter.Enemies.Where(e=>e!=null&&!e.Dead).ToArray())
                                {enemy.HitPoints.TakeDamage(200,enemy.transform.position,mission.Player.gameObject);yield return WaitGameSeconds(.65f);}
                                fire=Time.time;
                            }
                            yield return null;
                        }
                        yield return Dialogue();
                    }
                    else if(task.kind=="crawl") {Key(UnityEngine.InputSystem.Key.C);yield return WaitDoneOrScene(task.id,40);Key();yield return Dialogue();}
                    else yield return Dialogue();
                    yield return WaitDone(task.id,12);
                    movie.Snapshot(task.id);yield return WaitGameSeconds(.8f);
                }
                Assert.IsTrue(chapter.State.finished&&chapter.State.hungDead&&chapter.State.tankDestroyed);
                movie.Section("KẾT THÚC · Làng được giữ", "Đủ 43 bước. Model/animation phụ và lồng tiếng mới chưa hoàn thiện; đây không phải video nghiệm thu hình ảnh cuối.");
                yield return WaitGameSeconds(4);movie.End();File.WriteAllText(Folder+"/result.txt","PASS — 43 tasks, fresh state to dawn, real interaction entrypoints; shortened traversal, simulated combat; no new voice track.");
            }
            finally
            {SessionState.EraseBool("Codex.Map02FlowMovie.Autostart");Key();if(movie!=null)movie.End();if(keyboard!=null)InputSystem.RemoveDevice(keyboard);RestoreInputRouting(input);Time.captureFramerate=0;}
            yield return new ExitPlayMode();
        }
        IEnumerator Cut(Vector3 p)
        {
            movie.cut=true;yield return WaitGameSeconds(.25f);
            var floor=Physics.RaycastAll(p+Vector3.up*3,Vector3.down,8,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.5f&&h.collider.GetComponentInParent<ShadowVale.Gameplay.Combat.Health>()==null&&Mathf.Abs(h.point.y-p.y)<2.5f).OrderBy(h=>Mathf.Abs(h.point.y-p.y)).ToArray();
            if(floor.Length>0)p.y=floor[0].point.y;
            File.AppendAllText(Folder+"/positions.txt",sectionName()+" "+p+" floor="+(floor.Length>0?floor[0].collider.name:"NONE")+Environment.NewLine);
            var cc=mission.Player.GetComponent<CharacterController>();cc.enabled=false;mission.Player.position=p+Vector3.up*.2f;cc.enabled=true;
            mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().RestoreMotion(false);Physics.SyncTransforms();
            yield return WaitGameSeconds(.35f);movie.cut=false;yield return WaitGameSeconds(.8f);
        }
        string sectionName()=>movie.section;
        IEnumerator Dialogue()
        {
            float limit=Time.time+300;int last=-1;string lastTask="";float started=Time.time;
            while(chapter.CurrentScene!=null&&Time.time<limit)
            {
                var task=chapter.CurrentScene;int index=Field<int>("lineIndex");var entries=Field<Map02DialogueCatalog.Entry[]>("lines");
                if(task.id!=lastTask||index!=last){lastTask=task.id;last=index;started=Time.time;}
                float read=Mathf.Clamp(entries[index].text.Length/20f,1.8f,5f);
                if(Time.time-started>read)
                {
                    if(index==entries.Length-1)
                    {
                        if(task.id=="N3_09"&&Field<float>("sceneClock")<Map02ChapterDirector.FadeAt){yield return null;continue;} // Keep the guns and the counter-attack in the video.
                        movie.note="RÚT GỌN KHOẢNG LẶNG CUỐI CẢNH · Giữ nguyên kết quả như skip trong game.";
                        typeof(Map02ChapterDirector).GetMethod("EndScene",Private).Invoke(chapter,null);yield return null;last=-1;
                    }
                    else{Key(UnityEngine.InputSystem.Key.E);yield return null;Key();yield return null;}
                }
                else yield return null;
            }
            Assert.Less(Time.time,limit,"Dialogue timeout");
        }
        IEnumerator WaitDone(string id,float seconds){float until=Time.time+seconds;while(!chapter.State.Done(id)&&Time.time<until)yield return null;Assert.IsTrue(chapter.State.Done(id),id+" did not complete");}
        IEnumerator WaitDoneOrScene(string id,float seconds){float until=Time.time+seconds;while(!chapter.State.Done(id)&&chapter.CurrentScene==null&&Time.time<until)yield return null;Assert.Less(Time.time,until,id);}
    }
    [DefaultExecutionOrder(120)]
    public sealed class Map02FlowMovie:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartEarly()
        {
            if(!SessionState.GetBool("Codex.Map02FlowMovie.Autostart",false))return;
            SessionState.EraseBool("Codex.Map02FlowMovie.Autostart");Time.captureFramerate=15;
            string file="Logs/Map02Chapter/FlowVideo/"+(string.IsNullOrEmpty(SessionState.GetString("Codex.Map02FlowMovie.Resume",""))?"Map2_ToanBoFlow_Demo.mp4":"Map2_Ending.mp4");
            File.WriteAllText(Path.ChangeExtension(file,".chapters.txt"),"");
            var m=new GameObject("Map 2 review recorder").AddComponent<Map02FlowMovie>();m.Begin(file);
            m.Section("00 · Cập bến","BẢN TRÌNH DIỄN LOGIC · NPC/animation phụ đang chờ asset; chưa có thoại thu âm mới.");
        }
        MediaEncoder encoder;string path;int frames;public string section,note;public bool cut;public Map02Mission mission;
        public void Begin(string target){path=target;StartCoroutine(Capture());}
        public void Section(string title,string detail){section=title;note=detail;File.AppendAllText(Path.ChangeExtension(path,".chapters.txt"),TimeSpan.FromSeconds(frames/15d).ToString(@"hh\:mm\:ss")+"  "+title+Environment.NewLine);}
        void Update(){if(mission?.Player!=null)mission.Player.GetComponent<ShadowVale.Gameplay.Combat.Health>().CinematicInvulnerable=true;}
        void OnGUI()
        {
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1920f,Screen.height/1080f,1));GUI.depth=-1000;
            var style=new GUIStyle(GUI.skin.label){fontSize=20,wordWrap=true,normal={textColor=Color.white}};
            if(cut){GUI.DrawTexture(new Rect(0,0,1920,1080),Texture2D.blackTexture);GUI.Label(new Rect(460,485,1000,100),"LƯỢC ĐOẠN DI CHUYỂN — "+section,new GUIStyle(style){fontSize=30});}
            GUI.Box(new Rect(360,165,1510,88),GUIContent.none);GUI.Label(new Rect(377,171,1470,32),section,style);GUI.Label(new Rect(377,204,1470,48),note,new GUIStyle(style){fontSize=18});GUI.matrix=matrix;
        }
        IEnumerator Capture()
        {
            while(true){yield return new WaitForEndOfFrame();if(!Application.isPlaying)yield break;var image=ScreenCapture.CaptureScreenshotAsTexture();if(image==null)continue;
                try{encoder??=new MediaEncoder(path,new VideoTrackAttributes{frameRate=new MediaRational(15),width=(uint)image.width,height=(uint)image.height,includeAlpha=false});encoder.AddFrame(image);frames++;}finally{Destroy(image);}}
        }
        public void Snapshot(string id)=>ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(path),id+".png"));
        public void End(){StopAllCoroutines();encoder?.Dispose();encoder=null;if(path!=null)File.WriteAllText(path+".info.txt",frames+" frames / 15 fps. Full quest order demo; traversal cuts, simulated combat, shortened cinematic holds. No newly recorded voice audio.");}
        void OnDisable()=>End();
    }
}
