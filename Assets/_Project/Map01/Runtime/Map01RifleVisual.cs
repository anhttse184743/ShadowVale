using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    /// <summary>Changes only the rifle prop and stats; existing actor poses and hand calibration stay intact.</summary>
    public sealed class Map01RifleVisual : MonoBehaviour
    {
        public string ItemId { get; private set; } = "rifle_standard";
        Renderer[] original;
        GameObject model;
        float akDamage, akCooldown;
        Transform akMuzzle, m16Muzzle;
        public static void Apply(Weapon weapon, string id)
        {
            if (weapon == null) return;
            var visual = weapon.GetComponent<Map01RifleVisual>() ?? weapon.gameObject.AddComponent<Map01RifleVisual>();
            visual.Set(weapon, id);
        }
        void Set(Weapon weapon, string id)
        {
            if (original == null) { original = weapon.GetComponentsInChildren<Renderer>(true); akDamage = weapon.Damage; akCooldown = weapon.Cooldown; akMuzzle = weapon.Muzzle; }
            bool m16 = id == Map01M16.Id;
            if (m16 && model == null)
            {
                var prefab = Resources.Load<GameObject>("Weapons/M16Visual");
                if (prefab == null) { Debug.LogError("Missing M16Visual prefab"); return; }
                model = Instantiate(prefab, transform, false);
                // Keep the AK rifle mount, with the M16 prop slightly below its origin.
                model.transform.SetLocalPositionAndRotation(new Vector3(0f, -.025f, 0f), Quaternion.identity);
                m16Muzzle = model.transform.Find("Muzzle");
            }
            foreach (var r in original) if (r != null) r.enabled = !m16;
            if (model != null) model.SetActive(m16);
            if (ItemId != id) weapon.ConfigureRifle(m16 ? akDamage * .9f : akDamage, m16 ? Map01M16.ShotSeconds : akCooldown, m16 ? m16Muzzle : akMuzzle, !m16);
            ItemId = id;
        }
    }
}
