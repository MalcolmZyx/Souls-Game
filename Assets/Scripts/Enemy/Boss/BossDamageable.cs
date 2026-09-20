using System;
using UnityEngine;

public class BossDamageable : MonoBehaviour, IDamageable
{
    public float maxHealth = 200f; 

    public float currentHealth;

    [SerializeField] private GameObject bloodEffectPrefab;

    [Header("Debug")]
    [Tooltip("Tick in Play Mode to instantly kill the boss and fire OnBossDeath.")]
    [SerializeField] private bool debugKillBoss = false;

    private float lastDamageTime = -1f;

    public event Action OnBossDeath;
    public event Action OnTakeDamage;
    public event Action<float, float> OnHealthChanged;
    
    /// <summary>
    /// Event fired when damage is taken. 
    /// Parameters: (int damageAmount, float hitStunForce)
    /// </summary>
    public event Action<int, float> OnDamageTaken;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (debugKillBoss)
        {
            debugKillBoss = false;
            Debug.Log("[BossDamageable] debugKillBoss triggered — calling Die().");
            Die();
        }
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (IsDead()) return;

        // I-frame check (short, snappier for fast swing registration)
        if (Time.time - lastDamageTime < 0.05f) return;
        lastDamageTime = Time.time;

        currentHealth -= damage;

        if (bloodEffectPrefab != null)
            Instantiate(bloodEffectPrefab, hitPoint, Quaternion.identity);

        
        
        Debug.Log($"[Boss Health] Took {damage}. HP: {currentHealth}");

        OnTakeDamage?.Invoke();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        // Invoke the new event for the BossDamageHandler to pick up
        // We cast damage to int and provide a default hitStunForce of 1.0f 
        OnDamageTaken?.Invoke((int)damage, 1.0f);

        if (currentHealth <= 0)
            Die();
    }

    public bool IsDead()
    {
        return currentHealth <= 0;
    }

    private void Die()
    {
        Debug.Log("[Boss Health] Boss defeated!");
        OnBossDeath?.Invoke();
    }
}