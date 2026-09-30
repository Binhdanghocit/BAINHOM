# 📦 Tổng Hợp Thay Đổi & Hướng Dẫn Setup — Triển Lãm Tranh Đông Hồ

Tài liệu này ghi chú toàn bộ các tính năng mới, các lỗi logic đã sửa và hướng dẫn kéo thả trong Unity Editor để nhóm đối soát và hoàn thiện dự án.

---

## 🗺️ 1. Sơ Đồ Luồng Game (PC / Mobile / VR)

```
[Bật Game]
    │
    ▼
[ExteriorScene] ← Scene 0 — Sảnh ngoài
    │  Player đứng trước cửa, viền vàng nhấp nháy
    │  Bấm E / Click / Tap / Trigger VR vào cửa
    ▼
[DoorMenuTrigger] → Menu 3 nút:
    ├── [🏛️ VÀO TRIỂN LÃM] → MainMenuManager.PlayGame()
    │       └─ Loading 100% → ExhibitionScene
    ├── [🎨 MINI GAME]     → Mở Panel tô màu (nếu đặt ở ngoài)
    └── [❌ THOÁT]         → Application.Quit()

[ExhibitionScene] ← Scene 1 — Bên trong triển lãm
    ├── Xem tranh → Bấm E/Click/Tap/Trigger → PaintingUIManager.ShowPaintingInfo()
    │               + Tự phát thuyết minh (AudioManager.PlayVoiceover)
    ├── Nói NPC   → Bấm E/Click/Tap/Trigger → DialogueUIManager.StartDialogue()
    ├── Bàn Vẽ    → Bấm E/Click/Tap/Trigger → MinigameTrigger.OpenMinigame()
    │               (Mở minigame tô màu ColorFillMinigame + khóa di chuyển nhân vật)
    └── ESC / Nút SETTING / Nút Menu VR → SettingsManager.TogglePanel()
          ├── [RA NGOÀI]   → SettingsManager.GoToMainMenu() → Load ExteriorScene
          └── [THOÁT GAME] → SettingsManager.QuitGame()
```

---

## 📝 2. Ghi Chú Các Thay Đổi & Fix Logic Đã Thực Hiện

### A. 5 Script Mới Đã Thêm (`Assets/Script/`)
1. **`DoorMenuTrigger.cs`**: Tương tác cánh cửa ngoài sảnh chờ, mở Menu 3 lựa chọn (`GoToGallery`, `OpenMinigame`, `ExitGame`, `CloseMinigame`). Tự động gắn `VRUIInputBridge`, khóa/mở chuột và tắt tạm Joystick Mobile khi mở menu.
2. **`MinigameTrigger.cs`**: Gắn vào Bàn Vẽ trong triển lãm để mở/đóng bảng Minigame (`OpenMinigame`, `CloseMinigame`, `ToggleMinigame`). Tự động tìm và khóa `PlayerController` + `MobileControlsOverlay` trong lúc người chơi đang tô màu.
3. **`ColorFillMinigame.cs`**: Minigame tô màu tự chọn. Hỗ trợ chọn màu (`SelectColor`), tô từng mảnh (`FillColor`), làm lại từ đầu (`ResetPainting`) và tự động kiểm tra hoàn thành (`CheckFinish` → bật `finishPanel` + phát `finishSound`).
4. **`WoodblockMinigame.cs`**: Minigame in tranh mộc bản theo từng lớp màu (`PrintLayer`, `ResetGame`).
5. **`ExitToExteriorUI.cs`**: Hộp thoại xác nhận rời triển lãm (`GoBackOutside`, `StayHere`) kèm hiệu ứng màn hình tối dần (`FadeOut`).

### B. 12 Script Cũ Đã Nâng Cấp & Fix Bug
1. **`PaintingInfo.cs`**: Thêm trường `public AudioClip voiceNarration` để gắn file âm thanh thuyết minh cho từng bức tranh.
2. **`AudioManager.cs`**: Thêm kênh `voiceSource` độc lập, hàm `PlayVoiceover(clip)` và `StopVoiceover()`, đồng bộ âm lượng thuyết minh theo thanh trượt Master Volume.
3. **`PaintingUIManager.cs`**: Bật `displayImage.preserveAspect = true` (chống méo ảnh tranh 4×3), tự phát `PlayVoiceover` khi mở tranh và `StopVoiceover` khi đóng, tắt tạm Joystick Mobile khi đang đọc tranh, tự gắn `VRUIInputBridge`.
4. **`MainMenuManager.cs` & `DialogueUIManager.cs`**: Sửa lỗi thiếu `referenceResolution = (1920, 1080)` và `matchWidthOrHeight = 0.5f` trên `CanvasScaler` khiến màn hình Loading và hộp thoại bị bé/mất trên Mobile. Cập nhật chữ gợi ý theo đúng thiết bị (VR / Mobile / PC).
5. **`SettingsManager.cs`**: Đổi `mainMenuSceneName` mặc định sang `"ExteriorScene"` và nút điều hướng thành `"RA NGOÀI"` để quay về sảnh chờ.
6. **`PlayerInteraction.cs`**:
   - Sửa lỗi `break` rơi chuỗi (fall-through) thành `return` để 1 cú click không kích hoạt chồng nhiều vật thể.
   - Bổ sung **Ưu tiên 3** (`DoorMenuTrigger`) và **Ưu tiên 4** (`MinigameTrigger`).
   - Chặn bắn tia Raycast khi đang mở bất kỳ bảng UI nào (`IsBlockingUIOpen`).
