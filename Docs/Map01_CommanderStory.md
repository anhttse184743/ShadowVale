# Map 1 — Luồng nhiệm vụ và hội thoại do chỉ huy bàn giao

## Cách chơi sau thay đổi

1. Chơi mới vào gameplay trong hầm. Đến chỉ huy, nhấn **E** để trò chuyện. Nam và chỉ huy không cầm súng trong hội thoại. Các lượt thoại tự phát và tự chuyển theo audio; E có thể bỏ qua câu, một lần nhấn E riêng sau câu cuối mới nhận lệnh. Giữ Esc một giây đóng cuộc nói chuyện, không tự nhận nhiệm vụ.
2. Nam nhận lệnh cứu Hùng. Tới gần bến phía bắc phát báo cáo qua đàm bằng giọng đã có: địch đông, không thể đánh trực diện, xin chỉ thị. Nam đứng tại chỗ; camera giữ góc người chơi, không quay tới Hùng.
3. Giữ luật cứu Hùng: đá dụ giám sát, dao phía sau hoặc kéo vào bụi. Khi giám sát còn bên Hùng, báo động dẫn tới thủ tiêu. Nếu hắn đã bị dụ đi xa, nhóm lính tập hợp giao chiến. Camera báo động vẫn ở phía Nam; tiếng thoại/phát bắn và kết quả nhiệm vụ xảy ra tại vị trí thật của Hùng. Giữ nguyên animation quỳ trói và các thao tác ám sát.
4. Hạ bốn lính rồi E cởi trói. Đưa Hùng về hầm, giữ ba đợt truy kích và cảnh vào hầm hiện tại. Hùng không giao lệnh chính.
5. E gặp chỉ huy ở căn cứ: Nam báo cáo, Hùng cung cấp thông tin nghe được khi bị bắt; chỉ huy giao nhiệm vụ thu hồi vật tư ở điểm 1.
6. Điểm 1: tới thùng vật tư được đánh dấu, E thu hồi. Nhận một lần: 1 hàng tiếp tế, 2 băng cứu thương, 30 đạn súng trường. Không cần giết hết lính. Di chuyển bị hủy/người chơi chết trước khi thao tác hoàn tất thì chưa nhận vật tư.
7. Rời trại/cắt đuôi, E báo cáo qua đàm. Chỉ huy giao ngắt nguồn đài tại điểm 2. Không thể dùng cuộc trò chuyện để đứng bất tử giữa giao tranh.
8. Điểm 2: tới bàn đài có sẵn, E ngắt nguồn. Đèn nguồn tắt, không cần diệt sạch lính. Rút khỏi giao tranh rồi E báo cáo để nhận nhiệm vụ điểm 3.
9. Điểm 3: tên chỉ huy địch có mặt từ lúc bắt đầu map ở vị trí cố định trong trại thứ ba. Giữ F nhìn qua ống nhòm hoặc tiến gần với đường ngắm rõ để xác nhận. Nam báo cáo, chỉ huy ra lệnh hạ mục tiêu và rút. Không còn chuỗi bắt buộc trinh sát đủ ba trại không bị phát hiện, rồi tiêu diệt toàn bộ lính của cả ba trại.
10. Hạ chỉ huy địch: giữ cảnh chết hiện tại và liên lạc rút lui. Lính khác không tự biến mất. Tới bến là lên thuyền, không cần giết hết địch. Hùng chèo, hai người còn lại yểm trợ như chuỗi đã có.
11. Trên đường rời bến: Nam hỏi nơi đến; chỉ huy cho biết điểm liên lạc ở làng ven sông; Hùng giữ thuyền sát bờ. Hoàn thành sau khi thuyền rời bến; giữ chuyển sang Map 2 hiện có.

Tutorial chưa được triển khai trong phạm vi này. Người phụ trách tutorial có thể chuyển vào Map 1 bằng `Map01SaveSystem.BeginGame(-1)`; map không tự mở cutscene chào/giao lệnh cũ và chưa tự nhận nhiệm vụ cứu Hùng.

## Kịch bản hội thoại

Nội dung thực thi nằm trong `Assets/_Project/Map01/Runtime/Map01Story.cs`, phương thức `Brief`, `Report`, `ObserveCommander`. Mỗi `Line` gồm người nói, câu nói, cờ thoại từ đầu đàm bên kia, ID audio tùy chọn. Thay câu tại đây để chỉnh kịch bản.

