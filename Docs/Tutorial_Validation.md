# Kiểm tra bản tutorial — cập nhật 16/10/2026

Unity Editor 6000.3.24f1. Scene thử: `Map00_Tutorial`. Các báo cáo XML được giữ cục bộ trong `Logs/TutorialValidation/`.

| Lượt kiểm tra | Kết quả | Báo cáo |
|---|---|---|
| UI bỏ Tiếp theo, camera giới thiệu ba bài, truy đuổi liên tục, thử lại và bắn/nạp | 5 qua, 0 lỗi | `tutorial-lesson-reveal-final.xml` |
| Vào cutscene với mesh không đọc được, tiếp xúc chân, bẫy, pause/huỷ và kết phim | 5 qua, 0 lỗi | `tutorial-nonreadable-capture.xml` |
| Sửa chân lún: đo mesh dép trên toàn cảnh, gót sát nền, hồi quy cảnh kết và ghi hình | 3 qua, 0 lỗi | `tutorial-foot-grounding-skinned.xml` |
| Vấp cành, dộng báng chéo, camera gục/mờ và hồi quy bắt giữ | 7 qua, 0 lỗi | `tutorial-trip-concussion-release.xml` |
| Mũi tên vàng theo mẫu: viền/lõi sáng/quầng mềm, không có đường nối | 2 qua, 0 lỗi | `tutorial-route-visual-release.xml` |
| UI theo ảnh mẫu: kính mờ/bo góc/phím viền vàng và tự chuyển bước | 6 qua, 0 lỗi | `tutorial-reference-ui-release.xml` |
| UI phím/chú thích, chevron dẫn đường và hồi quy pause/checkpoint/skip/cutscene | 6 qua, 0 lỗi | `tutorial-guidance-release.xml` |
| Cutscene mới: toàn bộ shot, rào, tiếp xúc, pause/cleanup và vào Map 1 | 4 qua, 0 lỗi | `tutorial-capture-release.xml` |
| Bản cuối: giữ cảnh gốc, đổi tuyến nhiệm vụ; 11 ca bị ảnh hưởng | 11 qua, 0 lỗi | `tutorial-original-route-final.xml` |
| Lượt đầu khôi phục cảnh, trước chỉnh vị trí xuất phát/lính | 21 qua, 2 lỗi (đã sửa và qua ở lượt cuối) | `tutorial-original-route.xml` |
| Bố cục cũ đã dời căn cứ — không còn dùng | 22 qua, 0 lỗi | `tutorial-straight.xml` |
| Bố cục cũ sau chỉnh nền — không còn dùng | 5 qua, 0 lỗi | `tutorial-straight-followup.xml` |
| Bộ tutorial sau khi sửa đích tới cầu gỗ/bến B | 21 qua, 0 lỗi | `tutorial-jetty.xml` |
| Bộ tutorial ở bố cục trước | 14 qua, 0 lỗi | `tutorial-final.xml` |
| Enter sau khi chết, sau bản sửa cuối | 1 qua, 0 lỗi | `tutorial-enter-retry.xml` |
| Menu và 4 ca tương thích Map 1 | 6 qua, 4 lỗi | `map01-recheck.xml` |
| Toàn bộ Map01 ban đầu, loại Replay | 122 qua, 11 lỗi, 1 bỏ qua / 134 ca | `map01-initial.xml` |

Lượt toàn bộ ban đầu có 1 lỗi trong fixture tutorial do biến closure bị mất khi đổi domain sang Play Mode; lỗi này đã được sửa và kiểm tra lại. Ca bị bỏ qua cần dịch vụ QIEA Python ở localhost:8011. Chưa chạy lại toàn bộ 134 ca sau các bản sửa cuối.

## Phần tutorial đã kiểm tra

- Avatar Hùng, AC_Player, WeaponAnchor dưới tay phải, đồ khởi đầu; không có quest/save của Map 1 trong tutorial.
- Pause/resume khôi phục slow motion. Chết rồi Enter thử lại giữ đúng bước và phục hồi máu, đạn dự trữ, băng đạn; thử lại nhiều lần không đăng ký trùng lính.
- Ném đá thật để lính nghe rồi hạ dao thật; hạ sai lính tập bằng dao có màn thử lại. Đạn súng thật chạm lính tập, và phải hoàn tất nạp đạn mới sang bài hồi máu.
- Dùng băng, tổ 3 lính được bind, giữ F để ghi nhận hàng tiếp tế có kiểm tra góc nhìn và vật cản.
- Đội tuần chỉ bật khi giẫm cành trên lượt về; xem hết cảnh kết hoặc bỏ qua từ pause đều vào Map 1 mới ở tốc độ thường.
- Menu chọn chơi hướng dẫn, scene có đúng một mục Build Settings, đường NavMesh từ đầu tuyến tới điểm trinh sát thông suốt.
- `git diff --check` sạch; asset mới có `.meta`; scene Map 1 và prefab Player gốc không thay đổi.

