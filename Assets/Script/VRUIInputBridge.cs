using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

// A single pointer survives panel and rig activation changes.
[DefaultExecutionOrder(-200)]
public class VRUIInputBridge : MonoBehaviour
{
    private static VRUIInputBridge instance;
    private static int consumedFrame = -1;
    public static bool ConsumedPressThisFrame => consumedFrame == Time.frameCount;
    private PointerEventData pointer;
    private PointerEventData queryPointer;
    private EventSystem pointerSystem;
    private readonly List<RaycastResult> results = new List<RaycastResult>();
    private GameObject pressedObject, dragObject, hoveredObject;
    private bool wasPressed, dragging;
    private bool waitForRelease;
    private InputDevice heldController;
    private XRUIInputModule xrModule;
    private bool previousXRInput;

    public static VRUIInputBridge EnsureInstance()
    {
        if (instance != null) return instance;
        return new GameObject("VR UI Pointer").AddComponent<VRUIInputBridge>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            if (Application.isPlaying) Destroy(this);
            return;
        }
        instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (instance != this) return;
        if (!PlatformHelper.IsXRDisplayRunning() || EventSystem.current == null)
        {
            CancelPointer();
            RestoreXRModule();
            return;
        }
        if (pointerSystem != EventSystem.current)
        {
            CancelPointer();
            RestoreXRModule();
            pointerSystem = EventSystem.current;
            pointer = new PointerEventData(pointerSystem) { pointerId = -100, button = PointerEventData.InputButton.Left };
            queryPointer = new PointerEventData(pointerSystem);
        }
        // Keep mouse/touch support; only the bridge emits XR UI events.
        if (xrModule == null)
        {
            xrModule = pointerSystem.GetComponent<XRUIInputModule>();
            if (xrModule != null)
            {
                previousXRInput = xrModule.enableXRInput;
                xrModule.enableXRInput = false;
            }
        }
        InputDevice controller = wasPressed || waitForRelease ? heldController : GetController();
        bool triggerPressed = controller.isValid && controller.TryGetFeatureValue(CommonUsages.triggerButton, out bool down) && down;
        if (waitForRelease)
        {
            if (triggerPressed) { consumedFrame = Time.frameCount; return; }
            waitForRelease = false;
        }
        Vector2 position = pointer.position;
        bool hasPosition = TryGetControllerRay(controller, out Ray ray) && TryProjectRay(Camera.main, ray, out position);
        if (!hasPosition) position = pointer.position;
        results.Clear();
        if (hasPosition)
        {
            queryPointer.position = position;
            pointerSystem.RaycastAll(queryPointer, results);
        }
        RaycastResult hit = default;
        foreach (RaycastResult result in results)
        {
            if (result.module is GraphicRaycaster && result.gameObject != null)
            {
                hit = result;
                break;
            }
        }
        if (triggerPressed && !wasPressed) heldController = controller;
        ProcessPointer(position, hit, triggerPressed);
    }

    private void ProcessPointer(Vector2 position, RaycastResult hit, bool triggerPressed)
    {
        pointer.delta = position - pointer.position;
        pointer.position = position;
        pointer.pointerCurrentRaycast = hit;
        GameObject target = hit.gameObject;
        if (hoveredObject != target)
        {
            ExecuteEvents.ExecuteHierarchy(hoveredObject, pointer, ExecuteEvents.pointerExitHandler);
            hoveredObject = target;
            ExecuteEvents.ExecuteHierarchy(hoveredObject, pointer, ExecuteEvents.pointerEnterHandler);
        }
        if (triggerPressed && !wasPressed)
        {
            if (target != null) consumedFrame = Time.frameCount;
            pointer.pressPosition = position;
            pointer.pointerPressRaycast = hit;
            pointer.eligibleForClick = true;
            pressedObject = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            if (pressedObject == null) pressedObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            pointer.pointerPress = pressedObject;
            pointer.rawPointerPress = target;
            dragObject = ExecuteEvents.GetEventHandler<IDragHandler>(target);
            pointer.pointerDrag = dragObject;
            ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.initializePotentialDrag);
        }
        else if (triggerPressed && wasPressed && dragObject != null)
        {
            if (!dragging && (position - pointer.pressPosition).sqrMagnitude >= pointerSystem.pixelDragThreshold * pointerSystem.pixelDragThreshold)
            {
                ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.beginDragHandler);
                dragging = pointer.dragging = true;
                pointer.eligibleForClick = false;
            }
            if (dragging) ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.dragHandler);
        }
        else if (!triggerPressed && wasPressed)
        {
            ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerUpHandler);
            if (dragging) ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.endDragHandler);
            else if (pointer.eligibleForClick && pressedObject != null && pressedObject.activeInHierarchy
                && ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) == pressedObject)
                ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerClickHandler);
            ClearPress();
        }
        wasPressed = triggerPressed;
    }

    private void ClearPress()
    {
        pressedObject = dragObject = null;
        dragging = wasPressed = false;
        if (pointer == null) return;
        pointer.pointerPress = pointer.rawPointerPress = pointer.pointerDrag = null;
        pointer.dragging = pointer.eligibleForClick = false;
    }

    private void CancelPointer()
    {
        waitForRelease |= wasPressed;
        if (pointer != null)
        {
            ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerUpHandler);
            if (dragging) ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.endDragHandler);
            ExecuteEvents.ExecuteHierarchy(hoveredObject, pointer, ExecuteEvents.pointerExitHandler);
        }
        hoveredObject = null;
        ClearPress();
    }

    private void RestoreXRModule()
    {
        if (xrModule != null) xrModule.enableXRInput = previousXRInput;
        xrModule = null;
    }

    private void OnDisable()
    {
        if (instance != this) return;
        CancelPointer();
        RestoreXRModule();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private static InputDevice GetController()
    {
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (left.isValid && left.TryGetFeatureValue(CommonUsages.triggerButton, out bool leftDown) && leftDown
            && !(right.isValid && right.TryGetFeatureValue(CommonUsages.triggerButton, out bool rightDown) && rightDown)) return left;
        return right.isValid ? right : left;
    }

    public static bool TryGetControllerRay(out Ray ray) => TryGetControllerRay(GetController(), out ray);

    private static bool TryGetControllerRay(InputDevice controller, out Ray ray)
    {
        ray = default;
        Camera camera = Camera.main;
        if (camera == null || !controller.isValid
            || !controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)
            || !controller.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) return false;
        XROrigin xrOrigin = camera.GetComponentInParent<XROrigin>();
        Transform trackingSpace = xrOrigin != null
            ? (xrOrigin.CameraFloorOffsetObject != null ? xrOrigin.CameraFloorOffsetObject.transform : xrOrigin.transform)
            : camera.transform.parent;
        ray = TrackingPoseToRay(trackingSpace, position, rotation);
        return true;
    }

    public static Ray TrackingPoseToRay(Transform trackingSpace, Vector3 position, Quaternion rotation)
    {
        return trackingSpace != null
            ? new Ray(trackingSpace.TransformPoint(position), trackingSpace.TransformDirection(rotation * Vector3.forward))
            : new Ray(position, rotation * Vector3.forward);
    }

    public static bool TryProjectRay(Camera camera, Ray ray, out Vector2 position)
    {
        position = default;
        if (camera == null) return false;
        var plane = new Plane(camera.transform.forward, camera.transform.position + camera.transform.forward * 2f);
        if (Vector3.Dot(camera.transform.forward, ray.direction) <= 0.001f || !plane.Raycast(ray, out float distance) || distance <= 0f) return false;
        Vector3 viewport = camera.WorldToViewportPoint(ray.GetPoint(distance));
        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
        position = camera.ViewportToScreenPoint(viewport);
        return true;
    }
}
