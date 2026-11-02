using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>Local exploration rig for Map 2; independent of Map 1 mission/save state.</summary>
    public sealed class Map02Exploration : MonoBehaviour
    {
        public PlayerController player;
        public Transform boatLandingSpawn;
        public ThirdPersonCamera followCamera;
        public int ResetCount { get; private set; }

        void Start() { Time.timeScale = 1; ReturnToLanding(); }
        void Update()
        {
            if (player == null) return;
            var p = player.transform.position;
            if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
                p.y < -.4f || Mathf.Abs(p.x) > 109 || Mathf.Abs(p.z) > 99)
                ReturnToLanding();
        }
        public void ReturnToLanding()
        {
            if (player == null || boatLandingSpawn == null) return;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(boatLandingSpawn.position, boatLandingSpawn.rotation);
            player.RestoreMotion(false);
            controller.enabled = true;
            followCamera?.ClearRecoil();
            ResetCount++;
        }
        void OnGUI()
        {
            GUI.Box(new Rect(16,16,550,76), "Map 2 — khám phá từ bến thuyền");
            GUI.Label(new Rect(28,40,525,22), "WASD: di chuyển   |   Shift: bật/tắt chạy   |   Space: nhảy");
            GUI.Label(new Rect(28,62,525,22), "Chuột: xoay   |   Cuộn: zoom   |   R: về bến   |   Esc: thả chuột");
        }
    }
}
