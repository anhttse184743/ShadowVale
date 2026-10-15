# Bộ kỹ năng nhân vật — ShadowVale

## Phạm vi đã triển khai (15/10/2026)

- Shooting, Stealth, Engineering: cấp nền 0, tối đa 4 nấc, tự lên cấp theo XP.
- Tab Kho đồ / Kĩ Năng trong balo Map 1. Ô đã đạt xanh, ô đang luyện tô theo XP, ô chưa mở trống; có số XP tới cấp tiếp theo và trạng thái TỐI ĐA.
- Hover/focus từng nấc mở popout trước → sau; click ghim. Popout có cả ở ô xanh, ghi Đã đạt hoặc Xem trước / Chưa mở, tự đặt trong panel. Dùng Tab/Esc đóng balo như trước.
- XP được lưu trong checkpoint và save slot Map 1, khôi phục cùng retry. Save cũ thiếu skill trở về cấp nền. Capture sở hữu bản sao, restore không trao XP hay phát thông báo lên cấp.
- PlayerSkills gắn trên player nên được giữ khi player chuyển sang Map 2 theo luồng hiện có. Không tạo thêm hệ thống balo/save Map 2.
- Chưa nối database. Không thay schema backend hoặc chỉ số nền.
- Không bổ sung crafting/repairing. Engineering có XP/state và API hệ số để nối sau, chưa có nguồn XP gameplay và chưa tác động vật phẩm.

## Cấu hình điều kiện và bonus đã chốt

Nguồn duy nhất: `Assets/Resources/Skills/SkillBalance.json`. Theo bản điều kiện và xác nhận của người dùng: mọi mốc hành động là số cần làm thêm sau cấp trước. Dòng Shooting L2 thứ hai được hiểu là L3. Shooting/Stealth nhân tiếp trên kết quả nấc trước; Engineering cộng điểm phần trăm độ bền tối đa.

- Shooting: cần thêm 2/5/10/20 gun kill; mốc tổng 0/2/7/17/37. Recoil từng nấc ×0.98/0.985/0.985/0.975; thời gian thay đạn ×0.99/0.985/0.98/0.975. Hệ số so với nền lưu trong config là tích của các nấc tới cấp tương ứng, không ghi đè chỉ số nền.
- Stealth: cần thêm 1/2/4/10 ám sát; mốc tổng 0/1/3/7/17. Range từng nấc ×1.05/1.04/1.05/1.05; hệ số so với nền 1/1.05/1.092/1.1466/1.20393.
- Engineering: cần thêm 2/4/6/8 lần sửa đầy; mốc tổng 0/2/6/12/20. Bonus cộng trên độ bền tối đa: 0/+20/+35/+50/+60 điểm phần trăm. Với lượng sửa nền 20%: 20/40/55/70/80%. Chưa có nguồn XP thực tế vì chưa triển khai repairing.

Mảng chứa cấp 0 và từng cấp nâng cấp. Hỗ trợ 1–4 nấc bằng độ dài mảng, threshold phải tăng chặt, hệ số hợp lệ và nhất quán với chiều cải thiện. Shooting/Stealth nhận 1 XP mỗi hành động thành công. Engineering có hook RecordFullRepair, chỉ nhận giao dịch tiêu hao nguyên liệu hợp lệ, chuyển vũ khí từ chưa đầy lên đúng 100%; hệ thống repairing tương lai phải gọi đúng một lần sau giao dịch thành công.

Popout mỗi nấc chỉ có hai dòng điều kiện: `Điều kiện: ...` và `Tiến độ: ...`; không hiển thị tổng cần đạt hoặc dòng sau cấp trước. Shooting hiển thị phần trăm riêng của nấc đang xem: `Độ giật — Giảm ...%`, `Tốc độ thay đạn — Tăng ...%`. Logic dùng thời gian thay đạn duy nhất như đã chốt. Stealth hiển thị `Tầm ám sát — Tăng ...%`, Engineering hiển thị bonus cộng ngoài lượng sửa nền.

Ba popout đã được render bằng bản sao đúng mã partial HUD và asset gốc trong project Unity preview tạm, với dữ liệu fixture theo cấu hình hiện hành; không phải ảnh chụp world Map 1. Ảnh: `Logs/SkillPreview/popout-shooting.png`, `popout-stealth.png`, `popout-engineering.png`. Kiểm tra render Passed: `Logs/skill-popout-render-tests.xml`. Công cụ preview tạm đã được dọn.

