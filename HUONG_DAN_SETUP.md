# Hướng dẫn setup và bàn giao BAINHOM

Dự án triển lãm tranh Đông Hồ dùng workshop tô vùng kín bằng `ColoringPageMinigame`: chọn màu rồi bấm vào vùng trong ảnh nét. Không cần cắt ảnh thành các nút mảnh tranh. Hướng dẫn áp dụng cho **Unity 6000.5.9f1**, cập nhật ngày 04/10/2026.

## 1. Mở dự án và chạy lần đầu

1. Clone/tải đầy đủ dự án, giữ cả `.meta`. Trong Unity Hub chọn **Add > Add project from disk**, trỏ tới thư mục chứa `Assets`, `Packages`, `ProjectSettings`.
2. Mở bằng **6000.5.9f1**; chờ package resolve và import/compile xong. Không đổi phiên bản package chỉ để bỏ cảnh báo.
3. Mở `Assets/Scenes/MainMenu.unity`. Triển lãm nằm ở `Assets/Scenes/SampleScene.unity`.
4. Trong **File > Build Profiles**, kiểm Scene List: MainMenu đứng đầu, SampleScene đứng sau, cả hai được bật. Cài module Windows/Android tương ứng qua Hub nếu thiếu; chờ reimport hoàn tất sau khi đổi target.
5. Bấm Play, dùng nút bắt đầu tham quan. Trong gallery thử tranh, NPC và workshop qua cửa/bàn/hướng dẫn viên.
6. Dừng Play trước khi cấu hình; thay đổi trong Play thường mất khi dừng. Sau mỗi lượt chỉnh scene, **Ctrl+S**, kiểm dấu `*` trên tên scene đã hết.

Workshop và menu cửa phải đóng khi lưu scene. Không bật `Panel_WorkshopColoring` làm màn hình khởi động: panel có thể che nút Play trong MainMenu.

## 2. Các menu setup

Các scene hiện có đã được cấu hình. Chỉ chạy setup cho phần cần cập nhật; lưu scene và sao lưu tùy chỉnh trước khi chạy.

| Menu Tools | Điều kiện | Thay đổi được tạo | Sau khi chạy |
|---|---|---|---|
| **Setup Workshop Tô Màu** | Scene cần workshop, dừng Play | Tạo/tái sử dụng WorkshopColoringCanvas, panel, safe area, khung ảnh nét/mẫu, palette, chọn tranh và nút; nối ColoringPageMinigame và cửa tìm thấy. Giữ danh sách tranh hiện có; danh sách mới trống. Bỏ Instructions, để panel đóng. Có thể bật Read/Write cho ảnh mặc định của tool. | Kiểm tham chiếu cửa/bàn, thêm tranh, Ctrl+S. Tool có thể đặt lại style/vị trí UI tùy chỉnh. |
| **Workshop tô màu > Thêm cặp tranh** | Có ColoringPageMinigame, dừng Play | Mở cửa sổ authoring; khi bấm thêm, tạo tài nguyên cạnh ảnh nguồn và append một mục vào workshop. | Kiểm preview, lưu scene; xem phần 3–5. |
| **Setup Gallery NPCs** | Đúng SampleScene, workshop có panel; có BusinessManVisual.prefab và model khách tham quan Casual Female 01 | Tạo prefab/instance còn thiếu; GalleryGuide cập nhật visual sang Business Man và GalleryVisitor cập nhật sang Nữ Casual 01 (URP + idle animation). Giữ lời thoại, collider, vị trí và liên kết workshop đã gán; chỉ nối bàn khi hướng dẫn viên chưa có liên kết. | Kiểm collider, lời thoại và workshopTrigger, Ctrl+S. Prefab được cập nhật trên disk; Undo scene không thay thế việc quản lý asset. |
| **Tự Động Setup 3 Nút Menu Cánh Cửa (Panel_DoorMenu)** | Có cửa tên cua hoặc DoorMenuTrigger; có panel gán doorMenuUI hoặc Panel_DoorMenu dưới cửa | Cập nhật chữ/font tiếng Việt, ba nút/callback và DoorMenuLayout; tái sử dụng hierarchy, giữ collider đã có, để menu đóng. | Kiểm minigameUI trỏ workshop; Ctrl+S. Nếu thiếu panel, gán panel hiện có trước. |

