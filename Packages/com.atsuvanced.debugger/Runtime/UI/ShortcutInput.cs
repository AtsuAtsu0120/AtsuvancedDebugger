using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && ATSU_DEBUGGER_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Atsuvanced.Debugger.UI
{
    /// <summary>Keyboard polling that works with the Input System, the legacy Input Manager, or both.</summary>
    internal static class ShortcutInput
    {
        public static bool WasPressed(KeyboardShortcut shortcut)
        {
            if (shortcut.Key == KeyCode.None)
            {
                return false;
            }
#if ENABLE_INPUT_SYSTEM && ATSU_DEBUGGER_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            var key = GetKey(kb, shortcut.Key);
            if (key == null || !key.wasPressedThisFrame)
            {
                return false;
            }

            return shortcut.Control == kb.ctrlKey.isPressed
                   && shortcut.Shift == kb.shiftKey.isPressed
                   && shortcut.Alt == kb.altKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetKeyDown(shortcut.Key)) return false;
            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            return shortcut.Control == ctrl && shortcut.Shift == shift && shortcut.Alt == alt;
#else
            return false;
#endif
        }

        public static bool WasPressed(KeyCode key)
        {
            return WasPressed(new KeyboardShortcut { Key = key });
        }

        /// <summary>Gamepad left (-1) / right (+1) shoulder pressed this frame.</summary>
        public static bool WasShoulderPressed(int direction)
        {
#if ENABLE_INPUT_SYSTEM && ATSU_DEBUGGER_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad == null)
            {
                return false;
            }

            return direction < 0 ? pad.leftShoulder.wasPressedThisFrame : pad.rightShoulder.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            // Shoulder buttons are joystick buttons 4 / 5 on most controllers (XInput layout).
            return Input.GetKeyDown(direction < 0 ? KeyCode.JoystickButton4 : KeyCode.JoystickButton5);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM && ATSU_DEBUGGER_INPUT_SYSTEM
        private static readonly Dictionary<KeyCode, Key> KeyMap = new();

        private static KeyControl GetKey(Keyboard kb, KeyCode code)
        {
            if (!KeyMap.TryGetValue(code, out var key))
            {
                key = ToKey(code);
                KeyMap[code] = key;
            }

            return key == Key.None ? null : kb[key];
        }

        private static Key ToKey(KeyCode code)
        {
            if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
            {
                return Key.Digit0 + (code - KeyCode.Alpha0);
            }

            if (code >= KeyCode.Keypad0 && code <= KeyCode.Keypad9)
            {
                return Key.Numpad0 + (code - KeyCode.Keypad0);
            }

            switch (code)
            {
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
            }

            return Enum.TryParse(code.ToString(), true, out Key key) ? key : Key.None;
        }
#endif
    }
}