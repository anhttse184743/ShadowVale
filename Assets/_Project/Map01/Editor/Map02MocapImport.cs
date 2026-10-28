using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    /// <summary>Night 1 motion capture (2026-10-25): the "Lời hứa" supper, Hùng's arm-hold walk and his startle at the duck.
    /// Exported from chuyen_dong by export_loihua_unity.py; every take is retargeted through the actor's own game avatar.</summary>
    public static class Map02MocapImport
    {
        const string Folder="Assets/_Project/Art/Map02DayOne/Mocap/";
        const string NamAvatar="Assets/_Project/Art/Characters/Player/Player.fbx",HungAvatar="Assets/_Project/Art/Characters/NPCs/hung.fbx",LanhAvatar="Assets/_Project/Art/Map02DayOne/Chị Lành.fbx",SauAvatar="Assets/_Project/Art/Map02DayOne/Ông 6.fbx",TiAvatar="Assets/_Project/Art/Map02DayOne/Bé Tí.fbx",BayAvatar="Assets/_Project/Art/Map02DayOne/Ông 7 chèo thuyền.fbx";
        // id, file, avatar, loop. The seated and startle takes keep the body where Blender had it round the actor's origin
        // (all root motion baked, based on the original); the walk was exported in place, so it is baked the same way.
        static readonly (string id,string file,string avatar,bool loop)[] Takes=
        {
            ("LoiHuaNam","LoiHua_Nam",NamAvatar,false),("LoiHuaHung","LoiHua_Hung",HungAvatar,false),
            ("HungArmWalk","Hung_DiOmTay",HungAvatar,true),("HungStartleGrab","Hung_GiatMinh_ChopVai",HungAvatar,false),("HungStartle","Hung_GiatMinh",HungAvatar,false),
            // Group B (2026-10-25): D1_02 the bandage, on Khoa's chị Lành (exported at her FBX's own size; the game scales her)
            ("HungThayBang","Hung_ThayBang",HungAvatar,false),("LanhThayBang","Lanh_ThayBang",LanhAvatar,false),
            // N1_04 the shift change: Hùng lying awake on the mat, Nam sitting on the boards
            ("HungDoiCa","Hung_DoiCa",HungAvatar,false),("NamDoiCa","Nam_DoiCa",NamAvatar,false),
            // D1_03 the medicine hand-over, and ông Sáu at rest on his chair through Day 1 (forward then back: a seamless loop)
            ("NamTraoThuoc","Nam_TraoThuoc",NamAvatar,false),("LanhNhanThuoc","Lanh_NhanThuoc",LanhAvatar,false),("SauNgoiNghi","Sau_NgoiNghi",SauAvatar,true),
            // Group C: Bé Tí crouched behind the porch post, and her giggle (D1_02)
            ("TiNapCot","Ti_NapCot",TiAvatar,true),("TiCuoi","Ti_Cuoi",TiAvatar,false),
            // D1_05: one two-handed mallet blow (played from each Space press), chú Bảy squatting with the stake
            ("NamVoCoc","Nam_VoCoc",NamAvatar,false),("BayGiuCoc","Bay_GiuCoc",BayAvatar,true),
            // C1 at the landing (Map02Arrival): Hùng's misstep on the top stair, Nam setting his pack down first and helping him up
            ("HungHutChan","Hung_HutChan",HungAvatar,false),("NamDoHung","Nam_DoHung",NamAvatar,false),
        };
        [MenuItem("ShadowVale/Map 2/Import Night 1 motion capture")]
        public static void Run()
        {
            var data=AssetDatabase.LoadAssetAtPath<Map02DayOneArt>("Assets/_Project/Map01/Resources/Story/Map02DayOneArt.asset");
            var actions=data.actions.ToList();
            foreach(var take in Takes)
            {
                string path=Folder+take.file+".fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var avatar=AssetDatabase.LoadAllAssetsAtPath(take.avatar).OfType<Avatar>().Single();
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;importer.sourceAvatar=avatar;
                importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importCameras=false;importer.importLights=false;
                var clips=importer.defaultClipAnimations;if(clips.Length==0)throw new InvalidOperationException("No take in "+path);
                var clip=clips[0];clip.name=take.id;clip.loopTime=take.loop;clip.loopPose=false;
                clip.lockRootRotation=clip.lockRootHeightY=clip.lockRootPositionXZ=true;clip.keepOriginalOrientation=clip.keepOriginalPositionY=clip.keepOriginalPositionXZ=true;clip.heightFromFeet=false;
                importer.clipAnimations=new[]{clip};importer.SaveAndReimport();
                var imported=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                if(!imported.humanMotion)throw new InvalidOperationException("Not a humanoid take: "+path);
                actions.RemoveAll(a=>a.id==take.id);actions.Add(new Map02DayOneArt.Action{id=take.id,clip=imported});
                Debug.Log($"MAP02_MOCAP {take.id} {imported.length:F2}s loop={imported.isLooping}");
            }
            data.actions=actions.ToArray();EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();Debug.Log("MAP02_MOCAP_IMPORT_PASS");
        }
    }
}