Scene mới: setup workshop trước, nối panel vào bàn/cửa, rồi setup NPC và menu cửa. Tool không tự tìm mọi đối tượng hoặc vị trí phù hợp cho level mới. Có nhiều workshop thì chọn đúng component trong ô Workshop, không chỉ dựa vào nút Tìm.

## 3. Thêm tranh từ ảnh nét

1. Đưa ảnh vào thư mục dưới Assets, ví dụ `Assets/TRAnh`. Ưu tiên nét đen rõ, nền sáng, đường bao kín. Import ảnh mẫu đã tô nếu có.
2. Mở scene có workshop, chọn **Tools > Workshop tô màu > Thêm cặp tranh**.
3. Chọn tab **Thêm tranh từ ảnh gốc**; kiểm ô Workshop. Bấm Tìm hoặc kéo component ColoringPageMinigame vào ô.
4. Nhập Tên tranh, chọn **Từ ảnh nét**, kéo texture vào Ảnh tranh gốc. Ảnh mẫu hoàn thiện là tùy chọn.
5. Chỉnh tham số và xem preview sau mỗi lần đổi:

   | Tham số | Cách dùng |
   |---|---|
   | Ngưỡng nét viền đen | Tăng nếu nét xám bị bỏ sót; giảm nếu mất vùng sáng nhỏ. |
   | Lấp khe hở nhỏ (pixels) | Khép khe trong đường bao; tăng ít một để không mất chi tiết. |
   | Vùng tối thiểu (pixels) | Diện tích vùng nhỏ bị loại khỏi phần cần tô, không phải độ rộng cạnh. |
   | Làm mịn biên nét | Giảm nhiễu biên; kiểm lại chi tiết nhỏ sau khi bật. |

6. Bấm/rá chuột trên preview để xem vùng. Màu pastel chỉ phân biệt nhãn, không quy định màu người chơi phải tô.
7. Nếu hai phần cần tô bị thông nhau, kiểm cảnh báo rò/gộp. Thử lấp khe; nếu cần bật **Vẽ nét ngăn thủ công trên Preview**, kéo qua khe với bán kính vừa đủ. Xóa nét vẽ tay bỏ nét bổ sung; đổi ảnh cũng bỏ nét vẽ tay.
8. Nếu có ảnh mẫu, chỉnh scale/offset hoặc căn sau khi thêm theo phần 5.
9. Bấm **Thêm tranh vào workshop**, kiểm đường dẫn asset trong hộp thoại, Ctrl+S. Tool chỉ gắn dữ liệu sau khi import/validation thành công; bị từ chối thì sửa preview và thử lại.
10. Kiểm trong game theo phần 6. Không sửa riêng width/height trong JSON để chữa lỗi: nhãn, span và mask cũng phải khớp ảnh thực tế.

Chế độ ảnh nét sinh bản chuẩn hóa riêng, không ghi đè nội dung ảnh nguồn. Việc chuẩn bị ảnh ở chế độ này có thể cập nhật importer; review `.meta` khi bàn giao.

## 4. Tạo tranh tô từ ảnh màu

Tool gộp màu gần nhau, tách mảng liên thông rồi sinh nét từ ranh giới. Ảnh màu phẳng thường dễ xử lý hơn ảnh chụp/tranh nhiều texture; luôn kiểm và sửa preview.

