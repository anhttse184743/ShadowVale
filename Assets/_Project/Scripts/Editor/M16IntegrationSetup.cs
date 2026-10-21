using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Combat;
using Object = UnityEngine.Object;
namespace ShadowVale.Editor
{
    [InitializeOnLoad]
    public static class M16IntegrationSetup
    {
        const string Request = "Tools/M16Integration.request";
        static M16IntegrationSetup() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Build(); } catch (Exception e) { Directory.CreateDirectory("Tools/M16Reports"); File.WriteAllText("Tools/M16Reports/status.txt",e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("ShadowVale/Build M16 and inventory previews")]
        public static void Build()
        {
            Directory.CreateDirectory("Tools/M16Reports");
            AssetDatabase.ImportAsset("Assets/_Project/Art/Items/Generated/m16_reload.glb", ImportAssetOptions.ForceSynchronousImport);
            if (!WeaponPrefabBuilder.BuildM16()) throw new Exception("M16 model import failed");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/W_M16.prefab");
            var root = Object.Instantiate(prefab); root.name = "M16Visual";
            var vertices = root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null)
                .SelectMany(f=>f.sharedMesh.vertices.Select(v=>root.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
            float front=vertices.Max(v=>v.z);var tip=vertices.Where(v=>v.z>front-.003f).ToArray();
            root.transform.Find("Muzzle").localPosition=new Vector3(tip.Average(v=>v.x),tip.Average(v=>v.y),front);
            Object.DestroyImmediate(root.GetComponent<Weapon>());
            if (ReloadMagazineCarrier.GatherMagazine(root.transform) == null) throw new Exception("M16 magazine was not separated");
            CalibrateMagazine(root);
            Directory.CreateDirectory("Assets/_Project/Map01/Resources/Weapons");
            var visual = PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Project/Map01/Resources/Weapons/M16Visual.prefab");
            Object.DestroyImmediate(root);
            var catalog = Map01ItemCatalog.Load();
            var entries = catalog.items.Where(e => e.id != Map01M16.Id && e.id != Map01M16.Ammo).ToList();
            entries.Add(new Map01ItemCatalog.Entry { id=Map01M16.Id, model=visual, scale=1 });
            var ammo = catalog.Model("ammo_rifle");
            root = Object.Instantiate(ammo); root.name = "Ammo556"; root.transform.localScale *= .85f;
            // Same cartridge-box presentation as AK ammunition, with its own catalog entry and icon label.
            var ammoModel = PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Project/Map01/Resources/Weapons/Ammo556.prefab");
            Object.DestroyImmediate(root);
            entries.Add(new Map01ItemCatalog.Entry { id=Map01M16.Ammo, model=ammoModel, scale=1 });
            catalog.items = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            var rendered = new List<string>();
            foreach (var entry in catalog.items)
            {
                if (entry.model == null) continue;
                Render(entry.id,entry.model,entry.scale > 0 ? entry.scale : 1);
                rendered.Add(entry.id);
            }
            RenderHudM16(visual);
            AssetDatabase.Refresh();
            File.WriteAllText("Tools/M16Reports/status.txt","PASS assets and previews: " + string.Join(", ", rendered));
        }
        static void CalibrateMagazine(GameObject model)
        {
            var actorObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            bool sampling = false;
            try
            {
                foreach (var behaviour in actorObject.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var actor = actorObject.GetComponentInChildren<Animator>();
                var carrier = actor.GetComponent<ReloadMagazineCarrier>();
                var anchor = actor.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponAnchor");
                var rifle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/W_AK47.prefab"), anchor, false);
                AssetDatabase.LoadAssetAtPath<WeaponGripConfig>("Assets/_Project/ScriptableObjects/GameSettings/WeaponGripConfig.asset").Apply(rifle.transform, WeaponKind.Rifle);
                var visual = Object.Instantiate(model, rifle.transform, false);
                visual.transform.localPosition = Map01M16.HeldOffset;
                var magazine = ReloadMagazineCarrier.GatherMagazine(visual.transform);
                var hand = actor.GetBoneTransform(HumanBodyBones.LeftHand);
                var contacts = new List<ReloadMagazineCarrier.Take>();
                AnimationMode.StartAnimationMode(); sampling = true;
                foreach (var original in carrier.Takes)
                {
                    string clipName = original.state == "ReloadEmpty_Rifle" ? "Nam_Rifle_Reload"
                        : original.state == "ReloadCrouch_Rifle" ? "Nam_Crouch_Reload" : "Nam_Rifle_ReloadTactical";
                    var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Nam/" + clipName + ".fbx")
                        .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (clip == null) throw new Exception("Missing reload clip " + clipName);
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(actor.gameObject, clip, original.leaves * clip.length);
                    AnimationMode.EndSampling();
                    var contact = original;
                    contact.inHandPosition = hand.InverseTransformPoint(magazine.position);
                    contact.inHandRotation = Quaternion.Inverse(hand.rotation) * magazine.rotation;
                    contacts.Add(contact);
                }
                if (contacts.Count != 3) throw new Exception("Expected three M16 reload contacts");
                var profile = model.GetComponent<MagazineReloadProfile>() ?? model.AddComponent<MagazineReloadProfile>();
                profile.takes = contacts.ToArray();
            }
            finally
            {
                if (sampling) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(actorObject);
            }
        }
        static void RenderHudM16(GameObject prefab)
        {
            var preview = new PreviewRenderUtility();
            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.AddSingleGO(instance);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(.94f, .92f, .79f, 1));
            material.SetFloat("_Cull", 0); material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", 0);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            }
            var camera = preview.camera;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            camera.transform.position = bounds.center + new Vector3(0, 1, .12f).normalized * Mathf.Max(2, extent * 3);
            camera.transform.LookAt(bounds.center, Vector3.left);
            camera.transform.Rotate(0, 0, -35, Space.Self);
            camera.aspect = 512f / 418f;
            camera.orthographicSize = extent * .43f;
            camera.ResetProjectionMatrix(); camera.ResetWorldToCameraMatrix();
            preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(30, 35, 0);
            preview.lights[1].intensity = .8f; preview.lights[1].transform.rotation = Quaternion.Euler(340, 210, 0);
            preview.ambientColor = new Color(.55f, .55f, .55f);
            camera.nearClipPlane = .001f; camera.farClipPlane = 100;
            var old = RenderTexture.active;
            var target = RenderTexture.GetTemporary(512, 418, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(512, 418, TextureFormat.RGBA32, false);
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 512, 418), 0, 0); texture.Apply();
                File.WriteAllBytes("Assets/_Project/Map01/Resources/Hud/HotbarIcons/" + Map01M16.Id + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = old;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture);
                preview.Cleanup(); Object.DestroyImmediate(material); ShaderUtil.allowAsyncCompilation = async;
            }
        }
        static void Render(string id, GameObject prefab, float scale)
        {
            var preview = new PreviewRenderUtility();
            var instance = Object.Instantiate(prefab); instance.transform.position=Vector3.zero; instance.transform.rotation=Quaternion.identity; instance.transform.localScale*=scale;
            foreach(var b in instance.GetComponentsInChildren<MonoBehaviour>()) b.enabled=false;
            preview.AddSingleGO(instance);
            var renderers=instance.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) { preview.Cleanup(); return; }
            var bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            var cam=preview.camera; cam.orthographic=true; cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.075f,.085f,.06f,1);
            float extent=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            cam.transform.position=bounds.center+new Vector3(1,.55f,.6f).normalized*Mathf.Max(2,extent*3); cam.transform.LookAt(bounds.center);
            cam.orthographicSize=Mathf.Max(.02f,extent*.65f); cam.nearClipPlane=.001f; cam.farClipPlane=1000;
            preview.lights[0].intensity=1.7f; preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);
            preview.lights[1].intensity=1.2f; preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0); preview.ambientColor=new Color(.4f,.4f,.4f);
            bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try
            {
                preview.BeginPreview(new Rect(0,0,512,512),GUIStyle.none); preview.camera.Render();
                var image=preview.EndPreview(); var old=RenderTexture.active;
                var rt=RenderTexture.GetTemporary(512,512,0); Graphics.Blit(image,rt); RenderTexture.active=rt;
                var tex=new Texture2D(512,512,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();
                foreach(string folder in new[]{"ItemIcons","ItemRenders","HotbarIcons"})
                {
                    string dir="Assets/_Project/Map01/Resources/Hud/"+folder;Directory.CreateDirectory(dir);
                    if (id == Map01M16.Id || id == Map01M16.Ammo || !File.Exists(dir+"/"+id+".png")) File.WriteAllBytes(dir+"/"+id+".png",tex.EncodeToPNG());
                }
                RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
            }
            finally { ShaderUtil.allowAsyncCompilation=async;preview.Cleanup(); }
        }
    }
}
