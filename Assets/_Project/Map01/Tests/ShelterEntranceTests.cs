using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ShelterEntranceTests : ForestSceneTestBase
    {
        [Test] public void EntranceHasContinuousWalkableFloorAndSealsBothThresholdSides()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/LayoutTest/Prefabs/ShelterShell.prefab");
            var shelter=Object.Instantiate(prefab);shelter.transform.position=new Vector3(2000,.12f,2000);
            try {
                var entrance=shelter.transform.Find("Natural entrance").GetComponent<MeshCollider>();Physics.SyncTransforms();
                foreach(float x in new[]{-1.3f,-.65f,0,.65f,1.3f}) {
                    float previous=float.NaN;
                    for(float z=-6.3f;z>=-22;z-=.25f) {
                        var origin=shelter.transform.TransformPoint(new Vector3(x,20,z));
                        Assert.IsTrue(entrance.Raycast(new Ray(origin,Vector3.down),out var hit,40),$"Floor gap at {x},{z}");
                        Assert.Greater(hit.normal.y,.7f,$"Walkway must not contain a steep step at {x},{z}");
                        if(!float.IsNaN(previous))Assert.Less(Mathf.Abs(hit.point.y-previous),.22f);
                        previous=hit.point.y;
                    }
                }
                foreach(float x in new[]{-1.53f,1.53f}) {
                    var origin=shelter.transform.TransformPoint(new Vector3(x,.65f,-5.5f));
                    Assert.IsTrue(entrance.Raycast(new Ray(origin,Vector3.back),out _,3),"The old triangular doorway gaps must be filled with earth.");
                }
            }finally{Object.DestroyImmediate(shelter);}
        }

        [Test] public void EmbeddedRocksLeaveTheCentralPassageClear()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/LayoutTest/Prefabs/ShelterShell.prefab");
            var shelter=Object.Instantiate(prefab);shelter.transform.position=new Vector3(2000,.12f,2000);
            try {
                var entrance=shelter.transform.Find("Natural entrance");Assert.AreEqual(18,entrance.childCount);
                foreach(Transform rock in entrance) {
                    Assert.Greater(Mathf.Abs(rock.localPosition.x),1.65f);
                    Assert.IsNotEmpty(rock.GetComponentsInChildren<Collider>());
                    foreach(var renderer in rock.GetComponentsInChildren<Renderer>())
                        Assert.IsFalse(renderer.bounds.Intersects(new Bounds(new Vector3(2000,renderer.bounds.center.y,renderer.bounds.center.z),new Vector3(2.1f,.1f,.1f))),"Rocks must not obstruct the middle of the path.");
                }
            }finally{Object.DestroyImmediate(shelter);}
        }

        [UnityTest] public IEnumerator PlayerWalksDownAndBackUpThePhysicalRamp()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var mission=Object.FindFirstObjectByType<Map01Mission>();foreach(var enemy in mission.Enemies)enemy.enabled=false;
            mission.ModernPlayer.enabled=false;mission.ModernCombat.enabled=false;mission.Cinematic=false;mission.ModernHealth.CinematicInvulnerable=true;
            var character=mission.player.GetComponent<CharacterController>();character.enabled=false;
            var entry=GameObject.Find("Natural entrance").GetComponent<Collider>();
            Assert.IsTrue(entry.Raycast(new Ray(new Vector3(-65,30,-84),Vector3.down),out var start,50));
            mission.player.position=start.point+Vector3.up*.04f;character.enabled=true;Physics.SyncTransforms();
            foreach(var target in new[]{new Vector3(-65,.18f,-67),start.point}) {
                float deadline=Time.time+20;
                while(Vector3.ProjectOnPlane(target-mission.player.position,Vector3.up).magnitude>.15f&&Time.time<deadline) {
                    var delta=Vector3.ProjectOnPlane(target-mission.player.position,Vector3.up);
                    character.Move(Vector3.ClampMagnitude(delta,2.4f*Time.deltaTime)+Vector3.down*(5*Time.deltaTime));
                    Assert.Greater(mission.player.position.y,-.2f,"The player must not fall through the threshold.");
                    yield return null;
                }
                Assert.Less(Vector3.ProjectOnPlane(target-mission.player.position,Vector3.up).magnitude,.2f,"Doorway and rocks must not block walking.");
                Assert.Less(Mathf.Abs(mission.player.position.y-target.y),.3f);
            }
            yield return new ExitPlayMode();
        }
    }
}
