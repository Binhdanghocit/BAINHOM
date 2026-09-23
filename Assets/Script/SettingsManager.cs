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
        if (GetComponent<VRUIInputBridge>() == null)
        {
            gameObject.AddComponent<VRUIInputBridge>();
        }
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
            masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // Cấu hình BGM Dropdown
        SetupBGMDropdown();

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
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
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
            settingsPanel.SetActive(true);
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
            MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
            if (controls != null) controls.SetGameplayInputEnabled(true);

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
                // Về game: PC khóa lại chuột, điện thoại giữ nguyên cảm ứng
                PlatformHelper.SetCursorLocked(true);
            }
        }
    }

    // Lấy đúng màn hình từng máy (kể cả tai thỏ) rồi neo nút Setting vào góc phải-trên.
    // Fix gốc: nút cũ neo (1,0.5) + y=500 nên màn thấp là tràn mất; Canvas Sort=0 nên bị Mobile (1000) đè.
    private void FixSettingsUIForAllScreens()
    {
        // 1. Luôn vẽ Settings trên cùng (cao hơn canvas Mobile runtime order 1000)
        if (settingsPanel != null)
        {
            Canvas panelCanvas = settingsPanel.GetComponentInParent<Canvas>();
            if (panelCanvas != null && panelCanvas.sortingOrder < 2000)
                panelCanvas.sortingOrder = 2000;
        }
        if (openSettingButton == null) return;
        Canvas rootCanvas = openSettingButton.GetComponentInParent<Canvas>();
        if (rootCanvas != null && rootCanvas.sortingOrder < 2000)
            rootCanvas.sortingOrder = 2000;

        RectTransform btn = openSettingButton.GetComponent<RectTransform>();
        if (btn == null || rootCanvas == null) return;
        RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        // 2. Đọc vùng an toàn của đúng máy đang chạy
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0)
            safe = new Rect(0, 0, Screen.width, Screen.height);
        float safeRightPx = Screen.width - (safe.x + safe.width);
        float safeTopPx = Screen.height - (safe.y + safe.height);

        // 3. Đổi px màn hình -> đơn vị canvas (đúng cả khi có CanvasScaler)
        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize.x <= 0 || canvasSize.y <= 0) return;
        float scaleX = canvasSize.x / Mathf.Max(1, Screen.width);
        float scaleY = canvasSize.y / Mathf.Max(1, Screen.height);

        const float padPx = 20f;
        btn.anchorMin = new Vector2(1f, 1f);
        btn.anchorMax = new Vector2(1f, 1f);
        btn.pivot = new Vector2(0.5f, 0.5f);
        btn.anchoredPosition = new Vector2(
            -(btn.sizeDelta.x * 0.5f + (padPx + safeRightPx) * scaleX),
            -(btn.sizeDelta.y * 0.5f + (padPx + safeTopPx) * scaleY));
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
            rt.localScale = tRt.localScale;
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
            tmp.enableWordWrapping = false;
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
