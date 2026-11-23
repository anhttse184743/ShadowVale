using System;
using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Lit, isolated inspection views of the same articulated meshes used in the village.</summary>
    public sealed class Map02NightWatchPreview : IDisposable
    {
        const int Layer = 31;
        readonly GameObject root;
        readonly Camera camera, latchCamera;
        readonly Map02NightWatchVisual gate, lamp;
        readonly Material floorMaterial;
        readonly Transform floor;
        Map02NightWatchVisual previousSource;Quaternion previousPose;Vector3 previousBolt;
        public RenderTexture Image { get; }
        public RenderTexture LatchImage { get; }
        public Map02NightWatchPreview()
        {
            root = new GameObject("Night watch inspection studio");
            root.transform.position = new Vector3(0, -250, 0);
            root.hideFlags = HideFlags.DontSave;
            gate = Create("VillageGate"); lamp = Create("ShieldedLamp");
            foreach (var node in lamp.GetComponentsInChildren<Transform>())
                if (node.name == "SupportPost" || node.name == "StandFoot") node.gameObject.SetActive(false);
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.color = new Color(.18f, .15f, .10f); floorMaterial.SetFloat("_Smoothness", .12f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Inspection surface";
            UnityEngine.Object.Destroy(ground.GetComponent<Collider>()); ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(8, .025f, 8); ground.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            floor = ground.transform; ground.layer = Layer;
            Image = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32) { name = "Night prop 3D view" }; Image.Create();
            LatchImage = new RenderTexture(512, 320, 24, RenderTextureFormat.ARGB32) { name = "Latch detail 3D view" }; LatchImage.Create();
            camera = CameraFor("Inspection camera", Image); latchCamera = CameraFor("Latch camera", LatchImage);
            KeyLight(new Vector3(-3, 4, -3), 2.8f); KeyLight(new Vector3(3, 2, 2), 1.1f);
        }
        Map02NightWatchVisual Create(string model)
        {
            var prefab = Resources.Load<GameObject>("Map02DayOne/NightWatch/" + model + "Prop");
            if (prefab == null) throw new InvalidOperationException("Missing night watch model: " + model);
            var go = UnityEngine.Object.Instantiate(prefab, root.transform, false);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach(var obstacle in go.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))obstacle.enabled=false;
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.cullingMask = 1 << Layer;
            var visual = go.GetComponent<Map02NightWatchVisual>(); visual.enabled = false; return visual;
        }
        Camera CameraFor(string name, RenderTexture texture)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var cam = go.AddComponent<Camera>(); cam.enabled = false; cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.045f, .055f, .043f);
            cam.orthographic = true; cam.nearClipPlane = .02f; cam.farClipPlane = 20; cam.targetTexture = texture; return cam;
        }
        void KeyLight(Vector3 local, float intensity)
        {
            var go = new GameObject("Inspection softbox"); go.transform.SetParent(root.transform, false); go.transform.localPosition = local;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = 12; light.intensity = intensity * 12;
            light.color = new Color(1, .91f, .76f); light.cullingMask = 1 << Layer; light.shadows = LightShadows.None;
        }
        void Aim(Camera cam, Vector3 eye, Vector3 target, float size)
        {
            cam.transform.position = root.transform.position + eye;
            cam.transform.rotation = Quaternion.LookRotation(target - eye); cam.orthographicSize = size;
        }
        public void Render(Map02NightWatchVisual source)
        {
            root.SetActive(true);
            var pose=source.gate?source.gateLeaf.localRotation:source.hood.localRotation;
            var boltPosition=source.gate?source.bolt.localPosition:Vector3.zero;
            if(previousSource==source&&Quaternion.Angle(previousPose,pose)<.01f&&Vector3.Distance(previousBolt,boltPosition)<.0001f)return;
            previousSource=source;previousPose=pose;previousBolt=boltPosition;
            gate.gameObject.SetActive(source.gate); lamp.gameObject.SetActive(!source.gate);
            floor.localPosition = new Vector3(0, source.gate ? -.025f : -.045f, 0);
            if (source.gate)
            {
                gate.gateLeaf.localRotation = source.gateLeaf.localRotation;
                gate.bolt.localPosition = source.bolt.localPosition;
                Aim(camera, new Vector3(1.5f, 1.5f, -3.8f), new Vector3(0, .72f, 0), 1.34f);
                Aim(latchCamera, new Vector3(-.85f, .92f, -1.5f), new Vector3(-.85f, .90f, -.09f), .29f);
                latchCamera.Render();
            }
            else
            {
                lamp.hood.localRotation = source.hood.localRotation;
                lamp.beam.enabled = lamp.glow.enabled = true;
                Aim(camera, new Vector3(.9f, 1.6f, -2.7f), new Vector3(0, .10f, 0), .57f);
            }
            camera.Render();
        }
        public void Hide() => root.SetActive(false);
        public void Dispose()
        {
            Image.Release(); LatchImage.Release(); UnityEngine.Object.Destroy(Image); UnityEngine.Object.Destroy(LatchImage);
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(floorMaterial);
        }
    }
}
