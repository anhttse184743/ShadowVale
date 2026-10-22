using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DayOneVisualTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator CaptureWorkAndPorchStaging()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();Map02Mission m=null;float end=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<end){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);
            Assert.That(Object.FindObjectsByType<Map02ChapterActor>(FindObjectsSortMode.None).SelectMany(a=>a.GetComponentsInChildren<Camera>(true)).All(camera=>!camera.enabled),"Imported actor cameras must not overwrite the gameplay camera.");
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera=>camera.enabled&&camera.targetTexture==null&&camera.gameObject.scene.name=="Map 2"),Is.EqualTo(1),"Exactly one scene camera owns the world view.");
            Assert.That(Object.FindObjectsByType<Map02ChapterActor>(FindObjectsSortMode.None).SelectMany(a=>a.GetComponentsInChildren<Light>(true)).All(light=>!light.enabled||light.intensity<100),"FBX preview lamps must not wash out character skin.");
            bool oldShaders=UnityEditor.EditorSettings.asyncShaderCompilation;bool oldAllow=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.EditorSettings.asyncShaderCompilation=false;UnityEditor.ShaderUtil.allowAsyncCompilation=false;var c=m.Chapter;c.enabled=false;Directory.CreateDirectory("Logs/Map02DayOne/Visual");SetPreviewResolution(1920,1080);
            object Call(string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
            void Clock(float time)=>typeof(Map02ChapterDirector).GetField("sceneClock",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,time);
            void LoadBefore(string id){var saved=m.Capture();var state=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==id)break;state.Complete(t.id);}saved.chapter=state;m.Restore(saved);}
            void Move(Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
            void Audit(string name){File.WriteAllLines("Logs/Map02DayOne/Visual/"+name+".txt",Object.FindObjectsByType<Map02ChapterActor>(FindObjectsSortMode.None).Select(a=>{var animator=a.visual?.GetComponentInChildren<Animator>();return a.role+" root="+a.transform.position+" visual="+a.visual?.position+" animator="+animator?.transform.position+" bounds="+a.visual?.GetComponentInChildren<SkinnedMeshRenderer>()?.bounds+" human="+animator?.isHuman+" head="+(animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head).position.ToString():"none");}));}
            void Angle(string name,Vector3 offset){var source=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled);var cameraObject=new GameObject("Angle audit");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;var focus=c.Actor("Hùng").transform.position+Vector3.up*.75f;camera.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));camera.fieldOfView=50;var target=new RenderTexture(1024,576,24);camera.targetTexture=target;camera.Render();var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,576,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,576),0,0);image.Apply();File.WriteAllBytes("Logs/Map02DayOne/Visual/angle_"+name+".png",image.EncodeToPNG());RenderTexture.active=before;camera.targetTexture=null;Object.Destroy(cameraObject);Object.Destroy(target);Object.Destroy(image);}
            try
            {
                LoadBefore("D1_02");Move(c.Sites["dayOneFamily"].position+Vector3.back*1.5f);c.Actor("Hùng").StopAt(c.Sites["dayOneFamily"].position+Vector3.forward*.6f);Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_02"),0,0);Clock(7);Call("LateUpdate");Audit("bandage");
                var lanhAnimator=c.Actor("Chị Lành").visual.GetComponentInChildren<Animator>();float floor=float.PositiveInfinity;foreach(var skin in lanhAnimator.GetComponentsInChildren<SkinnedMeshRenderer>()){var baked=new Mesh();skin.BakeMesh(baked);foreach(var vertex in baked.vertices)floor=Mathf.Min(floor,skin.transform.TransformPoint(vertex).y);Object.Destroy(baked);}Assert.That(floor,Is.EqualTo(c.Actor("Hùng").transform.position.y).Within(.04f),"Lành must stand on the floor during bandaging.");yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02DayOne/Visual/bandage.png");yield return null;c.Cancel();
                LoadBefore("N1_01");Move(c.Sites["dayOneFamily"].position+Vector3.back*1.5f);Call("BeginScene",Map02ChapterDefinition.Load().Find("N1_01"),0,0);Clock(12);Call("LateUpdate");Audit("meal");File.WriteAllLines("Logs/Map02DayOne/Visual/cameras.txt",Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(camera=>camera.name+" scene="+camera.gameObject.scene.name+" flags="+camera.hideFlags+" parent="+camera.transform.parent?.name+" enabled="+camera.enabled+" depth="+camera.depth+" at="+camera.transform.position+" yaw="+camera.transform.eulerAngles+" components="+string.Join(",",camera.GetComponents<MonoBehaviour>().Select(x=>x.GetType().Name))));Angle("A",new Vector3(-3,.3f,2));Angle("B",new Vector3(-4,.3f,-2));Angle("C",new Vector3(0,.2f,-3));Angle("D",new Vector3(-3,.2f,0));yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02DayOne/Visual/meal.png");yield return null;c.Cancel();
                LoadBefore("D1_03");Move(c.Sites["store"].position);c.State.Increment("D1_04");c.State.Increment("D1_04");c.State.Increment("D1_04");c.UpdateMarkers();var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D1_04");Assert.True(m.Interact(marker));yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02DayOne/Visual/packing.png");yield return null;c.Cancel();
                var repair=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D1_05");Move(repair.transform.position);Assert.True(m.Interact(repair));Call("EndScene");yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02DayOne/Visual/dock.png");yield return null;c.Cancel();
            }finally{c.Cancel();c.enabled=true;UnityEditor.EditorSettings.asyncShaderCompilation=oldShaders;UnityEditor.ShaderUtil.allowAsyncCompilation=oldAllow;}
            yield return new ExitPlayMode();
        }
    }
}
