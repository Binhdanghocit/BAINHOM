# 📦 Hướng Dẫn Setup & Vận Hành — Triển Lãm Tranh Đông Hồ (2 Scene)

Tài liệu này hướng dẫn chi tiết quy trình hoàn thiện dự án theo mô hình **2 Scene chuẩn (MainMenu ➔ Triển Lãm)**.

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
    ├── Xem tranh   ➔ Bấm E/Click/Tap/Trigger ➔ Hiện ảnh phóng to + Phát thuyết minh giọng đọc
    ├── Nói chuyện  ➔ Bấm E/Click/Tap/Trigger ➔ NPC Hướng dẫn viên mở khung thoại
    ├── Bàn Vẽ      ➔ Lại gần Bàn Workshop bấm E ➔ Mở Minigame Tô Màu (ColorFillMinigame)
    └── ESC / Nút SETTING / Nút Menu VR ➔ Mở bảng Settings
          ├── [VỀ MENU]    ➔ Quay trở lại Scene MainMenu
          └── [THOÁT GAME] ➔ Thoát khỏi ứng dụng
```

---

## 🔧 2. Các Bước Kéo Thả Trong Unity Editor

### Bước 1: Kiểm Tra Build Settings (Đảm bảo thứ tự Scene)
1. Vào menu **File > Build Settings...**
2. Đảm bảo danh sách **Scenes In Build** đúng thứ tự:
   - **Index 0:** `Assets/Scenes/MainMenu.unity`
   - **Index 1:** `Assets/Scenes/SampleScene.unity`

---

### Bước 2: Cấu Hình Scene `MainMenu`
1. Mở Scene `Assets/Scenes/MainMenu.unity`.
2. Chọn GameObject chứa script **`MainMenuManager`**.
3. Trong Inspector:
   - Ô **`Gallery Scene Name`** điền là: `SampleScene` (code cũng đã có fallback tự tìm nếu quên đổi).
   - Nút **Play (Bắt đầu)**: Đảm bảo OnClick gọi `MainMenuManager.PlayGame`.
   - Nút **Chơi VR**: OnClick gọi `MainMenuManager.PlayGameVR`.
   - Nút **Thoát**: OnClick gọi `MainMenuManager.QuitGame`.

---

### Bước 3: Cấu Hình Thuyết Minh Tranh trong `SampleScene`
1. Mở Scene `Assets/Scenes/SampleScene.unity`.
2. Bấm vào từng **GameObject bức tranh** trong Hierarchy (nơi gắn `PaintingInfo`).
3. Trong Inspector > component **Painting Info**:
   - Kéo file âm thanh `.mp3` / `.wav` thuyết minh vào ô **`Voice Narration`**.
4. *(Tùy chọn)* Trên GameObject **`AudioManager`**, thêm 1 component `AudioSource` thứ 3 kéo vào ô **`Voice Source`** (nếu để trống, code sẽ tự tạo khi phát).

---

### Bước 4: Đặt Bàn Vẽ & Giao Diện Minigame Tô Màu
1. Trong `SampleScene`, chọn hoặc tạo 1 cái bàn (`BanVe`) đặt ở góc phòng triển lãm:
   - Add Component: **Box Collider** (nhớ **tick `Is Trigger`**, chỉnh vùng va chạm rộng quanh bàn).
   - Add Component: **`InteractableOutline`** (để lại gần bàn vẽ tự sáng viền vàng).
   - Add Component: **`MinigameTrigger`**.
2. Tạo Panel UI Minigame (`Panel_Minigame`, mặc định tắt `SetActive = false`):
   - Kéo Panel này vào ô **`Minigame UI`** của `MinigameTrigger`.
3. Trong `Panel_Minigame`, tạo GameObject gắn script **`ColorFillMinigame`**:
   - Các nút **Bảng màu**: `OnClick` gọi `ColorFillMinigame.SelectColor` (kéo `Image` của chính nút màu đó vào).
   - Các nút **Mảnh tranh trắng**: `OnClick` gọi `ColorFillMinigame.FillColor` (kéo `Image` của chính mảnh đó vào), đồng thời kéo danh sách các mảnh vào ô **`Paintable Parts`**.
   - Nút **Tô lại**: `OnClick` gọi `ColorFillMinigame.ResetPainting`.
   - Nút **[X] Đóng**: `OnClick` gọi `MinigameTrigger.CloseMinigame`.

---

### Bước 5: Kiểm Tra Bảng Settings trong `SampleScene`
1. Chọn GameObject chứa **`SettingsManager`**.
2. Kiểm tra ô **`Main Menu Scene Name`** = `MainMenu`.
3. Kiểm tra nút trong Panel Settings hiển thị chữ **"VỀ MENU"** (Click sẽ gọi `SettingsManager.GoToMainMenu`).
4. Nút **"THOÁT GAME"** gọi `SettingsManager.QuitGame`.

---

### Bước 6: Tối Ưu Ánh Sáng (Bake Lighting)
1. Chọn các đối tượng tĩnh (Tường, Sàn, Cột, Khung tranh) > trên góc phải Inspector tick chọn **Static**.
2. Vào **Window > Rendering > Lighting** > bấm **Generate Lighting** để Unity nướng ánh sáng (giúp đạt 60-90 FPS mượt mà trên Mobile & VR).
