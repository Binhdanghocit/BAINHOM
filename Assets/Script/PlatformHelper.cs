using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

// Một nguồn sự thật duy nhất về nền tảng đang chạy:
// điện thoại (cảm ứng) / PC / kính VR. Dùng chung cho mọi system
// để cùng 1 bản build chạy được cả 3 nơi.
public static class PlatformHelper
{
    // True trên điện thoại/tablet (Android/iOS) hoặc bất kỳ thiết bị có cảm ứng.
    // Editor vẫn trả về false trừ khi đang giả lập touch — code desktop giữ nguyên.
    public static bool IsTouchDevice()
    {
        if (Application.isMobilePlatform) return true;
#if UNITY_EDITOR
        return false;
#else
        return Input.touchSupported && SystemInfo.deviceType == DeviceType.Handheld;
#endif
    }

    // True khi đang chạy trên kính Quest/Oculus (Android + tên máy chứa Quest/Oculus).
    public static bool IsQuestDevice()
    {
        if (Application.platform != RuntimePlatform.Android) return false;
        string model = SystemInfo.deviceModel ?? string.Empty;
        return model.IndexOf("quest", System.StringComparison.OrdinalIgnoreCase) >= 0
            || model.IndexOf("oculus", System.StringComparison.OrdinalIgnoreCase) >= 0
            || model.IndexOf("horizon", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // True khi XR display subsystem đang chạy (HMD đã kết nối + loader đã start).
    // Thay thế XRSettings.isDeviceActive (cũ) — vẫn đúng cả khi XR được
    // khởi động MUỘN lúc chạy (runtime init) thay vì lúc mở app.
    public static bool IsXRDisplayRunning()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null && displays[i].running) return true;
        }
#if UNITY_2019_1_OR_NEWER
#pragma warning disable 0618
        if (XRSettings.isDeviceActive) return true;
#pragma warning restore 0618
#endif
        return false;
    }

    // Khóa cursor CHỈ trên thiết bị có chuột (PC). Trên cảm ứng gọi hàm này
    // không làm gì — tránh khóa/ẩn con trỏ oan trên điện thoại.
    public static void SetCursorLocked(bool wantLocked)
    {
        if (IsTouchDevice() || IsXRDisplayRunning())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        Cursor.lockState = wantLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !wantLocked;
    }
}
