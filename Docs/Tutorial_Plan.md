# Kế hoạch: Tutorial Hùng (`Map00_Tutorial`)

Trạng thái: **đã có bản chơi thử trong Unity**. Viết ngày 15/10/2026, dựa trên nhánh `dev2` (beac555) và bản phác của nhóm. Cách mở và giới hạn của bản thử được ghi trong [Tutorial.md](Tutorial.md).

Kết quả 22 ca tutorial trước đây thuộc bố cục đã dời căn cứ, không chứng minh lần sửa tuyến hiện tại. Bản cuối đã qua **11/11 ca liên quan tới tuyến và các bài bị đổi vị trí**, gồm bảo toàn cảnh gốc, đường đi, ẩn nấp, bắn tập, quét và bắt giữ. Lượt trước có 6 ca menu qua; bộ Map01 còn ca lỗi, nên tiêu chí toàn bộ hồi quy qua chưa đạt. Xem [Tutorial_Validation.md](Tutorial_Validation.md).

Cập nhật sau review: **checkpoint từng bước**, nút **bỏ qua toàn bộ hướng dẫn**, **slow motion kèm UI phím nhấp sáng và chú thích**, chống kẹt tiến trình và **giới hạn vùng chơi theo tuyến gần căn cứ → cầu**. Cảnh gốc được giữ nguyên; các yêu cầu gameplay đã có trong bản thử; các tiêu chí kiểm tra bên dưới vẫn là mục tiêu hoàn thiện.

Phạm vi sửa tuyến: **giữ nguyên căn cứ, địa hình và cảnh vật**, chỉ đặt lại các điểm học/lính/mục tiêu theo hướng căn cứ → cầu gỗ/bến B. Các bài 0–7 thẳng hàng trên đất có sẵn; đoạn cuối theo ramp gỗ lên cầu. Quét kiện hàng trên sàn cầu, sau đó chạy về hướng căn cứ, bị chặn ở lối lên bờ và bị hai lính kéo trở lại cầu. Không dời căn cứ, san nền, dọn cây/đá hoặc sinh đường mới. Tối ưu scene để giảm dung lượng được tách khỏi việc sửa tuyến.

## 1. Tóm tắt

- Một màn hướng dẫn **người chơi điều khiển Hùng**, xảy ra **trước Map 1**. Màn kết thúc bằng cảnh Hùng bị truy đuổi, trúng tay phải, va vào lính ở lối lên bờ, bị đánh bất tỉnh và bị kéo về cầu gỗ/bến B. Map 1 mở đầu với Hùng bị trói ở cùng bến này và đi khập khiễng sau khi được cứu.
- Scene riêng `Assets/_Project/Scenes/Maps/Map00_Tutorial.unity`, **dùng lại cảnh từ Map 1 qua tool, giữ nguyên cảnh và giới hạn vùng chơi bằng rào tutorial**. Việc thu gọn scene để tối ưu chỉ làm sau khi phạm vi cảnh được xác nhận riêng.
- **Giữ `Map01Mission` làm lõi**: dữ liệu chung, tiếng động, khả năng tàng hình, lính, ném đá, dao và túi đồ đều dùng lại. `Map01Quest` thay bằng `TutorialDirector`, bỏ `Map01SaveSystem`; bổ sung các điểm mở rộng cho loadout, retry từng bước, lính bật muộn và menu tutorial (mục 7).
- Luồng menu: **CHƠI MỚI → Tutorial → Map 1**. Có nút **Bỏ qua toàn bộ hướng dẫn** trước khi vào và trong menu tạm dừng của tutorial, kể cả khi đang ở màn thất bại hoặc cutscene.
- Khi giới thiệu thao tác mới, bật **slow motion** để người chơi đọc và bấm theo **biểu tượng phím nhấp sáng kèm chú thích trên UI**. Nhận đúng thao tác thì trở lại tốc độ bình thường để thực hành; bước chỉ hoàn thành khi đạt điều kiện gameplay.

## 2. Cốt truyện và các bước

Màn có hai lượt:

- **Lượt đi**: Hùng xuất phát ngay ngoài căn cứ trong scene tutorial, đi thẳng qua ba cụm hướng dẫn tới cầu gỗ. Các mục tiêu học không bắt người chơi đi ngược về căn cứ giữa chừng. Khoảng cách từ điểm đầu gần căn cứ tới điểm quét khoảng 145 m; không rút ngắn bằng cách dời căn cứ.
- **Lượt về**: quét hàng trên cầu xong, Hùng quay về phía căn cứ, giẫm cành khô và bị phát hiện. Hùng chạy tiếp về hướng căn cứ, bị lính chặn ở phía trước và lính đuổi phía sau, va vào lính, ngã ngồi, bị đánh báng súng và bị hai lính kéo về cầu. Lượt chạy thoát luôn hướng về căn cứ; chỉ sau bất tỉnh mới bị kéo ngược về cầu.

