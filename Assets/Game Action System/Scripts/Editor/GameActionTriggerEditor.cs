using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Ilumisoft.GameActionSystem.Editor
{
    [SelectionBase]
    [CustomEditor(typeof(GameActionTrigger), true)]
    public class GameActionTriggerEditor : UnityEditor.Editor
    {
        public static UnityAction<GameActionTrigger> DrawGizmoAction = null;

        GameActionTrigger gameActionTrigger;

        SerializedProperty onlyOnceProperty;
        SerializedProperty cooldownProperty;
        SerializedProperty gameActionsPropery;

        private void OnEnable()
        {
            gameActionTrigger = (GameActionTrigger)target;

            onlyOnceProperty = serializedObject.FindProperty("onlyOnce");
            cooldownProperty = serializedObject.FindProperty("cooldown");
            gameActionsPropery = serializedObject.FindProperty("gameActions");
        }

        public override void OnInspectorGUI()
        {
            DrawInfoBox();

            serializedObject.Update();

            EditorGUILayout.PropertyField(onlyOnceProperty);

            if (onlyOnceProperty.boolValue == false)
            {
                EditorGUILayout.PropertyField(cooldownProperty);
            }

            EditorGUILayout.PropertyField(gameActionsPropery);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawInfoBox()
        {
            if (gameActionTrigger != null && Application.isPlaying)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Info");
                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.LabelField($"Has Been Triggered: {gameActionTrigger.HasBeenTriggered}", EditorStyles.miniBoldLabel);

                EditorGUI.EndDisabledGroup();
                GUILayout.EndVertical();
            }
        }

        void OnSceneGUI()
        {
            var gameActionTrigger = target as GameActionTrigger;

            if (gameActionTrigger.GameActions != null)
            {
                DrawGizmoAction?.Invoke(gameActionTrigger);
            }
        }

        [MenuItem("GameObject/Game Action System/Game Action Trigger", false, 10)]
        static void CreateGameActionTrigger(MenuCommand menuCommand)
        {
            GameObject gameObject = new("Game Action Trigger");
            GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
            Undo.AddComponent<GameActionTrigger>(gameObject);
            Selection.activeObject = gameObject;
        }
    }
}