## Áp dụng vào chỉ số thật

### Shooting

`WeaponSpreadState` tạo pattern recoil dọc từ 0.33° tới 1.00°, ngang từ 0 tới 0.34° theo viên trong loạt bắn. `PlayerCombat.FireGun` nhân kick dọc/ngang với hệ số skill trước khi truyền cho camera.

Cấp 1: viên đầu 0.33° → 0.3234°, giật ngang đỉnh 0.34° → 0.3332°. Cấp 2 nếu nền recoil giả định 12: 12×0.98×0.985 = 11.5836, đúng cách nhân tiếp đã xác nhận. Popout lấy pattern thực tế, không dùng nền 12 giả định.

Theo yêu cầu cập nhật, chỉ có một thời gian thay đạn nền 2.4s, không phân biệt còn đạn hay hết đạn. Cấp 1 thành 2.376s trong mọi trường hợp. Đã bỏ emptyReloadSeconds khỏi WeaponTuning, Magazine và prefab người chơi. Chốt hệ số tại lúc bắt đầu reload; không đổi thời gian giữa chừng, không cộng dồn lại khi reload lần tiếp theo. Các clip animation hiện có đều được chỉnh tốc độ theo cùng timer. Giữ nguyên spread theo tư thế, bloom, sát thương, tốc độ bắn và hồi giật.

XP chỉ đến từ gun trace của người chơi làm Health của địch chuyển sống → chết. Không tính melee, hit chưa chết, xác chết, damage kịch bản, cinematic hoặc load/restore. Map adapter lọc đúng Map01EnemyController.

### Stealth

Chỉ nhân range nền của lính thường: nền 1.6m, cấp 1 là 1.68m, cấp 4 là 1.926288m. Overseer không nhận bonus Stealth; tầm riêng 2m trong pha captive và các điều kiện encounter giữ nguyên. Popout chỉ hiển thị `Tầm ám sát — Tăng ...%` của từng nấc, bỏ số mét và mục giám sát giải cứu. Chú thích chính xác: `Dùng vũ khí cận chiến để tiếp cận địch từ phía sau lưng`. Chỉ đổi chú thích, điều kiện gameplay vẫn dùng dao như thiết kế hiện tại.

Giữ các điều kiện dao, địch sống, không boss, chưa giao chiến, tiếp cận phía sau, khoảng cách 3D và vật cản. Cơ chế hint hiện có tìm trong 3.2m, bao phủ cả range tối đa của cấu hình này. Animation tiếp cận/IK giữ nguyên.

Chỉ trao XP sau TrySilentTakedown thành công và địch chết; thao tác thất bại hoặc gọi lại trên xác không nhận XP. Không cộng Shooting cho ám sát.

### Engineering

Chỉ cung cấp RepairBonusAt/RepairGain và hook RecordFullRepair; không dựng chức năng sửa chữa khi chưa có repairing. Khi xây repairing, hệ thống đó tính:

`repairGain = min(maxDurability, baseRepairGain + maxDurability * repairBonusFraction)`

`newDurability = min(maxDurability, currentDurability + repairGain)`

Sau khi tiêu hao nguyên liệu và sửa từ chưa đầy tới 100% mới gọi RecordFullRepair để nhận XP. Sửa một phần hoặc sửa vũ khí đã đầy không tính. Hook chưa được nối với chức năng thực tế. Crafting cần thiết kế bonus riêng trước khi nối, không mặc định thêm bonus crafting.

## Dữ liệu và nối database sau này

`Nguồn nền → bộ tính skill → gameplay và HUD`

- Nền hiện lấy từ hardcode/pattern và rescue layout; không sửa trực tiếp asset, DTO content hoặc database.
- Save chỉ chứa XP, cấp suy ra từ threshold; không ghi chỉ số hiệu lực trở lại nền.
- `PlayerSkills.Configure(SkillBalance)` nhận cấu hình từ adapter content tương lai, kiểm tra trước khi thay và sao chép cấu hình để tránh sửa nguồn. XP được giữ khi thay cấu hình.
- Thay threshold có thể đổi cấp suy ra; khi phát hành content mới phải quyết định migration/version phù hợp.
- Backend đã có Skill.MaxLevel/XpCurve/Effects, nhưng cần thống nhất schema effects và ngưỡng XP tích lũy. MaxLevel mặc định 10 chưa đồng nghĩa dữ liệu thật đang dùng 10.
- Backend Weapon chưa có recoil pattern; không dùng một số recoil đơn lẻ thay pattern hiện tại. Tách tầm ám sát khỏi EffectiveRange của vũ khí. Chỉ ánh xạ một trường thời gian thay đạn nền từ backend.
- Engineering sẽ đọc lượng phục hồi nền từ cấu hình sửa chữa tương lai. Không dùng MaxDurability hay DurabilityPerUse làm lượng sửa.