| Trạm | Vị trí trên tuyến thu gọn | Bước | Hoàn thành khi |
|---|---|---|---|
| **1 — Chuẩn bị** | vùng an toàn gần căn cứ | 0. Di chuyển và camera (WASD, chuột) | thực sự di chuyển và xoay camera theo chỉ dẫn |
| | | 1. Chạy nhanh (Shift) | chạy được khoảng 8 m trong bước này |
| | | 2. Ngồi lén (Ctrl/C) | đi khom khoảng 4 m |
| | | 3. Đổi vũ khí (3 dao, 1 AK — ô của Map 1) | đã cầm cả súng lẫn dao |
| **2 — Lén lút và chiến đấu** | khoảng trống và chỗ nấp giữa căn cứ với cầu | 4. Khom (Ctrl), ẩn vào bụi cây, lẻn ra sau lưng lính hạ bằng dao; ném đá (Q) tuỳ chọn | lính của bước này (đứng ngay trước bụi, quay lưng lại) bị hạ bằng dao từ sau lưng; bị phát hiện là thất bại |
| | | 5. Ngắm, bắn và nạp đạn | ngắm chuột phải, hạ lính tập bằng súng với chuột trái và hoàn tất một lần nạp đạn R; lính tập có bắn trả |
| | | 6. Hồi máu (H) | dùng băng thành công khi máu chưa đầy. Có sẵn 2 băng theo cấu hình trong `tutorial_balance.json` vì chưa có bước craft |
| **3 — Thực chiến và trinh sát** | gần cầu, có góc quan sát hàng tiếp tế | 7. Đánh với tổ AI | hạ hết tổ mặc định 3 lính cùng squad (bộ điều phối QUBO); số lượng chỉnh được theo cấu hình |
| | | 8. Trinh sát hàng tiếp tế (F, ống nhòm) | giữ ống nhòm vào điểm tiếp tế khoảng 2 giây |
| **Lượt về** | rời điểm quan sát ở cầu | 9. Giẫm cành khô, bị phát hiện (**bắt buộc**) | đi vào vùng bẫy trên đường về: cành gãy kêu, Hùng khựng lại, tiếng động lớn, đội tuần mới xuất hiện quay lại |
| | | 10. Cutscene | cận vấp cành → địch phát hiện/rượt → trúng tay, ôm tay chạy → va lính, ngã ngồi → POV ăn báng súng → tối → hai lính kéo về cầu → Map 1 |

- **Giữ cầu gỗ/bến B của Map 1** (`B_HungCaptive`, (−3; 87)) trong tutorial. Cảnh bắt giữ xảy ra ở đoạn cầu sát bờ khi Hùng đang chạy về căn cứ; sau đó Hùng bị giam trên sàn bến ở Map 1.
- **Craft để sau**: chức năng craft chưa hoàn thiện nên chưa đưa vào tutorial. Khi có, thêm một bước ở trạm 1 (sau Ngồi lén): lấy nguyên liệu ở thùng, chế băng, để bước Hồi máu dùng đúng chiếc băng vừa chế.

### 2.1. Checkpoint từng bước

- **Fail hướng dẫn nào thì làm lại đúng hướng dẫn đó.** Trạm chỉ dùng để bố trí nội dung. Ví dụ đã xong ném đá/dao, chết ở bước bắn thì chỉ làm lại bước bắn.
- Tạo checkpoint ở đầu mỗi bước, sau khi đã chuẩn bị đủ vật phẩm và mục tiêu. Lưu trong bộ nhớ của phiên tutorial; không ghi vào slot save. Thoát tutorial thì lần vào sau bắt đầu lại từ đầu hoặc chọn bỏ qua.
- Checkpoint ghi: mã bước, vị trí/hướng Hùng và camera, máu/stamina, đồ trong túi, vũ khí đang cầm và đạn trong băng, trạng thái lính/mục tiêu/trigger liên quan. Đã hoàn thành bước nào thì giữ bước đó hoàn thành.
- Khi thử lại: khoá điều khiển trong lúc phục hồi, huỷ hành động/ống nhòm/ném đá/nạp đạn còn dở; phục hồi đầu bước, làm sạch cảnh báo AI, đồ rơi và bộ đếm tạm của lần thử thất bại. Không tạo trùng lính hoặc loot.
- Màn thất bại có **Thử lại bước này** và **Bỏ qua toàn bộ hướng dẫn**. Enter gọi thử lại bước hiện tại qua `TutorialDirector`, thay vì nhánh tải lại cả scene của `Map01Mission`.
- Bước 4 gồm khom, ẩn vào bụi rồi hạ dao từ sau lưng (ném đá tuỳ chọn); nếu fail trong chuỗi này thì làm lại bước 4. Bước 5 gồm ngắm, bắn và nạp đạn; không bắt làm lại các bước 0–4.
- Bước 9 là diễn biến cốt truyện dẫn sang bước 10, không phải thất bại của người chơi. Trong cảnh bắt giữ, sát thương/animation do cutscene điều khiển, không mở màn chết gameplay thông thường.

### 2.2. Bỏ qua toàn bộ hướng dẫn và vào Map 1

- CHƠI MỚI hiển thị hai lựa chọn rõ ràng: **Chơi hướng dẫn** và **Bỏ qua hướng dẫn**. Menu tạm dừng, màn thất bại và menu trong cutscene có nút **Bỏ qua toàn bộ hướng dẫn**.
- Bỏ qua ở bất kỳ bước nào đều kết thúc tutorial và bắt đầu **Map 1 mới**, không tải bản lưu cũ. Dùng chung một hàm chuyển màn với trường hợp hoàn thành tutorial.
- Trước khi chuyển màn: dừng thoại/coroutine tutorial, trả camera/input/con trỏ/timeScale về trạng thái phù hợp, xoá checkpoint tutorial và mọi yêu cầu chuyển màn cũ. Chặn gọi chuyển màn hai lần nếu người chơi bấm liên tiếp.
- Yêu cầu opening cutscene của Map 1 tại thời điểm thực sự vào Map 1. Không để cờ này bị dùng hoặc huỷ trong scene tutorial. Đồ và sát thương của Hùng không mang sang player Nam.
- **Giữ Esc 1 giây để bỏ qua cutscene** vẫn có; thao tác này bỏ qua đoạn phim kết và đi vào Map 1. Nút bỏ qua toàn bộ tutorial luôn có trong menu, không phụ thuộc người chơi đã tới cutscene.

### 2.3. Chống kẹt tiến trình và nhịp hướng dẫn

