from pathlib import Path
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
p=root/'Assets/_Project/Scripts/Gameplay/Combat/Health.cs';s=p.read_text(encoding='utf-8-sig')
old='''        public void TakeDamage(float amount, Vector3 hitPoint, GameObject source)
            => ApplyDamage(amount, hitPoint, source, false);'''
new='''        // Deferred gunfire can resolve after a weapon switch; the queued weapon owns the hit.
        internal void TakeBulletDamageFromWeapon(float amount, Vector3 hitPoint, GameObject source, Vector3 direction, WeaponKind weapon)
        {
            if (CinematicInvulnerable || IsDead || amount <= 0f) return;
            ApplyDamage(amount, hitPoint, source, true, weapon);
            BloodImpact.Play(hitPoint, direction);
        }

'''+old
assert old in s;s=s.replace(old,new)
s=s.replace('private void ApplyDamage(float amount, Vector3 hitPoint, GameObject source, bool bullet)', 'private void ApplyDamage(float amount, Vector3 hitPoint, GameObject source, bool bullet, WeaponKind? firedWeapon = null)')
s=s.replace('var kind = combat != null ? combat.EquippedKind : WeaponKind.Unarmed;', 'var kind = firedWeapon ?? (combat != null ? combat.EquippedKind : WeaponKind.Unarmed);')
p.write_text(s,encoding='utf-8',newline='\n')
p=root/'Assets/_Project/Scripts/Gameplay/Combat/PlayerCombat.cs';s=p.read_text(encoding='utf-8-sig')
old='health.TakeBulletDamage(gun.Damage, hit.point, gameObject, direction);';assert old in s;s=s.replace(old,'health.TakeBulletDamageFromWeapon(gun.Damage, hit.point, gameObject, direction, gun.Kind);');p.write_text(s,encoding='utf-8',newline='\n')
print('Damage now receives the queued gun identity; existing 4-argument damage API stays intact.')
