using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Map01.EditorTools
{
    /// <summary>
    /// Puts the Day 3 / Night 3 models into the chapter: the rank-and-file enemy model for the infantry, the
    /// Blender demolition charge and M41 tank, and Hùng's shot clip. The named NPCs are Map02CharacterSetup's. Writes Map02ChapterLayout and Logs/Map02Models/setup.txt. Safe to run again.
    /// Menu: ShadowVale > Map 2 > Set up Day 3 / Night 3 models. Batch: -executeMethod ShadowVale.Map01.EditorTools.Map02ModelSetup.Run
    /// </summary>
    public static class Map02ModelSetup
    {
        const string Npcs="Assets/_Project/Art/Characters/NPCs/",Out="Assets/_Project/Map01/Resources/Characters/Map02/";
        // The villagers, Thành, Lực and Tâm come from Map02CharacterSetup (ShadowVale > Map 2 > Đồng bộ model nhân vật).
        static readonly StringBuilder report=new StringBuilder();

        [MenuItem("ShadowVale/Map 2/Set up Day 3 · Night 3 models")]
        public static void Run()
        {
            report.Clear();Directory.CreateDirectory(Out);
            var layout=AssetDatabase.LoadAssetAtPath<Map02ChapterLayout>("Assets/_Project/Map01/Resources/Story/Map02ChapterLayout.asset");
            var soldier=Visual("Assets/_Project/Art/Characters/Enemies/lính rig.fbx","Enemy soldier",1.75f,AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Enemy.controller"));
            var charge=Charge();var tank=Tank();
            var fall=AssetDatabase.LoadAllAssetsAtPath(Npcs+"Animations/Hung_TrungDan.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview"));
            report.AppendLine($"Hùng shot clip: {(fall!=null?fall.name+" "+fall.length.ToString("0.00")+" s":"MISSING")}");
            if(soldier!=null)layout.enemyArt=soldier;
            if(charge!=null)layout.explosivesArt=charge;
            if(tank!=null)layout.tankArt=tank;
            if(fall!=null)layout.hungFallClip=fall;
            layout.grenadeCrateArt=Supply("GrenadeCrate");layout.ammoCanArt=Supply("AmmoCan");layout.medkitArt=Supply("MedkitBag");layout.grenadeArt=Supply("Grenade");layout.strawCartArt=Supply("StrawCart",true);
            EditorUtility.SetDirty(layout);AssetDatabase.SaveAssets();
            report.AppendLine($"Layout: enemyArt={(soldier!=null?soldier.name:"-")}, explosivesArt={(charge!=null?charge.name:"-")}, tankArt={(tank!=null?tank.name:"-")}");
            Directory.CreateDirectory("Logs/Map02Models");File.WriteAllText("Logs/Map02Models/setup.txt",report.ToString());
            Debug.Log("Map 2 models:\n"+report);
        }

        /// <summary>Humanoid import, then a prefab root with the model scaled to height and standing on y = 0.</summary>
        static GameObject Visual(string modelPath,string role,float height,RuntimeAnimatorController controller)
        {
            var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if(importer==null){report.AppendLine($"{role}: no model at {modelPath}");return null;}
            if(importer.animationType!=ModelImporterAnimationType.Human)
            {importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();}
            var avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            bool human=avatar!=null&&avatar.isValid&&avatar.isHuman;
            if(!human)
            {
                // A rig the Humanoid mapper cannot read stays Generic: the person is shown, standing still.
                importer.animationType=ModelImporterAnimationType.Generic;importer.SaveAndReimport();
                avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            }
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root=new GameObject(Path.GetFileNameWithoutExtension(modelPath)+" visual");
            var body=(GameObject)PrefabUtility.InstantiatePrefab(model);body.transform.SetParent(root.transform,false);
            var animator=body.GetComponentInChildren<Animator>();if(animator==null)animator=body.AddComponent<Animator>();
            animator.avatar=avatar;animator.runtimeAnimatorController=human?controller:null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            foreach(var extra in body.GetComponentsInChildren<Camera>(true).Cast<Component>().Concat(body.GetComponentsInChildren<Light>(true)))Object.DestroyImmediate(extra.gameObject);
            var renderers=body.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0){Object.DestroyImmediate(root);report.AppendLine($"{role}: model has no renderer");return null;}
            var bounds=renderers.Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            float scale=bounds.size.y>.01f?height/bounds.size.y:1;body.transform.localScale*=scale;
            bounds=body.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            body.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z); // Feet on the ground, centred on the slot.
            var path=Out+Path.GetFileNameWithoutExtension(modelPath).Replace(' ','_')+"_Visual.prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
            report.AppendLine($"{role}: {(human?"Humanoid":"GENERIC (no walk animation)")}, raw height {bounds.size.y/scale:0.00} -> {height:0.00} m (x{scale:0.###}), {renderers.Length} renderers -> {path}");
            return prefab;
        }

        /// <summary>
        /// The Blender M41-type tank (SourceArt/Map02_Props/build_m41_tank.py): one URP material with its baked atlas,
        /// gun along +Z, sitting on y = 0, a box collider so rounds hit it. Moving parts keep their names and pivots.
        /// </summary>
        /// <summary>One supply prop from SourceArt/Map02_Props/prepare_supply_props.py: URP Lit with base, normal and metallic/smoothness maps
        /// (a colour-only source stays matte). solid adds a mesh collider, for props Nam walks round and things rest on.</summary>
        static GameObject Supply(string name,bool solid=false)
        {
            const string dir="Assets/_Project/Art/Props/Map02/Supplies/";var model=dir+name+".fbx";
            var importer=AssetImporter.GetAtPath(model) as ModelImporter;if(importer==null){report.AppendLine(name+": FBX missing");return null;}
            if(!importer.bakeAxisConversion||importer.materialImportMode!=ModelImporterMaterialImportMode.None)
            {importer.bakeAxisConversion=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();}
            Texture2D Map(string kind,bool normal,bool linear)
            {
                var path=dir+name+"_"+kind+".png";var ti=AssetImporter.GetAtPath(path) as TextureImporter;
                if(ti!=null&&(ti.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default)||ti.sRGBTexture==linear))
                {ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=!linear;ti.SaveAndReimport();}
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var matPath=dir+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
            mat.SetTexture("_BaseMap",Map("Albedo",false,false));mat.SetColor("_BaseColor",Color.white);
            var normal=Map("Normal",true,true);mat.SetTexture("_BumpMap",normal);mat.SetFloat("_BumpScale",1);
            if(normal!=null)mat.EnableKeyword("_NORMALMAP");else mat.DisableKeyword("_NORMALMAP");
            var metalSmooth=Map("MetalSmooth",false,true);mat.SetTexture("_MetallicGlossMap",metalSmooth);
            if(metalSmooth!=null){mat.SetFloat("_Smoothness",1);mat.SetFloat("_Metallic",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");}
            else{mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Metallic",0);mat.DisableKeyword("_METALLICSPECGLOSSMAP");}
            EditorUtility.SetDirty(mat);
            var root=new GameObject(name+" visual");
            var body=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model));body.transform.SetParent(root.transform,false);
            foreach(var r in body.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();
            var bounds=body.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            body.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z); // Stands on its base centre.
            if(solid)foreach(var f in body.GetComponentsInChildren<MeshFilter>())f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,dir+name+".prefab");Object.DestroyImmediate(root);
            int tris=AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().Sum(m=>m.triangles.Length/3);
            report.AppendLine($"{name}: {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m, {tris} triangles -> {dir}{name}.prefab");
            return prefab;
        }

        static GameObject Tank()
        {
            const string dir="Assets/_Project/Art/Props/Map02/",model=dir+"M41_Tank.fbx";
            var importer=AssetImporter.GetAtPath(model) as ModelImporter;if(importer==null){report.AppendLine("Tank: FBX missing");return null;}
            if(!importer.bakeAxisConversion||importer.materialImportMode!=ModelImporterMaterialImportMode.None)
            {importer.bakeAxisConversion=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();}
            var normalImporter=AssetImporter.GetAtPath(dir+"M41_Tank_Normal.png") as TextureImporter;
            if(normalImporter!=null&&normalImporter.textureType!=TextureImporterType.NormalMap){normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport();}
            var path=dir+"M41_Tank_Worn.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",new Color(.96f,.96f,.96f));mat.SetFloat("_Smoothness",.18f); // Colours are baked to the reference; matte paint.
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"M41_Tank_Albedo.png"));
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"M41_Tank_Normal.png"));mat.SetFloat("_BumpScale",1);mat.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(mat);
            var root=new GameObject("M41 tank visual");
            var body=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model));body.transform.SetParent(root.transform,false);
            foreach(var r in body.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();
            var muzzle=body.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Gun_Muzzle");
            if(muzzle!=null&&root.transform.InverseTransformPoint(muzzle.position).z<0)body.transform.localRotation=Quaternion.Euler(0,180,0)*body.transform.localRotation; // The gun points forward.
            var bounds=body.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            body.transform.position-=new Vector3(bounds.center.x,bounds.min.y,0);
            var hull=body.GetComponentsInChildren<Renderer>().First(r=>r.name=="Hull").bounds;
            var box=root.AddComponent<BoxCollider>();box.center=root.transform.InverseTransformPoint(hull.center);box.size=hull.size;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,dir+"M41_Tank.prefab");Object.DestroyImmediate(root);
            int tris=AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().Sum(m=>m.triangles.Length/3);
            var parts=string.Join(", ",new[]{"Turret","Gun","Gun_Muzzle","Cupola","Hatch_Commander","Hatch_Loader","MG_Mount","MG","MG_Muzzle"}.Where(n=>prefab.GetComponentsInChildren<Transform>().Any(t=>t.name==n)));
            report.AppendLine($"Tank: {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m, {tris} triangles, moving parts: {parts} -> {dir}M41_Tank.prefab");
            return prefab;
        }

        /// <summary>The Blender charge: one URP material with the baked worn textures.</summary>
        static GameObject Charge()
        {
            const string model="Assets/_Project/Art/Props/Map02/DemolitionCharge.fbx",dir="Assets/_Project/Art/Props/Map02/";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(model);if(source==null){report.AppendLine("Charge: FBX missing");return null;}
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            Material Mat(string name,Color linear,float smooth)
            {
                var path=dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m==null){m=new Material(lit);AssetDatabase.CreateAsset(m,path);}
                m.SetColor("_BaseColor",linear.gamma);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
            }
            // Worn, crumpled plastic and faded tape: baked in Blender (SourceArt/Map02_Props) to albedo + normal maps.
            var normalImporter=AssetImporter.GetAtPath(dir+"DemolitionCharge_Normal.png") as TextureImporter;
            if(normalImporter!=null&&normalImporter.textureType!=TextureImporterType.NormalMap){normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport();}
            var worn=Mat("Charge_Worn",Color.white,.45f);
            worn.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"DemolitionCharge_Albedo.png"));
            worn.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"DemolitionCharge_Normal.png"));worn.SetFloat("_BumpScale",1);worn.EnableKeyword("_NORMALMAP");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(source);
            foreach(var r in root.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>worn).ToArray();
            var bounds=root.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,dir+"DemolitionCharge.prefab");Object.DestroyImmediate(root);
            report.AppendLine($"Charge: size {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m -> {dir}DemolitionCharge.prefab");
            return prefab;
        }
    }
}
