# Logic hoạt động của Highlight System

## Tổng quan

Mỗi vật thể tương tác có một **InteractableOutline** quản lý "viền vàng".
Viền được bật khi ÍT NHẤT MỘT trong hai nguồn highlight sau đang active:

```
┌─────────────────────┐      ┌─────────────────────┐
│  PROXIMITY          │      │  AIMED              │
│  Player đứng trong  │      │  Crosshair đang chỉ  │
│  trigger zone       │      │  vào vật (chỉ khi    │
│  (OnTriggerEnter/   │      │  proximity active)   │
│   OnTriggerExit)    │      │                     │
└─────────┬───────────┘      └─────────┬───────────┘
          │ SetProximity(on)           │ SetAimed(on)
          └────────────┬───────────────┘
                       ▼
            InteractableOutline
            proximity || aimed
                       │
                       ▼
              ApplyVisuals()
             ┌─────────┴──────────┐
             ▼                    ▼
      LineRenderer vàng    Prompt "Nhấn E"
      (+ hiệu ứng pulse)   (DialogueUIManager)
```

## Chi tiết từng phần

### 1. Nguồn PROXIMITY (khoảng cách)
- `InteractableOutline.OnTriggerEnter/OnTriggerExit` tự dò player bước
  vào/ra trigger zone (dùng `PlayerDetector.IsPlayer`).
- Cũng có thể set từ script khác: `outline.SetProximity(true/false)`.

### 2. Nguồn AIMED (tâm ngắm)
- `CrosshairReticle.Update()` bắn 1 tia raycast từ tâm màn hình
  (`aimCamera.ViewportPointToRay(0.5, 0.5, 0)`).
- Chỉ highlight vật khi **vừa trúng ray vừa đang proximity active**
  (`outline.IsProximityActive`) — đứng xa ngắm không thấy viền.
- Chỉ có MỘT vật bị aimed tại một thời điểm (`aimedOutline`); khi đổi vật
  aim, outline cũ tự tắt, vật mới tự bật.

### 3. Đồng bộ hai nguồn (tránh lỗi "viền không tắt / bật sai thời điểm")
- `proximity` và `aimed` là 2 flag **độc lập**, gộp bằng OR.
- Mọi thay đổi đều đi qua `SetProximity()` / `SetAimed()` →
  `ApplyVisuals()` — không có đường tắt nào sửa trực tiếp flag.
- `applied`: nhớ trạng thái "đã gửi prompt + đã vẽ outline" để tránh
  gửi thừa khi flag đổi liên tiếp trong 1 frame.
- `lineShown`: tách riêng trạng thái thực tế của LineRenderer, để khi
  tick/un-tick `drawOutline` ở runtime viền cập nhật ngay lập tức.
- Crosshair bị ẩn (UI mở, VR, tắt bằng settings) → gọi `ClearAimed()`
  → outline vật đang aim tự tắt nguồn `aimed`.

### 4. Prompt "Nhấn E / Click để tương tác"
- `DialogueUIManager` giữ counter `promptRequests`:
  mỗi outline active +1, inactive -1.
- Prompt chỉ hiện khi counter > 0 **và** không đang nói chuyện NPC.
- Có cache: chỉ gọi `SetActive` khi trạng thái hiện/ẩn thật sự đổi.

### 5. Color & hiệu ứng
- Crosshair màu: white = không aim gì, yellow = đang aim vật tương tác.
- LineRenderer: vàng, pulse (nhấp nháy alpha) nếu `pulse = true`.

## Tuân thủ khi mở rộng

1. Đừng set `line.enabled` trực tiếp từ bên ngoài — luôn qua
   `SetProximity` / `SetAimed`.
2. Source highlight mới (VD: "đã chọn trong danh sách") → thêm 1 flag
   nữa trong `InteractableOutline` và gộp vào `IsHighlightActive`.
3. Vật bị occlude (che khuất) phía sau player: `CrosshairReticle` tự
   bỏ qua collider của player (raycast bổ sung phía sau) — không cần
   xử lý riêng.
4. Muốn tắt toàn bộ viền (event-mode): tick/un-tick `drawOutline`
   ở Inspector hoặc batch: `FindObjectsOfType<InteractableOutline>().Each(o => o.drawOutline = false)`.
