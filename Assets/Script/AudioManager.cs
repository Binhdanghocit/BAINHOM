using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("--- Audio Sources ---")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource voiceSource;

    [Header("--- List BGM (Nhạc nền) ---")]
    public AudioClip[] bgmClips;

    [Header("--- SFX Clips (Cố định) ---")]
    public AudioClip footstepClip;
    public AudioClip jumpClip;
    public AudioClip inspectClip;

    // Session settings exist even before a scene creates the first manager.
    public static float MasterVolume { get; private set; } = 1f;
    public static float BGMVolume { get; private set; } = 1f;
    public static float SFXVolume { get; private set; } = 1f;
    public static AudioClip SelectedBGM { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Instance = null;
        SelectedBGM = null;
        MasterVolume = BGMVolume = SFXVolume = 1f;
        AudioListener.volume = MasterVolume;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Stop immediately, rather than waiting for deferred Destroy.
            foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
            {
                source.playOnAwake = false;
                source.Stop();
                source.enabled = false;
            }
            Instance.AddBGMClips(bgmClips);
            if (Instance.footstepClip == null) Instance.footstepClip = footstepClip;
            if (Instance.jumpClip == null) Instance.jumpClip = jumpClip;
            if (Instance.inspectClip == null) Instance.inspectClip = inspectClip;
            Instance.EnsureBGMPlaying();
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null) transform.SetParent(null);
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        AudioSource[] sources = GetComponents<AudioSource>();
        if (bgmSource == null && sources.Length > 0) bgmSource = sources[0];
        if (sfxSource == null && sources.Length > 1) sfxSource = sources[1];
        if (voiceSource == null && sources.Length > 2) voiceSource = sources[2];
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = sfxSource.playOnAwake = voiceSource.playOnAwake = false;
        bgmSource.loop = true;
        // Serialized sources cannot override a choice made before this Awake.
        if (SelectedBGM == null && bgmSource.clip != null) SelectedBGM = bgmSource.clip;
        ApplyVolumes();
    }

    private void Start()
    {
        if (Instance == this) EnsureBGMPlaying();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Retire the old scene source and give its catalog to a persistent owner.
    public static void RegisterSceneBGM(AudioSource legacySource, IEnumerable<AudioClip> clips)
    {
        if (legacySource != null && (Instance == null || legacySource != Instance.bgmSource))
        {
            legacySource.Stop();
            legacySource.playOnAwake = false;
            legacySource.enabled = false;
        }
        if (Instance == null)
            new GameObject("AudioManager").AddComponent<AudioManager>();
        Instance.AddBGMClips(clips);
        Instance.EnsureBGMPlaying();
    }

    private void AddBGMClips(IEnumerable<AudioClip> clips)
    {
        var catalog = new List<AudioClip>();
        if (bgmClips != null)
            foreach (AudioClip clip in bgmClips)
                if (clip != null && !catalog.Contains(clip)) catalog.Add(clip);
        if (clips != null)
            foreach (AudioClip clip in clips)
                if (clip != null && !catalog.Contains(clip)) catalog.Add(clip);
        bgmClips = catalog.ToArray();
    }

    private void EnsureBGMPlaying()
    {
        if (SelectedBGM == null && bgmClips != null)
            foreach (AudioClip clip in bgmClips)
                if (clip != null) { SelectedBGM = clip; break; }
        if (SelectedBGM == null || bgmSource == null) return;
        if (bgmSource.clip == SelectedBGM && bgmSource.isPlaying) return;
        bgmSource.clip = SelectedBGM;
        bgmSource.loop = true;
        bgmSource.volume = BGMVolume;
        bgmSource.Play();
    }

    public static void SelectBGM(AudioClip clip)
    {
        if (clip == null) return;
        SelectedBGM = clip;
        if (Instance != null) Instance.EnsureBGMPlaying();
    }

    public void ChangeBGM(int index)
    {
        if (bgmClips == null || index < 0 || index >= bgmClips.Length) return;
        SelectBGM(bgmClips[index]);
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
    }

    public void PlayVoiceover(AudioClip clip)
    {
        if (voiceSource == null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
        }
        voiceSource.Stop();
        if (clip == null) return;
        voiceSource.clip = clip;
        voiceSource.volume = 1f;
        voiceSource.Play();
    }

    public void StopVoiceover()
    {
        if (voiceSource != null) voiceSource.Stop();
    }

    public static void SaveMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
    }

    public static void SaveBGMVolume(float value)
    {
        BGMVolume = Mathf.Clamp01(value);
        if (Instance != null) Instance.ApplyVolumes();
    }

    public static void SaveSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        if (Instance != null) Instance.ApplyVolumes();
    }

    // Preserve gameplay callers and serialized callbacks.
    public void SetMasterVolume(float value) => SaveMasterVolume(value);
    public void SetBGMVolume(float value) => SaveBGMVolume(value);
    public void SetSFXVolume(float value) => SaveSFXVolume(value);

    private void ApplyVolumes()
    {
        // Master gain applies only at the listener, never again at a source.
        AudioListener.volume = MasterVolume;
        if (bgmSource != null) bgmSource.volume = BGMVolume;
        if (sfxSource != null) sfxSource.volume = SFXVolume;
        if (voiceSource != null) voiceSource.volume = 1f;
    }
}
