using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public sealed class ColoringRegionDataAsset
{
    public int width;
    public int height;
    public int regionCount;
    public ColoringRegionSpanItem[] regions = Array.Empty<ColoringRegionSpanItem>();
}

[Serializable]
public struct ColoringRegionSpanItem
{
    public int id;
    public int pixelCount;
    public ColoringPixelSpan[] spans;
}

[Serializable]
public struct ColoringPixelSpan
{
    public int start;
    public int length;
}

[Serializable]
public sealed class ColoringArtworkDefinition
{
    public string title;
    public Texture2D lineArt;
    public Texture2D referenceArt;
    public Texture2D paintMask;
    [Tooltip("Dữ liệu vùng tô sinh sẵn từ Editor (TextAsset JSON). Nếu để trống, workshop sẽ tự nhận diện lúc mở.")]
    public TextAsset regionData;
    [Min(0.05f)] public float referenceScale = 1f;
    [Tooltip("Dịch ảnh mẫu theo kích thước ảnh nét đã fit trong vùng chứa: X sang phải, Y lên trên. Không thay đổi ảnh nét hoặc mask.")]
    public Vector2 referenceOffset;
    [Min(0)] public int minimumRegionPixels = 80;
    [Range(1, 254)] public int whiteThreshold = 200;
    [Tooltip("Để trống để tự nhận diện vùng trắng kín. Nếu khai báo, mỗi điểm UV chọn một vùng cụ thể.")]
    public ColoringRegionSeed[] regions = Array.Empty<ColoringRegionSeed>();
}

[Serializable]
public struct ColoringRegionSeed
{
    public string id;
    [Tooltip("Tọa độ chuẩn hóa từ góc dưới trái của ảnh nét.")]
    public Vector2 normalizedPoint;
}

/// <summary>Workshop tô màu nhiều tranh, giữ tiến độ riêng trong suốt phiên chơi.</summary>
[RequireComponent(typeof(RawImage))]
public sealed class ColoringPageMinigame : MonoBehaviour, IPointerClickHandler
{
    [Header("Danh sách tranh — có thể để trống")]
    public ColoringArtworkDefinition[] paintings = Array.Empty<ColoringArtworkDefinition>();
    public int initialPaintingIndex;

    [Header("Giao diện")]
    public RawImage coloringImage;
    [Tooltip("Lớp hiển thị nét đen nằm trên màu tô để đường viền không bao giờ bị màu đè mất.")]
    public RawImage lineArtOverlayImage;
    public RawImage referenceImage;
    public RectTransform picturesArea;
    public RectTransform safeAreaRect;
    public RectTransform coloringImageFrame;
    public RectTransform referenceImageFrame;
    public AspectRatioFitter coloringAspectFitter;
    public AspectRatioFitter referenceAspectFitter;
    public TextMeshProUGUI paintingTitleText;
    public TextMeshProUGUI referenceMessageText;
    public TextMeshProUGUI emptyStateText;
    public GameObject paintingContent;
    public GameObject finishPanel;
    public GameObject workshopPanel;
    public DoorMenuTrigger doorTrigger;
    public SettingsManager settingsManager;
    public Transform paintingButtonContainer;
    public Button paintingButtonTemplate;
    public Image selectedColorIndicator;
    public TextMeshProUGUI progressText;
    public Button[] paletteButtons;
    public Button resetButton;
    public Button optionsButton;
    public GameObject optionsMenu;
    public Button addPaintingButton;
    public GameObject paintingSelectorPanel;
    public Button selectorCloseButton;
    public TextMeshProUGUI selectorEmptyText;
    public Button settingsButton;
    public Button returnToGalleryButton;
    public Button finishResetButton;
    public Button finishCloseButton;
    public Button toggleReferenceButton;

    [Header("Âm thanh không bắt buộc")]
    public AudioClip paintSound;
    public AudioClip finishSound;

    public Color[] palette =
    {
        new Color(0.10f, 0.10f, 0.10f), // đen
        new Color(0.72f, 0.12f, 0.10f), // đỏ son
        new Color(0.95f, 0.72f, 0.16f), // vàng
        new Color(0.20f, 0.42f, 0.24f), // xanh lá
        Color.white                     // trắng (trạng thái tô lưu riêng bằng paintedRegions)
    };

    private sealed class PaintingProgress
    {
        public Color32[] pixels;
        public bool[] paintedRegions;
        public int paintedCount;
        public bool complete;
    }

    private readonly List<int> validPaintingIndices = new List<int>();
    private readonly List<Button> generatedPaintingButtons = new List<Button>();
    private PaintingProgress[] savedProgress;
    private int selectedPaintingIndex = -1;
    private int[] componentLabels;
    private int[] componentStarts;
    private int[] componentSizes;
    private int[] regionPixels;
    private bool[] fillableComponents;
    private Color32[] sourcePixels;
    private Color32[] sourceMaskPixels;
    private Color32[] workingPixels;
    private Texture2D workingTexture;
    private Texture2D lineOverlayTexture;
    private ColoringRegionSpanItem[] cachedRegionSpans;
    private int[] pixelToRegion;
    private Color currentColor;
    private int fillableRegionCount;
    private int paintedRegionCount;
    private int backgroundComponent = -1;
    private bool isComplete;
    private int lastLayoutWidth;
    private int lastLayoutHeight;
    private Rect lastSafeArea;
    private bool referenceVisible = true;
    private ThirdPersonCamera playerCameraController;
    private bool playerCameraWasEnabled;
    private bool playerCameraStateCaptured;
    private bool workshopPresentationApplied;
    private Button standardSettingsButton;
    private bool standardSettingsButtonWasActive;
    private Canvas[] settingsCanvases;
    private int[] settingsCanvasOriginalOrders;
    private bool settingsCanvasOrdersCaptured;
    private MobileControlsOverlay mobileControls;
    private bool mobileControlsWereActive;
    private bool mobileInputWasEnabled;
    private CrosshairReticle crosshair;
    private FPSDisplay[] fpsDisplays;
    private bool[] fpsObjectsWereActive;

