using System.Collections;
using UnityEngine;

/// <summary>
/// Manages camera shake effects for impactful moments.
/// Attach to any GameObject, or call statically via singleton pattern.
/// </summary>
public class CameraShakeManager : MonoBehaviour
{
    private static CameraShakeManager instance;
    public static CameraShakeManager Instance => instance;
    
    private Character.PlayerController playerController;
    private Coroutine activeShakeCoroutine;

    [Header("Default Shake Settings")]
    [SerializeField] private float defaultMagnitude = 0.3f;
    [SerializeField] private float defaultDuration = 0.2f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (playerController == null)
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                playerController = playerGO.GetComponent<Character.PlayerController>();
        }
    }

    /// <summary>
    /// Play a camera shake effect. Sets the offset on the PlayerController to avoid conflicts.
    /// </summary>
    public void Shake(float magnitude = -1f, float duration = -1f)
    {
        magnitude = magnitude > 0 ? magnitude : defaultMagnitude;
        duration = duration > 0 ? duration : defaultDuration;

        // NEW: Check for CinemachineShake first
        if (CinemachineShake.Instance != null)
        {
            CinemachineShake.Instance.Shake(magnitude, duration);
            return;
        }

        FindPlayer();
        if (playerController == null)
        {
            Debug.LogWarning("[CameraShakeManager] No PlayerController or CinemachineShake found!");
            return;
        }

        Debug.Log($"[CameraShakeManager] Starting shake: magnitude={magnitude}, duration={duration}");

        if (activeShakeCoroutine != null)
            StopCoroutine(activeShakeCoroutine);

        activeShakeCoroutine = StartCoroutine(ShakeCoroutine(magnitude, duration));
    }

    public static void ShakeCamera(float magnitude = 0.3f, float duration = 0.2f)
    {
        if (instance != null)
            instance.Shake(magnitude, duration);
    }

    private IEnumerator ShakeCoroutine(float magnitude, float duration)
    {
        // This is now handled by the CinemachineShake component on the camera
        if (CinemachineShake.Instance != null)
        {
            CinemachineShake.Instance.Shake(magnitude, duration);
        }
        yield break;
    }

    public void StopShake()
    {
        if (activeShakeCoroutine != null)
        {
            StopCoroutine(activeShakeCoroutine);
            activeShakeCoroutine = null;
        }

        if (CinemachineShake.Instance != null)
            CinemachineShake.Instance.StopShake();
    }
}
