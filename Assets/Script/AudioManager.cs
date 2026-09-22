using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("--- Audio Sources ---")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("--- List BGM (Nhạc nền) ---")]
    public AudioClip[] bgmClips;

    [Header("--- SFX Clips (Cố định) ---")]
    public AudioClip footstepClip;
    public AudioClip jumpClip;
    public AudioClip inspectClip;

    private float masterVolume = 1.0f;
    private float bgmVolume = 1.0f;
    private float sfxVolume = 1.0f;

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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Tự động phát bài BGM đầu tiên (phần tử số 0) khi game vừa chạy
        if (bgmClips != null && bgmClips.Length > 0)
        {
            ChangeBGM(0);
        }
    }

    // --- CÁC HÀM PHÁT ÂM THANH ---
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
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
                bgmSource.volume = bgmVolume * masterVolume;
                bgmSource.Play();
            }
        }
    }

    // --- CÁC HÀM ĐIỀU CHỈNH ÂM LƯỢNG ---
    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        UpdateVolumes();
    }

    public void SetBGMVolume(float value)
    {
        bgmVolume = value;
        UpdateVolumes();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = value;
        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume * masterVolume;
        }
    }
}