using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace AggroBird.GameFramework
{
    // Consumable input action
    public abstract class InputAction<T>
    {
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
    public abstract class InputAxis<T>
    {
        internal InputAxis()
        {

        }

        protected T value;
        public T Value => value;

        public static implicit operator T(InputAxis<T> action)
        {
            return action.value;
        }
    }

    // Add this attribute to InputAction<T> or InputAxis<T> to bind input elements to them
    // InputButton maps to InputAction<bool>
    // InputDirection maps to InputAction<Direction>
    // LinearAxis maps to InputAxis<float>
    // VectorAxis maps to InputAxis<Vector2>
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

        public Vector2 CameraInput { get; protected set; }
        public InputAction<bool> Confirm => confirm;
        public InputAction<bool> Cancel => cancel;
        public InputAction<Direction> DirectionInput => directionInput;

        protected readonly WriteableInputAction<bool> confirm = new();
        protected readonly WriteableInputAction<bool> cancel = new();
        protected readonly WriteableInputAction<Direction> directionInput = new();


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
                public abstract void Update(Controller controller, int index);
                public abstract object Target { get; }
                public readonly List<MemberBinding> inputs = new();
                public float clampMax;

                protected struct BindingEnumerator<T>
                {
                    public BindingEnumerator(object binding)
                    {
                        index = -1;
                        this.binding = binding;
                        isArray = binding.GetType().IsArray;
                        count = isArray ? ((Array)binding).Length : 1;
                    }

                    private int index;
                    private readonly object binding;
                    private readonly bool isArray;
                    private readonly int count;

                    public readonly T Current => (T)(isArray ? ((Array)binding).GetValue(index) : binding);
                    public bool MoveNext() => ++index < count;
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
            }

            private sealed class BoolActionBinding : InputBinding
            {
                public readonly WriteableInputAction<bool> action = new();

                public override object Target => action;

                public override void Update(Controller controller, int index)
                {
                    bool value = false;
                    foreach (var input in inputs)
                    {
                        foreach (var inputButton in new BindingIterator<InputButton>(controller, input))
                        {
                            if (inputButton != null)
                            {
                                inputButton.Update(index);
                                value |= inputButton.IsPressed;
                            }
                        }
                    }
                    action.Value = value;
                }
            }

            private sealed class DirectionActionBinding : InputBinding
            {
                public readonly WriteableInputAction<Direction> action = new();

                public override object Target => action;

                public override void Update(Controller controller, int index)
                {
                    Direction value = Direction.None;
                    foreach (var input in inputs)
                    {
                        foreach (var inputDirection in new BindingIterator<InputDirection>(controller, input))
                        {
                            if (inputDirection != null)
                            {
                                inputDirection.Update(index);
                                if (value == Direction.None)
                                {
                                    value = inputDirection.Value;
                                }
                            }
                        }
                    }
                    action.Value = value;
                }
            }

            private sealed class LinearAxisBinding : InputBinding
            {
                public readonly WriteableInputAxis<float> axis = new();

                public override object Target => axis;

                public override void Update(Controller controller, int index)
                {
                    float value = 0;
                    foreach (var input in inputs)
                    {
                        foreach (var linearAxis in new BindingIterator<LinearAxis>(controller, input))
                        {
                            if (linearAxis != null)
                            {
                                linearAxis.Update(index);
                                value += linearAxis.Value;
                            }
                        }
                    }
                    axis.Value = Mathf.Clamp(value, -clampMax, clampMax);
                }
            }

            private sealed class VectorAxisBinding : InputBinding
            {
                public readonly WriteableInputAxis<Vector2> axis = new();

                public override object Target => axis;

                public override void Update(Controller controller, int index)
                {
                    Vector2 value = Vector2.zero;
                    foreach (var input in inputs)
                    {
                        foreach (var vectorAxis in new BindingIterator<VectorAxis>(controller, input))
                        {
                            if (vectorAxis != null)
                            {
                                vectorAxis.Update(index);
                                value += vectorAxis.Value;
                            }
                        }
                    }
                    axis.Value = Vector2.ClampMagnitude(value, clampMax);
                }
            }

            public ControllerInputBinding(Controller controller)
            {
                Type controllerType = controller.GetType();
                Dictionary<string, InputBinding> memberInputBindings = new();
                void Bind<T>(string name, MemberBinding target, MemberBinding input, float clampMax = float.MaxValue) where T : InputBinding, new()
                {
                    if (!memberInputBindings.TryGetValue(name, out InputBinding binding))
                    {
                        binding = new T { clampMax = clampMax };
                        target.SetValue(controller, binding.Target);
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
                                Type genericTypeDefinition = targetElementType.GetGenericTypeDefinition();
                                if (genericTypeDefinition != null)
                                {
                                    if (genericTypeDefinition.Equals(typeof(InputAction<>)))
                                    {
                                        var genericArgument = targetElementType.GetGenericArguments()[0];

                                        if (inputElementType.Equals(typeof(InputButton)) && genericArgument.Equals(typeof(bool)))
                                        {
                                            // InputAction<bool>
                                            Bind<BoolActionBinding>(memberName, targetBinding, inputBinding);
                                            continue;
                                        }
                                        else if (inputElementType.Equals(typeof(InputDirection)) && genericArgument.Equals(typeof(Direction)))
                                        {
                                            // InputAction<Direction>
                                            Bind<DirectionActionBinding>(memberName, targetBinding, inputBinding);
                                            continue;
                                        }
                                    }
                                    else if (genericTypeDefinition.Equals(typeof(InputAxis<>)))
                                    {
                                        var genericArgument = targetElementType.GetGenericArguments()[0];

                                        ClampAxisMagnitudeAttribute clampAttribute = target.GetCustomAttribute<ClampAxisMagnitudeAttribute>();
                                        float clampMax = clampAttribute == null ? float.MaxValue : clampAttribute.max;

                                        if (inputElementType.Equals(typeof(LinearAxis)) && genericArgument.Equals(typeof(float)))
                                        {
                                            // InputAxis<float>
                                            Bind<LinearAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                            continue;
                                        }
                                        else if (inputElementType.Equals(typeof(VectorAxis)) && genericArgument.Equals(typeof(Vector2)))
                                        {
                                            // InputAxis<Vector2>
                                            Bind<VectorAxisBinding>(memberName, targetBinding, inputBinding, clampMax);
                                            continue;
                                        }
                                    }
                                    Debug.LogError($"Failed to bind input element '{member.Name}' ({inputBinding.MemberType}) to member '{target.Name}' ({targetBinding.MemberType})");
                                }
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
