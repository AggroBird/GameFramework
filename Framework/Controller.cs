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
            return mapping != null ? mapping.GetValue() : default;
        }

        public static void Use<T>(this InputMapping<T> mapping)
        {
            mapping?.Use();
        }
        public static T GetValue<T>(this InputMapping<T> mapping, bool use)
        {
            return mapping != null ? mapping.GetValue(use) : default;
        }
    }

    // Generic base class
    public abstract class InputMapping<T> : InputMappingBase
    {
        // Inheriting classes write to this value
        protected T value = default;

        protected internal virtual T GetValue() => value;

        internal void Use()
        {
            value = default;
        }
        internal T GetValue(bool use)
        {
            T result = GetValue();
            if (use) Use();
            return result;
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

    // Input button(s)
    [Serializable, PolymorphicClassType(ShowFoldout = true)]
    public class InputButtonMapping : InputMapping<bool>
    {
        public InputButtonMapping(InputButton input, InputButton[] modifiers = null)
        {
            inputs = new InputButton[1] { input };
            this.modifiers = modifiers;
        }
        public InputButtonMapping(InputButton[] inputs, InputButton[] modifiers = null)
        {
            this.inputs = inputs;
            this.modifiers = modifiers;
        }

        [SerializeReference, PolymorphicField] private InputButton[] inputs;
        public ReadOnlySpan<InputButton> Inputs => inputs;

        [SerializeReference, PolymorphicField] private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        [SerializeField]
        private bool axis = false;
        [SerializeField, ConditionalField(nameof(axis), ConditionalFieldOperator.Equal, false)]
        private ButtonSwitch @switch;

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
                bool currentValue = modifiersDown >= mainButtonDown && mainButtonDown > 0;
                if (axis)
                {
                    value = currentValue;
                }
                else
                {
                    @switch.Update(currentValue);
                    value = @switch.State == ButtonState.Pressed;
                }
            }
        }
    }

    // Input direction
    [Serializable, PolymorphicClassType(ShowFoldout = true)]
    public class InputDirectionMapping : InputMapping<Direction>
    {
        public InputDirectionMapping(InputDirection input, InputButton[] modifiers = null)
        {
            Input = input;
            this.modifiers = modifiers;
        }

        [field: SerializeReference, PolymorphicField] public InputDirection Input { get; private set; }

        [field: SerializeReference, PolymorphicField] private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        [SerializeField] private bool axis = false;
        [SerializeField, ConditionalField(nameof(axis), ConditionalFieldOperator.Equal, false)]
        private DirectionSwitch @switch = new(false);

        private int modifiersDown = 0;
        private int mainButtonDown = 0;

        public override void Update(int index)
        {
            if (Input != null)
            {
                CheckModifiers(modifiers, index, ref modifiersDown);
                Direction mainButtonValue = Input.GetValue(index);
                mainButtonDown = (mainButtonValue != Direction.None) ? ++mainButtonDown : 0;
                var currentValue = modifiersDown >= mainButtonDown && mainButtonDown > 0 ? mainButtonValue : Direction.None;
                if (axis)
                {
                    value = currentValue;
                }
                else
                {
                    @switch.Update(currentValue);
                    value = @switch.State;
                }
            }
        }
    }

    // Linear axis
    [Serializable, PolymorphicClassType(ShowFoldout = true)]
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
    [Serializable, PolymorphicClassType(ShowFoldout = true)]
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
        [field: SerializeReference, PolymorphicField, Header("Default Input")]
        public VectorAxisMapping CameraInput { get; protected set; }
        [field: SerializeReference, PolymorphicField, Space]
        public InputButtonMapping Confirm { get; protected set; }
        [field: SerializeReference, PolymorphicField, Space]
        public InputButtonMapping Cancel { get; protected set; }
        [field: SerializeReference, PolymorphicField, Space]
        public InputDirectionMapping DirectionInput { get; protected set; }


        protected internal abstract void UpdateInput(Player player, int index, bool inputEnabled);
    }
}
