using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    /// <summary>Plan step 6, group B (2026-10-25): the shift change, ông Sáu at rest and chị Lành taking the medicine, on mocap.</summary>
    public sealed class Map02GroupBTests:ForestSceneTestBase
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        const string Folder="Logs/Map02Presentation_20261022";
        [UnityTest,Timeout(240000)]public IEnumerator ShiftChangeIsSeenFromInsideTheHouse()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return Check();yield return new ExitPlayMode();
        }
        static Vector3 Flat(Vector3 v)=>new Vector3(v.x,0,v.z);
        static IEnumerator Check()
        {
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();Directory.CreateDirectory(Folder);
            object Call(string n,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(n,Private).Invoke(c,args);
            object Get(string n)=>typeof(Map02ChapterDirector).GetField(n,Private).GetValue(c);
            void Set(string n,object v)=>typeof(Map02ChapterDirector).GetField(n,Private).SetValue(c,v);
            void LoadBefore(string id){var snap=m.Capture();var state=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id==id)break;state.Complete(task.id);}state.dayOne.arrivalSeen=true;snap.chapter=state;m.Restore(snap);}
            try
            {
                LoadBefore("N1_04");Call("TickLight",10f);Assert.False(((Transform)Get("mealRoot")).gameObject.activeSelf,"Supper is cleared once eaten; the watch mat lies there now.");
                Call("BeginScene",Map02ChapterDefinition.Load().Find("N1_04"),0,0);Assert.AreEqual(0f,(float)Get("dayApproachDuration"),"A fade, not a walk.");
                Set("sceneClock",6f);Call("LateUpdate");
                var door=(Vector3)Get("porchDoor");var outward=(Vector3)Get("porchOut");var hung=c.Actor("Hùng").Animator;var nam=m.Player.GetComponentInChildren<Animator>();
                Assert.Less(hung.GetBoneTransform(HumanBodyBones.Hips).position.y-door.y,.32f,"Hùng lies on the mat.");
                Assert.Less(Mathf.Abs(hung.GetBoneTransform(HumanBodyBones.Head).position.y-hung.GetBoneTransform(HumanBodyBones.Hips).position.y),.25f,"Flat on his back, not sitting up.");
                Assert.Less(nam.GetBoneTransform(HumanBodyBones.Hips).position.y-door.y,.3f,"Nam sits on the boards.");
                Assert.Greater(Vector3.Dot(Map02ChapterDirector.BodyForward(nam,m.Player),outward),.8f,"Back to the house, facing the yard.");
                Assert.Greater(Vector3.Dot(nam.transform.position-door,outward),Vector3.Dot(hung.transform.position-door,outward)+.6f,"Nam is out past Hùng, toward the yard.");
                c.enabled=true;yield return WaitGameSeconds(.3f);var lens=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null).transform;
                Assert.Less(Vector3.Dot(lens.position-door,outward),-1,"Seen from inside the house.");
                ScreenCapture.CaptureScreenshot(Folder+"/shift-mocap.png");yield return null;c.enabled=false;c.Cancel();
                // D1_03: the medicine goes to chị Lành, from his hands to hers, as Nam says his line; ông Sáu is in the inner room.
                LoadBefore("D1_03");Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_03"),0,0);var lanh=c.Actor("Chị Lành");
                Assert.AreEqual(.70f,Vector3.Distance(Flat(lanh.transform.position),Flat(m.Player.position)),.05f,"She stands before him to take it.");
                var said=(Map02DialogueCatalog.Entry[])Get("lines");Set("lineIndex",System.Array.FindIndex(said,l=>l.speaker=="Nam"));Set("sceneClock",5f);Call("LateUpdate");
                float start=(float)Get("medicineStart");Assert.GreaterOrEqual(start,0,"The hand-over starts with Nam's line.");
                Set("sceneClock",start+2.4f);Call("LateUpdate");var packet=(Transform)Get("medicineArt");var lh=lanh.Animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                {var na=m.Player.GetComponentInChildren<Animator>();var la=lanh.Animator;string P(Vector3 v)=>v.ToString("F3");
                 File.WriteAllText(Folder+"/medicine-mocap.txt","packet="+P(packet.position)+" namL="+P(na.GetBoneTransform(HumanBodyBones.LeftHand).position)+" namR="+P(na.GetBoneTransform(HumanBodyBones.RightHand).position)+" lanhL="+P(la.GetBoneTransform(HumanBodyBones.LeftHand).position)+" lanhR="+P(lh)+" namRoot="+P(m.Player.position)+" namFwd="+P(m.Player.forward)+" lanhRoot="+P(lanh.transform.position)+" lanhFwd="+P(lanh.transform.forward)+" lanhAnim="+P(la.transform.position)+" lanhScale="+P(la.transform.lossyScale)+" lanhHead="+P(la.GetBoneTransform(HumanBodyBones.Head).position)+" namHead="+P(na.GetBoneTransform(HumanBodyBones.Head).position));}
                c.enabled=true;yield return WaitGameSeconds(.25f);ScreenCapture.CaptureScreenshot(Folder+"/medicine-mocap.png");yield return null;c.enabled=false;
                Set("sceneClock",start+2.4f);Call("LateUpdate");lh=lanh.Animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                Assert.True(packet.gameObject.activeSelf);Assert.Less(Vector3.Distance(lh,packet.position),.2f,"Her hands are on the packet he holds out.");
                Set("sceneClock",start+3.6f);Call("LateUpdate");Assert.AreEqual(1,c.State.dayOne.medicineMask&1,"Given once she has it.");Assert.NotNull(c.CurrentScene,"The scene holds until the packet is hers.");c.Cancel();
                // D1_05: Nam at the stake with the two-handed mallet, chú Bảy holding the stake; a press lands a blow.
                LoadBefore("D1_05");var dock=Map02ChapterDefinition.Load().Find("D1_05");var kind=typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic);
                typeof(Map02ChapterDirector).GetMethod("OpenDay",Private).Invoke(c,new object[]{System.Enum.Parse(kind,"Dock"),dock});Call("DayLateUpdate");
                var stake=((System.Collections.Generic.List<Transform>)Get("physicalStakes"))[0];var bay=c.Actor("Chú Bảy").Animator;
                Assert.AreEqual(.95f,Vector3.Distance(Flat(m.Player.position),Flat(stake.position)),.1f,"Nam stands at the stake.");
                Assert.Less(Vector3.Distance(bay.GetBoneTransform(HumanBodyBones.LeftHand).position,stake.position+Vector3.up*(1.08f-.62f)),.2f,"Chú Bảy's hands are on the stake.");
                Call("StrikeStake");Set("strikeClock",.12f-Time.deltaTime);Call("DayLateUpdate");   // the late update adds this frame's time
                var mallet=(Transform)Get("twoHandMallet");var head=mallet.position+mallet.up*.6f;
                Assert.Less(Vector3.Distance(Flat(head),Flat(stake.position)),.3f,"The blow lands on the stake.");Assert.Less(Mathf.Abs(head.y-(stake.position.y+1.08f)),.3f);
                Set("strikeClock",5f);Call("DayLateUpdate");Assert.Greater(mallet.position.y+mallet.up.y*.6f-m.Player.position.y,1.8f,"Raised and ready between blows.");
                c.enabled=true;yield return WaitGameSeconds(.6f);ScreenCapture.CaptureScreenshot(Folder+"/stake-mocap.png");yield return null;
                // The dock is worked in the world: the camera is in on the stake, side on, and the controls are a strip.
                var lens2=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.enabled&&x.targetTexture==null).transform;
                Assert.Less(Vector3.Distance(lens2.position,stake.position),4.5f,"The camera comes in on the stake.");Assert.Greater(Vector3.Dot(lens2.forward,(stake.position-lens2.position).normalized),.8f,"The stake is in view.");
                Call("StrikeStake");yield return WaitGameSeconds(.1f);ScreenCapture.CaptureScreenshot(Folder+"/stake-blow.png");yield return null;c.enabled=false;Call("CloseDay",false);c.Cancel();
                // Laying the boards: the board in hand hangs over the step it would go on.
                LoadBefore("D1_05");c.State.dayOne.stakeHits=6;typeof(Map02ChapterDirector).GetMethod("OpenDay",Private).Invoke(c,new object[]{System.Enum.Parse(kind,"Dock"),dock});
                {var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.SetPositionAndRotation(new Vector3(stake.position.x,c.Sites["dock"].position.y,stake.position.z)-Vector3.forward*.95f,Quaternion.identity);cc.enabled=true;}
                int held=c.State.dayOne.boardOrder[0];Set("selectedBoard",held);Set("dockSlot",0);Set("dockOffset",.03f);Call("DayLateUpdate");
                var boards=(System.Collections.Generic.List<Transform>)Get("physicalBoards");
                Assert.Less(Vector3.Distance(boards[held].position,c.Sites["dock"].position+new Vector3(.03f,.36f,1.05f)),.05f,"The chosen board is held over step 1.");
                Assert.Greater(c.DockGrassHidden,0,"The landing is clear of the paddy's rice.");
                c.enabled=true;yield return WaitGameSeconds(.6f);ScreenCapture.CaptureScreenshot(Folder+"/dock-boards.png");yield return null;c.enabled=false;Call("CloseDay",false);
                Assert.Less(boards[held].position.y-c.Sites["dock"].position.y,.3f,"Put back on the stack when the job is closed.");c.Cancel();
                // The night posts: lamp hood, fence door and the firewood, each seen where it is.
                LoadBefore("N1_02");c.State.dayOne.postsReached|=1;Set("postIndex",0);var post=Map02ChapterDefinition.Load().Find("N1_02");
                {var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=c.Sites["bamboo"].position+new Vector3(-.6f,0,-.8f);cc.enabled=true;}
                foreach(var part in new[]{"Lamp","Gate","Path"})
                {
                    typeof(Map02ChapterDirector).GetMethod("OpenDay",Private).Invoke(c,new object[]{System.Enum.Parse(kind,part),post});
                    c.enabled=true;yield return WaitGameSeconds(.7f);ScreenCapture.CaptureScreenshot(Folder+"/post-"+part.ToLowerInvariant()+".png");yield return null;c.enabled=false;Call("CloseDay",false);
                }
                c.Cancel();
            }
            finally{c.Cancel();c.enabled=true;}
        }
    }
}
