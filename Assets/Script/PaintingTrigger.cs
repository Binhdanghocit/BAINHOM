using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(InteractableOutline))]
public class PaintingTrigger : MonoBehaviour
{
    private PaintingInfo paintingInfo; // Khai báo để lấy dữ liệu tranh
    private bool isPlayerNearby = false;
    private InteractableOutline outline;
    private int lastToggleFrame = -1;
    private readonly HashSet<Collider> playerCollidersInRange = new HashSet<Collider>();

    private void Awake()
    {
        // Tự động lấy component PaintingInfo nằm trên cùng GameObject bức tranh này
        paintingInfo = GetComponent<PaintingInfo>();

        if (paintingInfo == null)
        {
            Debug.LogError("Chưa gắn script PaintingInfo trên bức tranh này: " + gameObject.name);
        }

        // Viền vàng nhấp nháy khi player tới gần
        outline = GetComponent<InteractableOutline>();
    }



    private static bool IsOtherUIOpen()
    {
        var settings = Object.FindAnyObjectByType<SettingsManager>();
        if (settings != null && settings.IsSettingsOpen()) return true;
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        if (MinigameTrigger.IsAnyOpen) return true;
        if (DoorMenuTrigger.IsAnyOpen) return true;
        if (ExitToExteriorUI.IsAnyOpen) return true;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (playerCollidersInRange.Add(other)) UpdateNearbyState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (playerCollidersInRange.Remove(other)) UpdateNearbyState();
        }
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (IsOtherUIOpen()) return;

        // Chỉ hoạt động khi người chơi ĐANG ĐỨNG GẦN và có đủ dữ liệu
        if (isPlayerNearby && paintingInfo != null)
        {
            ToggleInteract();
        }
    }

    public void ToggleInteract()
    {
        if (IsOtherUIOpen()) return;
        // Chống double-toggle trong cùng 1 frame (do OnMouseDown + PlayerInteraction cùng bắt click)
        if (lastToggleFrame == Time.frameCount) return;
        lastToggleFrame = Time.frameCount;

        if (PaintingUIManager.Instance == null) return;

        // Popup đang mở -> bấm E / click / trigger lần nữa là thoát
        if (PaintingUIManager.Instance.IsPopupOpen)
        {
            PaintingUIManager.Instance.ClosePopup();
            return;
        }

        if (paintingInfo == null) return;

        // Bật Popup và TRUYỀN DỮ LIỆU tranh vào UIManager
        PaintingUIManager.Instance.ShowPaintingInfo(paintingInfo);

        // Phát âm thanh khi người chơi click xem tranh
        if (AudioManager.Instance != null && AudioManager.Instance.inspectClip != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.inspectClip);
        }
    }

    private void UpdateNearbyState()
    {
        bool nearby = playerCollidersInRange.Count > 0;
        if (nearby == isPlayerNearby) return;
        isPlayerNearby = nearby;
        if (outline != null) outline.SetProximity(nearby);
        if (!nearby && PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsShowingPainting(paintingInfo))
            PaintingUIManager.Instance.ClosePopup();
    }

    private void LateUpdate()
    {
        if (playerCollidersInRange.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy) > 0)
            UpdateNearbyState();
    }

    private void OnDisable()
    {
        playerCollidersInRange.Clear();
        UpdateNearbyState();
    }
}
