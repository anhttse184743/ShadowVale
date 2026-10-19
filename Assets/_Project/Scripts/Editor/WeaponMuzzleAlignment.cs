using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Editor
{
    [InitializeOnLoad]
    public static class WeaponMuzzleAlignment
    {
        static WeaponMuzzleAlignment()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            const string request="Tools/AlignWeaponMuzzle.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Delete(request);Align();
        }
        [MenuItem("ShadowVale/Weapons/Align AK muzzle to barrel mesh")]
        public static void Align()
        {
            const string path="Assets/_Project/Prefabs/Items/W_AK47.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var report=new StringBuilder();var weapon=root.GetComponent<Weapon>();
                report.AppendLine("Before: "+weapon.Muzzle.localPosition.ToString("F6"));
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>()) {
                    if(!filter.name.StartsWith("r_barrel") || filter.sharedMesh==null)continue;
                    bool first=true;var bounds=new Bounds();
                    foreach(var v in filter.sharedMesh.vertices) {
                        var p=root.transform.InverseTransformPoint(filter.transform.TransformPoint(v));
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                    }
                    report.AppendLine(filter.name+" min="+bounds.min.ToString("F6")+" max="+bounds.max.ToString("F6"));
                }
                if(!WeaponPrefabBuilder.TryBarrelExit(root.transform,out var exit))throw new System.InvalidOperationException("AK barrel-end mesh not found");
                weapon.Muzzle.localPosition=exit;weapon.Muzzle.localRotation=Quaternion.identity;
                report.AppendLine("After: "+exit.ToString("F6"));
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Directory.CreateDirectory("Tools/GunfireReports");File.WriteAllText("Tools/GunfireReports/barrel-mesh.txt",report.ToString());
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
