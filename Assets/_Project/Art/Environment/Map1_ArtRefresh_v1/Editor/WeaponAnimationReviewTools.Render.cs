using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace ShadowVale.EnvironmentReview
{
    public static partial class WeaponAnimationReviewTools
    {
        static bool asyncRunning;
        sealed class Shot {public int take;public float time;public string path;}
        static List<Shot> shots;static int shotIndex,renderPhase;static double renderAfter;
        static WeaponAnimationPreview renderDriver;static RenderTexture renderTarget;static Texture2D renderPixels;
        static object oldDrawer;static System.Reflection.PropertyInfo drawerMode;static UnityEngine.Rendering.RenderPipelineAsset renderPipeline;
        static string renderAction;
        static List<string> contactRows,geometryRows;
        static void RenderReview()=>BeginRender(false);
        static void VideoFrames()=>BeginRender(true);
        static void BeginRender(bool video)
        {
            CheckClean();if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=Preview)EditorSceneManager.OpenScene(Preview);
            renderDriver=UnityEngine.Object.FindFirstObjectByType<WeaponAnimationPreview>();shots=new List<Shot>();renderAction=video?"VIDEO":"RENDER";
            contactRows=new List<string>{"item,action,time_s,left_support_error_m"};geometryRows=new List<string>();
            if(!video){
                foreach(var s in new[]{("pistol_k54","aim",.5f),("pistol_k54","reload_empty",.30f),("rifle_mosin","aim",.5f),("rifle_mosin","bolt",.43f),("rifle_mosin","reload",.48f),("grenade_f1","throw",.22f),("grenade_f1","throw",.58f),("smoke_bomb","throw",.23f),("rifle_standard","reload",.30f),("knife","carry",.3f)}){
                    int n=Array.FindIndex(renderDriver.takes,t=>t.item==s.Item1&&t.action==s.Item2);if(n<0)throw new Exception("Missing take "+s);
                    shots.Add(new Shot{take=n,time=renderDriver.takes[n].clip.length*s.Item3,path=Work+"/Previews/"+s.Item1+"_"+s.Item2+"_"+Mathf.RoundToInt(s.Item3*100)+".png"});
                }
            }else{
                var sequences=new[]{new[]{("pistol_k54","draw"),("pistol_k54","aim"),("pistol_k54","fire"),("pistol_k54","reload_empty"),("pistol_k54","holster")},new[]{("rifle_mosin","aim"),("rifle_mosin","fire"),("rifle_mosin","bolt"),("rifle_mosin","reload")},new[]{("grenade_f1","throw"),("smoke_bomb","throw"),("grenade_f1","crouch_throw")}};var names=new[]{"K54","Mosin","Throwables"};
                for(int i=0;i<sequences.Length;i++){int frame=0;string folder=Work+"/VideoFrames/"+names[i];Directory.CreateDirectory(folder);var timeline=new List<string>();
                    foreach(var (item,action) in sequences[i]){int n=Array.FindIndex(renderDriver.takes,t=>t.item==item&&t.action==action);timeline.Add(frame+" "+item+" "+action);
                        for(int f=0;f<Mathf.CeilToInt(renderDriver.takes[n].clip.length*24);f++)shots.Add(new Shot{take=n,time=f/24f,path=folder+"/"+(frame++).ToString("D5")+".png"});}
                    File.WriteAllLines(folder+"/timeline.txt",timeline);
                }
            }
            foreach(var s in shots)Directory.CreateDirectory(Path.GetDirectoryName(s.path));
            renderPipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;drawerMode=renderPipeline?.GetType().GetProperty("gpuResidentDrawerMode");oldDrawer=drawerMode?.GetValue(renderPipeline);if(drawerMode!=null)drawerMode.SetValue(renderPipeline,Enum.ToObject(drawerMode.PropertyType,0));
            renderTarget=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);renderTarget.Create();renderPixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            shotIndex=0;renderPhase=0;asyncRunning=true;renderAfter=EditorApplication.timeSinceStartup+.4;EditorApplication.update+=RenderTick;
        }
        static void RenderTick()
        {
            if(EditorApplication.timeSinceStartup<renderAfter)return;
            try{
                if(shotIndex>=shots.Count){EndRender(null);return;}
                var shot=shots[shotIndex];
                if(renderPhase==0){renderDriver.Sample(shot.take,shot.time);renderDriver.View(2);renderPhase=1;renderAfter=EditorApplication.timeSinceStartup+.09;EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();return;}
                if(renderPhase==1){renderDriver.Sample(shot.take,shot.time);renderPhase=2;renderAfter=EditorApplication.timeSinceStartup+.05;EditorApplication.QueuePlayerLoopUpdate();return;}
                var camera=renderDriver.reviewCamera;var previous=RenderTexture.active;camera.targetTexture=renderTarget;camera.Render();RenderTexture.active=renderTarget;renderPixels.ReadPixels(new Rect(0,0,1920,1080),0,0);renderPixels.Apply(false);File.WriteAllBytes(shot.path,renderPixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;
                var p=renderDriver.CurrentProp;var take=renderDriver.Current;var h=renderDriver.actor.GetBoneTransform(HumanBodyBones.LeftHand);
                if((p.item=="pistol_k54"&&take.action=="aim")||p.item=="rifle_mosin")contactRows.Add(p.item+","+take.action+","+shot.time.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+Vector3.Distance(h.TransformPoint(renderDriver.palmOffset),p.root.TransformPoint(p.support)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                geometryRows.Add(p.item+" "+take.action+" actor="+V(renderDriver.actor.transform.position)+" hips="+V(renderDriver.actor.GetBoneTransform(HumanBodyBones.Hips).position)+" head="+V(renderDriver.actor.GetBoneTransform(HumanBodyBones.Head).position));
                if(shotIndex==0)geometryRows.AddRange(renderDriver.actor.GetComponentsInChildren<Renderer>(true).Select(r=>r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" forcedOff="+r.forceRenderingOff+" center="+V(r.bounds.center)+" size="+V(r.bounds.size)));
                File.WriteAllText(Work+"/status.txt","RUNNING "+renderAction+" "+(++shotIndex)+"/"+shots.Count);renderPhase=0;renderAfter=EditorApplication.timeSinceStartup+.01;
            }catch(Exception e){EndRender(e);}
        }
        static void EndRender(Exception failure)
        {
            EditorApplication.update-=RenderTick;asyncRunning=false;
            if(renderDriver!=null){renderDriver.reviewCamera.targetTexture=null;renderDriver.Sample(0,0);renderDriver.View(0);}
            if(renderTarget!=null){renderTarget.Release();UnityEngine.Object.DestroyImmediate(renderTarget);}if(renderPixels!=null)UnityEngine.Object.DestroyImmediate(renderPixels);
            if(drawerMode!=null)drawerMode.SetValue(renderPipeline,oldDrawer);
            File.WriteAllLines(Work+"/contact_metrics.csv",contactRows);File.WriteAllLines(Work+"/render_geometry.txt",geometryRows);
            if(failure==null){EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());File.WriteAllText(Work+"/status.txt","COMPLETE "+renderAction+" "+shots.Count+" frames");}else{File.WriteAllText(Work+"/status.txt","FAILED "+failure);Debug.LogException(failure);}
        }
    }
}
