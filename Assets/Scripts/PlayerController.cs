using UnityEngine;
using UnityEngine.InputSystem;

namespace Character
{
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(MovementController))]
    [RequireComponent(typeof(CombatController))]
    public class PlayerController : MonoBehaviour
    {
       
        private MovementController moveController;
        private CombatController combatController;

        [SerializeField] private PlayerVitals vitals;
        private Camera mainCamera;

        [SerializeField] private Animator animator;
        [SerializeField] private float dodgeSpeed = 5f;
        
        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;
        public Transform CameraTransform => cameraTransform;
        [SerializeField] private float cameraDistance = 5f;
        [SerializeField] private float cameraHeight = 2f;
        [SerializeField] private float cameraSensitivity = 100f;
        [SerializeField] private float rotationSmoothing = 10f;
        private float cameraYaw;
        private float cameraPitch;

        [SerializeField] private GameObject cameraTargetLocator;

        [Header("Targeting")]
        [SerializeField] private GameObject lookAtLocator;

        [Header("Player")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private Rigidbody rb; 
        [SerializeField] private GameObject deathHeightLocator;
        
        [Header("Combat Timing")]
        [SerializeField] private float heavyAttackThreshold = 0.4f;
        private float attackButtonTime;
        private bool isHoldingAttack;
        private bool heavyTriggered;
         private bool isAttacking;
        private bool isDodging;
        private float currentSpeed;
        
        [SerializeField] private bool _isResting;
        public bool isResting
        {
            get => _isResting;
            set
            {
                _isResting = value;
                if (moveController != null) moveController.isResting = value;
            }
        }
        
        public void OnMove(InputValue inputValue)
        {
            if (isResting) return;
            _rawMoveInput = inputValue.Get<Vector2>();
        }

        private Vector2 _rawMoveInput;

        public void OnSprint(InputValue value)
        {
            moveController.SetSprinting(value.isPressed);
        }

        public void OnDodge()
        {
            if (combatController.IsAttacking) return;
            if (moveController.IsDodging) return;
            if (!vitals.TryUseStamina(20f)) return;

            if (_rawMoveInput.sqrMagnitude > 0.1f)
            {
                Vector3 camForward = mainCamera.transform.forward;
                Vector3 camRight = mainCamera.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 dodgeDir = camForward * _rawMoveInput.y + camRight * _rawMoveInput.x;
                transform.rotation = Quaternion.LookRotation(dodgeDir);
            }

            moveController.TryDodge(); // guaranteed to succeed given the IsDodging check above
            animator.SetTrigger("Dodge");
            isDodging = true;
            
            PlayerDamageable damageable = GetComponent<PlayerDamageable>();
            if (damageable != null)
            {
                damageable.Iframes(0.5f); // 0.5 seconds of invincibility during dodge
            }
        }


        public void OnLock(InputValue inputValue)
        {
            lookAtLocator.GetComponent<LockOnTarget>().LockOn();
        }


        public void OnLightAttack(InputValue inputValue)
        {
            if (isDodging) return;
            if (combatController.IsAttacking) return;
            if (!vitals.TryUseStamina(10f)) return;
            combatController.LightAttack();
        }

        public void OnHeavyAttack(InputValue inputValue)
        {
            if (isDodging) return;
            if (combatController.IsAttacking) return;
            if (!vitals.TryUseStamina(20f)) return;
            combatController.HeavyAttack();
        }

      
        public void EndAttack()
        {
            isAttacking = false;
        }

  
        public void EndDodge()
        {
            moveController.EndDodge();
            isDodging = false;
        }


        void Start()
        {
            moveController = GetComponent<MovementController>();
            combatController = GetComponent<CombatController>();
            mainCamera = Camera.main;
            cameraYaw = cameraTransform.eulerAngles.y;
            rb.WakeUp();
        }

        void Update()
        {
            if (isResting) return;
            HandleCameraRelativeMovement();
           // HandleHeavyAttackHold();
            
            // Check if player has fallen below death height
            if (transform.position.y < deathHeightLocator.transform.position.y)
            {
                vitals.Kill();
                
            }
        }

        void LateUpdate()
        {   
            OrbitCamera();
        }


        public void OnAnimatorMove()
        {
            if (isResting) return;
            if (moveController.IsDodging)
            {
                Vector3 move = transform.forward * dodgeSpeed * Time.deltaTime;
                move.y = 0f;
                rb.MovePosition(rb.position + move);
            }
            else
            {
                // Strip vertical drift from root motion position
                Vector3 delta = animator.deltaPosition;
                delta.y = 0f;
                rb.MovePosition(rb.position + delta);

                // ✅ Rotate toward movement direction instead of using animation delta
                Vector3 flatDirection = new Vector3(moveController.MoveInput.x, 0f, moveController.MoveInput.z);
                if (flatDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(flatDirection);
                    rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.deltaTime * 15f));
                }
            }
        }


        private void HandleCameraRelativeMovement()
        {
            if (moveController.IsDodging) return;

            // Also read right-stick for camera (gamepad)
            if (Gamepad.current != null)
            {
                Vector2 look = Gamepad.current.rightStick.ReadValue();
                if (look.sqrMagnitude > 0.01f)
                {
                    cameraYaw += look.x * cameraSensitivity * Time.deltaTime;
                    cameraPitch -= look.y * cameraSensitivity * Time.deltaTime;
                    cameraPitch = Mathf.Clamp(cameraPitch, -30f, 60f);
                }
            }

            Vector2 move = _rawMoveInput;

            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camForward * move.y + camRight * move.x;

            if (moveDir.sqrMagnitude > 0.01f)
            {
                // Rotate player toward move direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation,
                    rotationSmoothing * Time.deltaTime);

                moveController.Move(moveDir.normalized * moveSpeed);

                // Animator blend (0.5 walk, 1.0 sprint)
                float target = moveController.IsSprinting ? 1f : 0.5f;
                currentSpeed = Mathf.Lerp(currentSpeed, target, 10f * Time.deltaTime);
            }
            else
            {
                moveController.Move(Vector3.zero);
                currentSpeed = Mathf.Lerp(currentSpeed, 0f, 10f * Time.deltaTime);
            }

            animator.SetFloat("Speed", currentSpeed);
        }

        // private void HandleHeavyAttackHold()
        // {
        //     if (!isHoldingAttack || heavyTriggered || isAttacking) return;
        //
        //     if (Time.time - attackButtonTime >= heavyAttackThreshold)
        //     {
        //         heavyTriggered = true;
        //         isAttacking = true;
        //         combatController.HeavyAttack();
        //     }
        // }

        private void OrbitCamera()
        {
            Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -cameraDistance);

            cameraTransform.position = transform.position + Vector3.up * cameraHeight + offset;
            cameraTransform.LookAt(transform.position + Vector3.up * cameraHeight);
        }
        
        public void OnInteract(InputValue inputValue)
        {
                Collider[] hits = Physics.OverlapSphere(transform.position, 2f);
                foreach (Collider hit in hits)
                {   
                    if (hit.CompareTag("Interactable"))
                    {
                        IInteractable interactable = hit.GetComponent<IInteractable>();
                        interactable?.Interact(transform);
                        break;
                    }
                }
        }   

        public void OnHeal(InputValue inputValue)
        {
            vitals.Heal(100f);
        }
    }
}
