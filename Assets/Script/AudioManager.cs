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

    private float bgmVolume = 1.0f;
    private float sfxVolume = 1.0f;

    // Chỉ tự phát BGM 1 lần duy nhất mỗi lần mở app. Không có cờ này, bản
    // duplicate ở scene gallery sẽ Start() và phát lại track 0 từ đầu mỗi
    // lần về menu rồi vào lại (BGM restart).
    private static bool bgmAutoStarted = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad chỉ áp dụng cho root GameObject. AudioManager trong
            // scene gallery đang nằm con (có parent) nên phải tách ra trước.
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Tự động tìm 2 AudioSource nếu chưa kéo
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length >= 1 && bgmSource == null) bgmSource = sources[0];
            if (sources.Length >= 2 && sfxSource == null) sfxSource = sources[1];
            if (sources.Length >= 3 && voiceSource == null) voiceSource = sources[2];
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Tự động phát bài BGM đầu tiên (phần tử số 0) khi game vừa chạy.
        // Guard: nếu nhạc đang phát rồi (về menu rồi vào lại gallery) thì giữ
        // nguyên, không restart từ đầu.
        if (bgmAutoStarted) return;
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmAutoStarted = true;
            return;
        }

        // Keep retrying on a later AudioManager if this scene has no usable
        // track/source. A missing clip must not consume the one-time startup.
        if (bgmSource == null || bgmClips == null || bgmClips.Length == 0 || bgmClips[0] == null)
            return;

        ChangeBGM(0);
        if (bgmSource.isPlaying) bgmAutoStarted = true;
    }

    // --- CÁC HÀM PHÁT ÂM THANH ---
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayVoiceover(AudioClip clip)
    {
        if (voiceSource == null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
        }
        voiceSource.Stop();
        if (clip != null)
        {
            voiceSource.clip = clip;
            voiceSource.volume = 1f;
            voiceSource.Play();
        }
    }

    public void StopVoiceover()
    {
        if (voiceSource != null && voiceSource.isPlaying)
        {
            voiceSource.Stop();
        }
    }


    public void ChangeBGM(int index)
    {
        if (bgmClips != null && index >= 0 && index < bgmClips.Length)
        {
            if (bgmSource != null)
            {
                bgmSource.clip = bgmClips[index];
                bgmSource.loop = true;
                bgmSource.volume = bgmVolume;
                bgmSource.Play();
            }
        }
    }

    // --- CÁC HÀM ĐIỀU CHỈNH ÂM LƯỢNG ---
    public void SetMasterVolume(float value)
    {
        // Global gain is applied once, including AudioSources outside this manager.
        AudioListener.volume = Mathf.Clamp01(value);
    }

    public void SetBGMVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        UpdateVolumes();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }
        if (voiceSource != null)
        {
            voiceSource.volume = 1f;
        }
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }
}