    private void Awake()
    {
        Canvas.willRenderCanvases += RefreshArtworkLayout;
        ResolveWorkshopUI();
        WireButtons();
        currentColor = palette != null && palette.Length > 0 ? palette[0] : Color.black;
        if (selectedColorIndicator != null) selectedColorIndicator.color = currentColor;
        PreparePaintingList();
        CacheGameplayHud();
        ApplyResponsiveLayout();
    }

    private void ResolveWorkshopUI()
    {
        Transform searchRoot = workshopPanel != null ? workshopPanel.transform : transform.root;
        if (coloringImage == null)
        {
            Transform frame = FindChildRecursive(searchRoot, "ColoringImageFrame");
            if (frame != null)
            {
                Transform art = FindChildRecursive(frame, "Artwork");
                if (art != null) coloringImage = art.GetComponent<RawImage>();
            }
        }
        if (lineArtOverlayImage == null && coloringImage != null)
        {
            Transform existingOverlay = coloringImage.transform.Find("LineArtOverlay");
            if (existingOverlay != null) lineArtOverlayImage = existingOverlay.GetComponent<RawImage>();
            if (lineArtOverlayImage == null)
            {
                GameObject overlayObject = new GameObject("LineArtOverlay", typeof(RectTransform), typeof(RawImage));
                overlayObject.transform.SetParent(coloringImage.transform, false);
                RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
                lineArtOverlayImage = overlayObject.GetComponent<RawImage>();
                lineArtOverlayImage.raycastTarget = false;
            }
        }
        if (paintingTitleText == null)
        {
            Transform title = FindChildRecursive(searchRoot, "PaintingTitle");
            if (title != null) paintingTitleText = title.GetComponent<TextMeshProUGUI>();
            if (paintingTitleText == null && workshopPanel != null)
            {
                GameObject titleObject = new GameObject("PaintingTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObject.transform.SetParent(safeAreaRect != null ? safeAreaRect : searchRoot, false);
                RectTransform titleRect = titleObject.GetComponent<RectTransform>();
                titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.anchoredPosition = new Vector2(0f, -28f);
                titleRect.sizeDelta = new Vector2(720f, 48f);
                paintingTitleText = titleObject.GetComponent<TextMeshProUGUI>();
                paintingTitleText.alignment = TextAlignmentOptions.Center;
                paintingTitleText.fontSize = 24f;
                paintingTitleText.color = new Color(0.92f, 0.78f, 0.38f);
                paintingTitleText.raycastTarget = false;
            }
        }

        if (paintingSelectorPanel == null)
        {
            Transform selector = FindChildRecursive(searchRoot, "PaintingSelectorViewport");
            if (selector != null) paintingSelectorPanel = selector.gameObject;
        }
        if (paintingSelectorPanel != null)
        {
            RectTransform selectorRect = paintingSelectorPanel.GetComponent<RectTransform>();
            if (selectorRect != null && safeAreaRect != null)
            {
                selectorRect.SetParent(safeAreaRect, false);
                selectorRect.anchorMin = new Vector2(0.1f, 0.24f);
                selectorRect.anchorMax = new Vector2(0.9f, 0.76f);
                selectorRect.pivot = new Vector2(0.5f, 0.5f);
                selectorRect.anchoredPosition = Vector2.zero;
                selectorRect.offsetMin = selectorRect.offsetMax = Vector2.zero;
                selectorRect.SetAsLastSibling();
            }
        }

        if (paintingButtonContainer == null && paintingSelectorPanel != null)
        {
            Transform content = FindChildRecursive(paintingSelectorPanel.transform, "Content");
            if (content != null) paintingButtonContainer = content;
        }
        if (paintingButtonTemplate == null && paintingSelectorPanel != null)
        {
            Transform template = FindChildRecursive(paintingSelectorPanel.transform, "PaintingChoiceTemplate");
            if (template != null) paintingButtonTemplate = template.GetComponent<Button>();
        }
        if (selectorCloseButton == null && paintingSelectorPanel != null)
        {
            Transform close = FindChildRecursive(paintingSelectorPanel.transform, "PaintingSelectorClose");
            if (close != null) selectorCloseButton = close.GetComponent<Button>();
        }
        if (selectorEmptyText == null && paintingSelectorPanel != null)
        {
            Transform empty = FindChildRecursive(paintingSelectorPanel.transform, "PaintingSelectorEmpty");
            if (empty != null) selectorEmptyText = empty.GetComponent<TextMeshProUGUI>();
            if (selectorEmptyText == null)
            {
                GameObject emptyObject = new GameObject("PaintingSelectorEmpty", typeof(RectTransform), typeof(TextMeshProUGUI));
                emptyObject.transform.SetParent(paintingSelectorPanel.transform, false);
                RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
                emptyRect.anchorMin = Vector2.zero;
                emptyRect.anchorMax = Vector2.one;
                emptyRect.offsetMin = new Vector2(20f, 20f);
                emptyRect.offsetMax = new Vector2(-20f, -56f);
                selectorEmptyText = emptyObject.GetComponent<TextMeshProUGUI>();
                selectorEmptyText.alignment = TextAlignmentOptions.Center;
                selectorEmptyText.fontSize = 20f;
                selectorEmptyText.color = Color.white;
                selectorEmptyText.raycastTarget = false;
            }
        }
        if (selectorCloseButton == null && paintingSelectorPanel != null)
        {
            GameObject closeObject = new GameObject("PaintingSelectorClose", typeof(RectTransform), typeof(Image), typeof(Button));
            closeObject.transform.SetParent(paintingSelectorPanel.transform, false);
            RectTransform closeRect = closeObject.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-6f, -6f);
            closeRect.sizeDelta = new Vector2(44f, 44f);
            Image closeImage = closeObject.GetComponent<Image>();
            closeImage.color = new Color(0.35f, 0.16f, 0.12f, 0.98f);
            selectorCloseButton = closeObject.GetComponent<Button>();
            selectorCloseButton.targetGraphic = closeImage;
            GameObject closeLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeLabel.transform.SetParent(closeObject.transform, false);
            RectTransform closeLabelRect = closeLabel.GetComponent<RectTransform>();
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = closeLabelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI closeText = closeLabel.GetComponent<TextMeshProUGUI>();
            closeText.text = "×";
            closeText.fontSize = 28f;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;
            closeText.raycastTarget = false;
        }

        if (safeAreaRect != null)
        {
            Transform existing = addPaintingButton != null ? addPaintingButton.transform
                : FindChildRecursive(searchRoot, "AddPaintingButton");
            GameObject buttonObject = existing != null ? existing.gameObject
                : new GameObject("AddPaintingButton", typeof(RectTransform), typeof(Image), typeof(Button));
            if (existing == null) buttonObject.transform.SetParent(safeAreaRect, false);
            else if (buttonObject.transform.parent != safeAreaRect) buttonObject.transform.SetParent(safeAreaRect, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(320f, -122f);
            buttonRect.sizeDelta = new Vector2(170f, 42f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.32f, 0.18f, 0.94f);
            addPaintingButton = buttonObject.GetComponent<Button>();
            addPaintingButton.targetGraphic = image;
            Transform labelTransform = buttonObject.transform.Find("Label");
            TextMeshProUGUI label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
            if (label == null)
            {
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                label = labelObject.GetComponent<TextMeshProUGUI>();
            }
            label.text = "Chọn tranh +";
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;
        if (parent.name == childName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    private void Start()
    {
        if (validPaintingIndices.Count == 0)
        {
            ShowEmptyState("Chưa có tranh để tô. Hãy thêm tên, ảnh nét và ảnh mẫu vào danh sách Paintings trong Inspector.");
            if (addPaintingButton != null) addPaintingButton.interactable = true;
            return;
        }

        int requestedIndex = Mathf.Clamp(initialPaintingIndex, 0, validPaintingIndices.Count - 1);
        SelectPainting(requestedIndex);
    }

    private void Update()
    {
        if (Screen.width != lastLayoutWidth || Screen.height != lastLayoutHeight || Screen.safeArea != lastSafeArea)
            ApplyResponsiveLayout();
    }

    private void LateUpdate()
    {
        UpdateArtworkLayout();
        bool active = workshopPanel != null && workshopPanel.activeInHierarchy
            && (doorTrigger == null || doorTrigger.IsOpen);
        if (!active) return;

        if (!workshopPresentationApplied) ApplyWorkshopPresentation();

        bool settingsOpen = settingsManager != null && settingsManager.IsSettingsOpen();
        SetSettingsCanvasOrders(settingsOpen);
        if (crosshair != null) crosshair.SetForceHidden(true);

        if (doorTrigger != null && doorTrigger.playerController != null)
            doorTrigger.playerController.enabled = false;
        if (playerCameraController == null) ResolvePlayerCameraController();
        if (playerCameraController != null)
        {
            if (!playerCameraStateCaptured)
            {
                playerCameraWasEnabled = playerCameraController.enabled;
                playerCameraStateCaptured = true;
            }
            playerCameraController.enabled = false;
        }
        if (mobileControls != null && MobileControlsOverlay.IsGameplayInputEnabled)
            mobileControls.SetGameplayInputEnabled(false);
        if (settingsManager == null || !settingsManager.IsSettingsOpen())
            PlatformHelper.SetCursorLocked(false);
    }

    private void CacheGameplayHud()
    {
        if (settingsManager == null) settingsManager = FindAnyObjectByType<SettingsManager>();
        if (settingsManager != null)
        {
            standardSettingsButton = settingsManager.openSettingButton;
            var canvases = new List<Canvas>(2);
            AddUniqueCanvas(canvases, standardSettingsButton != null ? standardSettingsButton.GetComponentInParent<Canvas>() : null);
            AddUniqueCanvas(canvases, settingsManager.settingsPanel != null ? settingsManager.settingsPanel.GetComponentInParent<Canvas>() : null);
            settingsCanvases = canvases.ToArray();
        }

        mobileControls = FindAnyObjectByType<MobileControlsOverlay>();
        crosshair = FindAnyObjectByType<CrosshairReticle>();
        fpsDisplays = FindObjectsByType<FPSDisplay>(FindObjectsInactive.Include);
        fpsObjectsWereActive = new bool[fpsDisplays.Length];
        for (int i = 0; i < fpsDisplays.Length; i++)
            fpsObjectsWereActive[i] = fpsDisplays[i] != null && fpsDisplays[i].gameObject.activeSelf;
    }

    private void ResolvePlayerCameraController()
    {
        if (doorTrigger != null && doorTrigger.playerController is PlayerController controller
            && controller.cameraTransform != null)
        {
            playerCameraController = controller.cameraTransform.GetComponent<ThirdPersonCamera>();
        }
        if (playerCameraController == null)
            playerCameraController = FindAnyObjectByType<ThirdPersonCamera>();
    }

    private static void AddUniqueCanvas(List<Canvas> canvases, Canvas canvas)
    {
        if (canvas != null && !canvases.Contains(canvas)) canvases.Add(canvas);
    }

    private void ApplyWorkshopPresentation()
    {
        workshopPresentationApplied = true;
        if (!settingsCanvasOrdersCaptured)
        {
            settingsCanvasOriginalOrders = new int[settingsCanvases.Length];
            for (int i = 0; i < settingsCanvases.Length; i++)
                settingsCanvasOriginalOrders[i] = settingsCanvases[i] != null ? settingsCanvases[i].sortingOrder : 0;
            settingsCanvasOrdersCaptured = true;
        }
        if (standardSettingsButton != null)
        {
            standardSettingsButtonWasActive = standardSettingsButton.gameObject.activeSelf;
            standardSettingsButton.gameObject.SetActive(false);
        }
        for (int i = 0; i < fpsDisplays.Length; i++)
            if (fpsDisplays[i] != null) fpsDisplays[i].gameObject.SetActive(false);

        if (crosshair != null) crosshair.SetForceHidden(true);
        if (mobileControls != null)
        {
            mobileControlsWereActive = mobileControls.gameObject.activeSelf;
            mobileInputWasEnabled = MobileControlsOverlay.IsGameplayInputEnabled;
            mobileControls.SetGameplayInputEnabled(false);
            mobileControls.SetVisible(false);
        }
        SetSettingsCanvasOrders(false);
    }

    private void SetSettingsCanvasOrders(bool settingsOpen)
    {
        if (settingsCanvases == null) return;
        int workshopOrder = GetComponentInParent<Canvas>() != null ? GetComponentInParent<Canvas>().sortingOrder : 1500;
        for (int i = 0; i < settingsCanvases.Length; i++)
        {
            if (settingsCanvases[i] == null) continue;
            settingsCanvases[i].sortingOrder = settingsOpen
                ? Mathf.Max(settingsCanvasOriginalOrders[i], workshopOrder + 100)
                : workshopOrder - 1;
        }
    }

    private void RestoreGameplayHud()
    {
        if (!workshopPresentationApplied) return;
        workshopPresentationApplied = false;
        if (standardSettingsButton != null)
            standardSettingsButton.gameObject.SetActive(standardSettingsButtonWasActive);
        for (int i = 0; i < fpsDisplays.Length; i++)
            if (fpsDisplays[i] != null) fpsDisplays[i].gameObject.SetActive(fpsObjectsWereActive[i]);
        if (crosshair != null) crosshair.SetForceHidden(false);
        if (mobileControls != null)
        {
            mobileControls.SetVisible(mobileControlsWereActive);
            mobileControls.SetGameplayInputEnabled(mobileInputWasEnabled);
        }
        if (settingsCanvases != null)
            for (int i = 0; i < settingsCanvases.Length; i++)
                if (settingsCanvases[i] != null) settingsCanvases[i].sortingOrder = settingsCanvasOriginalOrders[i];
    }

    private void WireButtons()
    {
        if (paletteButtons != null)
        {
            for (int i = 0; i < paletteButtons.Length; i++)
            {
                int paletteIndex = i;
                WireButton(paletteButtons[i], () => SelectPaletteIndex(paletteIndex));
            }
        }
        WireButton(resetButton, ResetPainting);
        WireButton(optionsButton, ToggleOptionsMenu);
        WireButton(addPaintingButton, TogglePaintingSelector);
        WireButton(selectorCloseButton, ClosePaintingSelector);
        WireButton(settingsButton, OpenSettings);
        WireButton(returnToGalleryButton, CloseWorkshop);
        WireButton(finishResetButton, ResetPainting);
        WireButton(finishCloseButton, CloseWorkshop);
        WireButton(toggleReferenceButton, ToggleReferenceImage);
        if (optionsMenu != null) optionsMenu.SetActive(false);
        if (paintingSelectorPanel != null) paintingSelectorPanel.SetActive(false);
        if (addPaintingButton != null) addPaintingButton.gameObject.SetActive(true);
        if (paintingButtonTemplate != null) paintingButtonTemplate.gameObject.SetActive(false);
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void PreparePaintingList()
    {
        validPaintingIndices.Clear();
        if (paintings == null) paintings = Array.Empty<ColoringArtworkDefinition>();
        savedProgress = new PaintingProgress[paintings.Length];
        for (int i = 0; i < paintings.Length; i++)
        {
            if (paintings[i] != null && paintings[i].lineArt != null)
                validPaintingIndices.Add(i);
        }
        RebuildPaintingSelector();
    }

    private void RebuildPaintingSelector()
    {
        foreach (Button oldButton in generatedPaintingButtons)
            if (oldButton != null) Destroy(oldButton.gameObject);
        generatedPaintingButtons.Clear();
        if (paintingButtonContainer == null || paintingButtonTemplate == null) return;

        for (int i = 0; i < validPaintingIndices.Count; i++)
        {
            int selection = i;
            ColoringArtworkDefinition data = paintings[validPaintingIndices[i]];
            Button button = Instantiate(paintingButtonTemplate, paintingButtonContainer);
            button.gameObject.SetActive(true);
            button.name = "PaintingChoice_" + validPaintingIndices[i];
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = string.IsNullOrWhiteSpace(data.title) ? data.lineArt.name : data.title;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectPainting(selection));
            generatedPaintingButtons.Add(button);
        }
    }

    public void SelectPainting(int validListIndex)
    {
        if (validListIndex < 0 || validListIndex >= validPaintingIndices.Count) return;
        SaveCurrentProgress();
        ReleaseWorkingTexture();

        selectedPaintingIndex = validPaintingIndices[validListIndex];
        ColoringArtworkDefinition data = paintings[selectedPaintingIndex];
        fillableRegionCount = 0;
        paintedRegionCount = 0;
        isComplete = false;
        backgroundComponent = -1;
        sourcePixels = null;
        sourceMaskPixels = null;
        workingPixels = null;
        componentLabels = componentStarts = componentSizes = regionPixels = null;
        fillableComponents = null;

        if (paintingContent != null) paintingContent.SetActive(true);
        if (paintingSelectorPanel != null) paintingSelectorPanel.SetActive(false);
        if (addPaintingButton != null) addPaintingButton.gameObject.SetActive(true);
        if (selectorEmptyText != null) selectorEmptyText.gameObject.SetActive(false);
        if (coloringImage != null) coloringImage.enabled = true;
        if (coloringImageFrame != null) coloringImageFrame.gameObject.SetActive(true);
        if (referenceImageFrame != null) referenceImageFrame.gameObject.SetActive(true);
        if (emptyStateText != null) emptyStateText.gameObject.SetActive(false);
        if (paintingTitleText != null)
            paintingTitleText.text = "Tranh đang tô: " + (string.IsNullOrWhiteSpace(data.title) ? data.lineArt.name : data.title);
        if (referenceImage != null)
        {
            referenceImage.texture = data.referenceArt;
            referenceImage.gameObject.SetActive(data.referenceArt != null || referenceMessageText != null);
            referenceImage.color = Color.white;
            referenceImage.uvRect = new Rect(0f, 0f, 1f, 1f);

        }
        if (referenceMessageText != null)
        {
            referenceMessageText.text = data.referenceArt == null ? "Ảnh mẫu hoàn thiện chưa được thêm." : string.Empty;
            referenceMessageText.gameObject.SetActive(data.referenceArt == null);
        }

        try
        {
            sourcePixels = data.lineArt.GetPixels32();
            if (data.paintMask != null)
            {
                if (data.paintMask.width != data.lineArt.width || data.paintMask.height != data.lineArt.height)
                {
                    ShowEmptyState("Mask không cùng kích thước với ảnh nét. Hãy tạo lại mask từ chính ảnh nét này.");
                    Debug.LogError("[ColoringPageMinigame] Kích thước mask phải trùng tuyệt đối ảnh nét.", this);
                    return;
                }
                sourceMaskPixels = data.paintMask.GetPixels32();
            }
        }
        catch (UnityException exception)
        {
            ShowEmptyState("Ảnh nét và mask cần bật Read/Write trong Texture Import Settings.");
            Debug.LogError("[ColoringPageMinigame] Ảnh nét hoặc mask cần bật Read/Write. " + exception.Message, this);
            return;
        }

        cachedRegionSpans = null;
        pixelToRegion = null;
        if (data.regionData != null && !string.IsNullOrWhiteSpace(data.regionData.text))
        {
            try
            {
                ColoringRegionDataAsset asset = JsonUtility.FromJson<ColoringRegionDataAsset>(data.regionData.text);
                if (TryBuildCachedRegions(asset, data.lineArt.width, data.lineArt.height, out cachedRegionSpans, out pixelToRegion))
                    fillableRegionCount = cachedRegionSpans.Length;
                else
                    Debug.LogWarning("[ColoringPageMinigame] Dữ liệu vùng không hợp lệ; dùng nhận diện vùng từ ảnh nét.", this);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ColoringPageMinigame] Lỗi đọc regionData: " + ex.Message + ", fallback sang tự nhận diện.", this);
                cachedRegionSpans = null;
                pixelToRegion = null;
            }
        }

        if (cachedRegionSpans == null)
        {
            BuildConnectedRegions(data);
        }

        PaintingProgress progress = savedProgress[selectedPaintingIndex];
        workingPixels = progress != null && progress.pixels != null
            ? (Color32[])progress.pixels.Clone()
            : (Color32[])sourcePixels.Clone();
        workingTexture = new Texture2D(data.lineArt.width, data.lineArt.height, TextureFormat.RGBA32, false)
        {
            name = data.lineArt.name + " (Runtime Coloring Copy)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        workingTexture.SetPixels32(workingPixels);
        workingTexture.Apply(false, false);
        if (coloringImage != null)
        {
            coloringImage.texture = workingTexture;
            coloringImage.color = Color.white;
            coloringImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        if (lineArtOverlayImage != null)
        {
            int width = data.lineArt.width;
            int height = data.lineArt.height;
            Color32[] overlayPixels = new Color32[sourcePixels.Length];
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                byte lum = (byte)Luminance(sourcePixels[i]);
                byte alpha = (byte)(255 - lum);
                overlayPixels[i] = new Color32(0, 0, 0, alpha);
            }
            lineOverlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = data.lineArt.name + " (Line Overlay)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            lineOverlayTexture.SetPixels32(overlayPixels);
            lineOverlayTexture.Apply(false, false);
            lineArtOverlayImage.texture = lineOverlayTexture;
            lineArtOverlayImage.color = Color.white;
            lineArtOverlayImage.enabled = true;

        }

        RefreshArtworkLayout();

        int totalRegionSlots = cachedRegionSpans != null ? cachedRegionSpans.Length : (componentSizes != null ? componentSizes.Length : 0);
        if (progress != null && progress.paintedRegions != null && progress.paintedRegions.Length == totalRegionSlots)
        {
            paintedComponentsCopy(progress.paintedRegions, out bool[] restored);
            paintedRegionCount = progress.paintedCount;
            isComplete = progress.complete;
            if (finishPanel != null) finishPanel.SetActive(isComplete);
        }
        else
        {
            savedProgress[selectedPaintingIndex] = null;
            currentPaintedComponents = new bool[totalRegionSlots];
            paintedRegionCount = 0;
            isComplete = false;
            if (finishPanel != null) finishPanel.SetActive(false);
        }

        if (fillableRegionCount == 0)
        {
            ShowEmptyState("Không tìm thấy vùng trắng đủ lớn để tô trong ảnh này.");
            return;
        }
        UpdateProgress();
        Debug.Log($"[ColoringPageMinigame] {data.title}: nhận diện {fillableRegionCount} vùng tô kín.", this);
    }

    // Assign through a helper so restoring can avoid sharing mutable arrays.
    private void paintedComponentsCopy(bool[] source, out bool[] restored)
    {
        restored = (bool[])source.Clone();
        currentPaintedComponents = restored;
    }

    private bool[] currentPaintedComponents;

    private static bool TryBuildCachedRegions(ColoringRegionDataAsset asset, int width, int height,
        out ColoringRegionSpanItem[] regions, out int[] lookup)
    {
        regions = null;
        lookup = null;
        long pixelCount = (long)width * height;
        if (asset == null || width <= 0 || height <= 0 || pixelCount > int.MaxValue
            || asset.width != width || asset.height != height || asset.regions == null
            || asset.regions.Length == 0 || asset.regionCount != asset.regions.Length) return false;
        var ordered = new ColoringRegionSpanItem[asset.regions.Length];
        var seen = new bool[ordered.Length];
        var labels = new int[(int)pixelCount];
        foreach (ColoringRegionSpanItem region in asset.regions)
        {
            if (region.id < 1 || region.id > ordered.Length || seen[region.id - 1]
                || region.spans == null || region.spans.Length == 0) return false;
            seen[region.id - 1] = true;
            long total = 0;
            foreach (ColoringPixelSpan span in region.spans)
            {
                if (span.start < 0 || span.length <= 0 || span.start >= pixelCount
                    || span.length > pixelCount - span.start) return false;
                total += span.length;
                for (int i = span.start; i < span.start + span.length; i++)
                {
                    if (labels[i] != 0) return false;
                    labels[i] = region.id;
                }
            }
            if (total != region.pixelCount) return false;
            ordered[region.id - 1] = region;
        }
        regions = ordered;
        lookup = labels;
        return true;
    }

    private void BuildConnectedRegions(ColoringArtworkDefinition data)
    {
        int width = data.lineArt.width;
        int pixelCount = sourcePixels.Length;
        componentLabels = new int[pixelCount];
        int[] queue = new int[pixelCount];
        List<int> flattenedPixels = new List<int>(pixelCount / 2);
        List<int> starts = new List<int>();
        List<int> sizes = new List<int>();
        int largestBackgroundSize = 0;

        for (int start = 0; start < pixelCount; start++)
        {
            if (componentLabels[start] != 0 || Luminance(sourcePixels[start]) <= data.whiteThreshold) continue;
            int componentId = starts.Count + 1;
            int head = 0, tail = 0;
            bool touchesBorder = false;
            queue[tail++] = start;
            componentLabels[start] = componentId;
            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int y = index / width;
                if (x == 0 || x == width - 1 || y == 0 || y == data.lineArt.height - 1) touchesBorder = true;
                AddNeighbor(index - 1, x > 0);
                AddNeighbor(index + 1, x + 1 < width);
                AddNeighbor(index - width, index >= width);
                AddNeighbor(index + width, index + width < pixelCount);
            }

            starts.Add(flattenedPixels.Count);
            sizes.Add(tail);
            for (int i = 0; i < tail; i++) flattenedPixels.Add(queue[i]);
            if (touchesBorder && tail > largestBackgroundSize)
            {
                largestBackgroundSize = tail;
                backgroundComponent = componentId - 1;
            }

            void AddNeighbor(int neighbor, bool inBounds)
            {
                if (!inBounds || componentLabels[neighbor] != 0 || Luminance(sourcePixels[neighbor]) <= data.whiteThreshold) return;
                componentLabels[neighbor] = componentId;
                queue[tail++] = neighbor;
            }
        }

        componentStarts = starts.ToArray();
        componentSizes = sizes.ToArray();
        regionPixels = flattenedPixels.ToArray();
        fillableComponents = new bool[componentSizes.Length];
        if (data.regions != null && data.regions.Length > 0)
        {
            foreach (ColoringRegionSeed seed in data.regions)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(seed.normalizedPoint.x * width), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(seed.normalizedPoint.y * data.lineArt.height), 0, data.lineArt.height - 1);
                int id = componentLabels[y * width + x] - 1;
                if (id >= 0 && id < componentSizes.Length && id != backgroundComponent && componentSizes[id] >= data.minimumRegionPixels)
                    fillableComponents[id] = true;
            }
        }
        else
        {
            for (int i = 0; i < componentSizes.Length; i++)
                fillableComponents[i] = i != backgroundComponent && componentSizes[i] >= data.minimumRegionPixels;
        }
        for (int i = 0; i < fillableComponents.Length; i++)
            if (fillableComponents[i]) fillableRegionCount++;
    }

    private static int Luminance(Color32 color) => (299 * color.r + 587 * color.g + 114 * color.b) / 1000;

    public void SelectPaletteIndex(int index)
    {
        if (palette == null || index < 0 || index >= palette.Length) return;
        currentColor = palette[index];
        if (selectedColorIndicator != null) selectedColorIndicator.color = currentColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (selectedPaintingIndex < 0 || workingTexture == null || isComplete || finishPanel != null && finishPanel.activeSelf) return;
        if (eventData == null) return;
        RefreshArtworkLayout();
        if (coloringImage == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                coloringImage.rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint)) return;

        ColoringArtworkDefinition data = paintings[selectedPaintingIndex];
        Rect rect = coloringImage.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;
        int width = data.lineArt.width;
        int x = Mathf.Clamp(Mathf.FloorToInt(u * width), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(v * data.lineArt.height), 0, data.lineArt.height - 1);
        int pixelIndex = y * width + x;
        int regionIndex = -1;
        if (pixelToRegion != null)
        {
            int regionId = pixelToRegion[pixelIndex];
            if (regionId > 0 && cachedRegionSpans != null && regionId <= cachedRegionSpans.Length)
                regionIndex = regionId - 1;
        }
        else if (componentLabels != null && fillableComponents != null)
        {
            if (Luminance(sourcePixels[pixelIndex]) <= data.whiteThreshold) return;
            if (sourceMaskPixels != null && sourceMaskPixels[pixelIndex].r < 128) return;
            int componentIndex = componentLabels[pixelIndex] - 1;
            if (componentIndex >= 0 && componentIndex < componentSizes.Length && fillableComponents[componentIndex])
                regionIndex = componentIndex;
        }

        if (regionIndex < 0) return;

        Color32 fillColor = currentColor;
        if (cachedRegionSpans != null)
        {
            ColoringPixelSpan[] spans = cachedRegionSpans[regionIndex].spans;
            if (spans != null)
            {
                for (int s = 0; s < spans.Length; s++)
                {
                    int start = spans[s].start;
                    int len = spans[s].length;
                    for (int p = 0; p < len; p++)
                    {
                        int fillIndex = start + p;
                        if (sourceMaskPixels != null && sourceMaskPixels[fillIndex].r < 128) continue;
                        byte lineAlpha = (byte)(255 - Luminance(sourcePixels[fillIndex]));
                        workingPixels[fillIndex] = Color32.Lerp(fillColor, new Color32(0, 0, 0, 255), lineAlpha / 255f);
                    }
                }
            }
        }
        else
        {
            int offset = componentStarts[regionIndex];
            int count = componentSizes[regionIndex];
            for (int i = 0; i < count; i++)
            {
                int fillIndex = regionPixels[offset + i];
                if (sourceMaskPixels != null && sourceMaskPixels[fillIndex].r < 128) continue;
                byte lineAlpha = (byte)(255 - Luminance(sourcePixels[fillIndex]));
                workingPixels[fillIndex] = Color32.Lerp(fillColor, new Color32(0, 0, 0, 255), lineAlpha / 255f);
            }
        }
        workingTexture.SetPixels32(workingPixels);
        workingTexture.Apply(false, false);

        if (currentPaintedComponents != null && regionIndex < currentPaintedComponents.Length && !currentPaintedComponents[regionIndex])
        {
            currentPaintedComponents[regionIndex] = true;
            paintedRegionCount++;
            if (AudioManager.Instance != null && paintSound != null) AudioManager.Instance.PlaySFX(paintSound);
            isComplete = paintedRegionCount >= fillableRegionCount;
            if (isComplete)
            {
                if (finishPanel != null) finishPanel.SetActive(true);
                if (AudioManager.Instance != null && finishSound != null) AudioManager.Instance.PlaySFX(finishSound);
            }
            UpdateProgress();
        }
    }