Các ca gameplay đưa nhân vật vào từng trạm bằng fixture. Cần chơi liên tục bằng tay để đánh giá nhịp học, tìm lối vượt rào và kiểm tra mép cảnh/camera. Cảnh kết đang dùng animation tạm; nền liền khối chưa được cắt thành mesh nhỏ.

## Sửa tuyến nhiệm vụ, giữ nguyên cảnh (16/10)

- Khôi phục căn cứ, địa hình và cảnh vật từ Map 1 trong scene tutorial; giữ cả cây/đá và cảnh bao quanh. Bỏ code tự san nền, xoá cây, dời căn cứ và sinh đường đất.
- Chỉ chuyển các mốc nhiệm vụ, lính tập, bụi ẩn nấp riêng, kiện hàng, bẫy và rào tutorial. Các bài 0–7 thẳng hàng từ gần căn cứ tới chân cầu; đoạn cuối theo cầu gỗ lên bến B, không đi ngược giữa các cụm.
- NavMesh hiện có đường hoàn chỉnh dài khoảng 145,38 m từ đầu tới điểm quét; giữ đúng vị trí căn cứ gốc (-65; 0,12; -63).
- Quét kiện hàng ngay trên sàn cầu. Lượt về chạy theo ramp gỗ hướng về căn cứ; cutscene mới cho Hùng va vào lính ở lối lên bờ rồi bị kéo trở lại cầu.
- Bản dự phòng trước sửa được lưu trong `Logs/TutorialRecovery/2026-10-16-before-route-fix/`.
- Bản cuối: **11/11 ca qua** trong `tutorial-original-route-final.xml`: so sánh toàn bộ transform cảnh và mesh nền với Map 1, tuyến nối thông/tiến dần, hàng và bắt giữ trên cầu, bị phát hiện rồi Enter thử lại, núp/ám sát, đi gây tiếng động rồi ẩn lại, bắn thật và nạp đạn, quét thật, chạy về căn cứ và ảnh xuất phát. Điểm đầu đã chuyển lên lối có sẵn để camera không bị cây che.
- Lượt đầu 23 ca có 21 qua, 2 lỗi: độ lệch đoạn ramp trong assert và vị trí lính tập khiến ca bị phát hiện không đạt. Cả hai đã qua sau chỉnh vị trí; 11 ca bị ảnh hưởng được chạy lại, chưa chạy lại toàn bộ 23 ca ở bản cuối. Các báo cáo 22 và 5 ca cũ chỉ kiểm tra bố cục đã bỏ, không đại diện cho cảnh hiện tại.

## Cutscene bắt giữ mới (16/10)

- `TutorialCaptureFilmTests`: clip Humanoid được đóng gói; chuỗi cảnh đi đủ thứ tự; Hùng ở bên phải gần camera, lính bên trái xa hơn; máu ở tay phải; tay trái ôm chỗ đau; hông hạ xuống khi ngã ngồi; góc nhìn ở đầu Hùng; đúng một cú báng súng rồi fade đen; hai lính nắm hai tay và kéo về chân cầu; kết vào Map 1 ở tốc độ thường.
- Kiểm tra các diễn viên không chạm/xuyên collider rào khi chạy các shot. Lượt chạy men theo ramp và lối đất gốc, không cắt ngang rào.
- Pause giữ nguyên pose, vị trí và thời gian shot. Huỷ từ góc nhìn Hùng khôi phục renderer, animator controller, collider, điều khiển, camera và HUD. Cleanup lúc unload không bật lại NavMeshAgent trên scene đã mất NavMesh.
- Ca cũ `ReturnPatrolWaitsForTheBranchAndThenCaptureStarts` cũng qua với chuỗi mới dài hơn.
- SHA-256 của `Map00_Tutorial.unity`, `Map 1.unity` và `Tutorial Root.prefab` trùng bản trước sửa cutscene. Không chỉnh lại tuyến, cảnh, cây/đá, địa hình, prefab nhân vật hay FBX nguồn. Tài nguyên mới chỉ là tham chiếu clip riêng cho phim.
- Ảnh các shot được lưu trong `Docs/Previews/TutorialCapture/`. Menu xem riêng: **ShadowVale → Tutorial → Xem thử cutscene bắt Hùng**.
- Các động tác mới dùng clip có sẵn cộng pose IK; chưa có motion capture, diễn xuất khuôn mặt hoặc thoại thu âm riêng. Chưa chạy lại toàn bộ bộ Map01 trong lần sửa cutscene này.

