using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Caps the game at <see cref="TargetFps"/> frames a second. With vSync off and no target, the
    /// menu and Map 1 were drawn as fast as the GPU allowed — hundreds of frames nobody sees, and
    /// the machine at full power for a light game.
    /// <para>
    /// Gameplay tests step single frames, and under a cap an editor tick is not always a game
    /// frame, so ForestSceneTestBase turns the cap off for them through <see cref="UncappedKey"/>.
    /// </para>
    /// </summary>
    public static class FramePacing
    {
        public const int TargetFps = 60;
        /// <summary>Editor SessionState flag: while set, Play runs uncapped. SessionState survives
        /// the domain reload of entering Play, which a static field would not.</summary>
        public const string UncappedKey = "ShadowVale.UncappedFrames";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(UncappedKey, false)) return;
#endif
            Application.targetFrameRate = TargetFps;
        }
    }
}
