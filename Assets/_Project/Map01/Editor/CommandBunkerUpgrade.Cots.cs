using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static partial class CommandBunkerUpgrade
    {
        static float CotHeadPlane(Transform shelter)
        {
            var wall=shelter.Find("ShelterShell").GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Side retaining wall").OrderBy(c=>c.bounds.center.x).First();
            var plane=wall.bounds.max.x;
            foreach(var c in shelter.GetComponentsInChildren<BoxCollider>())
            {
                if(!c.enabled||c.isTrigger||c.name.StartsWith("COLLIDER_Approved"))continue;
                var b=c.bounds;
                if(b.size.y>1&&b.size.x<2.5f&&b.size.z<2.5f&&b.min.x<wall.bounds.max.x+.8f&&b.max.x>wall.bounds.max.x&&b.max.x<wall.bounds.max.x+2.5f)plane=Mathf.Max(plane,b.max.x);
            }
            // Some visible structural columns have no collider. Measure their actual timber faces
            // at mattress height, including columns merged into a larger structural mesh.
            float height=shelter.Find("ShelterShell").GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Floor").bounds.max.y+.55f;
            foreach(var filter in shelter.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled||filter.sharedMesh==null)continue;
                if(PathOf(filter.transform).Contains("/Cot"))continue;
                if(!renderer.sharedMaterials.Where(m=>m!=null).Any(m=>m.name.IndexOf("walnut",StringComparison.OrdinalIgnoreCase)>=0||m.name.IndexOf("timber",StringComparison.OrdinalIgnoreCase)>=0||m.name.IndexOf("wood",StringComparison.OrdinalIgnoreCase)>=0))continue;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                for(int edge=0;edge<3;edge++)
                {
                    var a=vertices[triangles[i+edge]];var b=vertices[triangles[i+(edge+1)%3]];
                    if((a.y-height)*(b.y-height)>0||Mathf.Abs(a.y-b.y)<.00001f)continue;
                    var at=Vector3.Lerp(a,b,(height-a.y)/(b.y-a.y));
                    if(at.x>=wall.bounds.max.x-.3f&&at.x<=wall.bounds.max.x+1.5f&&Mathf.Abs(at.z-shelter.position.z)<5)plane=Mathf.Max(plane,at.x);
                }
            }
            File.WriteAllLines(Reports+"/cot_post_bounds.txt",shelter.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.bounds.center.x<wall.bounds.max.x+3&&c.bounds.size.y>1).Select(c=>PathOf(c.transform)+" "+c.bounds));
            return plane;
        }
        static void SpaceCotsForPlayer()
        {
            var cots=All<Transform>().Where(t=>t.name=="Cot"||t.name.StartsWith("Cot Row ")).OrderByDescending(t=>t.position.z).ToArray();
            if(cots.Length!=5)throw new Exception("Expected five cots");
            var shelter=cots[0].parent;var plane=CotHeadPlane(shelter);
            var template=All<CharacterController>().FirstOrDefault();
            float radius=template!=null?template.radius*Mathf.Max(template.transform.lossyScale.x,template.transform.lossyScale.z):.3f;
            float width=cots.Max(t=>CotVisualBounds(t).size.z);
            float gap=Mathf.Max(.9f,radius*2+.24f);float spacing=width+gap;
            for(int i=0;i<5;i++)
            {
                var cot=cots[i];var at=cot.position;at.z=shelter.position.z+(2-i)*spacing;cot.position=at;
                var b=CotVisualBounds(cot);cot.position+=Vector3.right*(plane+.03f-b.min.x);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cot);
            }
            Physics.SyncTransforms();
            var report=new StringBuilder();report.AppendLine("Player radius="+radius+"; bed spacing="+spacing+"; clear aisle="+gap+"; head plane="+plane);
            var floor=shelter.Find("ShelterShell").GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Floor");
            for(int i=0;i<4;i++)
            {
                var a=CotVisualBounds(cots[i]);var b=CotVisualBounds(cots[i+1]);float z=(a.min.z+b.max.z)*.5f;
                var actor=new GameObject("Temporary actual aisle walking probe"){hideFlags=HideFlags.HideAndDontSave};
                try
                {
                    if(template!=null)actor.layer=template.gameObject.layer;
                    actor.transform.position=new Vector3(Mathf.Max(a.max.x,b.max.x)+.6f,floor.bounds.max.y+.025f,z);
                    var cc=actor.AddComponent<CharacterController>();cc.height=template!=null?template.height:1.8f;cc.radius=radius;cc.center=template!=null?template.center:new Vector3(0,.9f,0);cc.skinWidth=.025f;cc.stepOffset=.15f;
                    var finish=plane+.60f;Physics.SyncTransforms();
                    for(int step=0;step<150&&actor.transform.position.x>finish+.03f;step++)cc.Move(Vector3.left*.025f+Vector3.down*.02f);
                    if(actor.transform.position.x>finish+.08f)throw new Exception("Player cannot pass aisle "+(i+1)+" reached "+actor.transform.position);
                    report.AppendLine("PASS actual CharacterController walked aisle "+(i+1)+"; clearance="+(a.min.z-b.max.z).ToString("F3")+" m");
                }finally{Object.DestroyImmediate(actor);}
            }
            foreach(var cot in cots)
            {
                var b=CotVisualBounds(cot);if(Mathf.Abs(b.min.x-plane-.03f)>.005f)throw new Exception("Head plane clearance failed");
                foreach(var col in shelter.GetComponentsInChildren<BoxCollider>().Where(c=>c.enabled&&!c.isTrigger&&c.name=="Support"))
                    if(b.Intersects(col.bounds))throw new Exception("Cot intersects a post: "+cot.name);
            }
            var scene=cots[0].gameObject.scene;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Reports+"/cot_aisles_validation.txt",report.ToString());
            EditorSceneManager.OpenScene(scene.path);EditorApplication.delayCall+=VerifyCotsAndSupplies;
        }
        static void RepairCotCore()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data=JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedCotsSupplies.meshdata.json"));
            var repaired=data.models.Single(m=>m.name=="FieldCot");repaired.name="FieldCotCoreV3";
            foreach(var part in repaired.parts)
            {
                part.name=part.name.Replace("FieldCot_","FieldCotCoreV3_");
                if(!string.IsNullOrEmpty(part.texture))
                {
                    var fresh=part.texture.Replace("FieldCot_","FieldCotCoreV3_");File.Copy(Root+"/"+part.texture,Root+"/"+fresh,true);part.texture=fresh;
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var repairedPrefab=Import(repaired);
            var cotRoot=PrefabUtility.LoadPrefabContents(Layout+"Cot.prefab");
            try
            {
                var old=cotRoot.transform.Find("Approved field equipment");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(repairedPrefab,cotRoot.scene);visual.name="Approved field equipment";visual.transform.SetParent(cotRoot.transform,false);
                PrefabUtility.SaveAsPrefabAsset(cotRoot,Layout+"Cot.prefab");
            }finally{PrefabUtility.UnloadPrefabContents(cotRoot);}
            var cots=All<Transform>().Where(t=>t.name=="Cot"||t.name.StartsWith("Cot Row ")).OrderByDescending(t=>t.position.z).ToArray();
            for(int i=0;i<cots.Length;i++)
            {
                var at=cots[i].position;at.z=cots[i].parent.position.z+(2-i)*1.4f;cots[i].position=at;
                PrefabUtility.RecordPrefabInstancePropertyModifications(cots[i]);
            }
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(scene.path);
            EditorApplication.delayCall+=VerifyCotsAndSupplies;
        }
        static Bounds CotVisualBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            if(renderers.Length==0)throw new Exception("Missing approved renderers: "+root.name);
            var result=renderers[0].bounds;foreach(var r in renderers)result.Encapsulate(r.bounds);return result;
        }
        static void ApplyCotsAndSupplies()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/_Project/Scenes/Maps/Map 1.unity")throw new Exception("Open Map 1 before applying.");
            Directory.CreateDirectory("Library/QuartermasterBackup");
            foreach(var name in new[]{"Cot","SupplyCrates"})if(!File.Exists("Library/QuartermasterBackup/"+name+"_BeforeCots.prefab"))File.Copy(Layout+name+".prefab","Library/QuartermasterBackup/"+name+"_BeforeCots.prefab");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data=JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedCotsSupplies.meshdata.json"));
            var models=data.models.ToDictionary(m=>m.name,Import);
            foreach(var name in new[]{"Cot","SupplyCrates"})
            {
                var path=Layout+name+".prefab";var asset=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var child in asset.transform.Cast<Transform>().ToArray())
                        if(child.name=="Approved field equipment"||child.GetComponentsInChildren<Renderer>(true).Length>0||child.GetComponentsInChildren<Collider>(true).Length>0)Object.DestroyImmediate(child.gameObject);
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(models[name=="Cot"?"FieldCot":"RationStores"],asset.scene);
                    visual.name="Approved field equipment";visual.transform.SetParent(asset.transform,false);
                    var bounds=CotVisualBounds(visual.transform);var center=asset.transform.InverseTransformPoint(bounds.center);
                    Block(asset,"COLLIDER_Approved "+name,center,bounds.size);
                    PrefabUtility.SaveAsPrefabAsset(asset,path);
                }finally{PrefabUtility.UnloadPrefabContents(asset);}
            }
            var cots=All<Transform>().Where(t=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t)==Layout+"Cot.prefab"&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToList();
            if(cots.Count==0)throw new Exception("No existing cot in bunker.");
            var shelter=cots[0].parent;var shell=shelter.Find("ShelterShell");
            var floor=shell.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Floor");
            var left=shell.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Side retaining wall").OrderBy(c=>c.bounds.center.x).First();
            var cotAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Layout+"Cot.prefab");
            while(cots.Count<5)
            {
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(cotAsset,scene);obj.transform.SetParent(shelter,false);cots.Add(obj.transform);
            }
            if(cots.Count!=5)throw new Exception("Unexpected cot count: "+cots.Count);
            var centerZ=shelter.position.z;
            for(int i=0;i<5;i++)
            {
                var cot=cots[i];cot.name=i==0?"Cot":"Cot Row "+(i+1);cot.localScale=Vector3.one;cot.rotation=shelter.rotation*Quaternion.Euler(0,270,0);
                cot.position=new Vector3(left.bounds.max.x+1.1f,floor.bounds.max.y,centerZ+(2-i)*1.4f);
                var visual=CotVisualBounds(cot);
                cot.position+=Vector3.right*(left.bounds.max.x+.03f-visual.min.x)+Vector3.up*(floor.bounds.max.y+.001f-visual.min.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cot);PrefabUtility.RecordPrefabInstancePropertyModifications(cot.gameObject);
            }
            var stores=All<Transform>().Single(t=>t.name=="SupplyCrates");var sb=CotVisualBounds(stores);
            stores.position+=Vector3.up*(floor.bounds.max.y+.001f-sb.min.y);PrefabUtility.RecordPrefabInstancePropertyModifications(stores);
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(scene.path);
            EditorApplication.delayCall+=VerifyCotsAndSupplies;
        }
        static void VerifyCotsAndSupplies()
        {
            var drawer=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.GPUResidentDrawer",false)).FirstOrDefault(t=>t!=null);
            var reset=drawer?.GetMethod("Reinitialize",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            reset?.Invoke(null,null);
            File.WriteAllText(Reports+"/cot_draw_cache.txt","GPU drawer="+drawer+" reset="+(reset!=null));
            var cots=All<Transform>().Where(t=>t.name=="Cot"||t.name.StartsWith("Cot Row ")).OrderByDescending(t=>t.position.z).ToArray();
            if(cots.Length!=5)throw new Exception("Expected exactly five cots after reload.");
            var shelter=cots[0].parent;var shell=shelter.Find("ShelterShell");
            var left=shell.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Side retaining wall").OrderBy(c=>c.bounds.center.x).First();
            var report=new StringBuilder();Physics.SyncTransforms();Bounds? previous=null;var headPlane=CotHeadPlane(shelter);
            foreach(var cot in cots)
            {
                var bounds=CotVisualBounds(cot);var gap=bounds.min.x-headPlane;
                if(Mathf.Abs(gap-.03f)>.006f)throw new Exception("Cot head-wall gap invalid: "+gap);
                if(Vector3.Dot(cot.forward,Vector3.left)<.99f)throw new Exception("Cot head does not face wall.");
                if(previous.HasValue&&previous.Value.min.z-bounds.max.z<.3f)throw new Exception("Cots overlap or insufficient aisle.");
                previous=bounds;
                if(cot.GetComponentsInChildren<MeshRenderer>().Any(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name.Contains("Error"))))throw new Exception("Missing cot material");
                var box=cot.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="COLLIDER_Approved Cot");ProbeCharacterMovement(box,report);
                report.AppendLine("PASS "+cot.name+" head faces wall; gap="+gap.ToString("F3")+" m; visible bounds="+bounds);
            }
            var stores=All<Transform>().Single(t=>t.name=="SupplyCrates");
            ProbeCharacterMovement(stores.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="COLLIDER_Approved SupplyCrates"),report);
            if(stores.GetComponentsInChildren<MeshRenderer>().Any(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name.Contains("Error"))))throw new Exception("Missing ration store material");
            report.AppendLine("PASS ration/ammunition store replaced; old geometry removed.");
            File.WriteAllText(Reports+"/cots_supplies_validation.txt",report.ToString());
            var target=cots[2].position+Vector3.up*.3f;
            Render(target+Vector3.right*5.7f+Vector3.up*1.6f+Vector3.forward*1.4f,target,"approved_five_field_cots",63);
            var detail=cots[2].position+Vector3.up*.4f;
            Render(detail+Vector3.right*2.0f+Vector3.up*1.7f+Vector3.back*1.6f,detail,"approved_field_cot_detail",58);
            var suppliesTarget=stores.position+Vector3.up*.6f;
            Render(suppliesTarget+new Vector3(-2,1.5f,-2.6f),suppliesTarget,"approved_ration_ammo_store",55);
            File.WriteAllText(Reports+"/cots_supplies_complete.txt","PASS approved Blender cots and ration stores installed; five cots face wall with 3 cm clearance; collision probes passed; Map 1 saved.");
        }
    }
}

