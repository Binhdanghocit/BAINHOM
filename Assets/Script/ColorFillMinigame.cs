using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ColorFillMinigame : MonoBehaviour
{
    [Header("Màu hiện tại (Cọ vẽ)")]
    public Color currentColor = Color.red;
    [Tooltip("Ô hình vuông nhỏ trên màn hình, báo cho player biết đang cầm màu gì")]
    public Image currentBrushIndicator;

    [Header("Âm thanh")]
    public AudioClip paintSound;   // Tiếng tô màu
    public AudioClip finishSound;  // Tiếng hoàn thành

    [Header("Danh sách mảnh tranh cần tô (kéo tất cả vào đây)")]
    [Tooltip("Kéo tất cả các Image mảnh ghép trắng vào đây để script biết khi nào tô xong hết")]
    public List<Image> paintableParts;

    [Header("UI Hoàn thành")]
    [Tooltip("Panel hiện ra khi tô xong tất cả các mảnh (chứa chữ 'Xuất sắc!' và nút Làm lại)")]
    public GameObject finishPanel;

    private readonly HashSet<Image> paintableSet = new HashSet<Image>();
    private readonly Dictionary<Image, Color> originalColors = new Dictionary<Image, Color>();
    private int paintedCount = 0;
    private bool initialized;
    private bool isComplete;

    private void Awake()
    {
        InitializeMinigame();
    }

    private void Start()
    {
        InitializeMinigame();
    }

    private void InitializeMinigame()
    {
        if (initialized) return;
        initialized = true;
        paintableSet.Clear();
        originalColors.Clear();
        paintedCount = 0;
        if (paintableParts != null)
        {
            foreach (var part in paintableParts)
            {
                if (part == null || !paintableSet.Add(part)) continue;
                originalColors.Add(part, part.color);
                if (!IsUnpainted(part.color)) paintedCount++;
            }
        }

        if (currentBrushIndicator != null) currentBrushIndicator.color = currentColor;
        UpdateCompletion(playSound: false);
    }

    // ─────────────────────────────────────────
    //  GÁN VÀO OnClick CỦA CÁC NÚT BẢNG MÀU
    // ─────────────────────────────────────────
    // Kéo chính Image của nút màu vào ô Object của OnClick
    public void SelectColor(Image colorButtonImage)
    {
        if (colorButtonImage == null) return;
        currentColor = colorButtonImage.color;

        if (currentBrushIndicator != null)
            currentBrushIndicator.color = currentColor;
    }

    // ─────────────────────────────────────────
    //  GÁN VÀO OnClick CỦA CÁC MẢNH TRANH
    // ─────────────────────────────────────────
    // Kéo chính Image mảnh đó vào ô Object của OnClick
    public void FillColor(Image targetImage)
    {
        InitializeMinigame();
        if (targetImage == null || !paintableSet.Contains(targetImage)) return;

        bool wasWhite = IsUnpainted(targetImage.color);
        bool willBeWhite = IsUnpainted(currentColor);
        Color previousColor = targetImage.color;
        targetImage.color = currentColor;

        if (previousColor != currentColor && AudioManager.Instance != null && paintSound != null)
            AudioManager.Instance.PlaySFX(paintSound);

        if (wasWhite && !willBeWhite)
        {
            // Lần đầu tô vào mảnh trắng -> tăng đếm
            paintedCount++;
        }
        else if (!wasWhite && willBeWhite)
        {
            paintedCount = Mathf.Max(0, paintedCount - 1);
        }

        UpdateCompletion(playSound: true);
    }

    // ─────────────────────────────────────────
    //  NÚT RESET — Tô lại từ đầu
    // ─────────────────────────────────────────
    public void ResetPainting()
    {
        InitializeMinigame();

        foreach (KeyValuePair<Image, Color> entry in originalColors)
        {
            if (entry.Key != null) entry.Key.color = entry.Value;
        }

        RecountPaintedParts();
        UpdateCompletion(playSound: false);
    }

    private void RecountPaintedParts()
    {
        paintedCount = 0;
        foreach (Image part in paintableSet)
        {
            if (part != null && !IsUnpainted(part.color)) paintedCount++;
        }
    }

    private void UpdateCompletion(bool playSound)
    {
        bool wasComplete = isComplete;
        isComplete = paintableSet.Count > 0 && paintedCount >= paintableSet.Count;
        if (finishPanel != null && finishPanel.activeSelf != isComplete)
            finishPanel.SetActive(isComplete);

        if (!wasComplete && isComplete)
        {
            if (playSound && AudioManager.Instance != null && finishSound != null)
                AudioManager.Instance.PlaySFX(finishSound);
            Debug.Log("[ColorFill] Hoàn thành! Đã tô hết " + paintedCount + " mảnh.");
        }
    }

    // Màu coi là "chưa tô" nếu gần màu trắng (r, g, b đều > 0.95)
    private bool IsUnpainted(Color c)
    {
        return c.r > 0.95f && c.g > 0.95f && c.b > 0.95f;
    }
}
