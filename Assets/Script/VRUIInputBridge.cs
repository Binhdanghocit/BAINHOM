using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

/// <summary>
/// Cho phép controller VR bấm UI Screen Space hiện có (Play, Settings, slider...)
/// mà không cần nhân bản menu sang một Canvas khác. Ray phải được chiếu lên mặt
/// phẳng trước camera rồi chuyển thành tọa độ màn hình của EventSystem.
/// </summary>
public class VRUIInputBridge : MonoBehaviour
{
    private PointerEventData pointer;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private GameObject pressedObject;
    private bool wasPressed;
    private bool dragging;

    private IEnumerator Start()
    {
        // Menu phải khởi XR trước để controller có pose/trigger. Trên PC không
        // có kính, XRBoot tự thất bại an toàn và input chuột vẫn không đổi.
        if (!Application.isMobilePlatform && !PlatformHelper.IsXRDisplayRunning())
            yield return XRBoot.StartXRRoutine();

        if (EventSystem.current != null)
            pointer = new PointerEventData(EventSystem.current);
    }

    private void Update()
    {
        if (!PlatformHelper.IsXRDisplayRunning() || EventSystem.current == null) return;
        if (pointer == null) pointer = new PointerEventData(EventSystem.current);

        InputDevice controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!controller.isValid) controller = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!controller.isValid) return;

        if (!controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 origin) ||
            !controller.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) return;

        if (!TryGetScreenPosition(origin, rotation * Vector3.forward, out Vector2 screenPosition)) return;

        pointer.Reset();
        pointer.position = screenPosition;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, raycastResults);
        GameObject target = raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
        pointer.pointerCurrentRaycast = raycastResults.Count > 0 ? raycastResults[0] : new RaycastResult();

        controller.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);
        if (triggerPressed && !wasPressed)
        {
            pressedObject = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            if (pressedObject == null) pressedObject = target;
            ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.initializePotentialDrag);
            dragging = false;
        }
        else if (triggerPressed && wasPressed && pressedObject != null)
        {
            GameObject dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(pressedObject);
            if (dragTarget != null)
            {
                if (!dragging) ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.beginDragHandler);
                dragging = true;
                ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.dragHandler);
            }
        }
        else if (!triggerPressed && wasPressed)
        {
            if (pressedObject != null)
            {
                ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerUpHandler);
                if (dragging) ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.endDragHandler);
                else if (target != null) ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerClickHandler);
            }
            pressedObject = null;
            dragging = false;
        }
        wasPressed = triggerPressed;
    }

    private static bool TryGetScreenPosition(Vector3 origin, Vector3 direction, out Vector2 screenPosition)
    {
        screenPosition = default;
        Camera cam = Camera.main;
        if (cam == null) return false;

        // Mặt phẳng UI giả định nằm 2m phía trước camera, đúng với Screen Space
        // Overlay trong headset. Chỉ nhận ray đang hướng vào trước mặt.
        Vector3 normal = cam.transform.forward;
        Vector3 planePoint = cam.transform.position + normal * 2f;
        float denominator = Vector3.Dot(normal, direction);
        if (denominator <= 0.001f) return false;
        float distance = Vector3.Dot(normal, planePoint - origin) / denominator;
        if (distance <= 0f) return false;

        Vector3 local = cam.transform.InverseTransformPoint(origin + direction * distance);
        float halfHeight = 2f * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float halfWidth = halfHeight * cam.aspect;
        float viewportX = 0.5f + local.x / halfWidth;
        float viewportY = 0.5f + local.y / halfHeight;
        if (viewportX < 0f || viewportX > 1f || viewportY < 0f || viewportY > 1f) return false;

        screenPosition = new Vector2(viewportX * Screen.width, viewportY * Screen.height);
        return true;
    }
}