### Nhận nhiệm vụ cứu Hùng — tại hầm

- Chỉ huy: “Trinh sát đã thấy Hùng ở bến phía bắc. Anh ấy còn sống, nhưng đang bị giữ.”
- Nam: “Có bao nhiêu lính canh?”
- Chỉ huy: “Chưa rõ. Cậu tới quan sát trước, tìm cách đưa Hùng ra. Đừng nổ súng khi chúng còn giữ anh ấy.”
- Nam: “Rõ. Tôi sẽ tiếp cận kín rồi báo tình hình.”
- Chỉ huy: “Kiểm tra trang bị trước khi đi. Cứu được người thì đưa thẳng về hầm.”

### Báo cáo cứu Hùng và nhận mục tiêu tiếp tế

- Nam: “Đã đưa Hùng về. Trên đường có lính truy kích.”
- Hùng: “Lúc bị giữ, tôi nghe chúng nói sẽ siết các lối ra bến. Trong rừng còn mấy điểm đóng quân.”
- Chỉ huy: “Vậy không thể đưa người ra bến ngay. Ta cần vật tư và một khoảng trống để rút.”
- Nam: “Tôi đi kiểm tra các điểm đó.”
- Chỉ huy: “Tôi giao từng mục tiêu. Hùng ở lại hồi sức. Nam, tới điểm thứ nhất lấy thuốc và vật tư mang được. Không cần giữ trại.”
- Nam: “Rõ. Lấy đủ tôi sẽ rời vị trí.”

### Từ tiếp tế sang phá liên lạc — qua đàm

- Nam: “Đã lấy được vật tư. Tôi đã ra khỏi trại.”
- Chỉ huy: “Rõ. Điểm thứ hai có đài liên lạc của các chốt. Cắt được nó, ta có thêm thời gian đưa Hùng ra bến.”
- Nam: “Tôi sẽ xử lý thiết bị. Nếu bị lộ thì sao?”
- Chỉ huy: “Hoàn thành rồi rút. Đừng kéo dài giao tranh.”

### Từ phá liên lạc sang tìm chỉ huy — qua đàm

- Nam: “Đài liên lạc đã ngừng hoạt động. Tôi đã rời vị trí.”
- Chỉ huy: “Tới điểm thứ ba. Tìm tên sĩ quan đang điều lính về hướng bến. Xác nhận mục tiêu trước khi hành động.”
- Nam: “Rõ. Tôi giữ hướng này.”
- Chỉ huy: “Tôi và Hùng sẽ ra bến theo đường kín. Hạ tên chỉ huy rồi rút, không truy kích sâu.”

### Xác nhận mục tiêu — qua đàm

- Nam: “Tôi thấy một sĩ quan đang điều lính về hướng bến. Đúng là tên chỉ huy các chốt.”
- Chỉ huy: “Hạ mục tiêu rồi rút. Tôi và Hùng ra bến theo đường kín. Đừng truy kích sâu.”
- Nam: “Rõ. Tôi sẽ mở đường ra bến.”

### Rời bến

- Hùng: “Lên thuyền đi, Nam. Tôi giữ thuyền.”
- Nam: “Mình đi đâu tiếp?”
- Chỉ huy: “Có điểm liên lạc ở làng ven sông. Tới đó kiểm tra đường rồi tính tiếp.”
- Hùng: “Tôi giữ thuyền sát bờ.”

## Asset và marker

Trong Map 1, chọn **Map01 Operations — commander orders**, component **Map01OperationsLayout**:

| Trường | Vai trò |
|---|---|
| Supply / Supply Full | Chồng tiếp tế sẵn có của trại 1; ẩn khi đã lấy |
| Supply Empty | Thùng trống tái sử dụng `Prop_Crate`, hiện sau khi lấy |
| Supply Approach | Điểm đứng thao tác với vật tư; phải ở trên NavMesh |
| Radio / Radio Approach | Bàn đài sẵn có của trại 2 và điểm đứng ngắt nguồn |
| Radio Powered / Radio Disabled | Dấu nguồn bật/tắt, hai vật liệu nhỏ tạo tại Resources/Story |
| Commander Post | Vị trí đứng và hướng nhìn ban đầu của chỉ huy địch tại trại 3 |
| Interaction Range | Phạm vi E, mặc định 2,4 m |
| Supply Seconds / Radio Seconds | Thời lượng thao tác mặc định 3,2 / 3,5 giây |

