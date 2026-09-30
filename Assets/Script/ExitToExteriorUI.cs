using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ExitToExteriorUI : MonoBehaviour
{
    [Header("Tên Scene")]
    [Tooltip("Nhập đúng tên Scene ngoại cảnh (phải có trong Build Settings)")]
    public string exteriorSceneName = "ExteriorScene";

    [Header("Phím tắt")]
    [Tooltip("Phím mở/đóng dialog xác nhận thoát (mặc định ESC)")]
    public KeyCode toggleKey = KeyCode.Escape;

    // Runtime references
    private GameObject dialogRoot;
    private bool isDialogOpen = false;
    private bool isLoading = false;

    public static bool IsAnyOpen { get; private set; }

    // ─────────────────────────────────────────
    private void Awake()
    {
        BuildDialog();
        BuildMobileMenuButton(); // Nút ☰ chỉ hiện trên Mobile, tự ẩn trên PC/VR
        IsAnyOpen = false;
    }

    private void OnDisable()
    {
        IsAnyOpen = false;
    }

    private void Update()
    {
        // ESC và nút Menu VR đã được SettingsManager xử lý (mở Settings).
        // ExitToExteriorUI chỉ mở khi bấm nút ☰ trên Mobile hoặc gọi ToggleDialog() trực tiếp.
        // Không bắt ESC ở đây để tránh xung đột — bấm ESC chỉ mở Settings, không mở cả 2.
    }

    // ─────────────────────────────────────────
    //  NÚT "QUAY LẠI NGOÀI"
    // ─────────────────────────────────────────
    public void GoBackOutside()
    {
        if (isLoading) return;

        // Đóng popup tranh / hội thoại NPC nếu đang mở
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
            PaintingUIManager.Instance.ClosePopup();
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
            DialogueUIManager.Instance.EndDialogue();

        // Dừng thuyết minh đang phát
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopVoiceover();

        StartCoroutine(LoadExteriorAsync());
    }

    // ─────────────────────────────────────────
    //  NÚT "Ở LẠI"
    // ─────────────────────────────────────────
    public void StayHere()
    {
        CloseDialog();
    }

    // ─────────────────────────────────────────
    //  TOGGLE / OPEN / CLOSE
    // ─────────────────────────────────────────
    public void ToggleDialog()
    {
        if (isDialogOpen) CloseDialog();
        else OpenDialog();
    }

    private void OpenDialog()
    {
        if (dialogRoot == null) return;
        isDialogOpen = true;
        IsAnyOpen = true;
        dialogRoot.SetActive(true);

        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null) controls.SetGameplayInputEnabled(false);

        PlatformHelper.SetCursorLocked(false); // Hiện chuột để bấm nút
    }

    private void CloseDialog()
    {
        if (dialogRoot == null) return;
        isDialogOpen = false;
        IsAnyOpen = false;
        dialogRoot.SetActive(false);

        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null) controls.SetGameplayInputEnabled(true);

        PlatformHelper.SetCursorLocked(true);  // Khóa lại chuột để điều khiển nhân vật
    }

    // ─────────────────────────────────────────
    //  LOAD SCENE NGOẠI CẢNH (CÓ FADE OUT)
    // ─────────────────────────────────────────
    private IEnumerator LoadExteriorAsync()
    {
        isLoading = true;
        IsAnyOpen = false;

        // Kiểm tra scene có trong Build Settings không
        if (!Application.CanStreamedLevelBeLoaded(exteriorSceneName))
        {
            Debug.LogError("[ExitUI] Scene không có trong Build Settings: " + exteriorSceneName);
            isLoading = false;
            yield break;
        }

        // Fade out: tối dần màn hình trong 0.5 giây
        yield return StartCoroutine(FadeOut(0.5f));

        // Load scene ngoại cảnh
        SceneManager.LoadScene(exteriorSceneName);
    }

    // ─────────────────────────────────────────
    //  HIỆU ỨNG FADE OUT (MÀN HÌNH TỐI DẦN)
    // ─────────────────────────────────────────
    private IEnumerator FadeOut(float duration)
    {
        // Tạo overlay đen phủ toàn màn hình (tự huỷ khi sang Scene mới, KHÔNG dùng DontDestroyOnLoad)
        var fadeGo = new GameObject("FadeOverlay");
        var fadeCanvas = fadeGo.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999;

        var fadeImage = fadeGo.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        var rt = fadeGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Tăng alpha từ 0 → 1
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
    }

    // ─────────────────────────────────────────
    //  TỰ DỰNG UI BẰNG CODE (không cần sửa Scene)
    // ─────────────────────────────────────────
    private void BuildDialog()
    {
        // Canvas
        var canvasGo = new GameObject("ExitDialogCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Overlay mờ phủ nền
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.55f);
        var overlayRt = overlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        // Hộp dialog chính (nền tối bo góc giả)
        var box = new GameObject("DialogBox");
        box.transform.SetParent(canvasGo.transform, false);
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.1f, 0.1f, 0.15f, 0.97f);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(620f, 280f);

        // Icon trang trí
        var icon = new GameObject("Icon");
        icon.transform.SetParent(box.transform, false);
        var iconTxt = icon.AddComponent<TextMeshProUGUI>();
        iconTxt.text = "🚪";
        iconTxt.fontSize = 52;
        iconTxt.alignment = TextAlignmentOptions.Center;
        var iconRt = iconTxt.rectTransform;
        iconRt.anchorMin = new Vector2(0.5f, 1f);
        iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.sizeDelta = new Vector2(100f, 70f);
        iconRt.anchoredPosition = new Vector2(0f, -50f);

        // Tiêu đề
        var title = new GameObject("Title");
        title.transform.SetParent(box.transform, false);
        var titleTxt = title.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "Rời khỏi Triển Lãm?";
        titleTxt.fontSize = 34;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = Color.white;
        titleTxt.alignment = TextAlignmentOptions.Center;
        var titleRt = titleTxt.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(560f, 55f);
        titleRt.anchoredPosition = new Vector2(0f, -120f);

        // Mô tả
        var desc = new GameObject("Desc");
        desc.transform.SetParent(box.transform, false);
        var descTxt = desc.AddComponent<TextMeshProUGUI>();
        descTxt.text = "Bạn có muốn quay lại sảnh chờ bên ngoài không?";
        descTxt.fontSize = 24;
        descTxt.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        descTxt.alignment = TextAlignmentOptions.Center;
        descTxt.textWrappingMode = TextWrappingModes.Normal;
        var descRt = descTxt.rectTransform;
        descRt.anchorMin = new Vector2(0.5f, 1f);
        descRt.anchorMax = new Vector2(0.5f, 1f);
        descRt.sizeDelta = new Vector2(540f, 50f);
        descRt.anchoredPosition = new Vector2(0f, -175f);

        // Nút "Quay Lại Ngoài" (màu đỏ cam)
        CreateButton(box.transform,
            text: "🚪  Quay Lại Ngoài",
            anchoredPos: new Vector2(-155f, -235f),
            size: new Vector2(270f, 58f),
            normalColor: new Color(0.82f, 0.25f, 0.18f, 1f),
            onClick: GoBackOutside);

        // Nút "Ở Lại" (màu xanh lá)
        CreateButton(box.transform,
            text: "✅  Ở Lại",
            anchoredPos: new Vector2(155f, -235f),
            size: new Vector2(270f, 58f),
            normalColor: new Color(0.18f, 0.6f, 0.25f, 1f),
            onClick: StayHere);

        dialogRoot = canvasGo;
        dialogRoot.SetActive(false); // Ẩn ban đầu
    }

    private void CreateButton(Transform parent, string text, Vector2 anchoredPos,
        Vector2 size, Color normalColor, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject(text.Replace(" ", "_"));
        btnGo.transform.SetParent(parent, false);

        var img = btnGo.AddComponent<Image>();
        img.color = normalColor;

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = normalColor * 1.2f;
        colors.pressedColor = normalColor * 0.8f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 26;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        var labelRt = label.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
    }

    // ─────────────────────────────────────────
    //  NÚT MENU NHỎ GÓC TRÊN TRÁI (chỉ hiện trên Mobile)
    //  Thay thế phím ESC cho người chơi điện thoại
    // ─────────────────────────────────────────
    private void BuildMobileMenuButton()
    {
        if (!PlatformHelper.IsTouchDevice()) return;

        var btnCanvasGo = new GameObject("MobileMenuButtonCanvas");
        btnCanvasGo.transform.SetParent(transform, false);
        var btnCanvas = btnCanvasGo.AddComponent<Canvas>();
        btnCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        btnCanvas.sortingOrder = 99;
        var sc = btnCanvasGo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        btnCanvasGo.AddComponent<GraphicRaycaster>();

        // Nút ☰ góc trên bên trái
        var btnGo = new GameObject("MobileMenuBtn");
        btnGo.transform.SetParent(btnCanvasGo.transform, false);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);

        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(ToggleDialog);

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(110f, 80f);
        rt.anchoredPosition = new Vector2(20f, -20f);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var lbl = labelGo.AddComponent<TextMeshProUGUI>();
        lbl.text = "☰ Menu";
        lbl.fontSize = 26;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.Center;
        var lblRt = lbl.rectTransform;
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
    }
}
