using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Run only on a copy of the prefab first. The existing animator and rig stay in place.
public static class NamAoBaBaInstall
{
    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, name);
        if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("Missing argument " + name);
        return args[at + 1].Replace('\\', '/');
    }

    public static void Run()
    {
        try
        {
            string prefabPath = Argument("-namPrefab");
            string skinPath = Argument("-namSkin");
            string outputFolder = Argument("-namOutput");
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var skinAsset = AssetDatabase.LoadAssetAtPath<GameObject>(skinPath);
            if (original == null || skinAsset == null) throw new InvalidOperationException("Prefab or new skin is missing.");
            if (AssetDatabase.IsValidFolder(outputFolder)) throw new InvalidOperationException("Output folder exists; refusing to overwrite existing assets.");
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
            GameObject prefab = null, source = null;
            try
            {
                prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                // The isolated audit project does not contain the game's gameplay assemblies.
                // Never pass this flag in the real game project.
                if (Environment.GetCommandLineArgs().Contains("-namStripMissingForIsolatedTest"))
                    foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                source = UnityEngine.Object.Instantiate(skinAsset);
                var animator = prefab.GetComponentsInChildren<Animator>(true).Single();
                if (animator.transform.Find("Nam_AoBaBa_Visual") != null)
                    throw new InvalidOperationException("Nam skin already installed; refusing duplicate.");
                var targets = animator.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name)
                    .ToDictionary(g => g.Key, g => g.ToArray());
                var sourceRenderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (sourceRenderers.Length != 3) throw new InvalidOperationException("Expected three new skin meshes.");
                var oldRenderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (oldRenderers.Length == 0) throw new InvalidOperationException("Old body meshes not found.");
                foreach (var item in sourceRenderers)
                {
                    if (item.sharedMesh == null || item.bones.Any(b => b == null || !targets.ContainsKey(b.name) || targets[b.name].Length != 1))
                        throw new InvalidOperationException("Missing or ambiguous skeleton bone for " + item.name);
                    if (item.sharedMaterials.Any(m => m == null))
                        throw new InvalidOperationException("Missing material for " + item.name);
                }
                bool useBindRest=Environment.GetCommandLineArgs().Contains("-namBindRestPose");
                var targetRest=new Dictionary<Transform,Matrix4x4>();
                var sourceRest=new Dictionary<Transform,Matrix4x4>();
                if(useBindRest)
                {
                    void CollectRest(SkinnedMeshRenderer renderer,Dictionary<Transform,Matrix4x4> result)
                    {
                        if(renderer.sharedMesh==null)return;
                        var binds=renderer.sharedMesh.bindposes;
                        for(int i=0;i<Math.Min(renderer.bones.Length,binds.Length);i++)
                            if(renderer.bones[i]!=null&&!result.ContainsKey(renderer.bones[i]))
                                result[renderer.bones[i]]=renderer.transform.localToWorldMatrix*binds[i].inverse;
                    }
                    foreach(var item in oldRenderers)CollectRest(item,targetRest);
                    foreach(var item in sourceRenderers)CollectRest(item,sourceRest);
                    foreach(var bone in sourceRenderers.SelectMany(r=>r.bones).Where(b=>b!=null).Distinct())
                        if(!sourceRest.ContainsKey(bone)||!targetRest.ContainsKey(targets[bone.name][0]))
                            throw new InvalidOperationException("Missing rest bind matrix for "+bone.name);
                }
                Matrix4x4 sourceToTarget = animator.transform.localToWorldMatrix * source.transform.worldToLocalMatrix;
                if (Environment.GetCommandLineArgs().Contains("-namNormalizeSkeleton"))
                {
                    var sourceBones=source.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                    var srcHips=sourceBones["Hips"]; var srcHead=sourceBones["Head"];
                    var dstHips=targets["Hips"][0]; var dstHead=targets["Head"][0];
                    Vector3 sourceHip=useBindRest?sourceRest[srcHips].GetColumn(3):srcHips.position;
                    Vector3 sourceHead=useBindRest?sourceRest[srcHead].GetColumn(3):srcHead.position;
                    Vector3 targetHip=useBindRest?targetRest[dstHips].GetColumn(3):dstHips.position;
                    Vector3 targetHead=useBindRest?targetRest[dstHead].GetColumn(3):dstHead.position;
                    float mappedDistance=Vector3.Distance(sourceToTarget.MultiplyPoint3x4(sourceHip),sourceToTarget.MultiplyPoint3x4(sourceHead));
                    float targetDistance=Vector3.Distance(targetHip,targetHead);
                    if(mappedDistance<1e-6f||targetDistance<1e-6f)throw new InvalidOperationException("Cannot normalize skeleton scale.");
                    float factor=targetDistance/mappedDistance;
                    sourceToTarget=animator.transform.localToWorldMatrix*Matrix4x4.Scale(Vector3.one*factor)*source.transform.worldToLocalMatrix;
                    sourceToTarget=Matrix4x4.Translate(targetHip-sourceToTarget.MultiplyPoint3x4(sourceHip))*sourceToTarget;
                    Debug.Log("Normalized skin to target skeleton, scale factor: "+factor);
                }
                var visual = new GameObject("Nam_AoBaBa_Visual");
                visual.transform.SetParent(animator.transform, false);
                foreach (var item in sourceRenderers)
                {
                    var child = new GameObject(item.name);
                    child.transform.SetParent(visual.transform, false);
                    Matrix4x4 relative = animator.transform.worldToLocalMatrix * sourceToTarget * item.transform.localToWorldMatrix;
                    child.transform.localPosition = relative.GetColumn(3);
                    child.transform.localRotation = relative.rotation;
                    child.transform.localScale = relative.lossyScale;
                    var targetBones = item.bones.Select(b => targets[b.name][0]).ToArray();
                    var mesh = UnityEngine.Object.Instantiate(item.sharedMesh);
                    mesh.name = "Nam_AoBaBa_" + item.name;
                    var bind = item.sharedMesh.bindposes;
                    if (bind.Length != targetBones.Length) throw new InvalidOperationException("Invalid bind pose count: " + item.name);
                    mesh.bindposes = bind.Select((matrix, i) => (useBindRest?targetRest[targetBones[i]].inverse:targetBones[i].worldToLocalMatrix) * sourceToTarget *
                        (useBindRest?sourceRest[item.bones[i]]:item.bones[i].localToWorldMatrix) * matrix).ToArray();
                    string meshPath = outputFolder + "/" + mesh.name + ".asset";
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    var renderer = child.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh;
                    renderer.sharedMaterials = item.sharedMaterials;
                    renderer.bones = targetBones;
                    renderer.rootBone = item.rootBone != null && targets.ContainsKey(item.rootBone.name) ? targets[item.rootBone.name][0] : animator.transform;
                    renderer.localBounds = item.localBounds;
                    renderer.quality = SkinQuality.Bone4;
                    renderer.updateWhenOffscreen = item.updateWhenOffscreen;
                    renderer.shadowCastingMode = item.shadowCastingMode;
                    renderer.receiveShadows = item.receiveShadows;
                }
                foreach (var old in oldRenderers) old.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath, out bool success);
                if (!success) throw new InvalidOperationException("Could not save updated player prefab.");
                AssetDatabase.SaveAssets();
                Debug.Log("Nam installation: " + sourceRenderers.Length + " meshes bound to existing animator; disabled " + oldRenderers.Length + " old meshes.");
            }
            finally
            {
                if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab);
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
            }
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
