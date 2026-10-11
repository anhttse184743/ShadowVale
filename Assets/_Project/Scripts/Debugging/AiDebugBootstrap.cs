using UnityEngine;

namespace ShadowVale.Debugging
{
    /// <summary>
    /// Adds the AI debug console and overlay to every play session in the editor and in
    /// development builds (this assembly is compiled only there). No scene edits needed.
    /// </summary>
    internal static class AiDebugBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Object.FindFirstObjectByType<AiDebugConsole>() != null) return;
            var go = new GameObject("[AI Debug]") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AiDebugConsole>();
            go.AddComponent<AiDebugOverlay>();
        }
    }
}