1. Import ảnh nguồn vào Assets. Dừng Play, mở Thêm cặp tranh.
2. Chọn đúng Workshop, nhập tên và chọn **Tạo tranh tô từ ảnh màu**.
3. Kéo texture vào Ảnh tranh gốc. Không cần chọn thêm mẫu; tool tạo bản mẫu riêng từ pixel texture đã import. Mặc định **Nét theo biên mảng màu**: mảng đen cũng có interior trắng để tô; độ tối không tự quyết định nét.
4. Chỉnh tham số trước khi sửa vùng:

   | Tham số | Ý nghĩa |
   |---|---|
   | **Mức gộp màu (Lab)**, 2–40 | Khoảng cách màu để gộp, không phải số màu đầu ra. Tăng để gom texture/màu gần; giảm để giữ khác biệt. Mặc định 24. |
   | **Gộp vùng nhỏ dưới (px)**, 1–2000 | Diện tích mảng nhỏ xét gộp vào vùng kề; chi tiết có màu và cạnh tương phản rõ được bảo vệ kể cả dưới ngưỡng. Tăng để giảm vụn nhưng phải kiểm chi tiết có chủ ý. Có thể thử 200 với tranh texture rồi điều chỉnh. |
   | **Giảm nhiễu giữ cạnh**, 0–1 | Lọc có trọng số theo khoảng cách màu/vị trí; không trộn qua nét tối được bảo vệ. Tăng để giảm texture. |
   | **Giữ cạnh**, 0–1 | Tăng để bảo vệ ranh giới rõ khi lọc/gộp; mức cao có thể giữ cả nhiễu. |
   | **Độ dày nét sinh (px)**, 1–8 | Chỉ điều khiển biên sinh mới; nét tối gốc giữ bề dày gốc. Bắt đầu ở 1. |

5. Chọn preset **Màu phẳng / Tranh có texture / Ảnh chụp** trước khi sửa vùng; preset đổi các tham số và reset chỉnh sửa. Cả ba preset mặc định không coi vùng tối là nét. Xem **Ảnh nguồn (trước lọc)**, **Sau lọc giữ cạnh** hoặc **Nét chồng ảnh nguồn**, **Ảnh trắng để tô / vẽ nét ngăn**, **Vùng tô (bấm để sửa)**. Dùng **Zoom preview** và thanh cuộn để kiểm chi tiết; bấm vùng trên preview đã zoom vẫn dùng grid nguồn. Cùng màu ở hai vị trí rời nhau vẫn là mảng riêng; nét có thể chia một mảng thành nhiều interior rời, xuất thành vùng tô riêng.
6. Chọn **Kiểm tra vùng**, bấm preview vùng để đọc thông tin. Màu pastel là mã vùng, không phải palette bắt buộc.
7. Chọn **Loại / khôi phục**, bấm nền/mảng không cần tô. Vùng đã loại hiển thị xám, không nhận tô; bấm lại để khôi phục. Nền trắng vẫn cần quyết định có tô hay không; pixel alpha dưới 128 tự loại.
8. Chọn **Gộp vùng**, bấm lần lượt hai mảng sát nhau. Tool từ chối vùng rời nhau hoặc đã loại; khôi phục trước nếu cần gộp. Kiểm lại nét sau khi gộp.
9. Nếu một mảng thiếu nét nội bộ, chọn **Vẽ nét ngăn** rồi kéo trên ảnh trắng để chia vùng; bán kính 0 tạo nét 1 pixel. **Xóa nét vẽ tay** chỉ xóa nét mình thêm, không xóa biên tự sinh. Mask và spans được cập nhật từ cùng nhãn màu sau khi cắt bằng nét, không nhận diện màu lại.
10. Xử lý hết vùng hồng, bấm **Thêm tranh vào workshop**, kiểm asset và Ctrl+S.

**Giữ nét tối gốc (tùy chọn)** mặc định tắt. Chỉ bật nếu muốn giữ nét gốc không tô; preview bổ sung đánh dấu đỏ toàn bộ pixel bị giữ làm nét để kiểm có nuốt mảng màu tối không. Tắt lại để mảng đen có interior trắng; đổi tùy chọn phải phân tích lại và reset sửa tay. **Làm mượt biên yếu** hạn chế răng cưa ở cạnh ít tương phản. **Nối khe nét nhạt 1 px** là tùy chọn: chỉ nối hai đầu nét có dấu xám ở khe; không tự khép khoảng trắng sáng có chủ ý. Nút Cancel trong progress bar hủy phân tích/sinh preview, chưa lưu tài nguyên. Xử lý giới hạn 30 giây mỗi giai đoạn phân vùng/sinh output; quá hạn cần giảm kích thước hoặc tham số.

