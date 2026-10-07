using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
public static class Map02ArrivalSetup {
[MenuItem("ShadowVale/Cutscene/Prepare Map 2 arrival clips")]
public static void Run() {
string target="Assets/Resources/Cutscenes/Map02";
Directory.CreateDirectory(target);AssetDatabase.Refresh();
// Mesh bind references are genuinely neutral. A seated animation's default node
// transforms must never become the avatar T-pose, or sitting retargets as standing.
foreach(string who in new[]{"Nam","Hung"}) {
string reference="Assets/_Project/Art/Characters/Animations/Map02Arrival/"+who+"_Arrival_Bind.fbx";
AssetDatabase.ImportAsset(reference,ImportAssetOptions.ForceSynchronousImport);
var referenceImporter=(ModelImporter)AssetImporter.GetAtPath(reference);
referenceImporter.animationType=ModelImporterAnimationType.Human;referenceImporter.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
referenceImporter.importAnimation=false;referenceImporter.materialImportMode=ModelImporterMaterialImportMode.None;referenceImporter.preserveHierarchy=true;
referenceImporter.SaveAndReimport();
var referenceAvatar=AssetDatabase.LoadAllAssetsAtPath(reference).OfType<Avatar>().Single();
if(!referenceAvatar.isValid || !referenceAvatar.isHuman)throw new InvalidOperationException("Invalid neutral reference: "+who);
foreach(string take in new[]{"Arrival_Row_Loop","Arrival_Travel","Boat_Stand","Boat_Turn","Jetty_StepUp","Walk_Ashore","Oar_Stow","Standing_Guard"}) {
string name=who+"_"+take,path="Assets/_Project/Art/Characters/Animations/Map02Arrival/"+name+".fbx";
AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
// Standing/walking takes retain the original tiny foot offsets; seated takes
// bake them out. Keep the same neutral axes and scale while matching each take.
var neutral=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(reference));
neutral.name=Path.GetFileNameWithoutExtension(reference);
var neutralNodes=neutral.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
var clipNodes=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
var neutralDescription=referenceAvatar.humanDescription;
neutralDescription.skeleton=neutralDescription.skeleton.Select(b=>{
if(clipNodes.TryGetValue(b.name,out var n) && b.name!="root" && b.name!="Hips" && Vector3.Distance(b.position,n.localPosition)<.005f)b.position=n.localPosition;
if(neutralNodes.TryGetValue(b.name,out var t)){t.localPosition=b.position;t.localRotation=b.rotation;t.localScale=b.scale;}
return b;}).ToArray();
var alignedAvatar=AvatarBuilder.BuildHumanAvatar(neutral,neutralDescription);
if(!alignedAvatar.isValid || !alignedAvatar.isHuman)throw new InvalidOperationException("Invalid neutral avatar: "+name);
alignedAvatar.name=name+"_Reference";
string avatarPath="Assets/_Project/Art/Characters/Animations/Map02Arrival/"+name+".avatar.asset";
var storedAvatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
if(storedAvatar==null){AssetDatabase.CreateAsset(alignedAvatar,avatarPath);storedAvatar=alignedAvatar;}
else {EditorUtility.CopySerialized(alignedAvatar,storedAvatar);EditorUtility.SetDirty(storedAvatar);UnityEngine.Object.DestroyImmediate(alignedAvatar);}
UnityEngine.Object.DestroyImmediate(neutral);
AssetDatabase.SaveAssets();
var importer=(ModelImporter)AssetImporter.GetAtPath(path);
importer.animationType=ModelImporterAnimationType.Human;
importer.preserveHierarchy=true;
// Reuse the neutral avatar itself, including its human scale. Creating an
// avatar from a seated take recalculates scale from bent legs even with the
// neutral skeleton description, displacing the retargeted body above the boat.
importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
importer.sourceAvatar=storedAvatar;
importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
importer.materialImportMode=ModelImporterMaterialImportMode.None;
var takes=importer.defaultClipAnimations;
foreach(var clip in takes){clip.name=name;clip.loopTime=take=="Walk_Ashore"||take=="Standing_Guard"||take=="Arrival_Row_Loop"||take=="Arrival_Travel";clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;}
importer.clipAnimations=takes;importer.SaveAndReimport();
var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
string bakedPath=target+"/"+name+".anim";var baked=AssetDatabase.LoadAssetAtPath<AnimationClip>(bakedPath);
if(baked==null){baked=new AnimationClip();AssetDatabase.CreateAsset(baked,bakedPath);}
EditorUtility.CopySerialized(source,baked);baked.name=name;EditorUtility.SetDirty(baked);
if(!baked.humanMotion)throw new InvalidOperationException("Invalid avatar: "+name);
}
}
var scenes=EditorBuildSettings.scenes.ToList();string map="Assets/_Project/Scenes/Maps/Map 2.unity";
if(!scenes.Any(s=>s.path==map))scenes.Add(new EditorBuildSettingsScene(map,true));
EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();Debug.Log("MAP02_ARRIVAL_READY");
}
}}