7. **`PaintingTrigger.cs`, `NPCInteractable.cs`, `DoorMenuTrigger.cs`, `MinigameTrigger.cs`**: Thêm khóa chống gọi kép trong cùng 1 frame (`lastToggleFrame == Time.frameCount`) để `OnMouseDown()` và `PlayerInteraction.TryInteract()` không bật rồi tắt UI ngay lập tức.
8. **`CrosshairReticle.cs`**: Tắt `raycastTarget` trên dấu `+` và bỏ `GraphicRaycaster` để tâm ngắm không chặn click chuột ở giữa màn hình; tự ẩn tâm ngắm khi mở Menu Cửa hoặc Minigame.
9. **`PlayerDetector.cs`**: Tự động dọn dẹp các tham chiếu `Transform` cũ đã bị hủy khi chuyển qua lại giữa các Scene.

---

## 🔧 3. Hướng Dẫn Setup Trong Unity Editor

### Bước 1: Setup Thuyết Minh Cho Từng Bức Tranh (`ExhibitionScene`)
1. Chọn từng **GameObject bức tranh** (nơi gắn `PaintingInfo`).
2. Trong Inspector > **Painting Info** > kéo file `.mp3` / `.wav` thuyết minh vào ô **`Voice Narration`**.
3. Trên GameObject **`AudioManager`**, có thể thêm **Audio Source thứ 3** kéo vào ô **`Voice Source`** (nếu để trống code sẽ tự tạo lúc chạy).

### Bước 2: Setup Cánh Cửa & Sảnh Ngoài (`ExteriorScene`)
1. Chọn **GameObject Cánh Cửa**:
   - Thêm **Box Collider** (tick chọn **`Is Trigger`**, chỉnh vùng va chạm phía trước cửa).
   - Thêm **`InteractableOutline`** và **`DoorMenuTrigger`**.
2. Tạo Panel UI Menu Cửa (`Panel_DoorMenu`, mặc định tắt `SetActive = false`) gồm 3 Button:
   - **Vào Triển Lãm**: `OnClick` → `DoorMenuTrigger.GoToGallery`
   - **Chơi Minigame**: `OnClick` → `DoorMenuTrigger.OpenMinigame`
   - **Thoát**: `OnClick` → `DoorMenuTrigger.ExitGame`
3. Kéo `Panel_DoorMenu` vào ô **`Door Menu UI`** của `DoorMenuTrigger`.
4. Tạo 1 Empty GameObject gắn **`MainMenuManager`**, điền **`Gallery Scene Name`** khớp chính xác tên Scene triển lãm.

### Bước 3: Setup Bàn Vẽ & Minigame Tô Màu
1. Đặt 1 đối tượng `BanVe` trong triển lãm, gắn **Box Collider (`Is Trigger`)**, **`InteractableOutline`** và **`MinigameTrigger`**.
2. Kéo `Panel_Minigame` vào ô **`Minigame UI`** của `MinigameTrigger`.
3. Trong `Panel_Minigame`, tạo Empty GameObject gắn **`ColorFillMinigame`**:
   - Các nút **Bảng màu**: `OnClick` → `ColorFillMinigame.SelectColor` (kéo `Image` của chính nút màu đó vào).
   - Các nút **Mảnh tranh trắng**: `OnClick` → `ColorFillMinigame.FillColor` (kéo `Image` của chính mảnh đó vào), đồng thời kéo toàn bộ mảnh trắng vào danh sách **`Paintable Parts`**.
   - Nút **Tô lại**: `OnClick` → `ColorFillMinigame.ResetPainting`.
   - Nút **[X] Đóng**: `OnClick` → `MinigameTrigger.CloseMinigame` (hoặc `DoorMenuTrigger.CloseMinigame` nếu mở từ cửa).

### Bước 4: Kiểm Tra Build Settings
- Vào **File > Build Settings...** và đảm bảo danh sách **Scenes In Build** có đủ:
  - **Index 0**: Scene Sảnh ngoài (`ExteriorScene`)
  - **Index 1**: Scene Triển lãm (`ExhibitionScene`)
- Kiểm tra lại ô **`Gallery Scene Name`** trong `MainMenuManager` và **`Main Menu Scene Name`** trong `SettingsManager` khớp đúng tên 2 Scene trên.