    public void ResetPainting()
    {
        if (selectedPaintingIndex < 0 || workingTexture == null) return;
        workingPixels = (Color32[])sourcePixels.Clone();
        workingTexture.SetPixels32(workingPixels);
        workingTexture.Apply(false, false);
        int totalSlots = cachedRegionSpans != null ? cachedRegionSpans.Length : (componentSizes != null ? componentSizes.Length : 0);
        currentPaintedComponents = new bool[totalSlots];
        paintedRegionCount = 0;
        isComplete = false;
        savedProgress[selectedPaintingIndex] = null;
        if (finishPanel != null) finishPanel.SetActive(false);
        UpdateProgress();
    }

    private void SaveCurrentProgress()
    {
        if (selectedPaintingIndex < 0 || workingPixels == null || currentPaintedComponents == null || savedProgress == null) return;
        savedProgress[selectedPaintingIndex] = new PaintingProgress
        {
            pixels = (Color32[])workingPixels.Clone(),
            paintedRegions = (bool[])currentPaintedComponents.Clone(),
            paintedCount = paintedRegionCount,
            complete = isComplete
        };
    }

    public void CloseWorkshop()
    {
        if (settingsManager != null && settingsManager.IsSettingsOpen()) settingsManager.ClosePanel();
        if (optionsMenu != null) optionsMenu.SetActive(false);
        foreach (MinigameTrigger owner in FindObjectsByType<MinigameTrigger>())
            if (owner.IsMinigameOpen && owner.minigameUI == workshopPanel) owner.CloseMinigame();
        if (doorTrigger != null) doorTrigger.CloseMinigame();
        else
        {
            if (workshopPanel != null) workshopPanel.SetActive(false);
            ViewModeController.TryResumeGameplayIfClear();
        }
    }

