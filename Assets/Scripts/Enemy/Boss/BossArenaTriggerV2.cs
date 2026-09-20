using UnityEngine;

/// <summary>
/// V2 Arena Trigger — identical behavior to BossArenaTrigger but references BossBrainV2.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossArenaTriggerV2 : MonoBehaviour
{
    private BossBrainV2 bossBrain;
    private BossController bossToControl;

    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private AudioClip bossMusicClip;
    
    [SerializeField]
    private AudioSource audioSourceBoss;
    
    void Start()
    {
        bossBrain = GetComponentInParent<BossBrainV2>();
        bossToControl = GetComponentInParent<BossController>();

        if (bossBrain == null)
            bossBrain = FindObjectOfType<BossBrainV2>();
        if (bossToControl == null)
            bossToControl = FindObjectOfType<BossController>();

        if (bossBrain == null)
            Debug.LogError("BossArenaTriggerV2: bossBrain not found!");
        if (bossToControl == null)
            Debug.LogError("BossArenaTriggerV2: bossToControl not found!");

        Collider arenaCollider = GetComponent<Collider>();
        if (arenaCollider == null) return;

        if (bossBrain == null) return;

        Bounds arenaBounds = arenaCollider.bounds;

        foreach (MonoBehaviour mb in FindObjectsOfType<MonoBehaviour>())
        {
            if (mb is IPlayerState player)
            {
                Vector3 playerPos = player.GetPosition();
                if (arenaBounds.Contains(playerPos))
                {
                    bossBrain.isPlayerInArena = true;
                    bossBrain.hasPlayerEverEnteredArena = true;

                    if (bossToControl != null)
                        bossToControl.WakeUp(player);

                    Debug.Log("BossArenaTriggerV2: Player inside arena at startup.");
                    break;
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        IPlayerState player = other.GetComponent<IPlayerState>();
        if (player != null && bossBrain != null)
        {
            bossBrain.isPlayerInArena = true;
            
            audioSource.Stop();
            audioSource.PlayOneShot(bossMusicClip);
            audioSourceBoss.enabled = true;
            
            if (!bossBrain.hasPlayerEverEnteredArena)
            {
                bossBrain.hasPlayerEverEnteredArena = true;
                bossToControl?.WakeUp(player);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        IPlayerState player = other.GetComponent<IPlayerState>();
        if (player != null && bossBrain != null)
        {
            bossBrain.isPlayerInArena = true;
            if (!bossBrain.hasPlayerEverEnteredArena)
            {
                bossBrain.hasPlayerEverEnteredArena = true;
                bossToControl?.WakeUp(player);
            }
            else if (bossToControl != null && !bossToControl.enabled)
            {
                bossToControl.WakeUp(player);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        IPlayerState player = other.GetComponent<IPlayerState>();
        if (player != null && bossBrain != null)
        {
            audioSource.Stop();
            audioSourceBoss.enabled = false;
            bossBrain.isPlayerInArena = false;
        }
    }
}