## Các file chính

- `Assets/_Project/Scripts/Gameplay/Skills/PlayerSkills.cs`: balance input, XP, cấp, snapshot và parameter resolver.
- `Assets/Resources/Skills/SkillBalance.json`: cân bằng tạm có thể thay sau.
- `PlayerCombat.cs`: nhân recoil và phát sự kiện lethal gun hit.
- `Map01Mission.cs`: tạo state, nối XP và thông báo lên cấp; hủy subscription khi kết thúc mission.
- `Map01EnemyController.cs`: range và XP ám sát.
- `Map01SaveSystem.cs`: Capture/Restore XP, giữ tương thích checkpoint cũ.
- `Map01Hud.Skills.cs`: tab, nấc, XP và popout dùng chỉ số thật.

## Xác minh

Sau cập nhật điều kiện đã chốt: 24 unit test skill/magazine chạy lại từ bản sao đúng các file nguồn mới trong project Unity kiểm thử tạm đều Passed (`Logs/skill-balance-isolated-tests.xml`). Ba kiểm thử tích hợp trên project thật cũng Passed: gun trace/checkpoint/reload/HUD, range ám sát/tín hiệu/vật cản, và animation băng đạn (`Logs/skill-balance-main-tests.xml`). Báo cáo main còn ghi một unit test dùng dữ liệu mẫu cũ; test đó đã sửa và có kết quả Passed trong báo cáo isolated. Tổng 27 test liên quan có kết quả cuối Passed.

Test tích hợp của lần trước xác nhận timer áp dụng skill và tín hiệu sẵn sàng ám sát khớp range. Sau khi gộp thời gian reload, đã cập nhật kỳ vọng tích hợp thành 2.376s cho cả hai trạng thái và chạy lại 17 unit test Magazine trong Unity: tất cả Passed (`Logs/unified-reload-tests.xml`). Bộ test kiểm tra cùng thời gian khi hết đạn/còn đạn, bonus skill, cancel, reserve và hoàn tất reload.

Các kiểm tra được duy trì từ lần triển khai đầu:

- Ngưỡng, nhiều cấp, cap và integer overflow.
- Tính parameter từ nền, không cộng dồn lên giá trị đã sửa.
- JSON, rollback, dữ liệu cũ/âm và restore không thông báo lên cấp.
- Thay cấu hình không sửa dữ liệu nguồn hoặc mất XP; từ chối cấu hình sai.
- Production checkpoint Restore giữ XP; gun trace thật trao XP một lần, không tính xác hoặc damage kịch bản.
- Range nâng cấp mở được tiếp cận hợp lệ, vẫn bị vật cản chặn; takedown thành công chỉ trao XP một lần và không cộng Shooting.
- HUD render thực tế ở 1600×900 và 1280×720; đã xem ảnh popout ô xanh, chữ và các nấc không tràn panel.

Kiểm tra hồi quy bổ sung: 19 test WeaponSpreadState, 1 test tiếp cận/ám sát cũ và 1 test balo dùng vật phẩm/save/load đều đã qua. Test balo được sửa để đợi animation ném đá kết thúc trước khi pause/lưu, đúng quy tắc NonSaveable hiện có. Tổng cộng 28 test liên quan có kết quả cuối cùng Passed.

Kết quả tính năng: `Logs/skill-feature-tests.xml`; hồi quy recoil/takedown: `Logs/skill-regression-tests.xml`; balo sau khi sửa điều kiện chờ animation: `Logs/skill-hud-regression-tests.xml`. Báo cáo hồi quy đầu còn ghi lần fail cũ của balo; kết quả cuối của test đó nằm ở báo cáo balo. Ảnh: `Logs/SkillPreview/`.
