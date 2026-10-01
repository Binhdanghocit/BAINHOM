using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using TMPro;

public static class SetupDoorMenuTool
{
    [MenuItem("Tools/Tự Động Setup 3 Nút Menu Cánh Cửa (Panel_DoorMenu)")]
    public static void SetupDoorMenu()
    {
        // 1. Tìm GameObject cua và component DoorMenuTrigger
        GameObject cuaGo = GameObject.Find("cua");
        if (cuaGo == null)
        {
            var trigger = Object.FindAnyObjectByType<DoorMenuTrigger>();
            if (trigger != null) cuaGo = trigger.gameObject;
        }

        if (cuaGo == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy GameObject 'cua' trong Scene!", "OK");
            return;
        }

        // Đảm bảo cua có DoorMenuTrigger, BoxCollider (IsTrigger), InteractableOutline
        var doorTrigger = cuaGo.GetComponent<DoorMenuTrigger>();
        if (doorTrigger == null) doorTrigger = Undo.AddComponent<DoorMenuTrigger>(cuaGo);

        var boxCol = cuaGo.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = Undo.AddComponent<BoxCollider>(cuaGo);
        boxCol.isTrigger = true;

        if (cuaGo.GetComponent<InteractableOutline>() == null)
            Undo.AddComponent<InteractableOutline>(cuaGo);

        // 2. Tìm Panel_DoorMenu
        GameObject panelGo = GameObject.Find("Panel_DoorMenu");
        if (panelGo == null)
        {
            Transform found = cuaGo.transform.Find("Canvas/Panel_DoorMenu");
            if (found != null) panelGo = found.gameObject;
        }

        if (panelGo == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy 'Panel_DoorMenu' trong Scene!", "OK");
            return;
        }

        Undo.RecordObject(panelGo, "Setup Panel_DoorMenu");
        panelGo.SetActive(true); // Bật tạm lên để thao tác dựng UI

        // 3. Tinh chỉnh kích thước và giao diện của Panel_DoorMenu
        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(480f, 420f);
        panelRt.anchoredPosition = Vector2.zero;

        Image panelImg = panelGo.GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f); // Nền tối hiện đại sang trọng
        }

        // Tiêu đề: CỬA TRIỂN LÃM
        Transform oldTitle = panelGo.transform.Find("Title_Text");
        if (oldTitle != null) Object.DestroyImmediate(oldTitle.gameObject);

        GameObject titleGo = new GameObject("Title_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
        titleTxt.text = "🚪 CỬA RA VÀO";
        titleTxt.fontSize = 28;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.85f, 0.3f, 1f); // Vàng ấm
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(440f, 45f);
        titleRt.anchoredPosition = new Vector2(0f, -25f);

        // Phụ đề: Bạn muốn làm gì?
        Transform oldSub = panelGo.transform.Find("Subtitle_Text");
        if (oldSub != null) Object.DestroyImmediate(oldSub.gameObject);

        GameObject subGo = new GameObject("Subtitle_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGo.transform.SetParent(panelGo.transform, false);
        var subTxt = subGo.GetComponent<TextMeshProUGUI>();
        subTxt.text = "Bạn muốn thực hiện thao tác nào?";
        subTxt.fontSize = 18;
        subTxt.alignment = TextAlignmentOptions.Center;
        subTxt.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        var subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.sizeDelta = new Vector2(440f, 30f);
        subRt.anchoredPosition = new Vector2(0f, -70f);

        // 4. Tạo 3 Button chuẩn chỉnh
        CreateOrUpdateButton(
            panelGo.transform,
            "Btn_Minigame",
            "🎨  Chơi Minigame",
            new Vector2(0f, -145f),
            new Vector2(400f, 65f),
            new Color(0.92f, 0.52f, 0.12f, 1f), // Màu cam nổi bật
            doorTrigger,
            nameof(doorTrigger.OpenMinigame)
        );

        CreateOrUpdateButton(
            panelGo.transform,
            "Btn_O_Lai",
            "🚶  Ở lại tham quan",
            new Vector2(0f, -225f),
            new Vector2(400f, 65f),
            new Color(0.18f, 0.65f, 0.32f, 1f), // Màu xanh lá tươi
            doorTrigger,
            nameof(doorTrigger.StayInGallery)
        );

        CreateOrUpdateButton(
            panelGo.transform,
            "Btn_Thoat",
            "❌  Về Menu Chính",
            new Vector2(0f, -305f),
            new Vector2(400f, 65f),
            new Color(0.78f, 0.22f, 0.2f, 1f), // Màu đỏ thoát
            doorTrigger,
            nameof(doorTrigger.GoToMainMenu)
        );

        // 5. Gán panel vào ô Door Menu UI trên DoorMenuTrigger
        Undo.RecordObject(doorTrigger, "Assign Door Menu UI");
        doorTrigger.doorMenuUI = panelGo;
        EditorUtility.SetDirty(doorTrigger);

        // 6. Mặc định tắt mắt (SetActive = false) như yêu cầu
        panelGo.SetActive(false);
        EditorUtility.SetDirty(panelGo);

        // Đánh dấu Scene đã thay đổi để Unity lưu
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("✅ [SetupDoorMenuTool] Đã tự động tạo và cấu hình 3 nút cho Panel_DoorMenu thành công 100%!");
        EditorUtility.DisplayDialog(
            "Thành công 🎉",
            "Đã tự động tạo và cấu hình hoàn tất 3 nút:\n\n" +
            "1. Btn_Minigame: 🎨 Chơi Minigame -> DoorMenuTrigger.OpenMinigame\n" +
            "2. Btn_O_Lai: 🚶 Ở lại tham quan -> DoorMenuTrigger.StayInGallery\n" +
            "3. Btn_Thoat: ❌ Về Menu Chính -> DoorMenuTrigger.GoToMainMenu\n\n" +
            "Panel_DoorMenu đã được gán vào Cánh Cửa và tắt mắt (SetActive = false) sẵn sàng!",
            "Tuyệt vời");
    }

    private static void CreateOrUpdateButton(
        Transform parent,
        string buttonName,
        string buttonText,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        Color btnColor,
        DoorMenuTrigger targetScript,
        string methodName)
    {
        Transform existing = parent.Find(buttonName);
        GameObject btnGo;
        if (existing != null)
        {
            btnGo = existing.gameObject;
        }
        else
        {
            btnGo = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
        }

        Undo.RecordObject(btnGo, "Configure " + buttonName);

        // RectTransform
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;

        // Image background
        Image img = btnGo.GetComponent<Image>();
        img.color = btnColor;

        // Button colors
        Button btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = btnColor;
        colors.highlightedColor = btnColor * 1.15f;
        colors.pressedColor = btnColor * 0.85f;
        btn.colors = colors;

        // Xóa listener cũ và gán persistent listener mới
        while (btn.onClick.GetPersistentEventCount() > 0)
        {
            UnityEventTools.RemovePersistentListener(btn.onClick, 0);
        }

        var method = typeof(DoorMenuTrigger).GetMethod(methodName);
        if (method != null)
        {
            var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), targetScript, method) as UnityEngine.Events.UnityAction;
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // Text bên trong nút
        Transform textTrans = btnGo.transform.Find("Text (TMP)");
        if (textTrans == null) textTrans = btnGo.transform.Find("Text");

        GameObject textGo;
        if (textTrans != null)
        {
            textGo = textTrans.gameObject;
        }
        else
        {
            textGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);
        }

        TextMeshProUGUI tmpText = textGo.GetComponent<TextMeshProUGUI>();
        if (tmpText == null) tmpText = textGo.AddComponent<TextMeshProUGUI>();

        tmpText.text = buttonText;
        tmpText.fontSize = 24;
        tmpText.fontStyle = FontStyles.Bold;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(btnGo);
    }
}
