using System;
using UnityEngine;
using Assets.PixelFantasy.Common.Scripts;
using Assets.PixelFantasy.PixelHeroes2.Scripts.CharacterScripts;

namespace Assets.PixelFantasy.PixelHeroes2.Scripts.ExampleScripts
{
    [RequireComponent(typeof(Creature))]
    public class CharacterAnimation : MonoBehaviour
    {
        private Creature _character;
        
        public void Start()
        {
            _character = GetComponent<Creature>();
            Idle();
        }

        public void Idle()
        {
            SetState(CharacterState.Idle);
        }

        public void Walk()
        {
            SetState(CharacterState.Walk);
        }

        public void Run()
        {
            SetState(CharacterState.Run);
        }

        public void Die()
        {
            SetState(CharacterState.Die);
        }

        public void Slash()
        {
            _character.Animator.SetTrigger("Slash");
        }

        public void SetState(CharacterState state)
        {
            foreach (var variable in new[] { "Idle", "Walk", "Run", "Die" })
            {
                _character.Animator.SetBool(variable, false);
            }

            switch (state)
            {
                case CharacterState.Idle: _character.Animator.SetBool("Idle", true); break;
                case CharacterState.Walk: _character.Animator.SetBool("Walk", true); break;
                case CharacterState.Run: _character.Animator.SetBool("Run", true); break;
                case CharacterState.Die: _character.Animator.SetBool("Die", true); break;
                default: throw new NotSupportedException(state.ToString());
            }

            //Debug.Log("SetState: " + state);
        }

        public CharacterState GetState()
        {
            if (_character.Animator.GetBool("Idle")) return CharacterState.Idle;
            if (_character.Animator.GetBool("Walk")) return CharacterState.Walk;
            if (_character.Animator.GetBool("Run")) return CharacterState.Run;
            if (_character.Animator.GetBool("Die")) return CharacterState.Die;

            return CharacterState.Idle;
        }

        public void SetBool(string paramName)
        {
            _character.Animator.SetBool(paramName, true);
        }

        public void UnsetBool(string paramName)
        {
            _character.Animator.SetBool(paramName, false);
        }
    }
}