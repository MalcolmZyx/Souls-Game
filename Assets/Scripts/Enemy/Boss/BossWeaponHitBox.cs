using UnityEngine;
using System.Collections.Generic;

// Attach this to the Boss's Weapon GameObject
public class BossWeaponHitbox : MonoBehaviour
{
    public float damageAmount = 15f;

    [Tooltip("The trigger collider to enable/disable for this hitbox. Drag the specific collider here — do not rely on GetComponent.")]
    [SerializeField] private Collider hitboxCollider;

    private bool hasHitThisFrame = false;
    private HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    void Awake()
    {
        if (hitboxCollider == null)
        {
            Debug.LogError($"[BossWeaponHitbox] No hitbox collider assigned on '{name}'! " +
                           "Drag the specific weapon collider into the Hitbox Collider field.", this);
            return;
        }
        hitboxCollider.enabled = false;
    }

    public void EnableHitbox()
    {
        if (hitboxCollider == null) return;
        hitTargets.Clear();
        hitboxCollider.enabled = true;
    }

    public void DisableHitbox()
    {
        if (hitboxCollider != null) hitboxCollider.enabled = false;
    }

    void LateUpdate()
    {
        hasHitThisFrame = false;
    }

    public bool HasHitThisFrame() => hasHitThisFrame;

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.root == transform.root) return;
        if (other.isTrigger && !other.CompareTag("Player")) return;

        bool isPlayer = other.CompareTag("Player");
        if (!isPlayer)
        {
            var mcCheck = other.GetComponentInParent<Character.MovementController>();
            if (mcCheck == null) mcCheck = other.transform.root.GetComponentInChildren<Character.MovementController>();
            isPlayer = (mcCheck != null);
        }

        // Only set hasHitThisFrame for actual player hits — moved inside the player check
        // since "hit something" should mean "hit a valid target", not "any trigger fired".
        if (!isPlayer) return;
        hasHitThisFrame = true;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null && other.attachedRigidbody != null) damageable = other.attachedRigidbody.GetComponent<IDamageable>();
        if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) damageable = other.transform.root.GetComponentInChildren<IDamageable>();

        if (damageable != null && !hitTargets.Contains(damageable))
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            damageable.TakeDamage(damageAmount, hitPoint);
            hitTargets.Add(damageable);

            Character.MovementController mc = other.GetComponentInParent<Character.MovementController>();
            if (mc == null) mc = other.transform.root.GetComponentInChildren<Character.MovementController>();
            if (mc == null) mc = Object.FindFirstObjectByType<Character.MovementController>();
            if (mc != null)
            {
                Vector3 pushDir = other.transform.position - transform.position;
                pushDir.y = 0f;
                pushDir = pushDir.normalized;
                mc.ApplyKnockback(pushDir * 87f, 0.8f);
            }

            Debug.Log($"[Boss Combat] Dealt {damageAmount} damage + knockback");
        }
    }
}