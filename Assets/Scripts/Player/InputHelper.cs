using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EngineAssembly
{
    /// <summary>
    /// Unified input utility providing safe input polling across both Unity's New Input System
    /// and the Legacy Input Manager, completely preventing InvalidOperationException when
    /// Active Input Handling is set to "Input System Package (New)".
    /// </summary>
    public static class InputHelper
    {
        /// <summary>
        /// Returns true during the frame the user starts pressing down the key identified by keyCode.
        /// Compatible with both New Input System (Keyboard.current) and Legacy Input Manager.
        /// </summary>
        public static bool IsKeyDown(KeyCode keyCode)
        {
            if (keyCode == KeyCode.None) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Key key = ConvertKeyCodeToKey(keyCode);
                if (key != Key.None)
                {
                    var control = Keyboard.current[key];
                    if (control != null && control.wasPressedThisFrame)
                    {
                        return true;
                    }
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKeyDown(keyCode);
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Returns true while the user holds down the key identified by keyCode.
        /// Compatible with both New Input System (Keyboard.current) and Legacy Input Manager.
        /// </summary>
        public static bool IsKeyHeld(KeyCode keyCode)
        {
            if (keyCode == KeyCode.None) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Key key = ConvertKeyCodeToKey(keyCode);
                if (key != Key.None)
                {
                    var control = Keyboard.current[key];
                    if (control != null && control.isPressed)
                    {
                        return true;
                    }
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKey(keyCode);
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Gets current mouse screen position safely across both New Input System and Legacy Input Manager.
        /// </summary>
        public static Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.mousePosition;
            }
            catch (System.InvalidOperationException) { }
#endif
            return Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Maps standard Unity KeyCode values to UnityEngine.InputSystem.Key enum values.
        /// </summary>
        public static Key ConvertKeyCodeToKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.None: return Key.None;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;

                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;

                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;

                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;

                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;

                default:
                    if (System.Enum.TryParse<Key>(keyCode.ToString(), true, out Key parsedKey))
                    {
                        return parsedKey;
                    }
                    return Key.None;
            }
        }
#endif
    }
}
