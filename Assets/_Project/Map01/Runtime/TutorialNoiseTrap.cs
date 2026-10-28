using ShadowVale.AI.Perception;
using UnityEngine;

namespace ShadowVale.Map01
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TutorialNoiseTrap : MonoBehaviour
    {
        public TutorialDirector director;
        public AudioClip branchSound;
        private bool fired;
        private AudioClip generated;
        private void Awake()
        {
            if (branchSound != null) return;
            // Original procedural crack for the prototype: no third-party recording/licence.
            const int rate = 22050; var samples = new float[rate / 5]; var random = new System.Random(41);
            for (int i = 0; i < samples.Length; i++) {
                float t = i / (float)rate;
                float envelope = Mathf.Exp(-t * 45) + .45f * Mathf.Exp(-Mathf.Abs(t - .045f) * 220);
                samples[i] = (float)(random.NextDouble() * 2 - 1) * envelope * .65f;
            }
            generated = AudioClip.Create("Dry branch crack (procedural)", samples.Length, 1, rate, false);
            generated.SetData(samples, 0); branchSound = generated;
        }
        private void OnEnable() => fired = false;
        private void OnTriggerEnter(Collider other)
        {
            if (fired || director == null || director.Step != TutorialStep.Return) return;
            var mission = director.GetComponent<Map01Mission>();
            if (other.transform != mission.player && !other.transform.IsChildOf(mission.player)) return;
            fired = true;
            director.BeginCapture();
        }
        public void Crack(Vector3 at) { if (branchSound != null) AudioSource.PlayClipAtPoint(branchSound, at); }
        private void OnDestroy() { if (generated != null) Destroy(generated); }
    }
}
