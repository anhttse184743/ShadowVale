using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// Lays the rifle on the crosshair.
    /// <para>
    /// A shot is traced from the middle of the view and its streak is drawn from the muzzle, but the aim and
    /// fire takes hold the rifle level whatever the view does: look up or down and the barrel pointed somewhere
    /// else, so the streak left it at an angle. Once the animation has posed him, the spine is bent by what
    /// separates the barrel from the line to the point under the crosshair. Arms, hands and rifle ride on the
    /// chest, so the grip the takes were authored with is untouched.
    /// </para>
    /// <para>
    /// The bend is measured from where the take holds the barrel on average, not from the barrel this frame:
    /// a kick or a flinch that a clip plays on top still shows, and a reload keeps the bend it started with.
    /// </para>
    /// Added by <see cref="PlayerCombat"/>. Runs after the camera has moved for the frame (ThirdPersonCamera)
    /// and before the bones that follow the hands (ForearmTwist).
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class BarrelAim : MonoBehaviour
    {
        /// <summary>Seconds for the bend to come in with the aim and go out with it.</summary>
        private const float BlendTime = 0.15f;

        /// <summary>How slowly the reference follows the take: longer than a kick, shorter than a change of stance.</summary>
        private const float PoseSettleTime = 0.12f;

        /// <summary>Seconds to refocus between something near under the crosshair and something far.</summary>
        private const float RefocusTime = 0.08f;

        /// <summary>The barrel never converges nearer than this past the muzzle: a wall at arm's length, or a
        /// branch between the camera and his back, must not fold the rifle sideways.</summary>
        private const float NearestFocus = 2.5f;

        /// <summary>Degrees the spine gives altogether. Past it the barrel stops short of the crosshair.</summary>
        private const float MaxBend = 80f;

        private PlayerCombat _combat;
        private Transform[] _spine = System.Array.Empty<Transform>();
        private float[] _share = System.Array.Empty<float>();
        // What the animation left in each spine bone, and what was written over it: a frame in which the
        // animator does not run (paused, culled) must not be bent a second time.
        private Quaternion[] _posed = System.Array.Empty<Quaternion>();
        private Quaternion[] _written = System.Array.Empty<Quaternion>();
        private bool _bent;

        private float _weight;
        private bool _settled;
        private Vector3 _held;      // the take's barrel in the character's own frame, eased
        private Vector3 _pose;      // the same, as it was this frame
        private int _poseFrame = -1;
        private float _focus;

        /// <summary>0 to 1: how much of the bend is in. For tests.</summary>
        public float Weight => _weight;

        internal void Bind(PlayerCombat combat, Animator animator)
        {
            _combat = combat;
            var chain = new System.Collections.Generic.List<Transform>();
            if (animator != null && animator.isHuman)
                foreach (HumanBodyBones bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t != null) chain.Add(t);
                }
            _spine = chain.ToArray();
            _posed = new Quaternion[_spine.Length];
            _written = new Quaternion[_spine.Length];
            // More of the bend high in the back, where it moves the rifle least off the shoulder.
            _share = new float[_spine.Length];
            float total = 0f;
            for (int i = 0; i < _share.Length; i++) total += i + 2;
            for (int i = 0; i < _share.Length; i++) _share[i] = (i + 2) / total;
        }

        /// <summary>
        /// How far round from the character's own forward the take points the barrel, in degrees — before any
        /// bend, which is what facing the body needs. False when no rifle was posed this frame or the last.
        /// </summary>
        internal bool TryGetPoseYaw(out float yaw)
        {
            yaw = 0f;
            if (_poseFrame < Time.frameCount - 1) return false;
            Vector3 flat = Vector3.ProjectOnPlane(_pose, Vector3.up);
            // Barrel straight up or down carries no yaw.
            if (flat.sqrMagnitude >= 0.0004f) yaw = Vector3.SignedAngle(Vector3.forward, flat, Vector3.up);
            return true;
        }

        private void LateUpdate()
        {
            Unbend();

            Weapon gun = _combat != null && _combat.isActiveAndEnabled ? _combat.Gun : null;
            if (gun == null)
            {
                _weight = 0f;
                _settled = false;
                _poseFrame = -1;
                return;
            }

            Transform muzzle = gun.Muzzle;
            _pose = Quaternion.Inverse(transform.rotation) * muzzle.forward;
            _poseFrame = Time.frameCount;

            bool wanted = _combat.RifleLevelled;
            bool rising = wanted && _weight < 1f;
            _weight = Mathf.MoveTowards(_weight, wanted ? 1f : 0f, Time.deltaTime / BlendTime);
            if (_weight <= 0f || _spine.Length == 0)
            {
                _settled = false;
                return;
            }

            // Coming up, the reference rides the take as it swings from the carry to the aim. Lowering again
            // or reloading, it stays where the aim had it, so neither is mistaken for the aim pose.
            if (!_settled || rising) _held = _pose;
            else if (wanted && !_combat.IsReloading && !_combat.RecoilPosePlaying)
                _held = Vector3.Slerp(_held, _pose, 1f - Mathf.Exp(-Time.deltaTime / PoseSettleTime)).normalized;

            Camera view = Camera.main;
            if (view == null) return;
            Vector3 eye = view.transform.position, look = view.transform.forward;
            float nearest = Vector3.Dot(muzzle.position - eye, look) + NearestFocus;
            float focus = Physics.Raycast(eye, look, out RaycastHit hit, gun.Range, _combat.HitMask, QueryTriggerInteraction.Ignore)
                ? hit.distance : gun.Range;
            // The direction follows the view at once; only the distance it converges at is eased, so the
            // barrel does not snap as the crosshair crosses the edge of something near.
            _focus = _settled ? Mathf.Lerp(_focus, focus, 1f - Mathf.Exp(-Time.deltaTime / RefocusTime)) : focus;
            _settled = true;
            Vector3 point = eye + look * Mathf.Max(_focus, nearest);

            Vector3 held = transform.rotation * _held;
            for (int i = 0; i < _spine.Length; i++) _posed[i] = _spine[i].localRotation;
            // Twice: bending the spine carries the muzzle with it, which shifts the line to the point a little.
            Quaternion bent = Quaternion.identity;
            for (int pass = 0; pass < 2; pass++)
            {
                Quaternion full = Quaternion.RotateTowards(Quaternion.identity, Turn(held, point - muzzle.position), MaxBend);
                Quaternion bend = Quaternion.Slerp(Quaternion.identity, full, _weight);
                Quaternion more = bend * Quaternion.Inverse(bent);
                for (int i = 0; i < _spine.Length; i++)
                    _spine[i].rotation = Quaternion.Slerp(Quaternion.identity, more, _share[i]) * _spine[i].rotation;
                bent = bend;
            }
            for (int i = 0; i < _spine.Length; i++) _written[i] = _spine[i].localRotation;
            _bent = true;
        }

        /// <summary>Takes back last frame's bend where the animator has not posed the bone since.</summary>
        private void Unbend()
        {
            if (!_bent) return;
            _bent = false;
            for (int i = 0; i < _spine.Length; i++)
            {
                Quaternion now = _spine[i].localRotation, was = _written[i];
                if (now.x == was.x && now.y == was.y && now.z == was.z && now.w == was.w) _spine[i].localRotation = _posed[i];
            }
        }

        /// <summary>
        /// The turn that carries <paramref name="from"/> onto <paramref name="to"/> as a swing about the
        /// vertical and then a tilt, so the rifle is not rolled over on the way.
        /// </summary>
        private static Quaternion Turn(Vector3 from, Vector3 to)
        {
            Vector3 flatFrom = Vector3.ProjectOnPlane(from, Vector3.up), flatTo = Vector3.ProjectOnPlane(to, Vector3.up);
            if (flatFrom.sqrMagnitude < 0.0004f || flatTo.sqrMagnitude < 0.0004f) return Quaternion.FromToRotation(from, to);
            Quaternion swing = Quaternion.AngleAxis(Vector3.SignedAngle(flatFrom, flatTo, Vector3.up), Vector3.up);
            return Quaternion.FromToRotation(swing * from, to) * swing;
        }
    }
}
