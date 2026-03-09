using UnityEditor;

namespace Ilumisoft.GameActionSystem.Editor
{
    [CustomEditor(typeof(GameActionTask), true)]
    public class GameActionTaskEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            InspectorUtility.DrawDefaultInspector(serializedObject);
        }
    }
}