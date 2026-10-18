using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01;

namespace ShadowVale.Editor
{
    [InitializeOnLoad]
    public static class CommanderModelSetup
    {
        const string Folder = "Assets/_Project/Map01/Resources/Characters";
        static CommanderModelSetup() { EditorApplication.update += Poll; }
        static void Poll()
        {
            const string enemyRequest = "Tools/RebuildEnemyCommander.request";
            if (File.Exists(enemyRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(enemyRequest);
                try { Build("chỉ huy mỹ", "EnemyCommanderVisual"); AssetDatabase.SaveAssets(); File.WriteAllText("Tools/RebuildEnemyCommander.result.txt", "PASS rebuilt enemy commander from existing FBX"); }
                catch(Exception e) { File.WriteAllText("Tools/RebuildEnemyCommander.result.txt", e.ToString()); }
            }
            const string resize = "Tools/CommanderModelSize.request";
            if (File.Exists(resize) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var requestedModel = File.ReadAllText(resize).Trim() == "enemy" ? "EnemyCommanderVisual" : "AlliedCommanderVisual";
                File.Delete(resize);
                try
                {
                    var path = Folder + "/" + requestedModel + ".prefab";
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try { MatchNam(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                catch (Exception e) { File.WriteAllText("Tools/CommanderModelSize.result.txt", e.ToString()); Debug.LogException(e); }
            }
            var previousStart = SessionState.GetString("CommanderValidationStart", "__unset__");
            if (!EditorApplication.isPlayingOrWillChangePlaymode && previousStart != "__unset__")
            {
                UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(previousStart);
                SessionState.EraseString("CommanderValidationStart");
            }
            const string request = "Tools/CommanderModelSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Run(); File.WriteAllText("Tools/CommanderModelSetup.result.txt", "PASS: both models have valid Humanoid avatars, color textures, normalized height, and role references."); }
            catch (Exception e) { File.WriteAllText("Tools/CommanderModelSetup.result.txt", e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("ShadowVale/Characters/Setup Commander Models")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var allied = Build("chỉ huy mình", "AlliedCommanderVisual");
            Build("chỉ huy mỹ", "EnemyCommanderVisual");
            var root = PrefabUtility.LoadPrefabContents(OpeningCutsceneSetup.PrefabPath);
            try
            {
                root.GetComponent<Map01OpeningCutscene>().commanderModel = allied;
                PrefabUtility.SaveAsPrefabAsset(root, OpeningCutsceneSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        static GameObject Build(string name, string output)
        {
            string path = "Assets/_Project/Art/Characters/NPCs/" + name + ".fbx";
            if (name == "chỉ huy mỹ" && !File.Exists(path)) path = "Assets/_Project/Art/Characters/Enemies/" + name + ".fbx";
            var configure = typeof(CharacterImportSetup).GetMethod("ConfigureModel", BindingFlags.Static | BindingFlags.NonPublic);
            configure.Invoke(null, new object[] { path, false });
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var model = UnityEngine.Object.Instantiate(source);
            try
            {
                model.name = output;
                var actor = model.GetComponent<Animator>();
                if (actor == null || !actor.isHuman || !actor.avatar.isValid) throw new Exception(name + ": invalid Humanoid");
                actor.applyRootMotion = false;
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (renderers.Length == 0) throw new Exception(name + ": no mesh");
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                    foreach (var mat in r.sharedMaterials)
                        if (mat == null || mat.mainTexture == null) throw new Exception(name + ": missing color");
                }
                model.transform.localScale *= 1.8f / bounds.size.y;
                MatchNam(model);
                return PrefabUtility.SaveAsPrefabAsset(model, Folder + "/" + output + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
        static float Stature(GameObject source)
        {
            var sample = UnityEngine.Object.Instantiate(source);
            sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var actor = sample.GetComponentInChildren<Animator>();
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim");
                using (var pose = new Map01CinematicPose(actor))
                {
                    pose.Sample(idle, 0);
                    float feet = (actor.GetBoneTransform(HumanBodyBones.LeftFoot).position.y + actor.GetBoneTransform(HumanBodyBones.RightFoot).position.y) * .5f;
                    return actor.GetBoneTransform(HumanBodyBones.Head).position.y - feet;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
        }
        static void MatchNam(GameObject model)
        {
            var nam = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            float target = Stature(nam), before = Stature(model);
            if (target < .5f || before < .5f) throw new Exception("Invalid standing stature");
            float factor = target / before;
            model.transform.localScale *= factor;
            float after = Stature(model);
            if (Mathf.Abs(after - target) > .005f) throw new Exception("Commander height does not match Nam");
            File.WriteAllText("Tools/CommanderModelSize.result.txt", $"PASS: uniform scale x{factor:F4}; standing head-to-foot height {before:F4}m -> {after:F4}m; Nam {target:F4}m; prefab scale {model.transform.localScale}.");
        }
    }
}

