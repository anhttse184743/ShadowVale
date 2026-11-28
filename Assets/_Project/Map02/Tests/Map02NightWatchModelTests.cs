using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02NightWatchModelTests : ForestSceneTestBase
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Folder = "Logs/NightWatchModels_20261123";
        [UnityTest, Timeout(240000)]
        public IEnumerator BlenderModelsKeepFrameFixedAndShowRealBoltAndShield()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity"); SetPreviewResolution(1600,900);
            yield return new EnterPlayMode();
            Map02Mission mission = null; float deadline = Time.realtimeSinceStartup + 90;
            while(Time.realtimeSinceStartup < deadline)
            {
                mission = Object.FindFirstObjectByType<Map02Mission>(); Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                if(mission?.Ready == true && mission.Chapter != null) break; yield return null;
            }
            Assert.NotNull(mission?.Chapter); var chapter = mission.Chapter;
            object Call(string name, params object[] args) => typeof(Map02ChapterDirector).GetMethod(name, Private).Invoke(chapter,args);
            T Get<T>(string name) => (T)typeof(Map02ChapterDirector).GetField(name,Private).GetValue(chapter);
            IEnumerator Photo(string name){ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return null;yield return null;}
            try
            {
                Directory.CreateDirectory(Folder); chapter.Cancel();
                var save = mission.Capture(); save.chapter = new Map02ChapterState();
                foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id=="N1_02")break;save.chapter.Complete(task.id);}
                save.chapter.dayOne.arrivalSeen=true;mission.Restore(save);chapter.Track("N1_02");
                var gate = Get<List<Transform>>("postGates")[0].GetComponent<Map02NightWatchVisual>();
                var lamp = Get<List<Transform>>("postLamps")[0].GetComponent<Map02NightWatchVisual>();
                var perimeter=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Continuous village boundary fence")).ToArray();
                Assert.IsNotEmpty(perimeter,"The village keeps an actual perimeter while leaving routes open");
                Assert.Greater(perimeter.Max(t=>t.position.z)-perimeter.Min(t=>t.position.z),200,"The long boundary continues beyond both ends of the village view");
                Assert.Less(perimeter.Min(t=>t.position.x),-110,"The western boundary stays outside the houses and lanes");Assert.NotNull(gate.gateLeaf);Assert.NotNull(gate.bolt);Assert.NotNull(lamp.hood);
                var nodes=gate.GetComponentsInChildren<Transform>();
                var fixedFrame=nodes.First(t=>t.name=="GateFrame");var position=fixedFrame.position;var rotation=fixedFrame.rotation;
                var receiver=nodes.First(t=>t.name=="ReceiverPlate");
                var post=chapter.State.dayOne.posts[0];post.gateClosed=post.latchSeated=false;post.lampAngle=0;Call("SyncDayProps");
                yield return WaitGameSeconds(.8f);
                var kind=typeof(Map02ChapterDirector).GetNestedType("DayActivity",BindingFlags.NonPublic);
                Call("OpenDay",Enum.Parse(kind,"Gate"),Map02ChapterDefinition.Load().Find("N1_02"));Call("FinishDayHelp");
                yield return WaitGameSeconds(.7f);yield return Photo("gate-open");
                Call("ClosePostGate");yield return WaitGameSeconds(.8f);
                Assert.Less(Vector3.Distance(position,fixedFrame.position),.001f,"Only the leaf moves, never the fence and posts");
                Assert.Less(Quaternion.Angle(rotation,fixedFrame.rotation),.1f);Assert.Less(Mathf.Abs(gate.LeafAngle),.5f);
                var navigation=new UnityEngine.AI.NavMeshPath();
                Assert.True(UnityEngine.AI.NavMesh.SamplePosition(lamp.InteractionPosition,out var lampStand,2,UnityEngine.AI.NavMesh.AllAreas));
                Assert.True(UnityEngine.AI.NavMesh.SamplePosition(gate.InteractionPosition,out var gateStand,2,UnityEngine.AI.NavMesh.AllAreas));
                Assert.True(UnityEngine.AI.NavMesh.CalculatePath(lampStand.position,gateStand.position,UnityEngine.AI.NavMesh.AllAreas,navigation));
                Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete,navigation.status);
                float walkLength=0;for(int i=1;i<navigation.corners.Length;i++)walkLength+=Vector3.Distance(navigation.corners[i-1],navigation.corners[i]);
                Assert.Less(walkLength,8,"The player inspects the closed gate from inside; never route around the fence to its outside");
                var retracted=gate.bolt.position;yield return Photo("gate-closed-unlatched");
                Call("LatchPostGate");yield return WaitGameSeconds(.8f);
                var advance=gate.bolt.position-retracted;
                File.WriteAllText(Folder+"/model-metrics.txt",$"gateRoot={gate.transform.rotation.eulerAngles} leafLocal={gate.gateLeaf.localRotation.eulerAngles} boltAdvance={advance} receiver={receiver.position} bolt={gate.bolt.position}\n");
                Assert.Greater(Vector3.Dot(advance,receiver.position-retracted),0,"The bolt slides toward its receiver");
                Assert.AreEqual(.25f,advance.magnitude,.01f);Assert.True(gate.Settled);
                yield return Photo("gate-latched");Call("CloseDay",false);
                Call("OpenDay",Enum.Parse(kind,"Lamp"),Map02ChapterDefinition.Load().Find("N1_02"));Call("FinishDayHelp");
                yield return WaitGameSeconds(1.0f);
                var outside=Vector3.ProjectOnPlane(lamp.beam.transform.forward,Vector3.up).normalized;
                yield return Photo("lamp-outward");
                Call("TurnPostLamp",180f);yield return WaitGameSeconds(1.5f);
                var inside=Vector3.ProjectOnPlane(lamp.beam.transform.forward,Vector3.up).normalized;
                Assert.Less(Vector3.Dot(outside,inside),-.99f,"The opening and real spot light reverse together");
                Assert.True(post.LampSafe);yield return Photo("lamp-inward");
                Assert.NotNull(Get<Map02NightWatchPreview>("nightPropPreview").Image);                Call("TurnPostLamp",-post.lampAngle);yield return WaitGameSeconds(1.5f);
                var rootRotation=lamp.transform.rotation;var rootPosition=lamp.transform.position;
                UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(220,500)});yield return null;yield return null;
                float largestStep=0;var previous=lamp.hood.rotation;
                for(int step=1;step<=60;step++){
                    UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=new Vector2(220+500f*step/60,500),delta=new Vector2(500f/60,0)});
                    yield return null;largestStep=Mathf.Max(largestStep,Quaternion.Angle(previous,lamp.hood.rotation));previous=lamp.hood.rotation;
                    if(step==30){yield return Photo("lamp-drag-mid");previous=lamp.hood.rotation;}
                }
                UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=new Vector2(720,500)});yield return null;yield return WaitGameSeconds(1.5f);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(post.lampAngle,180)),.5f,"Native mouse dragging changes the physical hood target continuously");
                Assert.Less(Vector3.Distance(rootPosition,lamp.transform.position),.001f);Assert.Less(Quaternion.Angle(rootRotation,lamp.transform.rotation),.01f,"The lamp body stays planted");
                Assert.Less(largestStep,20,"No stepped image rotation during dragging");yield return Photo("lamp-drag-finished");
                Call("CloseDay",false);
                var rig=typeof(Map02ChapterDirector).GetField("rig",Private).GetValue(chapter);
                foreach(int index in new[]{0,1}){
                    var worldGate=Get<List<Transform>>("postGates")[index];
                    var eye=worldGate.position+worldGate.rotation*new Vector3(3.3f,2.3f,-5.6f);var focus=worldGate.position+Vector3.up*.8f;
                    rig.GetType().GetMethod("SetCinematicView").Invoke(rig,new object[]{eye,Quaternion.LookRotation(focus-eye),52f,1f});
                    yield return WaitGameSeconds(.4f);yield return Photo("world-gate-"+index);
                }
                rig.GetType().GetMethod("ClearCinematicView").Invoke(rig,null);
            }
            finally { chapter.Cancel(); }
            yield return new ExitPlayMode();
        }
    }
}
