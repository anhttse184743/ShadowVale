using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Import only the approved paired knife take; preserve the existing sleep/wake assets.</summary>
    public static class C3BedKnifeBuilder
    {
        public const string Root="Assets/_Project/Art/C3_BedKnife_v10";
        public const string Work="Docs/Reviews/C3_BedKnife_Unity_20261020";
        public const string DataPath="Assets/_Project/Map01/Resources/C3/BedAnimationKnife.asset";
        public const string SourceHash="31a0087e64de55bd6e6971362d9e515e6a77831439dc6699bbe13db6ddc5c0c7";
        const string Export="SourceArt/C3_BedKnife_Unity_20261020/UnityExport";
        [Serializable] sealed class Receipt { public string source_sha256; public bool source_unchanged; }
        static Transform Named(GameObject model,string name)=>model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);

        [MenuItem("ShadowVale/C3/Build approved bedside knife v10")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before import.");
            var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(Export+"/export.json"));
            if(receipt.source_sha256!=SourceHash||!receipt.source_unchanged)throw new InvalidOperationException("Unapproved Blender source.");
            var original=AssetDatabase.LoadAssetAtPath<Map01BedAnimationData>(C3BedAnimationBuilder.DataPath);
            if(original==null||original.sleep==null||original.wake==null)throw new InvalidOperationException("Existing sleep/wake data is required.");
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Work);
            string path=Root+"/C3_BedKnife_v10.fbx";File.Copy(Export+"/C3_BedKnife_v10.fbx",path,true);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation=true;importer.importVisibility=false;importer.importCameras=false;importer.importLights=false;
            importer.optimizeGameObjects=false;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.isReadable=true;importer.skinWeights=ModelImporterSkinWeights.Standard;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.clipAnimations=new[]{new ModelImporterClipAnimation{
                name="C3_BedKnife_v10",takeName=importer.defaultClipAnimations.First().takeName,firstFrame=1,lastFrame=400,
                keepOriginalOrientation=true,keepOriginalPositionXZ=true,keepOriginalPositionY=true,
                lockRootRotation=false,lockRootPositionXZ=false,lockRootHeightY=false,loopTime=false}};
            importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            if(Mathf.Abs(clip.length-399f/60)>.02f)throw new InvalidOperationException("Knife take timing changed.");
            var pair=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                if(pair.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("K54")||t.name.Contains("Holster")))
                    throw new InvalidOperationException("Obsolete pistol prop was exported.");
                var animator=pair.GetComponent<Animator>();if(animator==null)animator=pair.AddComponent<Animator>();
                animator.runtimeAnimatorController=null;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                C3BedAnimationBuilder.ResolveMaterials(pair,Root);
                var data=Object.Instantiate(original);data.name="BedAnimationKnife";
                data.execution=clip;data.sourceHash=SourceHash;data.executionIsKnife=true;
                data.frames=400;data.fps=60;data.shotFrame=239;data.executionPropVisibleFrame=175;
                data.namPath=AnimationUtility.CalculateTransformPath(Named(pair,"Nam_DatCho"),pair.transform);
                data.enemyPath=AnimationUtility.CalculateTransformPath(Named(pair,"Dich_Rig"),pair.transform);
                data.executionPropPath=AnimationUtility.CalculateTransformPath(Named(pair,"Nam_Dao_KaBar"),pair.transform);
                data.pistolPath="";data.holsterPath="";
                using var pose=new Map01CinematicPose(animator,skinGrounding:false);
                var bones=Named(pair,"Player_Rig").GetComponentsInChildren<Transform>();
                var feet=bones.Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").ToArray();
                Vector3 Floor()=>new Vector3(feet.Average(t=>t.position.x),0,feet.Average(t=>t.position.z));
                pose.Sample(clip,0);data.approach=pair.transform.InverseTransformPoint(Floor());
                var up=bones.Single(t=>t.name=="Neck").position-bones.Single(t=>t.name=="Hips").position;
                var right=bones.Single(t=>t.name=="RightArm").position-bones.Single(t=>t.name=="LeftArm").position;
                data.approachRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.Cross(right,up),Vector3.up));
                pose.Sample(clip,data.Duration);data.exit=pair.transform.InverseTransformPoint(Floor());data.exitRotation=data.approachRotation;
                pose.Sample(clip,0);Named(pair,"Nam_Dao_KaBar").gameObject.SetActive(false);
                pair.name="C3 Bed knife v10";data.pairPrefab=PrefabUtility.SaveAsPrefabAsset(pair,Root+"/C3_BedKnife.prefab");
                var previous=AssetDatabase.LoadAssetAtPath<Map01BedAnimationData>(DataPath);
                if(previous==null)AssetDatabase.CreateAsset(data,DataPath);
                else{EditorUtility.CopySerialized(data,previous);Object.DestroyImmediate(data);EditorUtility.SetDirty(previous);data=previous;}
                AssetDatabase.SaveAssets();
                File.Copy(Export+"/source_witness.json",Work+"/source_witness.json",true);
                File.WriteAllText(Work+"/import.txt","source="+SourceHash+"\nduration="+clip.length+"\napproach="+data.approach+" exit="+data.exit+"\nsleep/wake source="+AssetDatabase.GetAssetPath(original)+"\n");
            }
            finally{Object.DestroyImmediate(pair);}
            Debug.Log("C3_BED_KNIFE_V10_BUILD_PASS");
        }
    }
}