## UI và đường dẫn sáng (16/10)

- `TutorialGuidanceTests`: cụm WASD, nhãn Nhấn/Giữ, biểu tượng chuột phải có mesh thực sự được render; chú thích không tràn khung ở 1600×900 và 1280×720. Gợi ý không chặn chuột gameplay.
- Dải chevron có đường NavMesh hoàn chỉnh, hướng tới mục tiêu, bám mặt đi và sáng theo thời gian thực khi đang slow motion. Đã kiểm tra lối tới cầu và lượt quay về phía bờ, ẩn khi pause/thất bại/mở map/cutscene và phục hồi sau retry.
- Bốn ca hồi quy qua: pause/resume slow motion, chết rồi thử lại đúng bước, bỏ qua từ pause vào Map 1, pause/huỷ cảnh bắt giữ từ góc nhìn Hùng.
- SHA-256 của hai scene map và `Tutorial Root.prefab` trùng bản trước sửa UI. Không chạy tool dựng lại cảnh hoặc bake lại NavMesh. Bản dự phòng ở `Logs/TutorialRecovery/2026-10-16-before-ui/`; ảnh ở `Docs/Previews/TutorialGuidance/`.
- Báo cáo cuối `tutorial-guidance-release.xml`: **6/6 qua**. Các báo cáo `tutorial-guidance.xml` và `tutorial-guidance-final.xml` là lượt trung gian lỗi đã được sửa (khởi tạo native object và API lấy mesh của test); được thay thế bởi báo cáo release. Chưa chạy lại toàn bộ Map01 trong lần sửa UI.

## UI theo ảnh mẫu (16/10)

- Đổi bảng có sẵn sang nền kính mờ bo góc; cụm phím rỗng viền vàng, bóng chữ và tiêu đề vàng. Không thêm ngôi sao. Ô “Tiếp theo” báo trạng thái; giữ nguyên tự chuyển bước sau khi đạt kết quả gameplay.
- Shader UI lấy ảnh màu của camera để làm mờ trong khung, chỉ bật bản sao màu camera ở runtime tutorial và trả thiết lập khi huỷ UI. Không sửa URP asset, scene hoặc prefab bản đồ.
- Báo cáo cuối `tutorial-reference-ui-release.xml`: **6/6 qua**, gồm UI ở 1600×900 và 1280×720, shader và biểu tượng chuột được render, chevron/đảo hướng, pause, checkpoint, bắn/nạp đạn rồi tự sang hồi máu, pause/huỷ cutscene. Ảnh mới đã cập nhật trong `Docs/Previews/TutorialGuidance/`.
- Hash hai scene map và `Tutorial Root.prefab` trùng bản trước thay UI. Dự phòng mã trước lần này nằm trong `Logs/TutorialRecovery/2026-10-16-before-reference-ui/`.

## Mũi tên vàng theo mẫu (16/10)

- Mũi tên liền mạch có viền vàng đồng, lõi sáng dày và quầng mềm, đặt cách nhau 1,8 m. Các mũi tách riêng, không có đường nối mảnh; nhịp sáng vẫn chạy lần lượt theo hướng mục tiêu.
- Lưới hiển thị bám cao độ nền/dốc cầu; mesh được tái dùng và huỷ cùng tutorial, không có collider. Shader làm rõ hình và hiệu ứng, không đổi ánh sáng hay post-processing của scene.
- `tutorial-route-visual-release.xml`: **2/2 qua**, kiểm tra shader, bám mặt đi, hướng và nhịp sáng trong slow motion, lên cầu/quay về, ẩn khi pause/thất bại/map/cutscene và phục hồi sau retry, UI ở hai độ phân giải. Ảnh cuối ở `Docs/Previews/TutorialGuidance/08-golden-chevrons.png`.
- Hash hai scene map và `Tutorial Root.prefab` giữ nguyên. Không đổi mục tiêu, NavMesh hoặc cảnh vật. Mã trước sửa lưu ở `Logs/TutorialRecovery/2026-10-16-before-route-visual/`.

