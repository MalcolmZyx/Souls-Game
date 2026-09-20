using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized audio for the boss. Two ways things get played:
///   1. Animation events on clips call methods like OnStep / OnImpact / OnRoar directly.
///   2. Code-driven attacks (e.g. arena reposition leap) call methods through BossBrainV2.PlaySound(...).
///
/// Add new categories by adding a clip array, a volume range, and a public OnX() method.
/// Keep the OnX() naming so animation events can find them.
/// </summary>
public class BossAudio : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;          // one-shot SFX
    [SerializeField] private AudioSource loopAudioSource;      // dedicated looping source (for the in-air whoosh, etc.)
    [SerializeField] private BossBrainV2 brain;                // for ground-state gating; auto-found in Awake
    
    [Header("One-Shot Clips")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private AudioClip[] impactClips;
    [SerializeField] private AudioClip[] smashClips;
    [SerializeField] private AudioClip[] roarClips;
    [SerializeField] private AudioClip[] deathClips;
    [SerializeField] private AudioClip[] projectileClips;
    [SerializeField] private AudioClip[] wingFlapClips;
    [SerializeField] private AudioClip[] dashWindupClips;
    [SerializeField] private AudioClip[] dashReleaseClips;
    [SerializeField] private AudioClip[] wavePushbackClips;
    [SerializeField] private AudioClip[] chargeUpClips;        // ranged / precise projectile windup
    [SerializeField] private AudioClip[] hurtClips;            // boss takes damage
    [SerializeField] private AudioClip[] phaseTransitionClips;
    [SerializeField] private AudioClip[] leapTakeoffClips;
    [SerializeField] private AudioClip[] leapLandClips;
    
    [Header("Loop Clips")]
    [SerializeField] private AudioClip leapInAirLoopClip;
    
    [Header("One-Shot Volume Ranges")]
    [SerializeField] public Vector2 footstepVolumeRange = new Vector2(0.05f, 0.1f);
    [SerializeField] public Vector2 impactVolumeRange = new Vector2(0.1f, 0.2f);
    [SerializeField] public Vector2 smashVolumeRange = new Vector2(0.2f, 0.3f);
    [SerializeField] public Vector2 roarVolumeRange = new Vector2(0.3f, 0.5f);
    [SerializeField] public Vector2 deathVolumeRange = new Vector2(0.3f, 0.5f);
    [SerializeField] public Vector2 projectileVolumeRange = new Vector2(0.1f, 0.2f);
    [SerializeField] public Vector2 wingFlapVolumeRange = new Vector2(0.2f, 0.35f);
    [SerializeField] public Vector2 dashWindupVolumeRange = new Vector2(0.15f, 0.25f);
    [SerializeField] public Vector2 dashReleaseVolumeRange = new Vector2(0.25f, 0.4f);
    [SerializeField] public Vector2 wavePushbackVolumeRange = new Vector2(0.25f, 0.4f);
    [SerializeField] public Vector2 chargeUpVolumeRange = new Vector2(0.15f, 0.25f);
    [SerializeField] public Vector2 hurtVolumeRange = new Vector2(0.2f, 0.35f);
    [SerializeField] public Vector2 phaseTransitionVolumeRange = new Vector2(0.4f, 0.6f);
    [SerializeField] public Vector2 leapTakeoffVolumeRange = new Vector2(0.3f, 0.45f);
    [SerializeField] public Vector2 leapLandVolumeRange = new Vector2(0.35f, 0.5f);
    
    [Header("Loop Settings")]
    [SerializeField, Range(0f, 1f)] private float leapInAirLoopVolume = 0.4f;
    
    void Awake()
    {
        if (brain == null) brain = GetComponent<BossBrainV2>();
        if (brain == null) brain = GetComponentInParent<BossBrainV2>();
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private Dictionary<AudioClip[], AudioClip> lastClipPerArray = new Dictionary<AudioClip[], AudioClip>();

    void PlayRandom(AudioClip[] clips, Vector2 volumeRange)
    {
        if (clips == null || clips.Length == 0 || audioSource == null) return;

        AudioClip clip;
        if (clips.Length == 1)
        {
            // Only one option — no choice but to repeat.
            clip = clips[0];
        }
        else
        {
            lastClipPerArray.TryGetValue(clips, out AudioClip last);
            // Pick until we get something different from last time.
            do
            {
                clip = clips[Random.Range(0, clips.Length)];
            } while (clip == last);
        }

        if (clip == null) return;
        lastClipPerArray[clips] = clip;
        audioSource.PlayOneShot(clip, Random.Range(volumeRange.x, volumeRange.y));
    }
    
    // ----------------------------------------------------------------
    // Animation-event hooks (also callable from code via BossBrainV2.PlaySound)
    // ----------------------------------------------------------------

    public void OnStep()
    {
        // Don't play footsteps while airborne. The arena reposition leap drives
        // the boss off the NavMesh, so any walk-cycle anim still firing footstep
        // events shouldn't make ground sounds.
        if (brain != null && brain.isInArenaSequence) return;
        PlayRandom(footstepClips, footstepVolumeRange);
    }
    
    public void OnImpact()           { PlayRandom(impactClips, impactVolumeRange); }
    public void OnSmash()            { PlayRandom(smashClips, smashVolumeRange); }
    public void OnRoar()             { PlayRandom(roarClips, roarVolumeRange); }
    public void OnDeath()            { PlayRandom(deathClips, deathVolumeRange); }
    public void OnProjectile()       { PlayRandom(projectileClips, projectileVolumeRange); }
    public void OnWingFlap()         { PlayRandom(wingFlapClips, wingFlapVolumeRange); }
    public void OnDashWindup()       { PlayRandom(dashWindupClips, dashWindupVolumeRange); }
    public void OnDashRelease()      { PlayRandom(dashReleaseClips, dashReleaseVolumeRange); }
    public void OnWavePushback()     { PlayRandom(wavePushbackClips, wavePushbackVolumeRange); }
    public void OnChargeUp()         { PlayRandom(chargeUpClips, chargeUpVolumeRange); }
    public void OnHurt()             { PlayRandom(hurtClips, hurtVolumeRange); }
    public void OnPhaseTransition()  { PlayRandom(phaseTransitionClips, phaseTransitionVolumeRange); }
    public void OnLeapTakeoff()      { PlayRandom(leapTakeoffClips, leapTakeoffVolumeRange); }
    public void OnLeapLand()         { PlayRandom(leapLandClips, leapLandVolumeRange); }

    // Convenience aliases for animation events that want to share the same sound category but have different named events for clarity. E.g. the wing flap sound is used for both the leap takeoff and the mid-air flaps, but we want separate events for each in the anim clips.
    public void OnFlap() { OnWingFlap(); }
    public void OnFire() { OnProjectile(); }
    
    // ----------------------------------------------------------------
    // Looping in-air sound
    // ----------------------------------------------------------------

    public void OnLeapAirLoopStart()
    {
        if (loopAudioSource == null || leapInAirLoopClip == null) return;
        if (loopAudioSource.isPlaying && loopAudioSource.clip == leapInAirLoopClip) return;
        loopAudioSource.clip = leapInAirLoopClip;
        loopAudioSource.loop = true;
        loopAudioSource.volume = leapInAirLoopVolume;
        loopAudioSource.Play();
    }

    public void OnLeapAirLoopStop()
    {
        if (loopAudioSource != null && loopAudioSource.isPlaying)
            loopAudioSource.Stop();
    }
}