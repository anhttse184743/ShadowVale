using UnityEngine;
namespace ShadowVale.Gameplay.Combat
{
    // Keep input in Update, but sample gunfire after camera, animation and bone corrections.
    [DefaultExecutionOrder(10000),DisallowMultipleComponent]
    public sealed class GunfireFrame : MonoBehaviour
    {
        private PlayerCombat combat;
        private void Awake()=>combat=GetComponent<PlayerCombat>();
        private void LateUpdate(){if(combat!=null)combat.ResolvePendingGunfire();}
    }
}