## Animation và kích thước Hùng (16/10)

- Hùng cũ cao bằng khoảng 75,5% Nam khi đo đầu–bàn chân trong cùng clip đứng. Đã tăng đồng đều Model khoảng 1,325 lần (scale 0,7945 → 1,0525), khớp đế chân; capsule 1,8 m cùng tâm/bán kính với Nam. Đo lại: cả hai 1,5623 m từ đầu tới bàn chân, đế chân ở −0,0241 m trong mẫu chuẩn. Số đo này không gồm mũ.
- Cutscene dùng `Standard Run` cho Hùng tay trống, `Standard Walk` cho lính kéo. IK giải cả mặt phẳng gập, giới hạn gập 140° và độ vươn 97,5%, bỏ góc cổ tay ép theo rig khác. Ngã ngồi lùi khỏi chân lính; tư thế kéo không còn cánh tay giơ theo clip cầm rifle.
- `HungPlayerGrip` chỉ trên PlayerHung: đặt rifle ở lòng bàn tay, đỡ bằng tay trái và theo hướng ngắm; không điều khiển tay khi reload, động tác tương tác, chết hoặc cutscene. Nam và các FBX nguồn giữ nguyên.
- `tutorial-animation-polish-release.xml`: **7/7 qua**, gồm cân chiều cao/đế chân, thao tác đi/chạy/khom/ngắm, góc nòng so với camera, toàn bộ chuỗi bắt giữ, giới hạn khuỷu tay, báng súng tới mặt, nắm tay khi kéo, pause/huỷ/khôi phục điều khiển, bắn thật+nạp đạn và núp+hạ dao. Chưa chạy lại toàn bộ Map01.
- Lượt `tutorial-animation-size-review.xml` có một lỗi khởi tạo scene trong fixture ảnh; đã sửa bằng tải tutorial rõ ràng sau EnterPlayMode. Các lượt final/release đều qua. Ảnh góc thoáng dùng sàn kiểm tra tạm trong Play Mode, không lưu sàn vào scene.
- SHA-256 hai scene map, `Tutorial Root.prefab` và Player Nam trùng bản trước sửa. Chỉ PlayerHung thay tỷ lệ/capsule và thêm phần chỉnh cầm súng. Dự phòng ở `Logs/TutorialRecovery/2026-10-16-before-animation-polish/`.
- `tutorial-animation-photo-review.xml`: **1/1 qua** sau đổi sàn chụp ảnh; ảnh góc thoáng đã xem lại tại `Docs/Previews/TutorialAnimationPolish/`, Hùng bên trái và Nam bên phải ở ảnh 09. Ảnh cutscene mới thay bản cũ trong `Docs/Previews/TutorialCapture/`.

## Báng súng vào camera và màn đen trước cảnh kéo (16/10)

- Sửa góc nhìn Hùng để đọc được toàn khẩu rifle: nâng ngang trước ngực, giữ nhịp rồi thúc báng vào giữa camera. Shot dài 2,5 giây; va chạm ở khoảng 1,85 giây, camera giật/lệch và tối dần sau đó. Đen hoàn toàn, giữ 1,1 giây rồi mới chuyển sang fade lên cảnh hai lính kéo Hùng.
- `tutorial-visible-stock-strike-release.xml`: **4/4 qua**. Kiểm tra báng và đầu nòng cùng ở trong khung hình khi lấy đà, chưa fade trước va chạm, báng tới mặt, fade sau đòn, ảnh render thực sự đen cả cảnh/phụ đề, giữ đen đủ lâu trước DragReveal, hai tay kéo khớp và hết cảnh vào Map 1; pause/huỷ vẫn khôi phục actor và điều khiển.
- Đã xem ảnh từng nhịp và quay đoạn POV → đen → kéo từ Unity. Ảnh động `Docs/Previews/TutorialCapture/13-stock-strike-blackout-drag.gif` dài khoảng 5,7 giây; các ảnh 10a/10b/10c/11 lần lượt là lấy đà, va chạm, mất ý thức và đen hẳn.
- Hai lượt trung gian `tutorial-visible-stock-strike.xml` và `tutorial-visible-stock-strike-final.xml` lỗi khởi tạo/tham chiếu scene trong fixture. Đã dùng tải async và chờ scene mới khởi tạo; báo cáo release thay thế các lượt đó.
- Hash hai scene map, Tutorial Root, PlayerHung và Player Nam trùng bản dự phòng `Logs/TutorialRecovery/2026-10-16-before-visible-stock-strike/`. Lần này chỉ chỉnh mã cutscene/test và tài liệu; không thay map, tuyến, prefab hoặc clip nguồn. Chưa chạy lại toàn bộ Map01.

