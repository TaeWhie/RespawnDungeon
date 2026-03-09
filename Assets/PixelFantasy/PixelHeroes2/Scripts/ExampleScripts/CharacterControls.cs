using Assets.PixelFantasy.Common.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.PixelFantasy.PixelHeroes2.Scripts.ExampleScripts
{
    [RequireComponent(typeof(Creature))]
    [RequireComponent(typeof(CharacterController2D))]
    [RequireComponent(typeof(CharacterAnimation))]
    public class CharacterControls : MonoBehaviour
    {
        private Creature _character;
        private CharacterController2D _controller;
        private CharacterAnimation _animation;

        public void Start()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<CharacterController2D>();
            _animation = GetComponent<CharacterAnimation>();
        }

        public void Update()
        {
            var keyboard = Keyboard.current;

            Move();
            Attack();

            if (keyboard == null) return;

            if (keyboard.iKey.wasPressedThisFrame) _animation.Idle();
            if (keyboard.wKey.wasPressedThisFrame) _animation.Walk();
            if (keyboard.rKey.wasPressedThisFrame) _animation.Run();
            if (keyboard.dKey.wasPressedThisFrame) _animation.Die();
            if (keyboard.lKey.wasReleasedThisFrame) EffectManager.Instance.Blink(_character);
        }

        private void Move()
        {
            _controller.Input = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.isPressed)
                _controller.Input.x = -1;
            else if (keyboard.rightArrowKey.isPressed)
                _controller.Input.x = 1;

            if (keyboard.upArrowKey.isPressed)
                _controller.Input.y = 1;
            else if (keyboard.downArrowKey.isPressed)
                _controller.Input.y = -1;
        }

        private void Attack()
        {
            if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
                _animation.Slash();
        }
    }
}