- Thêm đường nạp loadout từ cấu hình: đá, đạn dự trữ, băng và đạn trong băng súng khi cần. Hiện `SeedStartingLoadout` chỉ có súng, dao và đá; chỉ thêm JSON chưa tự cấp được băng/đạn. Mỗi bước bắt đầu với đủ tài nguyên để làm thử.
- Chỉ bật mục tiêu của bước đang học. Lính dành cho bước dao không được chết bởi súng trước khi hoàn thành yêu cầu; nếu bị phát hiện hoặc dùng sai cách khiến yêu cầu không thể hoàn thành, cho thử lại bước với lời giải thích ngắn.
- Hết đá/đạn hoặc mất mục tiêu mà chưa hoàn thành: có cách nhận lại vật phẩm hoặc **Thử lại bước này**. Mất băng ở bước hồi máu cũng phải phục hồi được; không buộc chơi lại các bước đã xong.
- Bước bắn dùng lính tập bắn trả với sát thương có kiểm soát. Nếu người chơi hạ lính quá nhanh mà vẫn đầy máu, chuẩn bị một tình huống trúng đạn nhẹ, có tín hiệu rõ, trước khi chốt checkpoint bước 6. Bước 6 luôn có máu chưa đầy và ít nhất một băng; không chờ vô hạn vào việc AI bắn trúng.
- Hoàn thành theo kết quả thật: đã dụ đúng lính rồi hạ dao, đã trúng mục tiêu, nạp đạn xong, dùng băng thành công. Việc nhấn phím hoặc dùng vật phẩm ở bước trước không tự hoàn thành bước sau.
- Giới thiệu từng thao tác ngay trước khi dùng. Thử nhịp đi bộ và góc camera trên tuyến giữ nguyên cảnh, tránh các đoạn đường trống chỉ để đi tới trạm tiếp theo.
- Đặt trận chiến cách đội tuần ở cuối tuyến, có địa hình che và cấu hình nghe phù hợp; không để lính bắt giữ nghe súng từ sớm rồi phá trình tự. Đội tuần mới tới sau khi trinh sát xong; giẫm cành khô làm lộ Hùng với đội này. Cành khô có dấu hiệu nhìn/nghe được, cảnh bắt giữ diễn ra liền mạch.

### 2.4. Slow motion và UI hướng dẫn phím

- **Luồng một thao tác mới:** hiện gợi ý → thế giới chạy chậm → người chơi thực hiện theo phím trên UI → phím sáng xác nhận → trở lại tốc độ thường để thực hành → đạt điều kiện thì chuyển hướng dẫn tiếp theo. Không giới hạn thời gian phản ứng hoặc bắt bấm đúng một khoảnh khắc.
- Tốc độ gợi ý ban đầu **0,2×**, chỉnh được trong `tutorial_balance.json`. Đây là slow motion có thể điều khiển, không phải pause: vẫn nhận chuột/phím gameplay, nhân vật và AI vẫn hoạt động. Không dùng `mission.Cinematic` hoặc overlay khoá input cho bảng gợi ý thao tác.
- Không làm chậm cả màn: chỉ bật khi lần đầu giới thiệu một thao tác hoặc khi thử lại bước cần học. Bài ám sát (bước 4) không làm chậm thời gian vì cần canh thời điểm. Với chuỗi ngắm → bắn → nạp đạn, lần lượt hiện đúng phím của thao tác hiện tại; không đổ toàn bộ phím lên màn hình cùng lúc. Trận tổ AI là phần thực hành ở tốc độ thường.
- Khi nhận đúng thao tác khởi đầu thì thoát slow motion, nhưng giữ chú thích/progress cần thiết. Ví dụ bật chạy bằng Shift thì trở lại tốc độ thường để chạy đủ 8 m; bật ống nhòm bằng F thì thực hiện quan sát đủ 2 giây ở tốc độ thường. Nhấn đúng phím không thay thế điều kiện hoàn thành bước.
- **Bảng gợi ý:** biểu tượng phím bàn phím/nút chuột lớn, chú thích tiếng Việt ngắn, phân biệt rõ **Nhấn / Giữ / Thả**, kèm tiến độ khi cần. Viền/phím nhấp sáng nhẹ liên tục khi chờ; chữ luôn đọc được. Khi thao tác được nhận thì đổi sang trạng thái đã nhận, khi đạt yêu cầu thì hiện dấu hoàn thành.
- **Chỉ đường (đã thêm 16/10):** dải `> > > >` đặt trên mặt đất/sàn cầu, màu vàng sáng chạy lần lượt qua từng dấu theo hướng mục tiêu hiện tại. Dẫn theo NavMesh và đổi hướng khi quay về; không sửa nền, cảnh hay vị trí các trạm. UI tách cụm WASD/biểu tượng nút chuột khỏi câu hướng dẫn và chú thích phụ; kiểm tra bố cục ở 1600×900 và 1280×720.
- **Mẫu UI đã chốt:** nền kính mờ, khung bo góc, phím viền vàng phát sáng, chữ trắng có bóng nhẹ và tiêu đề vàng; bỏ ngôi sao ở góc và ô “Tiếp theo”. Hoàn thành thao tác thì báo “Hoàn thành” rồi tự chuyển bài.
- Trước bài ám sát, bắn một lính và hạ tổ lính: camera lia tới mục tiêu, giữ nhịp quan sát rồi về vị trí/góc nhìn của người chơi trước khi hiện hướng dẫn thao tác. Tổng 2,8 giây, có pause và cleanup khi thử lại/đổi bài; Hùng và AI chờ trong thời gian giới thiệu.
- **Mẫu chỉ đường đã chốt:** các mũi tên vàng tách nhau, viền vàng đồng và lõi sáng có quầng mềm; bám đất và mặt cầu, sáng lần lượt theo hướng đi. Không thêm đường nối mảnh giữa các mũi tên. Giữ tuyến NavMesh và điểm nhiệm vụ hiện có.
- Bố trí gần giữa phía dưới màn hình, tránh tâm ngắm, mục tiêu và phụ đề. Có thể làm nổi bật thêm ô vũ khí/đồ tương ứng trên HUD khi hướng dẫn đổi vũ khí hoặc hồi máu. Dùng Canvas uGUI riêng cho gợi ý trong `Tutorial Root`, theo màu/chữ của HUD hiện có; phần gợi ý không chặn chuột ngắm/bắn, các nút menu vẫn tương tác được khi mở menu.
- Hiệu ứng nhấp sáng, chuyển bảng và xác nhận dùng **thời gian thực (unscaled)** để không chậm theo thế giới; chữ/thoại hướng dẫn có đủ thời gian đọc/nghe. Giá trị tốc độ và nhịp hiệu ứng nằm trong cấu hình, không ghi cứng trong C#.