### Vùng hồng và reset chỉnh sửa

**Hồng báo mảng bị nét chiếm hết phần bên trong, không còn pixel để tô. Tool chặn lưu khi còn mảng này.** Giảm độ dày nét, gộp với vùng kề thích hợp, hoặc chủ động loại nếu là chi tiết không cần tô. Nút **Loại … vùng màu hồng không đủ chỗ tô** loại các mảng đang mất; xem ảnh trước vì chúng sẽ không còn cần tô.

Đổi **preset/ảnh/chế độ nguồn/giảm nhiễu/Lab/giữ cạnh/diện tích vùng nhỏ/tùy chọn nét và biên**, hoặc bấm **Phân tích lại / bỏ chỉnh sửa vùng**, sẽ reset các chỉnh sửa gộp/loại và nét vẽ tay. Chọn tham số này trước rồi mới sửa tay. Đổi **độ dày nét** giữ chỉnh sửa nhưng có thể tạo vùng hồng mới, phải kiểm lại.

### Asset sinh ra

Tài nguyên nằm **cạnh ảnh nguồn**, theo tên tranh đã chuẩn hóa:

| Asset | Công dụng |
|---|---|
| `<Tên>_LineArt.png` | Ảnh nét dùng trong workshop. |
| `<Tên>_PaintMask.png` | Pixel được phép tô. |
| `<Tên>_RegionData.json` | Kích thước, nhãn, span và số pixel; runtime dùng cache. |
| `<Tên>_Reference.png` | Chỉ chế độ ảnh màu: bản mẫu cùng grid với nét, giữ màu trước lọc giữ cạnh. |

Tên trùng tạo đường dẫn mới, không ghi đè nguồn/entry cũ. Muốn thay tranh đã có: tạo mục mới, kiểm trong game rồi chỉnh danh sách paintings trong Inspector và lưu scene.

Grid lấy từ **texture thực sau import**, không nhất thiết bằng file nguồn. Override có thể khiến ảnh nguồn được import khác kích thước trên Windows/Android, dẫn tới kết quả phân vùng mới khác nhau. Bộ đầu ra đồng bộ được cấu hình giữ grid khi đổi target. Đừng chỉnh riêng Max Size, NPOT hoặc Compression của một ảnh đã xuất; nếu đổi độ phân giải, tạo/kiểm lại cả bộ.

Giới hạn: 4 triệu pixel, 100.000 component, palette gộp tối đa 256 màu. Ảnh lớn hơn cần chuẩn bị nguồn nhỏ hơn. Tool không biết ngữ nghĩa thân gà/lá cây/nền: gộp mạnh có thể mất chi tiết, gộp ít tạo vùng khó chạm. Kiểm mỹ thuật và khả năng tô trên thiết bị mục tiêu.

## 5. Căn tranh đã thêm

1. Mở scene, dừng Play, mở Thêm cặp tranh và tab **Căn tranh đã thêm**.
2. Chọn Cặp tranh; cần cả ảnh nét và mẫu.
3. Chỉnh Phóng to / thu nhỏ, Dịch ngang/dọc trên preview. Scale quanh tâm; offset tính theo kích thước nét đã fit. Mẫu có kích thước pixel nguồn khác nét có thể cần scale khác 1.
4. Bấm **Lưu căn ảnh mẫu**, Ctrl+S. Chỉ dữ liệu cặp chọn được cập nhật, không reset cặp khác.
5. Vào workshop kiểm ngang/dọc, resize; bấm tâm và gần bốn góc. Phần trống ngoài ảnh không nhận tô, lớp nét không lệch ảnh tô.

Đúng tỷ lệ khung chưa chứng minh nội dung hai ảnh khớp. Đối chiếu chi tiết thật; nếu crop/biến dạng khác nhau, chuẩn bị lại nguồn thay vì cố sửa bằng một offset.

## 6. Kiểm tranh trong game

