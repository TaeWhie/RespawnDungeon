using UnityEngine;

namespace Ilumisoft.GameActionSystem.Demo
{
    [RequireComponent(typeof(CharacterController))]
    public class DemoCharacterController : MonoBehaviour
    {
        public float rotationSpeed = 10.0f;
        public float moveSpeed = 5;

        public bool CanControl { get; set; } = true;

        CharacterController CharacterController { get; set; }

        void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
        }

        void Update()
        {
            if(!CanControl)
            {
                return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 movement = moveSpeed * Time.deltaTime * vertical * transform.forward;
            Vector3 rotation = horizontal * rotationSpeed * Time.deltaTime * Vector3.up;

            movement.y = Physics.gravity.y*Time.deltaTime;

            CharacterController.Move(movement);
            transform.Rotate(rotation);
        }
    }
}