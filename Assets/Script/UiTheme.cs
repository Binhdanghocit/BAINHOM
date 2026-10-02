using UnityEngine;
using UnityEngine.UI;

// Bảng màu dùng chung cho toàn game (cửa, Settings, dialog thoát, workshop).
// Muốn đổi màu nút 1 chỗ -> sửa ở đây, mọi UI dựng bằng code tự đồng bộ.
// Settings clone nút Close trong scene: đặt màu Image của nút Close trong
// scene = UiTheme.BtnConfirm để khớp hoàn toàn.
public static class UiTheme
{
    // Ngữ nghĩa nút (đồng bộ cửa <-> Settings/ESC <-> dialog thoát):
    public static readonly Color BtnConfirm = new Color(0.18f, 0.65f, 0.32f, 1f); // Xanh lá: Ở lại / đồng ý
    public static readonly Color BtnDanger  = new Color(0.78f, 0.22f, 0.20f, 1f); // Đỏ: Thoát game
    public static readonly Color BtnAccent   = new Color(0.92f, 0.52f, 0.12f, 1f); // Cam: Minigame/workshop

    // Khung panel + chữ:
    public static readonly Color PanelBg  = new Color(0.10f, 0.10f, 0.15f, 0.95f);
    public static readonly Color TitleGold = new Color(1f, 0.85f, 0.30f, 1f);
    public static readonly Color SubGray   = new Color(0.80f, 0.80f, 0.80f, 1f);
    public static readonly Color TextWhite = Color.white;

    // Tô màu chuẩn cho 1 Button (Image nền + 3 trạng thái), khớp style tool cửa cũ.
    public static void ApplyButton(Button btn, Color baseColor)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = baseColor;
            btn.targetGraphic = img;
        }
        var colors = btn.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = baseColor * 1.15f;
        colors.pressedColor = baseColor * 0.85f;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f);
        btn.colors = colors;
    }
}