Ví dụ chú thích phải khớp cách điều khiển đang có trong Map 1:

| Thao tác | Phím được làm sáng | Chú thích trên UI |
|---|---|---|
| Di chuyển | W / A / S / D | Giữ phím để di chuyển |
| Quan sát | biểu tượng chuột | Di chuyển chuột để nhìn xung quanh |
| Chạy nhanh | Shift | Nhấn Shift để bật chạy nhanh. Nhấn lại để tắt. |
| Đi khom | Ctrl hoặc C | Nhấn Ctrl hoặc C để đi khom. Nhấn lại để đứng lên. |
| Đổi vũ khí | 3 rồi 1, theo lượt | Nhấn 3 để cầm dao / Nhấn 1 để chuyển lại súng AK |
| Ném đá | Q, trạng thái giữ rồi thả | Giữ Q để ngắm hướng ném → Thả Q để ném đá |
| Ngắm và bắn | chuột phải rồi chuột trái | Giữ chuột phải để ngắm → Nhấn chuột trái để bắn |
| Nạp đạn | R | Nhấn R để nạp đạn |
| Hồi máu | H | Nhấn H để dùng băng |
| Trinh sát | F | Giữ F và hướng ống nhòm vào hàng tiếp tế |

- Shift và Ctrl/C hiện là **nhấn bật/tắt**, không viết hướng dẫn giữ phím cho hai thao tác này. Phím hạ bằng dao lấy theo thao tác thực tế của hệ combat/interaction; chỉ hiện khi ở đúng vị trí và đủ điều kiện hạ lính.
- `TutorialTimeControl` quản lý yêu cầu chạy chậm; pause có ưu tiên cao hơn. Mở menu thì dừng game, đóng menu khôi phục mức slow motion còn hiệu lực. Hiện `Map01Mission.SetPaused` và `ForestMenu.OnEnable` ghi trực tiếp `Time.timeScale = 1`, cần điều chỉnh để không làm mất slow motion khi mở/đóng menu.
- Retry huỷ slow motion/gợi ý của lần thử cũ rồi khởi tạo lại gợi ý cho đúng bước. Chết, bắt đầu cutscene, bỏ qua toàn bộ, về menu, chuyển Map 1 hoặc unload phải thu hồi yêu cầu slow motion; khôi phục tốc độ thường và `fixedDeltaTime` ban đầu nếu đã điều chỉnh. Không để Map 1 bị chạy chậm do trạng thái còn sót.

## 3. Tool tạo scene

Menu **ShadowVale ▸ Tutorial ▸ Tạo (hoặc làm lại) từ Map 1**, có thêm bản chạy headless qua `-executeMethod`.

1. Tách phần lõi của `TestMap1AI.Create` thành `Map01SceneCopy.Create(tên đích)`. Phần lõi gồm: copy scene và thư mục `Scenes/Maps/Map 1/` (Terrain, NavMesh, Surroundings), đổi GUID sang bản copy, copy tactical graph và đặt lại `sceneName`. `TestMap1AI` gọi lại hàm này nên hành vi giữ nguyên.
2. `TutorialSceneSetup`:
   - lấy bản copy riêng làm đầu vào cho `Map00_Tutorial`, giữ nguyên cảnh hiện có trong lần sửa tuyến;
   - chỉ đổi các object gameplay riêng của tutorial; không dọn/xoá môi trường theo bounds;
   - gỡ/gắn component (mục 5);
   - đặt prefab `Tutorial Root`, gồm cả rào chặn, các điểm đầu bước và camera;
   - đặt bounds riêng cho minimap/la bàn; bake NavMesh và tactical graph riêng sau khi thu gọn và thêm rào;
   - thêm scene vào Build Settings ngay sau `Map 1`;
   - lưu.
3. **Khác Test Map 1 AI**: scene này được commit, không cho vào `.gitignore`. Mức khoảng 23 MB scene và 20 MB dữ liệu trước đây là ước lượng copy full; đo lại sau khi thu gọn. Không chỉ tắt object ở xa rồi coi như đã giảm dung lượng asset. Tool báo các asset riêng của bản copy còn được tham chiếu để dọn dữ liệu thừa, không đụng asset gốc/dùng chung của Map 1.
4. **Quy tắc dựng lại:** object gameplay riêng của tutorial nằm trong `Prefabs/Tutorial/Tutorial Root.prefab`: lính tập, vùng kích hoạt, mục tiêu trinh sát, bẫy tiếng động, điểm đầu bước, rào chặn, UI gợi ý phím và camera cutscene. Tọa độ nhiệm vụ và tham số bake lưu trong cấu hình setup riêng; NavMesh/graph được tool sinh vào thư mục tutorial. Cấu hình không được dùng để tự xoá cảnh hoặc sửa mesh nền. Áp dụng chỉnh sửa prefab trước khi chạy lại tool; override chỉ nằm trên instance trong scene cũng sẽ mất.

## 4. Phạm vi cảnh trong bản copy

