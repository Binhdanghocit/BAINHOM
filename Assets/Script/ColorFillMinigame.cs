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

    // Cache màu trắng ban đầu của từng mảnh (để Reset)
    private List<Color> originalColors = new List<Color>();
    private int paintedCount = 0;

    private void Start()
    {
        // Lưu lại màu gốc (trắng) của từng mảnh để dùng khi Reset
        originalColors.Clear();
        foreach (var part in paintableParts)
        {
            originalColors.Add(part != null ? part.color : Color.white);
        }

        if (finishPanel != null) finishPanel.SetActive(false);
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
        if (targetImage == null) return;

        bool wasWhite = IsUnpainted(targetImage.color);
        targetImage.color = currentColor;

        // Phát âm thanh tô màu
        if (AudioManager.Instance != null && paintSound != null)
            AudioManager.Instance.PlaySFX(paintSound);

        // Tăng đếm mảnh đã tô (chỉ tính lần đầu tô vào mảnh trắng)
        if (wasWhite && !IsUnpainted(currentColor))
        {
            paintedCount++;
            CheckFinish();
        }
    }

    // ─────────────────────────────────────────
    //  NÚT RESET — Tô lại từ đầu
    // ─────────────────────────────────────────
    public void ResetPainting()
    {
        paintedCount = 0;

        for (int i = 0; i < paintableParts.Count; i++)
        {
            if (paintableParts[i] != null && i < originalColors.Count)
                paintableParts[i].color = originalColors[i];
        }

        if (finishPanel != null) finishPanel.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  KIỂM TRA HOÀN THÀNH
    // ─────────────────────────────────────────
    private void CheckFinish()
    {
        if (paintedCount >= paintableParts.Count)
        {
            if (finishPanel != null) finishPanel.SetActive(true);

            if (AudioManager.Instance != null && finishSound != null)
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
