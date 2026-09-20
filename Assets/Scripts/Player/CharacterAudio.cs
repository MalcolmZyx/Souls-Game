using Unity.VisualScripting;
using UnityEngine;

public class CharacterAudio : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Clips")]
    [SerializeField] private AudioClip swordSwingClip;
    [SerializeField] private AudioClip swordHitClip;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private AudioClip[] runClips;
    [SerializeField] private AudioClip[] rollClips;
    
    [Header("Settings")]
    [SerializeField] public Vector2 footstepVolumeRange = new Vector2(0.05f, 0.1f);
    [SerializeField] public Vector2 runVolumeRange = new Vector2(0.05f, 0.1f);
    
    // Called from animation event on footstep frames
    public void PlayFootstep()
    {
        if (footstepClips.Length == 0) return;
        if (animator.GetFloat("Speed") < 0.01f) return;

        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        audioSource.PlayOneShot(clip, Random.Range(footstepVolumeRange.x, footstepVolumeRange.y));
    }
    
    public void PlayRun()
    {
        if (runClips.Length == 0) return;

        float speed = animator.GetFloat("Speed");
        if (speed < 0.01f) return;
        
        var clip = runClips[Random.Range(0, runClips.Length)];
        audioSource.PlayOneShot(clip, Random.Range(runVolumeRange.x * speed, runVolumeRange.y * speed));
    }

    // Called from animation event at the start of a swing
    public void PlaySwordSwing()
    {
        audioSource.PlayOneShot(swordSwingClip);
    }
    
    public void PlayRoll()
    {
        if (rollClips.Length == 0) return;
        
        var clip = rollClips[Random.Range(0, rollClips.Length)];
        audioSource.PlayOneShot(clip);
    }
}