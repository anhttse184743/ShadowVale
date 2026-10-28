# M16 — cân bằng ngày 22/10/2026

Đã sửa thông số game. Phương án bố trí lại C3 ở `PHUONG_AN_C3.md` là đề xuất riêng, chưa áp dụng.

## Vấn đề được xác minh

M16 Nam cầm dùng `Map01RifleVisual` để lấy damage AK × 0,9. AK prefab thực là 30 damage, 120 m, 600 RPM, nên M16 thực là **27 damage, 120 m, 700 RPM**. JSON lại ghi 21,6 damage / 40 m. M16 dùng chung spread và recoil với AK. Cần sửa đường chạy thực cùng prefab/builder/HUD và dữ liệu, không chỉ sửa JSON.

## Tham khảo thiết kế

- [PUBG Update 22.2, 8/3/2023](https://pubg.com/en-asia/news/4555?category=patch_notes): M16A4 single/burst được tăng 600 → 800 RPM; nhà phát triển nêu rõ không có full auto. Cùng bản cập nhật, AUG tăng tốc độ bắn đồng thời tăng recoil và đổi damage/range. Bài học: tốc độ, sát thương và khả năng kiểm soát phải được cân bằng cùng nhau; RPM trong burst không phải RPM xả liên tục.
- [MWIII Season 1, phần cập nhật 6/12/2023](https://www.callofduty.com/patchnotes/2023/12/call-of-duty-modern-warfare-iii-season-1-patch-notes): M16 (MWII) giảm hệ số thân trên/tay/bàn tay 1,2 → 1, đồng thời giảm ADS 290 → 240 ms và sprint-to-fire 210 → 189 ms. Ví dụ cân bằng giảm sức mạnh gây sát thương, giữ phản hồi thao tác. Không áp hệ số này vào game nếu chưa có hệ hit zone tương ứng.

Đây là mốc lịch sử, không phải meta hiện tại. ShadowVale dùng HP, quy mô map và điều khiển riêng; các thông số dưới là lựa chọn thiết kế của dự án.

## Thông số trước / sau trong game

| Thuộc tính | Trước thực tế | Sau |
|---|---:|---:|
| Damage mỗi viên | 27 | 22 |
| Tốc độ | 700 RPM | 600 RPM / 0,1 s mỗi viên |
| DPS lý tưởng, không nạp | 315 | 220, giảm 30,16% |
| Tầm ray tối đa | 120 m | 80 m |
| Băng / nạp / loại đạn | 30 / 2,4 s / 5,56 | Giữ nguyên |
| Lính 100 HP | 4 phát trúng, 0,257 s lý tưởng | 5 phát trúng, 0,4 s lý tưởng |
| Đứng ngắm, nửa góc cone trước bloom | 0,10° | 0,12° |
| Khom ngắm | 0,06° | 0,075° |
| Đi ngắm | 0,35° | 0,60° |
| Đứng bắn ngang hông | 0,40° | 0,65° |
| Bắt đầu bloom | Viên thứ 4 | Viên thứ 3 |
| Tích bloom / hồi mỗi giây | 0,085 / 0,9 | 0,14 / 0,65 |
| Độ mở cộng tại bloom đầy | 0,9° | 1,35° |
| Nghỉ để bắt đầu nhịp mới | >0,25 s | >0,32 s |
| Giật dọc / ngang | Chung AK | ×1,35 / ×1,4; giữ mẫu có thể học và hệ số kỹ năng |
| Tiếng súng gameplay | 18 m | 18 m |

Không khóa cò thành burst mới; vẫn giữ cơ chế tự động đang có và cho người chơi tự bắn nhịp ngắn. Tăng ngẫu nhiên có giới hạn khi xả lâu, không làm mỗi phát ngắm đứng yên trượt ngẫu nhiên quá lớn. Ở 80 m, cone 0,12° tương đương lệch tối đa khoảng 16,8 cm khi chưa bloom; không phải tỉ lệ trúng đảm bảo.

`Map01M16` giữ damage/range/cadence rõ ràng. Cờ `Weapon.UsesM16Handling` theo súng thực, không chỉ theo `WeaponKind.Rifle` vì AK dùng cùng slot. Đổi về AK khôi phục cả damage/cooldown/range và handling; clone/save vẫn giữ chuẩn này. Generic configure xóa cờ M16. Sniper ưu tiên profile riêng dù nhận nhầm cờ M16.

Lính dùng M16 nhận nhịp 600 RPM và hệ số damage tương đối AK **22/30**, thay 0,9; vẫn qua hệ số độ khó, xác suất trúng, vật cản và HP của Nam/Hùng hiện có. Không gán damage 22 trực tiếp lên Nam để vượt qua hệ số AI. Các profile hồi đạn, loại đạn, cơ chế save và hình súng giữ nguyên.

## Kiểm thử

**51 test riêng biệt đạt ở kết quả mới nhất, 0 lỗi còn lại.** Chi tiết từng tên trong `test-summary.json`. Lượt cuối `regression-tests.xml` đạt 29/29; 22 test `WeaponSpreadStateTests` đã đạt ở `balance-tests.xml` và code handling không đổi sau đó. Bao gồm M16 đổi model/caliber, AK khôi phục đúng range, clone, nạp đạn, save Map 1/2, Mosin một phát/200 m/vật cản, reload từng viên và 9 ca perception/stealth. Unity lượt cuối exit 0.

Lượt thử đầu có lỗi fixture test do closure qua domain reload; đã chuyển phần fixture vào helper sau EnterPlayMode và ca bắn thật đã chạy lại đạt. Bằng chứng ghi ở `m16-live-hit-evidence.txt`: mục tiêu Map 1 đặt 80 HP nhận 22 mỗi phát, 4 phát trúng hạ gục; không chạm mục tiêu 81 m; tường đặc chặn; 6 phát gồm 2 phát không trúng tiêu tốn đúng 6 viên. Việc đã kiểm thử tự động chưa thay thế playtest cảm giác súng với người chơi.

Kiểm tra tĩnh `build_design_audit.py` xác nhận JSON chỉ thay 3 trường damage/fire_rate/range của M16 so với bản sao trước lượt này. Mosin 200 m và toàn bộ enemy archetypes giữ nguyên trong JSON. Chưa commit/push hay cập nhật dữ liệu từ xa.
