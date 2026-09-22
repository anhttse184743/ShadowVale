using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Map01;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Swaps Map 1's placeholder capsules for the real character models.
    /// <para>
    /// Every character in that scene is the same stand-in: an empty <c>VisualRoot</c> holding a
    /// capsule "Body" and a sphere "Helmet". The gameplay pieces — CharacterController on the
    /// player, NavMeshAgent on the rest — live on the parent, so only the contents of VisualRoot
    /// are touched here. Nothing else about the scene moves.
    /// </para>
    /// <para>
    /// Re-runnable: the models go back into VisualRoot each time, so this can be run again after
    /// the map is re-authored or a character is re-exported.
    /// </para>
    /// Menu <b>ShadowVale ▸ Map 1 ▸ Swap Capsules For Characters</b>.
    /// </summary>
    public static class Map01CharacterPopulator
    {
        private const string PlayerModelPath = "Assets/_Project/Art/Characters/Player/Player.fbx";
        private const string SoldierModelPath = "Assets/_Project/Art/Characters/Enemies/lính rig.fbx";

        private const string PlayerControllerPath =
            "Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";
        private const string EnemyControllerPath =
            "Assets/_Project/Art/Characters/Animations/Controllers/AC_Enemy.controller";

        /// <summary>The slot the models are parented under, and the only thing this rewrites.</summary>
        private const string VisualRootName = "VisualRoot";

        /// <summary>The one character the player drives. It gets the player model and animator.</summary>
        private const string PlayerCharacterName = "Nam";

        /// <summary>
        /// Left as a capsule on purpose — the companion has no model yet, and a soldier standing
        /// in for him would read as one more enemy.
        /// </summary>
        private const string SkippedCharacterName = "Hung";

        /// <summary>
        /// Hip height every character is scaled to, in metres. Matches the 1.8 m
        /// CharacterController already on the player, so the model fills its own collider.
        /// </summary>
        private const float TargetHipHeight = 0.99f;

        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        [MenuItem("ShadowVale/Map 1/Migrate Gameplay From Map Test")]
        public static void MigrateGameplayFromMapTest()
        {
            Scene scene = SceneManager.GetActiveScene();
            var oldMission = Object.FindFirstObjectByType<ForestMission>();
            if (oldMission == null)
            {
                Debug.LogWarning("[Map01] ForestMission was already removed.");
                return;
            }

            Transform oldPlayer = oldMission.player;
            Transform parent = oldPlayer != null ? oldPlayer.parent : oldMission.transform;
            Vector3 position = oldPlayer != null ? oldPlayer.position : Vector3.zero;
            Quaternion rotation = oldPlayer != null ? oldPlayer.rotation : Quaternion.identity;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError("[Map01] Missing Player.prefab; Map 1 was not changed.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            player.transform.SetParent(parent, true);
            player.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(player, "Bring Map Test player to Map 1");
            if (oldPlayer != null) Undo.DestroyObjectImmediate(oldPlayer.gameObject);

            Camera camera = oldMission.gameCamera != null ? oldMission.gameCamera : Camera.main;
            if (camera != null)
            {
                var oldCamera = camera.GetComponent<ForestThirdPersonCamera>();
                if (oldCamera != null) Undo.DestroyObjectImmediate(oldCamera);
                var follow = camera.GetComponent<ThirdPersonCamera>() ?? Undo.AddComponent<ThirdPersonCamera>(camera.gameObject);
                follow.SetTarget(player.transform);
                camera.orthographic = false;
                camera.fieldOfView = 65f;
            }

            var combat = player.GetComponent<PlayerCombat>();
            foreach (var hotbar in Object.FindObjectsByType<ShadowVale.UI.HUD.WeaponHotbar>(FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(hotbar.gameObject);
            if (combat != null) HudBuilder.CreateHud(parent, combat);

            foreach (var guard in Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None))
            {
                Vector3[] patrol = guard.patrol != null ? (Vector3[])guard.patrol.Clone() : System.Array.Empty<Vector3>();
                var animator = guard.GetComponentInChildren<Animator>();
                var health = guard.GetComponent<Health>() ?? Undo.AddComponent<Health>(guard.gameObject);
                var healthSo = new SerializedObject(health);
                healthSo.FindProperty("maxHealth").floatValue = 100f;
                healthSo.FindProperty("animator").objectReferenceValue = animator;
                healthSo.ApplyModifiedPropertiesWithoutUndo();
                if (guard.GetComponent<Collider>() == null)
                {
                    var capsule = Undo.AddComponent<CapsuleCollider>(guard.gameObject);
                    capsule.height = 1.8f;
                    capsule.radius = .35f;
                    capsule.center = Vector3.up * .9f;
                }
                var enemy = guard.GetComponent<Map01EnemyController>() ?? Undo.AddComponent<Map01EnemyController>(guard.gameObject);
                enemy.Configure(patrol);
                Undo.DestroyObjectImmediate(guard);
            }

            foreach (var coordinator in Object.FindObjectsByType<ForestSquadCoordinator>(FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(coordinator);
            Undo.DestroyObjectImmediate(oldMission);

            EditorSceneManagerMarkDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[Map01] Migrated Player, combat, camera, HUD and enemies from Map Test; ForestMission removed.");
        }

        [MenuItem("ShadowVale/Map 1/Swap Capsules For Characters")]
        public static void Run()
        {
            var playerModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            var soldierModel = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierModelPath);
            if (playerModel == null || soldierModel == null)
            {
                Debug.LogError($"[Map01] Missing a character model — player: {playerModel != null}, " +
                               $"soldier: {soldierModel != null}. Nothing was changed.");
                return;
            }

            var playerController = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            var enemyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);

            Scene scene = SceneManager.GetActiveScene();
            List<Transform> slots = FindVisualRoots(scene);
            if (slots.Count == 0)
            {
                Debug.LogWarning($"[Map01] No '{VisualRootName}' objects in '{scene.name}'. " +
                                 "Is Map 1 the open scene?");
                return;
            }

            int players = 0, soldiers = 0, skipped = 0;
            foreach (Transform slot in slots)
            {
                string owner = slot.parent != null ? slot.parent.name : slot.name;
                if (owner == SkippedCharacterName)
                {
                    skipped++;
                    continue;
                }

                bool isPlayer = owner == PlayerCharacterName;
                GameObject model = isPlayer ? playerModel : soldierModel;
                AnimatorController controller = isPlayer ? playerController : enemyController;

                if (Populate(slot, model, controller, owner))
                {
                    if (isPlayer) players++;
                    else soldiers++;
                }
            }

            EditorSceneManagerMarkDirty(scene);
            Debug.Log($"[Map01] {players} player, {soldiers} soldier(s), {skipped} left as a capsule. " +
                      "Save the scene to keep it.");
        }

        /// <summary>
        /// Replaces one slot's contents. Returns false when the model could not be measured, in
        /// which case the slot is left exactly as it was rather than half-built.
        /// </summary>
        private static bool Populate(Transform slot, GameObject model,
            AnimatorController controller, string owner)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null)
            {
                Debug.LogWarning($"[Map01] {owner}: could not instantiate {model.name}.");
                return false;
            }

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            instance.name = "Character";

            Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar = FindAvatar(model);
            animator.applyRootMotion = false;
            // The map is big enough that a character can sit off screen; without this their pose
            // freezes and they snap when the camera comes back round.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }

            // Parent before measuring: the scale below is read off world positions, which only
            // settle once the object is sitting under the slot's own transform.
            instance.transform.SetParent(slot, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            if (!Normalize(instance, slot))
            {
                Object.DestroyImmediate(instance);
                Debug.LogWarning($"[Map01] {owner}: {model.name} has no usable humanoid bones — " +
                                 "capsule left in place.");
                return false;
            }

            // Only now is the replacement known to be good, so the placeholder can go.
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                Transform child = slot.GetChild(i);
                if (child != instance.transform)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            Undo.RegisterCreatedObjectUndo(instance, "Swap capsule for character");
            return true;
        }

        /// <summary>
        /// Scales the model to a fixed hip height and drops it so the soles meet the slot's
        /// origin. Measured from bones rather than renderer bounds: a skinned mesh's bounds cover
        /// its whole animation range, so they report a standing character as far taller than it is.
        /// </summary>
        private static bool Normalize(GameObject instance, Transform slot)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return false;
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || leftFoot == null || rightFoot == null)
            {
                return false;
            }

            float hipHeight = hips.position.y - slot.position.y;
            if (hipHeight <= 0.01f)
            {
                return false;
            }

            float scale = TargetHipHeight / hipHeight;
            instance.transform.localScale = Vector3.one * scale;

            // Re-read after scaling: the ankles have moved. The sole sits an ankle below them.
            float ankle = Mathf.Min(leftFoot.position.y, rightFoot.position.y) - slot.position.y;
            float sole = ankle - 0.1f * scale;
            if (Mathf.Abs(sole) > 0.005f)
            {
                instance.transform.localPosition -= new Vector3(0f, sole, 0f);
            }
            return true;
        }

        private static List<Transform> FindVisualRoots(Scene scene)
        {
            var found = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == VisualRootName)
                    {
                        found.Add(t);
                    }
                }
            }
            return found;
        }

        private static Avatar FindAvatar(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is Avatar avatar)
                {
                    return avatar;
                }
            }
            return null;
        }

        private static void EditorSceneManagerMarkDirty(Scene scene)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
