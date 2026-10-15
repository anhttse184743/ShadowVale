using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowVale.Map01.Editor {
    public static class RescueStorySetup {
        const string Source="Assets/_Project/Art/Characters/Animations/RescueStory";
        const string Target="Assets/Resources/Rescue/Story";
        static AvatarMask HandMask(GameObject model){
            string path=Source+"/Nam_HandCurves.mask";
            var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if(mask==null){mask=new AvatarMask();AssetDatabase.CreateAsset(mask,path);}
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,false);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,false);
            var nodes=model.GetComponentsInChildren<Transform>(true).Skip(1).ToArray();mask.transformCount=nodes.Length;
            for(int i=0;i<nodes.Length;i++){
                mask.SetTransformPath(i,AnimationUtility.CalculateTransformPath(nodes[i],model.transform));
                bool finger=nodes[i].name.Contains("Hand")&&new[]{"Thumb","Index","Middle","Ring","Pinky"}.Any(f=>nodes[i].name.Contains(f));
                mask.SetTransformActive(i,finger||nodes[i].name.Contains("ForeArmTwist"));
            }
            EditorUtility.SetDirty(mask);return mask;
        }
        [MenuItem("ShadowVale/Rescue/Import rescue cinematics and bush actions")]
        public static void Prepare() {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);Directory.CreateDirectory(Target);AssetDatabase.Refresh();
            foreach(var raw in Directory.GetFiles(Source,"*.fbx")) {
                string file=raw.Replace("\\","/");
                string name=Path.GetFileNameWithoutExtension(file);
                string model=name.StartsWith("Hung_")?"NPCs/hung.fbx":name.StartsWith("Enemy_")?"Enemies/lính rig.fbx":"Player/Player.fbx";
                string modelPath="Assets/_Project/Art/Characters/"+model;
                var avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().First(a=>a.isHuman);
                // FBX roundtrips can differ by a millimetre at non-connected foot
                // joints. Keep the original neutral rotations and human scale;
                // align only these tiny node translations, never the seated pose.
                var neutral=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                var neutralNodes=neutral.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var clipNodes=AssetDatabase.LoadAssetAtPath<GameObject>(file).GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var description=avatar.humanDescription;
                if(name.StartsWith("Nam_"))description.human=description.human.Where(h=>!new[]{"Thumb","Index","Middle","Ring","Little"}.Any(f=>h.humanName.Contains(f))).ToArray();
                description.skeleton=description.skeleton.Select(b=>{
                    if(b.name!="root"&&b.name!="Hips"&&clipNodes.TryGetValue(b.name,out var n)&&Vector3.Distance(b.position,n.localPosition)<.005f)b.position=n.localPosition;
                    if(neutralNodes.TryGetValue(b.name,out var t)){t.localPosition=b.position;t.localRotation=b.rotation;t.localScale=b.scale;}return b;
                }).ToArray();
                var aligned=AvatarBuilder.BuildHumanAvatar(neutral,description);
                if(!aligned.isValid||!aligned.isHuman)throw new InvalidOperationException("Invalid neutral avatar for "+name);
                string avatarPath=Source+"/"+name+".avatar.asset";
                var stored=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                if(stored==null){aligned.name=name+" neutral reference";AssetDatabase.CreateAsset(aligned,avatarPath);stored=aligned;}
                else{EditorUtility.CopySerialized(aligned,stored);EditorUtility.SetDirty(stored);UnityEngine.Object.DestroyImmediate(aligned);}
                if(name=="Nam_Bush_GrabPull"){
                    string posePath=Target+"/Nam_Bush_Avatar.asset";var poseAvatar=AssetDatabase.LoadAssetAtPath<Avatar>(posePath);
                    if(poseAvatar==null)AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(stored),posePath);else{EditorUtility.CopySerialized(stored,poseAvatar);EditorUtility.SetDirty(poseAvatar);}
                }
                UnityEngine.Object.DestroyImmediate(neutral);AssetDatabase.SaveAssets();
                var importer=(ModelImporter)AssetImporter.GetAtPath(file);
                importer.preserveHierarchy=true;importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;importer.sourceAvatar=stored;importer.humanDescription=description;
                importer.importAnimation=true;importer.importCameras=importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.resampleCurves=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
                var take=importer.clipAnimations.FirstOrDefault()??importer.defaultClipAnimations.FirstOrDefault()??new ModelImporterClipAnimation();
                float seconds=name.EndsWith("Loop")?2:name.EndsWith("Escape")?1.4f:name.EndsWith("Bush_Finish")?2.4f:name.EndsWith("GrabPull")?2.6f:3;
                take.takeName="Scene";take.firstFrame=1;take.lastFrame=Mathf.RoundToInt(seconds*30)+1;take.name=name;take.loopTime=name.EndsWith("Loop");take.loopPose=false;
                take.lockRootRotation=true;take.keepOriginalOrientation=true;take.lockRootHeightY=true;take.keepOriginalPositionY=true;take.heightFromFeet=false;
                take.lockRootPositionXZ=true;take.keepOriginalPositionXZ=true;importer.clipAnimations=new[]{take};importer.SaveAndReimport();
                if(name.StartsWith("Nam_")){
                    take.maskType=ClipAnimationMaskType.CopyFromOther;take.maskSource=HandMask(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));take.ConfigureClipFromMask(take.maskSource);
                    importer.clipAnimations=new[]{take};importer.SaveAndReimport();
                }
                var clip=AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                if(!clip.humanMotion)throw new InvalidOperationException("Non-humanoid rescue clip: "+name);
                if(name.StartsWith("Nam_"))Debug.Log(name+" finger curves: "+AnimationUtility.GetCurveBindings(clip).Count(b=>b.type==typeof(Transform)&&b.path.Contains("Hand")));
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(Target+"/"+name+".anim");
                if(existing!=null){EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(clip),Target+"/"+name+".anim");
            }
            var walk=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Enemy/Enemy_Rifle_Run.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            var oldWalk=AssetDatabase.LoadAssetAtPath<AnimationClip>(Target+"/Enemy_Interrogate_Walk.anim");
            if(oldWalk==null)AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(walk),Target+"/Enemy_Interrogate_Walk.anim");
            else{EditorUtility.CopySerialized(walk,oldWalk);EditorUtility.SetDirty(oldWalk);}
            CopyRecovery("Nam_Bush_Recovery",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/KnifeCarry/Nam_Knife_Sneak_Idle.anim"));
            CopyRecovery("Nam_Bush_Approach",AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/KnifeCarry/Nam_Knife_Sneak_Walk.anim"));
            var approach=AssetDatabase.LoadAssetAtPath<AnimationClip>(Target+"/Nam_Bush_Approach.anim");
            var grasp=AssetDatabase.LoadAssetAtPath<AnimationClip>(Target+"/Nam_Bush_GrabPull.anim");
            foreach(var binding in AnimationUtility.GetCurveBindings(approach).Where(b=>b.type==typeof(Animator)&&new[]{"Thumb","Index","Middle","Ring","Little"}.Any(f=>b.propertyName.Contains(f))))AnimationUtility.SetEditorCurve(approach,binding,null);
            foreach(var binding in AnimationUtility.GetCurveBindings(grasp).Where(b=>b.type==typeof(Transform)&&b.path.Contains("Hand"))){
                float value=AnimationUtility.GetEditorCurve(grasp,binding).Evaluate(0);
                AnimationUtility.SetEditorCurve(approach,binding,AnimationCurve.Constant(0,approach.length,value));
            }
            approach.EnsureQuaternionContinuity();EditorUtility.SetDirty(approach);
            CopyRecovery("Enemy_Bush_Recovery",AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Enemy/Enemy_Rifle_AimIdle.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview")));
            AssetDatabase.SaveAssets();PrepareHugHands();AssetDatabase.SaveAssets();ExportReference();Debug.Log("Rescue story animations imported with original avatars.");
        }
        static void CopyRecovery(string name,AnimationClip clip){
            if(clip==null)throw new InvalidOperationException("Missing gameplay recovery clip: "+name);
            string path=Target+"/"+name+".anim";
            string source=AssetDatabase.GetAssetPath(clip);
            if(source.EndsWith(".anim",StringComparison.OrdinalIgnoreCase)){
                // Preserve Unity's serialized muscle/binding data as well as
                // editor curves. CopySerialized can leave an old edited clip's
                // native binding table empty despite identical curve values.
                File.Copy(source,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);return;
            }
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null){var copy=UnityEngine.Object.Instantiate(clip);copy.name=name;AssetDatabase.CreateAsset(copy,path);}else{EditorUtility.CopySerialized(clip,existing);existing.name=name;EditorUtility.SetDirty(existing);}
        }
        [MenuItem("ShadowVale/Rescue/Import refined hug hands only")]
        public static void PrepareHugHands(){
            string file=Source+"/Meshes/Nam_HugHands.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(file);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;importer.importAnimation=false;
            importer.importCameras=importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(file);
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx");
            foreach(string side in new[]{"Left","Right"}){
                var src=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Hug_"+side+"Hand");
                var dst=original.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Player_"+side+"Hand");
                var map=dst.transform.worldToLocalMatrix*src.transform.localToWorldMatrix;
                var normalMap=map.inverse.transpose;
                var mesh=UnityEngine.Object.Instantiate(src.sharedMesh);mesh.name="Nam_Hug_"+side+"Hand";
                mesh.vertices=mesh.vertices.Select(v=>map.MultiplyPoint3x4(v)).ToArray();
                mesh.normals=mesh.normals.Select(n=>normalMap.MultiplyVector(n).normalized).ToArray();
                var indices=src.bones.Select(b=>Array.FindIndex(dst.bones,t=>t.name==b.name)).ToArray();
                if(indices.Any(i=>i<0))throw new InvalidOperationException("Hug hand bone mismatch: "+side);
                mesh.boneWeights=mesh.boneWeights.Select(w=>new BoneWeight{boneIndex0=indices[w.boneIndex0],boneIndex1=indices[w.boneIndex1],boneIndex2=indices[w.boneIndex2],boneIndex3=indices[w.boneIndex3],weight0=w.weight0,weight1=w.weight1,weight2=w.weight2,weight3=w.weight3}).ToArray();
                mesh.bindposes=dst.sharedMesh.bindposes;mesh.RecalculateBounds();mesh.RecalculateTangents();
                if(mesh.vertexCount<=dst.sharedMesh.vertexCount||mesh.vertexCount>45000)throw new InvalidOperationException("Hand topology budget or subdivision failed: "+mesh.vertexCount);
                string path=Target+"/Nam_Hug_"+side+"Hand.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);}
                Debug.Log("Hug "+side+" hand: "+src.sharedMesh.vertexCount+" vertices, existing rig/materials preserved.");
            }
            AssetDatabase.SaveAssets();
        }
        [Serializable] class RefMesh { public string name;public Vector3[] vertices;public int[] triangles; }
        [Serializable] class Reference { public Vector3 captive;public RefMesh[] meshes; }
        static void ExportReference() {
            const string path="Assets/_Project/Scenes/Maps/Map 1.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{
            var roots=scene.GetRootGameObjects();
            var mission=roots.SelectMany(r=>r.GetComponentsInChildren<Map01Mission>(true)).First();var origin=mission.hung.position;
            var meshes=roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true))
                .Where(m=>m.sharedMesh!=null&&m.sharedMesh.isReadable&&m.GetComponent<Renderer>()!=null)
                .Where(m=>Vector3.Distance(m.GetComponent<Renderer>().bounds.ClosestPoint(origin),origin)<12)
                .Where(m=>m.GetComponent<Renderer>().bounds.size.y<2.5f||m.name.Contains("Terrain")||m.name.Contains("ground"))
                .Take(35).Select(m=>new RefMesh{name=m.name,vertices=m.sharedMesh.vertices.Select(v=>m.transform.TransformPoint(v)-origin).ToArray(),triangles=m.sharedMesh.triangles}).ToArray();
            Directory.CreateDirectory("SourceArt/Map01_Rescue");File.WriteAllText("SourceArt/Map01_Rescue/captive-reference.json",JsonUtility.ToJson(new Reference{captive=origin,meshes=meshes},true));
            }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
