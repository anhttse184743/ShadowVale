using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// 26/11: Hùng V119 wore Tests/Sandbox/T036_Generic/Hung/Meshy.mat (base colour only, from a test sandbox).
    /// Builds Art/.../Hung_Meshy/Hung_V119.mat from the Meshy PA2 texture set it came from (same base colour,
    /// byte for byte, plus its normal map) and puts it on the V119 skin in HungVisual.prefab and PlayerHung.prefab. Idempotent.
    /// </summary>
    public static class HungV119MaterialSetup
    {
        const string Source=@"D:\DO_AN_GAME\chuyen_dong\GiaoViec_Codex\T029_Hung_ThayNgoaiHinh\do\xuat_thu_unity\Hung_PA2\";
        const string Folder="Assets/_Project/Art/Characters/NPCs/Hung_Meshy/";
        const string BaseColor=Folder+"Hung_V119_BaseColor.png",Normal=Folder+"Hung_V119_Normal.png",MaterialPath=Folder+"Hung_V119.mat";
        const string Sandbox="Assets/_Project/Map01/Tests/Sandbox/T036_Generic/Hung/Meshy.mat";
        // Map 2's Art/Map02DayOne/hung.prefab also wears the sandbox material; it is T042 work in progress, left to its owner.
        static readonly string[] Prefabs={"Assets/_Project/Map01/Resources/Characters/HungVisual.prefab","Assets/_Project/Prefabs/Player/PlayerHung.prefab"};

        [MenuItem("ShadowVale/Hung/Lam lai vat lieu V119")]
        public static void Run()
        {
            var log=new System.Text.StringBuilder();
            Copy(Source+"Hung_PA2_BaseColor.png",BaseColor,log);Copy(Source+"Hung_PA2_Normal.png",Normal,log);
            AssetDatabase.Refresh();
            Texture(BaseColor,false);Texture(Normal,true);
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null){
                var template=AssetDatabase.LoadAssetAtPath<Material>(Sandbox);
                material=template!=null?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material,MaterialPath);log.AppendLine("created "+MaterialPath);
            }
            material.name="Hung_V119";
            var baseMap=AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColor);var normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>(Normal);
            material.SetTexture("_BaseMap",baseMap);material.SetTexture("_MainTex",baseMap);material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BumpMap",normalMap);material.SetFloat("_BumpScale",1);material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            // Put it on every renderer of the visual that wore the sandbox material.
            int swapped=0;foreach(var Prefab in Prefabs){
            var root=PrefabUtility.LoadPrefabContents(Prefab);int before=swapped;
            try{
                var sandbox=AssetDatabase.LoadAssetAtPath<Material>(Sandbox);
                foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
                    var mats=r.sharedMaterials;bool changed=false;
                    for(int i=0;i<mats.Length;i++)if(mats[i]!=null&&mats[i]==sandbox){mats[i]=material;changed=true;swapped++;}
                    if(changed){r.sharedMaterials=mats;log.AppendLine($"{Prefab} {r.name}: {Sandbox} -> {MaterialPath}");}
                }
                if(swapped>before)PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            log.AppendLine($"swapped {swapped}; base {baseMap?.width}x{baseMap?.height} normal {normalMap?.width}x{normalMap?.height}");
            Directory.CreateDirectory("Logs/HungDiVe");File.WriteAllText("Logs/HungDiVe/vatlieu_setup.txt",log.ToString());
            Debug.Log("HUNG_V119_MATERIAL\n"+log);
        }
        static void Copy(string from,string to,System.Text.StringBuilder log)
        {
            if(File.Exists(to))return;
            if(!File.Exists(from))throw new Exception("Missing Meshy PA2 texture: "+from);
            File.Copy(from,to);log.AppendLine($"copied {Path.GetFileName(from)} -> {to}");
        }
        static void Texture(string path,bool normal)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);bool dirty=false;
            var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            if(importer.textureType!=type){importer.textureType=type;dirty=true;}
            if(importer.sRGBTexture==normal){importer.sRGBTexture=!normal;dirty=true;}
            if(importer.maxTextureSize!=4096){importer.maxTextureSize=4096;dirty=true;}
            if(dirty)importer.SaveAndReimport();
        }
    }
}
