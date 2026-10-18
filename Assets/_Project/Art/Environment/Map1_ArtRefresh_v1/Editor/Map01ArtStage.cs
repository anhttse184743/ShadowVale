using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    /// <summary>Build and inspect art in a separate Map 1 copy before gameplay integration.</summary>
    public static class Map01ArtStage
    {
        public const string Root=Map01EnvironmentReview.Root, Work=Map01EnvironmentReview.Work;
        public const string Stage=Root+"/Map1_ArtStage.unity";
        [Serializable] public class MatSpec { public string name,albedo,normal,mask; public float metallic,smoothness; }
        [Serializable] public class Mats { public MatSpec[] items; }
        [Serializable] public class ColSpec { public string name; public float[] size; }
        [Serializable] public class BuildingSpec { public string name; public float width,depth; }
        [Serializable] public class ModelSpec { public string name; public ColSpec[] colliders; public BuildingSpec[] buildings; }
        [Serializable] public class Models { public ModelSpec[] items; }
        [Serializable] public class Pad { public string building; public Vector3 center,u,v; public float width,depth,height; }
        [Serializable] public class StageReport { public string status,scene; public Pad[] pads; public string[] notes; }
        static readonly Vector3[] Fronts={new Vector3(-.8f,0,.6f),new Vector3(-.5f,0,-.866f),Vector3.back};
        static int meshSerial;

        [MenuItem("ShadowVale/Art Review/Build detailed art stage copy")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play mode before building an art stage.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(Root+"/StageData/Meshes");Directory.CreateDirectory(Root+"/Materials");
            Directory.CreateDirectory(Root+"/StageData/PlatformBatches_v6");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var mats=MakeMaterials();
            var specs=JsonUtility.FromJson<Models>(File.ReadAllText(Root+"/StageData/models.json")).items;
            var scene=EditorSceneManager.OpenScene(Map01EnvironmentReview.MapPath);
            if(!EditorSceneManager.SaveScene(scene,Stage,true))throw new Exception("Could not create art stage copy.");
            scene=EditorSceneManager.OpenScene(Stage);
            var original=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var ground=original.Single(t=>t.name=="Continuous hills — 220 x 200").GetComponent<MeshFilter>();
            var groundCollider=ground.GetComponent<MeshCollider>();
            var terrain=Object.Instantiate(ground.sharedMesh);terrain.name="Map 1 art stage hills";
            SaveMesh(terrain,Root+"/StageData/StageTerrain.asset");
            ground.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/StageData/StageTerrain.asset");
            groundCollider.sharedMesh=ground.sharedMesh;
            var stageRoot=new GameObject("MAP 1 ART STAGE / gameplay source preserved").transform;
            var roots=new List<GameObject>();var pads=new List<Pad>();
            for(int i=0;i<3;i++)
            {
                string code="C"+(i+1);var spec=specs[i];
                var anchor=original.Single(t=>t.name==code+"_MissionAnchor");
                var camp=original.Single(t=>t.name=="Enemy outpost "+(i+1));
                foreach(Transform child in camp)if(child.name.StartsWith("Compact field camp"))child.gameObject.SetActive(false);
                string file=Root+"/Meshes/"+spec.name+".fbx";
                var imp=(ModelImporter)AssetImporter.GetAtPath(file);imp.importCameras=false;imp.importLights=false;imp.importAnimation=false;
                imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.SaveAndReimport();
                var wrapper=new GameObject(code+" / Mission art");wrapper.transform.SetParent(stageRoot);wrapper.transform.position=anchor.position;
                // Small offsets keep the legacy chest position reachable without changing runtime loot rules.
                if(i<2)wrapper.transform.position+=Fronts[i].normalized*(i==0?2.2f:1.5f);
                else{var position=wrapper.transform.position;position.y=8.25f;wrapper.transform.position=position;}
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(file));
                model.transform.SetParent(wrapper.transform,false);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var marker=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("ENTRY_FORWARD",StringComparison.Ordinal));
                var direction=marker.position-wrapper.transform.position;direction.y=0;
                wrapper.transform.rotation=Quaternion.Euler(0,Vector3.SignedAngle(direction,Fronts[i],Vector3.up),0);
                foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterials=r.sharedMaterials.Select(m=>mats[m.name.Replace(" (Instance)","")]).ToArray();
                    r.shadowCastingMode=ShadowCastingMode.TwoSided;
                }
                if(i==2)
                {
                    pads.Add(new Pad{building="Main garrison common platform",center=new Vector3(anchor.position.x,8.25f,anchor.position.z),u=Vector3.right,v=Vector3.forward,width=40,depth=35,height=8.25f});
                    foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Perimeter /")))t.gameObject.SetActive(false);
                    foreach(var b in spec.buildings??Array.Empty<BuildingSpec>())
                    {
                        var t=model.GetComponentsInChildren<Transform>(true).Single(x=>x.name==b.name);
                        var x=t.Cast<Transform>().Single(a=>a.name.StartsWith("PAD_AXIS_X"));
                        var y=t.Cast<Transform>().Single(a=>a.name.StartsWith("PAD_AXIS_Y"));
                        var u=(x.position-t.position).normalized;var v=(y.position-t.position).normalized;
                        float porch=b.name.Contains("command house")?2:b.name.Contains("quarters")?1.2f:b.name.Contains("Aid post")?1:0;
                        var center=t.position-v*porch/2;float width=b.width+.7f,depth=b.depth+porch+.7f;
                        float high=float.MinValue;
                        for(int xx=0;xx<5;xx++)for(int yy=0;yy<5;yy++)
                            high=Mathf.Max(high,GroundAt(groundCollider,center+u*((xx/4f-.5f)*width)+v*((yy/4f-.5f)*depth)));
                        float height=8.25f;
                        var p=t.position;p.y=height;t.position=p;center.y=height;
                        pads.Add(new Pad{building=b.name,center=center,u=u,v=v,width=width,depth=depth,height=height});
                    }
                    var checkpoint=model.GetComponentsInChildren<Transform>(true).First(t=>t.name.StartsWith("Checkpoint and duty post"));
                    var gateHut=spec.buildings.Where(b=>b.name.StartsWith("Gate duty shelter")).Select(b=>model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==b.name)).ToArray();
                    var hutPositions=gateHut.Select(t=>t.position).ToArray();
                    float gateHeight=8.25f;
                    var gatePosition=checkpoint.position;gatePosition.y=gateHeight;checkpoint.position=gatePosition;
                    for(int h=0;h<gateHut.Length;h++)gateHut[h].position=hutPositions[h];
                    pads.Add(new Pad{building="Main gate approach",center=gatePosition,u=wrapper.transform.right,v=wrapper.transform.forward,width=5.8f,depth=2.6f,height=gateHeight});
                    var tower=model.GetComponentsInChildren<Transform>(true).First(t=>t.name.StartsWith("Timber observation tower"));
                    tower.Rotate(Vector3.up,180,Space.World);
                    var towerPosition=tower.position;towerPosition.y=8.25f;tower.position=towerPosition;
                    pads.Add(new Pad{building="Observation tower and north-facing stairs",center=towerPosition+Vector3.forward*1.7f,u=Vector3.right,v=Vector3.forward,width=3.5f,depth=8.5f,height=towerPosition.y});
                    ApplyPads(ground,pads);groundCollider.sharedMesh=null;groundCollider.sharedMesh=ground.sharedMesh;Physics.SyncTransforms();
                    foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Sandbag revetment")||t.name.StartsWith("Generator under weather")))
                    {var p=t.position;p.y=GroundAt(groundCollider,p);t.position=p;}
                    int gateMesh=0;
                    foreach(var r in checkpoint.GetComponentsInChildren<MeshRenderer>().Where(r=>r.transform.parent==checkpoint||r.transform.parent.name.StartsWith("Raised gate boom")))
                    {
                        string collisionPath=Root+"/StageData/Meshes/GateCollision_"+(gateMesh++)+".asset";
                        SaveMesh(Object.Instantiate(r.GetComponent<MeshFilter>().sharedMesh),collisionPath);
                        r.gameObject.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);
                    }
                }
                var transforms=model.GetComponentsInChildren<Transform>(true);
                foreach(var c in spec.colliders??Array.Empty<ColSpec>())
                {
                    if(i==2&&(c.name.Contains("SideFence")||c.name.Contains("FrontFence")||c.name.Contains("RearFence")))continue;
                    var t=transforms.Single(x=>x.name==c.name);var box=t.gameObject.AddComponent<BoxCollider>();var scale=t.lossyScale;
                    box.size=new Vector3(c.size[0]/Mathf.Abs(scale.x),c.size[1]/Mathf.Abs(scale.y),c.size[2]/Mathf.Abs(scale.z));
                }
                AddFurnitureCollisions(model);
                if(i==2)BuildFence(wrapper.transform,groundCollider,mats["RG_WeatheredTimber"],mats["RG_BlackSteel"]);
                CombineForLods(wrapper,code,spec);
                roots.Add(wrapper);
            }
            // Lighting belongs to the stage scene; shared renderer and quality assets are never edited.
            var sun=original.SelectMany(t=>t.GetComponents<Light>()).First(l=>l.type==LightType.Directional);
            sun.intensity=1.8f;sun.color=new Color(1,.95f,.85f);sun.shadows=LightShadows.Soft;
            var fill=new GameObject("Art stage sky bounce").AddComponent<Light>();fill.transform.SetParent(stageRoot);fill.type=LightType.Directional;
            fill.intensity=.4f;fill.color=new Color(.76f,.85f,1);fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(55,140,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.69f,.74f);
            RenderSettings.ambientEquatorColor=new Color(.53f,.59f,.51f);RenderSettings.ambientGroundColor=new Color(.32f,.30f,.24f);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.59f,.63f,.56f));RenderSettings.ambientProbe=ambient;
            Physics.SyncTransforms();
            ClearArtOverlaps(original,roots);
            var originalGround=new GameObject("Temporary original ground sampler").AddComponent<MeshCollider>();
            originalGround.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Scenes/Maps/Map 1/Terrain.asset");originalGround.transform.SetPositionAndRotation(ground.transform.position,ground.transform.rotation);originalGround.transform.localScale=ground.transform.lossyScale;
            foreach(var t in original.Where(t=>IsLandscapeRoot(t)||(t.TryGetComponent<ShadowVale.Map01.ForestPoint>(out var point)&&point.kind==ShadowVale.Map01.ForestPointKind.Hide)))
            {var p=t.position;float change=GroundAt(groundCollider,p)-GroundAt(originalGround,p);if(Mathf.Abs(change)>.001f){p.y+=change;t.position=p;}}
            Object.DestroyImmediate(originalGround.gameObject);
            foreach(var c in original.SelectMany(t=>t.GetComponents<Camera>()))c.enabled=false;
            var camera=new GameObject("Art stage camera").AddComponent<Camera>();camera.transform.SetParent(stageRoot);camera.nearClipPlane=.1f;camera.farClipPlane=350;
            camera.aspect=16f/9f;camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var actorRenderers=original.SelectMany(t=>t.GetComponents<Animator>()).SelectMany(a=>a.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var enabled=actorRenderers.Select(r=>r.enabled).ToArray();for(int i=0;i<actorRenderers.Length;i++)actorRenderers[i].enabled=false;
            foreach(var go in roots)
            {
                string code=go.name.Substring(0,2);ViewCamera(camera,go.transform,code,false);Capture(camera,Work+"/Previews/Unity_"+code+"_ArtStage.png");
            }
            for(int i=0;i<actorRenderers.Length;i++)actorRenderers[i].enabled=enabled[i];
            ViewCamera(camera,roots[0].transform,"C1",false);
            EditorSceneManager.SaveScene(scene,Stage);
            File.WriteAllText(Work+"/Audit/art_stage_report.json",JsonUtility.ToJson(new StageReport{scene=Stage,status="Art stage only. Original Map 1 not saved. Navigation verification and gameplay integration still pending.",pads=pads.ToArray(),notes=new[]{"Legacy enemy counts and mission rules preserved.","C1/C2 placement shifted 1.5 m within their existing pads for chest clearance.","C3 terrain terraces are local to the stage mesh.","Two levels of static material batches reduce renderer count."}},true));
            EditorApplication.ExecuteMenuItem("Window/General/Game");Debug.Log("MAP ART STAGE COMPLETE");
        }
        static Dictionary<string,Material> MakeMaterials()
        {
            var result=new Dictionary<string,Material>();
            foreach(var spec in JsonUtility.FromJson<Mats>(File.ReadAllText(Root+"/StageData/materials.json")).items)
            {
                ConfigureTexture(spec.albedo,false,false);ConfigureTexture(spec.normal,true,false);ConfigureTexture(spec.mask,false,true);
                string path=Root+"/Materials/"+spec.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                m.name=spec.name;m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(spec.albedo));
                m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(spec.normal));m.SetFloat("_BumpScale",.5f);
                m.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(spec.mask));m.SetFloat("_Smoothness",1);m.SetFloat("_Metallic",spec.metallic);
                m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Cull",0);m.enableInstancing=true;
                EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);result.Add(m.name,m);
            }
            return result;
        }
        static void ConfigureTexture(string path,bool normal,bool linear)
        {
            if(!path.StartsWith(Root+"/",StringComparison.Ordinal))return;
            var t=(TextureImporter)AssetImporter.GetAtPath(path);var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            if(t.textureType==type&&t.sRGBTexture==(!normal&&!linear)&&t.maxTextureSize==2048&&t.textureCompression==TextureImporterCompression.CompressedHQ&&t.mipmapEnabled)return;
            t.textureType=type;t.sRGBTexture=!normal&&!linear;t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.CompressedHQ;t.mipmapEnabled=true;t.anisoLevel=4;t.SaveAndReimport();
        }
        static float GroundAt(MeshCollider ground,Vector3 position)
        {
            return ground.Raycast(new Ray(new Vector3(position.x,100,position.z),Vector3.down),out var hit,200)?hit.point.y:position.y;
        }
        public static void ApplyPads(MeshFilter ground,List<Pad> pads)
        {
            var mesh=ground.sharedMesh;var vs=mesh.vertices;
            for(int i=0;i<vs.Length;i++)
            {
                var p=ground.transform.TransformPoint(vs[i]);
                Pad containing=null;float nearest=float.MaxValue;
                foreach(var pad in pads)
                {
                    var delta=p-pad.center;delta.y=0;float dx=Mathf.Abs(Vector3.Dot(delta,pad.u))-pad.width/2;
                    float dz=Mathf.Abs(Vector3.Dot(delta,pad.v))-pad.depth/2;
                    if(Mathf.Max(dx,dz)<=0&&delta.sqrMagnitude<nearest){containing=pad;nearest=delta.sqrMagnitude;}
                }
                foreach(var pad in containing==null?pads:new List<Pad>{containing})
                {
                    var delta=p-pad.center;delta.y=0;float dx=Mathf.Abs(Vector3.Dot(delta,pad.u))-pad.width/2;
                    float dz=Mathf.Abs(Vector3.Dot(delta,pad.v))-pad.depth/2;float outside=Mathf.Max(dx,dz);
                    float blend=Mathf.Max(2.4f,Mathf.Abs(pad.height-p.y)*1.8f);
                    if(outside>=blend)continue;
                    p.y=Mathf.Lerp(pad.height,p.y,Mathf.SmoothStep(0,1,Mathf.Max(0,outside)/blend));
                }
                vs[i]=ground.transform.InverseTransformPoint(p);
            }
            mesh.vertices=vs;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
        }
        static void AddFurnitureCollisions(GameObject model)
        {
            foreach(var t in model.GetComponentsInChildren<Transform>(true))
            {
                if(!new[]{"Supply crate","200 litre steel drum","Tied ration sack","Berth ","Work table","Mess table"}.Any(n=>t.name.StartsWith(n,StringComparison.Ordinal)))continue;
                if(t.GetComponentsInChildren<Collider>(true).Length>0)continue;
                var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;
                bool first=true;var bounds=new Bounds();
                foreach(var r in rs)foreach(var corner in Corners(r.localBounds))
                {var p=t.InverseTransformPoint(r.transform.TransformPoint(corner));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                var box=t.gameObject.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
            }
        }
        static IEnumerable<Vector3> Corners(Bounds b)
        {for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)yield return b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));}
        static void CombineForLods(GameObject model,string code,ModelSpec spec)
        {
            var rs=model.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            var buildings=new HashSet<string>((spec.buildings??Array.Empty<BuildingSpec>()).Select(b=>b.name));
            string Building(Transform t)
            {while(t!=null&&t!=model.transform){if(buildings.Contains(t.name)||t.name.StartsWith(code+" /"))return t.name;t=t.parent;}return "Exterior";}
            bool Roof(Transform t){while(t!=null&&t!=model.transform){if(t.name.StartsWith("ROOF /"))return true;t=t.parent;}return false;}
            var near=new List<Renderer>();var far=new List<Renderer>();meshSerial=0;
            AssetDatabase.StartAssetEditing();try{for(int level=0;level<2;level++)
            {
                var parent=new GameObject("LOD"+level+" / static material batches").transform;parent.SetParent(model.transform,false);
                var source=level==0?rs:rs.Where(r=>
                    (Roof(r.transform)?r.sharedMaterials.Any(m=>m.name=="RG_PalmOld"||m.name=="RG_CorrugatedZinc"):
                    r.transform.parent==model.transform||r.transform.parent.name.StartsWith(code+" /")||buildings.Contains(r.transform.parent.name)||r.transform.parent.name.StartsWith("Perimeter /")||r.transform.parent.name.StartsWith("Timber observation tower")||r.transform.parent.name.StartsWith("Checkpoint and duty post"))).ToArray();
                var entries=source.SelectMany(r=>r.sharedMaterials.Select((m,index)=>new {renderer=r,material=m,index,roof=Roof(r.transform),building=Building(r.transform)}));
                foreach(var group in entries.GroupBy(e=>(level==0?e.building:"All")+"|"+e.roof+"|"+e.material.name))
                {
                    var first=group.First();var combines=group.Select(e=>new CombineInstance{mesh=e.renderer.GetComponent<MeshFilter>().sharedMesh,subMeshIndex=e.index,transform=model.transform.worldToLocalMatrix*e.renderer.transform.localToWorldMatrix}).ToArray();
                    var mesh=new Mesh{name=code+"_LOD"+level+"_"+(meshSerial++),indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines,true,true);mesh.RecalculateBounds();
                    string meshName=mesh.name,path=Root+"/StageData/PlatformBatches_v6/"+meshName+".asset";var stored=SaveMesh(mesh,path);
                    var go=new GameObject((first.roof?"ROOF / ":"Solid / ")+meshName);go.transform.SetParent(parent,false);
                    go.AddComponent<MeshFilter>().sharedMesh=stored;
                    var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=first.material;r.shadowCastingMode=ShadowCastingMode.TwoSided;
                    if(level==1&&first.roof)
                    {
                        go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
                        go.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
                    }
                    (level==0?near:far).Add(r);
                }
            }}finally{AssetDatabase.StopAssetEditing();}
            // Derived batches own the rendered geometry; source components must not retain duplicate FBX mesh data in a build.
            foreach(var r in rs){Object.DestroyImmediate(r.GetComponent<MeshFilter>());Object.DestroyImmediate(r);}
            var lod=model.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.CrossFade;lod.animateCrossFading=true;
            lod.SetLODs(new[]{new LOD(.18f,near.ToArray()),new LOD(.008f,far.ToArray())});lod.RecalculateBounds();
            Debug.Log(code+" renderer batches: "+near.Count+" near, "+far.Count+" far. Original mesh renderers: "+rs.Length);
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            if(!path.StartsWith(Root+"/",StringComparison.Ordinal))throw new Exception("Mesh write outside art stage.");
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            else{EditorUtility.CopySerialized(mesh,old);old.vertices=mesh.vertices;old.RecalculateBounds();EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);Object.DestroyImmediate(mesh);return old;}
        }
        static void BuildFence(Transform camp,MeshCollider ground,Material wood,Material metal)
        {
            var root=new GameObject("C3 / perimeter following surveyed terrain").transform;root.SetParent(camp,false);
            void Segment(Vector3 a,Vector3 b)
            {
                a=camp.position+a;b=camp.position+b;int n=Mathf.CeilToInt(Vector3.Distance(a,b)/2.1f);
                Vector3 previous=default;
                for(int j=0;j<=n;j++)
                {
                    var p=Vector3.Lerp(a,b,j/(float)n);p.y=GroundAt(ground,p);
                    Cube("Fence upright",p+Vector3.up*.75f,new Vector3(.08f,1.5f,.08f),Quaternion.identity,wood,root,false);
                    if(j>0)
                    {
                        var midpoint=(p+previous)/2;var delta=p-previous;
                        for(int k=0;k<3;k++)Cube("Perimeter strand",midpoint+Vector3.up*(.45f+k*.36f),new Vector3(.012f,.012f,delta.magnitude),Quaternion.LookRotation(delta),metal,root,false);
                        var collision=new GameObject("Fence collision").transform;collision.SetParent(root);collision.position=midpoint+Vector3.up*.65f;
                        collision.rotation=Quaternion.LookRotation(new Vector3(delta.x,0,delta.z));var box=collision.gameObject.AddComponent<BoxCollider>();box.size=new Vector3(.12f,1.3f+Mathf.Abs(delta.y),new Vector2(delta.x,delta.z).magnitude);
                    }
                    previous=p;
                }
            }
            Segment(new Vector3(-18,0,-15.5f),new Vector3(-2.35f,0,-15.5f));Segment(new Vector3(2.35f,0,-15.5f),new Vector3(18,0,-15.5f));
            Segment(new Vector3(-18,0,-15.5f),new Vector3(-18,0,15.5f));Segment(new Vector3(18,0,-15.5f),new Vector3(18,0,15.5f));
            Segment(new Vector3(-18,0,15.5f),new Vector3(18,0,15.5f));
        }
        static void Cube(string name,Vector3 p,Vector3 size,Quaternion rotation,Material mat,Transform parent,bool collider)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.SetPositionAndRotation(p,rotation);go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static void ClearArtOverlaps(Transform[] original,List<GameObject> camps)
        {
            foreach(var t in original.Where(t=>t.GetComponent<LODGroup>()!=null))
            {
                if(!IsLandscapeRoot(t))continue;
                for(int i=0;i<camps.Count;i++)
                {
                    var delta=t.position-camps[i].transform.position;delta.y=0;float radius=i==2?23:11;
                    if(delta.magnitude<radius){t.gameObject.SetActive(false);break;}
                }
            }
        }
        static bool IsLandscapeRoot(Transform t)=>t.GetComponent<LODGroup>()!=null&&(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("/ForestV2/")||t.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh!=null&&f.sharedMesh.name.Contains("Boulder")));
        static void ViewCamera(Camera camera,Transform root,string code,bool plan)
        {
            var all=root.GetComponentsInChildren<Transform>(true);var key=code+(plan?"_Plan":"_Overview");
            var eye=all.First(t=>t.name=="SHOT_"+key+"_Eye");var target=all.First(t=>t.name=="SHOT_"+key+"_Target");
            camera.orthographic=true;camera.orthographicSize=code=="C3"?21:6.2f;
            camera.transform.SetPositionAndRotation(eye.position,Quaternion.LookRotation(target.position-eye.position,Vector3.up));
        }
        static void Capture(Camera camera,string path)
        {
            var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;for(int i=0;i<3;i++)camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
    }

    [InitializeOnLoad]
    static class Map01ArtStageRequest
    {
        const string Request=Map01ArtStage.Work+"/build_stage.request";
        const string Status=Map01ArtStage.Work+"/Audit/build_stage_status.txt";
        static double next;
        static Map01ArtStageRequest()
        {
            if(!Directory.Exists(Map01ArtStage.Work))return;
            // Optional review output may be absent on a fresh checkout. Never let
            // an I/O failure poison this InitializeOnLoad type or its update loop.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Status));
                File.WriteAllText(Map01ArtStage.Work+"/Audit/stage_tool_version.txt","28");
                EditorApplication.update+=Check;
            }
            catch(Exception e){Debug.LogWarning("Map 1 art review watcher disabled: "+e.Message);}
        }
        static void Check()
        {
            try{CheckRequest();}
            catch(Exception e)
            {
                EditorApplication.update-=Check;
                Debug.LogWarning("Map 1 art review watcher disabled: "+e.Message);
            }
        }
        static void CheckRequest()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            Directory.CreateDirectory(Path.GetDirectoryName(Status));
            string action=File.ReadAllText(Request).Trim().TrimStart('\uFEFF');
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(EditorApplication.isPlayingOrWillChangePlaymode||(scene.isDirty&&action=="BUILD")||!(scene.path.StartsWith(Map01ArtStage.Root+"/",StringComparison.Ordinal)||((action=="TUNE_STAGE"||action=="TUNE_GAMEPLAY"||action=="TESTS"||action=="TEST_CHEST"||action=="SYNC_ITEM_ART"||action=="BENCH_BUILD"||action=="BENCH_BUILD_EXIT")&&scene.path==Map01EnvironmentReview.MapPath)))
                {File.WriteAllText(Status,"WAITING: editor must be in a clean Map1 art review scene.");return;}
            }
            File.Move(Request,Request+"."+DateTime.UtcNow.Ticks+".consumed");
            EditorApplication.delayCall+=()=>
            {
                try
                {
                    File.WriteAllText(Status,"RUNNING "+action);
                    if(action=="LANDSCAPE")Map01LandscapeStage.Apply();
                    else if(action=="VALIDATE_NAV")Map01ArtStageValidation.Run();
                    else if(action=="CAPTURE")Map01LandscapeStage.Recapture();
                    else if(action=="DIAGNOSE")Map01ArtStageValidation.Diagnose();
                    else if(action=="REPAIR")Map01ArtStageRepair.Run();
                    else if(action=="INTEGRATE")Map01ArtDelivery.Integrate();
                    else if(action=="TESTS")Map01ArtDelivery.RunTests();
                    else if(action=="TEST_CHEST")Map01ArtDelivery.RunTests(true);
                    else if(action=="SYNC_ITEM_ART")Map01ArtDelivery.SyncExistingGrenadeArt();
                    else if(action=="TUNE_STAGE")Map01ArtPerformance.TuneStage();
                    else if(action=="TUNE_GAMEPLAY")Map01ArtPerformance.ApplyToGameplay();
                    else if(action=="BENCH_BUILD")Map01ArtDelivery.BuildBenchmark();
                    else if(action=="BENCH_BUILD_EXIT"){Map01ArtDelivery.BuildBenchmark();EditorApplication.delayCall+=()=>{if(Enumerable.Range(0,SceneManager.sceneCount).All(i=>!SceneManager.GetSceneAt(i).isDirty))EditorApplication.Exit(0);};}
                    else if(action=="BUILD")Map01ArtStage.Build();
                    else throw new Exception("Unknown stage request: "+action);
                    File.WriteAllText(Status,"COMPLETE "+DateTime.UtcNow.ToString("O"));
                }
                catch(Exception e)
                {
                    // Report the original operation error even if its output
                    // directory was removed while the delayed action was pending.
                    try{Directory.CreateDirectory(Path.GetDirectoryName(Status));File.WriteAllText(Status,"FAILED "+e);}
                    catch(Exception statusError){Debug.LogWarning("Could not write art review status: "+statusError.Message);}
                    Debug.LogException(e);
                }
            };
        }
    }
}
