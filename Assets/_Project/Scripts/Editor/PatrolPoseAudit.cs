using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01;

[InitializeOnLoad]
public static class PatrolPoseAudit
{
    const string Source = "Assets/_Project/Art/Characters/Animations/Enemy/Enemy_Patrol_Walk.fbx";
    const string Raw = "Assets/_Project/Art/Characters/Animations/Enemy/PatrolRawAudit.fbx";
    static PatrolPoseAudit() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Tools/PatrolPoseAudit.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Run(); } catch(System.Exception e) { File.WriteAllText("Tools/PatrolPoseAudit.txt", e.ToString()); }
    }
    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
    static void Run()
    {
        var log = new StringBuilder();
        File.Copy(Source, Raw, true);
        AssetDatabase.ImportAsset(Raw, ImportAssetOptions.ForceSynchronousImport);
        try
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Raw);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var original = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Raw));
            try
            {
                log.AppendLine("RAW FBX / Generic (no Humanoid retarget)");
                for(int i=0;i<4;i++) { Clip(Raw).SampleAnimation(original,i*.25f); Measure(original,log,i*.25f); }
            }
            finally { UnityEngine.Object.DestroyImmediate(original); }
            foreach(string modelPath in new[] { "Assets/_Project/Art/Characters/Enemies/lính rig.fbx", "Assets/_Project/Map01/Resources/Characters/EnemyCommanderVisual.prefab" })
            {
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                try
                {
                    model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    log.AppendLine("HUMANOID: " + modelPath);
                    var actor = model.GetComponent<Animator>();
                    using var pose = new Map01CinematicPose(actor, skinGrounding:false);
                    for(int i=0;i<4;i++) { pose.Sample(Clip(Source), i*.25f); Measure(model,log,i*.25f); }
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
            File.WriteAllText("Tools/PatrolPoseAudit.txt",log.ToString());
        }
        finally { AssetDatabase.DeleteAsset(Raw); }
    }
    static void Measure(GameObject model,StringBuilder log,float time)
    {
        var nodes = model.GetComponentsInChildren<Transform>(true);
        Transform Bone(string name) => nodes.First(t => t.name == name);
        float height = Vector3.Distance(Bone("Head").position,Bone("LeftFoot").position);
        log.AppendLine("t="+time+" skeletonHeight="+height.ToString("F3"));
        foreach(string side in new[] { "Right", "Left" })
        {
            var hand=Bone(side+"Hand"); var lower=Bone(side+"ForeArm"); var upper=Bone(side+"Arm");
            var endpoint=hand.Cast<Transform>().FirstOrDefault();
            var headOffset = (hand.position-Bone("Head").position)/height;
            float wrist=endpoint!=null?Vector3.Angle(hand.position-lower.position,endpoint.position-hand.position):float.NaN;
            float elbow=Vector3.Angle(lower.position-upper.position,hand.position-lower.position);
            log.AppendLine(side+" wristDeg="+wrist.ToString("F1")+" elbowBend="+elbow.ToString("F1")+" handRelativeToHead="+headOffset.ToString("F3"));
        }
    }
}