    public void ToggleOptionsMenu()
    {
        if (optionsMenu != null) optionsMenu.SetActive(!optionsMenu.activeSelf);
    }

    public void TogglePaintingSelector()
    {
        if (paintingSelectorPanel == null) return;
        bool show = !paintingSelectorPanel.activeSelf;
        if (optionsMenu != null) optionsMenu.SetActive(false);
        if (selectorEmptyText != null)
        {
            bool hasPainting = validPaintingIndices.Count > 0;
            selectorEmptyText.text = hasPainting ? string.Empty
                : "Chưa có tranh. Thêm cặp tranh trong Tools > Workshop tô màu > Thêm cặp tranh.";
            selectorEmptyText.gameObject.SetActive(show && !hasPainting);
        }
        paintingSelectorPanel.SetActive(show);
        if (addPaintingButton != null) addPaintingButton.gameObject.SetActive(!show);
    }

    public void ClosePaintingSelector()
    {
        if (paintingSelectorPanel != null) paintingSelectorPanel.SetActive(false);
        if (selectorEmptyText != null) selectorEmptyText.gameObject.SetActive(false);
        if (addPaintingButton != null) addPaintingButton.gameObject.SetActive(true);
    }

    public void OpenSettings()
    {
        if (optionsMenu != null) optionsMenu.SetActive(false);
        if (settingsManager == null) settingsManager = FindAnyObjectByType<SettingsManager>();
        if (settingsManager != null)
        {
            settingsManager.OpenPanel();
            SetSettingsCanvasOrders(true);
        }
        else Debug.LogWarning("[ColoringPageMinigame] Không tìm thấy SettingsManager trong scene.", this);
    }

