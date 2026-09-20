using UnityEngine;

namespace Character
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovementController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] protected float acceleration = 20f;
        [SerializeField] protected float maxVelocity = 5f;
        [SerializeField] protected float sprintMultiplier = 1.6f;

        [Header("Dodge")]
        [SerializeField] private float dodgeDuration = 0.3f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundMask;

        // Runtime state
        protected Rigidbody rb;
        protected Vector3 moveInput;

        private bool isSprinting;
        private bool isDodging;
        private float dodgeTimer;

        // Knockback — just a lockout timer; the physics engine drives the actual motion
        private float knockbackTime = 0f;
        public bool isResting;
        public Vector3 MoveInput => moveInput;

        protected virtual void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        // --- Public API (called by PlayerController) ---

        public void Move(Vector3 worldDirection)
        {
            if (isDodging || knockbackTime > 0f) return;
            moveInput = worldDirection;
        }

        public void SetSprinting(bool sprinting)
        {
            isSprinting = sprinting;
        }

        public void Stop()
        {
            rb.linearVelocity = Vector3.zero;
            moveInput = Vector3.zero;
        }

        public bool TryDodge()
        {
            if (isDodging) return false;
            isDodging = true;
            dodgeTimer = dodgeDuration;
            return true;
        }

        public void EndDodge()
        {
            isDodging = false;
        }

        /// <summary>
        /// Apply a physics-based knockback. force = direction * magnitude (units = m/s, mass-independent).
        /// duration = how long the player loses movement control.
        /// </summary>
        public void ApplyKnockback(Vector3 force, float duration)
        {
            knockbackTime = duration;

            // ALWAYS zero the Y component of force here as a final safety net
            // (attack states should already do this, but belt-and-suspenders)
            force.y = 0f;

            // Only cancel horizontal velocity, NOT Y — so gravity keeps running uninterrupted
            Vector3 currentVel = rb.linearVelocity;
            currentVel.x = 0f;
            currentVel.z = 0f;
            rb.linearVelocity = currentVel;

            // Apply purely horizontal impulse — VelocityChange bypasses mass
            rb.AddForce(force, ForceMode.VelocityChange);

            Debug.Log($"[MovementController] ApplyKnockback: {force.magnitude:F1} m/s horizontal, lockout={duration}s");
        }

        // --- Queries ---

        public bool IsDodging => isDodging;
        public bool IsSprinting => isSprinting;
        public bool IsKnockedBack => knockbackTime > 0f;

        public virtual float GetHorizontalSpeedPercent()
        {
            return moveInput == Vector3.zero
                ? 0f
                : Mathf.Clamp01(rb.linearVelocity.magnitude / maxVelocity);
        }

        // --- Physics ---

        protected virtual void FixedUpdate()
        {
            if (isResting) return;
            // FORCE GROUNDING: If we are on the ground and not jumping, ensure we don't float UP.
            // This prevents "popping" or "floating" when colliding with the boss's slanted colliders.
            if (knockbackTime <= 0f)
            {
                // Check if we're on ground (feet level)
                Vector3 rayOrigin = rb.position + Vector3.up * 0.5f;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 0.7f, groundMask))
                {
                    // Ignore boss/enemies for grounding purposes
                    if (!hit.collider.CompareTag("Enemy") && !hit.collider.CompareTag("Boss"))
                    {
                        // If we have upward velocity, kill it. Gravity handles downward.
                        if (rb.linearVelocity.y > 0.01f)
                        {
                            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                        }
                    }
                }
            }

            if (knockbackTime > 0f)
            {
                // Tick the lockout timer
                knockbackTime -= Time.fixedDeltaTime;

                // Extra downward force to keep player grounded (3x normal gravity)
                rb.AddForce(Physics.gravity * 3f, ForceMode.Acceleration);

                // Moderate horizontal damping so player slides far but not forever
                Vector3 vel = rb.linearVelocity;
                float horizDamp = Mathf.Pow(0.92f, Time.fixedDeltaTime * 60f);
                vel.x *= horizDamp;
                vel.z *= horizDamp;
                rb.linearVelocity = vel;

                return;
            }

            if (isDodging) return;

            if (moveInput.sqrMagnitude < 0.01f)
            {
                // Damp horizontal velocity to a full stop when there's no input
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                return;
            }

            SimpleMovement();
        }

        private void SimpleMovement()
        {
            if (moveInput.sqrMagnitude < 0.01f) return;

            float speedScale = isSprinting ? sprintMultiplier : 1f;
            Vector3 movement = moveInput * (Time.fixedDeltaTime * acceleration * speedScale);

            // Project onto ground slope (ONLY if hitting real ground, not the boss)
            Vector3 rayOrigin = rb.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, groundMask))
            {
                if (!hit.collider.CompareTag("Enemy") && !hit.collider.CompareTag("Boss"))
                {
                    movement = Vector3.ProjectOnPlane(movement, hit.normal);
                }
                else
                {
                    // If we hit the boss, treat it as a wall (pure horizontal)
                    movement.y = 0f;
                }
            }
            else
            {
                movement.y = 0f;
            }

            rb.MovePosition(rb.position + movement);
        }

        private void Update()
        {
            if (isDodging)
            {
                dodgeTimer -= Time.deltaTime;
                if (dodgeTimer <= 0f)
                    isDodging = false;
            }
        }
    }
}
