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
    public AudioSource bgmAudioSource; // Legacy scene source, retired by the shared owner.
    public List<AudioClip> localBGMList = new List<AudioClip>(); // Danh sách nhạc BGM riêng cho Scene này

    [Header("--- Tâm ngắm (Crosshair) ---")]
    public CrosshairReticle crosshair; // Nếu để trống sẽ tự tìm trong scene
    public bool crosshairEnabled = true;

    [Header("--- Joystick di chuyển (điện thoại) ---")]
    [Tooltip("Bật: joystick xuất hiện tại vị trí chạm. Tắt: cố định góc trái dưới.")]
    public bool floatingJoystickEnabled = true;

    [Header("--- Điều hướng (Main Menu / Thoát) ---")]
    [Tooltip("Tên scene Main Menu để quay về từ Gallery.")]
    public string mainMenuSceneName = "MainMenu";
    [Tooltip("Nút 'Về Main Menu' trong panel Settings (PC/Mobile bấm chuột/chạm, VR bấm bằng ray). Để trống sẽ tự tìm nút tên chứa MainMenu/Home/VeMenu.")]
    public Button mainMenuButton;
    [Tooltip("Nút 'Thoát game' trong panel Settings. Để trống sẽ tự tìm nút tên chứa Quit/Exit/Thoat.")]
    public Button quitButton;
    [Tooltip("VR: tick nếu muốn về Main Menu là THOÁT kính về màn hình phẳng (dành cho PCVR). Để trống = GIỮ VR ở menu (khuyên dùng cho Quest).")]
    public bool exitVRWhenToMainMenu = false;

    private void Awake()
    {
        // Gallery không gắn sẵn bridge như menu: tự gắn để controller VR bấm/kéo
        // được UI Settings. Bridge tự ngắt khi không chạy XR nên gắn thường trực an toàn.
        VRUIInputBridge.EnsureInstance();
    }

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (openSettingButton != null)
            openSettingButton.onClick.AddListener(OpenPanel);

        if (closeSettingButton != null)
            closeSettingButton.onClick.AddListener(ClosePanel);

        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // Cấu hình BGM Dropdown
        AudioManager.RegisterSceneBGM(bgmAudioSource, localBGMList);
        SyncAudioSettingsUI();

        // Cấu hình FPS Dropdown
        SetupFPSDropdown();
        lastXRState = PlatformHelper.IsXRDisplayRunning();

        // Nút điều hướng: designer kéo thả trong Inspector; scene cũ chưa có nút
        // thì tự tìm theo tên để không phải sửa code.
        WireNavButtons();

        // Đồng bộ trạng thái tâm ngắm với cài đặt
        if (crosshair == null)
        {
            crosshair = FindAnyObjectByType<CrosshairReticle>();
        }
        if (crosshair != null)
        {
            crosshair.SetCrosshairEnabled(crosshairEnabled);
        }

        MobileControlsOverlay.SetFloatingJoystickEnabled(floatingJoystickEnabled);

        // Canvas Setting trong scene đang để Sort Order = 0,_anchor giữa-phải (1,0.5) + y=500
        // -> máy màn thấp/tai thỏ bị tràn mất nút + bị canvas Mobile (order 1000) đè.
        // Tự sửa runtime theo đúng màn hình từng máy.
        FixSettingsUIForAllScreens();
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

    // Gán cho Toggle "Joystick nổi" trong panel Settings
    public void SetFloatingJoystickEnabled(bool value)
    {
        floatingJoystickEnabled = value;
        MobileControlsOverlay.SetFloatingJoystickEnabled(value);
    }

    // Panel có đang mở không (mobile dùng để chặn tap xuyên Settings)
    public bool IsSettingsOpen()
    {
        return settingsPanel != null && settingsPanel.activeSelf;
    }

    private int lastScreenW;
    private int lastScreenH;
    private Rect lastSettingsSafeArea;
    private Vector2 lastSettingsCanvasSize;
    private RectTransform settingsSafeContent;
    private RectTransform settingsCard;
    private TMP_Text settingsHeading;
    private TMP_Text musicHeading;
    private TMP_Text fpsHeading;
    private readonly List<Toggle> settingsExtraToggles = new List<Toggle>();
    // Giá trị FPS thật song song với từng option hiển thị (mobile không có 90).
    private readonly List<int> fpsValues = new List<int>();
    private bool lastXRState;
    private float nextXRCheckTime;

    private void Update()
    {
        // Nhấn ESC (PC / nút Back Android) hoặc nút menu controller (VR) để mở/đóng Settings
        if (Input.GetKeyDown(KeyCode.Escape) || HandTriggerInput.WasMenuButtonPressedThisFrame())
        {
            TogglePanel();
        }

        // Xoay màn / đổi máy / chia đôi màn hình -> tính lại vị trí nút Setting
        Canvas settingsCanvas = settingsPanel != null ? settingsPanel.GetComponentInParent<Canvas>() : null;
        Vector2 canvasSize = settingsCanvas != null ? ((RectTransform)settingsCanvas.transform).rect.size : Vector2.zero;
        if (Screen.width != lastScreenW || Screen.height != lastScreenH || Screen.safeArea != lastSettingsSafeArea
            || canvasSize != lastSettingsCanvasSize)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            lastSettingsSafeArea = Screen.safeArea;
            lastSettingsCanvasSize = canvasSize;
            FixSettingsUIForAllScreens();
        }

        // XR có thể bật MUỘN (Quest auto / PC bấm "Chơi VR") -> ép FPS về Không giới hạn.
        // Check thưa 1s/lần để không alloc List subsystem mỗi frame.
        if (Time.unscaledTime >= nextXRCheckTime)
        {
            nextXRCheckTime = Time.unscaledTime + 1f;
            bool nowXR = PlatformHelper.IsXRDisplayRunning();
            if (nowXR != lastXRState)
            {
                lastXRState = nowXR;
                if (nowXR) RefreshFPSDropdownForXR();
            }
            // Hết cửa sổ xác nhận Thoát trong VR -> trả label về bình thường (1 lần).
            if (quitArmed && Time.unscaledTime - quitArmTime > QuitConfirmWindow)
            {
                quitArmed = false;
                if (quitButton != null) SetButtonLabel(quitButton.gameObject, "THOÁT GAME");
            }
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
            SyncAudioSettingsUI();
            settingsPanel.SetActive(true);
            FixSettingsUIForAllScreens();
            ViewModeController.PauseGameplayForModal();
            MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
            if (controls != null) controls.SetGameplayInputEnabled(false);
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

            // Hiện lại tâm ngắm khi đóng Settings (tâm tự ẩn khi UI khác còn mở,
            // nên luôn xả cờ force-hidden ở đây là an toàn).
            if (crosshair != null)
            {
                crosshair.SetForceHidden(false);
            }

            if (SceneManager.GetActiveScene().name == mainMenuSceneName)
            {
                ViewModeController.TryResumeGameplayIfClear();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // FIX kẹt input: chỉ khôi phục mobile/cursor khi KHÔNG còn modal nào
                // (menu cửa, workshop, popup tranh, hội thoại...). Đóng Settings trong
                // lúc workshop/menu cửa còn mở mà bật lại input + khóa chuột sẽ làm
                // người chơi không bấm được nút modal đang mở.
                ViewModeController.TryResumeGameplayIfClear();
            }
        }
    }

    // Lấy đúng màn hình từng máy (kể cả tai thỏ) rồi neo nút Setting vào góc phải-trên.
    // Fix gốc: nút cũ neo (1,0.5) + y=500 nên màn thấp là tràn mất; Canvas Sort=0 nên bị Mobile (1000) đè.
    private void FixSettingsUIForAllScreens()
    {
        ApplySettingsLayout(Screen.safeArea, new Vector2(Screen.width, Screen.height));
    }

    private void ApplySettingsLayout(Rect safe, Vector2 screenSize)
    {
        if (screenSize.x <= 0 || screenSize.y <= 0) return;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(Vector2.zero, screenSize);
        // 1. Luôn vẽ Settings trên cùng (cao hơn canvas Mobile runtime order 1000)
        if (settingsPanel != null)
        {
            Canvas panelCanvas = settingsPanel.GetComponentInParent<Canvas>();
            if (panelCanvas != null && panelCanvas.sortingOrder < 2000)
                panelCanvas.sortingOrder = 2000;
        }
        Canvas rootCanvas = settingsPanel != null ? settingsPanel.GetComponentInParent<Canvas>() : null;
        if (rootCanvas == null && openSettingButton != null) rootCanvas = openSettingButton.GetComponentInParent<Canvas>();
        if (rootCanvas != null && rootCanvas.sortingOrder < 2000)
            rootCanvas.sortingOrder = 2000;

        if (rootCanvas == null) return;
        RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        // 2. Đọc vùng an toàn của đúng máy đang chạy
        safe.xMin = Mathf.Clamp(safe.xMin, 0, screenSize.x);
        safe.xMax = Mathf.Clamp(safe.xMax, safe.xMin, screenSize.x);
        safe.yMin = Mathf.Clamp(safe.yMin, 0, screenSize.y);
        safe.yMax = Mathf.Clamp(safe.yMax, safe.yMin, screenSize.y);
        float safeRightPx = screenSize.x - safe.xMax;
        float safeTopPx = screenSize.y - safe.yMax;

        // 3. Đổi px màn hình -> đơn vị canvas (đúng cả khi có CanvasScaler)
        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize.x <= 0 || canvasSize.y <= 0) return;
        float scaleX = canvasSize.x / screenSize.x;
        float scaleY = canvasSize.y / screenSize.y;

        const float padPx = 20f;
        if (openSettingButton != null)
        {
            RectTransform btn = openSettingButton.GetComponent<RectTransform>();
            btn.anchorMin = btn.anchorMax = Vector2.one;
            btn.pivot = new Vector2(0.5f, 0.5f);
            btn.anchoredPosition = new Vector2(
                -(btn.rect.width * btn.localScale.x * 0.5f + (padPx + safeRightPx) * scaleX),
                -(btn.rect.height * btn.localScale.y * 0.5f + (padPx + safeTopPx) * scaleY));
        }

        RectTransform panel = settingsPanel != null ? settingsPanel.GetComponent<RectTransform>() : null;
        if (panel == null) return;
        // The background still catches clicks over the whole screen. All controls
        // live in a separate safe-area card, with no inherited template scaling.
        panel.localScale = Vector3.one;
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        if (settingsSafeContent == null)
        {
            settingsSafeContent = new GameObject("SettingsSafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            settingsSafeContent.SetParent(panel, false);
            settingsCard = new GameObject("SettingsCard", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            settingsCard.SetParent(settingsSafeContent, false);
            Image card = settingsCard.GetComponent<Image>();
            card.color = new Color(0.94f, 0.94f, 0.94f, 0.98f);
            card.raycastTarget = false;
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
                if (text.transform.parent == panel) { settingsHeading = text; break; }
            if (settingsHeading == null) settingsHeading = NewSettingsLabel("SettingsHeading", "CÀI ĐẶT");
            musicHeading = NewSettingsLabel("MusicHeading", "Nhạc nền");
            fpsHeading = NewSettingsLabel("FPSHeading", "Giới hạn FPS");
            foreach (Toggle toggle in rootCanvas.GetComponentsInChildren<Toggle>(true))
                if (toggle.name == "CrosshairToggle" || toggle.name == "JoystickToggle") settingsExtraToggles.Add(toggle);
        }
        settingsSafeContent.anchorMin = new Vector2(safe.xMin / screenSize.x, safe.yMin / screenSize.y);
        settingsSafeContent.anchorMax = new Vector2(safe.xMax / screenSize.x, safe.yMax / screenSize.y);
        settingsSafeContent.offsetMin = new Vector2(24f * scaleX, 24f * scaleY);
        settingsSafeContent.offsetMax = -settingsSafeContent.offsetMin;
        settingsSafeContent.localScale = Vector3.one;
        Vector2 available = settingsSafeContent.rect.size;
        bool portrait = available.y > available.x;
        float extraHeight = settingsExtraToggles.Count * (portrait ? 80f : 56f);
        Vector2 design = portrait ? new Vector2(560f, 822f + extraHeight) : new Vector2(880f, 470f + extraHeight);
        float fit = Mathf.Max(0.01f, Mathf.Min(available.x / design.x, available.y / design.y));
        fit = Mathf.Min(fit, Mathf.Max(1f, 1f / Mathf.Max(0.01f, rootCanvas.scaleFactor)));
        settingsCard.anchorMin = settingsCard.anchorMax = settingsCard.pivot = new Vector2(0.5f, 0.5f);
        settingsCard.anchoredPosition = Vector2.zero;
        settingsCard.sizeDelta = design;
        settingsCard.localScale = Vector3.one * fit;
        PlaceSettingsControl(settingsHeading.rectTransform, new Vector2(0, -48), new Vector2(design.x - 40, 60));
        FormatSettingsText(settingsHeading, 36f);
        float sliderX = portrait ? 0f : -210f;
        float width = portrait ? 470f : 340f;
        PlaceSettingsSlider(masterSlider, new Vector2(sliderX, portrait ? -174 : -154), width);
        PlaceSettingsSlider(bgmSlider, new Vector2(sliderX, portrait ? -284 : -254), width);
        PlaceSettingsSlider(sfxSlider, new Vector2(sliderX, portrait ? -394 : -354), width);
        float dropdownX = portrait ? 0f : 210f;
        PlaceSettingsControl(musicHeading.rectTransform, new Vector2(dropdownX, portrait ? -466 : -110), new Vector2(width, 38));
        PlaceSettingsControl(fpsHeading.rectTransform, new Vector2(dropdownX, portrait ? -578 : -250), new Vector2(width, 38));
        FormatSettingsText(musicHeading, 28f);
        FormatSettingsText(fpsHeading, 28f);
        PlaceSettingsDropdown(bgmDropdown, new Vector2(dropdownX, portrait ? -518 : -166), width);
        PlaceSettingsDropdown(fpsDropdown, new Vector2(dropdownX, portrait ? -630 : -306), width);
        for (int i = 0; i < settingsExtraToggles.Count; i++)
        {
            Toggle toggle = settingsExtraToggles[i];
            PlaceSettingsControl((RectTransform)toggle.transform, new Vector2(dropdownX, portrait ? -704 - i * 80f : -396 - i * 56f), new Vector2(width, 48));
            Image hitArea = toggle.GetComponent<Image>();
            if (hitArea == null) hitArea = toggle.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            if (toggle.targetGraphic != null && toggle.targetGraphic.transform.parent == toggle.transform)
            {
                RectTransform box = toggle.targetGraphic.rectTransform;
                box.anchorMin = box.anchorMax = new Vector2(0, 0.5f);
                box.pivot = new Vector2(0, 0.5f);
                box.anchoredPosition = Vector2.zero;
                box.sizeDelta = new Vector2(40, 40);
                box.localScale = Vector3.one;
            }
            foreach (Text label in toggle.GetComponentsInChildren<Text>(true))
            {
                RectTransform rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.15f, 0);
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                label.fontSize = 28;
                label.color = Color.black;
                label.alignment = TextAnchor.MiddleLeft;
                label.raycastTarget = false;
            }
        }
        PlaceSettingsButton(closeSettingButton, new Vector2(0, (portrait ? -704 : -418) - extraHeight), portrait ? 470f : 230f);
        PlaceSettingsButton(mainMenuButton, new Vector2(portrait ? -125f : -260f, (portrait ? -780 : -418) - extraHeight), 230f);
        PlaceSettingsButton(quitButton, new Vector2(portrait ? 125f : 260f, (portrait ? -780 : -418) - extraHeight), 230f);
    }

    private TMP_Text NewSettingsLabel(string name, string text)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(settingsCard, false);
        if (bgmDropdown != null && bgmDropdown.captionText != null) label.font = bgmDropdown.captionText.font;
        label.text = text;
        label.color = Color.black;
        return label;
    }

    private void PlaceSettingsControl(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        if (rect.parent != settingsCard) rect.SetParent(settingsCard, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void FormatSettingsText(TMP_Text text, float size)
    {
        text.fontSize = text.fontSizeMax = size;
        text.fontSizeMin = 18f;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.color = Color.black;
    }

    private void PlaceSettingsSlider(Slider slider, Vector2 position, float width)
    {
        if (slider == null) return;
        PlaceSettingsControl((RectTransform)slider.transform, position, new Vector2(width, 44));
        foreach (TMP_Text text in slider.GetComponentsInChildren<TMP_Text>(true))
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 48);
            rect.sizeDelta = new Vector2(width, 38);
            rect.localScale = Vector3.one;
            FormatSettingsText(text, 28f);
        }
    }

    private void PlaceSettingsDropdown(TMP_Dropdown dropdown, Vector2 position, float width)
    {
        if (dropdown == null) return;
        PlaceSettingsControl((RectTransform)dropdown.transform, position, new Vector2(width, 72));
        if (dropdown.captionText != null) FormatSettingsText(dropdown.captionText, 28f);
        if (dropdown.template != null)
        {
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 128f);
            if (dropdown.itemText != null) FormatSettingsText(dropdown.itemText, 26f);
        }
    }

    private void PlaceSettingsButton(Button button, Vector2 position, float width)
    {
        if (button == null) return;
        PlaceSettingsControl((RectTransform)button.transform, position, new Vector2(width, 72));
        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true)) FormatSettingsText(text, 28f);
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioManager.SaveMasterVolume(value);
    }

    private void OnBGMVolumeChanged(float value)
    {
        AudioManager.SaveBGMVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        AudioManager.SaveSFXVolume(value);
    }

    private readonly List<AudioClip> dropdownClips = new List<AudioClip>();

    private void SyncAudioSettingsUI()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(AudioManager.MasterVolume);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(AudioManager.BGMVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioManager.SFXVolume);
        SetupBGMDropdown();
    }

    // Keep a clip mapping for this scene's order, not the manager's indices.
    private void SetupBGMDropdown()
    {
        if (bgmDropdown == null) return;

        bgmDropdown.onValueChanged.RemoveListener(OnBGMSelected);
        bgmDropdown.ClearOptions();
        dropdownClips.Clear();
        List<string> options = new List<string>();

        // Ưu tiên 1: Lấy danh sách nhạc tự kéo trong Inspector của Scene này
        if (localBGMList != null && localBGMList.Count > 0)
        {
            dropdownClips.AddRange(localBGMList);
        }
        // Ưu tiên 2: Nếu không kéo nhạc riêng thì lấy từ AudioManager (nếu có)
        else if (AudioManager.Instance != null && AudioManager.Instance.bgmClips != null)
        {
            dropdownClips.AddRange(AudioManager.Instance.bgmClips);
        }

        AudioClip selected = AudioManager.SelectedBGM;
        if (selected != null && !dropdownClips.Contains(selected)) dropdownClips.Add(selected);
        foreach (AudioClip clip in dropdownClips)
            options.Add(clip != null ? clip.name : "Không có nhạc");
        if (options.Count == 0) options.Add("Không có nhạc");

        bgmDropdown.AddOptions(options);
        bgmDropdown.SetValueWithoutNotify(Mathf.Max(0, dropdownClips.IndexOf(selected)));
        bgmDropdown.RefreshShownValue();
        bgmDropdown.onValueChanged.AddListener(OnBGMSelected);
    }

    private void OnBGMSelected(int index)
    {
        if (index >= 0 && index < dropdownClips.Count)
            AudioManager.SelectBGM(dropdownClips[index]);
        // Invalid/null entries leave both playback and the displayed selection unchanged.
        SyncAudioSettingsUI();
    }

    // --- NÚT ĐIỀU HƯỚNG: tự tìm nút trong panel theo tên, thiếu thì tự tạo clone ---
    private void WireNavButtons()
    {
        if (mainMenuButton == null)
            mainMenuButton = FindButtonInPanel("mainmenu", "main menu", "ve menu", "về menu", "home");
        if (quitButton == null)
            quitButton = FindButtonInPanel("quit", "exit", "thoat", "thoát");

        // Scene cũ chưa có 2 nút này -> tự đẻ từ nút Close có sẵn để PC/Mobile/VR
        // đều có đủ "Về Main Menu / Thoát game" mà không cần sửa scene tay.
        EnsureNavButtons();

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    // Clone nút Close có sẵn thành 2 nút điều hướng. Chạy được mọi nền tảng:
    // PC bấm chuột, Mobile chạm, VR bấm bằng ray + trigger (qua VRUIInputBridge).
    private void EnsureNavButtons()
    {
        if (settingsPanel == null || closeSettingButton == null) return;
        RectTransform template = closeSettingButton.GetComponent<RectTransform>();
        if (template == null) return;

        // Hàng dưới Close, đối xứng 2 bên qua trục x của nút Close:
        // MainMenu lệch trái, Quit lệch phải, cùng y. Nút làm rộng hơn nút Close
        // để chữ "VỀ MAIN MENU" vừa 1 dòng.
        // Nút giữ rộng gốc (220), khe giữa nới +200px so với bản khít (40 -> 240).
        Vector2 navSize = new Vector2(220f, 36f);
        if (mainMenuButton == null)
        {
            mainMenuButton = CloneNavButton(closeSettingButton, "MainMenuButton",
                "VỀ MENU", new Vector2(-230f, -80f), navSize);
        }
        if (quitButton == null)
        {
            quitButton = CloneNavButton(closeSettingButton, "QuitButton",
                "THOÁT GAME", new Vector2(230f, -80f), navSize);
        }
    }

    private Button CloneNavButton(Button template, string goName, string label, Vector2 offset, Vector2 newSize)
    {
        GameObject go = Instantiate(template.gameObject, settingsPanel.transform);
        go.name = goName;
        go.SetActive(true);

        Button btn = go.GetComponent<Button>();
        if (btn != null) btn.onClick.RemoveAllListeners(); // xóa listener ClosePanel copy theo

        RectTransform rt = go.GetComponent<RectTransform>();
        RectTransform tRt = template.GetComponent<RectTransform>();
        if (rt != null && tRt != null)
        {
            rt.anchorMin = tRt.anchorMin;
            rt.anchorMax = tRt.anchorMax;
            rt.pivot = tRt.pivot;
            rt.sizeDelta = newSize;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = tRt.anchoredPosition + offset;
        }

        SetButtonLabel(go, label);
        return btn;
    }

    // Giữ đúng font/màu của nút gốc (clone từ Close), chỉ ép chữ 1 dòng duy nhất:
    // tắt xuống dòng + tự co cỡ chữ cho vừa nút, nên "VỀ MAIN MENU" hay
    // "BẤM LẦN NỮA ĐỂ THOÁT" đều không tràn/xuống dòng.
    private static void SetButtonLabel(GameObject buttonGo, string label)
    {
        TMP_Text tmp = buttonGo.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = label;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            float max = tmp.fontSize > 0 ? tmp.fontSize : 24f;
            tmp.fontSizeMax = max;
            tmp.fontSizeMin = Mathf.Min(14f, max);
            tmp.enableAutoSizing = true;
            return;
        }
        Text legacy = buttonGo.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            legacy.text = label;
            legacy.alignment = TextAnchor.MiddleCenter;
            legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacy.verticalOverflow = VerticalWrapMode.Overflow;
            legacy.resizeTextForBestFit = true;
            legacy.resizeTextMaxSize = legacy.fontSize > 0 ? legacy.fontSize : 24;
            legacy.resizeTextMinSize = 14;
        }
    }

    private Button FindButtonInPanel(params string[] keywords)
    {
        if (settingsPanel == null) return null;
        Button[] buttons = settingsPanel.GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            if (b == null || b.gameObject == null) continue;
            // Bỏ qua 2 nút đóng/mở đã có để không bắt nhầm.
            if (b == openSettingButton || b == closeSettingButton) continue;
            string n = b.gameObject.name.ToLower();
            foreach (string k in keywords)
            {
                if (!string.IsNullOrEmpty(k) && n.Contains(k.ToLower()))
                    return b;
            }
        }
        return null;
    }

    // --- VỀ MAIN MENU: dùng chung PC / Mobile. VR giữ kính theo đề xuất bên dưới. ---
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;

        // Ngắt thuyết minh tranh (AudioManager sống xuyên scene).
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopVoiceover();

        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null) controls.SetGameplayInputEnabled(true);
        if (crosshair != null) crosshair.SetForceHidden(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Đang ở sẵn MainMenu -> chỉ đóng panel, không load lại.
        if (SceneManager.GetActiveScene().name == mainMenuSceneName)
        {
            ClosePanel();
            return;
        }

        // PHƯƠNG ÁN VR (đề xuất):
        // - Mặc định (exitVRWhenToMainMenu = false, khuyên dùng cho Quest standalone):
        //   GIỮ XR, chỉ LoadScene. Menu MainMenu đã có VRUIInputBridge nên vẫn bấm
        //   bằng ray + trigger, không tốn 2-5s deinit/init lại, không risk đen màn.
        // - Nếu là PCVR muốn về desktop dùng chuột (tick exitVRWhenToMainMenu = true,
        //   hoặc gọi ExitVRAndGoToMainMenu()): StopXR trước rồi LoadScene, menu chạy phẳng nhẹ.
        if (PlatformHelper.IsXRDisplayRunning() && exitVRWhenToMainMenu)
        {
            XRBoot.StopXR();
        }

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError("[Settings] Scene không có trong Build Settings: " + mainMenuSceneName);
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Nút riêng cho PCVR: thoát kính về phẳng rồi mới về menu (gọi từ OnClick).
    public void ExitVRAndGoToMainMenu()
    {
        XRBoot.StopXR();
        bool keep = exitVRWhenToMainMenu;
        exitVRWhenToMainMenu = false; // đã stop rồi, tránh stop 2 lần trong GoToMainMenu
        GoToMainMenu();
        exitVRWhenToMainMenu = keep;
    }

    // Chống bấm nhầm nút Thoát khi đang đeo kính: VR phải bấm 2 lần trong 3s.
    private float quitArmTime = -10f;
    private bool quitArmed;
    private const float QuitConfirmWindow = 3f;

    // --- THOÁT HẲN GAME: dùng chung PC / Mobile / VR (Quest = về Quest Home). ---
    // VR gọi đúng hàm này (ray + trigger qua VRUIInputBridge), lần 1 hiện xác nhận,
    // lần 2 mới thoát thật.
    public void QuitGame()
    {
        if (PlatformHelper.IsXRDisplayRunning() &&
            Time.unscaledTime - quitArmTime > QuitConfirmWindow)
        {
            quitArmTime = Time.unscaledTime;
            quitArmed = true;
            if (quitButton != null) SetButtonLabel(quitButton.gameObject, "BẤM LẦN NỮA ĐỂ THOÁT");
            Debug.Log("[Settings] Bấm Thoát lần nữa trong 3s để thoát hẳn (chống bấm nhầm trong VR).");
            return;
        }
        quitArmed = false;
        DoQuitNow();
    }

    private void DoQuitNow()
    {
#if UNITY_EDITOR
        Debug.Log("[Settings] Thoát game (Editor: dừng Play Mode).");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Debug.Log("Đã thoát triển lãm!");
        Application.Quit();
#endif
    }

    // Hàm tường minh cho VR (gắn vào nút VR riêng nếu designer muốn tách nút
    // PC/Mobile và VR): giữ kính + về menu, hoặc thoát hẳn.
    public void VRGoToMainMenu() { GoToMainMenu(); }
    public void VRQuitGame() { QuitGame(); }

    private void SetupFPSDropdown()
    {
        if (fpsDropdown == null) return;

        fpsDropdown.onValueChanged.RemoveAllListeners();
        fpsDropdown.ClearOptions();
        fpsValues.Clear();

        // Mobile (điện thoại/tablet): BỎ 90 FPS. Đa số máy 60Hz, ép 90 gây nóng máy,
        // hao pin, frame trồi sụt; cần mượt thì chọn "Không giới hạn" để máy tự lên
        // theo màn hình (60/90/120Hz) thay vì ép cứng 90.
        // PC: giữ đủ 30/60/90/Không giới hạn.
        List<string> fpsOptions;
        if (PlatformHelper.IsTouchDevice())
        {
            fpsOptions = new List<string> { "30 FPS", "60 FPS", "Không giới hạn" };
            fpsValues.Add(30);
            fpsValues.Add(60);
            fpsValues.Add(-1);
        }
        else
        {
            fpsOptions = new List<string> { "30 FPS", "60 FPS", "90 FPS", "Không giới hạn" };
            fpsValues.Add(30);
            fpsValues.Add(60);
            fpsValues.Add(90);
            fpsValues.Add(-1);
        }

        fpsDropdown.AddOptions(fpsOptions);

        fpsDropdown.SetValueWithoutNotify(1); // mặc định 60 FPS
        // Đang cắm kính (Quest / Link / PCVR): bỏ cap FPS để compositor pacing
        // theo tần số kính (72/90/120Hz). Cap 60 trên kính 72Hz+ gây giật đều
        // dù đồng hồ FPS báo đủ.
        if (PlatformHelper.IsXRDisplayRunning())
            fpsDropdown.SetValueWithoutNotify(fpsOptions.Count - 1);
        fpsDropdown.RefreshShownValue();
        fpsDropdown.onValueChanged.AddListener(OnFPSSelected);
        OnFPSSelected(fpsDropdown.value);
    }

    // XR bật muộn (sau Start) -> ép dropdown về "Không giới hạn".
    private void RefreshFPSDropdownForXR()
    {
        if (fpsDropdown == null || fpsValues.Count == 0) return;
        int last = fpsValues.Count - 1;
        fpsDropdown.SetValueWithoutNotify(last);
        fpsDropdown.RefreshShownValue();
        OnFPSSelected(last);
    }

    public void OnFPSSelected(int index)
    {
        QualitySettings.vSyncCount = 0;

        int fps = 60;
        if (index >= 0 && index < fpsValues.Count)
            fps = fpsValues[index];

        // Trong VR luôn để không giới hạn, bất kể dropdown đang chọn gì.
        if (PlatformHelper.IsXRDisplayRunning())
            fps = -1;

        Application.targetFrameRate = fps;

        // Ép lại dropdown hiển thị đúng cái vừa chọn (fix TMP_Dropdown không refresh
        // khi panel đang tắt lúc Start, nhìn tưởng vẫn kẹt ở 60).
        // Map ngược giá trị -> index đúng cho cả list mobile (không có 90) và PC.
        int wantIndex = fpsValues.IndexOf(fps);
        if (PlatformHelper.IsXRDisplayRunning())
            wantIndex = fpsValues.Count - 1;
        if (fpsDropdown != null && wantIndex >= 0 && fpsDropdown.value != wantIndex)
        {
            fpsDropdown.SetValueWithoutNotify(wantIndex);
            fpsDropdown.RefreshShownValue();
        }
    }
}