    public void ToggleReferenceImage()
    {
        referenceVisible = !referenceVisible;
        if (referenceImageFrame != null) referenceImageFrame.gameObject.SetActive(referenceVisible);
        if (referenceMessageText != null && referenceImage != null && referenceImage.texture == null)
            referenceMessageText.gameObject.SetActive(referenceVisible);
    }

    private void ShowEmptyState(string message)
    {
        // Keep the controller component active (it lives on the editable RawImage)
        // so the options and gallery buttons still work in the empty state.
        if (paintingContent != null) paintingContent.SetActive(true);
        if (coloringImage != null) coloringImage.enabled = false;
        if (referenceImageFrame != null) referenceImageFrame.gameObject.SetActive(false);
        if (emptyStateText != null)
        {
            emptyStateText.text = message;
            emptyStateText.gameObject.SetActive(true);
        }
        if (progressText != null) progressText.text = message;
        if (resetButton != null) resetButton.interactable = false;
        if (paletteButtons != null)
            foreach (Button button in paletteButtons) if (button != null) button.interactable = false;
        if (finishPanel != null) finishPanel.SetActive(false);
    }

    private void UpdateProgress()
    {
        if (progressText != null)
            progressText.text = isComplete ? "Hoàn thành!" : $"Đã tô {paintedRegionCount}/{fillableRegionCount} vùng";
        if (resetButton != null) resetButton.interactable = true;
        if (paletteButtons != null)
            foreach (Button button in paletteButtons) if (button != null) button.interactable = true;
    }

