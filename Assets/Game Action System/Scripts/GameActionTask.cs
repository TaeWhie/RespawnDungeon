using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    public enum TaskState
    {
        /// <summary>
        /// The task is not started
        /// </summary>
        Idle,

        /// <summary>
        /// The task is running
        /// </summary>
        Running,

        /// <summary>
        /// The task is complete
        /// </summary>
        Completed,
    }

    public enum StatusCode
    {
        Running, Completed
    }

    [RequireComponent(typeof(GameAction))]
    public abstract class GameActionTask : MonoBehaviour
    {
        [Tooltip("Delay in seconds before the task is executed."), Min(0), SerializeField]
        private float startDelay = 0;

        public TaskState State { get; private set; } = TaskState.Idle;

        float startTime = 0.0f;

        /// <summary>
        /// Gets or sets the start delay of the Game Action Task
        /// </summary>
        public float StartDelay
        {
            get => startDelay;
            set
            {
                // Make sure start delay is >= 0
                startDelay = Mathf.Max(0, value);
            }
        }

        protected virtual void Start()
        {
            // While there is nothing to do here by default,
            // having a Start method allows to enable/disable a component in the inspector,
            // which can be very useful
        }

        /// <summary>
        /// This gets called when the task is started
        /// </summary>
        public virtual void OnStart() { }

        /// <summary>
        /// This gets called when the task is completed
        /// </summary>
        public virtual void OnStop() { }

        /// <summary>
        /// Resets the task to idle
        /// </summary>
        public void Restart()
        {
            State = TaskState.Idle;
        }

        /// <summary>
        /// Executes the task
        /// </summary>
        public TaskState Tick()
        {
            // Do nothing if the task is already completed
            if(State == TaskState.Completed) 
            {
                return TaskState.Completed;
            }

            // Start if not running yet
            if(State != TaskState.Running) 
            {
                startTime = Time.time;
                OnStart();
                State = TaskState.Running;
            }

            // Wait until start delay is over
            if(Time.time<startTime+StartDelay)
            {
                return State;
            }

            // Execute the task
            var statusCode = OnExecute();

            // Get the task state from the status code
            State = statusCode switch
            {
                StatusCode.Running => TaskState.Running,
                StatusCode.Completed => TaskState.Completed,
                _ => throw new System.NotImplementedException(),
            };

            // Call stop callback when completed
            if (State != TaskState.Running)
            {
                OnStop();
            }

            return State;
        }

        /// <summary>
        /// Gets called when the task is executed
        /// </summary>
        protected abstract StatusCode OnExecute();

        /// <summary>
        /// Returns the label of teh task
        /// </summary>
        /// <returns></returns>
        public virtual string GetLabel()
        {
            return GetType().Name;
        }

        /// <summary>
        /// Returns a description of what the task is doing
        /// </summary>
        /// <returns></returns>
        public virtual string GetDescription()
        {
            return string.Empty;
        }
    }
}