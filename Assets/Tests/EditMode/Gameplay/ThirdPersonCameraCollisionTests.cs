using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    public sealed class ThirdPersonCameraCollisionTests
    {
        private GameObject player, view, wall;
        private ThirdPersonCamera rig;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private void Set(string name, object value) => typeof(ThirdPersonCamera).GetField(name, Private).SetValue(rig, value);
        [SetUp] public void Setup()
        {
            player = new GameObject("Camera test player"); player.transform.position = new Vector3(2000, 100, 2000);
            view = new GameObject("Camera test view"); var camera = view.AddComponent<Camera>();
            camera.nearClipPlane = .1f;
            rig = view.AddComponent<ThirdPersonCamera>(); rig.SetTarget(player.transform); rig.InputAllowed = () => false;
            Set("_camera", camera); Set("_pitch", 0f); Set("_currentDistance", 2f);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(wall); Object.DestroyImmediate(view); Object.DestroyImmediate(player);
        }
        private void Tick()
        {
            Physics.SyncTransforms(); typeof(ThirdPersonCamera).GetMethod("LateUpdate", Private).Invoke(rig, null);
        }
        private void Wall(Vector3 offset, Vector3 scale)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.transform.position + offset; wall.transform.localScale = scale;
        }
        [Test] public void WithoutAShakeTheViewIsExactlyTheSame()
        {
            // Map 1 never shakes: the view must not move unless a scene asks.
            Tick(); var before = view.transform.position; Tick();
            Assert.AreEqual(0f, rig.ShakeAmplitude); Assert.AreEqual(before, view.transform.position);
            rig.Shake(0f, 1f); rig.Shake(.5f, 0f); Assert.AreEqual(0f, rig.ShakeAmplitude, "A zero shake is no shake.");
        }
        [Test] public void ShakeMovesTheViewWithinItsAmplitudeAndAWeakerOneCannotReplaceIt()
        {
            Tick(); var still = view.transform.position;
            rig.Shake(.5f, 10f); rig.Shake(.1f, 10f); Assert.AreEqual(.5f, rig.ShakeAmplitude, .01f);
            Tick(); float moved = Vector3.Distance(still, view.transform.position);
            Assert.Greater(moved, 0f); Assert.LessOrEqual(moved, .5f * 2f * Mathf.Sqrt(3f) * .5f + .001f);
        }
        [Test] public void NearbyWallRetractsBelowMinimumZoomDistance()
        {
            Wall(new Vector3(0, 1.6f, -.85f), new Vector3(6, 4, .1f)); Tick();
            Assert.Greater(view.transform.position.z - player.transform.position.z, -.55f);
        }
        [Test] public void ShoulderCannotStartOnOtherSideOfWall()
        {
            Wall(new Vector3(.32f, 1.6f, 0), new Vector3(.1f, 4, 6)); Set("_aimBlend", 1f);rig.SetAiming(true);Tick();
            Assert.Less(view.transform.position.x - player.transform.position.x, .27f);
        }
        [Test] public void PlayerCollidersAndTriggersDoNotBlockArm()
        {
            var body=player.AddComponent<CapsuleCollider>();body.center=Vector3.up;body.height=2;body.radius=.5f;
            Wall(new Vector3(0, 1.6f, -.85f), new Vector3(6, 4, .1f));wall.GetComponent<Collider>().isTrigger=true;Tick();
            Assert.AreEqual(-2f,view.transform.position.z-player.transform.position.z,.001f);
        }
        [Test] public void WallOnDifferentPhysicalLayerAlsoRetractsArm()
        {
            Wall(new Vector3(0,1.6f,-.85f),new Vector3(6,4,.1f));wall.layer=2;Tick();
            Assert.Greater(view.transform.position.z-player.transform.position.z,-.55f);
        }
        [Test] public void WideNearPlaneCannotCrossWallAtScreenEdge()
        {
            var camera=view.GetComponent<Camera>();camera.nearClipPlane=.3f;camera.aspect=2.4f;
            Wall(new Vector3(0,1.6f,-1.2f),new Vector3(6,4,.1f));Tick();
            var corners=new Vector3[4];camera.CalculateFrustumCorners(new Rect(0,0,1,1),camera.nearClipPlane,Camera.MonoOrStereoscopicEye.Mono,corners);
            foreach(var corner in corners) Assert.Greater(view.transform.TransformPoint(corner).z,player.transform.position.z-1.15f);
        }
    }
}
