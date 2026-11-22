#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class T007InstallApproved
{
    public const string ControllerPath = "Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";
    public const string Destination = "Assets/_Project/Art/Characters/NamAoBaBa_20261111/Animations/T007_ApprovedV18A";
    public const string TreeName = "T007_Rifle_8_huong";
    public const string MoveName = "T007_Sneak_Rifle_Move";
    const string AvatarGuid = "69052aafa8761084fb62c9c7eaf74a2c";
    static readonly string[] Names = { "Idle", "Fwd", "FwdL", "FwdR", "L", "R", "Back", "BackL", "BackR" };
    [Serializable] class Speeds { public Speed Fwd, FwdL, FwdR, L, R, Back, BackL, BackR; }
    [Serializable] class Speed { public Retimed vong4; }
    [Serializable] class Retimed { public float retarget_scaled_speed_m_s; }

    static byte[] Hash(string path) { using (var sha = SHA256.Create()) using (var f = File.OpenRead(path)) return sha.ComputeHash(f); }
    static AnimatorStateTransition Clone(AnimatorState state, AnimatorStateTransition src)
    {
        AnimatorStateTransition dst;
        if (src.isExit) dst = state.AddExitTransition();
        else if (src.destinationState != null) dst = state.AddTransition(src.destinationState);
        else if (src.destinationStateMachine != null) dst = state.AddTransition(src.destinationStateMachine);
        else throw new Exception("T007: transition khong co dich: " + src.name);
        EditorUtility.CopySerialized(src, dst);
        return dst;
    }
    [MenuItem("ShadowVale/Animation/Install T007 approved V18A")]
    public static void Install()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!string.Equals(project.TrimEnd(Path.DirectorySeparatorChar), @"D:\DO_AN_GAME\ShadowVale", StringComparison.OrdinalIgnoreCase))
            throw new Exception("T007: chi cai vao project ShadowVale duoc duyet.");
        string task = Environment.GetEnvironmentVariable("T007_TASK");
        if (string.IsNullOrEmpty(task)) throw new Exception("T007_TASK chua duoc dat; dung bo chay Claude.");
        string sourceDir = Path.Combine(task, "unity", "T007_KhomNgam_V18");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
        var motor = prefab ? prefab.GetComponentInChildren<PlayerController>(true) : null;
        var avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(AvatarGuid)).OfType<Avatar>().SingleOrDefault(a => a.isValid && a.isHuman);
        if (!controller || !motor || !avatar) throw new Exception("T007: thieu controller, Player hoac avatar Humanoid.");
        var speedProperty = new SerializedObject(motor).FindProperty("sneakSpeed");
        if (speedProperty == null || speedProperty.floatValue <= 0) throw new Exception("T007: sneakSpeed khong hop le.");
        float speed = speedProperty.floatValue;
        foreach (string parameter in new[] { "Speed", "MoveX", "MoveZ" })
            if (!controller.parameters.Any(p => p.name == parameter && p.type == AnimatorControllerParameterType.Float)) throw new Exception("T007: thieu float " + parameter);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.states.Select(s => s.state).Single(s => s.name == "Sneak_Rifle");
        var states = machine.states.Select(s => s.state).Where(s => s.name == MoveName).ToArray();
        var trees = AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>().Where(t => t.name == TreeName).ToArray();
        if (states.Length > 1 || trees.Length > 1) throw new Exception("T007: state/tree trung, dung de kiem tra.");
        var data = JsonUtility.FromJson<Speeds>(File.ReadAllText(Path.Combine(sourceDir, "toc_do.json")));
        if (data == null) throw new Exception("T007: toc_do.json rong.");
        var speeds = new[] { data.Fwd, data.FwdL, data.FwdR, data.L, data.R, data.Back, data.BackL, data.BackR };
        if (speeds.Any(s => s == null || s.vong4 == null || s.vong4.retarget_scaled_speed_m_s <= 0)) throw new Exception("T007: toc do nguon khong hop le.");
        foreach (string name in Names) if (!File.Exists(Path.Combine(sourceDir, "Nam_Rifle_Crouch_Aim_" + name + ".fbx"))) throw new Exception("T007: thieu FBX " + name);
        if (!AssetDatabase.IsValidFolder(Destination))
        {
            string parent = Path.GetDirectoryName(Destination).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent) || string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, "T007_ApprovedV18A"))) throw new Exception("T007: khong tao duoc thu muc dich.");
        }
        var clips = new AnimationClip[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            string file = "Nam_Rifle_Crouch_Aim_" + Names[i] + ".fbx";
            string src = Path.Combine(sourceDir, file), dest = Destination + "/" + file;
            if (!File.Exists(dest)) File.Copy(src, dest, false);
            else if (!Hash(src).SequenceEqual(Hash(dest))) throw new Exception("T007: FBX dich khac ban duyet, khong ghi de: " + dest);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(dest) as ModelImporter;
            if (!importer) throw new Exception("T007: thieu ModelImporter " + dest);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            var settings = importer.clipAnimations;
            if (settings.Length == 0) settings = importer.defaultClipAnimations;
            if (settings.Length != 1) throw new Exception("T007: FBX phai co mot clip: " + dest);
            settings[0].loopTime = true;
            settings[0].lockRootPositionXZ = true;
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            clips[i] = AssetDatabase.LoadAllAssetsAtPath(dest).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview"));
            if (!clips[i].isHumanMotion) throw new Exception("T007: clip khong phai Humanoid " + dest);
        }
        var walk = states.SingleOrDefault();
        if (!walk)
        {
            walk = machine.AddState(MoveName);
            walk.writeDefaultValues = idle.writeDefaultValues; walk.iKOnFeet = idle.iKOnFeet;
            foreach (var transition in idle.transitions) Clone(walk, transition);
        }
        var tree = trees.SingleOrDefault();
        if (!tree) { tree = new BlendTree { name = TreeName }; AssetDatabase.AddObjectToAsset(tree, controller); }
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX"; tree.blendParameterY = "MoveZ"; tree.useAutomaticThresholds = false;
        var directions = new[] { new Vector2(0,1), new Vector2(-.7071068f,.7071068f), new Vector2(.7071068f,.7071068f), new Vector2(-1,0), new Vector2(1,0), new Vector2(0,-1), new Vector2(-.7071068f,-.7071068f), new Vector2(.7071068f,-.7071068f) };
        tree.children = Enumerable.Range(0, 8).Select(i => new ChildMotion { motion = clips[i+1], position = directions[i] * speed, timeScale = speed / speeds[i].vong4.retarget_scaled_speed_m_s }).ToArray();
        idle.motion = clips[0]; idle.speed = 1; idle.speedParameterActive = false; walk.motion = tree;
        var starts = idle.transitions.Where(t => t.destinationState == walk).ToArray();
        var stops = walk.transitions.Where(t => t.destinationState == idle).ToArray();
        if (starts.Length > 1 || stops.Length > 1) throw new Exception("T007: transition trung.");
        var start = starts.SingleOrDefault() ?? idle.AddTransition(walk);
        start.hasExitTime = false; start.hasFixedDuration = true; start.duration = .225f;
        start.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.Greater, threshold = .05f, parameter = "Speed" } };
        var stop = stops.SingleOrDefault() ?? walk.AddTransition(idle);
        stop.hasExitTime = false; stop.hasFixedDuration = true; stop.duration = .225f;
        stop.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.Less, threshold = .05f, parameter = "Speed" } };
        EditorUtility.SetDirty(idle); EditorUtility.SetDirty(walk); EditorUtility.SetDirty(tree); EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        Debug.Log("T007 V18A installed; gameplay and video still require review.");
    }
}
#endif
