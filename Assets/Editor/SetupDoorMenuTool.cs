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

        // Đảm bảo cua có BoxCollider (IsTrigger) TRƯỚC, vì DoorMenuTrigger ->
        // InteractableOutline có [RequireComponent(Collider)]. Add sai thứ tự
        // sẽ gây "Adding component failed" (log Editor.log line 2410).
        var boxCol = cuaGo.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = Undo.AddComponent<BoxCollider>(cuaGo);
        boxCol.isTrigger = true;

        // Vùng tương tác kiểu tranh: đủ rộng để player bước vào mới bấm E được.
        // Chỉ nới khi đang nhỏ hơn, không thu hẹp thiết kế có sẵn.
        Vector3 minZone = new Vector3(3f, 2.5f, 3f);
        Vector3 s = boxCol.size;
        boxCol.size = new Vector3(Mathf.Max(s.x, minZone.x), Mathf.Max(s.y, minZone.y), Mathf.Max(s.z, minZone.z));

        var doorTrigger = cuaGo.GetComponent<DoorMenuTrigger>();
        if (doorTrigger == null) doorTrigger = Undo.AddComponent<DoorMenuTrigger>(cuaGo);

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
            panelImg.color = UiTheme.PanelBg; // Nền tối hiện đại sang trọng
        }

        // Tiêu đề: CỬA TRIỂN LÃM
        Transform oldTitle = panelGo.transform.Find("Title_Text");
        if (oldTitle != null) Object.DestroyImmediate(oldTitle.gameObject);

        GameObject titleGo = new GameObject("Title_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
        titleTxt.text = "CUA RA VAO";
        titleTxt.fontSize = 28;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = UiTheme.TitleGold; // Vàng ấm
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
        subTxt.color = UiTheme.SubGray;
        var subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.sizeDelta = new Vector2(440f, 30f);
        subRt.anchoredPosition = new Vector2(0f, -70f);

        // 4. Tạo 3 Button chuẩn chỉnh (màu đồng bộ UiTheme với Settings/dialog)
        CreateOrUpdateButton(
            panelGo.transform,
            "Btn_Minigame",
            "Choi Minigame",
            new Vector2(0f, -145f),
            new Vector2(400f, 65f),
            UiTheme.BtnAccent, // Màu cam nổi bật
            doorTrigger,
            nameof(doorTrigger.OpenMinigame)
        );

        CreateOrUpdateButton(
            panelGo.transform,
            "Btn_O_Lai",
            "O lai tham quan",
            new Vector2(0f, -225f),
            new Vector2(400f, 65f),
            UiTheme.BtnConfirm, // Màu xanh lá tươi
            doorTrigger,
            nameof(doorTrigger.StayInGallery)
        );

        CreateOrUpdateButton(
            panelGo.transform,
            "Btn_Thoat",
            "Thoat game",
            new Vector2(0f, -305f),
            new Vector2(400f, 65f),
            UiTheme.BtnDanger, // Màu đỏ thoát
            doorTrigger,
            nameof(doorTrigger.ExitGame)
        );

        // 5. Gán panel vào ô Door Menu UI trên DoorMenuTrigger
        Undo.RecordObject(doorTrigger, "Assign Door Menu UI");
        doorTrigger.doorMenuUI = panelGo;

        // 5b. Tự gán Workshop/Minigame UI nếu đang trống: tìm panel workshop
        // có sẵn trong scene (bàn tô màu). Nút "Choi Minigame" chết
        // trong log cũ chính vì minigameUI == null và không có Resolve.
        if (doorTrigger.minigameUI == null)
        {
            GameObject workshop = FindWorkshopPanel();
            if (workshop != null)
            {
                doorTrigger.minigameUI = workshop;
                Debug.Log($"[SetupDoorMenuTool] Đã tự gán Workshop UI '{workshop.name}' vào DoorMenuTrigger.minigameUI.");
            }
            else
            {
                Debug.LogWarning("[SetupDoorMenuTool] Không tìm thấy panel tô màu trong scene. Hãy kéo panel chứa ColorFillMinigame vào ô 'Minigame UI' của DoorMenuTrigger, nút 'Choi Minigame' sẽ chỉ cảnh báo cho tới lúc đó.");
            }
        }
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
            "1. Btn_Minigame: Choi Minigame -> DoorMenuTrigger.OpenMinigame (mo Workshop)\n" +
            "2. Btn_O_Lai: O lai tham quan -> DoorMenuTrigger.StayInGallery (đóng menu, ở lại)\n" +
            "3. Btn_Thoat: Thoat game -> DoorMenuTrigger.ExitGame (thoát hẳn app)\n\n" +
            "Panel_DoorMenu đã được gán vào Cánh Cửa và tắt mắt (SetActive = false) sẵn sàng!",
            "Tuyệt vời");
    }

    // Tìm panel Workshop/Minigame có sẵn trong scene để auto-gán.
    // Ưu tiên MinigameTrigger đã setup ở bàn vẽ, rồi tới tên panel quen thuộc.
    private static GameObject FindWorkshopPanel()
    {
        var mgTrigger = Object.FindAnyObjectByType<MinigameTrigger>();
        if (mgTrigger != null && mgTrigger.minigameUI != null)
            return mgTrigger.minigameUI;

        string[] names = { "Panel_Minigame", "Panel_Workshop", "WorkshopUI", "MinigameUI", "Panel_ColorFill" };
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go != null) return go;
        }
        return null;
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

        // Image background + màu 3 trạng thái (đồng bộ UiTheme)
        Button btn = btnGo.GetComponent<Button>();
        UiTheme.ApplyButton(btn, btnColor);

        // Xóa listener cũ và gán persistent listener mới
        while (btn.onClick.GetPersistentEventCount() > 0)
        {
            UnityEventTools.RemovePersistentListener(btn.onClick, 0);
        }

        // Fix ArgumentException "Could not register callback ... class null"
        // (log Editor.log line 2419): targetScript null do AddComponent fail dây chuyền.
        if (targetScript == null)
        {
            Debug.LogError($"[SetupDoorMenuTool] target DoorMenuTrigger bị null, bỏ qua gán nút '{buttonName}'.");
            EditorUtility.SetDirty(btnGo);
            return;
        }
        var method = typeof(DoorMenuTrigger).GetMethod(methodName);
        if (method != null)
        {
            try
            {
                var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), targetScript, method) as UnityEngine.Events.UnityAction;
                if (action != null)
                    UnityEventTools.AddPersistentListener(btn.onClick, action);
                else
                    Debug.LogError($"[SetupDoorMenuTool] Không tạo được UnityAction cho '{methodName}'.");
            }
            catch (System.ArgumentException ex)
            {
                Debug.LogError($"[SetupDoorMenuTool] Gán nút '{buttonName}' -> '{methodName}' thất bại: {ex.Message}");
            }
        }
        else
        {
            Debug.LogError($"[SetupDoorMenuTool] Không tìm thấy method DoorMenuTrigger.{methodName}");
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
        tmpText.color = UiTheme.TextWhite;

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(btnGo);
    }
}
