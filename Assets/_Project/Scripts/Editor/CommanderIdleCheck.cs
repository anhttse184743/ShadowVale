using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShadowVale.Map01;

[InitializeOnLoad]
public static class CommanderIdleCheck
{
    static double until;
    static string previous;
    static CommanderIdleCheck() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Tools/CommanderIdleCheck.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(request);
            previous = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
            SessionState.SetString("CommanderIdleStart", previous);
            SessionState.SetBool("CommanderIdleCheck", true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");
            EditorApplication.isPlaying = true;
        }
        if (!SessionState.GetBool("CommanderIdleCheck", false)) return;
        if (!EditorApplication.isPlaying) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "01_MainMenu")
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Maps/Map 1.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            return;
        }
        var opening = UnityEngine.Object.FindFirstObjectByType<Map01OpeningCutscene>();
        if (opening == null || opening.CommanderTransform == null) return;
        if (until == 0) { until = EditorApplication.timeSinceStartup + 5; return; }
        if (EditorApplication.timeSinceStartup < until) return;
        var boss = UnityEngine.Object.FindFirstObjectByType<Map01Quest>().CommanderEnemy;
        var bossActor = boss != null ? boss.GetComponentInChildren<Animator>() : null;
        var bossSkins = boss != null ? boss.GetComponentsInChildren<SkinnedMeshRenderer>() : Array.Empty<SkinnedMeshRenderer>();
        bool bossValid = boss != null && boss.IsBoss && bossActor != null && bossActor.isHuman && bossActor.avatar.isValid && bossSkins.Length > 0;
        string bossReport = "Boss=" + (boss != null ? boss.name : "missing") + "\n";
        foreach (var skin in bossSkins)
        {
            string source = AssetDatabase.GetAssetPath(skin.sharedMesh);
            bossValid &= skin.sharedMesh != null && source == "Assets/_Project/Art/Characters/Enemies/chỉ huy mỹ.fbx";
            bossReport += "Mesh source=" + source + "\n";
            foreach (var mat in skin.sharedMaterials) bossValid &= mat != null && mat.mainTexture != null;
        }
        File.WriteAllText("Tools/BossCommanderCheck.result.txt", (bossValid ? "PASS" : "FAIL") + "\n" + bossReport + "Avatar=" + (bossActor != null ? AssetDatabase.GetAssetPath(bossActor.avatar) : "missing"));
        float sole = float.PositiveInfinity;
        var baked = new Mesh();
        foreach (var skin in opening.CommanderTransform.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.BakeMesh(baked, true);
            foreach (var v in baked.vertices) sole = Mathf.Min(sole, skin.transform.TransformPoint(v).y);
        }
        UnityEngine.Object.DestroyImmediate(baked);
        var anchor = UnityEngine.Object.FindFirstObjectByType<Map01Mission>().alliedCommanderPost.position;
        float floor = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(anchor + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore))
            if (hit.normal.y > .65f && hit.point.y <= anchor.y + .5f && !hit.transform.IsChildOf(opening.CommanderTransform)) floor = Mathf.Max(floor, hit.point.y);
        float gap = sole - floor;
        File.WriteAllText("Tools/CommanderIdleCheck.result.txt", $"Idle after 5 seconds: total root correction {opening.CommanderGroundCorrection:F4}m; independently baked sole {sole:F5}m; floor {floor:F5}m; gap {gap:F5}m; model scale {opening.CommanderTransform.localScale}.\n" + (Mathf.Abs(gap) < .005f ? "PASS" : "FAIL"));
        SessionState.SetBool("CommanderIdleCheck", false);
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("CommanderIdleStart", ""));
        until = 0;
    }
}
