using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests {
public sealed class LegacyPaddleGripTests {
 [UnityTest] public IEnumerator WristOnlyMeshKeepsProceduralGrip(){
  yield return new EnterPlayMode();
  var prefab=Resources.Load<GameObject>("Characters/CommanderCandidate20261110/Commander");Assert.NotNull(prefab);
  var model=Object.Instantiate(prefab);
  try {
   var actor=model.GetComponentInChildren<Animator>();Assert.IsTrue(actor.isHuman);actor.Rebind();actor.Update(0);
   Assert.IsNull(actor.GetBoneTransform(HumanBodyBones.LeftIndexProximal));
   using(var grip=new Map01PaddleGrip(actor)) {
    Assert.IsTrue(grip.TryGrip(actor.GetBoneTransform(HumanBodyBones.LeftHand),out _));
    Assert.IsTrue(grip.TryGrip(actor.GetBoneTransform(HumanBodyBones.RightHand),out _));
    grip.Set(1,1);var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();
    int left=skin.sharedMesh.GetBlendShapeIndex("PaddleGripLeft");int right=skin.sharedMesh.GetBlendShapeIndex("PaddleGripRight");Assert.GreaterOrEqual(left,0);Assert.GreaterOrEqual(right,0);
    var deltas=new Vector3[skin.sharedMesh.vertexCount];skin.sharedMesh.GetBlendShapeFrameVertices(left,0,deltas,null,null);
    float max=0;foreach(var v in deltas)max=Mathf.Max(max,v.sqrMagnitude);Assert.Greater(max,1e-9f,"Legacy hand must still deform");
   }
  } finally {Object.Destroy(model);}
  yield return new ExitPlayMode();
 }
}
}
