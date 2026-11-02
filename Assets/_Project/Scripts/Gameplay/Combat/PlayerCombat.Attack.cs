using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// One attack: gunfire (queued shots, spread, the ray and its hit), the melee swing, and the damage dealt.
    /// </summary>
    public sealed partial class PlayerCombat
    {
        private void Attack()
        {
            // Nothing in hand (a scene without weapon prefabs): there is no punch any more.
            if (_equipped == null) return;
            if (_equippedKind == WeaponKind.Knife && TrySpecialMelee != null && TrySpecialMelee()) return;
            // Everything that can refuse the shot is settled before a single side effect runs.
            // Pulling the trigger on an empty magazine used to burn the cooldown and play the
            // firing animation anyway, so the weapon mimed a shot it never took.
            bool firingGun = _equipped != null && _equipped.IsGun;
            if (firingGun)
            {
                if (GunUsable != null && !GunUsable()) return;
                if (_magazine != null)
                {
                    if (!_magazine.TryConsume())
                    {
                        // Out, or mid-reload. Reaching for a fresh magazine is what the player
                        // meant by pulling the trigger on an empty gun.
                        TryReload();
                        return;
                    }
                }
                else if (TryConsumeRound != null && !TryConsumeRound())
                {
                    return;
                }
            }

            float cooldown = _equipped != null ? _equipped.Cooldown : punchCooldown;

            // `Time.time + cooldown` rounds every gap up to a whole frame, quietly turning 600
            // rounds per minute into 500 at 50 fps. Carrying the remainder forward fixes that
            // during sustained fire, but only while the weapon is genuinely mid-burst: once the
            // schedule is more than a cooldown stale the burst is over, and resuming from it
            // would leave no delay at all before the next shot.
            _nextAttackTime = Time.time - _nextAttackTime > cooldown
                ? Time.time + cooldown
                : _nextAttackTime + cooldown;

            // The muzzle is where a gunshot is heard from; an empty hand has none, so fall back
            // to the character. Raised before the trace so a listener cannot miss a kill's shot.
            Transform sound = _equipped != null && _equipped.Muzzle != null ? _equipped.Muzzle : transform;
            // Mosin's attack event starts its bolt cycle and closes the PU scope.
            // Freeze the ray the player actually aimed before that camera transition;
            // effects still resolve after this frame's animated muzzle is positioned.
            Ray? scopedRay = null;
            if(_equippedKind==WeaponKind.Sniper && cameraRig!=null && cameraRig.IsWeaponScope) {
                var scopeCamera=Camera.main;
                if(scopeCamera!=null)scopedRay=new Ray(scopeCamera.transform.position,scopeCamera.transform.forward);
            }
            Attacked?.Invoke(_equippedKind, sound.position);

            _upperBodyHoldUntil = Time.time + cooldown + attackLayerTail;
            if (animator != null && _equippedKind != WeaponKind.Pistol && _equippedKind != WeaponKind.Sniper)
            {
                animator.SetTrigger(AnimatorParams.Attack);
            }

            if (_equipped != null && _equipped.IsGun)
            {
                _faceCameraHoldUntil = Time.time + cooldown + faceCameraTail;
                _fireHoldUntil = _upperBodyHoldUntil;
                _pendingGunfire.Add(new PendingGunShot(_equipped,CurrentStance(),_aiming,scopedRay));
            }
            else
            {
                SwingMelee();
            }
        }

        private readonly struct PendingGunShot
        {
            public readonly Weapon Gun;public readonly ShooterStance Stance;public readonly bool Aiming;public readonly Ray? ScopedRay;
            public PendingGunShot(Weapon gun,ShooterStance stance,bool aiming,Ray? scopedRay){Gun=gun;Stance=stance;Aiming=aiming;ScopedRay=scopedRay;}
        }
        private readonly System.Collections.Generic.List<PendingGunShot> _pendingGunfire=new();

        internal void ResolvePendingGunfire()
        {
            if(!isActiveAndEnabled){_pendingGunfire.Clear();return;}
            while(isActiveAndEnabled && _pendingGunfire.Count>0) {
                var shot=_pendingGunfire[0];_pendingGunfire.RemoveAt(0);
                if(shot.Gun!=null)FireShotAlongRay(shot.Gun,shot.Stance,shot.Aiming,shot.ScopedRay);
            }
            _pendingGunfire.Clear();
        }

        private void OnDisable() => _pendingGunfire.Clear();

        /// <summary>Resolve after animation and barrel aim, using this frame's camera and muzzle.</summary>
        private void FireGun(Weapon gun)
            => FireShot(gun,CurrentStance(),_aiming);

        private void FireShot(Weapon gun,ShooterStance stance,bool aiming)
            => FireShotAlongRay(gun,stance,aiming,null);

        private void FireShotAlongRay(Weapon gun,ShooterStance stance,bool aiming,Ray? scopedRay)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 origin = scopedRay?.origin ?? cam.transform.position;
            Vector3 direction = scopedRay?.direction ?? cam.transform.forward;

            // How steady the shooter is decides the cone, and how long they have held the
            // trigger widens it. The weapon's own HipSpread is no longer consulted: one flat
            // number could not tell a crouched aimed tap from a shot fired mid-jump.
            Vector2 kick = _spread.Fire(Time.time, gun.UsesM16Handling);
            direction = WeaponSpreadState.Scatter(direction, _spread.ConeHalfAngle(stance, aiming, gun.Kind, gun.UsesM16Handling));
            if (gun.Kind == WeaponKind.Sniper) kick = new Vector2(1.8f,.12f);
            var skills = GetComponent<ShadowVale.Gameplay.Skills.PlayerSkills>();
            if (skills != null) kick *= skills.RecoilMultiplier;
            if (cameraRig != null) cameraRig.AddRecoil(kick.x, kick.y);

            // The trace starts at the camera so the shot lands on the crosshair, but the streak
            // has to come out of the barrel or it looks like the player is firing from their eyes.
            Vector3 endPoint;
            if (TryShotHit(origin, direction, gun.Range, out RaycastHit hit))
            {
                var health = hit.collider.GetComponentInParent<Health>();
                bool alive = health != null && !health.IsDead;
                if (health != null) health.TakeBulletDamageFromWeapon(gun.DamageAgainst(health), hit.point, gameObject, direction, gun.Kind, gun.IsLethalAgainst(health));
                else ApplyDamage(hit.collider, gun.Damage, hit.point);
                if (alive && health.IsDead) GunKilled?.Invoke(health.gameObject);
                endPoint = hit.point;
            }
            else
            {
                endPoint = origin + direction * gun.Range;
            }

            PlayTracer(gun.Muzzle.position, endPoint)?.AnchorEmission(gun.Muzzle);
            gun.PlayMuzzleFlash();
        }

        /// <summary>
        /// Colliders a round goes straight through, set by a map (Map 1: its thatch and zinc roofs, which stop no
        /// rifle bullet). Null: the first collider on the line stops it, as before.
        /// </summary>
        public System.Func<Collider, bool> ShotPassesThrough { get; set; }

        /// <summary>The first thing on the line that stops the round.</summary>
        private bool TryShotHit(Vector3 origin, Vector3 direction, float range, out RaycastHit hit)
        {
            if (ShotPassesThrough == null)
                return Physics.Raycast(origin, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore);
            hit = default;
            bool found = false;
            foreach (var h in Physics.RaycastAll(origin, direction, range, hitMask, QueryTriggerInteraction.Ignore))
                if ((!found || h.distance < hit.distance) && !ShotPassesThrough(h.collider)) { hit = h; found = true; }
            return found;
        }

        /// <summary>Sphere sweep forward from chest height — forgiving enough for a greybox test.</summary>
        private void SwingMelee()
        {
            float range = _equipped != null ? _equipped.Range : punchRange;
            float radius = _equipped != null ? _equipped.SweepRadius : punchSweepRadius;
            float damage = _equipped != null ? _equipped.Damage : punchDamage;

            Vector3 origin = transform.position + Vector3.up * 1.2f;
            if (Physics.SphereCast(origin, radius, transform.forward, out RaycastHit hit, range,
                    hitMask, QueryTriggerInteraction.Ignore))
            {
                ApplyDamage(hit.collider, damage, hit.point);
            }
        }

        /// <summary>A lethal gun hit, never melee, corpse hits, restores or scripted damage.</summary>
        public event System.Action<GameObject> GunKilled;

        private void ApplyDamage(Collider target, float amount, Vector3 point)
        {
            var damageable = target.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(amount, point, gameObject);
        }
    }
}
