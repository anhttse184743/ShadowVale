using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class TutorialCaptureSetup
    {
        private const string PreviewKey = "ShadowVale.Tutorial.CapturePreview";
        private const string StartedKey = PreviewKey + ".Started";
        private const string PauseKey = PreviewKey + ".PauseAt";
        static TutorialCaptureSetup()
        {
            EditorApplication.update += PreviewTick;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) SessionState.SetBool(PreviewKey, false); };
        }
        [MenuItem("ShadowVale/Tutorial/Xem thử cutscene bắt Hùng", priority = 22)]
        public static void Preview() => PreviewAt(TutorialCaptureCutscene.Shot.None);
        public static void PreviewAt(TutorialCaptureCutscene.Shot pauseAt)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before starting a preview.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAssets(); EditorSceneManager.OpenScene(TutorialSceneSetup.ScenePath);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialSceneSetup.ScenePath);
            SessionState.SetBool(PreviewKey, true); SessionState.SetBool(StartedKey, false); SessionState.SetInt(PauseKey, (int)pauseAt);
            EditorApplication.isPlaying = true;
        }
        private static void PreviewTick()
        {
            if (!SessionState.GetBool(PreviewKey, false) || !EditorApplication.isPlaying) return;
            var director = UnityEngine.Object.FindFirstObjectByType<TutorialDirector>();
            if (director == null) return;
            var mission = director.GetComponent<Map01Mission>(); if (!mission.IsInitialized || mission.RetryRequested == null) return;
            if (!SessionState.GetBool(StartedKey, false)) {
                SessionState.SetBool(StartedKey, true);
                var body = mission.player.GetComponent<CharacterController>(); body.enabled = false;
                mission.player.position = director.noiseTrap.transform.position; body.enabled = true;
                typeof(TutorialDirector).GetMethod("BeginStep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(director, new object[] { TutorialStep.Return });
                director.BeginCapture();
            }
            var film = director.GetComponent<TutorialCaptureCutscene>();
            var stop = (TutorialCaptureCutscene.Shot)SessionState.GetInt(PauseKey, 0);
            if (stop != TutorialCaptureCutscene.Shot.None && film.CurrentShot == stop && film.ShotElapsed > .8f) {
                mission.SetPaused(true); SessionState.SetBool(PreviewKey, false);
            }
            if (!film.Playing) SessionState.SetBool(PreviewKey, false);
        }
        public static void BuildAssets()
        {
            const string path = "Assets/_Project/Resources/TutorialCapture.asset";
            var assets = AssetDatabase.LoadAssetAtPath<TutorialCaptureAssets>(path);
            if (assets == null) { assets = ScriptableObject.CreateInstance<TutorialCaptureAssets>(); AssetDatabase.CreateAsset(assets, path); }
            const string folder = "Assets/_Project/Art/Characters/Animations/";
            assets.run = Clip(folder + "Clips/Standard Run.fbx");
            assets.hit = Clip("Assets/_Project/Art/Characters/NPCs/Animations/Hung_TrungDan.fbx");
            assets.idle = Clip(folder + "Clips/Breathing Idle.fbx");
            assets.startled = Clip(folder + "Enemy/Enemy_Startled.fbx");
            assets.enemyRun = Clip(folder + "Enemy/Enemy_Rifle_Run.fbx");
            assets.enemyAim = Clip(folder + "Enemy/Enemy_Rifle_AimIdle.fbx");
            assets.enemyFire = Clip(folder + "Enemy/Enemy_Rifle_Fire.fbx");
            assets.enemyWalk = Clip(folder + "Enemy/Enemy_Patrol_Walk.fbx");
            assets.unarmedWalk = Clip(folder + "Clips/Standard Walk.fbx");
            assets.concussionProfile = BuildConcussion();
            assets.footMeshes = BuildFootContacts();
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            Debug.Log("[Tutorial capture] Clip references saved; no scene or map geometry modified.");
        }
        private static TutorialCaptureAssets.FootMesh[] BuildFootContacts()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Build tutorial foot contacts outside Play Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerHung.prefab");
            var root = UnityEngine.Object.Instantiate(prefab); root.hideFlags = HideFlags.HideAndDontSave;
            try {
                var actor = root.GetComponentsInChildren<Animator>().First(a => a.isHuman); actor.Rebind();
                var result = new System.Collections.Generic.List<TutorialCaptureAssets.FootMesh>();
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    var mesh = skin.sharedMesh; if (mesh == null) continue;
                    var vertices = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes; var bones = skin.bones;
                    TutorialCaptureAssets.FootVertex[] Foot(HumanBodyBones id)
                    {
                        var foot = actor.GetBoneTransform(id); var samples = new System.Collections.Generic.List<TutorialCaptureAssets.FootVertex>();
                        for (int i = 0; i < vertices.Length && i < weights.Length; i++) {
                            var w = weights[i]; var indices = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                            var strength = new Vector4(w.weight0, w.weight1, w.weight2, w.weight3); bool onFoot = false, valid = true;
                            var local = new Vector3[4];
                            for (int j = 0; j < 4; j++) if (strength[j] > 0) {
                                int index = indices[j];
                                if (index < 0 || index >= bones.Length || index >= bind.Length || bones[index] == null) { valid = false; break; }
                                if (strength[j] > .5f && bones[index] == foot) onFoot = true;
                                local[j] = bind[index].MultiplyPoint3x4(vertices[i]);
                            }
                            if (valid && onFoot) samples.Add(new TutorialCaptureAssets.FootVertex { vertexIndex = i, boneIndices = indices, boneLocalPoints = local, weights = strength });
                        }
                        return samples.ToArray();
                    }
                    var data = new TutorialCaptureAssets.FootMesh { rendererPath = AnimationUtility.CalculateTransformPath(skin.transform, actor.transform), vertexCount = mesh.vertexCount,
                        left = Foot(HumanBodyBones.LeftFoot), right = Foot(HumanBodyBones.RightFoot) };
                    if (data.left.Length > 0 || data.right.Length > 0) result.Add(data);
                }
                if (result.Count == 0) throw new InvalidOperationException("Hùng's foot contact mesh was not found.");
                return result.ToArray();
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static UnityEngine.Rendering.VolumeProfile BuildConcussion()
        {
            const string path = "Assets/_Project/Resources/TutorialConcussion.asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            T Effect<T>() where T : UnityEngine.Rendering.VolumeComponent
            {
                if (!profile.TryGet<T>(out var effect)) { effect = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(effect, profile); }
                effect.SetAllOverridesTo(true); EditorUtility.SetDirty(effect); return effect;
            }
            var blur = Effect<UnityEngine.Rendering.Universal.DepthOfField>();
            blur.mode.value = UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh;
            blur.focusDistance.value = .1f; blur.focalLength.value = 75; blur.aperture.value = 1;
            var vignette = Effect<UnityEngine.Rendering.Universal.Vignette>();
            vignette.intensity.value = .45f; vignette.smoothness.value = .6f; vignette.rounded.value = true;
            var motion = Effect<UnityEngine.Rendering.Universal.MotionBlur>();
            motion.mode.value = UnityEngine.Rendering.Universal.MotionBlurMode.CameraOnly;
            motion.quality.value = UnityEngine.Rendering.Universal.MotionBlurQuality.Medium;
            motion.intensity.value = .35f; motion.clamp.value = .08f;
            EditorUtility.SetDirty(profile); return profile;
        }
        private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"))
            ?? throw new InvalidOperationException("Missing cinematic clip: " + path);
    }
}
