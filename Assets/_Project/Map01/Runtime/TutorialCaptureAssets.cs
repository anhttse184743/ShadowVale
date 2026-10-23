using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Clip references for the tutorial film, separate from map geometry and actor prefabs.</summary>
    public sealed class TutorialCaptureAssets : ScriptableObject
    {
        [System.Serializable]
        public sealed class FootVertex
        {
            public int vertexIndex;
            public int[] boneIndices;
            public Vector3[] boneLocalPoints;
            public Vector4 weights;
        }
        [System.Serializable]
        public sealed class FootMesh
        {
            public string rendererPath;
            public int vertexCount;
            public FootVertex[] left, right;
        }
        public AnimationClip run, hit, idle, startled, enemyRun, enemyAim, enemyFire, enemyWalk, unarmedWalk;
        public UnityEngine.Rendering.VolumeProfile concussionProfile;
        public FootMesh[] footMeshes;
        public static TutorialCaptureAssets Load() => Resources.Load<TutorialCaptureAssets>("TutorialCapture");
    }
}
