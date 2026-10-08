# Build và kiểm runtime PC / Android / Quest

Lưu scene và công việc đang mở trước khi đổi Active Input Handling hoặc target. Các menu dưới đây cấu hình dự án, không tự build và không đổi scene. Input System có thể yêu cầu khởi động lại Editor. Chọn đúng target trong File > Build Profiles sau khi chạy setup.

| Loại bản build | Menu Tools > Build Setup | XR |
| --- | --- | --- |
| Windows, có thể vào PCVR | Windows + optional PCVR | OpenXR loader, Oculus Touch; khởi động thủ công bằng XRBoot |
| Điện thoại Android thông thường | Android Touch | Không có loader hoặc feature XR bật; không yêu cầu Quest |
| Quest | Quest OpenXR | OpenXR loader, Meta Quest Support, Oculus Touch; XRBoot tự khởi động trên Quest |

Ba cấu hình dùng **Input System Package (New)**. GameplayInput đọc keyboard, mouse và Enhanced Touch; chế độ Both cũng chỉ đọc backend mới một lần. Joystick, vuốt, nhúm, nhảy, tap và UI tiếp tục sử dụng cùng tọa độ màn hình. Android/Quest dùng IL2CPP ARM64, min SDK ít nhất 32. Menu Tools > Setup Mobile Build (Android) chọn cấu hình Android Touch; muốn Quest phải dùng menu Quest riêng sau đó.

Setup thay feature của **target đang cấu hình** bằng tập cần cho gallery; các feature AR/MR/Android XR khác sẽ tắt trên target đó. Dự án dùng manual XR lifecycle, nên Init XR on Startup và automatic loading/running đều tắt. Lựa chọn Input Handling là setting dùng chung toàn dự án. Không dùng một cấu hình Android touch để kết luận APK đó hỗ trợ Quest. Giữ package version hiện tại; không sửa PackageCache.

`Assets/XR/link.xml` giữ các type feature Meta/Android OpenXR khi IL2CPP stripping trên target có assembly đó. Các type có thể vẫn được tham chiếu trong asset settings dù feature đã tắt. Assembly Meta hiện không có trong Windows Player: `OpenXRRuntimeSettingsBuild` tạo bản sao settings/feature chỉ dành cho build, bỏ tham chiếu disabled có assembly không hỗ trợ target; giữ nguyên asset Editor và dữ liệu Android. Feature unsupported mà đang bật sẽ chặn build để yêu cầu chọn đúng profile. Bản sao tạm trong Assets/__RuntimeXRBuild được dọn sau build, hoặc phục hồi qua state trong Library khi build hủy/reload; thư mục này không đưa vào Git. Sau thay linker/profile, dùng **Clean Build** tới thư mục đầu ra mới; chạy bản mới và đọc Player.log, không lấy compile/build thành công thay cho kiểm runtime.

## Quy tắc tương tác

E/nút Interact trên màn phẳng: thử ray trước; nếu ray không chọn được thì xét collider trong phạm vi, phía trước (cone 60°), cùng độ cao gần người chơi và không bị collider đặc che. Trong các mục hợp lệ, mục gần nhất được chọn chung cho NPC/tranh/cửa/workshop. Collider trigger không tương tác không che ray. NPC bị disable không tương tác; collider thân vẫn là vật cản.

Click khi con trỏ mở và tap chọn đúng ray tại vị trí chạm. Click/tap trên UI không mở đối tượng phía sau. Các thao tác này không dùng fallback gần. XR chỉ dùng controller ray: thiếu ray hoặc trỏ trượt không mở NPC/cửa gần người chơi. UI đang mở vẫn giữ modal guard.

## Bàn giao kiểm thiết bị

### Lỗi đóng gói Gradle trên môi trường kiểm 08/10/2026

Lỗi `Unable to establish loopback connection` ở lượt này có stack `PipeImpl → WEPollSelectorImpl → UnixDomainSockets`, nguyên nhân trực tiếp `Invalid argument: connect`. Probe TCP IPv4/IPv6 đạt, nhưng NIO Pipe/Selector mặc định thất bại. Đây không phải bằng chứng lỗi input/gameplay hoặc registry package. Trên máy kiểm, property **`jdk.net.unixdomain.tmpdir`** trỏ tới thư mục tạm riêng trong Logs làm probe đạt; cần áp cho **cả Java cha và JVM con**, không chỉ `java.io.tmpdir` hoặc `preferIPv4Stack`.

