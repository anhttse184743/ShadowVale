using System;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// Carries the rifle's magazine through Nam's reload clips.
    /// <para>
    /// The clips move his support hand to the magazine, down to the pouches at his waist and back. Without this the
    /// rifle keeps its magazine fixed in the gun and the hand only mimes. The magazine's pieces are gathered under
    /// one pivot inside the rifle; while the clip has the hand carrying it the pivot rides the hand, between "put
    /// away" and "fresh one drawn" it is hidden, and it eases back into the well when the clip seats it.
    /// </para>
    /// The moments are fractions of the reload state, read from the Animator, so ReloadSpeed changes nothing. Any
    /// state that is not a reload — a cancelled reload, a hit, another weapon — puts the magazine straight back in
    /// the rifle, visible. Filled in by StoryAnimationSetup (the in-hand poses are sampled from the clips).
    /// </summary>
    [DefaultExecutionOrder(9500)]
    public sealed class ReloadMagazineCarrier : MonoBehaviour
    {
        [Serializable]
        public struct Take
        {
            public string state;
            [Range(0, 1)] public float leaves, putAway, fresh, seated;
            [Tooltip("The magazine in the hand, in the hand bone's own frame: sampled from the clip at the frame the hand has closed on it.")]
            public Vector3 inHandPosition;
            public Quaternion inHandRotation;
            [Tooltip("Share of the clip, just before it is seated, over which the magazine eases from the hand into the well (about two frames).")]
            public float seatEase;
        }

        public const string PivotName = "MagazinePivot";

        [SerializeField] private Animator animator;
        [SerializeField] private string layerName = PlayerCombat.UpperBodyLayer;
        [SerializeField] private Transform hand;
        [Tooltip("Where PlayerCombat puts the weapons: the rifle in use is looked up under it.")]
        [SerializeField] private Transform weaponRoot;
        [SerializeField] private Take[] takes = Array.Empty<Take>();

        private int _layer = -1;
        private Weapon _rifle;
        private Transform _magazine, _home;
        private Vector3 _homePosition, _homeScale;
        private Quaternion _homeRotation;
        private Renderer[] _renderers = Array.Empty<Renderer>();

        /// <summary>The magazine's pivot in the rifle in use; null until a rifle has been seen.</summary>
        public Transform Magazine => _magazine;

        /// <summary>How far the magazine is from its place in the rifle, in metres.</summary>
        public float OutOfWell => _magazine == null ? 0f : Vector3.Distance(_magazine.position, _home.TransformPoint(_homePosition));

        public bool Shown => _renderers.Length == 0 || _renderers[0].enabled;

        public Take[] Takes => takes;

        public void Configure(Animator actor, Transform handBone, Transform weapons, Take[] value)
        {
            animator = actor; hand = handBone; weaponRoot = weapons; takes = value ?? Array.Empty<Take>();
            _layer = -1;
        }

        /// <summary>
        /// Gathers a rifle's magazine pieces (the model's "bm_magazine…" nodes) under one pivot at their centre;
        /// returns the pivot it already has when called again.
        /// </summary>
        public static Transform GatherMagazine(Transform rifle)
        {
            Transform[] all = rifle.GetComponentsInChildren<Transform>(true);
            Transform existing = all.FirstOrDefault(t => t.name == PivotName);
            if (existing != null) return existing;
            Transform[] pieces = all.Where(t => t.name.StartsWith("bm_magazine") && !t.parent.name.StartsWith("bm_magazine")).ToArray();
            if (pieces.Length == 0) return null;
            Renderer[] renderers = pieces.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).ToArray();
            if (renderers.Length == 0) return null;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            Transform pivot = new GameObject(PivotName).transform;
            pivot.SetParent(pieces[0].parent, false);
            pivot.SetPositionAndRotation(bounds.center, rifle.rotation);
            foreach (Transform piece in pieces) piece.SetParent(pivot, true);
            return pivot;
        }

        /// <summary>Takes charge of this rifle's magazine (seating the one it had before).</summary>
        public void Bind(Transform rifle)
        {
            if (_magazine != null) Seat();
            _magazine = rifle != null ? GatherMagazine(rifle) : null;
            if (_magazine == null) { _renderers = Array.Empty<Renderer>(); return; }
            _home = _magazine.parent;
            _homePosition = _magazine.localPosition; _homeRotation = _magazine.localRotation; _homeScale = _magazine.localScale;
            _renderers = _magazine.GetComponentsInChildren<Renderer>(true);
        }

        private void OnDisable()
        {
            if (_magazine != null) Seat();
        }

        private void LateUpdate()
        {
            if (animator == null || hand == null) return;
            Weapon rifle = ActiveRifle();
            if (rifle != _rifle)
            {
                _rifle = rifle;
                Bind(rifle != null ? rifle.transform : null);
            }
            if (_magazine == null) return;
            if (_layer < 0) _layer = animator.GetLayerIndex(layerName);
            if (_layer < 0 || animator.GetLayerWeight(_layer) <= 0f) { Seat(); return; }
            AnimatorStateInfo info = animator.IsInTransition(_layer)
                ? animator.GetNextAnimatorStateInfo(_layer) : animator.GetCurrentAnimatorStateInfo(_layer);
            foreach (Take take in takes)
                if (info.IsName(take.state)) { Evaluate(take.state, info.normalizedTime); return; }
            Seat();
        }

        private Weapon ActiveRifle()
        {
            if (_rifle != null && _rifle.gameObject.activeInHierarchy) return _rifle;
            if (weaponRoot == null) return null;
            foreach (Transform child in weaponRoot)
                if (child.gameObject.activeInHierarchy && child.TryGetComponent(out Weapon weapon) && weapon.Kind == WeaponKind.Rifle)
                    return weapon;
            return null;
        }

        /// <summary>Places and shows the magazine for a reload state at a fraction of its length; null = no reload.</summary>
        public void Evaluate(string state, float time)
        {
            if (_magazine == null) return;
            int index = state == null ? -1 : Array.FindIndex(takes, t => t.state == state);
            if (index < 0 || time < takes[index].leaves || time >= takes[index].seated) { Seat(); return; }
            Take take = takes[index];
            Vector3 world = hand.TransformPoint(take.inHandPosition);
            Quaternion rotation = hand.rotation * take.inHandRotation;
            float ease = take.seatEase > 0 ? Mathf.InverseLerp(take.seated - take.seatEase, take.seated, time) : 0f;
            if (ease > 0)
            {
                ease = Mathf.SmoothStep(0, 1, ease);
                world = Vector3.Lerp(world, _home.TransformPoint(_homePosition), ease);
                rotation = Quaternion.Slerp(rotation, _home.rotation * _homeRotation, ease);
            }
            // it stays the rifle's child: its size never changes, only where it is
            _magazine.SetPositionAndRotation(world, rotation);
            Show(time < take.putAway || time >= take.fresh);
        }

        private void Seat()
        {
            _magazine.localPosition = _homePosition; _magazine.localRotation = _homeRotation; _magazine.localScale = _homeScale;
            Show(true);
        }

        private void Show(bool visible)
        {
            foreach (Renderer r in _renderers) r.enabled = visible && (_rifle == null || _rifle.UsesOriginalMagazine);
        }
    }
}
