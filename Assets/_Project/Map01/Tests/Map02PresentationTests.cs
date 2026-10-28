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
                // As played: Nam has brought Hùng to chị Lành's porch and stands a little off.
                Load("D1_02");c.Actor("Hùng").StopAt(c.Sites["dayOneFamily"].position);Place(c.Sites["dayOneFamily"].position+Vector3.back*2.2f+Vector3.left*1.2f);Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_02"),0,0);Set("sceneClock",5f);Call("LateUpdate");
                var hung=c.Actor("Hùng");var lanh=c.Actor("Chị Lành");var side=lanh.transform.position-hung.transform.position;Assert.Greater(Vector3.Dot(side,hung.transform.right),.55f,"Lành must work beside Hùng, not stand directly in front.");var chair=(Transform)Get("sceneChair");var seat=chair.position;
                c.enabled=true;yield return WaitGameSeconds(.4f);Set("sceneClock",10f);yield return WaitGameSeconds(.2f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/bandage-scene.png");yield return null;
                {var nam=m.Player.GetComponentInChildren<Animator>();File.WriteAllText("Logs/Map02Presentation_20261022/bandage-nam.txt","controller="+(nam.runtimeAnimatorController!=null?nam.runtimeAnimatorController.name:"null")+" hips="+nam.GetBoneTransform(HumanBodyBones.Hips).localRotation.eulerAngles.ToString("F1")+" lArm="+nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation.eulerAngles.ToString("F1")+" scene="+c.CurrentScene?.id+" lanh="+lanh.transform.position.ToString("F2")+" hung="+hung.transform.position.ToString("F2")+" nam="+m.Player.position.ToString("F2"));
                 Assert.Less(Vector3.Angle(nam.GetBoneTransform(HumanBodyBones.LeftLowerArm).position-nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,Vector3.down),60,"Nam stands at rest during the bandage, not in a T-pose.");}
                Call("EndScene");Assert.Greater(Vector3.Distance(hung.transform.position,seat),.7f,"Hùng must clear the chair before resuming standing locomotion.");
                var commander=c.Actor("Anh Thành").visual.GetComponentInChildren<Animator>();Assert.AreEqual(commander.GetBoneTransform(HumanBodyBones.Chest),commander.GetComponent<Map01Rifle>().weapon.transform.parent);
                Load("D1_03");c.State.carry="D1_04";Place(c.Sites["dayOneFamily"].position+Vector3.back*4);Keys(Key.W);yield return WaitGameSeconds(.8f);Assert.NotNull(Get("cargoPose"));Assert.IsNull(Get("dayNamPose"));Assert.True(c.WorkRifleSlung);
                var crates=(System.Collections.Generic.List<Transform>)Get("physicalCrates");Assert.True(crates[0].gameObject.activeInHierarchy);Assert.Less(Vector3.Distance(crates[0].position,m.Player.position),1.8f,"Carried crate must remain attached after mission Update/LateUpdate.");
                var camera=Object.FindObjectsByType<ShadowVale.Gameplay.Player.ThirdPersonCamera>(FindObjectsSortMode.None).First(x=>x.GetComponent<Camera>().enabled);var focus=m.Player.position+Vector3.up;var eye=focus+new Vector3(-3,1,3);camera.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),52,1);
                yield return WaitGameSeconds(.15f);RenderCargo();ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/live-carry.png");yield return null;Keys();c.Cancel();
                Load("N1_01");Place(c.Sites["dayOneFamily"].position+Vector3.back*5);c.Actor("Hùng").StopAt(c.Sites["dayOneFamily"].position+Vector3.right*4);var hungGameBody=c.Actor("Hùng").Animator.transform;var hungGameScale=hungGameBody.localScale;var hungGameLift=hungGameBody.localPosition;
                File.WriteAllText("Logs/Map02Presentation_20261022/hung-body.txt","localScale="+hungGameScale.ToString("F4")+" localPosition="+hungGameLift.ToString("F4")+" lossy="+hungGameBody.lossyScale.ToString("F4"));
                Call("BeginScene",Map02ChapterDefinition.Load().Find("N1_01"),0,0);
                yield return WaitGameSeconds(.2f);Assert.True((bool)typeof(Map02ChapterDirector).GetProperty("HoldMealVoice",flags).GetValue(c));
                float MealClock()=>(float)Get("sceneClock")-(float)Get("dayApproachDuration");
                float deadline=Time.time+30;while(MealClock()<.8f&&Time.time<deadline)yield return null;
                // "Lời hứa" as approved in Blender: cross-legged side by side behind the tray, facing the doorway, Hùng on Nam's right.
                var supper=(Transform)Get("mealRoot");var namBody=m.Player.GetComponentInChildren<Animator>().transform;var hungBody=c.Actor("Hùng").Animator.transform;
                Assert.Less(Vector3.Distance(namBody.position,supper.TransformPoint(new Vector3(.76f,0,-.35f))),.03f,"Nam sits at his place behind the tray.");
                Assert.Less(Vector3.Distance(hungBody.position,supper.TransformPoint(new Vector3(.76f,0,.35f))),.03f,"Hùng sits on his right.");
                Assert.Greater(Vector3.Dot(namBody.forward,-supper.right),.99f);Assert.Greater(Vector3.Dot(hungBody.forward,-supper.right),.99f,"Both face the tray and the doorway.");
                Assert.AreEqual(1f,hungBody.lossyScale.y,.01f,"Hùng at his own size for the supper, as it was made.");
                Assert.AreEqual("LoiHuaHung",((AnimationClip)typeof(Map01CinematicPose).GetField("selected",flags).GetValue(typeof(Map02ChapterActor).GetField("pose",flags).GetValue(c.Actor("Hùng")))).name);
                Assert.True((bool)typeof(Map02ChapterDirector).GetProperty("HoldMealVoice",flags).GetValue(c),"The opening is silent until the rice-bowl exchange.");
                ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/live-meal-start.png");yield return null;c.Cancel();
                Assert.AreEqual(hungGameScale,hungBody.localScale,"Hùng back at his game size once the supper ends.");Assert.Less(Vector3.Distance(hungGameLift,hungBody.localPosition),.001f);
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
            // The approved Night 1 motion capture (2026-10-24): humanoid, each on its own actor's game avatar; only the walk loops.
            foreach(var (id,file,loop) in new[]{("LoiHuaNam","LoiHua_Nam",false),("LoiHuaHung","LoiHua_Hung",false),("HungArmWalk","Hung_DiOmTay",true),("HungStartleGrab","Hung_GiatMinh_ChopVai",false),("HungStartle","Hung_GiatMinh",false)})
            {var clip=art.Find(id);Assert.NotNull(clip,id);StringAssert.Contains("Mocap/"+file+".fbx",AssetDatabase.GetAssetPath(clip));Assert.True(clip.humanMotion,id);Assert.AreEqual(loop,clip.isLooping,id);}
            Assert.AreEqual(70,art.Find("LoiHuaNam").length,.1f,"The supper take runs the whole 70 s scene.");
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
                var origin=((Transform)typeof(Map02ChapterDirector).GetField("mealRoot",flags).GetValue(c)).position+Vector3.up*.75f;var camera=new GameObject("Meal camera audit").AddComponent<Camera>();var src=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null);camera.CopyFrom(src);camera.enabled=false;camera.transform.SetPositionAndRotation(origin+offset,Quaternion.LookRotation(-offset));camera.fieldOfView=50;
                var target=new RenderTexture(1024,576,24);camera.targetTexture=target;camera.Render();var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,576,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,576),0,0);image.Apply();File.WriteAllBytes("Logs/Map02Presentation_20261022/angle-"+id+".png",image.EncodeToPNG());RenderTexture.active=before;camera.targetTexture=null;Object.Destroy(camera.gameObject);Object.Destroy(target);Object.Destroy(image);
            }
            try
            {
                LoadBefore("D1_03");yield return null;
                var commander=c.Actor("Anh Thành").visual.GetComponentInChildren<Map01Rifle>();Assert.NotNull(commander);Assert.True(commander.weapon.gameObject.activeInHierarchy);Assert.True(commander.weapon.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&!r.forceRenderingOff));
                var radio=(Transform)typeof(Map02ChapterDirector).GetField("hungRadio",flags).GetValue(c);var signaller=c.Actor("Hùng").transform;Call("UpdateHungRadio");
                // Newly enabled/moved props must reach the render loop before taking a manual camera snapshot.
                yield return null;yield return null;Call("UpdateHungRadio");
                Assert.True(radio.gameObject.activeSelf,"Hùng carries the radio set on Day 1.");var hungBody=c.Actor("Hùng").visual.GetComponentInChildren<Animator>();var hungFacing=Map02ChapterDirector.BodyForward(hungBody,signaller);Assert.Less(Vector3.Dot(radio.position-hungBody.GetBoneTransform(HumanBodyBones.Chest).position,hungFacing),-.1f,"The set rides on his back, behind his chest.");
                {var eye=signaller.position+signaller.right*2.2f+Vector3.up*1.2f;var across=Vector3.Cross(Vector3.up,hungFacing);foreach(var side in new[]{across-hungFacing*.8f,-across-hungFacing*.8f,-hungFacing,across,-across}){var candidate=signaller.position+side.normalized*2.2f+Vector3.up*1.2f;if(!Physics.Linecast(candidate,signaller.position+Vector3.up*1.15f,~0,QueryTriggerInteraction.Ignore)){eye=candidate;break;}}var radioLens=new GameObject("Radio audit").AddComponent<Camera>();radioLens.CopyFrom(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null));radioLens.enabled=false;radioLens.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(signaller.position+Vector3.up*1.15f-eye));radioLens.fieldOfView=50;var target=new RenderTexture(1024,576,24);radioLens.targetTexture=target;radioLens.Render();var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,576,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,576),0,0);image.Apply();File.WriteAllBytes("Logs/Map02Presentation_20261022/hung-radio.png",image.EncodeToPNG());RenderTexture.active=before;radioLens.targetTexture=null;Object.Destroy(radioLens.gameObject);Object.Destroy(target);Object.Destroy(image);}
                var elder=c.Actor("Ông Sáu");var child=c.Actor("Bé Tí");Assert.Greater(Vector3.Distance(elder.transform.position,child.transform.position),2);
                var meal=(Transform)typeof(Map02ChapterDirector).GetField("mealRoot",flags).GetValue(c);Call("TickLight",10f);Assert.False(meal.gameObject.activeSelf,"Supper is not laid out through Day 1.");
                var rest=(AnimationClip)typeof(Map02ChapterActor).GetField("seatedRest",flags).GetValue(elder);Assert.AreEqual("SauNgoiNghi",rest.name,"Ông Sáu rests on his chair (mocap, hands on his thighs), not reaching for a bowl.");
                View(elder.transform.position+Vector3.up*.8f,new Vector3(-3,1,-5));yield return WaitGameSeconds(.2f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/family-day.png");yield return null;
                var cargo=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D1_04");Place(cargo.transform.position);Assert.True(m.Interact(cargo));Call("LateUpdate");File.WriteAllLines("Logs/Map02Presentation_20261022/weapons.txt",m.Player.GetComponentsInChildren<Weapon>(true).SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).Select(r=>r.name+" "+r.GetType().Name+" enabled="+r.enabled+" forceOff="+r.forceRenderingOff+" active="+r.gameObject.activeInHierarchy));Assert.True(c.WorkRifleSlung);Assert.Greater(m.Player.GetComponentsInChildren<MeshRenderer>().Count(r=>r.transform.parent?.name=="Work rifle — slung over shoulder, no gameplay scripts"),0);
                Assert.True(m.Player.GetComponentsInChildren<Weapon>(true).SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).All(r=>r.forceRenderingOff));
                // 23/10 screenshots: the copy hung down the right thigh, muzzle low, in front of the crate.
                var sling=m.Player.GetComponentsInChildren<Transform>().First(t=>t.name=="Work rifle — slung over shoulder, no gameplay scripts");var parts=sling.GetComponentsInChildren<MeshRenderer>();var slung=parts[0].bounds;foreach(var part in parts)slung.Encapsulate(part.bounds);
                Assert.Less(Vector3.Dot(slung.center-m.Player.position,m.Player.forward),-.05f,"The slung rifle hangs behind Nam, not in front of the crate.");
                Assert.Greater(sling.Find("Muzzle").position.y-m.Player.position.y,1.35f,"Muzzle up behind the shoulder.");Assert.Greater(slung.min.y-m.Player.position.y,.55f,"The stock stays above the knee, not down the leg.");
                View(m.Player.position+Vector3.up,new Vector3(-3,1,3));c.enabled=true;yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/carry-slung.png");yield return null;c.enabled=false;File.WriteAllLines("Logs/Map02Presentation_20261022/nam-animators.txt",m.Player.GetComponentsInChildren<Animator>(true).Select(a=>a.name+" enabled="+a.enabled+" human="+a.isHuman+" skins="+string.Join(",",a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.name+"="+r.enabled+"/"+r.forceRenderingOff))));var anim=m.Player.GetComponentInChildren<Animator>();File.WriteAllText("Logs/Map02Presentation_20261022/carry-contact.txt","root="+m.Player.position+" left="+anim.GetBoneTransform(HumanBodyBones.LeftHand).position+" right="+anim.GetBoneTransform(HumanBodyBones.RightHand).position);c.State.carry=null;Call("UpdateWorkWeapon");Assert.False(c.WorkRifleSlung);
                typeof(Map02Mission).GetMethod("TogglePanel",flags).Invoke(m,new object[]{1});yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/inventory.png");yield return null;m.CloseGameplayPanel();
                LoadBefore("N1_01");Call("TickLight",10f);Assert.True(meal.gameObject.activeSelf,"Supper is laid out on Night 1.");
                Place(c.Sites["dayOneFamily"].position+Vector3.back*1.6f);Call("BeginScene",Map02ChapterDefinition.Load().Find("N1_01"),0,0);
                float approach=(float)typeof(Map02ChapterDirector).GetField("dayApproachDuration",flags).GetValue(c);var bowl=(Transform)typeof(Map02ChapterDirector).GetField("fishBowl",flags).GetValue(c);
                void Clock(float meal)=>typeof(Map02ChapterDirector).GetField("sceneClock",flags).SetValue(c,approach+meal);
                Clock(.2f);Call("SampleDayScene");var dish=bowl.localPosition;Clock(6f);Call("SampleDayScene");Assert.Less(Vector3.Distance(bowl.localPosition,dish),.005f,"The fish dish waits until Nam pushes it over (6.8 s), it does not jump there.");
                Clock(8.5f);Call("SampleDayScene");Assert.Greater(bowl.localPosition.z-dish.z,.1f,"Nam has pushed the fish dish over toward Hùng.");
                Clock(5f);Call("LateUpdate");
                // "Lời hứa" (doc): Nam and Hùng alone on the porch; the family is indoors, Lành speaks from the kitchen; seen from inside, through the doorway.
                foreach(var person in new[]{"Chị Lành","Ông Sáu","Bé Tí"})Assert.False(c.Actor(person).visual.gameObject.activeInHierarchy,person+" is indoors, not at the porch supper.");
                var door=(Vector3)typeof(Map02ChapterDirector).GetField("porchDoor",flags).GetValue(c);var outward=(Vector3)typeof(Map02ChapterDirector).GetField("porchOut",flags).GetValue(c);
                Assert.Greater(Vector3.Dot(meal.position-door,outward),.5f,"The tray is on the porch, outside the doorway.");
                var namAt=meal.InverseTransformPoint(m.Player.GetComponentInChildren<Animator>().transform.position);var hungAt=meal.InverseTransformPoint(c.Actor("Hùng").Animator.transform.position);
                Assert.Greater(namAt.x,.5f);Assert.Greater(hungAt.x,.5f,"Both sit on the yard side of the tray, facing the doorway.");Assert.Less(namAt.z,-.2f);Assert.Greater(hungAt.z,.2f,"Side by side, Hùng on Nam's right.");
                c.enabled=true;yield return WaitGameSeconds(.3f);var lens=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null).transform;
                Assert.Less(Vector3.Dot(lens.position-door,outward),-2,"The supper is framed from inside the house, through the doorway.");ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/family-meal.png");yield return null;
                // The Blender timeline's cuts on the scene's clock: the bump and the joke; "Quê tôi... không còn ai đợi"; each stays on the house side of the two men.
                foreach(var (at,shot) in new[]{(35f,"meal-two-shot"),(42.4f,"meal-duck"),(60f,"meal-nam-close")}){Clock(at);yield return WaitGameSeconds(.3f);Assert.Less(Vector3.Dot(lens.position-meal.position,meal.right),.5f,shot);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/"+shot+".png");yield return null;}c.enabled=false;c.Cancel();
                // The duck flaps on the way to the bamboo post (doc Night 1), not once Nam is already there.
                LoadBefore("N1_02");var hung=c.Actor("Hùng");Place(c.Sites["dayOneFamily"].position+Vector3.back*3);hung.StopAt(m.Player.position+Vector3.right);
                typeof(Map02ChapterDirector).GetField("escort",flags).SetValue(c,hung);c.State.activeTask="N1_02";Call("TickDay",.02f);
                Assert.NotNull(c.DuckPoint);Assert.False(c.State.dayOne.duckSeen,"Nothing flaps at the porch.");
                Assert.Greater(Vector3.Distance(c.DuckPoint.Value,m.Player.position),6);Assert.Greater(Vector3.Distance(c.DuckPoint.Value,c.Sites["bamboo"].position),9,"Halfway, not at the post.");
                Place(c.DuckPoint.Value);hung.StopAt(c.DuckPoint.Value+Vector3.right);Call("TickDay",.02f);Assert.True(c.State.dayOne.duckSeen);
                // Hùng jolts and grabs Nam's right shoulder from behind; Nam stands still for it, then both go on.
                Assert.True(c.DuckStartled);Assert.True(c.MovementLocked);c.enabled=true;yield return WaitGameSeconds(1.1f);
                var grip=hung.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position;var shoulder=m.Player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                Assert.Less(Vector3.Distance(grip,shoulder),.2f,"Hùng's good hand is on Nam's shoulder.");ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/duck-grab.png");yield return null;
                {var mid=(m.Player.position+hung.transform.position)*.5f+Vector3.up*1.1f;var eye=mid+m.Player.right*2.6f+m.Player.forward*.7f+Vector3.up*.3f;var side=new GameObject("Duck grab audit").AddComponent<Camera>();side.CopyFrom(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null));side.enabled=false;side.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(mid-eye));side.fieldOfView=40;
                var target=new RenderTexture(1280,720,24);side.targetTexture=target;side.Render();var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Map02Presentation_20261022/duck-grab-side.png",image.EncodeToPNG());RenderTexture.active=before;side.targetTexture=null;Object.Destroy(side.gameObject);Object.Destroy(target);Object.Destroy(image);}
                yield return WaitGameSeconds(3.6f);Assert.False(c.DuckStartled,"He lets go after \"Biết mà\".");Assert.False(c.MovementLocked);c.enabled=false;
                typeof(Map02ChapterDirector).GetField("escort",flags).SetValue(c,null);c.Cancel();
                LoadBefore("N1_02");var watch=c.Actor(Map02ChapterDirector.Guard);Assert.True(watch.visual.gameObject.activeInHierarchy,"The bamboo post has its own watch.");Assert.Less(Vector3.Distance(watch.transform.position,c.Sites["bamboo"].position),3);Assert.Less(Vector3.Distance(c.Actor("Anh Lực").transform.position,c.Sites["gate"].position),3,"Anh Lực keeps the village gate all night.");
                View(c.Sites["bamboo"].position+Vector3.up*1f,new Vector3(-2.5f,1.2f,4.5f));c.enabled=true;yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/bamboo-watch.png");yield return null;c.enabled=false;
                Call("StartPost",Map02ChapterDefinition.Load().Find("N1_02"));var lamp=(System.Collections.Generic.List<Transform>)typeof(Map02ChapterDirector).GetField("postLamps",flags).GetValue(c);Place(lamp[0].position+Vector3.back*.6f);var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.E));InputSystem.Update();Assert.True(c.TryInteractNearbyPost(),$"post input: nearby={c.HasNearbyPost} busy={c.Busy} panel={m.Panel} phase={c.State.phase} companion={c.Companion} held={keyboard.eKey.wasPressedThisFrame} posts={c.State.dayOne.postsReached}");Assert.AreEqual("Lamp",c.DayActivityName);InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());InputSystem.Update();
                yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/post-lamp.png");yield return null;c.Cancel();
                var bundle=(System.Collections.Generic.List<Transform>)typeof(Map02ChapterDirector).GetField("postBundles",flags).GetValue(c);Place(bundle[0].position);InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.E));InputSystem.Update();Assert.True(c.TryInteractNearbyPost(),$"post input: nearby={c.HasNearbyPost} busy={c.Busy} panel={m.Panel} phase={c.State.phase} companion={c.Companion} held={keyboard.eKey.wasPressedThisFrame} posts={c.State.dayOne.postsReached}");Assert.AreEqual("Path",c.DayActivityName);InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());InputSystem.Update();yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/post-path.png");yield return null;
            }
            finally{c.Cancel();c.enabled=true;RestoreInputRouting(route);EditorSettings.asyncShaderCompilation=oldShader;ShaderUtil.allowAsyncCompilation=oldAllow;}
        }
    }
}
