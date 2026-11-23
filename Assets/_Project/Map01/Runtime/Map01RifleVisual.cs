using UnityEngine;
using System.Linq;
using ShadowVale.Content;
using ShadowVale.Data.Content;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    /// <summary>Changes only the rifle prop and stats; existing actor poses and hand calibration stay intact.</summary>
    public sealed class Map01RifleVisual : MonoBehaviour
    {
        // Runtime guard templates can already carry an M16 when Instantiate copies them.
        // Copy the original AK baseline and prop references with the component as well.
        [field: SerializeField] public string ItemId { get; private set; } = "rifle_standard";
        [SerializeField] Renderer[] original;
        [SerializeField] GameObject model;
        [SerializeField] float akDamage, akCooldown, akRange;
        [SerializeField] Transform akMuzzle, m16Muzzle;
        public static void Apply(Weapon weapon, string id)
        {
            if (weapon == null) return;
            var visual = weapon.GetComponent<Map01RifleVisual>() ?? weapon.gameObject.AddComponent<Map01RifleVisual>();
            visual.Set(weapon, id, null);
        }

        public static void ApplyPlayer(PlayerCombat combat, string id)
        {
            var weapon = combat?.WeaponFor(WeaponKind.Rifle);
            if (weapon == null) return;
            WeaponDefinition profile = null;
            if (ContentService.TryGetPublished(out var bundle))
                profile = bundle.Weapons.FirstOrDefault(w => w.Id == id);
            var visual = weapon.GetComponent<Map01RifleVisual>() ?? weapon.gameObject.AddComponent<Map01RifleVisual>();
            visual.Set(weapon, id, profile);
            combat.ApplyPublishedRifleHandling(profile?.MagazineSize ?? 0, profile?.ReloadSeconds ?? 0f);
        }

        void Set(Weapon weapon, string id, WeaponDefinition profile)
        {
            if (original == null) { original = weapon.GetComponentsInChildren<Renderer>(true); akDamage = weapon.Damage; akCooldown = weapon.Cooldown; akRange = weapon.Range; akMuzzle = weapon.Muzzle; }
            bool m16 = id == Map01M16.Id;
            if (m16 && model == null)
            {
                var prefab = Resources.Load<GameObject>("Weapons/M16Visual");
                if (prefab == null) { Debug.LogError("Missing M16Visual prefab"); return; }
                model = Instantiate(prefab, transform, false);
                // Keep the AK rifle mount, with the M16 prop slightly below its origin.
                model.transform.SetLocalPositionAndRotation(Map01M16.HeldOffset, Quaternion.identity);
                m16Muzzle = model.transform.Find("Muzzle");
            }
            foreach (var r in original) if (r != null) r.enabled = !m16;
            if (model != null) model.SetActive(m16);
            weapon.SetReloadModel(m16 ? model.transform : null);
            if (ItemId != id || profile != null)
            {
                float cooldown = m16 ? Map01M16.ShotSeconds : akCooldown;
                if (profile != null && profile.FireRate > 0f) cooldown = 1f / profile.FireRate;
                weapon.ConfigureRifle(profile?.Damage ?? (m16 ? Map01M16.Damage : akDamage),
                    cooldown, m16 ? m16Muzzle : akMuzzle, !m16, profile?.Range ?? (m16 ? Map01M16.Range : akRange), m16);
            }
            ItemId = id;
        }
    }
}
