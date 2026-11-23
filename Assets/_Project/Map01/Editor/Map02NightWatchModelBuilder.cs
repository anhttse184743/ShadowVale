using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static class Map02NightWatchModelBuilder
    {
        const string Folder = "Assets/_Project/Map02/Resources/Map02DayOne/NightWatch";
        [MenuItem("ShadowVale/Map 2/Build Blender night watch models")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Folder + "/Materials");
            foreach (var model in new[] { "VillageGate", "ShieldedLamp", "FenceSection" })
            {
                string path = Folder + "/" + model + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = false; importer.importCameras = importer.importLights = false;
                importer.globalScale = 1; importer.useFileScale = true; importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (string name in new[] { "Wood", "Bamboo", "Iron", "Brass", "Rope", "Glass", "Flame" })
                {
                    string matPath = Folder + "/Materials/NW_" + name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, matPath); }
                    material.color = name == "Wood" ? Color.white : name == "Bamboo" ? new Color(.43f,.39f,.20f) : name == "Iron" ? new Color(.12f,.13f,.11f) : name == "Brass" ? new Color(.36f,.28f,.12f) : name == "Rope" ? new Color(.5f,.42f,.26f) : name == "Glass" ? new Color(.72f,.68f,.50f,.22f) : new Color(1,.57f,.12f);
                    material.SetFloat("_Smoothness", name == "Glass" ? .75f : name == "Iron" || name == "Brass" ? .40f : .15f);
                    material.SetFloat("_Metallic", name == "Iron" || name == "Brass" ? .65f : 0);
                    if (name == "Wood") material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/WoodGrain.png"));
                    if (name == "Glass") { material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10); material.SetFloat("_ZWrite",0); material.SetFloat("_Cull",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000; }
                    if (name == "Flame") { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",new Color(1,.3f,.035f)*3); }
                    EditorUtility.SetDirty(material);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"NW_"+name),material);
                }
                importer.SaveAndReimport();
                var root = new GameObject(model);
                try
                {
                    var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); art.transform.SetParent(root.transform,false);
                    var bounds = new Bounds(); bool first = true;
                    foreach(var r in art.GetComponentsInChildren<Renderer>()) { if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds); }
                    if(model=="VillageGate" && (bounds.size.x<4.5f || bounds.size.y<1.4f || bounds.size.y>2.1f)) throw new Exception("Gate scale/axes invalid: "+bounds);
                    if(model=="FenceSection"){
                        var box=root.AddComponent<BoxCollider>();box.center=new Vector3(0,.7f,0);box.size=new Vector3(3.58f,1.4f,.09f);
                        var nav=root.AddComponent<UnityEngine.AI.NavMeshObstacle>();nav.shape=UnityEngine.AI.NavMeshObstacleShape.Box;nav.center=box.center;nav.size=box.size;nav.carving=true;nav.carveOnlyStationary=true;
                        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/FenceSectionProp.prefab");continue;
                    }
                    var visual=root.AddComponent<Map02NightWatchVisual>(); visual.gate=model=="VillageGate";
                    Transform Find(string name)=>art.GetComponentsInChildren<Transform>().First(t=>t.name==name);
                    if(visual.gate){visual.gateLeaf=Find("GateLeafHinge");visual.bolt=Find("BoltSlide");visual.seatedBolt=visual.bolt.localPosition;}
                    else
                    {
                        visual.hood=Find("LampHood");
                        var light=new GameObject("Shielded light opening");light.transform.SetParent(visual.hood,false);light.transform.position=root.transform.TransformPoint(new Vector3(0,.22f,.10f));light.transform.rotation=root.transform.rotation*Quaternion.Euler(35,0,0);
                        visual.beam=light.AddComponent<Light>();visual.beam.type=LightType.Spot;visual.beam.range=4.5f;visual.beam.spotAngle=64;visual.beam.intensity=2.0f;visual.beam.color=new Color(1,.70f,.33f);visual.beam.shadows=LightShadows.Soft;
                        var glow=new GameObject("Wick glow");glow.transform.SetParent(root.transform,false);glow.transform.localPosition=new Vector3(0,.18f,0);visual.glow=glow.AddComponent<Light>();visual.glow.type=LightType.Point;visual.glow.range=.65f;visual.glow.intensity=.28f;visual.glow.color=visual.beam.color;
                    }
                    visual.CaptureReferencePose();
                    if(visual.gate){
                        void Barrier(string name,Vector3 center,Vector3 size,Transform parent){
                            var solid=new GameObject(name);solid.transform.position=root.transform.TransformPoint(center);solid.transform.rotation=root.transform.rotation;solid.transform.SetParent(parent,true);
                            var box=solid.AddComponent<BoxCollider>();box.size=size;
                            var obstacle=solid.AddComponent<UnityEngine.AI.NavMeshObstacle>();obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Box;obstacle.size=size;obstacle.carving=true;obstacle.carveOnlyStationary=true;
                        }
                        Barrier("Solid gate leaf",new Vector3(0,.83f,0),new Vector3(1.8f,1.42f,.075f),visual.gateLeaf);
                        foreach(float side in new[]{-1f,1f}){
                            Barrier("Solid gate post",new Vector3(side*1.02f,.85f,0),new Vector3(.16f,1.7f,.18f),root.transform);
                            Barrier("Village boundary fence",new Vector3(side*2.87f,.7f,0),new Vector3(3.58f,1.4f,.09f),root.transform);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+model+"Prop.prefab");
                    Debug.Log("Night watch model built: "+model+" bounds="+bounds);
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
