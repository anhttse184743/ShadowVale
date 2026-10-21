# Đồng bộ dev/thinh — cập nhật 21/10/2026

Người dùng đã yêu cầu hoàn tất kiểm tra, merge vào dev và push cả thinh/dev. Lựa chọn này cho phép commit và push phần đã kiểm chứng; thay yêu cầu chưa push của lượt merge trước. Không push cưỡng bức và không chạm origin/dev2.

- thinh trước sửa: 6c86097bfbf51b6585b3450e7ca53d90cd44436c.
- origin/thinh vừa fetch: e44dab1e4be14f14d3524dc731486508b8677242.
- dev mới: fbff9a6d013a75c425d38201df64710e165e1752; thêm M16/thay băng/loot và model NPC, không đổi scene Map 1 so với dev đã ghép trước.
- Preflight riêng dev: 17/18 ca đạt ở lượt đầu; ca sát thương cũ mặc định mọi lính dùng AK. Test cập nhật kiểm sát thương và cooldown thật cho cả AK và M16 đạt 1/1. Không đổi số liệu vũ khí để làm test xanh.
- Chẩn đoán cũ của worktree preflight được giữ bằng stash có tên `Preserve dev preflight diagnostics before 2026-10-21 sync` trước khi checkout dev mới.

## Nguyên tắc giải xung đột

1. Giữ gameplay K54/Mosin, nạp từng viên và luật Mosin bắn xa hiện tại; thêm M16 vào cùng hệ thống súng chính.
2. Giữ `HeldAmmoId` cho từng loại vũ khí; nhánh Rifle đọc PrimaryAmmo (AK/M16). Không dùng PrimaryAmmo cho K54/Mosin.
3. M16 đổi model/đạn trong magazine Rifle. Khi đang cầm K54 hoặc Mosin mà đổi súng chính trong túi, chỉ trả đạn của magazine Rifle, không động vào đạn lục/nhắm. Dùng API MagazineRounds/RestoreMagazine có WeaponKind hiện có.
4. Giữ đường FireRound và chứng cứ vị trí của thinh, nhận cadence/90% sát thương M16 từ dev.
5. Save tiếp tục nhận đúng Rifle/Pistol/Sniper; cho phép rifle_m16 ở ô Primary và áp lại model trước phục hồi số đạn. HUD/Draw/IsEquipped nhận đủ AK, M16, K54, Mosin.
6. Hai material preview chỉ khác fileID phụ do Unity sinh: giữ bản đang dùng của thinh. Folder Weapons giữ GUID hiện có; asset M16 giữ GUID của dev. C3/scene được đối chiếu hash.
7. Kiểm lại M16 + K54/Mosin + lưu/tải + đạn khi đổi slot, C3 và các ca đã sửa trước khi push. Chỉ stage code/asset thật cần cho bản ghép và báo cáo gọn; không stage toàn bộ demo/recovery/save/video chưa track.

Mốc remote phải kiểm lại trước push; chỉ cập nhật nhánh theo fast-forward. Nếu dev có commit mới trong lúc test, phải xét phần mới trước khi xuất bản.
