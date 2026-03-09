using System.Linq;
using UnityEngine;
using Assets.PixelFantasy.PixelHeroes2.Scripts.CharacterScripts;

namespace Assets.PixelFantasy.PixelHeroes2.Scripts.ExampleScripts
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CharacterAnimation))]
    public class CharacterController2D : MonoBehaviour
    {
        public Vector2 Input;
        public bool IsGrounded;

        public float Acceleration;
        public float MaxSpeed;
        public float JumpForce;
        public float Gravity;

        private Collider2D _collider;
        private Rigidbody2D _rigidbody;
        private CharacterAnimation _animation;

        private bool _jump;
        private bool _crouch;

        public void Start()
        {
            _collider = GetComponent<Collider2D>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _animation = GetComponent<CharacterAnimation>();
        }

        //public void FixedUpdate()
        //{
        //    var state = _animation.GetState();
        //    if (state == CharacterState.Die) return;
        //    var velocity = _rigidbody.linearVelocity;
        //    ...
        //}
        //private void Turn(float direction) { ... }
        //public void OnCollisionEnter2D ... OnCollisionExit2D ...
    }
}