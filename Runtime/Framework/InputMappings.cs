using AggroBird.UnityExtend;
using System;
using System.Text;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public abstract class InputMappingBase
    {
        public abstract void Update(int index);

        public abstract void ToString(PlatformProfile platformProfile, StringBuilder output);
        public void ToString(StringBuilder output)
        {
            if (PlatformProfile.TryGetCurrentPlatformProfile(out PlatformProfile platformProfile))
            {
                ToString(platformProfile, output);
            }
            else
            {
                output.Append(base.ToString());
            }
        }
        public override string ToString()
        {
            StringBuilder output = new();
            ToString(output);
            return output.ToString();
        }

        public string Name { get; internal set; }
        public uint Hash { get; internal set; }
    }

    // Null checked value queries
    public static class InputMappingUtility
    {
        public static T GetValue<T>(this InputMapping<T> mapping)
        {
            return mapping != null ? mapping.GetRawValue() : default;
        }
        public static T GetValue<T>(this InputMapping<T> mapping, bool use)
        {
            return mapping != null ? mapping.GetRawValue(use) : default;
        }

        public static void Use<T>(this InputMapping<T> mapping)
        {
            mapping?.Use();
        }
    }

    // Generic base class
    public abstract class InputMapping<T> : InputMappingBase
    {
        // Inheriting classes write to this value
        protected T value = default;

        protected internal virtual T GetRawValue() => value;

        internal void Use()
        {
            value = default;
        }
        internal T GetRawValue(bool use)
        {
            T result = GetRawValue();
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
                    if (modifier == null)
                    {
                        continue;
                    }

                    result &= modifier.GetValue(index);
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
                    result &= modifier.GetValue(index);
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
        public InputButtonMapping(InputButton input, InputButton[] modifiers = null, bool axis = false)
        {
            inputs = new InputButton[1] { input };

            if (modifiers != null)
            {
                this.modifiers = modifiers;
            }
            else
            {
                this.modifiers = new InputButton[0];
            }

            this.axis = axis;
        }
        public InputButtonMapping(InputButton[] inputs, InputButton[] modifiers = null)
        {
            this.inputs = inputs;
            this.modifiers = modifiers;
        }

        [SerializeReference, PolymorphicField]
        private InputButton[] inputs;
        public ReadOnlySpan<InputButton> Inputs => inputs;

        [SerializeReference, PolymorphicField]
        private InputButton[] modifiers;
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
                    mainButtonValue &= input.GetValue(index);
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

        public override void ToString(PlatformProfile platformProfile, StringBuilder output)
        {
            int c = 0;
            foreach (var modifier in modifiers)
            {
                if (c++ > 0) output.Append('+');
                modifier.ToString(platformProfile, output);
            }
            foreach (var input in inputs)
            {
                if (c++ > 0) output.Append('+');
                input.ToString(platformProfile, output);
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

        [field: SerializeReference, PolymorphicField]
        public InputDirection Input { get; private set; }

        [field: SerializeReference, PolymorphicField]
        private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        [SerializeField]
        private bool axis = false;
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

        public override void ToString(PlatformProfile platformProfile, StringBuilder output)
        {
            int c = 0;
            foreach (var modifier in modifiers)
            {
                if (c++ > 0) output.Append('+');
                modifier.ToString(platformProfile, output);
            }
            if (c++ > 0) output.Append('+');
            Input.ToString(platformProfile, output);
        }
    }

    // Input direction (1D)
    [Serializable, PolymorphicClassType(ShowFoldout = true)]
    public class IntegralDirectionMapping : InputMapping<int>
    {
        public IntegralDirectionMapping(IntegralDirection input, InputButton[] modifiers = null)
        {
            Input = input;
            this.modifiers = modifiers;
        }

        [field: SerializeReference, PolymorphicField]
        public IntegralDirection Input { get; private set; }

        [field: SerializeReference, PolymorphicField]
        private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        [SerializeField]
        private bool axis = false;
        [SerializeField, ConditionalField(nameof(axis), ConditionalFieldOperator.Equal, false)]
        private IntegralSwitch @switch = new(false);

        private int modifiersDown = 0;
        private int mainButtonDown = 0;

        public override void Update(int index)
        {
            if (Input != null)
            {
                CheckModifiers(modifiers, index, ref modifiersDown);
                int mainButtonValue = Input.GetValue(index);
                mainButtonDown = (mainButtonValue != 0) ? ++mainButtonDown : 0;
                var currentValue = modifiersDown >= mainButtonDown && mainButtonDown > 0 ? mainButtonValue : 0;
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

        public override void ToString(PlatformProfile platformProfile, StringBuilder output)
        {
            int c = 0;
            foreach (var modifier in modifiers)
            {
                if (c++ > 0) output.Append('+');
                modifier.ToString(platformProfile, output);
            }
            if (c++ > 0) output.Append('+');
            Input.ToString(platformProfile, output);
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

        [field: SerializeReference, PolymorphicField]
        public LinearAxis Input { get; private set; }

        [field: SerializeReference, PolymorphicField]
        private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        public override void Update(int index)
        {
            value = CheckModifiers(modifiers, index) ? Input.GetValue(index) : default;
        }

        public override void ToString(PlatformProfile platformProfile, StringBuilder output)
        {
            int c = 0;
            foreach (var modifier in modifiers)
            {
                if (c++ > 0) output.Append('+');
                modifier.ToString(platformProfile, output);
            }
            if (c++ > 0) output.Append('+');
            Input.ToString(platformProfile, output);
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

        [field: SerializeReference, PolymorphicField]
        public VectorAxis Input { get; private set; }

        [field: SerializeReference, PolymorphicField]
        private InputButton[] modifiers;
        public ReadOnlySpan<InputButton> Modifiers => modifiers;

        public override void Update(int index)
        {
            value = CheckModifiers(modifiers, index) ? Input.GetValue(index) : default;
        }

        public override void ToString(PlatformProfile platformProfile, StringBuilder output)
        {
            int c = 0;
            foreach (var modifier in modifiers)
            {
                if (c++ > 0) output.Append('+');
                modifier.ToString(platformProfile, output);
            }
            if (c++ > 0) output.Append('+');
            Input.ToString(platformProfile, output);
        }
    }
}
