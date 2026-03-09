using System.Collections.Generic;
using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [SelectionBase]
    [AddComponentMenu("Game Action System/Game Action Trigger", -100)]
    public class GameActionTrigger : MonoBehaviour
    {
        [Tooltip("If enabled, this Game Action Trigger can only be triggered once."), SerializeField]
        bool onlyOnce = false;

        [Tooltip("How much time must pass in seconds before the Game Action Trigger can be executed again."), SerializeField, Min(0)]
        float cooldown = 0.0f;

        [Tooltip("The list of Game Actions that will be executed, when the Game Action Trigger is triggered."), SerializeField]
        List<GameAction> gameActions = new();

        bool hasBeenTriggered = false;

        /// <summary>
        /// The last time the trigger has been triggered
        /// </summary>
        float lastTimeTriggered = 0.0f;

        /// <summary>
        /// Whether the trigger has been triggered or not
        /// </summary>
        public bool HasBeenTriggered => hasBeenTriggered;

        /// <summary>
        /// Gets or sets the cooldown of the Game Action Trigger
        /// </summary>
        public float Cooldown
        {
            get { return cooldown; }
            set 
            {
                // Make sure cooldown never gets smaller than 0
                cooldown = Mathf.Max(0, value); 
            }
        }

        /// <summary>
        /// Gets or sets whether the trigger can only be trigger once
        /// </summary>
        public bool OnlyOnce
        {
            get => onlyOnce;
            set => onlyOnce = value;
        }

        /// <summary>
        /// Gets the list of Game Actions, which will be executed by the trigger
        /// </summary>
        public List<GameAction> GameActions => gameActions;

        /// <summary>
        /// Makes the trigger execute all assigned game actions
        /// </summary>
        [ContextMenu("Trigger")]
        public void Trigger()
        {
            // Cancel if the trigger should only be triggered one and has already been triggered
            if(onlyOnce && hasBeenTriggered) 
            {
                return;
            }

            // Cancel if cooldown is not over yet
            if (onlyOnce == false && Time.time - lastTimeTriggered < Cooldown)
            {
                return;
            }

            // Execute all actions
            foreach (var action in gameActions)
            {
                action.Execute();
            }

            // Remember that the trigger has been triggered
            hasBeenTriggered = true;

            // Remember the time the trigger has been triggered
            lastTimeTriggered = Time.time;
        }
    }
}