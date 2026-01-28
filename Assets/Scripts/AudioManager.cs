using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [Header("Library")]
    [SerializeField] private GameAudioLibrary lib;

    [Header("Music")]
    [SerializeField] private AudioSource musicA;
    [SerializeField] private AudioSource musicB;
    [SerializeField] private float musicFadeSeconds = 0.5f;

    [Header("SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;

    [Header("Global Muffle")]
    [SerializeField] private float muffledCutoffHz = 900f;
    [SerializeField] private float normalCutoffHz = 22000f;

    [Header("Pitch Variation")]
    [SerializeField] private bool randomPitch = false;
    [SerializeField] private float minPitch = 0.97f;
    [SerializeField] private float maxPitch = 1.03f;

    private AudioSource activeMusic;
    private AudioSource inactiveMusic;

    // Low-pass per source so ALL audio gets muffled (musicA + musicB + sfx)
    private AudioLowPassFilter lpMusicA;
    private AudioLowPassFilter lpMusicB;
    private AudioLowPassFilter lpSfx;

    private bool isMuffled;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        // Auto-create sources if missing
        if (musicA == null) musicA = gameObject.AddComponent<AudioSource>();
        if (musicB == null) musicB = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

        SetupMusicSource(musicA);
        SetupMusicSource(musicB);
        SetupSfxSource(sfxSource);

        if (uiSource == null) uiSource = gameObject.AddComponent<AudioSource>();
        SetupSfxSource(uiSource);

        uiSource.ignoreListenerPause = true;

        // Ensure low-pass filters exist on all outputs
        lpMusicA = EnsureLowPass(musicA);
        lpMusicB = EnsureLowPass(musicB);
        lpSfx = EnsureLowPass(sfxSource);

        SetMuffled(false);

        activeMusic = musicA;
        inactiveMusic = musicB;
    }

    private static void SetupMusicSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f;
    }

    private static void SetupSfxSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.volume = 1f;
    }

    private static AudioLowPassFilter EnsureLowPass(AudioSource src)
    {
        var lp = src.GetComponent<AudioLowPassFilter>();
        if (lp == null) lp = src.gameObject.AddComponent<AudioLowPassFilter>();
        return lp;
    }

    // ---------------- GLOBAL MUFFLE API ----------------
    // Call AudioManager.I.SetMuffled(true/false) from anywhere.
    public void SetMuffled(bool muffled)
    {
        isMuffled = muffled;
        ApplyMuffleState();
    }

    public void ToggleMuffle()
    {
        isMuffled = !isMuffled;
        ApplyMuffleState();
    }

    private void ApplyMuffleState()
    {
        // "Raw" when not muffled: disable filters completely.
        // Muffled: enable and set cutoff.
        if (!isMuffled)
        {
            lpMusicA.enabled = false;
            lpMusicB.enabled = false;
            lpSfx.enabled = false;
            return;
        }

        lpMusicA.enabled = true;
        lpMusicB.enabled = true;
        lpSfx.enabled = true;

        lpMusicA.cutoffFrequency = muffledCutoffHz;
        lpMusicB.cutoffFrequency = muffledCutoffHz;
        lpSfx.cutoffFrequency = muffledCutoffHz;
    }

    // ---------------- MUSIC ----------------

    public void PlayMusic(MusicTrack track)
    {
        if (lib == null) return;

        AudioClip clip = track switch
        {
            MusicTrack.MainMenuTheme => lib.mainMenuTheme,
            MusicTrack.GameplayTheme => lib.gameplayTheme,
            MusicTrack.EndGameTheme => lib.endGameTheme,
            _ => null
        };

        if (clip == null) return;

        if (activeMusic.isPlaying && activeMusic.clip == clip)
            return;

        StopAllCoroutines();
        StartCoroutine(CrossFadeTo(clip));
    }

    private IEnumerator CrossFadeTo(AudioClip next)
    {
        inactiveMusic.clip = next;
        inactiveMusic.volume = 0f;
        inactiveMusic.Play();

        float t = 0f;
        float aStart = activeMusic.volume;

        while (t < musicFadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = (musicFadeSeconds <= 0f) ? 1f : Mathf.Clamp01(t / musicFadeSeconds);

            inactiveMusic.volume = k;
            activeMusic.volume = Mathf.Lerp(aStart, 0f, k);

            yield return null;
        }

        activeMusic.Stop();
        activeMusic.volume = 0f;

        var tmp = activeMusic;
        activeMusic = inactiveMusic;
        inactiveMusic = tmp;
    }

    // ---------------- SFX ----------------

    public void PlaySfx(SfxId id)
    {
        if (lib == null) return;

        AudioClip clip = GetRandomClip(id);
        if (clip == null) return;

        if (randomPitch)
            sfxSource.pitch = Random.Range(minPitch, maxPitch);
        else
            sfxSource.pitch = 1f;

        sfxSource.PlayOneShot(clip);
    }

    private AudioClip GetRandomClip(SfxId id)
    {
        AudioClip[] arr = id switch
        {
            SfxId.ButtonHover => lib.buttonHover,
            SfxId.ButtonClick => lib.buttonClick,
            SfxId.PointCollect => null,
            SfxId.Surf => lib.surf,
            SfxId.Move => lib.move,
            SfxId.Jump => lib.jump,
            SfxId.ObstacleHit => lib.obstacleHit,
            SfxId.Death => lib.death,
            SfxId.CurrentScoreJingle => lib.currentScoreJingle,
            SfxId.HighScoreJingle => lib.highScoreJingle,
            SfxId.Submerge => lib.submerge,
            SfxId.Surface => lib.surface,
            SfxId.LoseStreak => lib.loseStreak,
            _ => null
        };

        if (arr == null || arr.Length == 0) return null;
        return arr[Random.Range(0, arr.Length)];
    }

    public void PlayPointCollectTier(int tier)
    {
        if (lib == null || lib.pointCollect == null || lib.pointCollect.Length == 0)
            return;

        int idx = Mathf.Clamp(tier, 0, lib.pointCollect.Length - 1);
        AudioClip clip = lib.pointCollect[idx];
        if (clip == null) return;

        sfxSource.PlayOneShot(clip);
    }

        public void SetPausedAudio(bool paused)
    {
        AudioListener.pause = paused;

        if (paused)
        {
            if (musicA != null && musicA.isPlaying) musicA.Pause();
            if (musicB != null && musicB.isPlaying) musicB.Pause();
        }
        else
        {
            if (musicA != null) musicA.UnPause();
            if (musicB != null) musicB.UnPause();
        }
    }

    public void PlayUiSfx(SfxId id)
    {
        if (lib == null) return;
        AudioClip clip = GetRandomClip(id);
        if (clip == null) return;

        uiSource.PlayOneShot(clip);
    }

}