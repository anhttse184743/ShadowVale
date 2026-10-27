using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests {
public sealed class PaddleGripRegressionTests:ForestSceneTestBase {
 [UnityTest]public IEnumerator ExistingGripNamesAndOtherShapeFramesSurviveRepeatedSetup(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  yield return new EnterPlayMode();yield return Run();yield return new ExitPlayMode();
 }
 static IEnumerator Run(){
  var config=Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");Assert.NotNull(config);
  var model=Object.Instantiate(config.commanderModel);var actor=model.GetComponentInChildren<Animator>();Assert.NotNull(actor);actor.Rebind();actor.Update(0);
  var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.sharedMesh!=null&&s.sharedMesh.vertexCount==Resources.Load<Mesh>("Cutscenes/Commander_Paddle_Readable").vertexCount);
  var seed=Object.Instantiate(Resources.Load<Mesh>("Cutscenes/Commander_Paddle_Readable"));seed.ClearBlendShapes();
  var zero=new Vector3[seed.vertexCount];var face=new Vector3[seed.vertexCount];face[0]=new Vector3(.001f,.002f,0);
  seed.AddBlendShapeFrame("PaddleGripLeft",100,zero,zero,zero);seed.AddBlendShapeFrame("PaddleGripRight",100,zero,zero,zero);
  seed.AddBlendShapeFrame("FaceTest",50,face,zero,zero);seed.AddBlendShapeFrame("FaceTest",100,face,zero,zero);
  skin.sharedMesh=seed;skin.SetBlendShapeWeight(0,22);skin.SetBlendShapeWeight(1,31);skin.SetBlendShapeWeight(2,43);
  try {
   for(int attempt=0;attempt<2;attempt++){
    using(var grip=new Map01PaddleGrip(actor)){
     var copy=skin.sharedMesh;Assert.AreNotSame(seed,copy);Assert.AreEqual(3,copy.blendShapeCount);
     var index=copy.GetBlendShapeIndex("FaceTest");Assert.GreaterOrEqual(index,0);Assert.AreEqual(2,copy.GetBlendShapeFrameCount(index));Assert.AreEqual(43,skin.GetBlendShapeWeight(index));
     var vertices=new Vector3[copy.vertexCount];copy.GetBlendShapeFrameVertices(index,1,vertices,null,null);Assert.AreEqual(face[0],vertices[0]);
     grip.Set(.7f,.4f);Assert.AreEqual(70,skin.GetBlendShapeWeight(copy.GetBlendShapeIndex("PaddleGripLeft")),.001f);Assert.AreEqual(40,skin.GetBlendShapeWeight(copy.GetBlendShapeIndex("PaddleGripRight")),.001f);
    }
    Assert.AreSame(seed,skin.sharedMesh);Assert.AreEqual(3,seed.blendShapeCount);
    Assert.AreEqual(22,skin.GetBlendShapeWeight(0));Assert.AreEqual(31,skin.GetBlendShapeWeight(1));Assert.AreEqual(43,skin.GetBlendShapeWeight(2));
    yield return null;
   }
  }finally{Object.Destroy(model);Object.Destroy(seed);}
 }
}
}
