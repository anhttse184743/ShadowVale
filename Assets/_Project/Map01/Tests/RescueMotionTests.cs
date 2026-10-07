using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class RescueMotionTests:ForestSceneTestBase
    {
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01Rescue R=>M.GetComponent<Map01Rescue>();
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        static void Isolate(){Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/motion-saves/"+Guid.NewGuid().ToString("N")));foreach(var g in M.Enemies)g.enabled=false;EditorSettings.asyncShaderCompilation=false;ShaderUtil.allowAsyncCompilation=false;foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))skin.forceMatrixRecalculationPerRender=true;}
        static void Place(Vector3 p){var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=p;M.player.rotation=Quaternion.identity;cc.enabled=true;Physics.SyncTransforms();}
        static float Sole(Transform root){float lowest=float.PositiveInfinity;var mesh=new Mesh();foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var v in mesh.vertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(v).y);}Object.Destroy(mesh);return lowest;}
        static float Floor(Transform root){return Physics.RaycastAll(root.position+Vector3.up*1.2f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.6f&&h.point.y<=root.position.y+.5f&&!h.transform.IsChildOf(M.player)&&!h.transform.IsChildOf(M.hung)&&h.transform.GetComponentInParent<Health>()==null).Max(h=>h.point.y);}
        [UnityTest,Timeout(180000)]public IEnumerator UntieAndRiseKeepTheRenderedSkinOnTheJetty()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            foreach(var g in R.Squad)g.GetComponent<Health>().TakeDamage(9999,g.transform.position,null);
            Place(M.hung.position-M.hung.forward*.7f);M.GetComponent<Map01Quest>().TryRescueHung();
            yield return RecordGround();
            yield return new ExitPlayMode();
        }
        static IEnumerator RecordGround()
        {
            Time.captureDeltaTime=1f/30;Directory.CreateDirectory("Logs/Rescue/ground");
            var pump=M.gameObject.AddComponent<ExtractionCapturePump>();int frame=0;var rows=new List<string>{"frame,time,phase,sole,ground,error"};float max=0;
            float until=Time.time+15;
            while(M.GetComponent<Map01Quest>().Stage==0&&Time.time<until){
                bool done=false;pump.draw=()=>{
                    var camera=M.gameCamera;var focus=M.hung.position+Vector3.up*.9f;var eye=focus+new Vector3(-3,1.2f,-2);
                    camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));
                    var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                        float sole=Sole(M.hung),ground=Floor(M.hung),error=Mathf.Abs(sole-ground);max=Mathf.Max(max,error);
                        rows.Add($"{frame},{Time.time},{R.CurrentPhase},{sole},{ground},{error}");
                        if(frame%2==0){image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes("Logs/Rescue/ground/"+frame.ToString("D4")+".png",image.EncodeToPNG());}
                    }finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
                    frame++;done=true;
                };yield return new WaitUntil(()=>done);
            }
            File.WriteAllLines("Logs/Rescue/ground/contacts.csv",rows);
            Assert.AreEqual(1,M.GetComponent<Map01Quest>().Stage);Assert.LessOrEqual(max,.02f,"Rendered knee/shoe support must stay within 2 cm through the rise.");
            Object.Destroy(pump);Time.captureDeltaTime=0;
        }
        [UnityTest]public IEnumerator DeliveredSaveDoesNotReviveGuardsOrGrantSuppliesAgain()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            foreach(var g in R.Squad)g.GetComponent<Health>().TakeDamage(9999,g.transform.position,null);
            M.GetComponent<Map01Quest>().RestoreStage(1);M.ModernCombat.Equip(WeaponKind.Knife);
            M.hung.GetComponent<NavMeshAgent>().Warp(R.Layout.shelterDoor.position);Place(R.Layout.shelterDoor.position+Vector3.left*.7f);
            yield return WaitGameSeconds(.6f);M.GetComponent<Map01RescueCinematic>().Skip();
            int count=M.GetComponent<Map01Inventory>().Count("supplies");Assert.AreEqual(0,R.Remaining);Assert.IsTrue(R.RewardDelivered);
            Map01SaveSystem.BeginGame(ForestSaveSlots.AutoSlot);yield return WaitGameSeconds(.8f);
            Assert.AreEqual(2,M.GetComponent<Map01Quest>().Stage);Assert.AreEqual(0,R.Remaining);Assert.AreEqual(count,M.GetComponent<Map01Inventory>().Count("supplies"));Assert.IsFalse(R.CompleteDelivery());
            // Unloading/destroying the mission must release its hook on a surviving player.
            var combat=M.ModernCombat;var owner=R.gameObject;M.player.SetParent(null,true);Object.DontDestroyOnLoad(M.player.gameObject);Object.Destroy(owner);yield return null;
            Assert.IsNull(combat.TrySpecialMelee);Object.Destroy(combat.gameObject);
            yield return new ExitPlayMode();
        }
        [TearDown]public void Cleanup(){Time.captureDeltaTime=0;Storage.SetValue(null,null);}
    }
}