1. MainMenu → gallery → workshop qua cửa/bàn/lựa chọn hướng dẫn viên.
2. **Chọn tranh +** → mục mới; kiểm tên, mẫu, nét, palette.
3. Chọn màu, bấm bên trong một vùng. Chỉ vùng đó đổi; nét, vùng kề và nền đã loại giữ nguyên.
4. Tô A → chuyển B → quay A; kiểm màu/tiến độ. Đóng/mở workshop trong cùng instance rồi kiểm lại.
5. **Làm lại** xóa tiến độ tranh đang chọn; khi hoàn thành dùng Tô lại hoặc Về triển lãm.
6. **… > Setting** → chỉnh âm lượng → đóng Settings. Gameplay vẫn khóa khi workshop mở; phục hồi khi mọi modal đóng.

Tiến độ nằm trong bộ nhớ workshop, chưa có lưu qua thoát game/dừng Play/tải lại scene tạo instance mới. BGM/âm lượng dùng chung xuyên scene trong phiên chơi, chưa phải lưu qua lần chạy ứng dụng mới.

## 7. NPC, cửa và ánh sáng

### NPC

Kiểm tra NPCInteractable trên các NPC trong triển lãm:
- **GalleryGuide**: Hướng dẫn viên (Business Man), có liên kết workshopTrigger.
- **GalleryVisitor**: Khách tham quan sảnh chính (Nữ Casual 01).
- **GalleryVisitor_Floor1**: Khách xem tranh Tầng 1 (Nam Casual 01) đứng trước tranh Đàn lợn âm dương.
- **GalleryVisitor_Floor2**: Khách xem tranh Tầng 2 (Nữ Casual 02) đứng ở tầng 2 trước khu tranh lịch sử / tứ bình.

Collider thân chọn NPC/cản ray; trigger proximity riêng theo dõi người chơi. Rigidbody kinematic, không gravity; không gắn PlayerController vào NPC.

Chỉnh lời thoại rồi Ctrl+S. NPC thường có nút **Tiếp tục**; hướng dẫn viên có lựa chọn tham quan/workshop ở cuối. Disable component phải chặn tương tác và đóng hội thoại chính NPC đó. Kiểm hai NPC gần nhau không chuyển câu của người khác.

### Cửa và workshop

DoorMenuTrigger.doorMenuUI trỏ panel cửa, minigameUI trỏ Panel_WorkshopColoring. Bàn dùng MinigameTrigger với cùng panel. Menu **CỬA TRIỂN LÃM**, phụ đề **Bạn muốn làm gì?** có:

- **Vào workshop tô màu**: đổi từ menu sang workshop, tiếp tục khóa gameplay.
- **Tiếp tục tham quan**: đóng menu, phục hồi khi không còn modal.
- **Về menu chính**: tải MainMenu, không thoát ứng dụng.

Kiểm không trùng callback Button On Click; panel đóng trước lưu. Giữ collider thân cửa cản vật lý nếu có; trigger tương tác phải bao phủ nơi người chơi đứng được.

### Tranh treo tường và lighting

PaintingInfo/PaintingTrigger cấu hình tên/nội dung/Voice Narration; khác danh sách tranh workshop. SampleScene đã tắt fog, giữ ambient được tăng trước đó và lightmap người dùng bake. Khi chỉnh đèn/geometry, kiểm cửa vào/giữa phòng/góc tối trước khi bake lại. Không tăng exposure, ambient và đèn đồng thời khi chưa biết nguồn; kiểm màu tranh và chi tiết vùng sáng.

## 8. Điều khiển và Settings

| Nền tảng | Di chuyển/camera | Tương tác |
|---|---|---|
| PC | WASD/phím hướng; Shift chạy, Space nhảy; chuột nhìn, cuộn zoom; C đổi góc nhìn, Left Alt đổi trạng thái chuột camera desktop. | Lại gần/hướng mục tiêu, E hoặc click; Tiếp tục qua E/nút UI; Esc mở/đóng Settings. |
| Mobile | Joystick trái, vuốt vùng nhìn, pinch zoom, nút nhảy; tùy chọn joystick nổi/cố định và tâm ngắm. | Tap mục tiêu/nút hội thoại; chọn màu rồi tap vùng. Kiểm dọc/ngang và safe area. |
| VR | Rig XR/locomotion theo cấu hình controller; tracking/pointer giữ khi modal mở. | Trigger tương tác, pointer bấm UI, nút menu mở Settings. Mapping phải kiểm trên rig/thiết bị thực. |

