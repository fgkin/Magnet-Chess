using UnityEngine;

public class GameAudio : MonoBehaviour
{
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip magnetSnapClip;
    [SerializeField] private AudioClip placeClip;
    [SerializeField] private AudioClip invalidDropClip;
    [SerializeField] private AudioClip timeoutClip;

    [SerializeField] private AudioClip victoryClip;

    public void PlayMagnetSnap()
    {
        PlayClip(magnetSnapClip);
    }

    public void PlayPlace()
    {
        PlayClip(placeClip);
    }

    public void PlayInvalidDrop()
    {
        PlayClip(invalidDropClip);
    }

    public void PlayTimeout()
    {
        PlayClip(timeoutClip);
    }

    public void PlayVictory()
    {
        PlayClip(victoryClip);
    }
    
    private void PlayClip(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }
}