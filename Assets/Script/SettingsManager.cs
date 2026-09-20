using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    [Header("--- UI Panel ---")]
    public GameObject settingsPanel;
    public Button openSettingButton;
    public Button closeSettingButton;

    [Header("--- Volume Sliders ---")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("--- Dropdowns & Audio ---")]
    public TMP_Dropdown bgmDropdown;
    public TMP_Dropdown fpsDropdown;
    public AudioSource bgmAudioSource; // Kéo AudioSource phát nhạc ở Scene này vào
    public List<AudioClip> localBGMList = new List<AudioClip>(); // Danh sách nhạc BGM riêng cho Scene này

    [Header("--- Tâm ngắm (Crosshair) ---")]
    public CrosshairReticle crosshair; // Nếu để trống sẽ tự tìm trong scene
    public bool crosshairEnabled = true;

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (openSettingButton != null)
            openSettingButton.onClick.AddListener(OpenPanel);

        if (closeSettingButton != null)
            closeSettingButton.onClick.AddListener(ClosePanel);

        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // Cấu hình BGM Dropdown
        SetupBGMDropdown();

        // Cấu hình FPS Dropdown
        SetupFPSDropdown();

        // Đồng bộ trạng thái tâm ngắm với cài đặt
        if (crosshair == null)
        {
            crosshair = FindObjectOfType<CrosshairReticle>();
        }
        if (crosshair != null)
        {
            crosshair.SetCrosshairEnabled(crosshairEnabled);
        }
    }

    // Hàm gọi từ Toggle "Hiện tâm" trong Menu Settings
    public void SetCrosshairEnabled(bool value)
    {
        crosshairEnabled = value;
        if (crosshair != null)
        {
            crosshair.SetCrosshairEnabled(value);
        }
    }

    private void Update()
    {
        // Nhấn ESC để mở/đóng Settings (trong khi chơi)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    public void OpenPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Ẩn tâm ngắm khi menu Settings mở
            if (crosshair != null)
            {
                crosshair.SetForceHidden(true);
            }
        }
    }

    public void ClosePanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);

            // Hiện lại tâm ngắm khi đóng Settings
            if (crosshair != null)
            {
                crosshair.SetForceHidden(false);
            }

            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value; // Chỉnh âm lượng tổng hệ thống
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);
    }

    private void OnBGMVolumeChanged(float value)
    {
        if (bgmAudioSource != null)
            bgmAudioSource.volume = value;

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetBGMVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);
    }

    // --- CẤU HÌNH BGM DROPDOWN ---
    private void SetupBGMDropdown()
    {
        if (bgmDropdown == null) return;

        bgmDropdown.ClearOptions();
        List<string> options = new List<string>();

        // Ưu tiên 1: Lấy danh sách nhạc tự kéo trong Inspector của Scene này
        if (localBGMList != null && localBGMList.Count > 0)
        {
            for (int i = 0; i < localBGMList.Count; i++)
            {
                if (localBGMList[i] != null)
                    options.Add(localBGMList[i].name);
                else
                    options.Add("Bài nhạc " + (i + 1));
            }
        }
        // Ưu tiên 2: Nếu không kéo nhạc riêng thì lấy từ AudioManager (nếu có)
        else if (AudioManager.Instance != null && AudioManager.Instance.bgmClips != null)
        {
            for (int i = 0; i < AudioManager.Instance.bgmClips.Length; i++)
            {
                if (AudioManager.Instance.bgmClips[i] != null)
                    options.Add(AudioManager.Instance.bgmClips[i].name);
                else
                    options.Add("Nhạc " + (i + 1));
            }
        }
        else
        {
            options.Add("Không có nhạc");
        }

        bgmDropdown.AddOptions(options);
        bgmDropdown.onValueChanged.AddListener(OnBGMSelected);
    }

    private void OnBGMSelected(int index)
    {
        // Phát nhạc trực tiếp bằng localBGMList nếu có
        if (localBGMList != null && localBGMList.Count > index && bgmAudioSource != null)
        {
            bgmAudioSource.clip = localBGMList[index];
            bgmAudioSource.Play();
        }
        // Hoặc phát bằng AudioManager
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ChangeBGM(index);
        }
    }

    private void SetupFPSDropdown()
    {
        if (fpsDropdown == null) return;

        fpsDropdown.ClearOptions();
        List<string> fpsOptions = new List<string> { "30 FPS", "60 FPS", "90 FPS", "Không giới hạn" };

        fpsDropdown.AddOptions(fpsOptions);
        fpsDropdown.onValueChanged.AddListener(OnFPSSelected);

        fpsDropdown.value = 1;
        OnFPSSelected(1);
    }

    private void OnFPSSelected(int index)
    {
        QualitySettings.vSyncCount = 0;

        switch (index)
        {
            case 0: Application.targetFrameRate = 30; break;
            case 1: Application.targetFrameRate = 60; break;
            case 2: Application.targetFrameRate = 90; break;
            case 3: Application.targetFrameRate = -1; break;
        }
    }
}