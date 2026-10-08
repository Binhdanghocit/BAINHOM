# Chạy game với Quest 2

## Chơi trên PC qua Quest Link / Air Link

1. Kết nối Quest 2 với Meta Quest Link và vào môi trường Link trong kính.
2. Đặt Meta Quest Link làm OpenXR runtime đang hoạt động.
3. Trong Unity chọn **Tools → Build Setup → Windows + optional PCVR** nếu chuẩn bị build Windows.
4. Mở game, chọn **Chơi VR (Quest Link)**. Nút Play thông thường giữ chế độ PC.

Ở tab PC của XR Plug-in Management, giữ OpenXR được chọn và tắt **Initialize XR on Startup**. Game khởi động loader khi chọn Chơi VR, tránh thử khởi tạo kính khi chỉ chơi PC. Nếu khởi tạo VR thất bại, game tiếp tục ở chế độ thường; kiểm kết nối Link và log trước khi thử lại.

## Chạy APK trực tiếp trên Quest 2

1. Chọn **Tools → Build Setup → Quest OpenXR** trước khi build Android.
2. Bật Developer Mode, kết nối USB, chấp nhận USB debugging trong kính.
3. Build và cài APK lên Quest 2, mở ứng dụng trong kính.

XRBoot nhận thiết bị Quest và tự khởi động VR. APK Quest không cần nút Quest Link. Không dùng profile **Android Touch** để build APK dành cho kính: profile đó loại bỏ XR loader.

## Kiểm sau khi kết nối kính thật

- Camera theo chuyển động đầu; hai controller có tracking.
- Ray/trigger bấm Play, Settings, hội thoại và thông tin tranh.
- Đi lại/teleport dừng khi mở modal, hoạt động lại khi đóng modal cuối cùng.
- Về menu rồi vào triển lãm lại không mất tracking hoặc phát nhiều nguồn âm thanh.

Các test Editor dùng loader giả và pointer mô phỏng chỉ kiểm luồng khởi động/UI; không xác nhận tracking, render hoặc input Quest 2 thật.
