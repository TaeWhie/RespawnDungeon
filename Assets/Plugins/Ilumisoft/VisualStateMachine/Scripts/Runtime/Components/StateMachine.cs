namespace Ilumisoft.VisualStateMachine
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Events;

    [DefaultExecutionOrder(-1)]
    [DisallowMultipleComponent]
    public class StateMachine : MonoBehaviour
    {
        [SerializeField]
        private Graph graph = new Graph();

        /// <summary>
        /// Returns the ID of the previously active state. Empty if the state machine has not changed states yet
        /// </summary>
        public string PreviousState { get; protected set; } = string.Empty;

        /// <summary>
        /// Returns the ID of the currently active state or string. Empty if none is active
        /// </summary>
        public string CurrentState { get; protected set; } = string.Empty;

        /// <summary>
        /// Gets  the graph of the state machine, which contains all nodes and transitions
        /// </summary>
        public Graph Graph => graph;

        /// <summary>
        /// Gets the ID of the entry state
        /// </summary>
        public string EntryState => this.graph.EntryStateID;

        /// <summary>
        /// Returns true if the state machine has switched states yet
        /// </summary>
        public bool HasPreviousState => PreviousState != string.Empty;

        public bool IsPaused { get; protected set; } = false;

        // The transition handler executes transitions
        TransitionHandler transitionHandler;

        #region Callbacks
        /// <summary>
        /// Callback invoked when a state is entered
        /// </summary>
        public UnityAction<State> OnEnterState = null;

        /// <summary>
        /// Callback invoked when a state is left
        /// </summary>
        public UnityAction<State> OnExitState = null;

        /// <summary>
        /// Callback invoked when a transition is triggered.
        /// Event Order: OnTriggerTransition->OnExitState->OnEnterTransition->OnExitTransition->OnEnterState
        /// </summary>
        public UnityAction<Transition> OnTriggerTransition = null;

        /// <summary>
        /// Callback invoked when a transition is entered
        /// </summary>
        public UnityAction<Transition> OnEnterTransition = null;

        /// <summary>
        /// Callback invoked when a transition is left
        /// </summary>
        public UnityAction<Transition> OnExitTransition = null;
        #endregion

        private void Awake()
        {
            transitionHandler = new TransitionHandler(this);
        }

        public void Start()
        {
            // Don't reset state machine, if it has already been started.
            // When the state machine is created at runtime, this method might get called multiple times
            // or from anopther script
            if (CurrentState == string.Empty)
            {
                Restart();
            }
        }

        private void Update()
        {
            // When the state machine is paused, states and running transitions won't be updated
            if (IsPaused)
            {
                return;
            }

            transitionHandler.Update();

            // Trigger the OnUpdateState event of the currently active state
            if (CurrentState != string.Empty && graph.TryGetState(CurrentState, out State state))
            {
                state?.OnUpdateState?.Invoke();
            }
        }

        /// <summary>
        /// Restarts the state machine by entering the entry state.
        /// Remark: The OnExitState event of the currently active state will not be triggered, when calling this method!
        /// </summary>
        public void Restart()
        {
            IsPaused = false;

            transitionHandler.Stop();

            this.PreviousState = string.Empty;
            this.CurrentState = this.EntryState;

            if (this.graph.TryGetNode(this.CurrentState, out Node node))
            {
                if (node is State state)
                {
                    OnEnterState?.Invoke(state);
                    state.OnEnterState.Invoke();
                }
            }
            else
            {
                Debug.LogWarning("Could not start state machine, because no entry state has been set", this);
            }
        }

        /// <summary>
        /// Instantly enters the state with the given ID.
        /// Please Note: Using this method will not call OnExit on the current state or require a transition between the current state and the given one.
        /// </summary>
        /// <param name="stateID"></param>
        public void ForceEnterState(string stateID)
        {
            if (this.graph.TryGetNode(stateID, out Node targetNode))
            {
                if (targetNode is State state)
                {
                    this.PreviousState = CurrentState;
                    this.CurrentState = state.ID;

                    OnEnterState?.Invoke(state);

                    state.OnEnterState.Invoke();

                    return;
                }
            }

            Debug.LogWarning($"Failed to enter state '{stateID}', because there is no state with the given ID on this state machine", this);
        }

        /// <summary>
        /// Tries to trigger the first transition found, which has the given label and goes out from the currently active state. 
        /// Returns true on success, false if none could be found.
        /// </summary>
        /// <param name="transitionLabel"></param>
        /// <returns></returns>
        public bool TryTriggerByLabel(string transitionLabel)
        {
            // Cancel if triggering a transition is currently not possible
            if (!CanTriggerTransitions())
            {
                return false;
            }

            // Check if there is any transition with the given label and the current state as its origin
            foreach (var transition in graph.Transitions)
            {
                if (transition.Label == transitionLabel && transition.OriginID == CurrentState)
                {
                    Trigger(transition);
                    return true;
                }
            }

            // Check if there is any transition with the given label and any state as its origin
            foreach (var transition in graph.Transitions)
            {
                if (transition.Label == transitionLabel)
                {
                    if (graph.TryGetNode(transition.OriginID, out Node node) && node is AnyState)
                    {
                        Trigger(transition);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Triggers the first transition found, which has the given label and goes out from the currently active state.
        /// </summary>
        /// <param name="transitionLabel"></param>
        public void TriggerByLabel(string transitionLabel)
        {
            // Cancel if triggering a transition is currently not possible
            if (!CanTriggerTransitions())
            {
                return;
            }

            if (TryTriggerByLabel(transitionLabel) == false)
            {
                Debug.LogWarning($"There is no transition with label {transitionLabel}, which could be triggered in the current context", this);
            }
        }

        /// <summary>
        /// Tries to trigger the first transition found which goes to the given state id from the currently active state. 
        /// </summary>
        /// <param name="stateID">ID of the state we're transitioning to.</param>
        public void TriggerByState(string stateID)
        {
            if (TryTriggerByState(stateID) == false)
            {
                Debug.LogWarning($"There is no transition from the current node to state {stateID}.", this);
            }
        }

        /// <summary>
        /// Tries to trigger the first transition found which goes to the given state id from the currently active state. 
        /// </summary>
        /// <param name="stateID">ID of the state we're transitioning to.</param>
        /// <returns>True on success, false if a suitable transition could not be found.</returns>
        public bool TryTriggerByState(string stateID)
        {
            // Cancel if triggering a transition is currently not possible
            if (!CanTriggerTransitions())
            {
                return false;
            }

            // Check if there is any transition to the given state with the current state as its origin
            foreach (var transition in graph.Transitions)
            {
                if (transition.OriginID == CurrentState && transition.TargetID == stateID)
                {
                    Trigger(transition);
                    return true;
                }
            }

            // Check if there is any transition to the given state with any state as its origin
            foreach (var transition in graph.Transitions)
            {
                if (transition.TargetID == stateID && graph.TryGetNode(transition.OriginID, out Node node) && node is AnyState)
                {
                    Trigger(transition);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to trigger the transition with the given ID and returns true on success, false otherwise.
        /// </summary>
        /// <param name="transitionID"></param>
        /// <returns></returns>
        public bool TryTrigger(string transitionID)
        {
            // Cancel if triggering a transition is currently not possible
            if (!CanTriggerTransitions())
            {
                return false;
            }

            bool success = false;

            if (graph.TryGetTransition(transitionID, out Transition transition))
            {
                if (this.graph.TryGetNode(transition.OriginID, out Node originNode))
                {
                    if (originNode is State state && state.ID == this.CurrentState)
                    {
                        success = true;
                    }
                    else if (originNode is AnyState anyState)
                    {
                        success = true;
                    }
                }
            }

            if (success)
            {
                Trigger(transition);
            }

            return success;
        }

        /// <summary>
        /// Triggers the transition with the given id, if it exists and if its origin state is the current state or an any state
        /// </summary>
        /// <param name="transitionID"></param>
        public void Trigger(string transitionID)
        {
            // When the state machine is paused, all transitions will be ignored
            if (IsPaused)
            {
                return;
            }

            if (TryTrigger(transitionID) == false)
            {
                if (graph.TryGetTransition(transitionID, out Transition transition))
                {
                    if (transition.OriginID != CurrentState)
                    {
                        Debug.LogWarningFormat("Failed to trigger transition with id {0}, because the current state is not its origin", transitionID);
                    }
                }
                else
                {
                    Debug.LogWarningFormat("Failed to trigger transition with id {0}, because no transition with this id exists", transitionID);
                }
            }
        }

        string loopOrigin = string.Empty;

        /// <summary>
        /// Returns true if transitions can be triggered, false if not (e.g. because the state machine is paused or currently in a transition with transition mode being set to 'locked')
        /// </summary>
        /// <returns></returns>
        bool CanTriggerTransitions()
        {
            // When the state machine is paused, all transitions will be ignored
            if (IsPaused)
            {
                return false;
            }

            // If a (timed) transition is currently being executed and the transition mode is set to 'Locked', triggering another transition will be ignored
            if(StateMachineManager.Instance.Configuration.TransitionMode == TransitionMode.Locked)
            {
                if(transitionHandler.IsRunning)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Triggers the given transition
        /// </summary>
        /// <param name="transition"></param>
        public void Trigger(Transition transition)
        {
            // Cancel if triggering a transition is currently not possible
            if (!CanTriggerTransitions())
            {
                return;
            }

            if (transition == null)
            {
                Debug.LogWarningFormat("Failed to trigger the given transition, because it is null");
                return;
            }

            if (transition != null && graph.HasTransition(transition.ID) == false)
            {
                Debug.LogWarningFormat("Failed to trigger transition with name {0}, because no transition with this id exists", transition.ID);
                return;
            }

            // Make sure the transition is not in an infinite call loop.
            // This can happen for transitions between states, which are called instantly and from OnEnterState, e.g.
            // StateA->StateB-StateC->StateA
            // If each of these states uses OnEnterState to trigger the next, this would cause an infite loop of method calls...
            if (loopOrigin == string.Empty)
            {
                loopOrigin = transition.OriginID;
            }
            else if (transition.OriginID == loopOrigin)
            {
                Debug.LogWarning("Stopped executing transition '" + transition.ID + "' due to a transition loop outgoing from state '" + loopOrigin + "'. Make sure you have no circular transitions triggered by OnEnterState, e.g. StateA->StateB-StateC->StateA", this);
                return;
            }

            OnTriggerTransition?.Invoke(transition);

            // Execute the transition
            transitionHandler.Execute(transition);

            loopOrigin = string.Empty;
        }

        /// <summary>
        /// Tries to exit the origin state of the given transition and returns true on success, false otherwise
        /// </summary>
        /// <param name="transition"></param>
        /// <returns></returns>
        private bool TryExitTransitionOriginState(Transition transition)
        {
            if (this.graph.TryGetNode(transition.OriginID, out Node originNode))
            {
                if (originNode is State state && state.ID == this.CurrentState)
                {
                    OnExitState?.Invoke(state);
                    state.OnExitState.Invoke();
                }
                else if (originNode is AnyState anyState)
                {
                    if (this.graph.TryGetNode(this.CurrentState, out Node activeState))
                    {
                        if (activeState is State)
                        {
                            OnExitState?.Invoke((State)activeState);
                            ((State)activeState).OnExitState.Invoke();
                        }
                    }
                }
                else
                {
                    Debug.LogErrorFormat("Failed to trigger transition with name {0}, because its origin is not the current state", transition.ID, this);

                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to enter the target state of the given transition and returns true on success, false otherwise.
        /// </summary>
        /// <param name="transition"></param>
        /// <returns></returns>
        private bool TryEnterTransitionTargetState(Transition transition)
        {
            if (this.graph.TryGetNode(transition.TargetID, out Node targetNode))
            {
                if (targetNode is State state)
                {
                    this.PreviousState = this.CurrentState;
                    this.CurrentState = state.ID;

                    OnEnterState?.Invoke(state);
                    state.OnEnterState.Invoke();

                    return true;
                }
            }

            return false;
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        class TransitionHandler
        {
            readonly StateMachine stateMachine;
            readonly TransitionTimer timer;

            // The transition being executed
            Transition transition;

            public bool IsRunning { get; private set; } = false;

            public TransitionHandler(StateMachine stateMachine)
            {
                this.stateMachine = stateMachine;

                timer = new TransitionTimer();
            }

            /// <summary>
            /// Executes the given transition
            /// </summary>
            /// <param name="transition"></param>
            public void Execute(Transition transition)
            {
                this.transition = transition;

                // If the transition has a duration it will not be completed after a given amount of time
                if (transition.Duration > 0.0f)
                {
                    IsRunning = true;
                    timer.Start(transition.Duration, transition.TimeMode);
                    StartTransition();
                }
                // Otherwise it will be executed instantly
                else
                {
                    IsRunning = false;
                    StartTransition();
                    CompleteTransition();
                }
            }

            public void Update()
            {
                if (IsRunning == false)
                {
                    return;
                }

                timer.Update();

                if (timer.IsCompleted)
                {
                    CompleteTransition();
                    IsRunning = false;
                }
            }

            public void Stop()
            {
                IsRunning = false;
            }

            void StartTransition()
            {
                stateMachine.TryExitTransitionOriginState(transition);
                stateMachine.OnEnterTransition?.Invoke(transition);
                transition.OnEnterTransition.Invoke();
            }

            void CompleteTransition()
            {
                stateMachine.OnExitTransition?.Invoke(transition);
                transition.OnExitTransition.Invoke();
                stateMachine.TryEnterTransitionTargetState(transition);
            }
        }

        class TransitionTimer
        {
            TimeMode timeMode;
            float elapsed = 0.0f;
            float duration = 0.0f;

            public bool IsCompleted = false;

            public void Start(float duration, TimeMode timeMode)
            {
                this.timeMode = timeMode;
                this.duration = duration;
                elapsed = 0.0f;
                IsCompleted = false;
            }

            public void Update()
            {
                if(IsCompleted)
                {
                    return;
                }

                elapsed += (timeMode == TimeMode.Scaled) ? Time.deltaTime : Time.unscaledDeltaTime;

                if (elapsed >= duration)
                {
                    elapsed = duration;
                    IsCompleted = true;
                }
            }
        }

        #region Obsolete

        /// <summary>
        /// Returns true if the graph contains a state with the given id, false otherwise
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Graph.HasState instead.")]
        public bool HasState(string id) => graph.HasState(id);

        /// <summary>
        /// return true if the graph contains a transition with the given id, false otherwise
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Graph.HasTransition instead.")]
        public bool HasTransition(string id) => graph.HasTransition(id);

        /// <summary>
        /// Returns the OnEnterState event of the state with the given name, null if the state does not exist 
        /// </summary>
        /// <param name="stateName"></param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Graph.GetState instead.")]
        public UnityEvent GetOnEnterStateEvent(string stateName)
        {
            if (graph.TryGetNode(stateName, out Node node))
            {
                if (node is State state)
                {
                    return state.OnEnterState;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the OnExitState event of the state with the given name, null if the state does not exist 
        /// </summary>
        /// <param name="stateName"></param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Graph.GetState instead.")]
        public UnityEvent GetOnExitStateEvent(string stateName)
        {
            if (graph.TryGetNode(stateName, out Node node))
            {
                if (node is State state)
                {
                    return state.OnExitState;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the OnEnterTransition event of the transition with the given name, null if the transition does not exist 
        /// </summary>
        /// <param name="transitionName"></param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Graph.GetTransition instead.")]
        public UnityEvent GetOnEnterTransitionEvent(string transitionName)
        {
            if (graph.TryGetTransition(transitionName, out Transition transition))
            {
                return transition.OnEnterTransition;
            }

            return null;
        }

        /// <summary>
        /// Returns the OnExitTransition event of the transition with the given name, null if the transition does not exist 
        /// </summary>
        /// <param name="transitionName"></param>
        /// <returns></returns>
        [Obsolete("This method is obsolete. Call Graph.GetTransition instead.")]
        public UnityEvent GetOnExitTransitionEvent(string transitionName)
        {
            if (graph.TryGetTransition(transitionName, out Transition transition))
            {
                return transition.OnExitTransition;
            }

            return null;
        }
        #endregion
    }
}