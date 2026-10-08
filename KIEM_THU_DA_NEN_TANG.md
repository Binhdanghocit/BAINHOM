# Ma trận kiểm thử game và workshop tô màu

Mỗi kết quả phải gắn với SHA256 bản build, profile, thiết bị/giả lập, hệ điều hành, GPU và ngày chạy. Ghi riêng **Build**, **Game runtime**, **Workshop**, **Hiệu năng**. Editor và giả lập không xác nhận thiết bị thật.

## Cấu hình

- Unity: 6000.5.9f1. Scene: MainMenu, SampleScene.
- Windows: x64 IL2CPP, Input System, OpenXR được cấu hình nhưng khởi động thủ công qua Chơi VR.
- Android điện thoại: ARM64 IL2CPP, Android Touch, không XR loader.
- Quest 2: ARM64 IL2CPP, Quest OpenXR, Meta Quest Support và Oculus Touch; XRBoot tự vào VR trên Quest.
- Application ID Android hiện tại: `com.nhom3.trienlam`. Min SDK 32. Target SDK setting = Automatic; ghi Target SDK thực tế từ APK thay vì xem Automatic là SDK 0.
- Build Android Touch và Quest là hai artifact riêng. Khi cài cùng application ID trên cùng thiết bị, chúng thay thế nhau; chọn đúng APK cho mục tiêu.

## Ma trận workshop bắt buộc

| Mục tiêu | Cấu hình | Input cần kiểm | Hiển thị/lifecycle | Điều kiện ghi Workshop PASS |
|---|---|---|---|---|
| Windows | x64, chuột/phím | Chọn tranh/màu/vùng; click sát biên; kéo các control hiện có | Resize, tiếng Việt, đóng/mở, menu/gallery | Các chức năng bắt buộc đạt trên bản Windows đã ghi hash |
| Android điện thoại | ARM64, touch | Tap/drag; joystick và vùng tô không tranh touch; đa chạm | Dọc/ngang, safe area, Back, background/resume | Đạt trên model/Android thật đã ghi rõ |
| Android giả lập | APK Android Touch, ghi ABI và cơ chế dịch ARM nếu có | Tap/swipe phát qua giả lập; Back | Viewport, lifecycle, shader, crash/logcat | Chỉ ghi PASS giả lập; không thay cho điện thoại |
| Quest 2 độc lập | APK Quest, controller | Chọn tranh/màu/vùng bằng ray và trigger | UI trong kính; modal khóa locomotion nhưng giữ tracking/pointer | Đạt tracking/controller/workshop trên kính thật |
| Windows + Quest Link | Windows, OpenXR runtime Meta | Bật/dừng VR, controller | Mất kết nối, tháo/đeo kính, đổi rig | Đạt trên tổ hợp PC/runtime/Quest cụ thể |
| macOS | Build và máy Mac mục tiêu | Chuột/trackpad | Font, shader/màu; đường dẫn/quyền nếu có lưu file | Build và runtime workshop đạt trên Mac đã ghi cấu hình |
| Linux | Build và distro mục tiêu | Chuột/phím | Font, shader, đường dẫn phân biệt hoa/thường | Build và runtime workshop đạt trên máy Linux |
| iOS/iPadOS | Xcode, thiết bị mục tiêu | Touch/đa chạm | Safe area, RAM, lifecycle; sandbox nếu có xuất/lưu | Đạt trên iPhone/iPad đã ghi cấu hình |
| WebGL | Browser/OS cụ thể | Chuột/touch tùy browser | Shader/audio sau tương tác; tải asset, RAM, resize | Đạt riêng từng Chrome/Edge hoặc browser mục tiêu |

## Checklist workshop dùng cho mỗi dòng

