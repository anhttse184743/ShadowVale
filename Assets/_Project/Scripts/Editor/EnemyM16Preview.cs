using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ShadowVale.Map01;
using Object = UnityEngine.Object;
namespace ShadowVale.Editor
{
    [InitializeOnLoad]
    public static class EnemyM16Preview
    {
        const string Folder = "Tools/M16Reports";
        static EnemyM16Preview() { EditorApplication.update += Poll; }
        static void Poll()
        {
            const string request = "Tools/EnemyM16Preview.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Capture(); } catch (Exception e) { File.WriteAllText(Folder+"/enemy-preview.txt",e.ToString());Debug.LogException(e); }
        }
        [MenuItem("ShadowVale/Preview enemy with M16")]
        public static void Capture()
        {
            Directory.CreateDirectory(Folder);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var preview = new PreviewRenderUtility();
            GameObject model = null;
            Map01CinematicPose pose = null;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                var source = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Map01EnemyController>(true))
                    .First(e=>!e.IsBoss && e.GetComponentInChildren<Animator>(true)!=null);
                var sourceActor = source.GetComponentInChildren<Animator>(true);
                model = Object.Instantiate(sourceActor.gameObject);
                model.name = "Enemy M16 preview";
                model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                model.transform.localScale = sourceActor.transform.lossyScale;
                foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                model.SetActive(true);
                preview.AddSingleGO(model);
                var actor = model.GetComponent<Animator>();
                actor.enabled = true;
                actor.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyAnimatorBuilder.ControllerPath);
                actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                actor.Rebind(); actor.Update(0);
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.enabled = true; skin.forceRenderingOff = false; skin.updateWhenOffscreen = true;
                    skin.localBounds = new Bounds(Vector3.up,Vector3.one*8);
                }
                var grip = Map01EnemyGrip.BindPreview(actor);
                var rifle = model.GetComponent<Map01Rifle>();
                if(grip==null||!grip.Calibrated||rifle==null)throw new Exception("Enemy rifle grip was not calibrated");
                Map01RifleVisual.Apply(rifle.weapon,Map01M16.Id);
                pose = new Map01CinematicPose(actor,skinGrounding:false);
                preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.camera.backgroundColor=new Color(.10f,.12f,.13f);
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
                preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,30,0);
                preview.lights[1].intensity=1.2f;preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0);
                preview.ambientColor=new Color(.5f,.5f,.5f);
                ShaderUtil.allowAsyncCompilation=false;
                pose.Sample(StoryAnimationSetup.Clip("","Enemy_Rifle_Fire"),.05f,true);grip.Apply(false);
                Photo(preview,new Vector3(2.7f,1.65f,3.2f),new Vector3(0,.9f,.15f),30,"enemy-m16-fire.png");
                Photo(preview,new Vector3(-2.3f,1.65f,1.6f),new Vector3(0,1.3f,.2f),30,"enemy-m16-fire-close.png");
                File.WriteAllText(Folder+"/enemy-preview.txt","PASS: "+source.name+"; avatar="+sourceActor.avatar.name+"; existing enemy firing clip and runtime rifle mount; M16 visual.");
            }
            finally
            {
                pose?.Dispose();preview.Cleanup();ShaderUtil.allowAsyncCompilation=async;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        static void Photo(PreviewRenderUtility preview,Vector3 eye,Vector3 focus,float fov,string filename)
        {
            var camera=preview.camera;camera.transform.position=eye;camera.transform.LookAt(focus);camera.fieldOfView=fov;
            var target=RenderTexture.GetTemporary(960,900,24);var old=RenderTexture.active;
            var pixels=new Texture2D(960,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,960,900),0,0);pixels.Apply();File.WriteAllBytes(Folder+"/"+filename,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(pixels);}
        }
    }
}