Đường đã kiểm trên máy này là **Export Project → Gradle CLI**, với `JAVA_TOOL_OPTIONS` bổ sung `-Djdk.net.unixdomain.tmpdir=<thư mục tạm>` trong phiên đóng gói và cùng property trong JVM arguments. Khôi phục biến của phiên sau khi chạy. Chỉ truyền flag cho Java cha chưa đủ: Gradle daemon cũng cần nhận property. Có thể thử khởi động lại Editor từ phiên shell có biến này, nhưng lượt kiểm này chưa xác nhận Unity chuyển biến tới Gradle; không coi đó là cách sửa đã đạt. Không ghi đường dẫn máy cá nhân vào gradle.properties/template của dự án, không xóa cache/lockfile hoặc đổi package chỉ vì lỗi này. Đọc log lần thử lại và xác nhận APK thực được tạo; sự cố NIO được xử lý không tự chứng minh runtime Android/Quest.

Phạm vi người dùng chọn ngày 08/10/2026: PC/Editor; Android và Quest bàn giao để thử thiết bị. Các test queue input hoặc pointer mô phỏng không phải chuột/touch/controller vật lý.

### PC

1. Clean build Windows bằng IL2CPP; mở MainMenu, Play, gallery, Settings, NPC, cửa và workshop; quay menu rồi Play lại.
2. Thử E, click khi khóa/mở con trỏ, C đổi góc, Alt đổi khóa, WASD/Shift/Space, rê chuột và cuộn zoom.
3. NPC sau tường/khác tầng không mở qua fallback. Hai đối tượng lệch khỏi tâm nhưng trong cone: cửa gần hơn NPC phải được chọn. Trigger rỗng không che body ray.
4. Đọc `%USERPROFILE%/AppData/LocalLow/Nhom3/Triển Lãm Tranh Đông Hồ/Player.log`; lưu cả log và SHA256 build. Tìm `different serialization layout`, `referenced script`, exception input và stack gameplay.

### Android touch

1. Build Android Touch tới thư mục riêng; cài APK lên điện thoại và thu `adb logcat`, ghi model/Android/GPU.
2. Joystick cố định/nổi: chạm bắt đầu không tự chạy, kéo hướng/độ lớn đúng, nhấc tay dừng. Vuốt nhìn, nhúm zoom và Nhảy; thử nhiều ngón đồng thời.
3. Tap nhanh trên NPC/tranh ở nửa phải, tap trượt, tap UI, giữ/vuốt dài: chỉ tap mục tiêu hợp lệ mở tương tác. Thử Settings → đóng → điều khiển lại, xoay màn hình khi UI mở.
4. Chuyển menu/gallery nhiều lần; kiểm log không lỗi input/serialization. Không có loader XR trên điện thoại.

### Quest / PCVR

1. Build Quest riêng; kiểm Meta Quest Support và Oculus Touch bật, OpenXR loader được gán. PCVR dùng cấu hình Windows và runtime OpenXR tương ứng trên máy.
2. Trỏ controller ra khoảng trống rồi bấm gần NPC/cửa: không mở. Trỏ thân mục tiêu trong phạm vi rồi bấm: mở đúng mục tiêu; UI pointer vẫn thao tác được.
3. Mở từng modal, hai modal chồng nhau; thử displacement thật, move/turn/grab/teleport, kể cả teleport đã xếp hàng. Đóng modal cuối mới phục hồi locomotion; provider vốn disabled vẫn disabled.
4. Đổi rig, chuyển scene và scene additive trong lúc modal; giữ tracking và UI pointer. Thu log startup/chuyển scene/shutdown để xác nhận layout serialization trên thiết bị.

## Chạy hồi quy trên bản sao

`TestRuntimeInteraction.Run`: batchmode, scene/PhysX selection. `TestRuntimeInteraction.BeginInput`: batchmode Play Mode, input device events và joystick/UI hit testing; có thể thoát Editor. Lưu công việc trước, chạy trên bản sao dự án, không chạy lên Editor đang mở. Gameplay, camera, VR modal, menu và audio có entry point riêng trong Assets/Editor.

Kết quả cụ thể, log và diff của lượt này nằm trong `Logs/RuntimeXRInteractionReview/EXECUTED.md` ở workspace; Logs/build/bản sao không đưa vào Git. Giới hạn thiết bị vẫn phải được ghi kể cả khi assertion và build đều đạt.