## Sửa hai tay lính trong cú báng súng (16/10)

- Bản trước xoay nòng súng ngược sâu qua ngực khi đưa báng tới camera, khiến các điểm nắm và hai tay bị chìm trong thân. Đã đổi hướng kết thúc để nòng hướng lên, chỉnh tư thế nghiêng người và điểm lấy đà; cả hai tay nắm súng phía trước ngực trong suốt động tác. Giữ nguyên thời điểm va chạm, fade và màn đen trước cảnh kéo.
- `tutorial-stock-arms-release.xml`: **4/4 qua**. Thêm kiểm tra liên tục từng khung trong đoạn POV: cổ tay trước mặt phẳng ngực, khuỷu không ra sau thân, hai bàn tay ở trong khung hình và bám đúng hai điểm cầm súng, góc gập khuỷu hợp lệ. Các kiểm tra báng tới camera, màn đen render thật, kéo Hùng và pause/huỷ vẫn qua.
- Đã xem lại ảnh lấy đà, giữa cú vung và lúc va chạm; hai cánh tay hiện ở trước thân. Ảnh động mới `14-stock-strike-visible-arms.gif` thay bản xem `13-stock-strike-blackout-drag.gif`; bản 13 cũ chưa kiểm tra mặt phẳng thân người nên không phát hiện lỗi này.
- Hai scene map, Tutorial Root, PlayerHung và Player Nam giữ nguyên hash so với dự phòng `Logs/TutorialRecovery/2026-10-16-before-stock-arms-fix/`. Chỉ đổi tư thế riêng trong shot POV, kiểm tra và tài liệu; chưa chạy lại toàn bộ Map01.

## Vấp cành, dộng báng và góc nhìn mất ý thức (16/10)

- Cú vấp dài 2,05 giây: giữ mũi dép tại đầu cành cây hiện có, thân tiếp tục chúi về trước, chân kia bước đỡ rồi nhấc chân bị mắc ra. Âm thanh cành gãy chuyển sang nhịp tiếp xúc ở khoảng 0,34 giây. Cành nằm chéo gần dọc hướng chạy nên điểm chạm được chọn ở đầu cành, không đặt chân vào giữa thân cành.
- Lính nâng súng chếch lên một bên, giữ nhịp rồi dộng báng xuống với tốc độ tăng dần trong 0,3 giây cuối; va chạm ở khoảng giây 1,85 của shot POV dài 3,35 giây. Hai tay vẫn cầm phía trước ngực. Đầu Hùng gục sang bên; camera giật ngắn rồi hạ xuống, nghiêng và xoay sang bên trong khoảng 0,85 giây.
- Mất nét tăng trước fade bằng Depth of Field, kèm Motion Blur khi giật và Vignette; dùng bản sao Volume Profile riêng, khôi phục các cờ/mask camera khi hết POV hoặc huỷ. Fade bắt đầu sau cú đánh 0,6 giây, đen hoàn toàn rồi giữ 1,1 giây trước khi hiện cảnh kéo đi. Profile riêng được tham chiếu từ Resources để đưa các hiệu ứng vào tài nguyên build; chưa kiểm tra bản build độc lập.
- `tutorial-trip-concussion-release.xml`: **7/7 qua**. Kiểm tra chân bị giữ rồi bước đỡ, vertex dép không nằm trong mesh cành; báng/nòng trong khung khi lấy đà, hai tay bám súng và ở trước thân, tiếp xúc báng tới mặt; camera thực sự đổi góc sau đòn, pause giữ nguyên góc/độ mờ; ảnh render mất chi tiết so với cùng góc tắt Volume; khôi phục post-processing/mask/depth khi huỷ; màn đen thực tế, nắm tay kéo và kết vào Map 1. Hồi quy đội tuần chờ giẫm cành cũng qua.
- Đã xem lại từng nhịp chân và cú đánh. Ảnh động mới từ Unity: `15-stumble-foot-contact.gif` (2,38 giây) và `16-diagonal-strike-concussion.gif` (6,54 giây) trong `Docs/Previews/TutorialCapture/`. Ảnh tĩnh 10a–10e cho thấy lấy đà, va chạm, rung, đầu gục/mờ và fade; ảnh 15 có bản bật/tắt mất nét ở cùng camera.
- Hash hai scene map, Tutorial Root, PlayerHung và Player Nam trùng dự phòng `Logs/TutorialRecovery/2026-10-16-before-stumble-concussion/`. Không dựng lại cảnh hoặc bake NavMesh. Chưa chạy lại toàn bộ Map01 trong lần này.

