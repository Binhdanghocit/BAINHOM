# Build Android: Quest (VR) + Điện thoại

Điện thoại và Quest cùng dùng Android nhưng là **hai cấu hình build khác nhau**.
Không dùng APK Quest để phát hành lên Google Play: Android thường phải tắt OpenXR.

## 1. Cấu hình đã làm sẵn (không cần đụng)

| Hạng mục | Giá trị | File |
|---|---|---|
| Bundle ID | `com.bainhom.dongho` (đổi khỏi `com.DefaultCompany...`) | `ProjectSettings/ProjectSettings.asset` |
| Min SDK | 32 (cài được trên Quest 2/3/3S + điện thoại Android 12L+) | idem |
| Target SDK | 34 | idem |
| Kiến trúc | ARM64 + IL2CPP | idem (có sẵn) |
| Điều khiển cảm ứng | joystick trái / vuốt phải / nút Tương tác | `MobileControlsOverlay` |

## 2. Điều khiển trên điện thoại

- **Nửa trái màn hình, vuốt giữ**: joystick ảo di chuyển (đẩy hết cỡ = chạy).
- **Nửa phải màn hình, vuốt**: xoay góc nhìn (Góc 1 xoay người, Góc 3 xoay camera).
- **Tap 1 ngón nhanh**: tương tác với tranh/NPC (như click chuột).
- **Tap 2 ngón**: nhảy.
- **Nút Back (Android)**: mở/đóng Settings (`KeyCode.Escape` có sẵn).
- Chạm bắt đầu trên UI (nút, popup) không làm xoay/di chuyển.

## 3. Các bước build (làm trong Editor)

### Lần đầu (cài module Android)
1. Unity Hub → Installs → bản 6000.5.9f1 → Add Modules → tick
   **Android Build Support** + **SDK & NDK Tools** + **OpenJDK**. Install.
2. Unity → `Preferences → External Tools`: để mặc định đường dẫn SDK/NDK/JDK
   do Hub cài (không tự trỏ tay trừ khi rành).

### Mỗi lần build
3. `File → Build Settings`: chọn platform **Android** → **Switch Platform**
   (chờ reimport 1 lần, hơi lâu).
4. Cùng cửa sổ: tick **MainMenu** + **SampleScene** trong Scenes In Build
   (đã có sẵn, kiểm tra lại).
5. `Player Settings → Other Settings` kiểm tra nhanh:
   - Package Name: `com.bainhom.dongho`
   - Minimum API Level: Android 12L (API 32), Target: 34
   - Scripting Backend: IL2CPP, Target Architectures: **chỉ ARM64**.
6. Chọn đúng profile bên dưới trước khi nhấn Build.

### Đưa lên Quest
- `Project Settings → XR Plug-in Management → Android`: bật **OpenXR** và
  Meta Quest Support. Đây là cấu hình dành riêng cho kính.
- Bật Developer Mode trên kính (app Meta Horizon trên điện thoại → Devices →
  Developer Mode), cắm cáp → `Build And Run` là tự cài.
- Build ra `.apk` để sideload / Build And Run.
- Muốn icon + tên đẹp trên kính: `Player Settings → Icon`, đổi
  Product Name (hiện tại lấy theo tên project).

### Đưa lên điện thoại (CH Play)
- `Project Settings → XR Plug-in Management → Android`: **tắt OpenXR** trước
  khi build. Nếu để OpenXR, điện thoại/emulator có thể khởi XR sai và đen màn hình.
- `Build Settings` → tick **Build App Bundle (Google Play)** để xuất `.aab`;
  Google Play không nhận `.apk` mới cho bản phát hành.
- Tăng `Bundle Version Code` mỗi bản upload mới.
- `Player Settings → Publishing Settings`: tạo và sao lưu **keystore**, alias và
  mật khẩu. Không thể cập nhật cùng ứng dụng trên CH Play nếu mất chúng.

## 4. Lưu ý đã biết

- Khi chạy, `ViewModeController` chọn `vrRig` chỉ khi XR display thực sự chạy;
  còn điện thoại dùng `desktopPlayerRig` và Canvas cảm ứng, PC thường dùng
  `desktopPlayerRig` không có Canvas.
- APK chạy **offline hoàn toàn** (không cần mạng khi chơi).
- Lần Switch Platform đầu tiên Unity reimport toàn bộ texture/shader
  (có thể 10–30 phút máy yếu) — bình thường, chỉ 1 lần.
- Dung lượng APK sẽ to (gallery + lightmap + XR libs): nếu cần gọn thì
  chuyển texture tranh sang ASTC trong `Project Settings → Android`.
