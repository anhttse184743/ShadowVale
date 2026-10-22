# Mosin PU — chỉnh sát thương và tầm ngắm của Nam, 22/10/2026

Yêu cầu: Nam bắn trúng đầu hoặc người địch thì một viên hạ gục. Người dùng đã xác nhận “tầm ngắm địch là tầm ngắm của Nam”; không chỉnh tầm phát hiện, góc nhìn hoặc trí nhớ của AI.

## Tham khảo

Các tài liệu dưới đây là mốc thiết kế của từng phiên bản, không phải tuyên bố về meta hiện tại hoặc thông số của ShadowVale:

| Tài liệu gốc | Điều rút ra | Áp dụng vào ShadowVale |
|---|---|---|
| [MWIII Season 5, 2024 — XRK Stalker](https://www.callofduty.com/patchnotes/2024/07/call-of-duty-modern-warfare-iii-season-5-patch-notes) | Bản cập nhật cho phép XRK Stalker hạ mục tiêu bằng một phát bất kể vùng trúng, trong tầm sát thương tối đa. | Lethality áp dụng cho địch mà viên đạn thực sự chạm vào, trong giới hạn tầm bắn. Không cần bắt buộc headshot. |
| [PUBG Console 13.1, 2021 — SR/Mosin](https://www.pubg.com/en/news/1722) | Vai trò SR là bắn xa; hệ số sát thương xa được tăng 0,9 → 0,95, giảm trễ lên đạn 10% và tăng tốc độ bắn 20% trong bản cập nhật đó. | Tăng khoảng cách dùng được so với súng trường, giữ nhịp bolt chậm và khoảng ngắt rõ giữa các phát. Không sao chép sát thương PvP/giáp sang hệ HP của game này. |
| [Sniper Elite — manual do Steam phân phối, trang PDF 19](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/3700/manuals/SniperEliteManual.pdf?t=1579791723) | Manual liệt kê Mosin M91: băng 5 viên, ống 6× và tầm 1000 m trong ngữ cảnh game đó; có điều khiển zoom và tư thế ảnh hưởng độ ổn định. | Giữ băng 5 viên và phân biệt độ chính xác theo tư thế. ShadowVale đang dùng mẫu PU 3,5× đã tích hợp nên giữ đúng optic này; 200 m là lựa chọn riêng cho quy mô bản đồ. |

## Thông số triển khai

| Thuộc tính | Trước | Sau |
|---|---:|---:|
| Sát thương cơ sở | 95 | 200 |
| Địch trúng đầu/thân | Phụ thuộc HP | Một viên hạ gục, gồm lính nặng/chỉ huy khi có thể nhận sát thương |
| Tầm hitscan tối đa | 80 m | 200 m |
| Phóng đại PU thực | 3,5× | 3,5× |
| Băng đạn / nạp đầy | 5 viên / 5 s | 5 viên / 5 s |
| Tốc độ bắn | 0,7 phát/s | 0,7 phát/s — khoảng 1,43 s/phát |
| Cone nửa góc, đứng ngắm | 0,10° dùng chung súng trường | 0,01° — lệch tối đa khoảng 3,5 cm ở 200 m |
| Cone nửa góc, khom ngắm | 0,06° | 0,005° — khoảng 1,75 cm ở 200 m |
| Cone khi đi ngắm / chạy | 0,35° / 3° | 0,25° / 6° |
| Cone bắn ngang hông đứng | 0,40° | 2,5° |
| Giật camera mỗi phát | 0,16° dọc ở phát đầu, dùng chung AK | 1,8° dọc + 0,12° ngang, vẫn nhận hệ số kỹ năng |
| Tiếng súng trong gameplay | 30 m | 30 m |

Tầm 200 m là giới hạn thực của ray, không phải khoảng cách AI nhìn thấy Nam. Không thêm đạn xuyên vật cản hoặc mô phỏng đạn rơi/gió trong lượt này.

`Weapon.lethalAgainstEnemies` chỉ nâng sát thương đủ hạ mục tiêu có `IEnemyAgent`. Viên đạn vẫn qua raycast, `Health`, bảo vệ cinematic, sự kiện chết, XP, loot và save hiện có. Health của Nam/Hùng hoặc đối tượng trung lập không nhận quy tắc tự động hạ gục này. Chỉ Mosin bật cờ; K54/AK/M16 giữ sát thương và cách bắn của mình.

Hàng đợi bắn chốt vũ khí, trạng thái ngắm và tư thế tại lúc bóp cò. Đổi sang khẩu khác trước lượt resolve cuối không biến viên Mosin đã ngắm thành phát bắn ngang hông. Tâm mảnh hơn trong ống ngắm giúp không che đầu địch ở xa; dòng thông số trong scope cho biết PU 3,5× và tầm bắn 200 m.

## Kiểm chứng

Đã chạy Unity và đạt 4/4 ca `MosinBalanceTests`: độ chính xác 200 m và giới hạn lethality; 8 phát đầu/thân ở 35/80/120/199 m với HP 80/160/2100; phát đã ngắm giữ đúng khi đổi sang K54; chỉ huy thật 700 HP một phát trúng thân; 201 m không trúng, tường đặc chặn và bảo vệ cinematic vẫn áp dụng. Ảnh `scope-120m.png` chụp scope PU thật trong Unity, kiểm tra tâm camera thực sự chạm collider địch ở 120 m.

Kiểm thử hồi quy tiếp tục trong đợt M16, sau khi hệ thống duyệt lệnh đã hoạt động lại: `../M16_C3_Balance_20261022/regression-tests.xml` đạt 29/29, gồm 4 ca Mosin trên, 3 ca nạp từng viên, 1 ca ba băng độc lập và 9 ca stealth/perception. Các ca kiểm tra trí nhớ khi địch còn sống dùng fixture Mosin cố ý giảm xuống 95 damage, không bật one-hit; các ca Mosin production xác nhận lethality mới và địch xa không tự tạo sight/contact. Tổng hợp tên từng test ở `../M16_C3_Balance_20261022/test-summary.json`. Các ảnh và log của đợt trước được giữ riêng.

Tầm phát hiện là điều kiện nhìn thấy Nam, không phải tầm scope của Nam. Trong Map 1 hiện tại, lính C3 chủ yếu nhìn cơ sở 14 m, tháp canh 30 m; còn xét hướng nhìn, tư thế và vật cản. Mosin phát tiếng gameplay 30 m: nghe được thì điều tra vị trí phát súng đã ghi; phát hiện xác có thể gây tìm kiếm/báo động mà không biết chính xác Nam đang ở đâu. Không biến Mosin thành súng giảm thanh hoặc xóa thông tin Nam đã bị nhìn thấy trước đó.

Chưa commit/push hoặc thay cơ sở dữ liệu từ xa. Nguồn thông số Map 1 đang chạy là `Assets/_Project/Map01/Generated/Map01Content.json`; prefab và builder Mosin được đồng bộ với nguồn này.