## Chân Hùng lún nền khi ngã và bị kéo (16/10)

- Tái hiện bằng mesh dép đã skin so với collider sàn/đất thật: bản cũ lún tối đa khoảng 32,5 cm trong nhịp ngã ngồi, khoảng 13 cm trong nhịp kéo. Các ca trước chỉ kiểm tra hông, xương chân hoặc cành cây nên chưa phát hiện việc đế dép xuyên nền.
- Thêm phần tiếp xúc chân riêng cho cutscene Hùng. Đo các điểm đỡ từ mesh dép ở tư thế chuẩn một lần; sau mỗi pose, lấy cao độ nền tại gót/mũi/mép đế và giải lại chân khi cần. Tư thế ngồi/kéo dùng cổ chân thả lỏng, mũi ngửa nhẹ, gót theo nền; chân đang vung khi chạy vẫn được giữ chuyển động.
- Góc cổ chân dùng pháp tuyến của mặt sàn từ raycast. Không suy góc dốc từ chênh cao hai điểm nằm hai bên mép bậc; cách đó có thể làm nghiêng chân quá mức lúc tới cầu.
- Các điểm đỡ theo đủ trọng số xương của vertex dép, gồm cả ảnh hưởng từ cẳng chân; không coi dép là một khối cứng gắn riêng vào bàn chân. Chỉ lấy mặt sàn vật lý bên dưới từng điểm, bỏ điểm không có sàn thay vì lấy cao độ NavMesh lân cận ở mép cầu.
- Bản cuối `tutorial-foot-grounding-skinned.xml`: **3/3 qua** — mesh dép không xuyên nền khi lấy mẫu suốt cảnh, gót kéo không treo quá 3 cm, toàn bộ chuỗi camera/tiếp xúc/màn đen/kéo/kết vào Map 1 và ghi hình liên tục. Số đo cuối: khoảng hở nhỏ nhất 0,0016 m; gót trong đoạn kéo hở tối đa 0,0098 m. Chưa chạy lại toàn bộ Map01.
- Lượt `tutorial-foot-grounding-release.xml` có 7/7 qua ở kiểm tra không xuyên nền, nhưng chưa kiểm tra gót treo. Hai lượt final/settled sau khi bổ sung điều kiện này còn 1 lỗi ở mép cầu (gót hở khoảng 10 cm); báo cáo skinned là bản thay thế sau sửa điểm đỡ. Báo cáo before cố ý tái hiện lỗi lún của bản cũ.
- Ảnh mới: `Docs/Previews/TutorialCapture/18-drag-feet-grounded-side.png`; ảnh động `17-grounded-strike-and-drag.gif` thay bản xem 16 có chân bị lún. Số đo CSV cuối ở `Temp/TutorialFootContact/clearance.csv`.
- Dự phòng mã, số đo và ảnh trước sửa ở `Logs/TutorialRecovery/2026-10-16-before-foot-grounding/`. Hai scene map, Tutorial Root và hai prefab nhân vật giữ nguyên hash; chỉ đổi pose/contact của cutscene và phần kiểm tra/tài liệu.

## Lỗi vào cutscene khi mesh Hùng không đọc được (16/10)

