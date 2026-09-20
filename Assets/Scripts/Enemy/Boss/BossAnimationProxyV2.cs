using UnityEngine;

/// <summary>
/// V2 Animation Proxy — catches animation events on child objects
/// and forwards them to BossBrainV2 on the parent.
/// </summary>
public class BossAnimationProxyV2 : MonoBehaviour
{
    private BossBrainV2 brain;

    void Awake()
    {
        brain = GetComponentInParent<BossBrainV2>();
    }

    public void OnFlap()          { if (brain != null) brain.OnFlap(); }
    public void OnSmash()         { if (brain != null) brain.OnSmash(); }
    public void OnFire()          { if (brain != null) brain.OnFire(); }
    public void Hit()             { if (brain != null) brain.Hit(); }
    public void Footstep()        { if (brain != null) brain.Footstep(); }
    public void OnActionComplete(){ if (brain != null) brain.OnActionComplete(); }
}
