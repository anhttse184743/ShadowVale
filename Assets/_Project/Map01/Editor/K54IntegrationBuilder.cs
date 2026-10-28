using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Reproducible one-way import from the approved Blender copy; original player assets are read only.</summary>
    public static class K54IntegrationBuilder
    {
        public const string Root="Assets/_Project/Art/K54_v12";
        public const string Model=Root+"/Nam_K54_v12_Game.fbx";
        public const string DataPath="Assets/_Project/Map01/Resources/Weapons/K54V12.asset";
        public const string Work="Docs/Reviews/K54_Integration_20261018";
        [Serializable] sealed class Receipt {public string source_sha256;public bool[] magazine_visible,round_visible;}
        sealed class Spec
        {
            public string root=Root,data=DataPath,work=Work,sourceRoot="K54_Player",weapon="pistol_k54";
            public string fbx="SourceArt/K54_Integration_20261018/Export/Nam_K54_v12_Game.fbx",receipt="SourceArt/K54_Integration_20261018/Export/export.json";
            public int frames=652,fps=60;public WeaponKind kind=WeaponKind.Pistol;public bool ownHands=true;
        }
        static Dictionary<string,Transform> Map(GameObject root)=>root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        static Matrix4x4 InHip(Transform hip,Transform bone)=>hip.worldToLocalMatrix*bone.localToWorldMatrix;
        static Map01PistolAnimationData.Pose Decompose(Matrix4x4 m)=>new(){position=m.GetColumn(3),rotation=m.rotation,scale=m.lossyScale};
        static bool Upper(string name)=>name.StartsWith("Spine")||name.StartsWith("Neck")||name.StartsWith("Head")||
            new[]{"Left","Right"}.Any(side=>name.StartsWith(side+"Shoulder")||name.StartsWith(side+"Arm")||name.StartsWith(side+"ForeArm")||name.StartsWith(side+"Hand"));
        static int Depth(Transform t){int n=0;while(t.parent!=null){t=t.parent;n++;}return n;}
        static int RestoreBindPose(GameObject actor)
        {
            // An FBX can display its first animation frame on instantiation. That is NOT the skin's rest pose.
            var rest=new Dictionary<Transform,Matrix4x4>();
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var poses=skin.sharedMesh.bindposes;
                for(int i=0;i<skin.bones.Length;i++)if(skin.bones[i]!=null&&!rest.ContainsKey(skin.bones[i]))
                    rest[skin.bones[i]]=skin.transform.localToWorldMatrix*poses[i].inverse;
            }
            foreach(var pair in rest.OrderBy(p=>Depth(p.Key)))
            {
                var local=pair.Key.parent.worldToLocalMatrix*pair.Value;
                Decompose(local).Apply(pair.Key);
            }
            return rest.Count;
        }
        static T Save<T>(T asset,string path) where T:Object
        {
            var previous=AssetDatabase.LoadAssetAtPath<T>(path);
            if(previous==null){AssetDatabase.CreateAsset(asset,path);return asset;}
            EditorUtility.CopySerialized(asset,previous);Object.DestroyImmediate(asset);EditorUtility.SetDirty(previous);return previous;
        }
        static Material Material(string name,Color color,float metal,float smooth,string root)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
            return Save(m,root+"/"+name+".mat");
        }
        [MenuItem("ShadowVale/K54/Build approved assets")]
        public static void Build()=>Bake(new Spec());
        [MenuItem("ShadowVale/Mosin/Build motion assets")]
        public static void BuildMosin()=>Bake(new Spec{root="Assets/_Project/Art/Mosin_v13",data="Assets/_Project/Map01/Resources/Weapons/MosinV13.asset",work="Docs/Reviews/Mosin_Integration_20261018",sourceRoot="Mosin_Player",weapon="rifle_mosin",fbx="SourceArt/Mosin_Integration_20261018/Export/Nam_Mosin_v13_Game.fbx",receipt="SourceArt/Mosin_Integration_20261018/Export/export.json",frames=589,fps=60,kind=WeaponKind.Sniper,ownHands=false});
        public static void BuildFinal(){BuildMosin();AuditK54Mechanics();AuditMosinMechanics();}
        public static void AuditMosinMechanics()
        {
            var data=AssetDatabase.LoadAssetAtPath<Map01PistolAnimationData>("Assets/_Project/Map01/Resources/Weapons/MosinV13.asset");
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));GameObject gun=null;
            try
            {
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                actor.GetComponentInChildren<Animator>().enabled=false;var map=Map(actor);gun=Object.Instantiate(data.prefab,map["RightHand"],false);
                var parts=data.parts.Select(t=>gun.transform.Find(t.name)).ToArray();var round=parts.First(t=>t!=null&&t.name=="Cartridge");var rows=new List<string>();int checkedRounds=0;
                for(int f=1;f<=data.frames;f++)
                {
                    if(data.roundVisible==null||!data.roundVisible[f-1]||f%10!=0)continue;
                    foreach(var track in data.bones)track.At(f).Apply(map[track.name]);data.weapon.At(f).Apply(gun.transform);
                    for(int i=0;i<parts.Length;i++)data.parts[i].At(f).Apply(parts[i]);
                    var pinch=(map["RightHandIndex3"].position+map["RightHandThumb3"].position)*.5f;
                    float distance=Vector3.Distance(round.position,pinch);rows.Add(f+" cartridgeToPinch="+distance);
                    if(distance>.1f)throw new Exception("Mosin cartridge detached from the loading hand at frame"+f);
                    checkedRounds++;
                }
                if(checkedRounds==0)throw new Exception("Mosin cartridge never appears in the loading take.");
                var bolt=data.parts.First(t=>t.name=="bolt"||t.name.EndsWith("/bolt"));var first=bolt.At(189);float motion=0;
                for(int f=190;f<=249;f++)motion=Mathf.Max(motion,Vector3.Distance(first.position,bolt.At(f).position)+Quaternion.Angle(first.rotation,bolt.At(f).rotation)*Mathf.Deg2Rad);
                if(motion<.001f)throw new Exception("Mosin bolt is static during the cycle.");
                rows.Add("boltMotion="+motion);File.WriteAllText("Docs/Reviews/Mosin_Integration_20261018/mechanics_audit.txt",string.Join("\n",rows));Debug.Log("MOSIN_MECHANICS_AUDIT_PASS");
            }
            finally{if(gun!=null)Object.DestroyImmediate(gun);Object.DestroyImmediate(actor);}
        }
        public static void AuditK54Mechanics()
        {
            var data=AssetDatabase.LoadAssetAtPath<Map01PistolAnimationData>(DataPath);var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));GameObject gun=null;
            try
            {
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                actor.GetComponentInChildren<Animator>().enabled=false;var map=Map(actor);gun=Object.Instantiate(data.prefab,map["RightHand"],false);
                var parts=data.parts.Select(t=>gun.transform.Find(t.name)).ToArray();var magazine=parts.First(t=>t!=null&&t.name=="magazine");var renderer=magazine.GetComponentInChildren<Renderer>(true);
                var rows=new List<string>();
                foreach(int f in new[]{1,335,353,360,375,390,400,413,420})
                {
                    foreach(var track in data.bones)track.At(f).Apply(map[track.name]);data.weapon.At(f).Apply(gun.transform);
                    for(int i=0;i<parts.Length;i++)data.parts[i].At(f).Apply(parts[i]);
                    bool visible=data.magazineVisible[f-1];float distance=Vector3.Distance(renderer.bounds.center,map["LeftHand"].position);
                    rows.Add(f+" visible="+visible+" renderer="+renderer.enabled+" size="+renderer.bounds.size+" leftWristDistance="+distance);
                    if(renderer.bounds.size.magnitude<.02f)throw new Exception("K54 magazine is exported at an invisible scale.");
                    if(f>=335&&f<=400&&visible&&distance>.2f)throw new Exception("K54 magazine detached from the carrying hand at frame"+f);
                }
                File.WriteAllText(Work+"/magazine_world_audit.txt",string.Join("\n",rows));Debug.Log("K54_MAGAZINE_WORLD_AUDIT_PASS");
            }
            finally{if(gun!=null)Object.DestroyImmediate(gun);Object.DestroyImmediate(actor);}
        }
        static void Bake(Spec spec)
        {
            string Root=spec.root,Model=Root+"/"+Path.GetFileName(spec.fbx),DataPath=spec.data,Work=spec.work;
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before baking assets.");
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.GetDirectoryName(DataPath));Directory.CreateDirectory(Work);
            File.Copy(spec.fbx,Model,true);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.motionNodeName="Player_Rig";
            importer.importAnimation=true;importer.importVisibility=false;importer.importCameras=false;importer.importLights=false;importer.optimizeGameObjects=false;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.isReadable=true;importer.skinWeights=ModelImporterSkinWeights.Standard;
            string take=importer.defaultClipAnimations.First().takeName;
            importer.clipAnimations=new[]{new ModelImporterClipAnimation{name=spec.weapon+"_Source60",takeName=take,firstFrame=0,lastFrame=spec.frames-1,keepOriginalOrientation=true,keepOriginalPositionXZ=true,keepOriginalPositionY=true,lockRootRotation=true,lockRootPositionXZ=true,lockRootHeightY=true}};
            importer.SaveAndReimport();
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            try
            {
                foreach(var behaviour in target.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
                var sa=source.GetComponent<Animator>();if(sa!=null)sa.enabled=false;
                var ta=target.GetComponentInChildren<Animator>();ta.enabled=false;
                int sourceBindBones=RestoreBindPose(source),targetBindBones=RestoreBindPose(target);
                var sm=Map(source);var tm=Map(target);var sh=sm["Hips"];var th=tm["Hips"];
                var sourceRest=sm.Values.ToDictionary(t=>t,t=>Map01PistolAnimationData.Pose.Read(t));
                var upper=sm.Keys.Where(n=>tm.ContainsKey(n)&&Upper(n)).OrderBy(n=>Depth(sm[n])).ToArray();
                // FBX bone-local units can be centimetres although world-space mesh sizes match.
                // Convert both rest AND pose matrices, not just positions after retargeting.
                float sourceLength=Vector3.Distance(InHip(sh,sm["LeftForeArm"]).GetColumn(3),InHip(sh,sm["LeftHand"]).GetColumn(3));
                float targetLength=Vector3.Distance(InHip(th,tm["LeftForeArm"]).GetColumn(3),InHip(th,tm["LeftHand"]).GetColumn(3));
                Quaternion Basis(Transform hip,Dictionary<string,Transform> map)
                {
                    Vector3 up=InHip(hip,map["Neck"]).GetColumn(3);
                    Vector3 right=(Vector3)InHip(hip,map["RightArm"]).GetColumn(3)-(Vector3)InHip(hip,map["LeftArm"]).GetColumn(3);
                    return Quaternion.LookRotation(Vector3.Cross(right,up).normalized,up.normalized);
                }
                float unit=targetLength/sourceLength;var alignment=Basis(th,tm)*Quaternion.Inverse(Basis(sh,sm));
                var units=Matrix4x4.Rotate(alignment)*Matrix4x4.Scale(Vector3.one*unit);
                var sr=upper.ToDictionary(n=>n,n=>units*InHip(sh,sm[n])*units.inverse);var tr=upper.ToDictionary(n=>n,n=>InHip(th,tm[n]));
                var data=ScriptableObject.CreateInstance<Map01PistolAnimationData>();data.name=Path.GetFileNameWithoutExtension(DataPath);data.frames=spec.frames;data.fps=spec.fps;
                var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(spec.receipt));data.sourceHash=receipt.source_sha256;data.magazineVisible=receipt.magazine_visible;data.roundVisible=receipt.round_visible;
                data.bones=upper.Select(n=>new Map01PistolAnimationData.Track{name=n,poses=new Map01PistolAnimationData.Pose[spec.frames]}).ToArray();
                var root=sm[spec.sourceRoot];
                var children=root.GetComponentsInChildren<Transform>(true).Where(t=>t!=root).ToArray();
                if(spec.kind==WeaponKind.Pistol&&!children.Any(t=>t.name=="magazine"))throw new Exception("Detached K54 magazine is missing from the exported hierarchy.");
                if(spec.kind==WeaponKind.Sniper&&(!children.Any(t=>t.name=="bolt")||!children.Any(t=>t.name=="Cartridge")))throw new Exception("Mosin bolt or cartridge is missing from the exported hierarchy: "+string.Join(",",children.Select(t=>t.name)));
                data.parts=children.Select(t=>new Map01PistolAnimationData.Track{name=AnimationUtility.CalculateTransformPath(t,root),poses=new Map01PistolAnimationData.Pose[spec.frames]}).ToArray();
                data.weapon=new Map01PistolAnimationData.Track{name=spec.sourceRoot,poses=new Map01PistolAnimationData.Pose[spec.frames]};
                var clip=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                var correction=tr["RightHand"].inverse*units*InHip(sh,sm["RightHand"]);
                float scaleError=0,restPositionError=upper.Max(n=>Vector3.Distance(sr[n].GetColumn(3),tr[n].GetColumn(3)));
                File.WriteAllText(Work+"/rig_alignment.txt","bindBones="+sourceBindBones+"/"+targetBindBones+" unit="+unit+" axes="+alignment.eulerAngles+"\n"+string.Join("\n",upper.Select(n=>n+" source="+sr[n].GetColumn(3)+" target="+tr[n].GetColumn(3)+" error="+Vector3.Distance(sr[n].GetColumn(3),tr[n].GetColumn(3)))));
                for(int f=0;f<spec.frames;f++)
                {
                    clip.SampleAnimation(source,f/(float)spec.fps);
                    var posed=upper.ToDictionary(n=>n,n=>units*InHip(sh,sm[n])*units.inverse*sr[n].inverse*tr[n]);
                    for(int i=0;i<upper.Length;i++)
                    {
                        string n=upper[i],parent=sm[n].parent.name;
                        Matrix4x4 parentPose=posed.TryGetValue(parent,out var p)?p:Matrix4x4.identity;
                        var pose=Decompose(parentPose.inverse*posed[n]);data.bones[i].poses[f]=pose;
                        scaleError=Mathf.Max(scaleError,(pose.scale-tm[n].localScale).magnitude);
                    }
                    data.weapon.poses[f]=Decompose(correction*sm["RightHand"].worldToLocalMatrix*root.localToWorldMatrix);
                    for(int i=0;i<children.Length;i++)data.parts[i].poses[f]=Map01PistolAnimationData.Pose.Read(children[i]);
                }
                foreach(var entry in sourceRest)entry.Value.Apply(entry.Key);
                // Extract only the prop. Each part retains its own original mesh and local animation space.
                var prop=Object.Instantiate(root.gameObject);prop.name=spec.weapon+"_animated";prop.transform.SetParent(null,false);
                prop.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);prop.transform.localScale=Vector3.one;
                var available=AssetDatabase.FindAssets("t:Material",new[]{"Assets/_Project/Art/Environment/Map01Items"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
                var metal=Material("Blued steel",new Color(.055f,.065f,.07f),.8f,.52f,Root);
                Material Resolve(Material m)
                {
                    if(m==null)return metal;
                    if(available.TryGetValue(m.name,out var existing))return existing;
                    string clean=System.Text.RegularExpressions.Regex.Replace(m.name,@"\.\d{3}$","");
                    if(available.TryGetValue(clean,out existing))return existing;
                    if(clean.IndexOf("bakelite",StringComparison.OrdinalIgnoreCase)>=0)return Material("K54 brown grip",new Color(.18f,.065f,.025f),0,.3f,Root);
                    if(clean.IndexOf("black",StringComparison.OrdinalIgnoreCase)>=0||clean.StartsWith("PROP_Flat"))return Material("K54 black details",new Color(.008f,.01f,.012f),.25f,.2f,Root);
                    if(m.name.StartsWith("Cartridge brass"))return Material("Cartridge brass",new Color(.48f,.29f,.07f),.7f,.35f,Root);
                    if(m.name.StartsWith("Cartridge copper"))return Material("Cartridge copper",new Color(.34f,.13f,.045f),.7f,.35f,Root);
                    return metal;
                }
                foreach(var renderer in prop.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(Resolve).ToArray();
                    renderer.enabled=true;renderer.forceRenderingOff=false;
                }
                var profile=JsonUtility.FromJson<ForestBundle>(File.ReadAllText("Assets/_Project/Map01/Generated/Map01Content.json")).weapons.First(w=>w.id==spec.weapon);
                var tips=prop.GetComponentsInChildren<Transform>(true);
                var tip=tips.First(t=>t.name.EndsWith("_Muzzle"));
                var weapon=prop.AddComponent<Weapon>();weapon.Configure(spec.kind,profile.damage,profile.range,profile.fire_rate,false,tip,lethalHits:spec.kind==WeaponKind.Sniper);
                data.prefab=PrefabUtility.SaveAsPrefabAsset(prop,Root+"/"+(spec.ownHands?"K54_v12":"Mosin_v13")+".prefab");Object.DestroyImmediate(prop);
                // Rebind improved hand meshes to the same 62-bone skeleton, without importing another character.
                var hands=new List<Map01PistolAnimationData.Hand>();
                if(spec.ownHands){
                data.skin=Material("Nam approved smooth hands",new Color(.41f,.255f,.165f),0,.28f,Root);
                foreach(string side in new[]{"Left","Right"})
                {
                    string name="Player_"+side+"Hand";var skin=sm[name].GetComponent<SkinnedMeshRenderer>();var dst=tm[name].GetComponent<SkinnedMeshRenderer>();
                    var mesh=Object.Instantiate(skin.sharedMesh);mesh.name=name+"_K54v12";
                    var matrix=dst.transform.worldToLocalMatrix*th.localToWorldMatrix*units*sh.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                    var vertices=mesh.vertices;var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;var tangents=mesh.tangents;
                    for(int i=0;i<vertices.Length;i++){vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;if(tangents.Length>i){var v=matrix.MultiplyVector(tangents[i]);tangents[i]=new Vector4(v.x,v.y,v.z,tangents[i].w);}}
                    mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;
                    mesh.bindposes=skin.bones.Select(b=>tm[b.name].worldToLocalMatrix*dst.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
                    var saved=Save(mesh,Root+"/"+name+".asset");
                    hands.Add(new Map01PistolAnimationData.Hand{name=name,mesh=saved,bones=skin.bones.Select(b=>b.name).ToArray(),rootBone=skin.rootBone.name,bounds=new Bounds(saved.bounds.center,Vector3.one*4)});
                }
                }else{var shared=AssetDatabase.LoadAssetAtPath<Map01PistolAnimationData>(K54IntegrationBuilder.DataPath);data.skin=shared.skin;hands=shared.hands.ToList();}
                data.hands=hands.ToArray();Save(data,DataPath);AssetDatabase.SaveAssets();
                File.WriteAllText(Work+"/unity_bake_report.txt","Baked "+spec.weapon+"\nupperBones="+upper.Length+" frames="+spec.frames+" fps="+spec.fps+"\nsourceClipLength="+clip.length+" unitConversion="+unit+"\nmaxRestPositionDifference="+restPositionError+" maxLocalScaleDifference="+scaleError+"\nhandTriangles="+hands.Sum(h=>h.mesh.triangles.Length/3)+"\nsource="+data.sourceHash);
                if(restPositionError>.01f||scaleError>.01f)throw new Exception("Rig rest alignment needs review; see bake report");
                Debug.Log(spec.weapon+"_BAKE_PASS");
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(target);}
        }
    }
}