    private void ApplyResponsiveLayout()
    {
        lastLayoutWidth = Screen.width;
        lastLayoutHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        if (safeAreaRect != null)
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) safe = new Rect(0f, 0f, Screen.width, Screen.height);
            safeAreaRect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeAreaRect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeAreaRect.offsetMin = safeAreaRect.offsetMax = Vector2.zero;
        }
        if (picturesArea == null || coloringImageFrame == null || referenceImageFrame == null) return;
        Vector2 available = safeAreaRect != null ? safeAreaRect.rect.size : picturesArea.rect.size;
        bool stack = available.y > available.x || available.x < 900f;
        picturesArea.anchorMin = Vector2.zero;
        picturesArea.anchorMax = Vector2.one;
        picturesArea.offsetMin = new Vector2(24f, 130f);
        picturesArea.offsetMax = new Vector2(-24f, stack ? -210f : -180f);
        Canvas canvas = GetComponentInParent<Canvas>();
        float widthInCanvasUnits = available.x;
        if (paintingTitleText != null)
        {
            RectTransform titleRect = paintingTitleText.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(stack ? 0f : -80f, stack ? -108f : -118f);
            titleRect.sizeDelta = new Vector2(stack ? widthInCanvasUnits * 0.86f : 650f, 42f);
        }
        if (addPaintingButton != null)
        {
            RectTransform buttonRect = addPaintingButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 1f);
                buttonRect.anchoredPosition = new Vector2(stack ? 0f : 370f, stack ? -150f : -118f);
                buttonRect.sizeDelta = new Vector2(stack ? 180f : 170f, 42f);
            }
        }
        if (stack)
        {
            coloringImageFrame.anchorMin = new Vector2(0.08f, 0.51f);
            coloringImageFrame.anchorMax = new Vector2(0.92f, 0.98f);
            referenceImageFrame.anchorMin = new Vector2(0.08f, 0.02f);
            referenceImageFrame.anchorMax = new Vector2(0.92f, 0.49f);
        }
        else
        {
            coloringImageFrame.anchorMin = new Vector2(0.02f, 0.04f);
            coloringImageFrame.anchorMax = new Vector2(0.49f, 0.96f);
            referenceImageFrame.anchorMin = new Vector2(0.51f, 0.04f);
            referenceImageFrame.anchorMax = new Vector2(0.98f, 0.96f);
        }
        coloringImageFrame.offsetMin = coloringImageFrame.offsetMax = Vector2.zero;
        referenceImageFrame.offsetMin = referenceImageFrame.offsetMax = Vector2.zero;
    }

    private Vector2 previousAvailableSize;

    private void UpdateArtworkLayout()
    {
        Vector2 available = safeAreaRect != null ? safeAreaRect.rect.size : (picturesArea != null ? picturesArea.rect.size : Vector2.zero);
        if (available != previousAvailableSize)
        {
            previousAvailableSize = available;
            ApplyResponsiveLayout();
        }
        // Refit without reselection or recreating the painted texture. Child
        // viewports can resize even when the safe-area parent stays the same.
        RefreshArtworkLayout();
    }

    public void RefreshArtworkLayout()
    {
        if (selectedPaintingIndex < 0 || paintings == null || selectedPaintingIndex >= paintings.Length) return;
        ColoringArtworkDefinition data = paintings[selectedPaintingIndex];
        if (data == null || data.lineArt == null) return;
        RectTransform coloringViewport = ColoringArtworkLayout.Prepare(coloringImage, coloringImageFrame, false);
        RectTransform referenceViewport = ColoringArtworkLayout.Prepare(referenceImage, referenceImageFrame, true);
        Vector2 line = new Vector2(data.lineArt.width, data.lineArt.height);
        if (coloringViewport != null)
            coloringImage.rectTransform.sizeDelta = ColoringArtworkLayout.Fit(line, coloringViewport.rect.size);
        if (referenceViewport != null && data.referenceArt != null)
        {
            Rect alignment = ColoringArtworkLayout.ReferenceRect(line,
                new Vector2(data.referenceArt.width, data.referenceArt.height), referenceViewport.rect.size,
                data.referenceScale, data.referenceOffset);
            referenceImage.rectTransform.sizeDelta = alignment.size;
            ((RectTransform)referenceImage.transform.parent).anchoredPosition = alignment.center;
        }
        if (lineArtOverlayImage != null && coloringImage != null)
        {
            if (lineArtOverlayImage.transform.parent != coloringImage.transform)
                lineArtOverlayImage.transform.SetParent(coloringImage.transform, false);
            AspectRatioFitter overlayFitter = lineArtOverlayImage.GetComponent<AspectRatioFitter>();
            if (overlayFitter != null) overlayFitter.enabled = false;
            RectTransform overlay = lineArtOverlayImage.rectTransform;
            overlay.localScale = Vector3.one;
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            lineArtOverlayImage.raycastTarget = false;
        }
    }
    private void ReleaseWorkingTexture()
    {
        if (workingTexture != null)
        {
            if (coloringImage != null && coloringImage.texture == workingTexture) coloringImage.texture = null;
            if (Application.isPlaying) Destroy(workingTexture);
            else DestroyImmediate(workingTexture);
            workingTexture = null;
        }
        if (lineOverlayTexture != null)
        {
            if (lineArtOverlayImage != null && lineArtOverlayImage.texture == lineOverlayTexture) lineArtOverlayImage.texture = null;
            if (Application.isPlaying) Destroy(lineOverlayTexture);
            else DestroyImmediate(lineOverlayTexture);
            lineOverlayTexture = null;
        }
    }

    private void OnDestroy()
    {
        Canvas.willRenderCanvases -= RefreshArtworkLayout;
        RestoreGameplayHud();
        ReleaseWorkingTexture();
    }

    private void OnDisable()
    {
        SaveCurrentProgress();
        RestoreGameplayHud();
        if (playerCameraController != null && playerCameraStateCaptured)
            playerCameraController.enabled = playerCameraWasEnabled;
        playerCameraStateCaptured = false;
        if (paintingSelectorPanel != null) paintingSelectorPanel.SetActive(false);
    }
}
