using UnityEngine;

namespace ShadowVale.Map01
{
    // Instanced-only property blocks let repeated prefabs use GPU instancing
    // instead of submitting each compatible tree through the SRP Batcher.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ForestInstanceTint : MonoBehaviour
    {
        [Range(.8f, 1.2f)] public float tone = 1;
        static readonly int Tone = Shader.PropertyToID("_InstanceTone");
        void OnEnable() => Apply();
        void OnValidate() => Apply();
        void Apply()
        {
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(Tone, tone);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                renderer.SetPropertyBlock(properties);
        }
    }
}
