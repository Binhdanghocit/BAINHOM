# Hướng dẫn thêm vật thể tương tác mới

Hệ thống tương tác trong project được thiết kế theo mô-đun: mỗi vật thể tự
đăng ký "vùng trigger" của mình, còn mọi logic hiển thị (viền vàng, prompt,
crosshair) nằm ở các class dùng chung.

## 1. Vật thể tương tác loại "Tranh" (mở popup thông tin)

### Bước 1 — Cấu trúc object
```
MyPainting                     (GameObject)
├── MeshRenderer + Collider     (vật thể bị tia raycast trúng)
├── Trigger Zone                (child, BoxCollider IS TRIGGER, không vẽ)
│   ├── InteractableOutline
│   ├── PaintingTrigger
│   └── PaintingInfo
└── (tùy chọn) PaintingNamePlate trên child label
```

### Bước 2 — Gán components (theo thứ tự)
1. **Collider trigger**: BoxCollider bao quanh vị trí đứng ngắm tranh → tick `Is Trigger`.
2. **InteractableOutline**: tự tạo LineRenderer vàng khi được highlight.
3. **PaintingInfo**: điền `paintingTitle`, `paintingDescription`, `paintingSprite`.
4. **PaintingTrigger**: không cần điền gì — tự tìm `PaintingInfo` + `InteractableOutline` trên cùng object.

### Bước 3 — Kiểm tra
- Đưa object vào scene, chạy Play.
- Bước vào trigger zone → hiện viền vàng + prompt "Nhấn E / Click để tương tác".
- Nhắm đúng vật + đứng trong vùng trigger → crosshair chuyển vàng.
- Bấm E / click chuột / trigger tay VR (VR) → mở popup. Bấm lần nữa → đóng.

## 2. Vật thể loại "NPC" (hội thoại)

```
MyNPC
├── Collider trigger (IS TRIGGER)
├── InteractableOutline
└── NPCInteractable
```

- **NPCInteractable** cần `InteractableOutline` (RequireComponent).
- Điền `npcName` + `dialogueLines` (từng dòng = 1 lần nhấn E).
- Khi player ở ngoài trigger → hội thoại tự đóng (OnTriggerExit).

## 3. Vật thể loại "Bục thông tin" (hiện ẩn UI khi đi qua)

- Chỉ cần **Collider trigger (IS TRIGGER)** + script **InfoPodiumTrigger**.
- Kéo object/Canvas muốn hiện vào `infoDisplay`.
- Không dùng viền vàng, không dùng prompt.

## 4. Quy tắc quan trọng (tránh bug đã từng gặp)

1. **Collider trigger phải tick "Is Trigger"**, và object trigger KHÔNG được
   trùng vị trí với collider vẽ của vật (nên đặt trigger zone làm child).
2. **Người chơi (player) phải được nhận diện** — 1 trong 3 cách, theo thứ tự ưu tiên:
   - Gán **Tag "Player"** (đã có sẵn trong TagManager) trên root object player.
   - Hoặc ViewModeController trỏ đúng `desktopPlayerRig` / `vrRig`
     (script tự đăng ký qua `PlayerDetector.RegisterRoot`).
   - Hoặc rig desktop có `CharacterController` / `PlayerController` (fallback tự động).

   Mọi system (InteractableOutline, PaintingTrigger, NPCInteractable,
   InfoPodiumTrigger, CrosshairReticle) đều dùng chung `PlayerDetector.IsPlayer()`
   → đừng viết lại logic nhận diện player trong script mới.
3. **Một nút bấm chỉ kích hoạt MỘT hệ thống**: nếu popup tranh đang mở thì
   trigger của NPC (và ngược lại) sẽ bị bỏ qua. Không cần lo cạnh tranh
   khi nhiều trigger zone chồng lấn.
4. Muốn vật mới có viền vàng nhưng loại tương tác khác → chỉ cần
   `InteractableOutline` + script riêng gọi `outline.SetProximity(bool)`
   trong `OnTriggerEnter/OnTriggerExit` (xem `PaintingTrigger` làm mẫu).

## 5. Checklist nhanh

- [ ] Trigger zone: BoxCollider + Is Trigger
- [ ] InteractableOutline (nếu cần viền vàng)
- [ ] Script tương tác đúng loại + dữ liệu đầy đủ
- [ ] Player nhận diện được (Tag hoặc Rig đăng ký)
- [ ] Test: vào vùng trigger → viền vàng + prompt; bấm E/trigger → đúng hành vi
- [ ] Test đi ra xa → viền vàng biến mất, UI tự đóng
