using UnityEngine;

public class PaintingInfo : MonoBehaviour
{
    [Header("Thông tin bức tranh")]
    public string paintingTitle = "Bà Nguyệt";
    [TextArea(3, 10)]
    public string paintingDescription = "Tranh dân gian Đông Hồ...";
    public Sprite paintingSprite; // Ảnh phóng to hiển thị trên UI
    [Header("Âm thanh thuyết minh (Voiceover)")]
    public AudioClip voiceNarration; // File âm thanh đọc thuyết minh cho bức tranh
}