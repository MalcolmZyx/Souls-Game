using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    public float damage = 15f;
    public GameObject visualEffectSlot;

    private ParticleSystem _cachedPS;

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        if (visualEffectSlot != null)
        {
            _cachedPS = visualEffectSlot.GetComponent<ParticleSystem>();
            if (_cachedPS == null) _cachedPS = visualEffectSlot.GetComponentInChildren<ParticleSystem>();
        }
    }

    private void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 1.2f);
        foreach (Collider hit in hits)
        {
            // Skip non-player triggers
            if (hit.isTrigger && !hit.CompareTag("Player")) continue;
            
            // Skip self/boss
            if (hit.transform.root == transform.root) continue;
            if (hit.gameObject.name.Contains("Boss")) continue;

            bool isPlayer = hit.CompareTag("Player");
            Character.MovementController mc = null;

            if (!isPlayer)
            {
                mc = hit.GetComponentInParent<Character.MovementController>();
                if (mc == null) mc = hit.transform.root.GetComponentInChildren<Character.MovementController>();
                isPlayer = (mc != null);
            }
            else
            {
                mc = hit.GetComponentInParent<Character.MovementController>() 
                     ?? hit.transform.root.GetComponentInChildren<Character.MovementController>();
            }

            if (isPlayer)
            {
                // Try to deal damage
                IDamageable dmg = hit.GetComponent<IDamageable>()
                    ?? hit.GetComponentInParent<IDamageable>()
                    ?? hit.transform.root.GetComponentInChildren<IDamageable>();

                if (dmg == null && hit.attachedRigidbody != null) 
                    dmg = hit.attachedRigidbody.GetComponent<IDamageable>();

                if (dmg != null)
                {
                    dmg.TakeDamage(damage, transform.position);
                    
                    if (mc != null)
                    {
                        // Heavy Camera Shake on impact
                        if (CinemachineShake.Instance != null)
                            CinemachineShake.Instance.Shake(0.6f, 0.25f);
                        else if (CameraShakeManager.Instance != null)
                            CameraShakeManager.Instance.Shake(0.6f, 0.25f);
                    }
                    Debug.Log($"[Boss Projectile] Hit Player! Dealt {damage} damage and applied heavy shake.");
                }

                // ALWAYS destroy if we hit any part of the player
                CleanupAndDestroy();
                return;
            }
        }

        // 2. ENVIRONMENT COLLISION: Destroy on walls/floors
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit envHit, 0.8f))
        {
            if (!envHit.collider.isTrigger && !envHit.collider.CompareTag("Player"))
            {
                CleanupAndDestroy();
            }
        }
    }

    private void CleanupAndDestroy()
    {
        // 1. Hide the projectile mesh immediately
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;

        // 2. Handle VFX
        if (_cachedPS != null)
        {
            _cachedPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _cachedPS.transform.SetParent(null);
            Destroy(_cachedPS.gameObject, 2f);
        }
        else
        {
            ParticleSystem[] psList = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ps.transform.SetParent(null);
                Destroy(ps.gameObject, 2f);
            }
        }

        // 3. Final removal
        Destroy(gameObject);
    }
}
