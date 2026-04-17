using AggroBird.UnityExtend.Editor;
using UnityEditor;
using UnityEditor.UI;

namespace AggroBird.GameFramework.Editor
{
    [CustomEditor(typeof(FocusableSelectable), editorForChildClasses: true)]
    public class FocusableSelectableInspector : ButtonEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            bool passedOnClick = false;
            serializedObject.Update();
            foreach (var prop in new SerializedPropertyEnumerator(serializedObject))
            {
                if (!passedOnClick)
                {
                    if (prop.name == "m_OnClick")
                    {
                        passedOnClick = true;
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(prop);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}