Không mua hoặc tải asset mới. Dùng lại `FieldCamp/SupplyStack`, `FieldCamp/RadioStation`, `Map01_Optimized/Prop_Crate`, model lính, rig, clip idle/talking/pickup/open-chest và cutscene đang có. Không thêm animation chiến đấu hoặc chỉnh xương. Không chỉnh địa hình hoặc nội dung Map 2.

Menu `ShadowVale > Story > Prepare Map 1 operations` tạo setup nếu chưa có; khi đã có chỉ kiểm tra, không ghi đè marker bạn chỉnh. Lưu scene sau khi chỉnh marker. Đường tiếp cận được kiểm tra NavMesh khi tạo.

## Save và tương thích

Giữ nguyên IDs 0–11 và phiên bản địa hình 7. Snapshot có thêm `story` với revision 1: nhận lệnh cứu Hùng, tiếp tế đã lấy, đài đã tắt, đã xác nhận chỉ huy, chỉ huy đã chết. Save/load không cộng lại vật tư.

| ID cũ | Vai trò hiện tại |
|---|---|
| 0 / 1 / 2 | Cứu Hùng / hộ tống / báo cáo chỉ huy |
| 3 / 4 | Lấy tiếp tế / báo cáo qua đàm |
| 5 / 6 | Ngắt đài / báo cáo qua đàm |
| 7 / 8 | Tìm-hạ chỉ huy / chuyển cảnh sau khi hạ |
| 9 / 10 / 11 | Đã hoàn thành / extraction / boarding |

Save cũ không có `story` được coi đã nhận lệnh. Stage từ 4 trở đi không phải làm lại tiếp tế; từ 6 trở đi không phải tắt lại đài. Save cũ đang chờ báo cáo chiến thắng ở stage 8 vẫn hoàn thành. Boss snapshot cũ được ánh xạ sang actor ổn định tại trại 3. Địch chết sớm không hồi sinh và không được thêm phần thưởng lần nữa. Không lưu trong hội thoại/cutscene hoặc khi thao tác toàn thân chưa bàn giao điều khiển.

## Audio

Đã lồng 22 lượt thoại bằng bộ giọng hiện có, theo phê duyệt riêng tối đa 35 credits. Nam/Brodie, chỉ huy/Tom, Hùng/Clint giữ cùng giọng; mỗi câu có ID riêng và caption khớp hoàn toàn. Thoại tự chuyển theo thời lượng audio. Câu cuối chờ E xác nhận sau khi nói xong. Xem `Docs/Map01_CommanderVoices.md`.

Ba cue báo cáo cứu Hùng đã có được tái sử dụng. Nam dùng master sạch tại chỗ; chỉ huy từ đầu đàm giữ bản lọc. Script `SourceArt/Dialogue/apply_rescue_voice_channels.py` dùng thư viện Python chuẩn để áp dụng lại từ master có sẵn, không gọi TTS. Bản WAV sạch và bản nguồn MP3 vẫn được giữ. Các câu extraction đã khớp nội dung được giữ; câu “đường đã thông” cũ không còn phát.

## Kiểm tra

`Map01StoryTests` kiểm tra New Game không tự chạy intro; E nhiều lượt và xác nhận riêng; Nam đứng yên; lấy tiếp tế/tắt đài khi lính còn sống; save/load giữ vật tư, đài, boss không nhân đôi; hạ boss chuyển sang extraction; camera intro/báo động không rời Nam; skip cho cùng kết quả.

`RescueRadioIntroTests` kiểm tra playback/skip, chân đứng yên, giữ aim xuyên bàn giao và phục hồi controller, vũ khí, máu, đạn. `Map02ArrivalTests` kiểm tra hồi quy thuyền/hành khách khi chuyển cảnh. Kết quả chạy được ghi riêng trong báo cáo kiểm chứng của lần bàn giao, không xem việc biên dịch thành công là đã chơi thử.
