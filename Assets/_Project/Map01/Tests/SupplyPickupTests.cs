using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class SupplyPickupTests:ForestSceneTestBase
    {
        [UnityTest]public IEnumerator PickupKeepsAuthoredWristAndRestoresGameplayAvatar()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return Review();yield return new ExitPlayMode();
        }
        static IEnumerator Review()
        {
            var m=Object.FindFirstObjectByType<Map01Mission>();float deadline=Time.realtimeSinceStartup+45;
            var warm=Map01InteractionWarmup.For(m);while(!warm.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(warm.Ready);
            foreach(var enemy in m.Enemies)enemy.enabled=false;
            var layout=m.GetComponent<Map01Story>().Layout;var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;
            m.player.SetPositionAndRotation(layout.chestApproach.position,layout.chestApproach.rotation);cc.enabled=true;
            layout.supplyProps.Present(true,true,false,false,m.player);
            var actor=m.player.GetComponentInChildren<Animator>();var originalAvatar=actor.avatar;var originalController=actor.runtimeAnimatorController;
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);var shoulder=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rest=actor.avatar.humanDescription.skeleton.First(b=>b.name==hand.name).rotation;
            var clip=Resources.Load<AnimationClip>("OperationsAnimation/Nam_SupplyCollect");Assert.IsFalse(clip.humanMotion,"Exact-skeleton pickup must not be retargeted through Humanoid wrist muscles.");
            var reference=Resources.Load<Avatar>("OperationsAnimation/Nam_SupplyCollectAvatar");Assert.NotNull(reference);Assert.IsTrue(reference.isValid);
            SetPreviewResolution(1920,1080);Directory.CreateDirectory("Logs/SupplyArmReview");
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.forceMatrixRecalculationPerRender=true;
            var recorder=m.gameCamera.gameObject.AddComponent<Map01OperationMovie>();recorder.Begin("supply-wrist-repair");
            float holdBend=0,maxJump=0;var previous=hand.localRotation;Vector3 min=hand.position,max=min;
            using(var session=new Map01OperationSession(m)){
                for(int frame=0;frame<=204;frame++){
                    float t=frame/30f;session.Sample(clip,t,layout.supply.position+Vector3.up*.55f);
                    layout.supplyProps.OpenLid(Mathf.SmoothStep(0,1,(t-1)/1.5f));
                    if(t>=3.3f&&t<5.4f)layout.supplyProps.HoldBag(actor);
                    if(t>=5.4f)layout.supplyProps.CarryBag(actor);
                    if(frame>0)maxJump=Mathf.Max(maxJump,Quaternion.Angle(previous,hand.localRotation));previous=hand.localRotation;
                    if(t>=3.3f&&t<=5.15f)holdBend=Mathf.Max(holdBend,Quaternion.Angle(rest,hand.localRotation));
                    min=Vector3.Min(min,hand.position);max=Vector3.Max(max,hand.position);
                    var focus=(hand.position+shoulder.position)*.5f;
                    var eye=m.player.position+m.player.forward*.3f+m.player.right*1.05f+Vector3.up*1.45f;
                    m.gameCamera.GetComponent<ThirdPersonCamera>().SetCinematicView(eye,Quaternion.LookRotation(focus-eye),58,1);
                    if(new[]{84,95,99,103,109,117,126,141,156}.Contains(frame))Capture(m,"pickup-"+frame);
                    yield return null;
                }
            }
            recorder.End();Object.Destroy(recorder);
            File.WriteAllText("Logs/SupplyArmReview/runtime-metrics.txt","Wrist during lift "+holdBend+" deg; maximum adjacent-frame change "+maxJump+" deg; hand travel extent "+(max-min).magnitude+"m");
            Assert.Less(holdBend,20);Assert.Less(maxJump,16);Assert.Greater((max-min).magnitude,.25f,"Reject a static/unbound arm.");
            Assert.AreSame(originalAvatar,actor.avatar);Assert.AreSame(originalController,actor.runtimeAnimatorController);
            Assert.IsTrue(actor.isHuman&&m.ModernCombat.enabled&&m.ModernPlayer.enabled);Assert.IsFalse(m.Cinematic);
        }
        static void Capture(Map01Mission m,string name)
        {
            var camera=m.gameCamera;var view=camera.GetComponent<ThirdPersonCamera>();
            var hand=m.player.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHand");
            var shoulder=m.player.GetComponentsInChildren<Transform>().First(t=>t.name=="RightArm");
            var focus=(hand.position+shoulder.position)*.5f;camera.transform.SetPositionAndRotation(m.player.position+m.player.forward*.3f+m.player.right*1.05f+Vector3.up*1.45f,Quaternion.LookRotation(focus-(m.player.position+m.player.forward*.3f+m.player.right*1.05f+Vector3.up*1.45f)));
            var rt=RenderTexture.GetTemporary(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/SupplyArmReview/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
        }
    }
}