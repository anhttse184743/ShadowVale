using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class HungPlayerSetup
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Player/PlayerHung.prefab";
        public const string ModelPath = "Assets/_Project/Art/Characters/NPCs/hung.fbx";
        [MenuItem("ShadowVale/Characters/Setup Hung Player")]
        public static void Run()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid) throw new InvalidOperationException("Hùng requires a valid Humanoid avatar.");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(original);
            try {
                root.name = "PlayerHung";
                var actor = root.GetComponentsInChildren<Animator>(true).First(a => a.name == "Model");
                var model = actor.transform;
                var anchor = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponAnchor");
                Vector3 anchorPosition = anchor.localPosition, anchorScale = anchor.localScale; Quaternion anchorRotation = anchor.localRotation;
                // A variant cannot remove inherited GameObjects. Retain them disabled, with unique
                // names so Hùng's avatar resolves the new skeleton rather than Nam's inherited one.
                foreach (var bone in model.GetComponentsInChildren<Transform>(true).Where(t => t != model)) bone.name = "UnusedNam_" + bone.name;
                foreach (Transform child in model) child.gameObject.SetActive(false);
                var fresh = (GameObject)PrefabUtility.InstantiatePrefab(imported);
                PrefabUtility.UnpackPrefabInstance(fresh, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                while (fresh.transform.childCount > 0) fresh.transform.GetChild(0).SetParent(model, false);
                Object.DestroyImmediate(fresh);
                actor.avatar = avatar;
                actor.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller");
                model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
                model.localScale = Vector3.one;
                var hand = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "RightHand");
                anchor = new GameObject("WeaponAnchor").transform;
                anchor.SetParent(hand, false); anchor.SetLocalPositionAndRotation(anchorPosition, anchorRotation); anchor.localScale = anchorScale;
                var combat = new SerializedObject(root.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>());
                combat.FindProperty("handAnchor").objectReferenceValue = anchor; combat.ApplyModifiedPropertiesWithoutUndo();
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Characters/NPCs/Materials/hung - Material.001.mat");
                BalanceToNam(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            } finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("ShadowVale/Characters/Match Hung Player Size To Nam")]
        public static void MatchNamScale()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { BalanceToNam(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BalanceToNam(GameObject root)
        {
            var nam = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var reference = MeasureStanding(nam); var current = MeasureStanding(root);
            var model = root.GetComponentsInChildren<Animator>(true).First(a => a.name == "Model").transform;
            float factor = reference.stature / current.stature;
            model.localScale *= factor;
            var resized = MeasureStanding(root);
            model.localPosition += Vector3.up * (reference.sole - resized.sole);
            var sourceBody = nam.GetComponent<CharacterController>(); var body = root.GetComponent<CharacterController>();
            body.height = sourceBody.height; body.center = sourceBody.center; body.radius = sourceBody.radius;
            if (root.GetComponent<HungPlayerGrip>() == null) root.AddComponent<HungPlayerGrip>();
            Debug.Log($"[Hung player] Uniform scale ×{factor:F3}; standing stature matches Nam, capsule {body.height:F2} m. Nam and source FBX are unchanged.");
        }

        // Compare the same evaluated pose. Renderer.bounds includes animation padding and
        // the captive NPC's height is not the playable character's standing size.
        internal static (float stature, float sole) MeasureStanding(GameObject prefab)
        {
            var sample = Object.Instantiate(prefab);
            sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try {
                var actor = sample.GetComponentsInChildren<Animator>(true).First(a => a.name == "Model");
                var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Clips/Breathing Idle.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
                using (var pose = new Map01Extraction.PosePlayer(actor)) {
                    pose.Play(idle); pose.Settle(); pose.Rate(1); pose.Tick(.2f);
                    float feet = (actor.GetBoneTransform(HumanBodyBones.LeftFoot).position.y + actor.GetBoneTransform(HumanBodyBones.RightFoot).position.y) * .5f;
                    float stature = actor.GetBoneTransform(HumanBodyBones.Head).position.y - feet, sole = float.PositiveInfinity;
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                        if (!skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                        var baked = new Mesh();
                        // useScale=true compensates each renderer's own FBX unit scale before
                        // TransformPoint; false applies the character/gear scale a second time.
                        skin.BakeMesh(baked, true);
                        foreach (var vertex in baked.vertices) sole = Mathf.Min(sole, skin.transform.TransformPoint(vertex).y);
                        Object.DestroyImmediate(baked);
                    }
                    if (stature < .5f || float.IsInfinity(sole)) throw new InvalidOperationException("Cannot measure the standing character.");
                    return (stature, sole);
                }
            } finally { Object.DestroyImmediate(sample); }
        }
    }
}
