using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class GuardApproachTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        [UnityTest]public IEnumerator RecordOverseerContacts()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var mode=pipeline.GetType().GetProperty("gpuResidentDrawerMode");var drawer=mode.GetValue(pipeline);mode.SetValue(pipeline,System.Enum.ToObject(mode.PropertyType,0));
            yield return null;yield return null;
            EditorSettings.asyncShaderCompilation=false;ShaderUtil.allowAsyncCompilation=false;
            foreach(var g in M.Enemies)g.enabled=false;
            // Manual camera renders do not update GPU resident instance transforms reliably.
            // Exclude meshes from the drawer for this capture only; production settings stay intact.
            var block=new MaterialPropertyBlock();block.SetFloat("_GuardCapture",1);
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))r.SetPropertyBlock(block);
            yield return WaitGameSeconds(.5f);
            yield return CaptureGuard();
            var guard=M.GetComponent<Map01Rescue>().Squad[0];var actor=guard.GetComponentInChildren<Animator>();
            actor.Play("Aim",0,0);yield return WaitGameSeconds(.3f);yield return CaptureGuard("aim");
            Assert.IsTrue(M.GetComponent<Map01Rescue>().Squad[0].GetComponentInChildren<Map01OverseerGrip>().Calibrated);
            Assert.Less(M.GetComponent<Map01Rescue>().Squad[0].GetComponentInChildren<Map01OverseerGrip>().PalmError,.025f);
            mode.SetValue(pipeline,drawer);yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator CrouchedRearApproachGivesTimeForOneKnifeKill()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var rescue=M.GetComponent<Map01Rescue>();var guard=rescue.Squad[0];
            foreach(var g in M.Enemies)g.enabled=g==guard;
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;
            M.player.SetPositionAndRotation(guard.transform.position-guard.transform.forward*1.45f,guard.transform.rotation);cc.enabled=true;
            M.ModernPlayer.RestoreMotion(true);M.Crouched=true;M.ModernCombat.Equip(ShadowVale.Gameplay.Combat.WeaponKind.Knife);
            Physics.SyncTransforms();
            Assert.IsFalse(guard.Hear(guard.transform.position-guard.transform.forward*1.8f,3,Map01NoiseKind.Footstep));
            Assert.IsTrue(guard.Hear(M.player.position,3,Map01NoiseKind.Footstep));
            Assert.IsFalse(guard.Alerted);yield return WaitGameSeconds(.45f);
            Assert.IsFalse(guard.Alerted);Assert.IsFalse(rescue.Failed);Assert.IsTrue(guard.CanSilentTakedown());
            Assert.IsTrue(guard.TrySilentTakedown());Assert.IsFalse(guard.Alive);Assert.IsFalse(rescue.OverseerAlive);
            yield return WaitGameSeconds(5);Assert.IsFalse(M.Cinematic);Assert.IsFalse(rescue.Failed);
            yield return new ExitPlayMode();
        }
        static IEnumerator CaptureGuard(string prefix="guard")
        {
            var guard=M.GetComponent<Map01Rescue>().Squad[0];var actor=guard.GetComponentInChildren<Animator>();var gun=actor.GetComponent<Map01Rifle>();
            Directory.CreateDirectory("Logs/GuardApproach");
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=guard.transform.position-guard.transform.forward*3;M.ModernPlayer.enabled=false;M.ModernCombat.enabled=false;
            var camera=M.gameCamera;camera.useOcclusionCulling=false;camera.cullingMask=~0;camera.layerCullDistances=new float[32];var focus=guard.transform.position+Vector3.up*1.05f;
            string data="";
            foreach(var id in new[]{HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand}){
                var bone=actor.GetBoneTransform(id);data+=id+" pos="+guard.transform.InverseTransformPoint(bone.position).ToString("F4")+" rot="+bone.rotation.eulerAngles+"\n";
            }
            data+="Gun local="+gun.weapon.transform.localPosition+" Euler="+gun.weapon.transform.localEulerAngles+" worldForward="+guard.transform.InverseTransformDirection(gun.weapon.transform.forward)+"\n";
            data+="Support="+guard.transform.InverseTransformPoint(gun.support.position)+" Muzzle="+guard.transform.InverseTransformPoint(gun.weapon.Muzzle.position)+"\n";
            foreach(Transform child in gun.weapon.GetComponentsInChildren<Transform>())data+=child.name+" "+child.localPosition+" "+child.localEulerAngles+"\n";
            foreach(var r in gun.weapon.GetComponentsInChildren<Renderer>(true))data+="RENDER "+r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" off="+r.forceRenderingOff+" bounds="+r.bounds+" localCenter="+gun.weapon.transform.InverseTransformPoint(r.bounds.center)+" scale="+r.transform.lossyScale+" shader="+r.sharedMaterial.shader.name+"\n";
            File.WriteAllText("Logs/GuardApproach/contacts.txt",data);
            for(int view=0;view<2;view++){
                var eye=focus+guard.transform.forward*2.8f+guard.transform.right*(view==0?.6f:2.8f)+Vector3.up*.3f;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));camera.fieldOfView=46;
                camera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().SetCinematicView(eye,Quaternion.LookRotation(focus-eye),46,1);
                yield return Draw("Logs/GuardApproach/"+prefix+"-"+view+".png");
            }
        }
        static IEnumerator Draw(string path)
        {
            var pump=M.gameObject.AddComponent<ExtractionCapturePump>();bool ready=false;
            pump.draw=()=>{
                var camera=M.gameCamera;var rt=RenderTexture.GetTemporary(960,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);
                try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
                finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(pixels);}ready=true;
            };yield return new WaitUntil(()=>ready);Object.Destroy(pump);
        }
    }
}
