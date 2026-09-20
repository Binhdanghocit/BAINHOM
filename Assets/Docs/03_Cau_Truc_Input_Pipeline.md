# Cấu trúc Input Pipeline

## Tổng quan

Mọi hành động "bấm để tương tác" trong project đều hợp lưu về MỘT điểm
quyết định duy nhất: `pressed = E || click chuột || trigger tay VR`.
Ba nguồn input độc lập, nhưng logic kích hoạt chỉ viết một lần.

```
┌──────────────┐   ┌──────────────┐   ┌──────────────────┐
│ Phím E       │   │ Chuột trái   │   │ Trigger tay VR   │
│ Input.GetKey │   │ OnMouseDown/ │   │ HandTriggerInput │
│ Down(KeyCode │   │ PlayerInter- │   │ .WasPressedThis  │
│ .E)          │   │ action       │   │ Frame()          │
└──────┬───────┘   └──────┬───────┘   └────────┬─────────┘
       └─────────┬────────┴───────────┬────────┘
                 ▼                    ▼
        ┌─────────────────────────────────┐
        │  PaintingTrigger.Update /       │
        │  NPCInteractable.Update         │
        │  + guard UI đang mở             │
        └───────────────┬─────────────────┘
                        ▼
        ┌─────────────────────────────────┐
        │  PaintingUIManager /            │
        │  DialogueUIManager              │
        │  (mở / đóng / chuyển dòng)      │
        └─────────────────────────────────┘
```

## 1. Nguồn Desktop: phím E

- `Input.GetKeyDown(KeyCode.E)` trong `PaintingTrigger.Update` và
  `NPCInteractable.Update`.
- Yêu cầu: player đang đứng trong trigger zone (`isPlayerNearby == true`).
- Chỉ hoạt động khi không có UI khác đang mở (guard chống kích hoạt kép).

## 2. Nguồn Desktop: chuột trái

Hai đường riêng, phục vụ 2 mục đích khác nhau:

| Đường | File | Khi nào dùng |
|---|---|---|
| `OnMouseDown()` trên chính vật | `PaintingTrigger` | Đã đứng gần, click trực tiếp vào tranh |
| Raycast từ tâm màn hình | `PlayerInteraction` | Click ở bất kỳ đâu, tia từ crosshair chạm tranh (bỏ qua collider của player) |

- Cả hai chỉ chạy khi `Cursor.lockState == Locked` (đang chơi, không mở menu).
- `PlayerInteraction.TryInteract` có guard null `Camera.main` (chế độ VR
  không có main camera thì bỏ qua an toàn).

## 3. Nguồn VR: `HandTriggerInput`

File: `Assets/Script/HandTriggerInput.cs` (static class, không cần attach).

### Nguyên tắc frame-based caching (fix race condition)
- Nhiều script (`PaintingTrigger`, `NPCInteractable`, ...) đều gọi
  `WasPressedThisFrame()` trong cùng 1 frame.
- Nếu mỗi script tự đọc trigger riêng: script chạy trước "ăn" mất sự kiện
  cạnh lên (rising edge), script chạy sau thấy nút đã giữ → **mất sự kiện**.
- Cách fix: lần gọi ĐẦU TIÊN trong frame đọc thiết bị thật + tính edge,
  cache kết quả theo `Time.frameCount`; các lần gọi sau trong cùng frame
  trả về đúng giá trị đã cache → mọi hệ thống thấy CÙNG MỘT sự kiện.

```csharp
if (Time.frameCount == lastCachedFrame) return cachedResult; // cache hit
lastCachedFrame = Time.frameCount;                           // cache miss: tính 1 lần
... đọc LeftHand/RightHand, tính rising edge ...
cachedResult = pressedNow && !wasPressed;
```

### Đọc thiết bị
- `InputDevices.GetDeviceAtXRNode(XRNode.LeftHand/RightHand)`, chỉ lấy lại
  khi `device.isValid == false` (tránh tìm thiết bị mỗi frame).
- Đọc `CommonUsages.triggerButton` qua `TryGetFeatureValue` → an toàn khi
  tay cầm ngắt kết nối giữa chừng (trả về false, không crash).
- Rising edge: `pressedNow && !wasPressed` → true đúng 1 frame cho mỗi lần bóp.

### Quy tắc khi dùng
- LUÔN gọi `HandTriggerInput.WasPressedThisFrame()` (có cache), KHÔNG tự
  đọc `InputDevices` ở script khác → tránh phá vỡ đồng bộ.
- Vì là static + tồn tại xuyên scene, state cũ (`wasLeft/wasRight`) tự
  hết hạn theo frame mới → không cần reset thủ công.

## 4. Guard chống kích hoạt kép (trigger VR không ổn định)

Một lần bóp trigger / bấm E chỉ được kích hoạt **đúng 1 hệ thống**:

- `PaintingTrigger`: nếu popup tranh đang mở → nút này chỉ ĐÓNG popup;
  nếu hội thoại NPC đang mở → bỏ qua.
- `NPCInteractable`: nếu popup tranh đang mở → bỏ qua
  (popup tự xử lý nút đóng của nó).
- Thứ tự ưu tiên khi nhiều zone chồng lấn: **UI đang mở > hệ thống mới**.

## 5. Mở rộng: thêm nguồn input mới

Ví dụ thêm nút tay cầm Xbox / nút UI trên màn hình cảm ứng:

1. Nếu là sự kiện "bấm 1 lần": gộp vào biến `pressed` trong
   `PaintingTrigger.Update` / `NPCInteractable.Update`, KHÔNG viết
   hàm xử lý mới.
2. Nếu là thiết bị VR/XR mới: thêm vào `HandTriggerInput` (đọc thêm node,
   giữ nguyên cơ chế cache) để mọi hệ thống hưởng lợi tự động.
3. Nếu là nút UI (Button.onClick): gọi thẳng
   `PaintingUIManager.ShowPaintingInfo/ClosePopup` hoặc
   `DialogueUIManager.AdvanceLine` — không đi qua guard trigger.