1. Mở workshop; chọn từng tranh; ảnh nét, mẫu và vùng tô khớp nhau. Không bỏ qua tranh nhiều vùng nhỏ.
2. Chọn màu; tô vùng nhỏ/lớn, sát biên; kiểm đúng vùng, không lem. Khi không có vùng hợp lệ, input không làm hỏng tranh.
3. Kiểm đặt lại, đổi tranh, ảnh mẫu, selector/options/settings và các control thật sự có trong bản build. Hoàn tác chỉ ghi nếu phiên bản có chức năng đó.
4. Đổi tranh rồi quay lại: kiểm tiến độ theo hành vi sản phẩm. Code hiện có lưu tiến độ trong session; không mặc định có lưu ra đĩa hoặc giữ sau khi thoát app.
5. Đóng/mở ít nhất 10 lần, xen kẽ NPC/Settings/cửa; modal cuối đóng mới trả điều khiển. VR giữ tracking/UI khi locomotion bị khóa.
6. Kiểm viewport/safe area, chữ tiếng Việt, màu/shader. Android thêm Back, background/resume và xoay màn hình theo cấu hình hỗ trợ.
7. Nếu bản build có lưu/xuất file: kiểm file hợp lệ, mở lại, quyền/sandbox và lỗi ghi. Nếu không có tính năng, ghi N/A, không tự suy ra từ hàm SaveCurrentProgress.
8. Tô liên tục 15–30 phút; lưu FPS/frame time, RAM/PSS, thời gian đổi tranh, crash/ANR. Windows mục tiêu 60 FPS; điện thoại mục tiêu tối thiểu 30 FPS trên thiết bị được chọn. Quest theo refresh rate của kính.

## Bằng chứng và trạng thái

- **Pass**: tất cả tiêu chí bắt buộc của đúng phạm vi đã kiểm đạt.
- **Fail**: có lỗi tái hiện, kèm bước và log/ảnh/video.
- **Blocked**: không thể chạy vì thiếu module/thiết bị/toolchain hoặc trở ngại cụ thể.
- **Chưa test**: chưa thực hiện; không gán Pass từ OS khác hoặc từ Editor.
- Log/artifact lần kiểm hiện tại ở `Logs/PlatformVerification`. Báo cáo đó là nguồn trạng thái, không dùng kết quả cũ làm kết quả mới.

Các suite: TestColoringWorkshopSuite, TestResponsiveLayoutSuite, TestSettingsLayoutPlayMode, TestArtworkRegionPlayMode, TestGameplayRegressionSuite, TestMainMenuPlayMode, TestAudioSettingsPlayMode, TestQuestLinkEntry và TestVRModalLocomotionPlayMode. Entry point có thể đổi scene/tạo fixture/thoát Unity: chỉ chạy trên bản sao.

## Kết quả đã thực hiện — 09/10/2026

Kiểm trên bản sao `Logs/NPCValidationProject`, Unity 6000.5.9f1. Không kết luận tương thích thiết bị từ các kết quả Editor.

| Phạm vi | Kết quả | Giới hạn |
|---|---|---|
| Windows x64 IL2CPP | Build thành công; smoke headless đi MainMenu → gallery → Settings → MainMenu hoàn tất | Chưa kiểm render/input/workshop trên cửa sổ bản build |
| Android Touch ARM64 | Xuất Unity và đóng gói APK thành công; giả lập LDPlayer Android 14 cài và mở menu được | Giả lập dùng dịch ARM; chưa kiểm workshop/lifecycle đầy đủ, chưa có điện thoại thật |
| Quest ARM64 OpenXR | Xuất Unity và đóng gói APK thành công | Chưa có Quest 2 để kiểm tracking/controller, di chuyển, UI và workshop |
| Quest Link | Test entry với loader giả và pointer mô phỏng đạt | Chưa kết nối kính thật |
| Gameplay / coloring | Editor 14/14 và 5/5 đạt | Không thay cho kiểm bản build/thiết bị |
| Workshop vùng tô | 21 entry đã lưu, 11.610 vùng cache, 11.744 assertion, 11 bước Play Mode đạt | Pointer mô phỏng; một entry dùng phân vùng native |
| Menu / audio / layout / VR modal | Các suite đạt; audio 8 bước, VR modal 15 bước, layout 36 tổ hợp hình học | Chưa xác nhận input vật lý, âm thanh nghe thực hoặc tracking |
| macOS / Linux / iOS / WebGL | Chưa test | Chưa chạy build/runtime của các mục tiêu này |
| Hiệu năng chơi dài | Chưa test | Chưa đo 15–30 phút hoặc nhiệt thiết bị |

Test vùng tô ban đầu thất bại vì còn giả định số tranh/vùng của phiên bản cũ. Đã cập nhật kiểm dữ liệu thật từng tranh, giữ các kiểm bounds/spans/mask và tô, rồi chạy lại thành công. Không tính lần thất bại cũ là PASS.

Build còn cảnh báo TMP import/IL2CPP, symbol/diagnostics và OnMouse trên handheld; Gradle còn thông báo API deprecated. Không kết luận Console sạch. Log, APK và bản sao được giữ cục bộ trong `Logs/PlatformVerification`, không đưa vào Git. Build development từ bản sao có probe smoke riêng; đây là artifact kiểm thử, chưa phải bản phát hành.
