using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace ShadowVale.EnvironmentReview
{
    public static partial class NativeWeaponPreviewTools
    {
        sealed class Shot {public string item,action;public float u;}
        static readonly Shot[] CaptureShots={new Shot{item="pistol_k54",action="aim",u=.5f},new Shot{item="rifle_mosin",action="aim",u=.5f},new Shot{item="rifle_mosin",action="bolt",u=.46f},new Shot{item="grenade_f1",action="throw",u=.22f},new Shot{item="grenade_f1",action="throw",u=.60f},new Shot{item="smoke_bomb",action="ready",u=.4f},new Shot{item="ak47",action="reload",u=.46f},new Shot{item="knife",action="strike",u=.55f}};
        static NativeWeaponPreviewPlayer capturePlayer;static int captureIndex,capturePhase;static double captureAfter;
        static RenderTexture captureTarget;static Texture2D capturePixels;static RenderPipelineState pipelineState;
        sealed class RenderPipelineState {
            public UnityEngine.Rendering.RenderPipelineAsset pipeline;public System.Reflection.PropertyInfo mode;public object previous;
            public void Disable(){pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;mode=pipeline?.GetType().GetProperty("gpuResidentDrawerMode");previous=mode?.GetValue(pipeline);if(mode!=null)mode.SetValue(pipeline,Enum.ToObject(mode.PropertyType,0));}
            public void Restore(){if(mode!=null)mode.SetValue(pipeline,previous);}
        }
        static void BeginRender()
        {
            Open();capturePlayer=Object.FindFirstObjectByType<NativeWeaponPreviewPlayer>();capturePlayer.playing=false;Directory.CreateDirectory(Work+"/Previews");
            pipelineState=new RenderPipelineState();pipelineState.Disable();captureTarget=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);captureTarget.Create();capturePixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            captureIndex=0;capturePhase=0;captureAfter=EditorApplication.timeSinceStartup+.3;busy=true;EditorApplication.update+=CaptureTick;
        }
        static void CaptureTick()
        {
            if(EditorApplication.timeSinceStartup<captureAfter)return;
            try{
                if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Another operation changed the scene; stopped preview capture");
                if(captureIndex>=CaptureShots.Length){EndCapture(null);return;}
                var shot=CaptureShots[captureIndex];int index=Array.FindIndex(capturePlayer.takes,t=>t.item==shot.item&&t.action==shot.action);
                if(index<0)throw new Exception("Missing native take "+shot.item+" "+shot.action);
                if(capturePhase<2){capturePlayer.Sample(index,capturePlayer.takes[index].clip.length*shot.u);capturePlayer.View(shot.action=="strike"?0:2);capturePhase++;captureAfter=EditorApplication.timeSinceStartup+.08;EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();return;}
                var camera=capturePlayer.reviewCamera;var previous=RenderTexture.active;camera.targetTexture=captureTarget;camera.Render();RenderTexture.active=captureTarget;capturePixels.ReadPixels(new Rect(0,0,1920,1080),0,0);capturePixels.Apply(false);
                File.WriteAllBytes(Work+"/Previews/"+shot.item+"_"+shot.action+"_"+Mathf.RoundToInt(shot.u*100)+".png",capturePixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;
                File.WriteAllText(Work+"/status.txt","RUNNING RENDER "+(++captureIndex)+"/"+CaptureShots.Length);capturePhase=0;captureAfter=EditorApplication.timeSinceStartup+.02;
            }catch(Exception e){EndCapture(e);}
        }
        static void EndCapture(Exception failure)
        {
            EditorApplication.update-=CaptureTick;busy=false;
            if(capturePlayer!=null){capturePlayer.reviewCamera.targetTexture=null;capturePlayer.playing=true;int initial=Array.FindIndex(capturePlayer.takes,t=>t.item=="pistol_k54"&&t.action=="aim");capturePlayer.Sample(initial,capturePlayer.takes[initial].clip.length*.5f);capturePlayer.View(2);}
            if(captureTarget!=null){captureTarget.Release();Object.DestroyImmediate(captureTarget);}if(capturePixels!=null)Object.DestroyImmediate(capturePixels);pipelineState?.Restore();
            if(failure==null){EditorSceneManager.SaveScene(SceneManager.GetActiveScene());capturePlayer.Release();File.WriteAllText(Work+"/status.txt","COMPLETE RENDER "+CaptureShots.Length+" Full HD frames");}else{File.WriteAllText(Work+"/status.txt","FAILED "+failure);Debug.LogException(failure);}
        }
        const string ProofKey="ShadowVale.NativePreview.Proof";static double proofDeadline,proofStableAfter;static int proofStep;static readonly List<string> proofRows=new List<string>();
        [InitializeOnLoadMethod]static void InstallProof(){EditorApplication.playModeStateChanged+=ProofChanged;}
        static void BeginProof(){Open();SessionState.SetBool(ProofKey,true);EditorApplication.isPlaying=true;busy=true;}
        static void ProofChanged(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(ProofKey,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){busy=true;proofStep=0;proofRows.Clear();proofDeadline=EditorApplication.timeSinceStartup+45;proofStableAfter=EditorApplication.timeSinceStartup+10;EditorApplication.update-=ProofTick;EditorApplication.update+=ProofTick;}
            if(state==PlayModeStateChange.EnteredEditMode){busy=false;SessionState.EraseBool(ProofKey);File.WriteAllText(Work+"/status.txt",File.Exists(Work+"/play_proof.txt")?File.ReadAllText(Work+"/play_proof.txt"):"FAILED no native play proof result");}
        }
        static void ProofTick()
        {
            if(!EditorApplication.isPlaying)return;
            try{
                if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Scene changed during art proof; no further inputs sent");
                var p=Object.FindFirstObjectByType<NativeWeaponPreviewPlayer>();if(p==null){if(EditorApplication.timeSinceStartup<proofDeadline)return;throw new Exception("Native player did not start");}
                if(p.takes.Length!=41)throw new Exception("Expected 41 takes");
                if(Object.FindFirstObjectByType<ShadowVale.Map01.Map01Mission>()!=null||Object.FindFirstObjectByType<ShadowVale.Map01.Map01Inventory>()!=null||Object.FindFirstObjectByType<ShadowVale.Gameplay.Combat.PlayerCombat>()!=null)throw new Exception("Gameplay component found in art-only scene");
                if(proofStep<CaptureShots.Length){
                    var shot=CaptureShots[proofStep++];int n=Array.FindIndex(p.takes,t=>t.item==shot.item&&t.action==shot.action);p.playing=false;p.Sample(n,p.takes[n].clip.length*shot.u);
                    var transforms=p.actor.GetComponentsInChildren<Transform>(true);string name=shot.item=="ak47"?"AK47_Player":shot.item=="knife"?"Nam_Dao_KaBar":null;
                    var prop=name!=null?transforms.FirstOrDefault(t=>t.name==name):transforms.FirstOrDefault(t=>t.name.StartsWith("PROP")&&t.name.Contains(shot.item));
                    var renderers=prop==null?Array.Empty<Renderer>():prop.GetComponentsInChildren<Renderer>(true);float visibleSize=renderers.Length==0?0:renderers.Max(r=>r.bounds.size.magnitude);
                    if(prop==null||visibleSize<.03f)throw new Exception("Expected visible prop at "+shot.item+"/"+shot.action+" size="+visibleSize);
                    var hand=transforms.FirstOrDefault(t=>t.name=="RightHand");proofRows.Add(shot.item+"/"+shot.action+" propSize="+visibleSize.ToString("F4")+" propScale="+prop.lossyScale+" hand="+(hand==null?"missing":hand.position.ToString("F4")));return;
                }
                if(EditorApplication.timeSinceStartup<proofStableAfter)return;
                EditorApplication.update-=ProofTick;File.WriteAllLines(Work+"/play_details.txt",proofRows);File.WriteAllText(Work+"/play_proof.txt","PASS: actual Play Mode stable for 10 seconds; 41 Blender takes; 6 weapon groups; no mission/inventory/combat; authored prop visibility checked");EditorApplication.isPlaying=false;
            }catch(Exception e){EditorApplication.update-=ProofTick;File.WriteAllText(Work+"/play_proof.txt","FAILED "+e);Debug.LogException(e);if(SceneManager.GetActiveScene().path==ScenePath)EditorApplication.isPlaying=false;else{busy=false;SessionState.EraseBool(ProofKey);File.WriteAllText(Work+"/status.txt","STOPPED: another operation changed the scene; "+e.Message);}}
        }
    }
}
