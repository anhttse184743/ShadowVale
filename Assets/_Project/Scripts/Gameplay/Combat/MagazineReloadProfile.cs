using UnityEngine;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>Weapon-specific magazine contacts for the actor's existing reload clips.</summary>
    public sealed class MagazineReloadProfile : MonoBehaviour
    {
        public ReloadMagazineCarrier.Take[] takes = System.Array.Empty<ReloadMagazineCarrier.Take>();
    }
}
