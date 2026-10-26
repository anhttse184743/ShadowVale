using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class TamEscortBuilder
    {
        const string Request="Temp/TamEscortBuild.request";
        static TamEscortBuilder(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Request);
            try{Build();File.WriteAllText("Temp/TamEscortBuild.result","PASS animation clips built and bindings validated");}
            catch(Exception e){File.WriteAllText("Temp/TamEscortBuild.result","FAIL "+e);Debug.LogException(e);}
        }
        [MenuItem("ShadowVale/Map 2/Build approved Tâm escort")]
        public static void Build()
        {
            const string output="Assets/_Project/Map01/Resources/Story/TamEscort";
            Directory.CreateDirectory(output);AssetDatabase.Refresh();
            foreach(var who in new[]{"Nam","Tam"})foreach(var take in new[]{"Walk","Idle"})
            {
                string path="Assets/_Project/Art/TamEscort/"+who+"_ShoulderAssist_"+take+".fbx";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Generic;
                importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
                importer.resampleCurves=true;importer.preserveHierarchy=true;importer.SaveAndReimport();
                var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                string modelPath=who=="Nam"?"Assets/_Project/Art/Characters/Player/Player.fbx":"Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx";
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var names=model.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var result=new AnimationClip{name=who+take,frameRate=30};int bound=0;
                foreach(var binding in AnimationUtility.GetCurveBindings(source))
                {
                    if(binding.type!=typeof(Transform)||string.IsNullOrEmpty(binding.path))continue;
                    string bone=binding.path.Split('/').Last();if(!names.TryGetValue(bone,out var target))continue;
                    var adjusted=binding;adjusted.path=AnimationUtility.CalculateTransformPath(target,model.transform);
                    AnimationUtility.SetEditorCurve(result,adjusted,AnimationUtility.GetEditorCurve(source,binding));bound++;
                }
                if(bound<100)throw new InvalidOperationException(who+take+": only "+bound+" transform curves bound.");
                var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=take=="Walk";AnimationUtility.SetAnimationClipSettings(result,settings);
                string asset=output+"/"+who+take+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
                if(existing!=null){EditorUtility.CopySerialized(result,existing);UnityEngine.Object.DestroyImmediate(result);}else AssetDatabase.CreateAsset(result,asset);
                var instance=UnityEngine.Object.Instantiate(model);try
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);clip.SampleAnimation(instance,0);
                    var hips=instance.GetComponentsInChildren<Transform>().First(t=>t.name=="Hips");
                    if(!float.IsFinite(hips.position.y)||hips.position.y<.5f||hips.position.y>1.5f)throw new InvalidOperationException(who+take+": invalid imported hips "+hips.position);
                }finally{UnityEngine.Object.DestroyImmediate(instance);}
            }
            foreach(string who in new[]{"Nam","Tam"})
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(who=="Nam"?"Assets/_Project/Art/Characters/Player/Player.fbx":"Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx");
                var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>(output+"/"+who+"Walk.anim");var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(output+"/"+who+"Idle.anim");
                var paths=AnimationUtility.GetCurveBindings(walk).Where(b=>b.type==typeof(Transform)&&!string.IsNullOrEmpty(b.path)).Select(b=>b.path).Distinct().ToArray();
                var instance=UnityEngine.Object.Instantiate(model);try
                {
                    var nodes=paths.Select(p=>instance.transform.Find(p)).ToArray();if(nodes.Any(t=>t==null))throw new InvalidOperationException("Missing escort source bones: "+who);
                    var data=new Map02EscortPoseData{names=nodes.Select(t=>t.name).ToArray(),duration=walk.length};
                    idle.SampleAnimation(instance,0);data.idle=new Map02EscortPoseData.Frame{bones=nodes.Select(Map01PistolAnimationData.Pose.Read).ToArray()};
                    int count=Mathf.RoundToInt(walk.length*30)+1;data.walk=new Map02EscortPoseData.Frame[count];
                    for(int frame=0;frame<count;frame++){walk.SampleAnimation(instance,walk.length*frame/(count-1));data.walk[frame]=new Map02EscortPoseData.Frame{bones=nodes.Select(Map01PistolAnimationData.Pose.Read).ToArray()};}
                    File.WriteAllText(output+"/"+who+"Poses.json",JsonUtility.ToJson(data));
                }finally{UnityEngine.Object.DestroyImmediate(instance);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
    }
}
