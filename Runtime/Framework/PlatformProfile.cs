using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using GamepadButtonCode = UnityEngine.InputSystem.LowLevel.GamepadButton;
using KeyCode = UnityEngine.InputSystem.Key;
using MouseButtonCode = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace AggroBird.GameFramework
{
    public enum InputMode
    {
        KeyboardMouse,
        Gamepad,
    }

    public abstract class PlatformProfile : ScriptableObject
    {
        [field: SerializeField]
        public GamepadButtonCode GamepadPlatformConfirm { get; private set; } = GamepadButtonCode.South;
        [field: SerializeField]
        public GamepadButtonCode GamepadPlatformCancel { get; private set; } = GamepadButtonCode.East;

        public static bool TryGetCurrentPlatformProfile<T>(out T platformProfile) where T : PlatformProfile
        {
            var appInstance = AppInstance.Instance;
            if (appInstance)
            {
                if (appInstance.PlatformProfile is T casted)
                {
                    platformProfile = casted;
                    return true;
                }
            }
            platformProfile = null;
            return false;
        }

        public event Action<InputMode> OnInputModeChanged;

        public abstract bool SupportsMouseKeyboard { get; }
        public abstract InputMode ActiveInputMode { get; }

        public abstract void Initialize();
        public abstract void Shutdown();

        public abstract void UpdateInputMode();

        public abstract Controller CreateController(Player player);
        public virtual Controller DefaultControllerPrefab => null;

        protected void InputModeChanged(InputMode inputMode)
        {
            var appInstance = AppInstance.Instance;
            int playerCount = appInstance.PlayerCount;
            for (int i = 0; i < playerCount; i++)
            {
                appInstance.GetPlayer(i).OnInputModeChanged(inputMode);
            }
            OnInputModeChanged?.Invoke(inputMode);
        }


        protected virtual string DefaultControllerGlyphAtlas
        {
            get
            {
                if (Gamepad.current is UnityEngine.InputSystem.DualShock.DualShockGamepad)
                {
                    return "PlaystationControllerGlyphs";
                }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_WSA
                if (Gamepad.current is UnityEngine.InputSystem.Switch.SwitchProControllerHID)
                {
                    return "SwitchControllerGlyphs";
                }
#endif

                return "DefaultControllerGlyphs";
            }
        }
        protected virtual string DefaultKeyboardGlyphAtlas => "DefaultKeyboardGlyphs";
        protected virtual string DirectionGlyphAtlas => "DirectionGlyphs";

        protected void FormatGlyphTag(string atlas, string glyph, StringBuilder output)
        {
            // Backwards compatibility fix
            if (atlas == "ControllerGlyphs")
            {
                atlas = DefaultControllerGlyphAtlas;
            }
            output.Append($"<sprite=\"{atlas}\" tint=1 name=\"{glyph}\">");
        }

        public virtual void FormatGlyph(KeyCode code, StringBuilder output)
        {
            FormatGlyphTag("KeyboardGlyphs", $"KeyboardKey{code}", output);
        }
        public virtual void FormatGlyph(MouseButtonCode code, StringBuilder output)
        {
            FormatGlyphTag("KeyboardGlyphs", $"MouseButton{code}", output);
        }
        public virtual void FormatGlyphMouse(StringBuilder output)
        {
            FormatGlyphTag("KeyboardGlyphs", $"Mouse", output);
        }
        public virtual void FormatGlyphMouse(StringBuilder output, Direction direction)
        {
            FormatGlyphTag("KeyboardGlyphs", $"Mouse{direction}", output);
        }
        public virtual void FormatGlyphMouse(StringBuilder output, Axis axis)
        {
            FormatGlyphTag("KeyboardGlyphs", $"Mouse{axis}", output);
        }

        public virtual void FormatGlyph(GamepadButtonCode code, StringBuilder output)
        {
            // Overlapping enums cannot be reliably stringified
            switch (code)
            {
                case GamepadButtonCode.North:
                    FormatGlyphTag(DefaultControllerGlyphAtlas, "North", output);
                    return;
                case GamepadButtonCode.East:
                    FormatGlyphTag(DefaultControllerGlyphAtlas, "East", output);
                    return;
                case GamepadButtonCode.South:
                    FormatGlyphTag(DefaultControllerGlyphAtlas, "South", output);
                    return;
                case GamepadButtonCode.West:
                    FormatGlyphTag(DefaultControllerGlyphAtlas, "West", output);
                    return;
            }
            FormatGlyphTag(DefaultControllerGlyphAtlas, code.ToString(), output);
        }
        public virtual void FormatGlyph(GamepadStick stick, StringBuilder output)
        {
            FormatGlyphTag(DefaultControllerGlyphAtlas, stick.ToString(), output);
        }
        public virtual void FormatGlyph(GamepadStick stick, Direction direction, StringBuilder output, bool glyphAsArrow)
        {
            if (glyphAsArrow)
            {
                FormatGlyphTag(DirectionGlyphAtlas, $"{direction}", output);
                return;
            }

            FormatGlyphTag(DefaultControllerGlyphAtlas, $"{stick}{direction}", output);
        }
        public virtual void FormatGlyph(Direction8 direction, StringBuilder output, bool glyphAsArrow)
        {
            if (glyphAsArrow)
            {
                FormatGlyphTag(DirectionGlyphAtlas, $"{direction}", output);
                return;
            }

            FormatGlyphTag(DefaultControllerGlyphAtlas, $"{direction}", output);
        }
        public virtual void FormatGlyph(GamepadStick stick, Axis axis, StringBuilder output)
        {
            FormatGlyphTag(DefaultControllerGlyphAtlas, $"{stick}{axis}", output);
        }

        public virtual string FormatMappingTag(InputMappingBase mapping)
        {
            return $"<mapping=\"{mapping.Name}\">";
        }
    }
}