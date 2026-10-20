using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ShadowVale.Map01;
using Object=UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    [InitializeOnLoad]
    public static partial class WeaponAnimationReviewTools
    {
        public const string Work="Docs/Reviews/WeaponAnimation_20261016";
        public const string Root="Assets/_Project/Art/WeaponAnimationReview_20261016";
        const string Player="Assets/_Project/Prefabs/Player/Player.prefab";
        static double next;
        static WeaponAnimationReviewTools(){Directory.CreateDirectory(Work);File.WriteAllText(Work+"/tool_version.txt","8");EditorApplication.update+=Poll;AssemblyReloadEvents.beforeAssemblyReload+=ReleasePreviewGraphs;EditorApplication.delayCall+=ConfigurePreviewStartup;}
        static void ReleasePreviewGraphs(){foreach(var d in Object.FindObjectsByType<WeaponAnimationPreview>(FindObjectsInactive.Include,FindObjectsSortMode.None))d.ReleaseGraph();}
        static void ConfigurePreviewStartup(){if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==Preview)EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Preview);}
        static void Poll()
        {
            if(asyncRunning||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            var request=Work+"/request.txt";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var action=File.ReadAllText(request).Trim().TrimStart('\uFEFF');File.Move(request,request+"."+DateTime.UtcNow.Ticks+".consumed");
            EditorApplication.delayCall+=()=>{try{File.WriteAllText(Work+"/status.txt","RUNNING "+action);Dispatch(action);if(!asyncRunning)File.WriteAllText(Work+"/status.txt","COMPLETE "+action);}catch(Exception e){File.WriteAllText(Work+"/status.txt","FAILED "+e);Debug.LogException(e);}};
        }
        static void Dispatch(string action){if(action=="AUDIT")Audit();else if(action=="BUILD")BuildReview();else if(action=="RENDER")RenderReview();else if(action=="VIDEO")VideoFrames();else if(action=="OPEN")OpenPreview();else if(action=="PLAY_PROOF")BeginProof();else if(action=="REFRESH")AssetDatabase.Refresh();else throw new Exception("Unknown weapon review action: "+action);}
        static string V(Vector3 p)=>p.ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
        static void Audit()
        {
            var scene=EditorSceneManager.NewPreviewScene();GameObject nam=null;
            try{
                nam=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(nam,scene);
                foreach(var b in nam.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                var a=nam.GetComponentInChildren<Animator>();var text=new StringBuilder();text.AppendLine("Unity "+Application.unityVersion+" human="+a.isHuman+" scale="+a.humanScale);
                foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.RightHand,HumanBodyBones.LeftHand,HumanBodyBones.RightLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightIndexProximal,HumanBodyBones.LeftFoot}){
                    var t=a.GetBoneTransform(bone);text.AppendLine(bone+" "+(t==null?"NOT MAPPED":t.name+" pos="+V(t.position)+" euler="+V(t.eulerAngles)+" scale="+V(t.lossyScale)));}
                foreach(var clip in a.runtimeAnimatorController.animationClips.Distinct())text.AppendLine("CLIP "+clip.name+" seconds="+clip.length+" path="+AssetDatabase.GetAssetPath(clip));
                foreach(var id in new[]{"pistol_k54","rifle_mosin","grenade_f1","smoke_bomb"}){
                    var go=Object.Instantiate(Map01ItemCatalog.Load().Model(id));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    text.AppendLine("ITEM "+id+" scale="+V(go.transform.localScale));
                    foreach(var t in go.GetComponentsInChildren<Transform>())text.AppendLine("PART "+t.name+" local="+V(t.localPosition)+" rotation="+V(t.localEulerAngles)+" scale="+V(t.localScale));
                    foreach(var r in go.GetComponentsInChildren<Renderer>())text.AppendLine("BOUNDS "+r.name+" center="+V(r.bounds.center)+" size="+V(r.bounds.size));
                    Object.DestroyImmediate(go);
                }
                var source=a.runtimeAnimatorController.animationClips.First(c=>c.name=="Nam_Rifle_Idle");
                text.AppendLine("BINDINGS "+string.Join(";",AnimationUtility.GetCurveBindings(source).Take(35).Select(b=>b.path+":"+b.propertyName)));
                File.WriteAllText(Work+"/audit.txt",text.ToString());
            }finally{if(nam!=null)Object.DestroyImmediate(nam);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