Settings có master/BGM/SFX, chọn bài, giới hạn FPS. Master qua AudioListener một lần; BGM có âm lượng riêng. Master 0,2/BGM 0,5 phải hiển thị đúng sau chuyển scene. Chọn bài không tạo nguồn nhạc thứ hai; mở Settings để đồng bộ UI không phát lại bài đang chạy. XR hoạt động bỏ cap FPS desktop để ưu tiên pacing XR.

Các modal khóa controller desktop/mobile và locomotion VR. Đóng một trong hai bảng không mở khóa bảng còn lại; provider vốn disabled vẫn disabled sau resume.

## 9. Lỗi thường gặp

| Hiện tượng | Kiểm tra/cách xử lý |
|---|---|
| Play bị che/workshop mở ngay | Dừng Play, để Panel_WorkshopColoring inactive trong MainMenu và lưu. |
| Tool thiếu workshop/panel | Đúng scene/component/tham chiếu; setup cửa cần panel có sẵn, setup workshop tạo panel workshop. |
| Thêm tranh bị khóa | Dừng Play; đủ workshop/tên/ảnh asset/vùng tô; xử lý hết vùng hồng/lỗi phân tích. |
| Tô lan vùng khác | Preview có chung nhãn do khe/gộp. Ảnh nét: khép khe/vẽ ngăn. Ảnh màu: giảm Lab, kiểm gộp rồi tạo bộ mới. |
| Nền vẫn tô được | Ảnh màu: Loại / khôi phục để loại nền, kiểm xám trước lưu. |
| Quá nhiều vùng vụn | Lọc giữ cạnh/tăng Lab/vùng nhỏ/gộp kề; kiểm không mất chi tiết có chủ ý. |
| regionData không hợp lệ/fallback | Line/mask/JSON phải cùng grid/cùng entry. Không sửa width/height riêng; tạo lại cả bộ và kiểm cache. Giữ bản cũ tới khi bản mới đạt. |
| Mẫu lệch/nhỏ | Căn cặp đã thêm, kiểm pixel/crop nguồn. Không sửa transform do layout quản lý lúc chạy. |
| Tiếng Việt thiếu dấu | Kiểm font TMP/glyph. Menu cửa dùng LiberationSans động; có thể chạy setup cửa cập nhật UI. |
| BGM chồng/volume đổi | Kiểm AudioManager, nguồn local/dropdown clip; không thêm nguồn BGM tự chạy độc lập. Thử menu → gallery → menu. |
| Package Manager: Operation cancelled | Ca 03/10: UPM dừng bất thường → IPC mất kết nối; Unity tự restart. Retry online đạt, manifest/lock không đổi. Chưa rõ nguyên nhân nội bộ crash. Nếu tái phát: lưu scene, thu log trước restart Editor/Hub; không mặc định xóa cache/lock/nâng package. |
| Services/My Assets: 401 | Kiểm đăng nhập Hub/quyền dự án; endpoint Services không tự chứng minh registry lỗi. Không đưa token/proxy cá nhân vào Git. |
| SearchDatabase.ArgumentOutOfRangeException | Stack đã thấy thuộc UnityEditor Search indexing; ghi log/thao tác, xử lý Editor riêng, không sửa gameplay chỉ từ thông báo này. |
| Licensing/Curl error 35 | Kiểm Hub/license/kết nối, tách startup đã phục hồi với resolve thất bại. Compile PASS không chứng minh Console sạch. |
| JSON/span sai trong test | Suite cố ý đưa dữ liệu lỗi để kiểm fallback. Đối chiếu tên test/stack; cảnh báo từ tranh thật cần xử lý riêng. |

