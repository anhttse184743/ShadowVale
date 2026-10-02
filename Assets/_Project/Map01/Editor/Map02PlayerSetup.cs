using System;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class Map02PlayerSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Maps/Map 2.unity";
        const string Reports = "Tools/Map02Reports";
        [MenuItem("ShadowVale/Map 2/Add exploration player at boat landing")]
        public static void Build()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.isDirty)throw new Exception("Save current edits first.");
            if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
            if(Object.FindFirstObjectByType<Map02Exploration>()!=null)throw new Exception("Exploration player already exists.");
            Directory.CreateDirectory(Reports);
            if(!File.Exists(Reports+"/Map2_before_player.unity"))EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_player.unity",true);
            var landing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name=="Timber evacuation landing").OrderByDescending(t=>t.position.z).First();
            var root=new GameObject("04 Exploration • player at boat landing");
            var rig=root.AddComponent<Map02Exploration>();
            var spawn=new GameObject("Boat landing spawn • bank end").transform;spawn.SetParent(root.transform,false);
            var p=landing.position-landing.right*3;
            p.y=landing.GetComponent<Collider>().bounds.max.y+.06f;
            spawn.SetPositionAndRotation(p,Quaternion.LookRotation(-landing.right,Vector3.up));
            rig.boatLandingSpawn=spawn;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
            player.name="Player • Map 2 explorer";player.transform.SetPositionAndRotation(p,spawn.rotation);
            rig.player=player.GetComponent<PlayerController>();
            foreach(var behaviour in player.GetComponents<MonoBehaviour>())
                if(behaviour.GetType().Name=="PlayerCombat"||behaviour.GetType().Name=="WeaponHotbar") {behaviour.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);}
            foreach(var old in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>())) {old.enabled=false;old.tag="Untagged";if(old.TryGetComponent<AudioListener>(out var listener))listener.enabled=false;}
            var cameraObject=new GameObject("Main Camera • Map 2 third person");cameraObject.transform.SetParent(root.transform,false);cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>();camera.farClipPlane=1800;camera.nearClipPlane=.15f;camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();var follow=cameraObject.AddComponent<ThirdPersonCamera>();rig.followCamera=follow;
            var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.requiresDepthTexture=true;cameraData.requiresColorTexture=true;
            var settings=new SerializedObject(follow);settings.FindProperty("target").objectReferenceValue=player.transform;settings.FindProperty("distance").floatValue=4.5f;settings.FindProperty("maxDistance").floatValue=16;settings.FindProperty("obstructionMask").intValue=~((1<<player.layer)|(1<<4));settings.ApplyModifiedPropertiesWithoutUndo();
            var controllerData=new SerializedObject(rig.player);controllerData.FindProperty("cameraTransform").objectReferenceValue=camera.transform;controllerData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(rig.player);
            camera.transform.SetPositionAndRotation(p-spawn.forward*4.5f+Vector3.up*2.7f,Quaternion.LookRotation(spawn.forward-Vector3.up*.2f));
            Physics.SyncTransforms();
            if(!Physics.Raycast(p+Vector3.up,Vector3.down,out var hit,2,~(1<<player.layer),QueryTriggerInteraction.Ignore)||hit.collider!=landing.GetComponent<Collider>())throw new Exception("Spawn is not on the landing deck.");
            if(player.GetComponentInChildren<Animator>().runtimeAnimatorController==null)throw new Exception("Player animation missing");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Reports+"/player-setup-checks.txt","PASS player prefab, animation and third-person camera\nPASS boat landing spawn supported by deck collider at "+p+"\nPASS overview camera and listener disabled; one gameplay camera\n");
        }
        public static void BuildAndExit(){try{Build();EditorApplication.Exit(0);}catch(Exception e){Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/player-setup-checks.txt","FAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}}
        [MenuItem("ShadowVale/Map 2/Play from boat landing")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);EditorApplication.EnterPlaymode();
        }
    }
}
