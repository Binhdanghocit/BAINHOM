using UnityEngine;
using TMPro;

public class FPSDisplay : MonoBehaviour
{
    [Header("--- UI Reference ---")]
    public TextMeshProUGUI fpsText; // Kéo Text UI vào đây

    [Header("--- Settings ---")]
    public float updateInterval = 0.5f; // Thời gian cập nhật FPS (giây)

    private float accum = 0; // Tổng FPS tích lũy
    private int frames = 0;   // Số khung hình đếm được
    private float timeleft;  // Thời gian đếm ngược

    private void Start()
    {
        if (fpsText == null)
            fpsText = GetComponent<TextMeshProUGUI>();

        timeleft = updateInterval;
    }

    private void Update()
    {
        timeleft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;

        // Cập nhật con số hiển thị sau mỗi khoảng updateInterval
        if (timeleft <= 0.0f)
        {
            float fps = accum / frames;

            if (fpsText != null)
            {
                fpsText.text = string.Format("{0:F0} FPS", fps);

                // Đổi màu chữ theo mức FPS để dễ quan sát
                if (fps >= 50)
                    fpsText.color = Color.green;      // Mượt
                else if (fps >= 30)
                    fpsText.color = Color.yellow;     // Tạm ổn
                else
                    fpsText.color = Color.red;        // Giật/Lag
            }

            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }
}