Log hiện tại có thể ở `Logs/Editor.log`, `Logs/upm.log` trong dự án; bootstrap log tại thư mục Unity người dùng có thể chỉ báo chuyển chỗ. Bỏ token trước chia sẻ. [Hướng dẫn Package Manager của Unity](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-errors) giúp phân biệt lỗi registry, dependency và đăng nhập theo bằng chứng.

## 10. Test và checklist bàn giao

**Lưu scene/asset trước test; ưu tiên bản sao dự án.** Không mở batch test vào dự án đang dùng trong Editor.

### Manifest cho batch tranh và checkout mới

Đầu vào của `BatchSourceArtwork` và `TestAllSourceArtworks` nằm trong **Assets/Editor/ArtworkBatchData**, được lưu cùng Git và `.meta`:

- `Inventory.json`: danh sách 40 nguồn, đường dẫn tương đối trong Assets và tham số preview ban đầu.
- `Retry.json`: ba mục có preset thử lại. Chạy Preview trước Retry để có kết quả ban đầu.
- `Reviewed.json`: quyết định từng nguồn; chỉ chín mục `approved` được export. Không tự đổi mục bị loại thành approved.
- `Exported.json`: snapshot đã review, gồm đường dẫn line/mask/reference/JSON của chín bộ asset trong **Assets/ColoringArtworks/Generated**. Test dùng snapshot này, không cần preview local.

Ảnh nguồn được lưu trong Assets; không đổi đường dẫn sang thư mục Downloads hoặc ổ đĩa cá nhân. `ArtPreviews` là artifact local được Git bỏ qua. Các lệnh mới tạo preview, báo lỗi và kết quả trong **Logs/ArtworkBatch**, cũng được bỏ qua; không ghi lại các manifest đầu vào khi chạy.

Trên bản sao checkout, mở bằng Unity 6000.5.9f1 và đợi import/package resolve. Có thể dùng `-batchmode -projectPath "<đường dẫn bản sao>" -executeMethod <entry> -logFile "<đường dẫn log>"` với Unity.exe của phiên bản này. Các entry dưới đây **tự thoát Editor**; chạy từng lệnh, đợi hoàn tất trước lệnh kế tiếp:

| Entry | Công dụng và đầu ra |
|---|---|
| `TestAllSourceArtworks.BeginMain` | Audit grid/spans/cache, selector và tô/giữ tiến độ trong MainMenu; ghi Logs/ArtworkBatch/Play-MainMenu.txt. |
| `TestAllSourceArtworks.BeginSample` | Cùng kiểm tra cho SampleScene; ghi Play-SampleScene.txt. Hai test không lưu scene. |
| `BatchSourceArtwork.Preview` | Tạo preview theo Inventory; ghi từng thư mục nguồn và Previews.json trong Logs/ArtworkBatch. |
| `BatchSourceArtwork.Retry` | Đọc Previews.json vừa tạo và Retry.json đã lưu; giữ ảnh lượt đầu, thử lại ba mục. |
| `BatchSourceArtwork.Export` | Đọc Reviewed.json; tạo asset còn thiếu và thêm vào cả hai scene, **có lưu scene**. Chỉ chạy trên bản sao khi kiểm lại; không chạy để xem preview. Ghi Exported.json trong Logs/ArtworkBatch. |

Khi thêm hoặc thay nguồn đã được review: cập nhật inventory/preset/quyết định, tạo preview và review hình trước Export. Kiểm output Exported.json với asset thực; chỉ sau review mới cập nhật snapshot **Assets/Editor/ArtworkBatchData/Exported.json**, import JSON và giữ `.meta` hiện có. Không dùng số assertion để tự phê duyệt nguồn mới. Commit ảnh nguồn, asset sinh ra, snapshot và scene cần thiết; không commit Logs/ArtPreviews hoặc bản sao kiểm thử.

**Tools > Tests > Run Gameplay Regression Suite**, **Run Coloring Workshop Test Suite**, **Run All Review Checks** là kiểm Editor thông thường. Coloring có test tự chạy sau reload; đọc kết quả, phân biệt cảnh báo dự kiến.