| Object | Xử lý |
|---|---|
| `Detailed layout — reference routes and hills` (địa hình, cầu, trại, bến) | Giữ nguyên toàn bộ địa hình, vị trí căn cứ/cầu, cây/đá và props; chỉ thay các đối tượng gameplay tutorial riêng. |
| `Sun`, `Third person shoulder camera` | Giữ và gán lại camera target sang player Hùng |
| `03 Surroundings` | Giữ nguyên cảnh bao quanh trong lần sửa tuyến. Chưa tự cắt/xoá cảnh để tối ưu. |
| `02 Mission`: `Hide spots • bushes`, `cover_*`, `hide_*` | Giữ các điểm trong vùng chơi, bỏ điểm ngoài tuyến |
| `02 Mission`: object `HUD` (`WeaponHotbar`, tâm ngắm) | Giữ |
| `02 Mission`: `Hung` (NPC), `patrol_0..2`, `Outpost guards`, `documents`, `supplies`, `workbench`, `tutorial_loot`, `river_exit`, `Encounter exit • east bridge` | Xoá |
| Player Nam (instance của `Player.prefab`) | Thay bằng `PlayerHung` (mục 6) |

### 4.1. Giới hạn map và các hướng đi

- **Giới hạn vùng chơi tutorial trong cảnh hiện có**: tuyến từ gần căn cứ tới cầu gỗ/bến B, có chỗ cho ba cụm hướng dẫn, lượt về và cảnh bị bắt. Chừa độ rộng để núp, dụ lính và đánh tổ AI; không ép thành hành lang quá hẹp khiến kỹ năng vừa học không dùng được.
- Bao ngoài là lớp cảnh đệm đủ cho góc nhìn khi đứng/khom/ngắm/ống nhòm và camera cutscene. Chốt độ rộng sau khi thử camera; không cắt sát đường đi hoặc chỉ dựa vào minimap. Không cần thêm khu vực chơi nếu nó chỉ xuất hiện như cảnh nền.
- Dùng rừng rậm, vách đất, hàng rào, chướng ngại hoặc chốt gác để giải thích ranh giới; collider chặn di chuyển ngăn đi vòng, nhảy qua, lội sông hoặc luồn dưới cầu ra ngoài. Bố trí/layer rào không vô tình chắn tầm nhìn và đạn ngoài ý muốn.
- Rào dẫn tuyến luôn có từ đầu; vùng nhiệm vụ mở theo bước chỉ chặn đi tắt khi cần. Lượt về vẫn thông và đi qua bẫy. Chỗ kết thúc tuyến phải chặn đủ cả hai bờ hoặc mọi lối thực sự đi được.
- NavMesh và tactical graph chỉ chứa vùng AI được phép di chuyển, không dẫn lính vòng ra phần cảnh nền. **Bake riêng sau khi cắt cảnh, thay collider hoặc thêm rào**; không coi NavMesh full copy là kết quả cuối.
- Map 1 có mesh nền liên tục và cảnh bao quanh lớn. Lần sửa tuyến này giữ nguyên chúng, chấp nhận dữ liệu cảnh còn kích thước đầy đủ. Không cắt, san hoặc dựng lại địa hình. Tối ưu dung lượng cần phạm vi công việc riêng.
- Giữ cả căn cứ và cầu gỗ/bến B tại vị trí gốc. Tuyến nhiệm vụ chạy trên nền sẵn có; không sinh hoặc sửa mesh địa hình/đường.

## 5. Kiến trúc component trên `02 Mission`

| Component | Trong tutorial |
|---|---|
| `Map01Mission` | Giữ. Gán `balanceJson` = `tutorial_balance.json` (số liệu riêng: máu Hùng, sát thương lính tập, đồ ban đầu) |
| `Map01Inventory` | Giữ; bổ sung seed loadout theo dữ liệu và phục hồi vật phẩm khi thử lại bước |
| `Map01PlayerInteraction` | **Giữ** để phím bấm giống hệt Map 1. Sửa cho chạy được khi không có quest hoặc bản lưu (mục 7) |
| `Map01Hud` | **Giữ** để giao diện giống Map 1. Phần mục tiêu và la bàn đọc từ `TutorialDirector`; bỏ qua các bảng cứu Hùng và trinh sát trại |
| `Map01Quest` | Gỡ. Thay bằng **`TutorialDirector`** |
| `Map01SaveSystem` | Gỡ |
| mới: `TutorialDirector` | Danh sách bước (mục 2), điều kiện hoàn thành theo kết quả thật và việc làm khi bắt đầu. Lo checkpoint **từng bước**, thử lại đúng bước đang học, chuẩn bị vật phẩm/mục tiêu, bind lính mới bật và chuyển sang Map 1. Phát trạng thái thao tác đang chờ cho UI và yêu cầu slow motion. Trạng thái hoàn thành không phụ thuộc chỉ số Stage của Map01Quest. |
| mới: `TutorialPromptUI` | View Canvas uGUI trong `Tutorial Root`: phím/nút chuột nhấp sáng, chú thích Nhấn/Giữ/Thả và progress/xác nhận. Đọc trạng thái từ director; chỉ hiển thị, không tự quyết định bước đã xong. Animation UI chạy bằng unscaled time. |
| mới: `TutorialTimeControl` | Component cùng mission điều phối tốc độ gợi ý với pause/cutscene/retry/chuyển màn; đọc hệ số từ JSON, có cleanup khi unload. Không để nhiều component cùng ghi đè tốc độ theo mỗi frame. |
| mới: `TutorialScouting` | Ống nhòm cho bước 8: giữ F, nhìn vào `Supply cache` trong 45 m, có đường nhìn, khoảng 2 giây. `Map01Scouting` không dùng lại được vì gắn chặt với 3 trại và luật thất bại của Map 1 |
| mới: `TutorialNoiseTrap` | Vùng bẫy trên đường về, **chỉ bật sau khi trinh sát xong** (bước 8), nên lượt đi đi qua không sao. Mặc định vấp cành khô: vào cutscene khoá input, mũi dép mắc đầu cành thật, thân chúi và chân kia bước đỡ trong 2,05 giây; tiếng cành gãy ở nhịp chân chạm. Dùng nền đi tay trống và IK, không dùng hit reaction làm cú vấp. Phát tiếng động lớn (`NoiseKind.Other`), lính tuần quay về phía Hùng. Đặt bẫy rộng hết lối về để người chơi không né được |
| mới: `TutorialCaptureCutscene` | Cảnh kết, dựng theo khuôn `Map01EndingCutscene`: camera riêng, khoá input gameplay, giữ Esc 1 giây để bỏ qua nhưng vẫn mở được menu bỏ qua toàn bộ tutorial. Điều khiển lính/animation trong cảnh riêng để không bị mission.Cinematic đóng băng AI ngoài ý muốn. Hết cảnh hoặc bỏ qua thì dùng chung hàm vào `Map 1` mới. |

