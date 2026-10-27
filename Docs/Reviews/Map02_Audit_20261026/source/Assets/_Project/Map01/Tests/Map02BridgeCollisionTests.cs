using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02BridgeCollisionTests:ForestSceneTestBase
    {
        static BoxCollider[] Decks()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BoxCollider>(true)).Where(c=>c.name=="Walkable bridge deck"||c.name=="Bridge deck").ToArray();
        [Test]public void SavedSceneHasSolidSidesOnEveryBridgeWithoutDuplicateBarriers()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");var decks=Decks();Assert.GreaterOrEqual(decks.Length,2);
            foreach(var deck in decks)foreach(var name in new[]{Map02BridgeRails.Left,Map02BridgeRails.Right})
            {
                var rail=deck.transform.Find(name)?.GetComponent<BoxCollider>();Assert.NotNull(rail,deck.transform.parent.name+" "+name);Assert.IsTrue(rail.enabled);Assert.IsFalse(rail.isTrigger);
            }
            int before=decks.Sum(d=>d.GetComponentsInChildren<BoxCollider>().Length);
            Assert.AreEqual(decks.Length*2,Map02BridgeRails.Install(SceneManager.GetActiveScene()).Count);
            Assert.AreEqual(before,decks.Sum(d=>d.GetComponentsInChildren<BoxCollider>().Length));
        }
        [UnityTest,Timeout(240000)]public IEnumerator RealCharacterCannotWalkThroughRailsAndChildLoadsBesideMother()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);Physics.SyncTransforms();
            var probe=new GameObject("Bridge collision character probe");probe.layer=m.Player.gameObject.layer;var body=probe.AddComponent<CharacterController>();body.height=1.75f;body.radius=.3f;body.center=Vector3.up*.875f;body.stepOffset=.3f;
            try
            {
                foreach(var deck in Decks())
                {
                    float top=deck.center.y+deck.size.y*.5f+.04f;
                    foreach(int side in new[]{-1,1})
                    {
                        PlaceProbe(body,deck.transform.TransformPoint(new Vector3(deck.center.x,top,deck.center.z)));
                        bool stopped=false;for(int step=0;step<35;step++)stopped|=(body.Move(deck.transform.forward*(side*.15f)+Vector3.down*.03f)&CollisionFlags.Sides)!=0;
                        var local=deck.transform.InverseTransformPoint(probe.transform.position);
                        Assert.IsTrue(stopped,deck.transform.parent.name+" side "+side);
                        Assert.Less(side*(local.z-deck.center.z),deck.size.z*.5f-.15f,"Character must remain inside the rail.");
                        Assert.Greater(side*(local.z-deck.center.z),.6f,"Probe must really move toward the rail.");
                    }
                    var start=deck.transform.TransformPoint(new Vector3(deck.center.x-deck.size.x*.4f,top,deck.center.z));
                    var end=deck.transform.TransformPoint(new Vector3(deck.center.x+deck.size.x*.4f,top,deck.center.z));PlaceProbe(body,start);
                    int steps=Mathf.CeilToInt(Vector3.Distance(start,end)/.15f);for(int step=0;step<steps;step++)body.Move(deck.transform.right*.15f+Vector3.down*.03f);
                    Assert.Greater(deck.transform.InverseTransformPoint(probe.transform.position).x-deck.center.x,deck.size.x*.3f,"Bridge centre must stay passable.");
                    var path=new NavMeshPath();Assert.IsTrue(NavMesh.SamplePosition(start,out var a,2,NavMesh.AllAreas));Assert.IsTrue(NavMesh.SamplePosition(end,out var b,2,NavMesh.AllAreas));
                    Assert.IsTrue(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
                }
            }
            finally{Object.Destroy(probe);}
            var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");
            foreach(int phase in new[]{2,3})
            {
                preview.Invoke(null,new object[]{m,phase});yield return WaitGameSeconds(.3f);
                var oldSave=m.Capture();oldSave.chapter.actors.Single(a=>a.role=="Bé Tí").position=m.Chapter.Sites["radio"].position;
                m.Restore(Map02Mission.Parse(JsonUtility.ToJson(oldSave)));yield return null;
                Assert.Less(Vector3.Distance(m.Chapter.Actor("Bé Tí").transform.position,m.Chapter.Actor("Chị Lành").transform.position),2);
                Assert.IsFalse(m.Chapter.Actor("Bé Tí").Animator.GetBool("Sitting"));
            }
            var save=m.Capture();save.chapter=Through("N3_02");save.patrolCheckpoint=null;m.Restore(save);yield return null;
            Assert.Less(Vector3.Distance(m.Chapter.Actor("Bé Tí").transform.position,m.Chapter.Sites["child"].position),2,"The Night 3 lost-child task must remain playable.");
            save=m.Capture();save.chapter=Through("N3_03");m.Restore(save);yield return null;
            Assert.Less(Vector3.Distance(m.Chapter.Actor("Bé Tí").transform.position,m.Chapter.Actor("Chị Lành").transform.position),2);
            yield return new ExitPlayMode();
        }
        static void PlaceProbe(CharacterController body,Vector3 position){body.enabled=false;body.transform.position=position;body.enabled=true;Physics.SyncTransforms();}
        static Map02ChapterState Through(string id){var state=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(state.Complete(task.id),task.id);if(task.id==id)break;}return state;}
    }
}
