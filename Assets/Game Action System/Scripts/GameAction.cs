using System.Collections;
using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [System.Serializable]
    public enum GameActionExecutionMode
    {
        Parallel,
        Sequential
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Game Action System/Game Action", -100)]
    public class GameAction : MonoBehaviour
    {
        [SerializeField, Tooltip("When set to parallel, all tasks will be executed simultaneously. When set to sequential, tasks will be executed one after the other and each task will only be executed when its predecessor has been completed.")]
        GameActionExecutionMode executionMode = GameActionExecutionMode.Parallel;

        /// <summary>
        /// The list of tasks that will be executed when the action is executed
        /// </summary>
        GameActionTask[] tasks = null;

        private void Awake()
        {
            tasks = GetComponents<GameActionTask>();
        }

        /// <summary>
        /// Executes the game action
        /// </summary>
        public void Execute()
        {
            StartCoroutine(ExecuteTasksCoroutine());
        }

        /// <summary>
        /// Executes all tasks
        /// </summary>
        /// <returns></returns>
        IEnumerator ExecuteTasksCoroutine()
        {
            switch (executionMode)
            {
                case GameActionExecutionMode.Sequential:
                    yield return ExecuteSequentialCoroutine();
                    break;
                case GameActionExecutionMode.Parallel:
                    yield return ExecuteParallellCoroutine();
                    break;
            }
        }

        /// <summary>
        /// Executes all tasks sequentially
        /// </summary>
        /// <returns></returns>
        IEnumerator ExecuteSequentialCoroutine()
        {
            int currentTaskIndex = 0;

            RestartTasks();

            // Execute all tasks one after the other
            while (currentTaskIndex < tasks.Length)
            {
                var task = tasks[currentTaskIndex];

                if (task != null && task.isActiveAndEnabled)
                {
                    var state = task.Tick();

                    // Continue if task is completed
                    if (state == TaskState.Completed)
                    {
                        currentTaskIndex++;
                        continue;
                    }
                }
                // Skip inactve tasks
                else
                {
                    currentTaskIndex++;
                    continue;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Executes all tasks parallelly
        /// </summary>
        /// <returns></returns>
        IEnumerator ExecuteParallellCoroutine()
        {
            bool isCompleted = true;

            RestartTasks();

            // Run until all tasks are completed
            do
            {
                foreach(var task in tasks)
                {
                    if(task != null && task.isActiveAndEnabled && task.State != TaskState.Completed)
                    {
                        var state = task.Tick();

                        if(state != TaskState.Completed)
                        {
                            isCompleted = false;
                        }
                    }
                }

                yield return null;

            }while(isCompleted == false);
        }

        /// <summary>
        /// Restarts all tasks
        /// </summary>
        void RestartTasks()
        {
            foreach (var task in tasks)
            {
                if (task != null)
                {
                    task.Restart();
                }
            }
        }
    }
}