- Người dùng phát hiện `Mesh_0.001.isReadable == false`: phần căn chân đọc `sharedMesh.vertices` ngay sau khi khoá điều khiển, sau đó truy cập mảng đỉnh rỗng gây `IndexOutOfRangeException`. Các lượt Editor trước chưa chủ động bỏ dữ liệu CPU của mesh nên chưa bao phủ điều kiện này.
- Chuyển vị trí đỉnh ở không gian bind và trọng số xương sang dữ liệu chuẩn bị trong Edit Mode, lưu tại `TutorialCapture.asset`. Runtime chỉ ánh xạ renderer/xương và tính các điểm đỡ; không đọc buffer mesh nguồn hoặc cần bật Read/Write. Kiểm tra độ dài mảng và chỉ số xương trước khi dùng; có điểm đỡ dự phòng khi cấu hình thiếu.
- `tutorial-nonreadable-capture.xml`: **5/5 qua**. Bài đo chân tạo bản sao mesh rồi gọi `UploadMeshData(true)`, xác nhận `isReadable == false` trước khi bắt đầu phim, chạy hết chuỗi tới Map 1 và tiếp tục đo mesh đã skin. Khoảng hở nhỏ nhất 0,0018 m; gót kéo hở tối đa 0,0098 m. Các ca tài nguyên đóng gói, luồng bẫy, chuỗi camera/tiếp xúc và pause/huỷ cũng qua. Chưa chạy lại toàn bộ Map01 hoặc build độc lập.
- Kiểm tra thêm Play Mode thường qua menu xem cutscene, ngoài Test Runner: tới `VictimView` với mesh nhập `Mesh_0.001.isReadable == false`; đoạn log mới không có lỗi truy cập vertex hoặc IndexOutOfRange. Trạng thái lưu ở `Logs/TutorialValidation/nonreadable-normal-play.json`.
- Phân biệt với log Telemetry: backend localhost:5000 không trả lời thì uploader giữ các session trong hàng đợi, chạy nền và không gây exception làm dừng cutscene. Không xoá hàng đợi để xử lý lỗi này.
- Nguồn API: [Unity — truy cập mesh trong Editor và điều kiện isReadable](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MeshUtility.AcquireReadOnlyMeshData.html). Dự phòng trước sửa ở `Logs/TutorialRecovery/2026-10-16-before-nonreadable-mesh-fix/`.

## Bỏ ô Tiếp theo, giới thiệu địch và sửa nhịp truy đuổi (16/10)

- Bỏ hẳn ô/nhãn `TIẾP THEO` do runtime tạo; hoàn thành chỉ báo `HOÀN THÀNH` rồi tự chuyển bài. Không thay khung, cụm phím hoặc cách chuyển bước.
- Bài ám sát, bắn một lính và hạ tổ lính có nhịp camera 0,9 giây tới mục tiêu → giữ 1 giây → 0,9 giây trở về. Chọn góc nhìn có đường ngắm thoáng cả collider lẫn lá/cỏ (dùng dữ liệu SightCover); bảng phím chỉ hiện sau khi camera trả về. Quyền camera được blend với camera chơi đang chạy để tránh giật khi trả về. Hùng/AI chờ; pause dừng nhịp, retry giới thiệu lại, fail/đổi bài/huỷ trả quyền camera và input.
- Lính truy đuổi trước đây đổi toạ độ đột ngột ở nhịp vào chạy, dừng bắn và chạy lại. Đã nối vị trí giữa các nhịp, tăng dần khoảng đuổi sau phát bắn; hướng/độ lệch của lính theo chính vị trí của lính khi qua chỗ rẽ. Skin tiếp tục cập nhật ngoài khung hình và camera phim tạm tắt occlusion culling, có khôi phục khi huỷ.
- Sau khi xem video, góc máy vòng được đổi sang quay quanh trung điểm Hùng/lính, giữ đủ khoảng cách để lính không ra khỏi mép hình. `tutorial-chase-framing-final.xml`: **1/1 qua** sau chỉnh góc này, thêm kiểm tra cả Hùng và lính nằm trong khung ở các mẫu của ChaseWide và ChaseFront. Các ảnh động được xuất lại từ lượt này.
- `tutorial-lesson-reveal-final.xml`: **5/5 qua**. Kiểm tra đủ mục tiêu trong khung ở cả ba bài, không bị lá che, camera về góc chơi trước UI; pause/retry/fail/đổi bài không kẹt; lính truy đuổi không bật/tắt renderer hoặc nhảy vị trí qua các shot; vị trí địch liên tục theo thời gian lấy mẫu, và các cờ render/camera được khôi phục. Hai ca gameplay tutorial bắn thật/nạp đạn và chết/Enter thử lại vẫn qua.
- `KeycapsMouseAndNotesStayReadableAtCommonResolutions` qua trong lượt đầu, gồm UI 1600×900 và 1280×720. Các lỗi đầu ở ba fixture mới là cách EnterPlayMode trong coroutine lồng; đã sửa. Lượt review phát hiện fixture đo tốc độ ở mỗi hai khung nhưng chia thời gian một khung và camera trả về chưa ổn định; bản cuối sửa mẫu đo, quyền camera và góc tránh cây.
- Ảnh ba góc và UI ở `Docs/Previews/TutorialPresentation/`; `03-continuous-pursuit.gif` ghi đoạn phát hiện → rượt → dừng bắn → chạy lại. Các ca lần này chỉ chạy trong `Map00_Tutorial`; ca ghi phim dừng và huỷ trước đoạn chuyển Map 1, không chạy bộ test Map 1.
- Hash hai scene map, Tutorial Root, hai prefab nhân vật và TutorialCapture.asset giữ nguyên so với dự phòng `Logs/TutorialRecovery/2026-10-16-before-lesson-reveal/`.

