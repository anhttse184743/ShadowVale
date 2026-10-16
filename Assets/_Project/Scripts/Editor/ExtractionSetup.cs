using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Map01;

namespace ShadowVale.Editor
{
    public static class ExtractionSetup
    {
        public const string PrefabPath="Assets/_Project/Map01/Resources/Cutscenes/Map01Extraction.prefab";
        public const string Clips="Assets/_Project/Art/Characters/Animations/Extraction/";
        [MenuItem("ShadowVale/Cutscene/Prepare extraction")]
        public static void Prepare()
        {
            ExportReference();
            PrepareActors();
            var root=new GameObject("Map01Extraction");
            try {
                var sequence=root.AddComponent<Map01Extraction>();
                string[] names={"Radio_Receive","Seated_Rifle_Ready","Seated_Rifle_Fire","Seated_Rifle_Reload","Seated_Lower_Weapon","Board_Boat_StepDown","Boat_Turn_And_Sit","Boat_Seated_Travel"};
                var clips=names.Select(n=>Import("Nam_"+n)).ToArray();
                sequence.radio=clips[0];sequence.seatedReady=clips[1];sequence.seatedFire=clips[2];sequence.seatedReload=clips[3];
                sequence.lowerWeapon=clips[4];sequence.board=clips[5];sequence.sit=clips[6];sequence.travel=clips[7];
                sequence.hungClips=names.Select(n=>Import("Hung_"+n)).ToArray();
                sequence.rifleStow=Import("Hung_Rifle_Stow");sequence.oarPickup=Import("Hung_Oar_Pickup");
                sequence.rowStart=Import("Hung_Row_Start");sequence.rowLoop=Import("Hung_Row_Loop");sequence.rowStop=Import("Hung_Row_Stop");
                foreach(var extra in new[]{"Rifle_Stow","Oar_Pickup","Row_Start","Row_Loop","Row_Stop"})Import("Nam_"+extra);
                sequence.paddleAudio=AssetDatabase.LoadAssetAtPath<AudioClip>(Clips+"Paddle_Splash.wav");
                sequence.rifleAudio=AssetDatabase.LoadAssetAtPath<AudioClip>(Clips+"Cover_Rifle.wav");
                sequence.engine=AssetDatabase.LoadAssetAtPath<AudioClip>(Clips+"Boat_Engine.wav");
                var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                var guards=UnityEngine.Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).Where(e=>e.name.StartsWith("Outpost guard ")).OrderBy(e=>e.SaveId,StringComparer.Ordinal).ToArray();
                var path=new NavMeshPath();
                if(!NavMesh.SamplePosition(guards[0].transform.position,out var start,4,NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(sequence.approach,out var end,4,NavMesh.AllAreas)
                    || !NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("No complete route from the commander camp to the jetty.");
                float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                var posts=new System.Collections.Generic.List<Vector3>();
                foreach(float fraction in new[]{.58f,.82f}) {
                    float remaining=length*fraction;Vector3 point=start.position;
                    for(int i=1;i<path.corners.Length;i++) { float segment=Vector3.Distance(path.corners[i-1],path.corners[i]); if(remaining<=segment) {point=Vector3.Lerp(path.corners[i-1],path.corners[i],remaining/segment);break;}remaining-=segment; }
                    foreach(float offset in new[]{-.7f,.7f}) {
                        if(!NavMesh.SamplePosition(point+Vector3.right*offset,out var post,2,NavMesh.AllAreas))throw new InvalidOperationException("Blocking post not navigable.");
                        posts.Add(post.position);
                    }
                }
                sequence.blockingPosts=posts.ToArray();
                // The actual north boundary begins at z=100. Keep the entire 6 m hull
                // inside the authored river; it need not leave the map to leave the jetty.
                sequence.departurePath=new[]{new Vector3(1.55f,0,87),new Vector3(1.6f,0,90),new Vector3(1.35f,0,93),new Vector3(-.2f,0,95),new Vector3(-1.6f,0,96.5f)};
                Physics.SyncTransforms();
                var collisions=new System.Collections.Generic.HashSet<string>();
                for(int step=0;step<=150;step++) {
                    float t=step/150f;var at=Map01Extraction.RiverPoint(sequence.departurePath,t);
                    var direction=t<1 ? Map01Extraction.RiverPoint(sequence.departurePath,t+.001f)-at : at-Map01Extraction.RiverPoint(sequence.departurePath,t-.001f);
                    foreach(var hit in Physics.OverlapBox(at+Vector3.up*.05f,new Vector3(.80f,.24f,3.02f),Quaternion.LookRotation(direction),~0,QueryTriggerInteraction.Ignore))
                        if(hit.name!="Boat bench" && !hit.name.StartsWith("Oar ")) collisions.Add(hit.name+" bounds="+hit.bounds+" sample="+at);
                    var rotation=Quaternion.LookRotation(direction);
                    foreach(var hit in Physics.OverlapBox(at+rotation*new Vector3(1.35f,.05f,-1.6f),new Vector3(.8f,.4f,.9f),rotation,~0,QueryTriggerInteraction.Ignore))
                        if(hit.name!="Boat bench" && !hit.name.StartsWith("Oar ") && hit.name!="Moored wooden sampan")collisions.Add("Paddle sweep: "+hit.name+" at "+at);
                }
                File.WriteAllText("SourceArt/Map01_Extraction/departure-clearance.txt",collisions.Count==0?"151 hull and starboard paddle sweep samples (including endpoint): clear of solid scene geometry.":string.Join("\n",collisions));
                if(collisions.Count>0)throw new InvalidOperationException("Departure geometry overlaps; see departure-clearance.txt");
                var actor=UnityEngine.Object.FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>().GetComponentInChildren<Animator>();
                Debug.Log("Extraction player visual scale="+actor.transform.lossyScale+" offset="+actor.transform.localPosition);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
                File.WriteAllText("SourceArt/Map01_Extraction/unity-layout.json",JsonUtility.ToJson(new Layout {posts=sequence.blockingPosts,departure=sequence.departurePath,boarding=sequence.boardingAnchors},true));
                Debug.Log("Extraction ready: 16 retargeted clips; complete NavMesh route; four stable guard IDs.");
            } finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        private static void PrepareActors()
        {
            const string path="Assets/_Project/Map01/Resources/Characters/Map01ActorAssets.asset";
            var data=AssetDatabase.LoadAssetAtPath<Map01ActorAssets>(path);
            if(data==null) {data=ScriptableObject.CreateInstance<Map01ActorAssets>();AssetDatabase.CreateAsset(data,path);}
            data.rifle=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/W_AK47.prefab").GetComponent<ShadowVale.Gameplay.Combat.Weapon>();
            var nam=Measure("Assets/_Project/Art/Characters/Player/Player.fbx");
            var hung=Measure("Assets/_Project/Art/Characters/NPCs/hung.fbx");
            data.namHeight=nam.x;data.hungHeight=hung.x;data.hungSole=hung.y;
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var anchor=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="WeaponAnchor");
            var grip=AssetDatabase.LoadAssetAtPath<ShadowVale.Gameplay.Combat.WeaponGripConfig>("Assets/_Project/ScriptableObjects/GameSettings/WeaponGripConfig.asset");
            grip.TryGetOffset(ShadowVale.Gameplay.Combat.WeaponKind.Rifle,out var offset);
            data.riflePosition=anchor.localPosition+anchor.localRotation*offset.localPosition;
            data.rifleEuler=(anchor.localRotation*Quaternion.Euler(offset.localEuler)).eulerAngles;
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            Debug.Log("Reference character dimensions: Nam="+nam+" Hung="+hung);
        }
        private static Vector2 Measure(string path)
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try {
                go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
                var actor=go.GetComponentInChildren<Animator>();actor.Rebind();actor.Update(0);
                float sole=float.PositiveInfinity;
                foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    var mesh=new Mesh();renderer.BakeMesh(mesh);
                    foreach(var v in mesh.vertices) sole=Mathf.Min(sole,renderer.transform.TransformPoint(v).y);
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                return new Vector2(actor.GetBoneTransform(HumanBodyBones.Head).position.y-sole,sole);
            } finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        [Serializable] private class Layout {public Vector3[] posts,departure,boarding;}
        private static AnimationClip Import(string name)
        {
            string path=Clips+name+".fbx";
            string bakedPath=Clips+name+".anim";
            if(File.Exists(bakedPath) && File.GetLastWriteTimeUtc(bakedPath)>=File.GetLastWriteTimeUtc(path)) {
                var cached=AssetDatabase.LoadAssetAtPath<AnimationClip>(bakedPath);
                if(cached!=null && cached.humanMotion)return cached;
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips) {clip.name=name;clip.loopTime=name.EndsWith("Ready")||name.EndsWith("Travel")||name.EndsWith("Row_Loop");clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;}
            importer.clipAnimations=clips;importer.SaveAndReimport();
            var result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            // Blender's armature-object curves must not resize or rotate the Unity visual root.
            // Only the unchanged 28-bone hierarchy is animated by the baked take.
            var baked=AssetDatabase.LoadAssetAtPath<AnimationClip>(bakedPath);
            if(baked==null) {baked=new AnimationClip();AssetDatabase.CreateAsset(baked,bakedPath);}
            EditorUtility.CopySerialized(result,baked);baked.name=name;
            if(!baked.humanMotion)throw new InvalidOperationException("Expected a valid Humanoid take: "+name);
            EditorUtility.SetDirty(baked);
            return baked;
        }
        [Serializable] public class Geometry { public Part[] parts; }
        [Serializable] public class Part { public string name; public Vector3[] vertices; public int[] triangles; public Vector3 center, size; }
        public static void ExportReference()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var parts = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f => f.sharedMesh != null && (f.name == "Moored wooden sampan" || (Vector3.Distance(f.transform.position,new Vector3(1.55f,0,87)) < 12 && f.sharedMesh.vertexCount < 3000 && !f.name.Contains("Wood") && !f.name.Contains("Leaves")) || f.name.ToLowerInvariant().Contains("bridge")))
                .Select(f => new Part { name=f.name, vertices=f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray(), triangles=f.sharedMesh.triangles,
                    center=f.GetComponent<Renderer>()?.bounds.center ?? f.transform.position, size=f.GetComponent<Renderer>()?.bounds.size ?? Vector3.zero }).ToArray();
            Directory.CreateDirectory("SourceArt/Map01_Extraction");
            File.WriteAllText("SourceArt/Map01_Extraction/jetty-reference.json", JsonUtility.ToJson(new Geometry {parts=parts}, true));
            Debug.Log("Extraction reference exported: " + parts.Length);
        }
    }
}
