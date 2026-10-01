# 📦 Hướng Dẫn Setup & Vận Hành — Triển Lãm Tranh Đông Hồ (Tương Tác Qua Cửa)

Tài liệu này hướng dẫn chi tiết quy trình hoàn thiện dự án theo mô hình **Tương tác Minigame & Thoát qua Cánh Cửa phòng triển lãm** (không cần tạo thêm Bàn Vẽ, không làm chật phòng tranh).

---

## 🗺️ 1. Sơ Đồ Luồng Game (PC / Mobile / VR)

```
[Bật Game]
    │
    ▼
[MainMenu] ← Scene 0 (MainMenu.unity)
    │  Màn hình bắt đầu: Nút "Bắt đầu tham quan" / "Chơi VR" / "Thoát"
    │  Bấm "Bắt đầu" ➔ Thanh tiến trình Loading 100%
    ▼
[SampleScene] ← Scene 1 (SampleScene.unity — Phòng Triển Lãm)
    ├── Xem tranh   ➔ Bấm E / Click / Tap / Trigger ➔ Hiện ảnh phóng to + Phát thuyết minh giọng đọc
    ├── Nói chuyện  ➔ Bấm E / Click / Tap / Trigger ➔ NPC Hướng dẫn viên mở khung thoại
    ├── Cánh Cửa    ➔ Lại gần cửa bấm E / Click / Tap / Trigger ➔ Mở Menu Cửa 3 nút:
    │     ├── [🎨 CHƠI MINIGAME]    ➔ Mở Minigame Tô Màu (Nhân vật đứng yên, chuột mở ra)
    │     │       ├── [🏛️ QUAY LẠI] ➔ Đóng minigame, đứng trước cửa xem tranh tiếp
    │     │       └── [❌ THOÁT]    ➔ Thoát game
    │     ├── [🚶 Ở LẠI THAM QUAN]  ➔ Đóng menu cửa, quay lại đi dạo tiếp
    │     └── [❌ VỀ MENU / THOÁT]  ➔ Quay lại Scene MainMenu hoặc thoát ứng dụng
    └── ESC / Nút SETTING / Nút Menu VR ➔ Mở bảng Settings (Âm lượng, BGM, FPS, Thoát)
```

---

## 🔧 2. Các Bước Kéo Thả Trong Unity Editor

### Bước 1: Gắn Tính Năng Cho Cánh Cửa Trong `SampleScene`
1. Mở Scene `Assets/Scenes/SampleScene.unity`.
2. Chọn **GameObject Cánh Cửa** (cửa ra vào hoặc cổng vòm trong phòng triển lãm):
   - Thêm component **`Box Collider`** (nhớ **tick chọn `Is Trigger`**, chỉnh vùng va chạm bao quanh trước cửa).
   - Thêm component **`InteractableOutline`** (để lại gần cửa tự phát sáng viền vàng).
   - Thêm component **`DoorMenuTrigger`**.

---

### Bước 2: Tạo Giao Diện Menu Cánh Cửa (`Panel_DoorMenu`)
1. Trong Canvas UI, tạo 1 Panel đặt tên **`Panel_DoorMenu`** (mặc định tắt `SetActive = false`).
2. Tạo **3 Button** bên trong Panel này:
   - **Nút 1: "🎨 Chơi Minigame Tô Màu"**
     - `OnClick (+)` ➔ Kéo **Cánh Cửa** vào ➔ Chọn hàm: **`DoorMenuTrigger.OpenMinigame`**
   - **Nút 2: "🚶 Ở lại tham quan"**
     - `OnClick (+)` ➔ Kéo **Cánh Cửa** vào ➔ Chọn hàm: **`DoorMenuTrigger.StayInGallery`**
   - **Nút 3: "❌ Về Menu Chính"** (hoặc Thoát Game)
     - `OnClick (+)` ➔ Kéo **Cánh Cửa** vào ➔ Chọn hàm: **`DoorMenuTrigger.GoToMainMenu`** (hoặc `DoorMenuTrigger.ExitGame`)
3. Chọn lại **Cánh Cửa**, kéo `Panel_DoorMenu` vừa tạo vào ô **`Door Menu UI`** của `DoorMenuTrigger`.

---

### Bước 3: Tạo Giao Diện Minigame Tô Màu (`Panel_Minigame`)
1. Tạo 1 Panel đặt tên **`Panel_Minigame`** (mặc định tắt `SetActive = false`).
2. Chọn **Cánh Cửa**, kéo `Panel_Minigame` vào ô **`Minigame UI`** của `DoorMenuTrigger`.
3. Thiết kế bên trong `Panel_Minigame`:
   - Các nút **Bảng màu**: Nút màu Đỏ, Xanh, Vàng...
   - Các nút **Mảnh tranh trắng**: Thân, cánh, đuôi... (cắt PNG nền trong suốt).
   - Nút **"🏛️ Quay lại Triển Lãm"** (hoặc nút [X] ở góc):
     - `OnClick (+)` ➔ Kéo **Cánh Cửa** vào ➔ Chọn hàm: **`DoorMenuTrigger.CloseMinigame`**
   - Nút **"Tô lại từ đầu"**:
     - `OnClick (+)` ➔ Gọi hàm `ColorFillMinigame.ResetPainting`
4. Tạo 1 Empty GameObject gắn script **`ColorFillMinigame`**:
   - Nối sự kiện nút màu ➔ `ColorFillMinigame.SelectColor`.
   - Nối sự kiện mảnh tranh ➔ `ColorFillMinigame.FillColor`, đồng thời kéo danh sách các mảnh vào ô **`Paintable Parts`**.

---

### Bước 4: Thuyết Minh Tranh
- Bấm vào từng bức tranh trong triển lãm (component `PaintingInfo`) ➔ Kéo file `.mp3` / `.wav` vào ô **`Voice Narration`**.

---

### Bước 5: Build Settings
- **File > Build Settings...**
  - **Index 0:** `Assets/Scenes/MainMenu.unity`
  - **Index 1:** `Assets/Scenes/SampleScene.unity`