## Lính truy đuổi tụt khỏi sàn cầu rồi bật lên (16/10)

- Người dùng phát hiện đường chạy phía sau Hùng cắt chéo ngoài mép sàn chờ trước khi gặp cầu dẫn. Raycast lấy đúng nền dưới sàn nên root lính tụt từ khoảng 1,485 m xuống −1,312 m, rồi lên lại khi chạm cầu. Kiểm tra liên tục trước đó chỉ đo phương ngang nên bỏ sót thay đổi độ cao này.
- `tutorial-pursuer-deck-before.xml` tái hiện lỗi tại ChaseWide giây 0,667: tốc độ 3D vọt tới 33,674 m/s. CSV trước sửa lưu ở `Logs/TutorialRecovery/2026-10-16-before-pursuer-deck-fix/`.
- Riêng đoạn tiếp cận của lính phía sau cành cây lấy đường NavMesh có sẵn nối sàn chờ với cầu dẫn, di chuyển theo chiều dài từng đoạn và xoay theo tiếp tuyến. Sau cành cây tiếp tục đường đuổi hiện tại. Không sửa đường Hùng, tuyến tutorial, collider, hình học map hoặc bake lại NavMesh.
- `tutorial-pursuer-deck-final.xml`: **1/1 qua**. Ghi và đo đến hết đoạn chạy/va chạm, kiểm tra chuyển động 3D và cao độ trên sàn chờ/cầu dẫn, giữ cả hai nhân vật trong khung và khôi phục renderer/camera khi huỷ. Tốc độ 3D lớn nhất 2,694 m/s; root lính trên đoạn sàn/cầu dẫn nằm trong 1,485–1,488 m. Chỉ chạy tutorial và huỷ trước chuyển Map 1.
- Đã xuất lại `Docs/Previews/TutorialPresentation/03-continuous-pursuit.gif` dài 9,5 giây; số đo ở `Temp/TutorialChaseReview/positions.csv`. Hash hai scene, Tutorial Root, hai prefab nhân vật và TutorialCapture.asset giữ nguyên so với bản dự phòng.

## Các ca Map 1 còn lỗi

Cả 6 ca `ForestMenuIntegrationTests` đã qua. Bốn ca dưới đây vẫn lỗi khi chạy riêng trong nhóm kiểm tra lại:

| Ca | Hiện tượng |
|---|---|
| `EndingCutsceneTests.DefeatUsesExistingDeathThenRadioAndExtraction` | Đối tượng vẫn active ở assert cuối, dòng 59. |
| `HungVisualTests.ModelAnimatesAndRescueNavigationSurvives` | Giữ tham chiếu mission đã bị huỷ sau đổi scene, dòng 129. |
| `RescueCompatibilityTests.KnifeRejectsRangeFrontAndPhysicalObstructionThenKillsOverseerLast` | Assert vật cản dao nhận `true` thay vì `false`, dòng 51. |
| `ScoutingTests.AQuietKnifeTakedownAwayFromTheCampGoesUnnoticed` | Lính chưa chết ở assert sau hạ dao, dòng 294. |

Lượt toàn bộ ban đầu còn 5 lỗi `ForestFlowTests` (luồng cứu Hùng, theo về căn cứ, tương tác, tên mục tiêu, tiếp tế) và 1 lỗi `ForestHudTests.HudItemUseAndSaveRestoreUseRealInventory` bị chặn lưu. Chưa chạy lại 6 ca này. Một số assert vẫn theo luồng dùng herb cứu Hùng hoặc mục tiêu “THÙNG VẬT TƯ”, trong khi mã tại HEAD ban đầu đã dùng luồng cởi trói ở bến tàu.

Chưa chạy các ca lỗi trên một checkout baseline sạch, nên chưa kết luận toàn bộ nguyên nhân hoặc loại trừ mọi hồi quy Map 1. Kết quả hiện tại đủ để thử prototype tutorial; tiêu chí toàn bộ Map01 qua trong kế hoạch vẫn chưa đạt.
