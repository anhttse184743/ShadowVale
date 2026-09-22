using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowVale.UI.HUD
{
    /// <summary>
    /// Bottom-of-screen weapon hotbar. Purely a readout: <see cref="PlayerCombat"/> owns the
    /// input and the equipped state, and this follows its <c>WeaponChanged</c> event.
    /// Slot order comes from <see cref="PlayerCombat.SlotOrder"/>, so the numbers on screen
    /// always match the number keys.
    /// </summary>
    public sealed class WeaponHotbar : MonoBehaviour
    {
        /// <summary>The pieces of one slot that change when selection moves.</summary>
        [System.Serializable]
        public struct Slot
        {
            public Image background;
            public Image border;
            public Text label;
        }

        [SerializeField] private PlayerCombat combat;
        [SerializeField] private Slot[] slots = System.Array.Empty<Slot>();

        [Header("Colours")]
        [SerializeField] private Color idleBackground = new(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color activeBackground = new(0.10f, 0.10f, 0.12f, 0.85f);
        [SerializeField] private Color idleBorder = new(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color activeBorder = new(1f, 0.78f, 0.25f, 1f);
        [SerializeField] private Color idleLabel = new(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color activeLabel = Color.white;

        private void Awake()
        {
            if (combat == null)
            {
                combat = FindFirstObjectByType<PlayerCombat>();
            }
        }

        private void OnEnable()
        {
            if (combat != null)
            {
                combat.WeaponChanged += SetEquipped;
                SetEquipped(combat.EquippedKind);
            }
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.WeaponChanged -= SetEquipped;
            }
        }

        /// <summary>
        /// Paints the slots for <paramref name="equipped"/>. Public so the build step can set the
        /// starting weapon's highlight, which keeps the HUD correct in edit mode and avoids an
        /// unhighlighted first frame at runtime.
        /// </summary>
        public void SetEquipped(WeaponKind equipped)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                // A slot with no matching entry in SlotOrder can never be selected.
                bool active = i < PlayerCombat.SlotOrder.Length
                              && PlayerCombat.SlotOrder[i] == equipped;

                Slot slot = slots[i];
                if (slot.background != null) slot.background.color = active ? activeBackground : idleBackground;
                if (slot.border != null) slot.border.color = active ? activeBorder : idleBorder;
                if (slot.label != null) slot.label.color = active ? activeLabel : idleLabel;
            }
        }
    }
}
