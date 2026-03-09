using UnityEditor;

namespace Ilumisoft.GameActionSystem.Editor
{
    public static class InspectorUtility
    {
        /// <summary>
        /// Draws the default inspector without the script field
        /// </summary>
        /// <param name="serializedObject"></param>
        public static void DrawDefaultInspector(SerializedObject serializedObject)
        {
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return;
            }

            serializedObject.Update();

            if (serializedObject.targetObject != null)
            {
                SerializedProperty iterator = serializedObject.GetIterator();

                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (iterator.propertyPath != "m_Script")
                        {
                            EditorGUILayout.PropertyField(iterator, true);
                        }

                    } while (iterator.NextVisible(false));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}