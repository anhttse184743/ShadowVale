using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Keeps Nam's hands on the guard during the knife takedown, whatever the two humanoid retargets did to
    /// their bodies: his left hand goes back over the guard's mouth and his knife wrist to the side of his
    /// neck, exactly where the Blender scene has them frame by frame (review_AmSat8, frames 60–100).
    /// Lives on Nam's Animator; AC_Player's base layer has its IK pass on for it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Map01TakedownIK : MonoBehaviour
    {
        // Nam's left wrist from the guard's Head bone and his right wrist from the guard's Neck bone, in the
        // guard's own frame (right, up, forward) — one entry per clip frame from FirstFrame.
        private const int FirstFrame = 60;
        private static readonly Vector3[] LeftFromHead = {
            new Vector3(-0.096f,0.187f,0.033f),
            new Vector3(-0.096f,0.188f,0.032f),
            new Vector3(-0.096f,0.189f,0.032f),
            new Vector3(-0.097f,0.191f,0.032f),
            new Vector3(-0.099f,0.192f,0.033f),
            new Vector3(-0.099f,0.194f,0.034f),
            new Vector3(-0.099f,0.196f,0.035f),
            new Vector3(-0.097f,0.195f,0.036f),
            new Vector3(-0.095f,0.177f,0.053f),
            new Vector3(-0.106f,0.158f,0.061f),
            new Vector3(-0.122f,0.130f,0.066f),
            new Vector3(-0.137f,0.104f,0.065f),
            new Vector3(-0.145f,0.089f,0.064f),
            new Vector3(-0.145f,0.084f,0.067f),
            new Vector3(-0.145f,0.083f,0.065f),
            new Vector3(-0.145f,0.083f,0.065f),
            new Vector3(-0.145f,0.083f,0.065f),
            new Vector3(-0.145f,0.083f,0.065f),
            new Vector3(-0.145f,0.083f,0.064f),
            new Vector3(-0.147f,0.083f,0.061f),
            new Vector3(-0.143f,0.081f,0.070f),
            new Vector3(-0.140f,0.080f,0.019f),
            new Vector3(-0.144f,0.079f,-0.043f),
            new Vector3(-0.158f,0.082f,-0.114f),
            new Vector3(-0.184f,0.093f,-0.197f),
            new Vector3(-0.226f,0.114f,-0.285f),
            new Vector3(-0.285f,0.147f,-0.376f),
            new Vector3(-0.359f,0.193f,-0.460f),
            new Vector3(-0.445f,0.254f,-0.536f),
            new Vector3(-0.483f,0.271f,-0.588f),
            new Vector3(-0.520f,0.304f,-0.673f),
            new Vector3(-0.586f,0.368f,-0.779f),
            new Vector3(-0.626f,0.446f,-0.896f),
            new Vector3(-0.687f,0.545f,-0.985f),
            new Vector3(-0.705f,0.630f,-1.084f),
            new Vector3(-0.734f,0.677f,-1.148f),
            new Vector3(-0.757f,0.697f,-1.205f),
            new Vector3(-0.772f,0.740f,-1.265f),
            new Vector3(-0.799f,0.794f,-1.300f),
            new Vector3(-0.844f,0.838f,-1.308f),
            new Vector3(-0.869f,0.858f,-1.326f)
        };
        private static readonly Vector3[] RightFromNeck = {
            new Vector3(0.255f,-0.140f,-0.112f),
            new Vector3(0.256f,-0.141f,-0.109f),
            new Vector3(0.257f,-0.142f,-0.108f),
            new Vector3(0.258f,-0.143f,-0.107f),
            new Vector3(0.258f,-0.143f,-0.108f),
            new Vector3(0.259f,-0.142f,-0.108f),
            new Vector3(0.260f,-0.142f,-0.110f),
            new Vector3(0.260f,-0.142f,-0.110f),
            new Vector3(0.281f,-0.090f,-0.064f),
            new Vector3(0.297f,-0.051f,-0.040f),
            new Vector3(0.314f,-0.004f,-0.034f),
            new Vector3(0.319f,0.025f,-0.045f),
            new Vector3(0.309f,0.032f,-0.057f),
            new Vector3(0.289f,0.029f,-0.061f),
            new Vector3(0.265f,0.021f,-0.063f),
            new Vector3(0.246f,0.010f,-0.057f),
            new Vector3(0.238f,0.002f,-0.040f),
            new Vector3(0.236f,-0.001f,-0.012f),
            new Vector3(0.236f,-0.003f,0.020f),
            new Vector3(0.234f,-0.008f,0.046f),
            new Vector3(0.236f,-0.018f,0.071f),
            new Vector3(0.240f,-0.023f,0.023f),
            new Vector3(0.240f,-0.019f,-0.043f),
            new Vector3(0.232f,-0.006f,-0.124f),
            new Vector3(0.214f,0.018f,-0.216f),
            new Vector3(0.183f,0.056f,-0.315f),
            new Vector3(0.139f,0.108f,-0.415f),
            new Vector3(0.084f,0.174f,-0.506f),
            new Vector3(0.022f,0.254f,-0.586f),
            new Vector3(0.009f,0.290f,-0.639f),
            new Vector3(-0.004f,0.339f,-0.723f),
            new Vector3(-0.042f,0.413f,-0.825f),
            new Vector3(-0.059f,0.497f,-0.938f),
            new Vector3(-0.097f,0.599f,-1.030f),
            new Vector3(-0.102f,0.688f,-1.129f),
            new Vector3(-0.122f,0.737f,-1.194f),
            new Vector3(-0.141f,0.757f,-1.247f),
            new Vector3(-0.153f,0.794f,-1.302f),
            new Vector3(-0.179f,0.840f,-1.335f),
            new Vector3(-0.223f,0.881f,-1.348f),
            new Vector3(-0.249f,0.905f,-1.371f)
        };
        private static readonly int TakedownState = Animator.StringToHash("Act_Takedown");

        private Animator nam;
        private Animator guard;
        private Transform guardRoot;

        public void Begin(Animator guardActor, Transform guardTransform)
        {
            guard = guardActor; guardRoot = guardTransform; enabled = guard != null;
        }

        public void End() { guard = null; guardRoot = null; enabled = false; }

        private void Awake() => nam = GetComponent<Animator>();

        /// <summary>0 before a, eases to 1 by b, holds to c, eases back to 0 by d.</summary>
        private static float Window(float f, float a, float b, float c, float d) =>
            f <= a || f >= d ? 0f : f < b ? Mathf.SmoothStep(0, 1, (f - a) / (b - a)) : f <= c ? 1f : Mathf.SmoothStep(1, 0, (f - c) / (d - c));

        private static Vector3 Sample(Vector3[] table, float frame)
        {
            float x = Mathf.Clamp(frame - FirstFrame, 0, table.Length - 1);
            int i = (int)x, j = Mathf.Min(i + 1, table.Length - 1);
            return Vector3.Lerp(table[i], table[j], x - i);
        }

        private void OnAnimatorIK(int layer)
        {
            if (layer != 0 || nam == null) return;
            float lw = 0, rw = 0;
            Vector3 left = Vector3.zero, right = Vector3.zero;
            var info = nam.GetCurrentAnimatorStateInfo(0);
            if (guard != null && guardRoot != null && info.shortNameHash == TakedownState) {
                float frame = 1 + Mathf.Clamp01(info.normalizedTime) * 140f;
                lw = Window(frame, 64, 70, 82, 92);      // the hand closes over his mouth, holds, lets go
                rw = Window(frame, 66, 72, 80, 88);      // the thrust and the rip
                Transform head = guard.GetBoneTransform(HumanBodyBones.Head), neck = guard.GetBoneTransform(HumanBodyBones.Neck);
                Vector3 r = guardRoot.right, f = guardRoot.forward;
                Vector3 l = Sample(LeftFromHead, frame), k = Sample(RightFromNeck, frame);
                if (head != null) left = head.position + r * l.x + Vector3.up * l.y + f * l.z; else lw = 0;
                if (neck != null) right = neck.position + r * k.x + Vector3.up * k.y + f * k.z; else rw = 0;
            }
            nam.SetIKPositionWeight(AvatarIKGoal.LeftHand, lw);
            nam.SetIKPositionWeight(AvatarIKGoal.RightHand, rw);
            if (lw > 0) nam.SetIKPosition(AvatarIKGoal.LeftHand, left);
            if (rw > 0) nam.SetIKPosition(AvatarIKGoal.RightHand, right);
        }
    }
}
