using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>Short, pooled world-space blood droplets, emitted only after a confirmed bullet hit.</summary>
    public sealed class BloodImpact : MonoBehaviour
    {
        private static BloodImpact instance;
        private readonly ParticleSystem[] pool = new ParticleSystem[12];
        private Material material;
        private bool ownsMaterial;
        private int cursor;

        public static void Play(Vector3 position, Vector3 direction)
        {
            if (!Application.isPlaying) return;
            if (instance == null) instance = new GameObject("Blood impacts").AddComponent<BloodImpact>();
            instance.Emit(position, direction);
        }

        private void Emit(Vector3 position, Vector3 direction)
        {
            if (material == null)
            {
                material = Resources.Load<Material>("CombatFeedback/Blood");
                if (material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) return;
                    material = new Material(shader);
                    material.SetColor("_BaseColor", new Color(.48f, .015f, .025f, 1f));
                    ownsMaterial = true;
                }
            }
            var particles = pool[cursor];
            if (particles == null)
            {
                var child = new GameObject("Blood burst");
                child.transform.SetParent(transform);
                particles = child.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false; main.loop = false; main.duration = .6f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .42f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 2.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(.018f, .065f);
                main.gravityModifier = .65f; main.maxParticles = 24;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = particles.emission; emission.enabled = false;
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 32f; shape.radius = .045f;
                var size = particles.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                var mesh = Resources.Load<Mesh>("CombatFeedback/Droplet");
                if (mesh != null) { renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh; }
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pool[cursor] = particles;
            }
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // Splash back toward the entry side, where the camera can see the hit.
            particles.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(direction.sqrMagnitude > .001f ? -direction.normalized : Vector3.up));
            particles.Play(); particles.Emit(16);
            cursor = (cursor + 1) % pool.Length;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (ownsMaterial && material != null) Destroy(material);
        }
    }
}
