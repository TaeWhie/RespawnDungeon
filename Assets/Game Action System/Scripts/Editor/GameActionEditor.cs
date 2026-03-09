using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Ilumisoft.GameActionSystem.Editor
{
    [SelectionBase]
    [CustomEditor(typeof(GameAction), true)]
    public class GameActionEditor : UnityEditor.Editor
    {
        public static UnityAction<GameActionTrigger> DrawGizmoAction = null;

        readonly GUIContent triggerListGUIContent = new("Triggers", "List of all triggers connected to this action.");
        readonly GUIContent taskListGUIContent = new("Tasks", "List of all tasks executed by this action.");
        readonly List<GameActionTrigger> triggers = new();

        GameActionTask[] tasks = null;

        SerializedProperty modeProperty;

        void OnEnable()
        {
            var gameAction = target as GameAction;

            modeProperty = serializedObject.FindProperty("executionMode");

            // Find triggers
            CreateTriggerList(gameAction);

            // Get the tasks belonging to the Game Action
            tasks = gameAction.GetComponents<GameActionTask>();
        }

        /// <summary>
        /// Finds all triggers which are triggering the Game Action and adds them to the triggers list
        /// </summary>
        /// <param name="gameAction"></param>
        private void CreateTriggerList(GameAction gameAction)
        {
            triggers.Clear();

            var availableTriggers = FindObjectsByType<GameActionTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

            // Find all triggers connected to this Game Action
            foreach (var trigger in availableTriggers)
            {
                if (trigger.GameActions != null && trigger.GameActions.Contains(gameAction))
                {
                    triggers.Add(trigger);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(modeProperty);
            serializedObject.ApplyModifiedProperties();

            DrawTriggerListGUI();
            DrawTasksListGUI();
        }

        /// <summary>
        /// Draws the list of triggers
        /// </summary>
        private void DrawTriggerListGUI()
        {
            // Show a warning help box if no triggers are connected to the Game Action
            if (triggers.Count == 0)
            {
                EditorGUILayout.HelpBox("No Game Action Trigger is connected to this Game Action.", MessageType.Warning);
            }
            // Otherwise show the list of triggers
            else
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label(triggerListGUIContent, EditorStyles.miniBoldLabel);
                EditorGUI.BeginDisabledGroup(true);

                foreach (var trigger in triggers)
                {
                    EditorGUILayout.ObjectField(trigger, typeof(GameActionTrigger), true);
                }

                EditorGUI.EndDisabledGroup();
                GUILayout.EndVertical();
            }
        }

        /// <summary>
        /// Draws the list of tasks
        /// </summary>
        private void DrawTasksListGUI()
        {
            // Show a warning help box if no task has been added to the Game Action
            if (tasks == null || tasks.Length == 0)
            {
                EditorGUILayout.HelpBox("No task assigned to this Game Action. Click 'Add Component' and select a task from 'Game Action System/Tasks' to add one.", MessageType.Warning);
            }
            // Otherwise show the list of tasks
            else
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label(taskListGUIContent, EditorStyles.miniBoldLabel);
                EditorGUI.BeginDisabledGroup(true);

                foreach (var task in tasks)
                {
                    GUILayout.BeginHorizontal();
                    GUIContent guiContent = EditorGUIUtility.ObjectContent(task, task.GetType());

                    if (task.StartDelay > 0)
                    {
                        guiContent.text = $"{task.GetLabel()} after {task.StartDelay}s";
                    }
                    else
                    {
                        guiContent.text = task.GetLabel();
                    }

                    guiContent.tooltip = task.GetDescription();
                    EditorGUILayout.LabelField(guiContent, EditorStyles.objectField);
                    GUILayout.EndHorizontal();
                }

                EditorGUI.EndDisabledGroup();
                GUILayout.EndVertical();
            }
        }

        void OnSceneGUI()
        {
            foreach (var trigger in triggers)
            {
                DrawGizmoAction?.Invoke(trigger);
            }
        }

        [MenuItem("GameObject/Game Action System/Game Action", false, 10)]
        static void CreateGameAction(MenuCommand menuCommand)
        {
            GameObject gameObject = new("Game Action");
            GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
            Undo.AddComponent<GameAction>(gameObject);
            Selection.activeObject = gameObject;
        }
    }
}