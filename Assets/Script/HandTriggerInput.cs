using UnityEngine;
using UnityEngine.XR;

public static class HandTriggerInput
{
    private static InputDevice left;
    private static InputDevice right;
    private static bool wasLeft;
    private static bool wasRight;

    private static int lastCachedFrame = -1;
    private static bool cachedResult;

    // Trả về true đúng 1 khung khi người dùng bấm trigger (tay trái hoặc tay phải)
    // Tối ưu hóa bằng cách cache kết quả theo Frame, tránh lỗi khi nhiều script gọi cùng 1 frame.
    public static bool WasPressedThisFrame()
    {
        if (Time.frameCount == lastCachedFrame)
        {
            return cachedResult;
        }

        lastCachedFrame = Time.frameCount;

        if (!left.isValid) left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!right.isValid) right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool leftNow = Read(left);
        bool rightNow = Read(right);
        bool pressedNow = leftNow || rightNow;
        bool wasPressed = wasLeft || wasRight;

        wasLeft = leftNow;
        wasRight = rightNow;

        cachedResult = pressedNow && !wasPressed;
        return cachedResult;
    }

    private static bool Read(InputDevice device)
    {
        if (!device.isValid) return false;

        return device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
    }
}