Hai giao diện nhỏ trong `ShadowVale.Map01`, để code Map 1 không cần biết có tutorial:

- `IMapObjectives`: chữ mục tiêu, điểm la bàn chỉ tới, đã xong màn chưa. `Map01Quest` và `TutorialDirector` cùng cài đặt. `Map01Hud` và `Map01ObjectiveGuide` đọc qua giao diện này.
- `IBinoculars`: `HoldBinoculars(bool)` và `Binoculars`. `Map01Scouting` và `TutorialScouting` cùng cài đặt. `Map01Mission`, `Map01StoneThrow` và `Map01PlayerInteraction` đọc qua giao diện này.

**Lính tập** đều là `Map01EnemyController`, vì hạ bằng dao và dụ bằng đá chỉ chạy trên lính của Map 1.

- Tổ AI ở trạm 3: cùng `SquadId = "T3"`, bộ điều phối tự gắn qua `Map01Mission`.
- Lính tắt lúc đầu phải được `BindMission` sau khi kích hoạt/khởi tạo, trước khi AI chạy; cập nhật danh sách lính của mission và các đăng ký cần thiết. Hiện mission chỉ quét lính đang hoạt động một lần trong `Start`, nên chỉ bật GameObject là chưa đủ. Thử lại bước không đăng ký trùng.
- Thêm một mục map cho `scene_name: "Map00_Tutorial"` trong content bundle nếu cần chỉnh `squad_alert_range` và các thông số tổ khác. Theo quy ước README, số liệu cân bằng để trong JSON, không để trong C#.

## 6. Hùng làm player: `Prefabs/Player/PlayerHung.prefab`

- Làm **Prefab Variant của `Player.prefab`**, nên vẫn giữ `PlayerController`, `PlayerCombat`, `Health`, `PlayerFootsteps`, `CombatAudio`, vũ khí và tinh chỉnh súng.
- Editor tool **ShadowVale ▸ Characters ▸ Setup Hung Player** (`HungPlayerSetup`), làm giống `NamModelSetup.RebuildPrefabModel`:
  - ruột của object `Model` đổi thành `Art/Characters/NPCs/hung.fbx`, material `hung - Material.001`;
  - object `Model` và `Animator` giữ nguyên, vì `PlayerController`/`PlayerCombat` đang trỏ vào chúng;
  - `WeaponAnchor` dời sang xương `RightHand` của Hùng; `HungPlayerGrip` chỉnh hai tay và hướng rifle riêng cho rig Hùng sau khi retarget, nhường lại cho reload, động tác tương tác và cutscene;
  - đo Hùng và Nam trong cùng clip đứng để tăng đồng đều tỷ lệ Model, khớp chiều cao đầu–bàn chân và cao độ đế chân; `CharacterController` dùng cùng chiều cao 1,8 m, tâm và bán kính của Nam. Không lấy kích thước NPC bị trói trong `Map01ActorAssets.hungHeight` để cân player;
  - menu **ShadowVale → Characters → Match Hung Player Size To Nam** cân lại prefab hiện có, không cần thay skeleton hoặc dựng lại map.
- Animator dùng **`AC_Player`**. Hai model đều Humanoid nên clip của Nam (dao, ném đá, ống nhòm, băng bó, hạ bằng dao) chạy được trên Hùng. **Không dùng `AC_Hung`**, vì nó không có layer cầm súng/ngắm.
- **Tay Hùng đã chốt:** giữ model `hung.fbx` hiện có với 22 xương Humanoid, không có xương ngón tay. Tư thế nắm đấm khi cầm súng và dao được chấp nhận; không cần bổ sung xương ngón hoặc xuất lại model từ Blender. Chỉ chỉnh vị trí/góc vũ khí để cầm hợp lý.
- `Map01HungVisual` (Hùng bị trói, khập khiễng) **không** gắn vào player.

## 7. Thay đổi trong code Map 1

Phạm vi gồm giao diện, kiểm tra null, loadout, vòng đời lính, phối hợp slow motion/UI với pause và đường xử lý retry/menu/chuyển màn. Dùng hành vi hiện có làm mặc định cho Map 1, tutorial cung cấp phần thay thế khi cần. Cần chạy lại toàn bộ test Map01 sau khi sửa.

