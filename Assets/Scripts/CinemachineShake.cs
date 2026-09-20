using UnityEngine;

/// <summary>
/// A simple, direct shaker that adds jitter to the camera.
/// Attach this script to your Main Camera.
/// </summary>
public class CinemachineShake : MonoBehaviour
{
    public static CinemachineShake Instance { get; private set; }

    private Vector3 currentOffset;
    private Quaternion currentRotation = Quaternion.identity;
    private Coroutine activeShakeCoroutine;

    void Awake()
    {
        Instance = this;
    }

    public void Shake(float magnitude, float duration)
    {
        if (activeShakeCoroutine != null)
            StopCoroutine(activeShakeCoroutine);
            
        activeShakeCoroutine = StartCoroutine(ShakeCoroutine(magnitude, duration));
    }

    private System.Collections.IEnumerator ShakeCoroutine(float magnitude, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = 1f - (elapsed / duration);
            float currentMag = magnitude * progress;

            // Generate the shake values
            currentOffset = new Vector3(
                Random.Range(-currentMag, currentMag),
                Random.Range(-currentMag, currentMag),
                Random.Range(-currentMag, currentMag) * 0.5f
            );

            float rotMag = currentMag * 5f;
            currentRotation = Quaternion.Euler(
                Random.Range(-rotMag, rotMag),
                Random.Range(-rotMag, rotMag),
                Random.Range(-rotMag, rotMag)
            );

            yield return null;
        }

        currentOffset = Vector3.zero;
        currentRotation = Quaternion.identity;
        activeShakeCoroutine = null;
    }

    public void StopShake()
    {
        if (activeShakeCoroutine != null)
            StopCoroutine(activeShakeCoroutine);
            
        currentOffset = Vector3.zero;
        currentRotation = Quaternion.identity;
        activeShakeCoroutine = null;
    }

    // We apply the shake at the very end of the frame.
    // By adding it to the position after all other logic has run, 
    // it effectively "layers" on top of the existing camera movement.
    void LateUpdate()
    {
        transform.position += currentOffset;
        transform.rotation *= currentRotation;
    }
}
