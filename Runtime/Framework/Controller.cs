using AggroBird.UnityExtend;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public sealed class InputCategoryReference<T> : IDisposable where T : Controller.Category
    {
        public InputCategoryReference(T category)
        {
            Category = category;
            categoryName = category.name;

            category.controller.IncrementCategoryReference(category);

            Controller.OnControllerReplacedCallback += OnControllerReplaced;
        }

        public void Dispose()
        {
            Controller.OnControllerReplacedCallback -= OnControllerReplaced;

            Category.controller.DecrementCategoryReference(Category);
        }

        private void OnControllerReplaced(Controller newController)
        {
            newController.TryGetInputCategory(categoryName, out var category);
            Category = category as T;
        }

        public T Category { get; private set; }
        private readonly string categoryName;
    }

    // Controller base class
    public abstract class Controller : ScriptableObject
    {
        protected enum UpdateMode
        {
            RequiresActivation,
            RequiresInputEnabled,
            AlwaysUpdate,
        }

        [AttributeUsage(AttributeTargets.Property)]
        protected sealed class CategoryUpdateModeAttribute : Attribute
        {
            public CategoryUpdateModeAttribute(UpdateMode updateMode)
            {
                UpdateMode = updateMode;
            }

            public UpdateMode UpdateMode { get; private set; }
        }

        public struct InputMappingProperty
        {
            public InputMappingProperty(PropertyInfo propertyInfo, Category category)
            {
                this.propertyInfo = propertyInfo;
                mapping = propertyInfo.GetValue(category) as InputMappingBase;

                name = propertyInfo.Name;
                hash = 0;
            }
            public readonly PropertyInfo propertyInfo;
            public readonly InputMappingBase mapping;
            internal string name;
            internal uint hash;

            public override readonly string ToString() => name;
        }

        public abstract class Category
        {
            private UpdateMode updateMode = UpdateMode.RequiresActivation;
            internal int references = 0;
            internal bool ShouldUpdate(bool inputEnabled)
            {
                return updateMode switch
                {
                    UpdateMode.RequiresActivation => inputEnabled && references > 0,
                    UpdateMode.RequiresInputEnabled => inputEnabled,
                    UpdateMode.AlwaysUpdate => true,
                    _ => false,
                };
            }

            public PropertyInfo PropertyInfo { get; private set; }
            internal string name;
            internal Controller controller;

            internal readonly List<InputMappingProperty> mappingProperties = new();
            public ReadOnlyList<InputMappingProperty> Mappings => mappingProperties;

            internal void GatherMappingProperties(Controller controller, PropertyInfo categoryPropertyInfo)
            {
                this.controller = controller;
                PropertyInfo = categoryPropertyInfo;
                name = PropertyInfo.Name;

                var updateModeAttribute = categoryPropertyInfo.GetCustomAttribute<CategoryUpdateModeAttribute>();
                if (updateModeAttribute != null)
                {
                    updateMode = updateModeAttribute.UpdateMode;
                }

                mappingProperties.Clear();

                Type thisType = GetType();
                foreach (var propertyInfo in thisType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance))
                {
                    if (propertyInfo.CanRead && typeof(InputMappingBase).IsAssignableFrom(propertyInfo.PropertyType))
                    {
                        var property = new InputMappingProperty(propertyInfo, this);
                        mappingProperties.Add(property);
                    }
                }
            }
            internal void Update(int index)
            {
                foreach (var property in mappingProperties)
                {
                    property.mapping?.Update(index);
                }
            }

            public override string ToString() => name;
        }

        [Serializable]
        public sealed class DefaultCategory : Category
        {
            [field: SerializeReference, PolymorphicField(allowNull: true)]
            public VectorAxisMapping CameraInput { get; internal set; }
            [field: SerializeReference, PolymorphicField(allowNull: true)]
            public InputButtonMapping Confirm { get; internal set; }
            [field: SerializeReference, PolymorphicField(allowNull: true)]
            public InputButtonMapping Cancel { get; internal set; }
            [field: SerializeReference, PolymorphicField(allowNull: true)]
            public InputDirectionMapping DirectionInput { get; internal set; }
        }
        [field: SerializeField]
        [CategoryUpdateMode(UpdateMode.AlwaysUpdate)]
        public DefaultCategory Default { get; internal set; } = new();

        private bool mappingsInitialized = false;
        private readonly Dictionary<uint, (string name, InputMappingBase mapping)> hashLookup = new();
        private readonly Dictionary<string, InputMappingBase> nameLookup = new();

        private readonly Dictionary<string, Category> inputCategories = new();
        protected IReadOnlyCollection<Category> InputCategories => inputCategories.Values;
        internal bool TryGetInputCategory(string name, out Category category)
        {
            return inputCategories.TryGetValue(name, out category);
        }

        public static uint HashMappingName(ReadOnlySpan<char> s)
        {
            const uint Offset = 2166136261u;
            const uint Prime = 16777619u;
            uint hash = Offset;
            for (int i = 0; i < s.Length; ++i)
            {
                uint c = s[i];
                hash ^= c;
                hash *= Prime;
            }
            return hash;
        }

        public static string[] GetInputMappingsFromControllerType(Type controllerType)
        {
            List<string> properties = new();
            foreach (var categoryProperty in controllerType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance))
            {
                if (categoryProperty.CanRead && typeof(Category).IsAssignableFrom(categoryProperty.PropertyType))
                {
                    foreach (var mappingProperty in categoryProperty.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance))
                    {
                        if (mappingProperty.CanRead && typeof(InputMappingBase).IsAssignableFrom(mappingProperty.PropertyType))
                        {
                            properties.Add($"{categoryProperty.Name}/{mappingProperty.Name}");
                        }
                    }
                }
            }
            return properties.ToArray();
        }

        public void RebuildMappings()
        {
            mappingsInitialized = false;
            inputCategories.Clear();
            EnsureMappings();
        }

        private void EnsureMappings()
        {
            if (!mappingsInitialized)
            {
                mappingsInitialized = true;

                hashLookup.Clear();
                nameLookup.Clear();

                foreach (var propertyInfo in GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance))
                {
                    if (propertyInfo.CanRead && typeof(Category).IsAssignableFrom(propertyInfo.PropertyType))
                    {
                        Category category = propertyInfo.GetValue(this) as Category;
                        if (category != null)
                        {
                            category.GatherMappingProperties(this, propertyInfo);

                            if (category.mappingProperties.Count > 0)
                            {
                                inputCategories.Add(category.name, category);
                                for (int i = 0; i < category.mappingProperties.Count; i++)
                                {
                                    var property = category.mappingProperties[i];
                                    property.name = $"{category.name}/{property.name}";
                                    property.hash = HashMappingName(property.name);
                                    if (property.mapping != null)
                                    {
                                        property.mapping.Name = property.name;
                                        property.mapping.Hash = property.hash;
                                    }
                                    else
                                    {
                                        //Debug.LogWarning($"Input mapping '{property.name}' has no value for controller '{this}'", this);
                                    }
                                    AddMappingToLookup(property);
                                    category.mappingProperties[i] = property;
                                }
                            }
                        }
                    }
                }
            }
        }
        protected void AddMappingToLookup(in InputMappingProperty property)
        {
            hashLookup[property.hash] = (property.name, property.mapping);
            nameLookup[property.name] = property.mapping;
        }


        internal static event Action<Controller> OnControllerReplacedCallback;

        public virtual void Initialize()
        {
            EnsureMappings();
        }

        public virtual void OnReplaceController(Controller previousController)
        {
            foreach (var pair in previousController.inputCategories)
            {
                if (inputCategories.TryGetValue(pair.Key, out Category category))
                {
                    category.references = pair.Value.references;
                }
            }
            OnControllerReplacedCallback?.Invoke(this);
        }

        protected internal virtual void IncrementCategoryReference(Category category)
        {
            category.references++;
        }
        protected internal virtual void DecrementCategoryReference(Category category)
        {
            category.references--;
        }

        public T GetMapping<T>(uint hash) where T : InputMappingBase
        {
            EnsureMappings();

            return hashLookup.TryGetValue(hash, out var pair) && pair.mapping is T casted ? casted : null;
        }
        public T GetMapping<T>(string name) where T : InputMappingBase
        {
            EnsureMappings();

            return nameLookup.TryGetValue(name, out var mapping) && mapping is T casted ? casted : null;
        }


        protected internal virtual void UpdateInput(Player player, int index, bool inputEnabled)
        {
            EnsureMappings();

            foreach (var category in inputCategories.Values)
            {
                if (category.ShouldUpdate(inputEnabled))
                {
                    category.Update(index);
                }
            }
        }
    }
}
