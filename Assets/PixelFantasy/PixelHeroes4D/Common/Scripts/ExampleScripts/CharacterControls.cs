using System.Collections;
using Assets.PixelFantasy.PixelHeroes4D.Common.Scripts.CharacterScripts;
using UnityEngine;

namespace Assets.PixelFantasy.PixelHeroes4D.Common.Scripts.ExampleScripts
{
    public class CharacterControls : MonoBehaviour
    {
        public SpriteRenderer SpriteRenderer;
        public Animator Animator;
        public Vector2 MoveSpeed = new(5, 3);

        private CharacterState _idle = CharacterState.Idle;
        private Vector3 _movement;

        public void Update()
        {
            var speed = Input.GetKey(KeyCode.LeftControl) ? MoveSpeed.x : MoveSpeed.y;
            var jumpState = Animator.GetInteger("JumpState");

            if (Input.GetKey(KeyCode.LeftArrow))
            {
                SpriteRenderer.flipX = true;
                Animator.SetInteger("Direction", 1);
                _movement = new Vector3(-speed, 0);
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                SpriteRenderer.flipX = false;
                Animator.SetInteger("Direction", 1);
                _movement = new Vector3(speed, 0);
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                SpriteRenderer.flipX = false;
                Animator.SetInteger("Direction", 0);
                _movement = new Vector3(0, -speed);
            }
            else if (Input.GetKey(KeyCode.UpArrow))
            {
                SpriteRenderer.flipX = false;
                Animator.SetInteger("Direction", 2);
                _movement = new Vector3(0, speed);
            }
            else if (jumpState == 0)
            {
                Animator.SetInteger("State", (int) _idle);
                _movement = Vector3.zero;
            }

            switch (jumpState)
            {
                case 1: _movement = new Vector3(0, 10); break;
                case 2: _movement = new Vector3(0, -10); break;
                case 3: _movement = Vector3.zero; break;
            }
            
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.UpArrow))
            {
                Animator.SetInteger("State", Input.GetKey(KeyCode.LeftControl) ? (int) CharacterState.Run : (int) CharacterState.Walk);
            }

            if (Input.GetKey(KeyCode.U))
            {
                _idle = CharacterState.Climb;
            }
            else if (Input.GetKeyDown(KeyCode.W) && Input.GetKey(KeyCode.LeftControl))
            {
                _idle = CharacterState.Wink;
            }
            else if (Input.GetKeyDown(KeyCode.Space) && jumpState == 0)
            {
                StartCoroutine("Jump");
                return;
            }
            else if (Input.GetKeyDown(KeyCode.I))
            {
                _idle = CharacterState.Idle;
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                _idle = CharacterState.Walk;
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                _idle = CharacterState.Ready;
            }
            else if (Input.GetKeyDown(KeyCode.B))
            {
                _idle = CharacterState.Block;
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                _idle = CharacterState.Die;
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                Animator.SetTrigger("Shot");
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                Animator.SetTrigger("Slash");
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                Animator.SetTrigger("Jab");
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                Animator.SetTrigger("Cast");
            }
        }

        public void FixedUpdate()
        {
            transform.position += _movement * Time.fixedDeltaTime;
        }

        private IEnumerator Jump()
        {
            Animator.SetInteger("State", (int) CharacterState.Jump);
            Animator.SetInteger("JumpState", 1);

            var ground = transform.position;

            yield return new WaitForSeconds(0.25f);

            Animator.SetInteger("JumpState", 2);

            yield return new WaitForSeconds(0.25f);

            transform.position = ground;
            _movement = Vector3.zero;

            Animator.SetInteger("JumpState", 3);

            yield return new WaitForSeconds(0.10f);

            Animator.SetInteger("JumpState", 0);
        }
    }
}