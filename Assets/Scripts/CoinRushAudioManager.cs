using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinRushAudioManager : MonoBehaviour
{
    [SerializeField] private CoinRushGameManager game;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioClip coinClip;
    [SerializeField] private AudioClip victoryClip;
    private bool pausedMusic;
    private bool victoryPlayed;

    public AudioSource MusicSource => musicSource;
    public AudioSource EffectsSource => effectsSource;

    public void Configure(CoinRushGameManager owner, AudioSource music, AudioSource effects,
        AudioClip background, AudioClip collect, AudioClip victory)
    {
        game = owner;
        musicSource = music;
        effectsSource = effects;
        coinClip = collect;
        victoryClip = victory;
        musicSource.clip = background;
        musicSource.loop = true;
        musicSource.volume = 0.25f;
        musicSource.mute = false;
        musicSource.pitch = 1f;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0;
        musicSource.ignoreListenerPause = false;
        effectsSource.clip = null;
        effectsSource.loop = false;
        effectsSource.volume = 1f;
        effectsSource.mute = false;
        effectsSource.pitch = 1f;
        effectsSource.playOnAwake = false;
        effectsSource.spatialBlend = 0;
        // Victory remains audible on the outcome screen, independent of scaled gameplay time.
        effectsSource.ignoreListenerPause = true;
    }

    public void StartRound()
    {
        StopRound();
        victoryPlayed = false;
        if (musicSource != null && musicSource.clip != null) musicSource.Play();
    }

    public void PlayCoin()
    {
        if (game == null || !game.IsRunning || Time.timeScale <= 0f || effectsSource == null || coinClip == null) return;
        effectsSource.PlayOneShot(coinClip, 0.65f);
    }

    public void PauseRound()
    {
        pausedMusic = musicSource != null && musicSource.isPlaying;
        if (pausedMusic) musicSource.Pause();
        if (effectsSource != null) effectsSource.Pause();
    }

    public void ResumeRound()
    {
        if (pausedMusic && musicSource != null) musicSource.UnPause();
        if (effectsSource != null) effectsSource.UnPause();
        pausedMusic = false;
    }

    public void StopRound()
    {
        if (musicSource != null) musicSource.Stop();
        if (effectsSource != null) effectsSource.Stop();
        pausedMusic = false;
    }

    public void PlayVictory()
    {
        if (victoryPlayed) return;
        victoryPlayed = true;
        StopRound();
        if (effectsSource != null && victoryClip != null) effectsSource.PlayOneShot(victoryClip, 0.85f);
    }

    private void OnDestroy() => StopRound();
}
