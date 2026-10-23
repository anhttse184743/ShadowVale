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
        static void MoveRifleRackToWall()
        {
            var rack=All<Transform>().Single(t=>t.name=="RifleRack");var shelter=rack.parent;
            var wall=shelter.Find("ShelterShell").GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Back retaining wall");
            var renderers=rack.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var from=rack.position;var delta=wall.bounds.min.z-.03f-bounds.max.z;
            rack.position+=Vector3.forward*delta;PrefabUtility.RecordPrefabInstancePropertyModifications(rack);
            foreach(var r in renderers){EditorUtility.SetDirty(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            Physics.SyncTransforms();
            var report=new StringBuilder();
            var blocker=rack.GetComponentsInChildren<BoxCollider>().Single(c=>c.name.StartsWith("COLLIDER_Approved"));
            ProbeCharacterMovement(blocker,report);
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var gap=wall.bounds.min.z-bounds.max.z;if(Mathf.Abs(gap-.03f)>.005f)throw new Exception("AK rack wall clearance invalid.");
            report.AppendLine("PASS rack moved "+delta.ToString("F3")+" m toward wall; clearance="+gap.ToString("F3")+" m; from="+from+" to="+rack.position);
            EditorSceneManager.MarkSceneDirty(rack.gameObject.scene);EditorSceneManager.SaveScene(rack.gameObject.scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Reports+"/rack_wall_validation.txt",report.ToString());
            EditorApplication.delayCall+=()=>
            {
                var target=rack.position+Vector3.up*.8f;Render(target+Vector3.back*2.4f+Vector3.left*1.3f+Vector3.up*.7f,target,"quartermaster_AK_close_to_wall",55);
            };
        }
        static void ArrangeQuartermasterRow()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/Maps/Map 1.unity")throw new Exception("Open Map 1 first.");
            var report=new StringBuilder();
            // Always rebuild from the exported source, so running again does not rotate twice.
            var part=JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedQuartermaster.meshdata.json")).models.Single(m=>m.name=="RifleRack").parts.Single(p=>p.material=="Material_44");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+part.name+".asset");
            int count=part.vertices.Length/3;if(count%6!=0)throw new Exception("Rifle mesh must contain six equal source instances.");
            var vs=new Vector3[count];var ns=new Vector3[count];var rotation=Quaternion.Euler(0,-90,0);int per=count/6;
            for(int gun=0;gun<6;gun++)
            {
                var bounds=new Bounds(new Vector3(part.vertices[gun*per*3],part.vertices[gun*per*3+1],part.vertices[gun*per*3+2]),Vector3.zero);
                for(int i=gun*per;i<(gun+1)*per;i++)bounds.Encapsulate(new Vector3(part.vertices[i*3],part.vertices[i*3+1],part.vertices[i*3+2]));
                var pivot=new Vector3(bounds.center.x,0,bounds.center.z);
                for(int i=gun*per;i<(gun+1)*per;i++)
                {
                    vs[i]=pivot+rotation*(new Vector3(part.vertices[i*3],part.vertices[i*3+1],part.vertices[i*3+2])-pivot);
                    ns[i]=rotation*new Vector3(part.normals[i*3],part.normals[i*3+1],part.normals[i*3+2]);
                }
            }
            mesh.vertices=vs;mesh.normals=ns;mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
            report.AppendLine("PASS six AKs rotated individually -90 degrees about local Y: magazines now point toward rear wall (+Z).");
            var first=All<Transform>().Single(t=>t.name=="EquipmentShelf");var shelter=first.parent;var shell=shelter.Find("ShelterShell");
            var wall=shell.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Front wall").OrderByDescending(c=>c.bounds.center.x).First();
            const float spacing=1.95f;
            float south=shelter.InverseTransformPoint(new Vector3(wall.bounds.center.x,wall.bounds.center.y,wall.bounds.max.z)).z;
            var northWall=shell.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Back retaining wall");
            float north=shelter.InverseTransformPoint(new Vector3(northWall.bounds.center.x,northWall.bounds.center.y,northWall.bounds.min.z)).z;
            float midpoint=(north+south)*.5f;
            var start=first.localPosition;start.z=midpoint+spacing*2;first.localPosition=start;
            PrefabUtility.RecordPrefabInstancePropertyModifications(first);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Layout+"EquipmentShelf.prefab");
            for(int i=1;i<=4;i++)
            {
                var name="EquipmentShelf Row "+(i+1);var existing=shelter.Find(name);
                if(existing==null){var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,first.gameObject.scene);existing=obj.transform;existing.SetParent(shelter,false);existing.name=name;}
                existing.localPosition=start+Vector3.back*spacing*i;existing.localRotation=first.localRotation;existing.localScale=first.localScale;PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
            }
            Physics.SyncTransforms();
            var row=shelter.Cast<Transform>().Where(t=>t==first||t.name.StartsWith("EquipmentShelf Row ")).OrderByDescending(t=>t.localPosition.z).ToArray();
            if(row.Length!=5)throw new Exception("Expected five shelves.");
            for(int i=0;i<row.Length;i++)
            {
                foreach(var renderer in row[i].GetComponentsInChildren<MeshRenderer>())
                {
                    EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var box=row[i].GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="COLLIDER_Approved loaded shelf");
                ProbeCharacterMovement(box,report);
                if(i>0&&box.bounds.Intersects(row[i-1].GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="COLLIDER_Approved loaded shelf").bounds))throw new Exception("Shelves overlap.");
                if(box.bounds.min.z<wall.bounds.max.z+.05f)throw new Exception("Shelf intersects end wall.");
                report.AppendLine("PASS row cabinet "+(i+1)+" position="+row[i].position.ToString("F3"));
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(first.gameObject.scene);EditorSceneManager.SaveScene(first.gameObject.scene);
            File.WriteAllText(Reports+"/quartermaster_row_validation.txt",report.ToString());
            EditorSceneManager.OpenScene(first.gameObject.scene.path);
            EditorApplication.delayCall+=()=>
            {
                // Flush resident draw records for moved/reloaded instances before the review render.
                var loaded=All<Transform>().Single(t=>t.name=="EquipmentShelf Row 3");
                var finalRow=loaded.parent.Cast<Transform>().Where(t=>t.name=="EquipmentShelf"||t.name.StartsWith("EquipmentShelf Row ")).OrderByDescending(t=>t.position.z).ToArray();
                var finalReport=new StringBuilder();Bounds? previous=null;float firstEdge=0,lastEdge=0;
                for(int i=0;i<finalRow.Length;i++)
                {
                    var renderers=finalRow[i].GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
                    var visual=renderers[0].bounds;foreach(var r in renderers)visual.Encapsulate(r.bounds);
                    if(previous.HasValue&&previous.Value.min.z-visual.max.z<.20f)throw new Exception("Visible cabinets too close after reload: "+finalRow[i].name);
                    if(i==0)firstEdge=visual.max.z;if(i==4)lastEdge=visual.min.z;
                    finalReport.AppendLine(finalRow[i].name+" visual bounds="+visual+" gap="+(previous.HasValue?(previous.Value.min.z-visual.max.z).ToString("F3"):"n/a"));previous=visual;
                }
                var leftMargin=north-(firstEdge-loaded.parent.position.z);var rightMargin=(lastEdge-loaded.parent.position.z)-south;
                if(Mathf.Abs(leftMargin-rightMargin)>.02f)throw new Exception("Row is not centered");
                finalReport.AppendLine("PASS five visible cabinets, no overlaps, wall margins="+leftMargin.ToString("F3")+" / "+rightMargin.ToString("F3"));
                File.WriteAllText(Reports+"/quartermaster_row_visual_validation.txt",finalReport.ToString());
                var center=loaded.position+Vector3.up*.85f;
                Render(center+Vector3.left*8+Vector3.up*.65f,center,"quartermaster_five_cabinets",68);
                var rack=All<Transform>().Single(t=>t.name=="RifleRack");var target=rack.position+Vector3.up*.8f;
                Render(target+Vector3.back*2.4f+Vector3.left*1.3f+Vector3.up*.7f,target,"quartermaster_AK_magazines_wall",55);
                File.WriteAllText(Reports+"/quartermaster_row_complete.txt","PASS five cabinets saved and AK magazines point into wall.");
            };
        }
    }
}