| File | Thay đổi |
|---|---|
| `Map01Mission` | Cho `hung` được phép null; đọc ống nhòm qua `IBinoculars`. Bỏ qua `Map01OpeningCutscene` và `Map01HungVisual` khi không có Hùng NPC. Cổng Stopped/hoàn thành đọc trạng thái phù hợp, không phụ thuộc Stage Map01Quest trong tutorial. Thêm hook retry để Enter khi chết gọi phục hồi bước; đăng ký lính được bật muộn. `SetPaused` phối hợp với `TutorialTimeControl` khi có. Telemetry ghi `map00_tutorial`. |
| `Map01EnemyController` | Rà toàn bộ truy cập Hùng NPC để cho phép null; hỗ trợ bind/reset lính từng bước, nhận lại noise và squad đúng một lần |
| `ForestSettings`, `Map01Inventory`, `PlayerCombat` | Mở rộng dữ liệu loadout cho băng/đạn và cấu hình slow motion/nhịp gợi ý; hỗ trợ capture/restore đạn trong băng, vũ khí, vật phẩm và huỷ hành động khi retry. Giá trị mặc định giữ loadout Map 1 hiện tại. |
| `Map01PlayerInteraction` | `quest` và `saveSystem` được phép null: không có đồng đội đi theo, F5/F9 không làm gì, E không nói chuyện với Hùng |
| `Map01StoneThrow` | Ống nhòm qua `IBinoculars` |
| `Map01Hud`, `Map01ObjectiveGuide`, `Map01Minimap` | Mục tiêu và la bàn qua `IMapObjectives`; bỏ qua các bảng cứu Hùng và trinh sát trại khi không có; màn hình kết không còn ghi cứng chữ "Map 2" |
| `ForestMenu` | CHƠI MỚI cho chọn chơi/bỏ qua tutorial. Khi ở tutorial: nút bỏ qua toàn bộ trong menu tạm dừng; không cho lưu, không gọi saveSystem khi null; nội dung về menu/thoát phải nói rõ tutorial không lưu. Mở/đóng menu phối hợp điều phối thời gian để khôi phục đúng slow motion, không ghi đè về 1 ngoài ý muốn. Tải bản lưu Map 1 vẫn đi thẳng tới bản lưu. Cả kết thúc và bỏ qua tutorial dùng chung đường vào Map 1 mới, đặt opening request đúng lúc. |

## 8. Animation cần làm (Blender)

| Clip | Có sẵn? |
|---|---|
| Hùng vấp cành rồi lấy lại thăng bằng | Bản hiện tại ghép clip đi tay trống với chân bị giữ ở đầu cành, thân chúi, tay cân bằng và chân còn lại bước đỡ; nhấc chân bị vướng sau đó. Clip diễn xuất riêng là phần nâng cấp |
| Tiếng cành khô gãy | Bản thử tổng hợp tiếng gãy ngắn bằng code, không dùng bản ghi bên thứ ba; có thể thay bằng AudioClip sau. |
| Hùng chạy, trúng tay, ôm tay chạy tiếp | `Standard Run` khi tay trống, `Hung_TrungDan` và IK tay trái ôm bắp tay phải; giới hạn gập khớp, không ép góc cổ tay từ rig khác |
| Hùng va vào lính | Đã bố trí chuyển động va ngực và cận camera, tiếp nối ngã ngồi |
| Lính đánh báng súng | Góc nhìn Hùng 3,35 giây: nâng súng chếch lên một bên, giữ nhịp lấy đà rồi dộng nhanh xuống; cú vung tăng tốc tới va chạm ở khoảng 1,85 giây. Hai tay ở trước thân, nòng hướng lên khi báng tới mặt. Camera giật rồi gục/nghiêng sang bên, hình mờ tăng dần trước fade; đen hoàn toàn và giữ 1,1 giây trước cảnh kéo |
| Hùng ngã ngồi, bị đánh rồi bị kéo | Hông hạ theo mặt đất, chân tách khỏi lính chặn; cổ chân thả lỏng, gót/mũi/mép đế dép lấy cao độ nền riêng để chân không lún khi ngã hoặc qua dốc. Khi trúng báng, cổ/đầu gục sang bên và góc nhìn đi theo, rung ngắn rồi mờ dần. Hai lính dùng `Standard Walk` khi không cầm súng và nắm mỗi bên một tay kéo về cầu |
| Lính đuổi theo | Có: `Enemy_Rifle_Run` |

Lời thoại hướng dẫn và tiếng Hùng đi qua `DialogueVoice` có sẵn. Cần thu âm thêm.

## 9. Test

- `TutorialSceneTests` (PlayMode):
  - scene mở không có lỗi;
  - player có model Hùng và không có `Map01Quest`/`Map01SaveSystem`;
  - giả lập input để đi qua từng bước, xác nhận kết quả thật của ném đá/dao, bắn, nạp đạn và dùng băng;
  - khi hiện thao tác mới thì chạy chậm và hiện đúng phím/chú thích; input vẫn hoạt động, nhấn sai không tiến bước, nhấn đúng chỉ thoát slow motion chứ chưa bỏ qua điều kiện gameplay;
  - UI nhấp sáng/xác nhận vẫn chạy đúng nhịp thực ở slow motion; các chỉ dẫn Nhấn/Giữ/Thả đúng với điều khiển, không chặn chuột ngắm/bắn;
  - pause rồi resume trong lúc gợi ý khôi phục đúng tốc độ; retry khởi tạo lại gợi ý đúng bước; chết/cutscene/skip/về menu/unload không để sót slow motion;
  - fail/chết ở mỗi bước thì chỉ làm lại bước đó; Enter và nút thử lại cho cùng kết quả, không tải lại cả tutorial;
  - retry phục hồi đúng HP, đồ, vũ khí, đạn trong băng, camera, lính và trigger; thử lại nhiều lần không trùng lính/loot/đăng ký;
  - dùng hết đá/đạn/băng, hạ sai mục tiêu hoặc hạ lính bắn trả quá nhanh đều có đường phục hồi; bước hồi máu không kẹt vì đầy máu;
  - lính bật ở bước sau nhận tuning, noise và squad; lính các bước tương lai không can thiệp bước hiện tại;
  - bỏ qua toàn bộ từ màn chọn ban đầu, các bước, pause, màn thất bại và cutscene đều vào Map 1 mới; bấm liên tiếp chỉ chuyển màn một lần;
  - bỏ qua cutscene bằng Esc và xem hết cutscene đều mở Map 1 với opening request đúng, không mang theo checkpoint, damage, pause hay khoá input tutorial;
  - pause/lưu/tải/về menu/thoát hoạt động khi không có Map01SaveSystem; không tạo hoặc ghi đè slot save trong tutorial.