**Run Saved Artwork Region Play Mode có thể thoát Editor khi xong.** Các entry tự động trong TestColorReferenceAuthoring, TestArtworkAuthoringDimensions, TestMainMenuPlayMode, TestVRModalLocomotionPlayMode, TestAudioSettingsPlayMode, TestSettingsLayoutPlayMode, TestDoorMenuFogSuite, TestColorPreprocessing.RunAndExit/RunAndAuthoring/AuditCrossTargetAndExit, TestColorBoundaryAuthoring.Begin/AuditGeneratedAndExit, TestMuseumEmployeeIntegration.Begin và camera/NPC có thể đổi scene, tạo fixture hoặc gọi EditorApplication.Exit. Chỉ chạy trên bản sao; đọc entry trước dùng -executeMethod. Pointer callback mô phỏng không phải mouse/touch/controller thật.

- [ ] Compile không lỗi C#; phân loại Console theo test/asset thật.
- [ ] Menu → gallery → workshop → gallery → menu; Play dùng được lần thứ hai.
- [ ] NPC thường/hướng dẫn viên, Tiếp tục/lựa chọn, NPC disabled.
- [ ] Mọi modal khóa/phục hồi input, gồm modal chồng nhau.
- [ ] Chỉnh BGM/volume trước gallery, đổi scene nhiều lần: một BGM, UI đúng.
- [ ] Mỗi tranh: preview không hồng, không tô ngoài vùng/nền; A → B → A, đóng/mở, reset.
- [ ] Reimport target khác: line/mask/reference/JSON đồng bộ, runtime cache không fallback ngoài ca lỗi chủ động.
- [ ] PC 16:9/21:9, mobile dọc/ngang/safe inset; resize/xoay khi UI mở.
- [ ] Camera tường/góc/zoom/phục hồi trên geometry level thật.
- [ ] Chuột/touch/âm thanh/controller/HMD thực; XR displacement, teleport và modal qua additive scene.
- [ ] Build Windows/Android, ghi version/target/kết quả; không ghi đạt thiết bị từ Editor target.
- [ ] Review meta/scene/callback/lightmap, giữ bake/geometry người dùng; không commit Logs/cache/validation/build.

### Phạm vi kiểm

Lượt nâng tiền xử lý 04/10/2026 bổ sung test nét một pixel, chi tiết bốn pixel, alpha, gradient/texture, khe sáng/khe nhạt, span, zoom, hủy, timeout và ảnh 2048×2048. Số vùng nhỏ gần nét gốc có thể tăng khi giữ lại hatching; không chỉ dùng tổng số vùng làm thước đo chất lượng. Preview cần được review trên từng tranh; chưa bảo đảm mọi ranh giới có ý nghĩa đã khớp. Bản tiền xử lý cũ đề xuất PARTIAL; lượt tiếp theo đổi mặc định sang biên màu, bổ sung nét ngăn vẽ tay và kiểm mảng đen. Báo cáo mới ở Logs/ColorBoundaryExecuted.md; đối chiếu cũ ở Logs/ColorPreprocessingExecuted.md (artifact local, không thuộc commit lượt trước).

Kết quả ngày 03/10/2026 trước nâng tiền xử lý:

Tool màu: Windows target **310 assertion**, Android target **424 assertion** trên Windows Editor, gồm tạo thật, runtime Play Mode và reimport reference từ target kia. Chế độ nét **245 assertion** ở lượt riêng; gameplay **13/13**, coloring **5/5**. Menu cửa setup **74 assertion**, Play Mode **101 assertion**, render 1920×1080/720×1600. Gallery chụp ba góc trước/sau fog trên cùng lightmap người dùng bake, không bake lại.

Các lượt trước kiểm audio, responsive layout, camera/NPC và enabled-state/lifecycle locomotion VR. Đây là Editor/PhysX/UI mô phỏng, **chưa chứng minh build APK/Windows, touch/HMD, âm thanh thiết bị, displacement/teleport thực hoặc modal qua additive scene**. Pivot camera trong tường và nội dung từng cặp ảnh cần kiểm level/thiết bị. Không hứa nhận diện hoàn hảo mọi ảnh màu, không kết luận Console sạch từ PASS.
