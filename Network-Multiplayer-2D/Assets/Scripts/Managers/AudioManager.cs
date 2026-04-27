using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Singleton { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip   menuMusic;
    [SerializeField] private AudioClip   gameMusic;

    [Header("Player")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip   deathClip;
    [SerializeField] private AudioClip   respawnClip;
    [SerializeField] private AudioClip   knockbackClip;
    [SerializeField] private AudioClip   freezeClip;
    [SerializeField] private AudioClip   shootClip;

    [Header("Obstacles")]
    [SerializeField] private AudioClip   fireClip;
    [SerializeField] private AudioClip   sinkClip;
    [SerializeField] private AudioClip   wallClip;
    [SerializeField] private AudioClip   bookClip;
     [SerializeField] private AudioClip   fallingChandelierClip;

    [Header("Goal")]
    [SerializeField] private AudioClip   goalClip;
    [SerializeField] private AudioClip   roundWinClip;

    [Header("Settings")]
    [SerializeField] private float musicVolume = 0.4f;
    [SerializeField] private float sfxVolume   = 1f;


    private void Awake()
    {
        if (Singleton != null) { Destroy(gameObject); return; }
        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        PlayMenuMusic();
    }


    public void PlayMenuMusic()
    {
        if (menuMusic == null || musicSource == null) return;
        musicSource.clip   = menuMusic;
        musicSource.loop   = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void PlayGameMusic()
    {
        if (gameMusic == null || musicSource == null) return;
        musicSource.clip   = gameMusic;
        musicSource.loop   = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }




    public void PlayDeath() => PlaySFX(deathClip);
    public void PlayRespawn() => PlaySFX(respawnClip);
    public void PlayKnockback() => PlaySFX(knockbackClip);
    public void PlayFreeze() => PlaySFX(freezeClip);
    public void PlayShoot()  => PlaySFX(shootClip);


   

    public void PlayFire() => PlaySFX(fireClip);
    public void PlaySink() => PlaySFX(sinkClip);
    public void PlayWall() => PlaySFX(wallClip);
    public void PlayBook()=> PlaySFX(bookClip);
    public void PlayFallingChandelier() => PlaySFX(fallingChandelierClip);


    public void PlayGoal() => PlaySFX(goalClip);
    public void PlayRoundWin() => PlaySFX(roundWinClip);




    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}