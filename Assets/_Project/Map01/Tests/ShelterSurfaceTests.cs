using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class ShelterSurfaceTests
    {
        private GameObject shelter;
        [SetUp] public void Setup()
        {
            shelter=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/LayoutTest/Prefabs/ShelterShell.prefab"));
            shelter.transform.position=new Vector3(2000,.12f,2000);Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(shelter);
        [Test] public void RoofMatchesGroundHeightAndCoversEntireTerrainOpening()
        {
            var filter=shelter.transform.Find("Ground roof surface").GetComponent<MeshFilter>();
            Assert.AreEqual(new Vector3(16,0,14),filter.sharedMesh.bounds.size);
            foreach(var vertex in filter.sharedMesh.vertices)Assert.AreEqual(4.2f,filter.transform.TransformPoint(vertex).y,.0001f);
            foreach(var x in new[]{-7.95f,0,7.95f})foreach(var z in new[]{-6.95f,0,6.95f}) {
                Assert.IsTrue(Physics.Raycast(shelter.transform.position+new Vector3(x,10,z),Vector3.down,out var hit,10));
                Assert.AreEqual(4.2f,hit.point.y,.001f);
            }
        }
        [Test] public void PortalReturnsCloseSideGapsAndLeavePassageOpen()
        {
            foreach(float x in new[]{-1.7f,1.7f})
                Assert.IsTrue(Physics.Raycast(shelter.transform.position+new Vector3(x,1.6f,-7.2f),Vector3.forward,1.7f));
            Assert.IsFalse(Physics.SphereCast(shelter.transform.position+new Vector3(0,1.2f,-7.2f),.35f,Vector3.forward,out _,2f));
        }
        [Test] public void RoofSlabOverlapsWallTopsAndRetainsDoorClearance()
        {
            var roof=shelter.transform.Find("Earth covered roof").GetComponent<BoxCollider>().bounds;
            var wall=shelter.transform.Find("Side retaining wall").GetComponent<BoxCollider>().bounds;
            Assert.Less(roof.min.y,wall.max.y);
            Assert.IsFalse(Physics.Raycast(shelter.transform.position+new Vector3(0,2.55f,-7.2f),Vector3.forward,2));
        }
    }
}
