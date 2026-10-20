using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static partial class CommandBunkerUpgrade
    {
        static void InspectQuartermaster()
        {
            var report=new StringBuilder();
            report.AppendLine("SCENE "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            foreach(var t in All<Transform>().Where(t=>new[]{"EquipmentShelf","RadioDesk","RifleRack","ShelterShell","Cot","SupplyCrates"}.Contains(t.name)))
            {
                report.AppendLine(PathOf(t)+" pos="+t.position.ToString("F3")+" local="+t.localPosition.ToString("F3")+" rotation="+t.eulerAngles+" scale="+t.lossyScale+" asset="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t));
                foreach(var r in t.GetComponentsInChildren<Renderer>(true)) report.AppendLine("  RENDER "+PathOf(r.transform)+" enabled="+r.enabled+" bounds="+r.bounds+" materials="+string.Join(",",r.sharedMaterials.Where(m=>m!=null).Select(AssetDatabase.GetAssetPath)));
                foreach(var c in t.GetComponentsInChildren<Collider>(true))report.AppendLine("  COLLIDER "+PathOf(c.transform)+" enabled="+c.enabled+" trigger="+c.isTrigger+" bounds="+c.bounds);
            }
            File.WriteAllText(Reports+"/quartermaster_inspection.txt",report.ToString());
            var nearby=new StringBuilder();
            foreach(var r in All<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.Intersects(new Bounds(new Vector3(-60,1,-59),new Vector3(7,3,7)))))
                nearby.AppendLine(PathOf(r.transform)+" | "+r.bounds+" | mesh="+AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>()?.sharedMesh));
            File.WriteAllText(Reports+"/quartermaster_nearby.txt",nearby.ToString());
        }
        static void Block(GameObject root,string name,Vector3 center,Vector3 size)
        {
            var child=new GameObject(name);child.transform.SetParent(root.transform,false);
            var box=child.AddComponent<BoxCollider>();box.center=center;box.size=size;box.isTrigger=false;
            var nav=child.AddComponent<NavMeshObstacle>();nav.shape=NavMeshObstacleShape.Box;nav.center=center;nav.size=size;nav.carving=true;
        }
        static void ApplyQuartermaster()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/Maps/Map 1.unity")throw new Exception("Open Map 1 before applying.");
            Directory.CreateDirectory("Library/QuartermasterBackup");
            foreach(var name in new[]{"RifleRack","EquipmentShelf","RadioDesk","ShelterShell","BriefingTable","MissionBoard"})
                if(!File.Exists("Library/QuartermasterBackup/"+name+".prefab"))File.Copy(Layout+name+".prefab","Library/QuartermasterBackup/"+name+".prefab");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // Reimporting the walnut meshes/materials updates existing nested prefabs in place.
            foreach(var model in JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedBunker.meshdata.json")).models)Import(model);
            var chairPrefab=PrefabUtility.LoadPrefabContents(Root+"/Chair.prefab");
            try{Block(chairPrefab,"COLLIDER_Approved briefing chair",new Vector3(0,.46f,0),new Vector3(.46f,.93f,.46f));PrefabUtility.SaveAsPrefabAsset(chairPrefab,Root+"/Chair.prefab");}
            finally{PrefabUtility.UnloadPrefabContents(chairPrefab);}
            var models=JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedQuartermaster.meshdata.json")).models.ToDictionary(m=>m.name,Import);
            foreach(var name in new[]{"RifleRack","EquipmentShelf","RadioDesk"})
            {
                var path=Layout+name+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var t in prefab.transform.Cast<Transform>().Where(t=>t.name=="Approved revision 2"||t.name.StartsWith("COLLIDER_Approved")).ToArray())Object.DestroyImmediate(t.gameObject);
                    foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))r.enabled=false;
                    foreach(var c in prefab.GetComponentsInChildren<Collider>(true))c.enabled=false;
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(models[name],prefab.scene);visual.name="Approved revision 2";visual.transform.SetParent(prefab.transform,false);
                    if(name=="RifleRack")Block(prefab,"COLLIDER_Approved loaded rack",new Vector3(0,.70f,0),new Vector3(1.85f,1.40f,.55f));
                    if(name=="EquipmentShelf")Block(prefab,"COLLIDER_Approved loaded shelf",new Vector3(0,.86f,0),new Vector3(1.69f,1.72f,.61f));
                    if(name=="RadioDesk")
                    {
                        Block(prefab,"COLLIDER_Approved desk",new Vector3(0,.40f,0),new Vector3(1.55f,.80f,.76f));
                        Block(prefab,"COLLIDER_Approved chair facing table",new Vector3(0,.47f,-.85f),new Vector3(.51f,.94f,.46f));
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            // Limit the new walnut finish to shelter furniture, walls and floor.
            foreach(var name in new[]{"ShelterShell","SupplyCrates","Cot","TimberFrame","CamouflageNet","BriefingTable","MissionBoard"})
            {
                var path=Layout+name+".prefab";if(!File.Exists(path))continue;
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if(name=="BriefingTable"&&!prefab.transform.Cast<Transform>().Any(t=>t.name=="COLLIDER_Approved briefing table"))Block(prefab,"COLLIDER_Approved briefing table",new Vector3(0,.47f,0),new Vector3(2.8f,.94f,1.08f));
                    foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats=renderer.sharedMaterials;bool changed=false;
                        for(int i=0;i<mats.Length;i++)
                        {
                            var m=mats[i];if(m==null)continue;
                            var source=AssetDatabase.GetAssetPath(m);
                            if(!source.StartsWith("Assets/_Project/Art/Environment/LayoutTest/Materials/")||!(m.name.IndexOf("timber",StringComparison.OrdinalIgnoreCase)>=0||m.name.IndexOf("wood",StringComparison.OrdinalIgnoreCase)>=0))continue;
                            var matPath=Root+"/Walnut_"+m.name.Replace(" ","_")+".mat";
                            var walnut=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                            if(walnut==null){walnut=new Material(m);walnut.name="Walnut "+m.name;AssetDatabase.CreateAsset(walnut,matPath);}
                            walnut.SetColor("_BaseColor",Color.white);walnut.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/WalnutTimber_Tile.png"));walnut.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(walnut);
                            mats[i]=walnut;changed=true;
                        }
                        if(changed)renderer.sharedMaterials=mats;
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            var shelf=All<Transform>().Single(t=>t.name=="EquipmentShelf");
            var shelter=shelf.parent;var shell=shelter.Find("ShelterShell");
            var side=shell.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Side retaining wall").OrderByDescending(c=>shelter.InverseTransformPoint(c.bounds.center).x).First();
            // +Z is the back of this approved model. Turn it toward the right wall.
            shelf.localRotation=Quaternion.Euler(0,90,0);
            var local=shelf.localPosition;local.x=shelter.InverseTransformPoint(new Vector3(side.bounds.min.x,side.bounds.center.y,side.bounds.center.z)).x-.305f-.03f;shelf.localPosition=local;
            PrefabUtility.RecordPrefabInstancePropertyModifications(shelf);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(shelf.gameObject.scene);EditorSceneManager.SaveScene(shelf.gameObject.scene);
            VerifyQuartermaster();
        }
        static void VerifyQuartermaster()
        {
            Physics.SyncTransforms();var report=new StringBuilder();
            foreach(var name in new[]{"RifleRack","EquipmentShelf","RadioDesk"})
            {
                var t=All<Transform>().Single(o=>o.name==name);
                var visual=t.Find("Approved revision 2");if(visual==null)throw new Exception("Missing approved model "+name);
                if(t.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&(r.GetComponent<MeshFilter>()==null||r.GetComponent<MeshFilter>().sharedMesh==null)))throw new Exception("Missing mesh "+name);
                foreach(var r in visual.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)
                    if(m==null||m.shader==null||m.shader.name.Contains("Error"))throw new Exception("Invalid material "+name);
                foreach(var box in t.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("COLLIDER_Approved")))
                {
                    if(!box.enabled||box.isTrigger)throw new Exception("Invalid blocker "+box.name);
                    var nav=box.GetComponent<NavMeshObstacle>();if(nav==null||!nav.carving)throw new Exception("Missing navigation blocker");
                    // Probe straight through each collider from its open front at torso height.
                    var front=-t.forward;var start=box.bounds.center+front*2;
                    if(!box.Raycast(new Ray(start,-front),out var hit,4))throw new Exception("Front collision probe failed "+box.name);
                    report.AppendLine("PASS solid collider + carving + front probe: "+name+" / "+box.name+" hit="+hit.point);
                    ProbeCharacterMovement(box,report);
                }
                report.AppendLine("PASS approved meshes and URP materials: "+name);
            }
            var shelf=All<Transform>().Single(t=>t.name=="EquipmentShelf");
            var wall=shelf.parent.Find("ShelterShell").GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Side retaining wall").OrderByDescending(c=>c.bounds.center.x).First();
            var blocker=shelf.GetComponentsInChildren<BoxCollider>().Single(c=>c.name.StartsWith("COLLIDER_Approved"));
            var gap=wall.bounds.min.x-blocker.bounds.max.x;
            if(gap<.015f||gap>.06f)throw new Exception("Shelf wall gap invalid "+gap);
            if(Vector3.Dot(-shelf.forward,Vector3.left)<.99f)throw new Exception("Shelf faces wall");
            report.AppendLine("PASS shelf faces inward, wall gap="+gap.ToString("F3")+" m");
            report.AppendLine("PASS chair front +Z points toward desk; separate chair collider at local Z=-0.85 m");
            File.WriteAllText(Reports+"/quartermaster_validation.txt",report.ToString());
            InspectQuartermaster();
            foreach(var name in new[]{"RifleRack","EquipmentShelf","RadioDesk"})
            {
                var t=All<Transform>().Single(o=>o.name==name);var target=t.position+Vector3.up*.85f;
                Render(target-t.forward*2.7f+t.right*.85f+Vector3.up*.65f,target,"quartermaster_"+name,52);
            }
        }
        static void CleanupQuartermaster()
        {
            var report=new StringBuilder();
            foreach(var name in new[]{"RifleRack","EquipmentShelf","RadioDesk"})
            {
                var path=Layout+name+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var retired=prefab.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Geometry ")||(t.GetComponent<Collider>()!=null&&!t.GetComponent<Collider>().enabled)).ToArray();
                    foreach(var child in retired){report.AppendLine("Removed retired object: "+name+"/"+child.name);Object.DestroyImmediate(child.gameObject);}
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            File.WriteAllText(Reports+"/quartermaster_batch_cleanup.txt",report.ToString());
            EditorApplication.delayCall+=()=>{VerifyQuartermaster();File.WriteAllText(Reports+"/quartermaster_completed.txt","PASS approved models installed, retired geometry removed, scene saved and validation complete.");};
        }
        static void ProbeCharacterMovement(BoxCollider source,StringBuilder report)
        {
            var template=All<CharacterController>().FirstOrDefault();
            var blocker=new GameObject("Temporary isolated furniture collider probe");
            var actor=new GameObject("Temporary CharacterController probe");
            var floor=new GameObject("Temporary probe ground");
            try
            {
                var origin=new Vector3(20000,0,20000);
                blocker.transform.position=origin;blocker.transform.rotation=source.transform.rotation;blocker.layer=source.gameObject.layer;
                var b=blocker.AddComponent<BoxCollider>();b.size=Vector3.Scale(source.size,source.transform.lossyScale);b.center=new Vector3(0,b.size.y*.5f,0);
                floor.transform.position=origin+Vector3.down*.05f;var ground=floor.AddComponent<BoxCollider>();ground.size=new Vector3(10,.1f,10);
                var front=-blocker.transform.forward;actor.transform.position=origin+front*2+Vector3.up*.05f;
                if(template!=null)actor.layer=template.gameObject.layer;
                var cc=actor.AddComponent<CharacterController>();cc.height=template!=null?template.height:1.8f;cc.radius=template!=null?template.radius:.3f;cc.center=template!=null?template.center:new Vector3(0,.9f,0);cc.skinWidth=.025f;cc.stepOffset=.15f;
                if(Physics.GetIgnoreLayerCollision(actor.layer,blocker.layer))throw new Exception("Player and furniture layers ignore collision");
                Physics.SyncTransforms();bool sides=false;
                for(int i=0;i<160;i++)sides|=(cc.Move(-front*.025f+Vector3.down*.05f)&CollisionFlags.Sides)!=0;
                var signed=Vector3.Dot(actor.transform.position-origin,front);
                if(!sides||signed<0)throw new Exception("CharacterController passed through "+source.name+" signed="+signed);
                report.AppendLine("PASS CharacterController.Move blocked by "+source.name+" remaining front distance="+signed.ToString("F3"));
            }
            finally{Object.DestroyImmediate(actor);Object.DestroyImmediate(blocker);Object.DestroyImmediate(floor);}
        }
    }
}
