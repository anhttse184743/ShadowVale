using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    /// <summary>Imports only the approved landing cast/takes; never rebuilds the gameplay player or scene.</summary>
    public static class Map02ArrivalC1Import
    {
        const string Art="Assets/_Project/Art/Map02ArrivalC1/",Folder="Assets/Resources/Map02ArrivalC1/";
        [Serializable] public sealed class Poses{public Frame[] frames;}
        [Serializable] public sealed class Frame{public int frame;public float time;public Bones Nam,Hung;}
        [Serializable] public sealed class Bones{public float[] Hips,Head,LeftHand,RightHand,LeftForeArm,LeftArm,RightLeg,LeftFoot,RightFoot;}
        static Vector3 V(float[] p)=>new(p[0],p[1],p[2]);
        static Vector3 Convert(Vector3 p)=>new(p.y,p.z,-p.x);
        static Quaternion Basis(Vector3 hips,Vector3 head,Vector3 left,Vector3 right)=>Quaternion.LookRotation(Vector3.Cross(right-left,head-hips).normalized,(head-hips).normalized);
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data=AssetDatabase.LoadAssetAtPath<Map02ArrivalC1Data>(Folder+"Approved.asset");
            if(data==null){data=ScriptableObject.CreateInstance<Map02ArrivalC1Data>();AssetDatabase.CreateAsset(data,Folder+"Approved.asset");}
            var poses=JsonUtility.FromJson<Poses>(File.ReadAllText(Folder+"ReviewPoses.json"));
            var color=new Color(.29f,.19f,.13f,1); // replaced below with the approved Blender material swatch
            var receipt=File.ReadAllText("SourceArt/Map02_C1_Unity_20261026/skin-color.json");var rgba=JsonUtility.FromJson<SkinColor>(receipt).rgba;color=new Color(rgba[0],rgba[1],rgba[2],1);
            var skin=AssetDatabase.LoadAssetAtPath<Material>(Art+"Nam_C1_Skin.mat");if(skin==null){skin=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(skin,Art+"Nam_C1_Skin.mat");}skin.SetColor("_BaseColor",color);skin.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(skin);
            for(int index=0;index<2;index++)
            {
                string who=index==0?"Nam":"Hung";string model=Art+who+"_C1_v4.fbx",motion=Art+who+"_C1_Motion_v4.fbx";
                var mi=(ModelImporter)AssetImporter.GetAtPath(model);mi.animationType=ModelImporterAnimationType.Human;mi.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;mi.importAnimation=false;mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.importCameras=mi.importLights=false;mi.optimizeGameObjects=false;mi.SaveAndReimport();
                var ci=(ModelImporter)AssetImporter.GetAtPath(motion);ci.animationType=ModelImporterAnimationType.Legacy;ci.avatarSetup=ModelImporterAvatarSetup.NoAvatar;ci.importAnimation=true;ci.animationCompression=ModelImporterAnimationCompression.Off;ci.materialImportMode=ModelImporterMaterialImportMode.None;ci.importCameras=ci.importLights=false;ci.optimizeGameObjects=false;
                var take=ci.defaultClipAnimations.First();take.name=who+"_C1_v4";take.loopTime=take.loopPose=false;take.lockRootHeightY=take.lockRootPositionXZ=take.lockRootRotation=true;take.keepOriginalPositionY=take.keepOriginalPositionXZ=take.keepOriginalOrientation=true;ci.clipAnimations=new[]{take};ci.SaveAndReimport();
                var clip=AssetDatabase.LoadAllAssetsAtPath(motion).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(model);var wrapper=new GameObject(who+" C1 approved v4");var go=UnityEngine.Object.Instantiate(asset,wrapper.transform,false);
                try
                {
                    var animator=go.GetComponent<Animator>();if(animator==null||!animator.isHuman||!animator.avatar.isValid)throw new InvalidOperationException(who+" C1 avatar is invalid");animator.enabled=false;
                    string original=index==0?"Assets/_Project/Art/Characters/Player/Player.fbx":"Assets/_Project/Art/Characters/NPCs/hung.fbx";
                    var originalModel=AssetDatabase.LoadAssetAtPath<GameObject>(original);var body=originalModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r=>r.sharedMesh.vertexCount).First();
                    foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.sharedMaterials=renderer.name.Contains("Hand")?new[]{skin}:body.sharedMaterials;renderer.updateWhenOffscreen=true;}
                    clip.SampleAnimation(go,0);
                    Vector3 B(HumanBodyBones b)=>animator.GetBoneTransform(b).position;
                    var f=index==0?poses.frames[0].Nam:poses.frames[0].Hung;var start=index==0?new Vector3(.78f,-.42f,0):new Vector3(0,.1f,-.21f);var facing=index==0?Quaternion.identity:Quaternion.LookRotation(Vector3.left);
                    Vector3 Desired(float[] p)=>Quaternion.Inverse(facing)*Convert(V(p)-start);
                    var q=Basis(Desired(f.Hips),Desired(f.Head),Desired(f.LeftHand),Desired(f.RightHand))*Quaternion.Inverse(Basis(B(HumanBodyBones.Hips),B(HumanBodyBones.Head),B(HumanBodyBones.LeftHand),B(HumanBodyBones.RightHand)));
                    var offset=Desired(f.Hips)-q*B(HumanBodyBones.Hips);wrapper.transform.SetPositionAndRotation(offset,q);
                    var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Folder+who+".prefab");if(index==0){data.nam=prefab;data.namMotion=clip;}else{data.hung=prefab;data.hungMotion=clip;}
                    Debug.Log($"C1_V4_IMPORT {who} avatar={animator.avatar.isValid} duration={clip.length:F3} offset={offset:F4} rotation={q.eulerAngles:F3}");
                }
                finally{UnityEngine.Object.DestroyImmediate(wrapper);}
            }
            data.pack=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"C1Pack.json");data.travel=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"C1Travel.json");data.reviewPoses=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"ReviewPoses.json");data.sourceSha256="15723d4da3281b245f401c11446c7ee006ec79889436096adf5fc9b7a8234ae0";EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();BuildReadablePack();Debug.Log("C1_V4_IMPORT_PASS");
        }
        public static void BuildReadablePack()
        {
            var data=AssetDatabase.LoadAssetAtPath<Map02ArrivalC1Data>(Folder+"Approved.asset");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var source=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Nam_BaloRanRi").sharedMesh;
            // Keep CPU data only for this small C1 prop, without enabling Read/Write on the entire player FBX.
            var copy=new Mesh{name="Nam C1 readable backpack",indexFormat=source.indexFormat,vertices=source.vertices,normals=source.normals,tangents=source.tangents,uv=source.uv,uv2=source.uv2,colors=source.colors,bindposes=source.bindposes};
            copy.subMeshCount=source.subMeshCount;for(int i=0;i<source.subMeshCount;i++)copy.SetIndices(source.GetIndices(i),source.GetTopology(i),i);
            using(var counts=source.GetBonesPerVertex())using(var weights=source.GetAllBoneWeights())copy.SetBoneWeights(counts,weights);
            copy.RecalculateBounds();var path=Art+"Nam_C1_Backpack.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(copy,path);existing=copy;}else{EditorUtility.CopySerialized(copy,existing);UnityEngine.Object.DestroyImmediate(copy);}
            data.backpackMesh=existing;EditorUtility.SetDirty(existing);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();Debug.Log("C1_PACK_READABLE_PASS vertices="+existing.vertexCount);
        }
        [Serializable] sealed class SkinColor{public float[] rgba;}
    }
}
