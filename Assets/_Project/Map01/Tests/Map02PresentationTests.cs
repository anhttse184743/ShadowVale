using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02PresentationTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(300000)]public IEnumerator LiveWalkingMealApproachAndDockControls()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return AuditLive();yield return new ExitPlayMode();
        }
        static IEnumerator AuditLive()
        {
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();var route=RouteInputToGame();const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            object Call(string n,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(n,flags).Invoke(c,args);
            object Get(string n)=>typeof(Map02ChapterDirector).GetField(n,flags).GetValue(c);
            void Set(string n,object v)=>typeof(Map02ChapterDirector).GetField(n,flags).SetValue(c,v);
            void Load(string id){var snap=m.Capture();var state=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id==id)break;state.Complete(task.id);}state.dayOne.arrivalSeen=true;snap.chapter=state;m.Restore(snap);}
            void Place(Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
            var kb=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            void Keys(params Key[] keys){InputSystem.QueueStateEvent(kb,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));InputSystem.Update();}
            Directory.CreateDirectory("Logs/Map02Presentation_20261022");
            void RenderCargo(){var cam=new GameObject("Cargo visual audit").AddComponent<Camera>();var src=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null);cam.CopyFrom(src);cam.enabled=false;var focus=m.Player.position+Vector3.up;var offset=m.Player.right*3+m.Player.forward*3+Vector3.up*.6f;cam.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));cam.fieldOfView=48;var target=new RenderTexture(1280,720,24);cam.targetTexture=target;cam.Render();var old=RenderTexture.active;RenderTexture.active=target;var img=new Texture2D(1280,720,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,720),0,0);img.Apply();File.WriteAllBytes("Logs/Map02Presentation_20261022/cargo-front.png",img.EncodeToPNG());RenderTexture.active=old;cam.targetTexture=null;Object.Destroy(cam.gameObject);Object.Destroy(img);Object.Destroy(target);}
            try {
                Load("D1_02");Place(c.Sites["dayOneFamily"].position+Vector3.back*8);var actor=c.Actor("Hùng");actor.StopAt(m.Player.position+Vector3.back*2);actor.Follow(m.Player,1.25f,false);Set("escort",actor);Set("workTask",Map02ChapterDefinition.Load().Find("D1_02"));
                yield return WaitGameSeconds(.2f);var anim=m.Player.GetComponentInChildren<Animator>();Keys(Key.W);yield return WaitGameSeconds(.8f);float first=anim.GetCurrentAnimatorStateInfo(0).normalizedTime;yield return WaitGameSeconds(.8f);
                Assert.NotNull(anim.runtimeAnimatorController,"Escorting must retain ordinary gameplay locomotion.");Assert.IsNull(Get("dayNamPose"));Assert.Greater(anim.GetCurrentAnimatorStateInfo(0).normalizedTime,first+.05f,"Walking must advance without repeated CrossFade resets.");Keys();c.Cancel();
                Load("D1_02");Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_02"),0,0);Set("sceneClock",5f);Call("LateUpdate");
                var hung=c.Actor("Hùng");var lanh=c.Actor("Chị Lành");var side=lanh.transform.position-hung.transform.position;Assert.Greater(Vector3.Dot(side,hung.transform.right),.6f,"Lành must work beside Hùng, not stand directly in front.");var chair=(Transform)Get("sceneChair");var seat=chair.position;Call("EndScene");Assert.Greater(Vector3.Distance(hung.transform.position,seat),.7f,"Hùng must clear the chair before resuming standing locomotion.");
                var commander=c.Actor("Anh Thành").visual.GetComponentInChildren<Animator>();Assert.AreEqual(commander.GetBoneTransform(HumanBodyBones.Chest),commander.GetComponent<Map01Rifle>().weapon.transform.parent);
                Load("D1_03");c.State.carry="D1_04";Place(c.Sites["dayOneFamily"].position+Vector3.back*4);Keys(Key.W);yield return WaitGameSeconds(.8f);Assert.NotNull(Get("cargoPose"));Assert.IsNull(Get("dayNamPose"));Assert.True(c.WorkRifleSlung);
                var crates=(System.Collections.Generic.List<Transform>)Get("physicalCrates");Assert.True(crates[0].gameObject.activeInHierarchy);Assert.Less(Vector3.Distance(crates[0].position,m.Player.position),1.8f,"Carried crate must remain attached after mission Update/LateUpdate.");
                var camera=Object.FindObjectsByType<ShadowVale.Gameplay.Player.ThirdPersonCamera>(FindObjectsSortMode.None).First(x=>x.GetComponent<Camera>().enabled);var focus=m.Player.position+Vector3.up;var eye=focus+new Vector3(-3,1,3);camera.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),52,1);
                yield return WaitGameSeconds(.15f);RenderCargo();ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/live-carry.png");yield return null;Keys();c.Cancel();
                Load("N1_01");Place(c.Sites["dayOneFamily"].position+Vector3.back*5);c.Actor("Hùng").StopAt(c.Sites["dayOneFamily"].position+Vector3.right*4);Call("BeginScene",Map02ChapterDefinition.Load().Find("N1_01"),0,0);
                yield return WaitGameSeconds(.2f);Assert.True((bool)typeof(Map02ChapterDirector).GetProperty("HoldMealVoice",flags).GetValue(c));
                float deadline=Time.time+20;while((bool)typeof(Map02ChapterDirector).GetProperty("HoldMealVoice",flags).GetValue(c)&&Time.time<deadline)yield return null;
                yield return WaitGameSeconds(.3f);Assert.Less(Vector3.Distance(m.Player.position,c.Sites["dayOneFamily"].position+new Vector3(0,0,-1.85f)),.25f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/live-meal-start.png");yield return null;c.Cancel();
                Load("D1_05");c.enabled=false;var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D1_05");Place(marker.transform.position);m.Interact(marker);Call("EndScene");for(int i=0;i<6;i++)c.State.dayOne.Strike(.5f);
                Set("selectedBoard",3);Set("dockSlot",0);Set("dockOffset",0f);Call("ConfirmDockBoard");Assert.AreEqual(0,c.State.dayOne.BoardCount);
                Set("selectedBoard",0);Set("dockOffset",.3f);Call("ConfirmDockBoard");Assert.AreEqual(0,c.State.dayOne.BoardCount);Set("dockOffset",0f);Call("ConfirmDockBoard");Assert.AreEqual(1,c.State.dayOne.BoardCount);
                ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/dock-redesign.png");yield return null;var save=m.Capture();c.Cancel();m.Restore(save);Assert.AreEqual(1,c.State.dayOne.BoardCount);Assert.False(c.State.Done("D1_05"));
            } finally {Keys();c.Cancel();c.enabled=true;RestoreInputRouting(route);}
        }
        [Test]public void CargoMaskPreservesForwardFeetAndChangesHandContact()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");var go=Object.Instantiate(prefab);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            try {
                var a=go.GetComponentInChildren<Animator>();var walk=a.runtimeAnimatorController.animationClips.First(c=>c.name=="Walk_Tuned");Vector3 left,right,hand;
                using(var pose=new Map01CinematicPose(a,null,false)){pose.Sample(walk,.32f,true);left=a.GetBoneTransform(HumanBodyBones.LeftFoot).position;right=a.GetBoneTransform(HumanBodyBones.RightFoot).position;hand=a.GetBoneTransform(HumanBodyBones.RightHand).position;}
                using(var cargo=new Map02UpperBodyPose(a,Resources.Load<Map02DayOneArt>("Story/Map02DayOneArt").Find("Carry"))){cargo.Sample(walk,.32f);Assert.Less(Vector3.Distance(left,a.GetBoneTransform(HumanBodyBones.LeftFoot).position),.03f);Assert.Less(Vector3.Distance(right,a.GetBoneTransform(HumanBodyBones.RightFoot).position),.03f);Assert.Greater(Vector3.Distance(hand,a.GetBoneTransform(HumanBodyBones.RightHand).position),.1f,"Upper layer must actually position the hands, not merely leave them swinging.");}
            } finally {Object.DestroyImmediate(go);}
        }
        [Test]public void CarryFramesAreForwardAndHungUsesTheMapOneAsset()
        {
            var art=Resources.Load<Map02DayOneArt>("Story/Map02DayOneArt");StringAssert.Contains("Hung_DiKhapKhieng",AssetDatabase.GetAssetPath(art.Find("InjuredWalk")));
            foreach(var id in new[]{"CarryWalk","AssistWalk"})foreach(var binding in AnimationUtility.GetCurveBindings(art.Find(id)).Where(b=>b.propertyName=="RootQ.y"))foreach(var key in AnimationUtility.GetEditorCurve(art.Find(id),binding).keys)Assert.AreEqual(0,key.value,.001f);
        }
        [UnityTest,Timeout(300000)]public IEnumerator ActualFamilyCargoAndPostPresentation()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return RunPresentation();yield return new ExitPlayMode();
        }
        static IEnumerator RunPresentation()
        {
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();c.enabled=false;var route=RouteInputToGame();bool oldShader=EditorSettings.asyncShaderCompilation,oldAllow=ShaderUtil.allowAsyncCompilation;EditorSettings.asyncShaderCompilation=false;ShaderUtil.allowAsyncCompilation=false;
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            object Call(string name,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(name,flags).Invoke(c,args);
            void Place(Vector3 point){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=point;m.Player.rotation=Quaternion.identity;cc.enabled=true;Physics.SyncTransforms();}
            void LoadBefore(string id){var snap=m.Capture();var state=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id==id)break;state.Complete(task.id);}state.dayOne.arrivalSeen=true;snap.chapter=state;m.Restore(snap);}
            void View(Vector3 focus,Vector3 offset){Object.FindObjectsByType<ShadowVale.Gameplay.Player.ThirdPersonCamera>(FindObjectsSortMode.None).First(x=>x.GetComponent<Camera>().enabled).SetCinematicView(focus+offset,Quaternion.LookRotation(-offset),52,1);}
            Directory.CreateDirectory("Logs/Map02Presentation_20261022");
            void Angle(string id,Vector3 offset)
            {
                var origin=c.Sites["dayOneFamily"].position+new Vector3(0,.75f,-1.1f);var camera=new GameObject("Meal camera audit").AddComponent<Camera>();var src=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null);camera.CopyFrom(src);camera.enabled=false;camera.transform.SetPositionAndRotation(origin+offset,Quaternion.LookRotation(-offset));camera.fieldOfView=50;
                var target=new RenderTexture(1024,576,24);camera.targetTexture=target;camera.Render();var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,576,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,576),0,0);image.Apply();File.WriteAllBytes("Logs/Map02Presentation_20261022/angle-"+id+".png",image.EncodeToPNG());RenderTexture.active=before;camera.targetTexture=null;Object.Destroy(camera.gameObject);Object.Destroy(target);Object.Destroy(image);
            }
            try
            {
                LoadBefore("D1_03");yield return null;
                var commander=c.Actor("Anh Thành").visual.GetComponentInChildren<Map01Rifle>();Assert.NotNull(commander);Assert.True(commander.weapon.gameObject.activeInHierarchy);Assert.True(commander.weapon.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&!r.forceRenderingOff));
                var elder=c.Actor("Ông Sáu");var child=c.Actor("Bé Tí");Assert.Greater(Vector3.Distance(elder.transform.position,child.transform.position),2);
                View(elder.transform.position+Vector3.up*.8f,new Vector3(-3,1,-5));yield return WaitGameSeconds(.2f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/family-day.png");yield return null;
                var cargo=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D1_04");Place(cargo.transform.position);Assert.True(m.Interact(cargo));Call("LateUpdate");File.WriteAllLines("Logs/Map02Presentation_20261022/weapons.txt",m.Player.GetComponentsInChildren<Weapon>(true).SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).Select(r=>r.name+" "+r.GetType().Name+" enabled="+r.enabled+" forceOff="+r.forceRenderingOff+" active="+r.gameObject.activeInHierarchy));Assert.True(c.WorkRifleSlung);Assert.Greater(m.Player.GetComponentsInChildren<MeshRenderer>().Count(r=>r.transform.parent?.name=="Work rifle — slung over shoulder, no gameplay scripts"),0);
                Assert.True(m.Player.GetComponentsInChildren<Weapon>(true).SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).All(r=>r.forceRenderingOff));
                View(m.Player.position+Vector3.up,new Vector3(-3,1,3));c.enabled=true;yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/carry-slung.png");yield return null;c.enabled=false;File.WriteAllLines("Logs/Map02Presentation_20261022/nam-animators.txt",m.Player.GetComponentsInChildren<Animator>(true).Select(a=>a.name+" enabled="+a.enabled+" human="+a.isHuman+" skins="+string.Join(",",a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.name+"="+r.enabled+"/"+r.forceRenderingOff))));var anim=m.Player.GetComponentInChildren<Animator>();File.WriteAllText("Logs/Map02Presentation_20261022/carry-contact.txt","root="+m.Player.position+" left="+anim.GetBoneTransform(HumanBodyBones.LeftHand).position+" right="+anim.GetBoneTransform(HumanBodyBones.RightHand).position);c.State.carry=null;Call("UpdateWorkWeapon");Assert.False(c.WorkRifleSlung);
                typeof(Map02Mission).GetMethod("TogglePanel",flags).Invoke(m,new object[]{1});yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/inventory.png");yield return null;m.CloseGameplayPanel();
                LoadBefore("N1_01");Place(c.Sites["dayOneFamily"].position+Vector3.back*1.6f);Call("BeginScene",Map02ChapterDefinition.Load().Find("N1_01"),0,0);typeof(Map02ChapterDirector).GetField("sceneClock",flags).SetValue(c,35f);Call("LateUpdate");
                foreach(var person in new[]{"Chị Lành","Ông Sáu","Bé Tí"})Assert.Less(Vector3.Distance(c.Actor(person).transform.position,c.Sites["dayOneFamily"].position),3,"Each family member must sit at the shared meal.");
                c.enabled=true;yield return WaitGameSeconds(.3f);Angle("A",new Vector3(3,1.7f,-3));Angle("B",new Vector3(0,2,-4));Angle("C",new Vector3(4,1.7f,0));Angle("D",new Vector3(-3,1.7f,0));ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/family-meal.png");yield return null;c.enabled=false;c.Cancel();
                LoadBefore("N1_02");Call("StartPost",Map02ChapterDefinition.Load().Find("N1_02"));var lamp=(System.Collections.Generic.List<Transform>)typeof(Map02ChapterDirector).GetField("postLamps",flags).GetValue(c);Place(lamp[0].position+Vector3.back*.6f);var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.E));InputSystem.Update();Assert.True(c.TryInteractNearbyPost());Assert.AreEqual("Lamp",c.DayActivityName);InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());InputSystem.Update();
                yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/post-lamp.png");yield return null;c.Cancel();
                var bundle=(System.Collections.Generic.List<Transform>)typeof(Map02ChapterDirector).GetField("postBundles",flags).GetValue(c);Place(bundle[0].position);InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.E));InputSystem.Update();Assert.True(c.TryInteractNearbyPost());Assert.AreEqual("Path",c.DayActivityName);InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());InputSystem.Update();yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/post-path.png");yield return null;
            }
            finally{c.Cancel();c.enabled=true;RestoreInputRouting(route);EditorSettings.asyncShaderCompilation=oldShader;ShaderUtil.allowAsyncCompilation=oldAllow;}
        }
    }
}
