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

        [Tooltip("The hotbar row only, not the whole HUD canvas — the crosshair is a sibling of " +
                 "this under the same canvas and must stay visible when the row is hidden.")]
        [SerializeField] private CanvasGroup hotbarGroup;

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
            if (hotbarGroup == null)
            {
                // Scenes built before hotbarGroup existed have no serialized reference — find the
                // row HudBuilder always names "Hotbar" instead of falling back to hiding the whole
                // canvas (and the crosshair sitting on it) the way this used to.
                Transform row = transform.Find("Hotbar");
                if (row != null)
                {
                    // Deliberately not "GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()"
                    // on one line — that combination did not reliably assign hotbarGroup here.
                    CanvasGroup existing = row.GetComponent<CanvasGroup>();
                    if (existing == null) hotbarGroup = row.gameObject.AddComponent<CanvasGroup>();
                    else hotbarGroup = existing;
                }
            }
        }

        private void LateUpdate()
        {
            if (combat == null || !combat.DrawsOwnWeaponHud || hotbarGroup == null) return;
            // The mission HUD already draws the weapon slots — hide only this
            // row, never the canvas it shares with the crosshair.
            hotbarGroup.alpha = 0;
            hotbarGroup.blocksRaycasts = false;
            hotbarGroup.interactable = false;
        }

        private void Start()
        {
            if (combat == null) return;
            // Deliberately not "GetComponent<T>() ?? AddComponent<T>()" on one line — that
            // combination did not reliably assign the result here (see hotbarGroup in Awake).
            LowHealthEffect effect = GetComponent<LowHealthEffect>();
            if (effect == null) effect = gameObject.AddComponent<LowHealthEffect>();
            effect.Bind(combat.GetComponent<Health>());
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
