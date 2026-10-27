using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static class Map02UndergroundStoreSetup
    {
        const string Folder="Assets/_Project/Map01/Resources/Story/NightStore";
        [MenuItem("ShadowVale/Map 2/Build Blender underground store")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Folder+"/Materials");
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/NightStore.fbx");
            if(importer==null)throw new InvalidOperationException("Export NightStore.fbx from Blender first.");
            importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(string name in new[]{"Walnut","Earth","Iron","Canvas","Paper","LampAmber"})
            {
                string path=Folder+"/Materials/NS_"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
                if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                mat.shader=shader;
                mat.name="NS_"+name;mat.color=name=="Iron"?new Color(.075f,.09f,.085f):name=="Canvas"?new Color(.32f,.33f,.19f):name=="Paper"?new Color(.7f,.63f,.43f):name=="LampAmber"?new Color(1,.47f,.12f):Color.white;
                mat.SetColor("_BaseColor",mat.color);
                mat.SetFloat("_Smoothness",name=="Iron"?.42f:.15f);mat.SetFloat("_Metallic",name=="Iron"?.75f:0);
                if(name=="Walnut"||name=="Earth"){var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/NightStore_"+name+".png");mat.SetTexture("_BaseMap",tex);mat.SetTexture("_MainTex",tex);}
                if(name=="LampAmber"){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(1,.33f,.04f)*2);}
                EditorUtility.SetDirty(mat);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"NS_"+name),mat);
            }
            importer.SaveAndReimport();AssetDatabase.SaveAssets();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/NightStore.fbx");var root=new GameObject("UndergroundStore");
            try
            {
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(model);visual.name="Blender authored cellar";visual.transform.SetParent(root.transform,false);
                foreach(var r in visual.GetComponentsInChildren<Renderer>())
                {
                    var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)if(mats[i]!=null){string name=mats[i].name.Replace(" (Instance)","");var owned=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+name+".mat");if(owned!=null)mats[i]=owned;}r.sharedMaterials=mats;
                }
                var bounds=new Bounds();bool first=true;foreach(var r in visual.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                if(Mathf.Abs(bounds.center.x-3)>.6f||Mathf.Abs(bounds.size.x-8.3f)>1||bounds.min.y> -6.8f||bounds.max.y>1||bounds.size.z<4.9f||bounds.size.z>6)throw new Exception("Blender export axes/scale invalid: "+bounds);
                var lid=visual.GetComponentsInChildren<Renderer>().First(r=>r.name=="HatchLeaf");if(Mathf.Abs(lid.bounds.center.x)>.12f||Mathf.Abs(lid.bounds.center.z)>.12f)throw new Exception("FBX hatch pivot offset is invalid: "+lid.bounds);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/UndergroundStore.prefab");
                Directory.CreateDirectory("SourceArt/Map02UndergroundStore");File.WriteAllText("SourceArt/Map02UndergroundStore/unity-import.txt","PASS Blender model imported at metre scale; bounds="+bounds+"\nRenderers="+visual.GetComponentsInChildren<Renderer>().Length+"\nHatchPivot="+visual.GetComponentsInChildren<Transform>().First(t=>t.name=="HatchPivot").position+"\n");
            }
            finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();Debug.Log("PASS Blender underground store prefab and URP materials built.");
        }
    }
}
