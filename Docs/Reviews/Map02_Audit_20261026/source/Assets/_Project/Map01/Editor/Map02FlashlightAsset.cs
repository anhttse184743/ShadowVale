using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]public static class Map02FlashlightAsset
    {
        const string Folder="Assets/Resources/Equipment";
        static Map02FlashlightAsset(){EditorApplication.update+=Tick;}
        static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Temp/Map02FlashlightAsset.request"))return;File.Delete("Temp/Map02FlashlightAsset.request");Build();}
        [MenuItem("ShadowVale/Map 2/Tạo asset đèn pin tuần tra")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var black=Material("Map02TorchBlack",new Color(.035f,.042f,.049f),.65f,.48f);
            var metal=Material("Map02TorchMetal",new Color(.09f,.105f,.12f),.8f,.6f);
            var lens=Material("Map02TorchLens",new Color(.55f,.8f,1),.1f,.85f);lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(.5f,.8f,1)*2);EditorUtility.SetDirty(lens);
            var root=new GameObject("Map02PatrolFlashlight");
            try
            {
                Cylinder(root.transform,"Handle",0,.135f,.033f,black);Cylinder(root.transform,"Head",.09f,.055f,.049f,black);
                Cylinder(root.transform,"Lens",.119f,.003f,.038f,lens);Cylinder(root.transform,"Tail",-.082f,.024f,.037f,metal);
                for(int i=0;i<10;i++)Cylinder(root.transform,"Grip ring "+i,-.06f+i*.012f,.0022f,.035f,metal);
                Cylinder(root.transform,"Head rim",.117f,.006f,.05f,metal);
                var clip=GameObject.CreatePrimitive(PrimitiveType.Cube);clip.name="Pocket clip";clip.transform.SetParent(root.transform,false);clip.transform.localPosition=new Vector3(.021f,0,-.024f);clip.transform.localScale=new Vector3(.004f,.009f,.105f);clip.GetComponent<Renderer>().sharedMaterial=metal;Object.DestroyImmediate(clip.GetComponent<Collider>());
                var emitter=new GameObject("Emitter");emitter.transform.SetParent(root.transform,false);emitter.transform.localPosition=Vector3.forward*.125f;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Map02PatrolFlashlight.prefab");AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllText("Logs/Map02Chapter/flashlight-asset.txt","Created "+Folder+"/Map02PatrolFlashlight.prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        static Material Material(string name,Color color,float metal,float smooth)
        {var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;}
        static void Cylinder(Transform root,string name,float z,float length,float diameter,Material material)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=Vector3.forward*z;g.transform.localRotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(diameter,length*.5f,diameter);g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());}
    }
}
