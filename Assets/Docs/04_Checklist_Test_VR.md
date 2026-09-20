# Checklist test VR khi có kính (Quest / Rift / Link)

Mọi setup file đã làm sẵn. Chỉ còn bước tick loader trong Editor (mục 1)
rồi làm theo checklist dưới đây.

## 1. Bật OpenXR loader (làm 1 lần duy nhất)

1. `Edit → Project Settings → XR Plug-in Management`.
2. Tab **PC**: tick **OpenXR** (dùng khi test qua Quest Link / Rift).
3. Tab **Android**: tick **OpenXR** (dùng khi build file APK cài lên Quest).
4. Vào mục **OpenXR → Interaction Profiles**: add **Oculus Touch Controller Profile**.
5. Bấm Play trong Editor (không cần kính) để chắc game vẫn chạy desktop bình thường.

## 2. Những gì đã setup sẵn (không cần đụng)

| Hạng mục | Trạng thái |
|---|---|
| Tag "Player" trong TagManager | Đã thêm |
| Tự gắn Tag Player cho 2 rig lúc runtime | `ViewModeController.EnsurePlayerTag` |
| Nhận diện player thống nhất | `PlayerDetector` (tag → rig đăng ký → CharacterController) |
| Teleport trên sàn gallery | `TeleportationArea` đã gắn vào object `san1` |
| Rig VR (XR Origin: camera HMD, 2 tay cầm, Move/Turn/Teleport ray) | Đã nối vào `ViewModeController.vrRig` |
| Ẩn crosshair desktop khi đeo kính | `CrosshairReticle` tự check `XRSettings.isDeviceActive` |
| Đọc trigger 2 tay, chống mất sự kiện | `HandTriggerInput` (frame-cache) |
| Chống kích hoạt kép tranh + NPC | Guard trong `PaintingTrigger` / `NPCInteractable` |

## 3. Test qua Quest Link (nhanh nhất, không cần build)

1. Cắm cáp Link, bật Quest Link trên kính.
2. Trong Editor bấm **Play**.
3. Kiểm tra từng mục:
   - [ ] Nhìn xuống thấy 2 tay cầm hiện trong VR.
   - [ ] Đẩy thumbstick → di chuyển liên tục; tia teleport hiện khi bấm nút teleport → bấm để nhảy tới.
   - [ ] Đi tới gần 1 bức tranh → **viền vàng + prompt** hiện.
   - [ ] **Bóp trigger (cò súng)** → popup thông tin tranh mở; bóp lần nữa → đóng.
   - [ ] Đi ra xa → popup tự đóng, viền vàng tắt.
   - [ ] Gặp NPC (nếu có trong scene) → bóp trigger → hội thoại hiện từng dòng.
   - [ ] Mở Settings (nếu có nút trong scene VR) → crosshair/object khác không bị kẹt highlight.

## 4. Test build APK lên Quest (muốn chạy độc lập)

1. `File → Build Settings`: chuyển platform sang **Android**, add scene `MainMenu` + `SampleScene`.
2. `Player Settings → XR Settings`: xác nhận OpenXR được tick cho Android.
3. `Minimum API Level`: Android 10+ (Quest yêu cầu).
4. Build & Run → đeo kính test lại toàn bộ mục 3.

## 5. Lỗi thường gặp và cách xử lý

| Hiện tượng | Nguyên nhân likely | Fix |
|---|---|---|
| Đeo kính nhưng vẫn thấy player desktop | `XRSettings.isDeviceActive == false` → OpenXR loader chưa tick / Link chưa kết nối | Làm lại mục 1, kiểm tra kết nối Link |
| Vào vùng tranh mà không hiện viền vàng | Rig VR không được nhận diện | Kiểm tra `ViewModeController.vrRig` có trỏ đúng XR Origin không; Tag Player tự gắn lúc runtime (xem log) |
| Teleport không hiện tia / không nhảy được | Tay cầm chưa map action teleport | Kiểm tra Interaction Profile (mục 1.4); thử thumbstick-move trước |
| Bóp trigger không mở popup | Trigger device chưa valid | Mở `Window → Analysis → Input Debugger` (có kính) xem `XRNode.LeftHand/RightHand` có `triggerButton` không |
| Popup mở nhưng không đóng được | Đã đi ra khỏi trigger zone | Đi lại gần tranh rồi bóp trigger lần nữa |

## 6. Giới hạn đã biết (chưa hỗ trợ, làm sau nếu cần)

- **Hand tracking** (bắt bằng tay không): rig hiện tại dùng controller. Muốn đổi thì thay `vrRig` bằng prefab `XR Origin Hands (XR Rig)` trong Samples, và map thêm pinch vào `HandTriggerInput`.
- **Bấm nút UI Settings bằng tia laser VR**: popup tranh/NPC đóng mở bằng trigger trực tiếp nên không cần; chỉ panel Settings (nếu muốn bấm trong VR) mới cần thêm `XRUIInputModule` + Tracked Device Ray sau.
