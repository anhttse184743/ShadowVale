using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Combat
{
    /// <summary>
    /// Reading the player's input: number keys to equip, aim, reload and attack.
    /// </summary>
    public sealed partial class PlayerCombat
    {
        // ---- Input ----------------------------------------------------------

        private void ReadEquipInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            for (int slot = 0; slot < SlotKeys.Length; slot++)
            {
                if (!kb[SlotKeys[slot]].wasPressedThisFrame) continue;
                if (SlotPressed != null && SlotPressed(slot)) return;
                // A key past the end of SlotOrder has nothing to draw.
                if (slot < SlotOrder.Length) Equip(SlotOrder[slot]);
                return;
            }
        }

        private void ReadAimInput()
        {
            if (_equipped == null || !_equipped.IsGun)
            {
                SetAiming(false);
                return;
            }

            // Aim follows the held button; release returns to normal movement/facing.
            SetAiming(Mouse.current != null && Mouse.current.rightButton.isPressed);
        }

        /// <summary>
        /// Reads how steady the shooter is right now. Feet off the ground beats everything —
        /// a sneaking player who jumps is not sneaking any more.
        /// </summary>
        private ShooterStance CurrentStance()
        {
            if (_controller == null) return ShooterStance.Standing;
            return WeaponSpreadState.ResolveStance(_characterController == null || _characterController.isGrounded,
                _controller.IsSneaking, _controller.IsSprinting, _controller.PlanarSpeed);
        }

        private void ReadReloadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) TryReload();
        }

        private void ReadAttackInput()
        {
            Mouse mouse = Mouse.current;
            _firingHeld = mouse != null && mouse.leftButton.isPressed && Gun != null && !IsReloading
                && (_magazine == null || _magazine.Rounds > 0);
            if (mouse == null)
            {
                _automaticBurst = false;
                return;
            }

            bool automatic = _equipped != null && _equipped.Automatic;
            bool pressed = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            bool continuing = _automaticBurst;
            _automaticBurst = automatic && pressed && !IsReloading;
            if (!pressed || Time.time < _nextAttackTime)
            {
                return;
            }

            if (!automatic || !continuing) { Attack(); return; }

            // A slow frame may contain several scheduled rounds. Preserve their deadlines rather than
            // silently reducing the fire rate to the frame rate. Never replay a long backlog after a stall.
            const int maxCatchUp = 4;
            int fired = 0;
            while (Time.time >= _nextAttackTime && fired < maxCatchUp)
            {
                float deadline = _nextAttackTime;
                float cooldown = _equipped.Cooldown;
                Attack();
                if (_nextAttackTime <= deadline) break; // Empty, reloading, broken or otherwise refused.
                _nextAttackTime = deadline + cooldown;
                fired++;
                if (_equipped == null || !_equipped.Automatic || IsReloading) break;
            }
            if (fired == maxCatchUp && _nextAttackTime <= Time.time)
                _nextAttackTime = Time.time + _equipped.Cooldown;
        }
    }
}
