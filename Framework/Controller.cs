using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace AggroBird.GameFramework
{
    // Consumable input action
    public class InputAction<T>
    {
        private static InputAction<T> empty = new();
        public static InputAction<T> Empty => empty;

        internal InputAction()
        {
            reactive = new(default);
        }

        protected T value = default;
        public T Value => value;

        protected readonly AsyncReactiveProperty<T> reactive;

        public UniTask<T> WaitAsync(CancellationToken cancellationToken = default) => reactive.WaitAsync(cancellationToken);

        public void Use()
        {
            value = default;
        }
        public T GetValue(bool use)
        {
            T result = value;
            if (use) Use();
            return result;
        }

        public static implicit operator T(InputAction<T> action)
        {
            return action.value;
        }
    }

    // Input axis
    public class InputAxis<T>
    {
        private static InputAxis<T> empty = new();
        public static InputAxis<T> Empty => empty;

        internal InputAxis()
        {

        }

        protected T value = default;
        public T Value => value;

        public static implicit operator T(InputAxis<T> action)
        {
            return action.value;
        }
    }

    // Add this attribute to InputAction<T> or InputAxis<T> to bind input elements to them
    // InputAction<bool> can only support InputButton.
    // InputAction<Direction> can only support InputDirection.
    // InputAxis<T> will try to apply the nearest conversion to the destination operand.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class BindInputAttribute : Attribute
    {
        public BindInputAttribute(string memberName)
        {
            this.memberName = memberName;
        }

        public readonly string memberName;
    }

    // Add this attribute to InputAxis<T> to clamp the magnitude
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ClampAxisMagnitudeAttribute : Attribute
    {
        public ClampAxisMagnitudeAttribute(float max)
        {
            this.max = max;
        }

        public readonly float max;
    }

    // Controller base class
    public abstract class Controller : ScriptableObject
    {
        protected sealed class WriteableInputAction<T> : InputAction<T>
        {
            public new T Value
            {
                get => value;
                set
                {
                    // Only update if the value has changed, this will invoke the reactive
                    if (!EqualityComparer<T>.Default.Equals(this.value, value))
                    {
                        reactive.Value = this.value = value;
                    }
                }
            }
        }

        protected sealed class WriteableInputAxis<T> : InputAxis<T>
        {
            public new T Value
            {
                get => value;
                set => this.value = value;
            }
        }

        public InputAxis<Vector2> CameraInput { get; protected set; } = InputAxis<Vector2>.Empty;
        public InputAction<bool> Confirm { get; protected set; } = InputAction<bool>.Empty;
        public InputAction<bool> Cancel { get; protected set; } = InputAction<bool>.Empty;
        public InputAction<Direction> DirectionInput { get; protected set; } = InputAction<Direction>.Empty;


        private sealed class ControllerInputBinding
        {
            const BindingFlags MemberBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private abstract class MemberBinding
            {
                public Type MemberType { get; protected set; }
                public abstract object GetValue(object target);
                public abstract void SetValue(object target, object value);
            }

            private sealed class FieldBinding : MemberBinding
            {
                public FieldBinding(FieldInfo fieldInfo)
                {
                    this.fieldInfo = fieldInfo;
                    MemberType = fieldInfo.FieldType;
                }

                private readonly FieldInfo fieldInfo;

                public override object GetValue(object target) => fieldInfo.GetValue(target);
                public override void SetValue(object target, object value) => fieldInfo.SetValue(target, value);
            }

            private sealed class PropertyBinding : MemberBinding
            {
                public PropertyBinding(PropertyInfo propertyInfo)
                {
                    this.propertyInfo = propertyInfo;
                    MemberType = propertyInfo.PropertyType;
                }

                private readonly PropertyInfo propertyInfo;

                public override object GetValue(object target) => propertyInfo.GetValue(target);
                public override void SetValue(object target, object value) => propertyInfo.SetValue(target, value);
            }

            private abstract class InputBinding
            {
                protected struct BindingEnumerator<T>
                {
                    public BindingEnumerator(object binding)
                    {
                        index = -1;
                        this.binding = binding;
                        isArray = binding.GetType().IsArray;
                        if (isArray)
                        {
                            array = (Array)binding;
                            count = array.Length;
                        }
                        else
                        {
                            array = null;
                            count = 1;
                        }
                    }

                    private int index;
                    private readonly Array array;
                    private readonly object binding;
                    private readonly bool isArray;
                    private readonly int count;

                    public readonly T Current => (T)(isArray ? array.GetValue(index) : binding);
                    public bool MoveNext()
                    {
                        while (true)
                        {
                            index++;
                            if (index >= count)
                            {
                                return false;
                            }
                            if (Current == null)
                            {
                                continue;
                            }
                            return true;
                        }
                    }
                }
                protected readonly struct BindingIterator<T>
                {
                    public BindingIterator(Controller controller, MemberBinding binding)
                    {
                        this.binding = binding.GetValue(controller);
                    }

                    private readonly object binding;

                    public BindingEnumerator<T> GetEnumerator()
                    {
                        return new BindingEnumerator<T>(binding);
                    }
                }

                public bool GatherInputButtonValues(Controller controller, int index)
                {
                    bool value = false;
                    foreach (var input in inputs)
                    {
                        foreach (var inputButton in new BindingIterator<InputButton>(controller, input))
                        {
                            inputButton.Update(index);
                            value |= inputButton.IsPressed;
                        }
                    }
                    return value;
                }
                public Direction GatherInputDirectionValues(Controller controller, int index)
                {
                    Direction value = Direction.None;
                    foreach (var input in inputs)
                    {
                        foreach (var inputDirection in new BindingIterator<InputDirection>(controller, input))
                        {
                            inputDirection.Update(index);
                            if (value == Direction.None)
                            {
                                value = inputDirection.Value;
                            }
                        }
                    }
                    return value;
                }
                public float GatherLinearAxisValues(Controller controller, int index)
                {
                    float value = 0;
                    foreach (var input in inputs)
                    {
                        foreach (var inputElement in new BindingIterator<InputElement>(controller, input))
                        {
                            inputElement.Update(index);

                            if (inputElement is LinearAxis linearAxis)
                            {
                                value += linearAxis.Value;
                            }
                            else if (inputElement is InputButton inputButton)
                            {
                                var state = inputButton.State;
                                value += (state == ButtonState.Pressed || state == ButtonState.Held) ? 1 : 0;
                            }
                        }
                    }
                    return Mathf.Clamp(value, -clampMax, clampMax);
                }
                public Vector2 GatherVectorAxisValues(Controller controller, int index)
                {
                    Vector2 value = Vector2.zero;
                    foreach (var input in inputs)
                    {
                        foreach (var inputElement in new BindingIterator<InputElement>(controller, input))
                        {
                            inputElement.Update(index);

                            if (inputElement is VectorAxis vectorAxis)
                            {
                                value += vectorAxis.Value;
                            }
                            else if (inputElement is InputDirection inputDirection)
                            {
                                switch (inputDirection.Value)
                                {
                                    case Direction.Up:
                                        value += Vector2.up;
                                        break;
                                    case Direction.Right:
                                        value += Vector2.right;
                                        break;
                                    case Direction.Down:
                                        value += Vector2.down;
                                        break;
                                    case Direction.Left:
                                        value += Vector2.left;
                                        break;
                                }
                            }
                        }
                    }
                    return Vector2.ClampMagnitude(value, clampMax);
                }

                public virtual void Bind(Controller controller) { }
                public abstract void Update(Controller controller, int index);

                public MemberBinding target;
                public readonly List<MemberBinding> inputs = new();
                public float clampMax;
            }

            private sealed class BoolActionBinding : InputBinding
            {
                private readonly WriteableInputAction<bool> action = new();

                public override void Bind(Controller controller) => target.SetValue(controller, action);
                public override void Update(Controller controller, int index) => action.Value = GatherInputButtonValues(controller, index);
            }

            private sealed class DirectionActionBinding : InputBinding
            {
                private readonly WriteableInputAction<Direction> action = new();

                public override void Bind(Controller controller) => target.SetValue(controller, action);
                public override void Update(Controller controller, int index) => action.Value = GatherInputDirectionValues(controller, index);
            }

            private sealed class BoolAxisBinding : InputBinding
            {
                private readonly WriteableInputAxis<bool> axis = new();

                public override void Bind(Controller controller) => target.SetValue(controller, axis);
                public override void Update(Controller controller, int index) => axis.Value = GatherLinearAxisValues(controller, index) > 0.5f;
            }

            private sealed class LinearAxisBinding : InputBinding
            {
                private readonly WriteableInputAxis<float> axis = new();

                public override void Bind(Controller controller) => target.SetValue(controller, axis);
                public override void Update(Controller controller, int index) => axis.Value = GatherLinearAxisValues(controller, index);
            }

            private sealed class DirectionAxisBinding : InputBinding
            {
                private readonly WriteableInputAxis<Direction> axis = new();

                public override void Bind(Controller controller) => target.SetValue(controller, axis);
                public override void Update(Controller controller, int index) => axis.Value = InputSystemUtility.DirectionFromVector(GatherVectorAxisValues(controller, index));
            }

            private sealed class VectorAxisBinding : InputBinding
            {
                private readonly WriteableInputAxis<Vector2> axis = new();

                public override void Bind(Controller controller) => target.SetValue(controller, axis);
                public override void Update(Controller controller, int index) => axis.Value = GatherVectorAxisValues(controller, index);
            }


            // Controller binding entry point
            public ControllerInputBinding(Controller controller)
            {
                Type controllerType = controller.GetType();
                Dictionary<string, InputBinding> memberInputBindings = new();
                void Bind<T>(string name, MemberBinding target, MemberBinding input, float clampMax = float.MaxValue) where T : InputBinding, new()
                {
                    if (!memberInputBindings.TryGetValue(name, out InputBinding binding))
                    {
                        binding = new T { target = target, clampMax = clampMax };
                        binding.Bind(controller);
                        memberInputBindings.Add(name, binding);
                        bindings.Add(binding);
                    }
                    binding.inputs.Add(input);
                }

                foreach (var member in controllerType.GetMembers(MemberBindingFlags))
                {
                    var enumerable = member.GetCustomAttributes<BindInputAttribute>().GetEnumerator();
                    if (enumerable.MoveNext())
                    {
                        MemberBinding inputBinding;
                        switch (member)
                        {
                            case FieldInfo fieldInfo:
                                inputBinding = new FieldBinding(fieldInfo);
                                break;
                            case PropertyInfo propertyInfo:
                                inputBinding = new PropertyBinding(propertyInfo);
                                break;
                            default:
                                Debug.LogError("Unexpected member type");
                                continue;
                        }

                        Type inputElementType = inputBinding.MemberType.IsArray ? inputBinding.MemberType.GetElementType() : inputBinding.MemberType;
                        do
                        {
                            MemberInfo FindTargetMember(string name)
                            {
                                foreach (var target in controllerType.FindMembers(MemberTypes.Property | MemberTypes.Field, MemberBindingFlags, findTargetMemberFilter, name))
                                {
                                    return target;
                                }
                                return null;
                            }

                            string memberName = enumerable.Current.memberName;
                            MemberInfo target = FindTargetMember(memberName);
                            if (target != null)
                            {
                                MemberBinding targetBinding;
                                switch (target)
                                {
                                    case FieldInfo fieldInfo:
                                        targetBinding = new FieldBinding(fieldInfo);
                                        break;
                                    case PropertyInfo propertyInfo:
                                        if (!propertyInfo.CanWrite)
                                        {
                                            Debug.LogError($"Failed to bind member '{target.Name}' ({propertyInfo.PropertyType}), property is not assignable");
                                            continue;
                                        }
                                        targetBinding = new PropertyBinding(propertyInfo);
                                        break;
                                    default:
                                        Debug.LogError("Unexpected member type");
                                        continue;
                                }

                                Type targetElementType = targetBinding.MemberType;
                                if (targetElementType.IsGenericType)
                                {
                                    Type genericTypeDefinition = targetElementType.GetGenericTypeDefinition();
                                    if (genericTypeDefinition != null)
                                    {
                                        if (genericTypeDefinition.Equals(typeof(InputAction<>)))
                                        {
                                            var genericArgument = targetElementType.GetGenericArguments()[0];

                                            if (inputElementType.Equals(typeof(InputButton)) && genericArgument.Equals(typeof(bool)))
                                            {
                                                Bind<BoolActionBinding>(memberName, targetBinding, inputBinding);
                                                continue;
                                            }
                                            else if (inputElementType.Equals(typeof(InputDirection)) && genericArgument.Equals(typeof(Direction)))
                                            {
                                                Bind<DirectionActionBinding>(memberName, targetBinding, inputBinding);
                                                continue;
                                            }
                                        }
                                        else if (genericTypeDefinition.Equals(typeof(InputAxis<>)))
                                        {
                                            var genericArgument = targetElementType.GetGenericArguments()[0];

                                            ClampAxisMagnitudeAttribute clampAttribute = target.GetCustomAttribute<ClampAxisMagnitudeAttribute>();
                                            float clampMax = clampAttribute == null ? float.MaxValue : clampAttribute.max;

                                            if (inputElementType.Equals(typeof(LinearAxis)))
                                            {
                                                if (genericArgument.Equals(typeof(bool)))
                                                {
                                                    Bind<BoolAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                                else if (genericArgument.Equals(typeof(float)))
                                                {
                                                    Bind<LinearAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                            }
                                            else if (inputElementType.Equals(typeof(VectorAxis)))
                                            {
                                                if (genericArgument.Equals(typeof(Direction)))
                                                {
                                                    Bind<DirectionAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                                else if (genericArgument.Equals(typeof(Vector2)))
                                                {
                                                    Bind<VectorAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                            }
                                            else if (inputElementType.Equals(typeof(InputButton)))
                                            {
                                                if (genericArgument.Equals(typeof(bool)))
                                                {
                                                    Bind<BoolAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                                else if (genericArgument.Equals(typeof(float)))
                                                {
                                                    Bind<LinearAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                            }
                                            else if (inputElementType.Equals(typeof(InputDirection)))
                                            {
                                                if (genericArgument.Equals(typeof(Direction)))
                                                {
                                                    Bind<DirectionAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                                else if (genericArgument.Equals(typeof(Vector2)))
                                                {
                                                    Bind<VectorAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                }

                                Debug.LogError($"Failed to bind input element '{member.Name}' ({inputBinding.MemberType}) to member '{target.Name}' ({targetBinding.MemberType})");
                            }
                            else
                            {
                                Debug.LogError($"Failed to find input target member '{memberName}'");
                            }
                        }
                        while (enumerable.MoveNext());
                    }
                }
            }

            private readonly List<InputBinding> bindings = new();

            public void Update(Controller controller, int index)
            {
                foreach (var binding in bindings)
                {
                    binding.Update(controller, index);
                }
            }
        }

        private static bool FindTargetMember(MemberInfo memberInfo, object search)
        {
            return memberInfo.Name.Equals((string)search, StringComparison.Ordinal);
        }
        private static readonly MemberFilter findTargetMemberFilter = new(FindTargetMember);

        private ControllerInputBinding inputBinding;


        protected internal virtual void UpdateInput(Player player, bool inputEnabled)
        {
            inputBinding ??= new(this);
            inputBinding.Update(this, 0);
        }

        public virtual bool GetInputGlyph(int index, out Sprite background, out string text)
        {
            background = null;
            text = string.Empty;
            return false;
        }
    }
}
