using AggroBird.UnityExtend;
using System;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public abstract class InputMappingBase
    {
        public abstract void Update(int index);
    }

    // Null checked value queries
    public static class InputMappingUtility
    {
        public static T GetValue<T>(this InputMapping<T> mapping)
        {
            return mapping != null ? mapping.value : default;
        }

        public static void Use<T>(this InputActionMapping<T> mapping)
        {
            mapping?.Use();
        }
        public static T GetValue<T>(this InputActionMapping<T> mapping, bool use)
        {
            return mapping != null ? mapping.GetValue(use) : default;
        }
    }

    public abstract class InputMapping<T> : InputMappingBase
    {
        // Inheriting classes write to this value
        protected internal T value = default;

        public static implicit operator T(InputMapping<T> action)
        {
            return action != null ? action.value : default;
        }

        protected static void CheckModifiers(InputButton[] modifiers, int index, ref int value)
        {
            if (!Utility.IsNullOrEmpty(modifiers))
            {
                bool result = true;
                foreach (var modifier in modifiers)
                {
                    result &= modifier != null && modifier.GetValue(index);
                }
                value = result ? ++value : 0;
            }
            else
            {
                value = int.MaxValue;
            }
        }
        protected static bool CheckModifiers(InputButton[] modifiers, int index)
        {
            if (!Utility.IsNullOrEmpty(modifiers))
            {
                bool result = true;
                foreach (var modifier in modifiers)
                {
                    result &= modifier != null && modifier.GetValue(index);
                }
                return result;
            }
            return true;
        }
    }

    public abstract class InputActionMapping<T> : InputMapping<T>
    {
        internal void Use()
        {
            value = default;
        }
        internal T GetValue(bool use)
        {
            T result = value;
            if (use) Use();
            return result;
        }
    }

    // Input button(s)
    [Serializable]
    public class InputButtonMapping : InputActionMapping<bool>
    {
        public InputButtonMapping(InputButton[] inputs, InputButton[] modifiers = null)
        {
            this.inputs = inputs;
            this.modifiers = modifiers;
        }

        [SerializeReference, PolymorphicField] private InputButton[] inputs;
        public ReadOnlySpan<InputButton> Inputs => inputs;

        [SerializeReference, PolymorphicField] private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        private int modifiersDown = 0;
        private int mainButtonDown = 0;

        public override void Update(int index)
        {
            if (!Utility.IsNullOrEmpty(inputs))
            {
                CheckModifiers(modifiers, index, ref modifiersDown);
                bool mainButtonValue = true;
                foreach (var input in inputs)
                {
                    mainButtonValue &= input != null && input.GetValue(index);
                }
                mainButtonDown = mainButtonValue ? ++mainButtonDown : 0;
                value = modifiersDown >= mainButtonDown && mainButtonDown > 0;
            }
        }
    }

    // Input direction
    [Serializable]
    public class InputDirectionMapping : InputActionMapping<Direction>
    {
        public InputDirectionMapping(InputDirection input, InputButton[] modifiers = null)
        {
            Input = input;
            this.modifiers = modifiers;
        }

        [field: SerializeReference, PolymorphicField] public InputDirection Input { get; private set; }

        [field: SerializeReference, PolymorphicField] private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        private int modifiersDown = 0;
        private int mainButtonDown = 0;

        public override void Update(int index)
        {
            if (Input != null)
            {
                CheckModifiers(modifiers, index, ref modifiersDown);
                Direction mainButtonValue = Input.GetValue(index);
                mainButtonDown = (mainButtonValue != Direction.None) ? ++mainButtonDown : 0;
                value = modifiersDown >= mainButtonDown && mainButtonDown > 0 ? mainButtonValue : Direction.None;
            }
        }
    }

    // Linear axis
    [Serializable]
    public class LinearAxisMapping : InputMapping<float>
    {
        public LinearAxisMapping(LinearAxis input, InputButton[] modifiers = null)
        {
            Input = input;
            this.modifiers = modifiers;
        }

        [field: SerializeReference, PolymorphicField] public LinearAxis Input { get; private set; }

        [field: SerializeReference, PolymorphicField] private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        public override void Update(int index)
        {
            value = Input != null && CheckModifiers(modifiers, index) ? Input.GetValue(index) : default;
        }
    }

    // Vector axis
    [Serializable]
    public class VectorAxisMapping : InputMapping<Vector2>
    {
        public VectorAxisMapping(VectorAxis input, InputButton[] modifiers = null)
        {
            Input = input;
            this.modifiers = modifiers;
        }

        [field: SerializeReference, PolymorphicField] public VectorAxis Input { get; private set; }

        [field: SerializeReference, PolymorphicField] private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        public override void Update(int index)
        {
            value = Input != null && CheckModifiers(modifiers, index) ? Input.GetValue(index) : default;
        }
    }

    // Controller base class
    public abstract class Controller : ScriptableObject
    {
        [field: SerializeReference, PolymorphicField] public VectorAxisMapping CameraInput { get; protected set; }
        [field: SerializeReference, PolymorphicField] public InputButtonMapping Confirm { get; protected set; }
        [field: SerializeReference, PolymorphicField] public InputButtonMapping Cancel { get; protected set; }
        [field: SerializeReference, PolymorphicField] public InputDirectionMapping DirectionInput { get; protected set; }


        protected internal abstract void UpdateInput(Player player, int index, bool inputEnabled);
    }
}
