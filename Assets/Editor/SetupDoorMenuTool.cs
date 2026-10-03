using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

public static class SetupDoorMenuTool
{
    [MenuItem("Tools/Tự Động Setup 3 Nút Menu Cánh Cửa (Panel_DoorMenu)")]
    public static void SetupDoorMenu() => SetupDoorMenuInScene(true);

    public static bool SetupDoorMenuInScene(bool showDialog = false)
    {
        GameObject door = GameObject.Find("cua");
        if (door == null)
        {
            var existing = Object.FindAnyObjectByType<DoorMenuTrigger>(FindObjectsInactive.Include);
            if (existing != null) door = existing.gameObject;
        }
        if (door == null) return Fail("Không tìm thấy cánh cửa trong scene.", showDialog);

        var collider = door.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = Undo.AddComponent<BoxCollider>(door);
            collider.size = new Vector3(3, 2.5f, 3);
            collider.isTrigger = true;
        }
        var trigger = door.GetComponent<DoorMenuTrigger>();
        if (trigger == null) trigger = Undo.AddComponent<DoorMenuTrigger>(door);
        if (door.GetComponent<InteractableOutline>() == null) Undo.AddComponent<InteractableOutline>(door);

        GameObject panel = trigger.doorMenuUI;
        if (panel == null)
        {
            foreach (var child in door.GetComponentsInChildren<Transform>(true))
                if (child.name == "Panel_DoorMenu") { panel = child.gameObject; break; }
        }
        if (panel == null) return Fail("Không tìm thấy Panel_DoorMenu trong scene.", showDialog);

        Undo.RecordObject(panel, "Configure door menu");
        var background = panel.GetComponent<Image>();
        if (background != null)
        {
            Undo.RecordObject(background, "Configure door menu background");
            background.color = UiTheme.PanelBg;
            background.raycastTarget = true;
        }
        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Undo.RecordObject(canvas, "Configure door menu sorting");
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 1500);
        }

        ConfigureLabel(panel.transform, "Title_Text", "CỬA TRIỂN LÃM", 32, UiTheme.TitleGold, FontStyles.Bold);
        ConfigureLabel(panel.transform, "Subtitle_Text", "Bạn muốn làm gì?", 20, UiTheme.SubGray, FontStyles.Normal);
        ConfigureButton(panel.transform, "Btn_Minigame", "Vào workshop tô màu", trigger, trigger.OpenMinigame);
        ConfigureButton(panel.transform, "Btn_O_Lai", "Tiếp tục tham quan", trigger, trigger.StayInGallery);
        ConfigureButton(panel.transform, "Btn_Thoat", "Về menu chính", trigger, trigger.GoToMainMenu);

        Undo.RecordObject(trigger, "Assign door menu");
        trigger.doorMenuUI = panel;
        if (trigger.minigameUI == null)
        {
            foreach (var workshop in Object.FindObjectsByType<MinigameTrigger>(FindObjectsInactive.Include))
                if (workshop.gameObject.scene == door.scene && workshop.minigameUI != null)
                { trigger.minigameUI = workshop.minigameUI; break; }
        }
        if (trigger.minigameUI == null) Debug.LogWarning("[SetupDoorMenuTool] Cần gán Workshop UI cho cánh cửa.");

        foreach (var rect in panel.GetComponentsInChildren<RectTransform>(true))
            Undo.RecordObject(rect, "Arrange door menu");
        var layout = panel.GetComponent<DoorMenuLayout>();
        if (layout == null) layout = Undo.AddComponent<DoorMenuLayout>(panel);
        Canvas.ForceUpdateCanvases();
        layout.RefreshLayout();
        panel.SetActive(false);
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(door.scene);
        Debug.Log("[SetupDoorMenuTool] Đã cập nhật menu cửa tiếng Việt; menu đang đóng.");
        if (showDialog) EditorUtility.DisplayDialog("Hoàn tất", "Menu cửa đã có ba nút: vào workshop tô màu, tiếp tục tham quan và về menu chính.", "OK");
        return true;
    }

    private static bool Fail(string message, bool showDialog)
    {
        Debug.LogError("[SetupDoorMenuTool] " + message);
        if (showDialog) EditorUtility.DisplayDialog("Lỗi", message, "OK");
        return false;
    }

    private static TextMeshProUGUI ConfigureLabel(Transform parent, string name, string text, float size, Color color, FontStyles style)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(created, "Create door menu label");
            created.transform.SetParent(parent, false);
            child = created.transform;
        }
        var label = child.GetComponent<TextMeshProUGUI>();
        if (label == null) label = Undo.AddComponent<TextMeshProUGUI>(child.gameObject);
        Undo.RecordObject(label, "Configure door menu label");
        // The bundled dynamic LiberationSans font includes Vietnamese glyphs.
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = label.fontSizeMax = size;
        label.fontSizeMin = 16;
        label.enableAutoSizing = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        EditorUtility.SetDirty(label);
        return label;
    }

    private static void ConfigureButton(Transform parent, string name, string text, DoorMenuTrigger owner, UnityEngine.Events.UnityAction action)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(created, "Create door menu button");
            created.transform.SetParent(parent, false);
            child = created.transform;
        }
        var button = child.GetComponent<Button>();
        if (button == null) button = Undo.AddComponent<Button>(child.gameObject);
        var image = child.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(child.gameObject);
        Undo.RecordObjects(new Object[] { button, image }, "Configure door menu button");
        UiTheme.ApplyButton(button, UiTheme.BtnAccent);
        image.color = Color.white; // ColorTint supplies the button color once.
        image.raycastTarget = true;
        button.interactable = true;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (button.onClick.GetPersistentTarget(i) is DoorMenuTrigger)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);

        var label = ConfigureLabel(child, child.Find("Text (TMP)") != null ? "Text (TMP)" : "Text", text, 24, UiTheme.TextWhite, FontStyles.Bold);
        var rect = label.rectTransform;
        Undo.RecordObject(rect, "Arrange door button label");
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(16, 6); rect.offsetMax = new Vector2(-16, -6);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(image);
    }
}
