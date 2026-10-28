using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static class ShelterSuppliesSetup
    {
        const string Source="Assets/_Project/Art/Characters/Animations/ShelterSupplies/";
        const string Output="Assets/Resources/OperationsAnimation/ShelterSupplies/";
        static Transform Node(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        public static void Import()
        {
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            foreach(var name in new[]{"Collect","Handoff","HungRest"}){
                var p=Source+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(p);
                importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
                importer.preserveHierarchy=true;importer.isReadable=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.importVisibility=false;
                var take=importer.defaultClipAnimations.First();take.name="Shelter_"+name;take.loopTime=false;importer.clipAnimations=new[]{take};importer.SaveAndReimport();
            }
            var data=AssetDatabase.LoadAssetAtPath<Map01SupplyPresentation>(Output+"Presentation.asset");if(data==null){data=ScriptableObject.CreateInstance<Map01SupplyPresentation>();AssetDatabase.CreateAsset(data,Output+"Presentation.asset");}
            data.collect=Clip("Collect");data.handoff=Clip("Handoff");data.hungRest=Clip("HungRest");
            data.collectCloth=AssetDatabase.LoadAssetAtPath<TextAsset>(Source+"Collect_Cloth.json");data.handoffCloth=AssetDatabase.LoadAssetAtPath<TextAsset>(Source+"Handoff_Cloth.json");
            data.namModel=Build("Collect","Assets/_Project/Prefabs/Player/Player.prefab",data);
            data.hungModel=Build("HungRest","Assets/_Project/Map01/Resources/Characters/HungVisual.prefab",data);
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/ShelterSupplies");File.WriteAllText("Logs/ShelterSupplies/import.txt",$"Collect {data.collect.length}s; handoff {data.handoff.length}s; rest {data.hungRest.length}s; floor {data.namFloorOffset}; basis {data.basisX}/{data.basisY}/{data.basisZ}");
            Debug.Log("SHELTER_SUPPLIES_IMPORTED");
        }
        static AnimationClip Clip(string name)=>AssetDatabase.LoadAllAssetsAtPath(Source+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        static GameObject Build(string name,string reference,Map01SupplyPresentation data)
        {
            var wrapper=new GameObject(name+" presentation");var raw=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source+name+".fbx"));raw.transform.SetParent(wrapper.transform,false);
            var x=raw.transform.InverseTransformPoint(Node(raw,"Basis_X").position);var y=raw.transform.InverseTransformPoint(Node(raw,"Basis_Y").position);var z=raw.transform.InverseTransformPoint(Node(raw,"Basis_Z").position);
            raw.transform.localRotation=Quaternion.Inverse(Quaternion.LookRotation(-y.normalized,z.normalized));
            foreach(var animator in raw.GetComponentsInChildren<Animator>())Object.DestroyImmediate(animator);
            var template=AssetDatabase.LoadAssetAtPath<GameObject>(reference);if(template==null)throw new Exception("Missing reference model: "+reference);
            var original=template.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var renderer in raw.GetComponentsInChildren<Renderer>(true)){
                var same=original.FirstOrDefault(r=>r.name==renderer.name||renderer is SkinnedMeshRenderer skin&&skin.sharedMesh!=null&&r.sharedMesh!=null&&r.sharedMesh.name==skin.sharedMesh.name);
                if(renderer.name=="Interaction_Backpack_Body"||renderer.name=="Interaction_Backpack_Flap")same=original.FirstOrDefault(r=>r.name.IndexOf("Balo",StringComparison.OrdinalIgnoreCase)>=0)??same;
                if(same!=null){renderer.sharedMaterials=same.sharedMaterials;continue;}
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++){
                    var old=materials[i];if(old==null)continue;
                    var materialPath=Output+old.name.Replace('/','_')+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));material.color=old.HasProperty("_Color")?old.color:new Color(.2f,.25f,.13f);AssetDatabase.CreateAsset(material,materialPath);}
                    materials[i]=material;
                }
                renderer.sharedMaterials=materials;
            }
            Clip(name).SampleAnimation(raw,0);
            if(name=="Collect"){
                data.basisX=x;data.basisY=y;data.basisZ=z;
                var mesh=new Mesh();float lowest=float.PositiveInfinity;
                foreach(var skin in raw.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var vertex in mesh.vertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(vertex).y);}
                Object.DestroyImmediate(mesh);data.namFloorOffset=-lowest;
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Output+name+".prefab");Object.DestroyImmediate(wrapper);return prefab;
        }
    }
}

