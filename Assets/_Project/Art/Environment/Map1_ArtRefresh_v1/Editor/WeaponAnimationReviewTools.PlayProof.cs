using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ShadowVale.Map01;

namespace ShadowVale.EnvironmentReview
{
    public static partial class WeaponAnimationReviewTools
    {
        const string ProofKey="ShadowVale.WeaponReview.PlayProof";
        [InitializeOnLoadMethod]static void ResumeProof(){EditorApplication.update-=ProofTick;EditorApplication.update+=ProofTick;}
        static void BeginProof()
        {
            CheckClean();ReleasePreviewGraphs();EditorSceneManager.OpenScene(Preview);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Preview);
            SessionState.SetBool(ProofKey,true);SessionState.SetString(ProofKey+".start","");SessionState.SetInt(ProofKey+".step",0);EditorApplication.isPlaying=true;
        }
        static void ProofTick()
        {
            if(!SessionState.GetBool(ProofKey,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            string start=SessionState.GetString(ProofKey+".start","");if(start==""){SessionState.SetString(ProofKey+".start",EditorApplication.timeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture));return;}
            double elapsed=EditorApplication.timeSinceStartup-double.Parse(start,System.Globalization.CultureInfo.InvariantCulture);int step=SessionState.GetInt(ProofKey+".step",0);var d=UnityEngine.Object.FindFirstObjectByType<WeaponAnimationPreview>();
            if(d==null){FinishProof("FAIL: Preview scene did not start");return;}
            if(elapsed>2&&step==0){d.selected=Array.FindIndex(d.takes,t=>t.item=="pistol_k54"&&t.action=="aim");d.seconds=0;d.playing=true;SessionState.SetInt(ProofKey+".step",1);}
            if(elapsed>4&&step==1){d.selected=Array.FindIndex(d.takes,t=>t.item=="rifle_mosin"&&t.action=="bolt");d.seconds=0;SessionState.SetInt(ProofKey+".step",2);}
            if(elapsed>7&&step==2){d.selected=Array.FindIndex(d.takes,t=>t.item=="smoke_bomb"&&t.action=="throw");d.seconds=0;SessionState.SetInt(ProofKey+".step",3);}
            if(elapsed>10){bool isolated=UnityEngine.Object.FindFirstObjectByType<Map01Mission>()==null&&UnityEngine.Object.FindFirstObjectByType<Map02Mission>()==null;bool valid=d.actor.isHuman&&d.takes.All(t=>t.clip!=null)&&d.CurrentProp.root.gameObject.activeInHierarchy;
                FinishProof((isolated&&valid?"PASS":"FAIL")+": actual Play Mode; takes="+d.takes.Length+" human="+d.actor.isHuman+" noMission="+isolated+" activeProp="+d.CurrentProp.item+" animator="+d.actor.isActiveAndEnabled);}
        }
        static void FinishProof(string result){File.WriteAllText(Work+"/preview_play_proof.txt",result);SessionState.EraseBool(ProofKey);EditorApplication.isPlaying=false;EditorApplication.delayCall+=ConfigurePreviewStartup;}
    }
}
