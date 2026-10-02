using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SetupColoringWorkshopTool
{
    private const string ArtPath = "Assets/deco/New folder/tranh-to-mau-tranh-dong-ho-dam-cuoi-chuot.jpg";
    private const string CanvasName = "WorkshopColoringCanvas";

    [MenuItem("Tools/Setup Workshop Tô Màu")]
    public static void SetupWorkshop()
    {
        Texture2D art = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath);
        if (art != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(art);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                art = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath);
            }
        }

        GameObject canvasObject = GameObject.Find(CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create coloring workshop");
        }
        Undo.RecordObject(canvasObject, "Configure workshop canvas");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvasObject);

        Transform existingPanel = canvasObject.transform.Find("Panel_WorkshopColoring");
        GameObject panel = existingPanel != null ? existingPanel.gameObject
            : MakeFullscreenPanel(canvasObject.transform, "Panel_WorkshopColoring", new Color(0.08f, 0.075f, 0.065f, 1f));
        Undo.RecordObject(panel, "Configure full-screen coloring workshop");
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = Undo.AddComponent<RectTransform>(panel);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage == null) panelImage = Undo.AddComponent<Image>(panel);
        panelImage.color = new Color(0.08f, 0.075f, 0.065f, 1f);
        GameObject safeArea = EnsureObject(panel.transform, "WorkshopSafeArea");
        RectTransform safeAreaRect = safeArea.GetComponent<RectTransform>();
        safeAreaRect.anchorMin = Vector2.zero;
        safeAreaRect.anchorMax = Vector2.one;
        safeAreaRect.offsetMin = safeAreaRect.offsetMax = Vector2.zero;
        string[] existingUiNames = {
            "Title", "Instructions", "PaintingTitle", "PicturesArea", "Progress", "SelectedColor",
            "Palette_0", "Palette_1", "Palette_2", "Palette_3", "Palette_4", "ResetPainting",
            "OptionsButton", "WorkshopOptionsMenu", "Panel_WorkshopComplete"
        };
        foreach (string childName in existingUiNames)
        {
            Transform oldChild = panel.transform.Find(childName);
            if (oldChild != null) oldChild.SetParent(safeArea.transform, false);
        }
        Transform uiRoot = safeArea.transform;

        TextMeshProUGUI title = MakeText(uiRoot, "Title", "WORKSHOP TÔ MÀU", 30, Vector2.zero, new Vector2(550f, 60f), UiTheme.TitleGold);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(35f, -24f), new Vector2(560f, 64f), new Vector2(0f, 1f));
        Transform oldInstructions = uiRoot.Find("Instructions");
        if (oldInstructions != null) Undo.DestroyObjectImmediate(oldInstructions.gameObject);
        TextMeshProUGUI paintingTitle = MakeText(uiRoot, "PaintingTitle", "Chưa chọn tranh", 22, Vector2.zero, new Vector2(720f, 44f), UiTheme.TitleGold);
        Anchor(paintingTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-80f, -118f), new Vector2(650f, 42f), new Vector2(0.5f, 1f));

        GameObject pictures = EnsureObject(uiRoot, "PicturesArea");
        RectTransform picturesRect = pictures.GetComponent<RectTransform>();
        picturesRect.anchorMin = new Vector2(0.03f, 0.12f);
        picturesRect.anchorMax = new Vector2(0.97f, 0.82f);
        picturesRect.offsetMin = picturesRect.offsetMax = Vector2.zero;

        GameObject content = EnsureObject(pictures.transform, "PaintingContent");
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

        Texture2D completedArt = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TRAnh/tranh-dam-cuoi-chuot-c.jpg");
        GameObject coloringFrame = MakePictureFrame(content.transform, "ColoringImageFrame", art, "TRANH ĐANG TÔ");
        GameObject referenceFrame = MakePictureFrame(content.transform, "ReferenceImageFrame", completedArt, "TRANH MẪU HOÀN THIỆN");
        RawImage raw = coloringFrame.GetComponentInChildren<RawImage>(true);
        AspectRatioFitter coloringFitter = raw.GetComponent<AspectRatioFitter>();
        AspectRatioFitter referenceFitter = referenceFrame.GetComponentInChildren<RawImage>(true).GetComponent<AspectRatioFitter>();
        TextMeshProUGUI emptyState = MakeText(pictures.transform, "EmptyState", "Chưa có tranh để tô.\nThêm tranh vào danh sách trong Inspector.", 24, Vector2.zero, new Vector2(900f, 150f), Color.white);
        emptyState.gameObject.SetActive(false);
        TextMeshProUGUI referenceMessage = MakeText(referenceFrame.transform, "ReferenceMissing", "Ảnh mẫu hoàn thiện\nchưa được thêm", 18, Vector2.zero, new Vector2(420f, 100f), Color.white);
        referenceMessage.rectTransform.anchorMin = referenceMessage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        referenceMessage.rectTransform.anchoredPosition = Vector2.zero;

        Transform oldSelector = uiRoot.Find("PaintingSelectorViewport");
        if (oldSelector != null) oldSelector.SetParent(uiRoot, false);
        GameObject selectorViewport = EnsureObject(uiRoot, "PaintingSelectorViewport", typeof(Image), typeof(Mask), typeof(ScrollRect));
        RectTransform viewportRect = selectorViewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.1f, 0.24f);
        viewportRect.anchorMax = new Vector2(0.9f, 0.76f);
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = selectorViewport.GetComponent<Image>();
        viewportImage.color = new Color(0.09f, 0.085f, 0.075f, 0.97f);
        selectorViewport.GetComponent<Mask>().showMaskGraphic = true;
        GameObject selectorContent = EnsureObject(selectorViewport.transform, "Content", typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform selectorContentRect = selectorContent.GetComponent<RectTransform>();
        selectorContentRect.anchorMin = new Vector2(0f, 0f);
        selectorContentRect.anchorMax = new Vector2(0f, 1f);
        selectorContentRect.pivot = new Vector2(0f, 0.5f);
        selectorContentRect.anchoredPosition = Vector2.zero;
        selectorContentRect.sizeDelta = new Vector2(0f, 0f);
        HorizontalLayoutGroup layout = selectorContent.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        selectorContent.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = selectorViewport.GetComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.viewport = viewportRect;
        scroll.content = selectorContentRect;
        Button selectorTemplate = MakeButton(selectorContent.transform, "PaintingChoiceTemplate", "Tên tranh", Vector2.zero, new Vector2(190f, 60f), UiTheme.BtnAccent);
        LayoutElement selectorElement = EnsureComponent<LayoutElement>(selectorTemplate.gameObject);
        selectorElement.preferredWidth = 190f;
        selectorElement.preferredHeight = 60f;
        selectorTemplate.gameObject.SetActive(false);
        Button selectorClose = MakeButton(selectorViewport.transform, "PaintingSelectorClose", "×", Vector2.zero, new Vector2(44f, 44f), UiTheme.BtnAccent);
        Anchor(selectorClose.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(44f, 44f), new Vector2(1f, 1f));
        TextMeshProUGUI selectorEmpty = MakeText(selectorViewport.transform, "PaintingSelectorEmpty", "", 18, Vector2.zero, new Vector2(560f, 100f), Color.white);
        selectorEmpty.rectTransform.anchorMin = Vector2.zero;
        selectorEmpty.rectTransform.anchorMax = Vector2.one;
        selectorEmpty.rectTransform.offsetMin = new Vector2(20f, 20f);
        selectorEmpty.rectTransform.offsetMax = new Vector2(-20f, -56f);
        selectorEmpty.gameObject.SetActive(false);

        TextMeshProUGUI progress = MakeText(uiRoot, "Progress", "Chọn tranh để bắt đầu", 17, Vector2.zero, new Vector2(700f, 34f), Color.white);
        Anchor(progress.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(700f, 34f), new Vector2(0.5f, 0f));
        Image selected = MakeSwatch(uiRoot, "SelectedColor", new Color(0.10f, 0.10f, 0.10f), Vector2.zero, new Vector2(38f, 38f));
        Anchor(selected.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-245f, 28f), new Vector2(40f, 40f), new Vector2(0.5f, 0f));

        Color[] colors = {
            new Color(0.10f, 0.10f, 0.10f), new Color(0.72f, 0.12f, 0.10f),
            new Color(0.95f, 0.72f, 0.16f), new Color(0.20f, 0.42f, 0.24f), Color.white
        };
        string[] colorNames = { "Đen", "Đỏ", "Vàng", "Xanh", "Trắng" };
        Button[] paletteButtons = new Button[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            float x = -160f + i * 80f;
            paletteButtons[i] = MakeButton(uiRoot, "Palette_" + i, colorNames[i], Vector2.zero, new Vector2(66f, 42f), colors[i]);
            TextMeshProUGUI paletteLabel = paletteButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
            if (paletteLabel != null && i == 4) paletteLabel.color = new Color(0.12f, 0.12f, 0.12f);
            if (i == 4)
            {
                Outline outline = EnsureComponent<Outline>(paletteButtons[i].gameObject);
                outline.effectColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            Anchor(paletteButtons[i].GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 25f), new Vector2(66f, 42f), new Vector2(0.5f, 0f));
        }

        Button reset = MakeButton(uiRoot, "ResetPainting", "Làm lại", Vector2.zero, new Vector2(120f, 46f), UiTheme.BtnAccent);
        Anchor(reset.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 22f), new Vector2(130f, 46f), new Vector2(1f, 0f));
        Button ellipsis = MakeButton(uiRoot, "OptionsButton", "…", Vector2.zero, new Vector2(68f, 60f), UiTheme.BtnAccent);
        Anchor(ellipsis.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(72f, 60f), new Vector2(1f, 1f));
        GameObject options = MakePanel(uiRoot, "WorkshopOptionsMenu", new Vector2(270f, 150f), new Color(0.12f, 0.12f, 0.15f, 0.98f));
        Anchor(options.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -92f), new Vector2(270f, 150f), new Vector2(1f, 1f));
        Button settingsButton = MakeButton(options.transform, "WorkshopSetting", "Setting", new Vector2(0f, 38f), new Vector2(230f, 48f), UiTheme.BtnAccent);
        Button returnButton = MakeButton(options.transform, "WorkshopReturn", "Quay lại triển lãm", new Vector2(0f, -36f), new Vector2(230f, 48f), UiTheme.BtnConfirm);
        options.SetActive(false);
        DoorMenuTrigger door = FindExhibitionDoor();
        if (door != null)
        {
            Undo.RecordObject(door, "Connect coloring workshop to door");
            door.minigameUI = panel;
        }

        GameObject finish = MakePanel(uiRoot, "Panel_WorkshopComplete", new Vector2(560f, 300f), new Color(0.04f, 0.06f, 0.09f, 0.96f));
        MakeText(finish.transform, "CompleteText", "Bức tranh đã hoàn thành!", 30, new Vector2(0f, 72f), new Vector2(500f, 60f), UiTheme.TitleGold);
        Button finishReset = MakeButton(finish.transform, "CompleteReset", "Tô lại", new Vector2(-125f, -75f), new Vector2(180f, 54f), UiTheme.BtnAccent);
        Button finishClose = MakeButton(finish.transform, "CompleteClose", "Về triển lãm", new Vector2(125f, -75f), new Vector2(200f, 54f), UiTheme.BtnConfirm);
        finish.SetActive(false);

        Transform previousAddPainting = coloringFrame.transform.Find("AddPaintingButton");
        if (previousAddPainting != null) previousAddPainting.SetParent(uiRoot, false);
        Button addPainting = MakeButton(uiRoot, "AddPaintingButton", "Chọn tranh +", Vector2.zero, new Vector2(170f, 42f), UiTheme.BtnConfirm);
        Anchor(addPainting.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(370f, -118f), new Vector2(170f, 42f), new Vector2(0.5f, 1f));

        ColoringPageMinigame[] existingGames = raw.GetComponents<ColoringPageMinigame>();
        ColoringPageMinigame game = existingGames.Length > 0
            ? existingGames[0]
            : Undo.AddComponent<ColoringPageMinigame>(raw.gameObject);
        for (int i = 1; i < existingGames.Length; i++)
            Undo.DestroyObjectImmediate(existingGames[i]);
        Undo.RecordObject(game, "Wire coloring workshop");
        game.coloringImage = raw;
        game.selectedColorIndicator = selected;
        game.progressText = progress;
        game.emptyStateText = emptyState;
        game.paintingContent = content;
        game.finishPanel = finish;
        game.workshopPanel = panel;
        game.doorTrigger = door;
        game.settingsManager = Object.FindAnyObjectByType<SettingsManager>();
        game.referenceImage = referenceFrame.GetComponentInChildren<RawImage>(true);
        game.referenceMessageText = referenceMessage;
        game.picturesArea = picturesRect;
        game.coloringImageFrame = coloringFrame.GetComponent<RectTransform>();
        game.referenceImageFrame = referenceFrame.GetComponent<RectTransform>();
        game.coloringAspectFitter = coloringFitter;
        game.referenceAspectFitter = referenceFitter;
        Transform overlay = raw.transform.Find("LineArtOverlay");
        GameObject overlayObj = overlay != null ? overlay.gameObject : EnsureObject(raw.transform, "LineArtOverlay", typeof(RawImage), typeof(AspectRatioFitter));
        RawImage overlayRaw = overlayObj.GetComponent<RawImage>();
        overlayRaw.raycastTarget = false;
        AspectRatioFitter overlayFitter = overlayRaw.GetComponent<AspectRatioFitter>();
        if (overlayFitter != null) overlayFitter.enabled = false;
        game.lineArtOverlayImage = overlayRaw;
        game.safeAreaRect = safeAreaRect;
        game.paintingTitleText = paintingTitle;
        game.optionsButton = ellipsis;
        game.optionsMenu = options;
        game.addPaintingButton = addPainting;
        game.paintingSelectorPanel = selectorViewport;
        game.selectorCloseButton = selectorClose;
        game.selectorEmptyText = selectorEmpty;
        game.settingsButton = settingsButton;
        game.returnToGalleryButton = returnButton;
        game.paletteButtons = paletteButtons;
        game.palette = colors;
        game.resetButton = reset;
        game.paintingButtonContainer = selectorContent.transform;
        game.paintingButtonTemplate = selectorTemplate;
        if (game.paintings == null) game.paintings = new ColoringArtworkDefinition[0];
        game.finishResetButton = finishReset;
        game.finishCloseButton = finishClose;

        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = panel;
        if (!Application.isBatchMode) EditorUtility.DisplayDialog("Workshop đã sẵn sàng", "Workshop toàn màn hình đã nối vào cửa. Danh sách tranh ban đầu để trống; thêm từng ColoringArtworkDefinition (tên, ảnh nét, ảnh mẫu tùy chọn và vùng UV tùy chọn) trong Inspector. Ảnh nét cần bật Read/Write. Nút … mở Setting hoặc quay lại triển lãm.", "OK");
    }

    private static DoorMenuTrigger FindExhibitionDoor()
    {
        DoorMenuTrigger[] doors = Object.FindObjectsByType<DoorMenuTrigger>();
        foreach (DoorMenuTrigger door in doors)
            if (door != null && door.doorMenuUI != null) return door;
        return doors.Length > 0 ? doors[0] : null;
    }

    private static GameObject MakePanel(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject go = EnsureObject(parent, name, typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        EnsureComponent<Image>(go).color = color;
        return go;
    }

    private static GameObject MakeFullscreenPanel(Transform parent, string name, Color color)
    {
        GameObject go = EnsureObject(parent, name, typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        EnsureComponent<Image>(go).color = color;
        return go;
    }

    private static GameObject MakePictureFrame(Transform parent, string name, Texture2D texture, string caption)
    {
        GameObject frame = EnsureObject(parent, name, typeof(Image));
        EnsureComponent<Image>(frame).color = new Color(0.20f, 0.18f, 0.14f, 1f);
        RawImage existingArt = frame.GetComponentInChildren<RawImage>(true);
        GameObject art = existingArt != null ? existingArt.gameObject : EnsureObject(frame.transform, "Artwork", typeof(RawImage));
        RectTransform artRect = art.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = new Vector2(12f, 34f);
        artRect.offsetMax = new Vector2(-12f, -34f);
        RawImage raw = EnsureComponent<RawImage>(art);
        raw.texture = texture;
        raw.color = Color.white;
        raw.raycastTarget = name == "ColoringImageFrame";
        ColoringArtworkLayout.Prepare(raw, frame.GetComponent<RectTransform>(), name == "ReferenceImageFrame");
        TextMeshProUGUI label = MakeText(frame.transform, "Caption", caption, 16, Vector2.zero, new Vector2(500f, 28f), UiTheme.TitleGold);
        Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(500f, 28f), new Vector2(0.5f, 1f));
        return frame;
    }

    private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float fontSize, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = EnsureObject(parent, name, typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        TextMeshProUGUI label = EnsureComponent<TextMeshProUGUI>(go);
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Image MakeSwatch(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject go = EnsureObject(parent, name, typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = EnsureComponent<Image>(go);
        image.color = color;
        return image;
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = EnsureObject(parent, name, typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        EnsureComponent<Image>(go).color = color;
        Button button = EnsureComponent<Button>(go);
        button.targetGraphic = go.GetComponent<Image>();
        if (!string.IsNullOrEmpty(label)) MakeText(go.transform, "Label", label, 18, Vector2.zero, size, Color.white);
        return button;
    }

    private static GameObject EnsureObject(Transform parent, string name, params System.Type[] components)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
            Undo.RecordObject(go, "Update workshop " + name);
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create workshop " + name);
            go.transform.SetParent(parent, false);
        }
        foreach (System.Type type in components)
            if (go.GetComponent(type) == null) Undo.AddComponent(go, type);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }
}