- `HungPlayerSetupTests`: `WeaponAnchor` nằm dưới tay phải; avatar Humanoid hợp lệ; Animator là `AC_Player`.
- `TutorialSceneSetupTests` (Editor): chạy lại tool vẫn giữ cấu hình/prefab đã lưu, không thêm trùng scene Build Settings và không thay đổi scene/asset gốc Map 1; không có điểm mục tiêu hoặc đường AI ngoài vùng chơi.
- Chơi thử trực tiếp: tìm cách đi vòng/nhảy/lội sông/luồn dưới cầu để vượt ranh giới; soi mép cảnh bằng các chế độ camera và camera cutscene; kiểm tra nhịp học, độ rõ của phím nhấp sáng/chú thích trên các độ phân giải, chuyển slow motion về tốc độ thường và cách giải thích đội tuần mới sau trận chiến.
- Chạy lại test Map01, nhớ thêm `-testCategory "!Replay"`.

## 10. Thứ tự làm

| Mốc | Việc | Xong khi |
|---|---|---|
| T0 | Chốt căn cứ, một cầu, bờ đi và phác tuyến ngắn; đặt ba cụm hoạt động, vùng chơi/cảnh đệm/rào | Tuyến đủ chỗ học từng thao tác, có lượt về và chỗ bắt giữ; không cần đi hết ba cầu |
| T1 | Tách `Map01SceneCopy` khỏi `TestMap1AI` | Test Map 1 AI tạo ra giống hệt trước |
| T2 | `HungPlayerSetup` → `PlayerHung.prefab` | Hùng chạy, khom, bắn, đổi vũ khí được trong một scene thử |
| T3 | Giao diện, null, hook retry, loadout, bind lính và menu không có saveSystem (mục 7) | Toàn bộ test Map01 qua; có đường để tutorial cung cấp checkpoint từng bước |
| T4 | `TutorialSceneSetup`: đặt tuyến nhiệm vụ trên cảnh gốc, rào, bounds UI, NavMesh/graph riêng và Build Settings | Hùng chơi được trong vùng giới hạn; camera không thấy mép trống, AI không đi ra cảnh nền |
| T4a | Khung `TutorialDirector`, luồng tối giản menu → tutorial → cutscene tạm → Map 1; nút bỏ qua toàn bộ và hàm chuyển màn chung | Chơi/bỏ qua đều vào Map 1 mới đúng trạng thái; kiểm tra ngay trước khi làm đủ các trạm |
| T5 | `TutorialDirector` + trạm 1 (bước 0–3), checkpoint từng bước, màn thử lại, `TutorialPromptUI` và `TutorialTimeControl` | Đi hết trạm 1 theo gợi ý phím nhấp sáng/chú thích; slow motion → nhận thao tác → tốc độ thường hoạt động đúng, pause/retry không mất trạng thái; fail bước nào chỉ làm lại bước đó |
| T6 | Trạm 2 (bước 4–6): lính tập, ngắm/bắn/nạp đạn, hồi máu và chống kẹt | Đi hết trạm 2; hết đồ/sai mục tiêu/đầy máu đều phục hồi được tại đúng bước |
| T7 | Trạm 3 và lượt về (bước 7–9): tổ mặc định 3 lính, `TutorialScouting`, `TutorialNoiseTrap` và đội tuần mới | Trinh sát xong, quay về, giẫm cành khô thì đội tuần phát hiện đúng thời điểm |
| T8 | `TutorialCaptureCutscene`: chuỗi camera khoảng 25 giây, vấp/ôm tay/ngã ngồi/dộng báng/kéo bằng pose IK, camera gục sang bên và mất nét trước màn đen tách cảnh kéo, kết ở cầu gỗ/bến B; clip Blender riêng là phần nâng cấp | Từ menu chơi hết tutorial hoặc bỏ qua ở bất kỳ giai đoạn nào đều vào Map 1 |
| T9 | Test tutorial, viết `Docs/Tutorial.md` | Test qua |

## 11. Rủi ro và việc còn mở

- **Chạy lại tool sẽ xoá chỉnh tay trong scene**, kể cả override chưa apply của `Tutorial Root`. Thay đổi bền vững phải nằm trong prefab/cấu hình setup; ai sửa scene cũng phải biết quy tắc này.
- **Map 1 đổi bố cục** (dời cầu, cây) thì tutorial không tự cập nhật; phải chạy lại tool, rà vùng giữ cảnh/rào/camera và bake lại dữ liệu riêng. Lính tập trong `Tutorial Root` có thể phải dời theo.
- **Thu gọn scene chưa chắc giảm ngay dữ liệu mesh**: nền/cảnh bao quanh liền khối cần cắt hoặc dựng lại riêng. Không tính dung lượng giảm chỉ từ số GameObject đã xoá.
- **Retry từng bước** phải phục hồi đủ state; thử lại nhiều lần là một tiêu chí kiểm tra bắt buộc, tránh nhân bản loot/lính hoặc sót trạng thái chết/khoá input.
- **Slow motion và pause** đang có các chỗ cùng ghi `Time.timeScale`; phải phối hợp theo mục 2.4, bảo đảm UI dùng unscaled time và không để tốc độ tutorial sót sang Map 1.
- **Craft chưa có**: bước craft được thêm vào trạm 1 khi chức năng xong (mục 2).
- **Cutscene phụ thuộc clip Blender** (mục 8). Làm luồng chuyển màn bằng clip tạm ngay ở T4a, hoàn thiện diễn xuất ở T8.
- Còn mở:
  - đích là cầu gỗ/bến B; căn cứ và cảnh gốc được giữ nguyên; chỉ sửa tuyến nhiệm vụ tiến dần tới cầu. Cần chơi thử nhịp từng bài trên quãng đường khoảng 145 m;
  - bounds vùng chơi/cảnh đệm và rào đã đặt; còn rà các lối vượt rào, mép cảnh và góc camera cuối;
  - phần mesh nền nào giữ nguyên ở bản đầu, phần nào cần cắt/dựng lại riêng và dung lượng sau thu gọn;
  - độ khó cuối của tổ AI sau chơi thử; mặc định 3 lính. Bước bắn mặc định dùng lính thật được cấu hình cho tutorial.
