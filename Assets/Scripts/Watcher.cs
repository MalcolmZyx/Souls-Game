using UnityEngine;

public class Watcher : MonoBehaviour
{
    [SerializeField] private Collider target;
    private bool lastState;

    void Awake()
    {
        if (target == null) target = GetComponent<Collider>();
        if (target != null) lastState = target.enabled;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (target.enabled != lastState)
        {
            // Stack trace is logged automatically by Debug.LogWarning in the editor.
            Debug.LogWarning(
                $"[ColliderWatcher] {target.GetType().Name} on {name} changed: {lastState} → {target.enabled}",
                this);
            lastState = target.enabled;
        }
    }
}