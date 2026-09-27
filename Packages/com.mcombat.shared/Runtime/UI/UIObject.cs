using UnityEngine;

public class UIObject : MonoBehaviour
{
    [SerializeField] private ParticleSystem particleSystem;
    [SerializeField] private AudioSource audioSource;

    public ParticleSystem ParticleSystem => particleSystem;
    
    public void PlaySoundOnce()
    {
        audioSource.volume = AppSetting.Value.EffectsVolume;
        audioSource.PlayOneShot(audioSource.clip);
    }
}
