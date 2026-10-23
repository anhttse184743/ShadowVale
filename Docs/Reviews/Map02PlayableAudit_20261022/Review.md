# Map 2 Ngày 1–Đêm 1 — Kiểm tra thao tác và lồng tiếng, 22/10/2026

Project được sửa: G:\game\Capstone\ShadowVale, nhánh Khoa. Editor.log của lần người dùng chụp ảnh xác nhận đã mở checkout cũ G:\game\ShadowVale; checkout cũ không có các sửa HUD/nhiệm vụ mới. Thanh tiêu đề Editor ở bản mới hiện đường dẫn project để phân biệt.

## Lỗi đã tái hiện và sửa

- Phím E của đèn/cửa/lối đi tại chốt bị vùng báo cáo người gác giành ưu tiên. Khi chốt còn lỗi, ưu tiên kiểm tra đồ vật gần; khi chốt đạt, cho báo cáo và ghim bám đúng người gác.
- E còn dư lúc kết thúc thoại hoặc tự kết thúc chặng hộ tống có thể mở tương tác kế tiếp. Thêm nhịp nhả phím và chặn sử dụng lại input ở bước chuyển.
- Điểm đổi ca cũ nằm sau công trình: NavMesh có đường nhưng CharacterController bị kẹt. Thêm marker Inspector nightOneRest ở khu nhà chị Lành, chỉ áp dụng N1_04. Không đổi điểm nghỉ các ngày sau.
- Hùng tự về chỗ nghỉ sau khi báo cáo chốt đầu làng; load ở mốc này tiếp tục đường đi đó.
- Vị trí đứng sau thay băng/bữa cơm được chọn trên NavMesh, tránh Nam và gia đình, không đứng chồng lên ghế hoặc ba lô.
- Trạng thái tay khi mở mini game được giữ ổn định; Nam đi bình thường cùng Hùng, chân khiêng dùng clip đi thường và lớp tay riêng.
- Cả ba mini game có hướng dẫn và thao tác bàn phím; chuột vẫn được hỗ trợ. Kho: 1–3 chọn thùng, mũi tên chọn ô, R xoay, E đặt, Enter xác nhận. Sửa bến: Space theo nhịp; 1–5 chọn ván, lên/xuống chọn bậc, A/D căn, R xoay, E kê. Chốt: A/D xoay đèn; A khép cửa, D cài chốt; 1/2 chọn chỗ cất bó vật liệu; E xác nhận.
- Cập bến Map 2 bỏ hai câu Rõ giữa lượt giao việc; chỉ còn một câu ở cuối. Lượt giao chỉ thị cuối ngày 3 cũng bỏ câu lặp. Giao nhiệm vụ cứu Hùng Map 1 bỏ câu Nam xác nhận sớm, giữ Rõ sau chỉ thị cuối.

## Lồng tiếng

49 câu có lời được tạo offline bằng VieNeu-TTS v3 Turbo, gồm ba câu trên thuyền, hội thoại nhiệm vụ, bữa cơm, hai câu trên đường (Vịt thôi / Biết mà), báo cáo chốt và xác nhận sửa bến. Một cue đổi ca không có chữ, giữ khoảng im lặng theo kịch bản. Tổng chi phí dịch vụ: 0; không dùng Runway hoặc credit trả phí. Bộ giọng được người dùng duyệt trước khi gắn.

Dùng preset v3 Turbo, không nhân bản giọng cá nhân. Bảng đầy đủ: Docs/Map02Chapter/DayOne/BangThoai.md. Nguồn, model revision, seed và SHA-256: SourceArt/Dialogue/Map02DayOneFree/voice-manifest.json; kèm LICENSE, model card và script generate_offline.py. WAV phát trực tiếp trong Unity; game không cần Python hay mạng.

Nguồn model: https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo (model card ghi Apache-2.0); SDK: https://github.com/pnnbao97/VieNeu-TTS .

## Bằng chứng kiểm tra

- flow6.xml: 25/25 đạt, gồm toàn tuyến mười mục tiêu, test trình diễn, kho/bến/chốt, save/load và các thứ tự công việc độc lập.
- Driver toàn tuyến đi bằng WASD, bấm E và giải qua input mini game. Không teleport, không gọi Complete hoặc EndScene để vượt nhiệm vụ. Cảnh thuyền chạy tự nhiên. Chạy ở timescale 2, tự bấm E để tiến thoại; không dùng lượt này làm thời lượng chơi lần đầu.
- voiced-flow.xml: toàn tuyến có audio và hai ca coverage/phát từng câu đạt. Test thoại Map 1 ở lượt đó phát hiện fixture cũ không tính 1 cm điều chỉnh chân; đối chiếu với test tương tác mở đầu đang dùng ngưỡng 2 cm và sửa fixture. Không đổi vị trí hoặc camera sản phẩm chỉ để khớp assertion cũ.
- Ảnh và log từng bước nằm ở Logs/Map02PlayableAudit_20261022. Kết thúc tới Ngày 2 với đủ 10 nhiệm vụ đã hoàn tất, controller được trả lại.
- Ô lưu 1 ban đầu giữ nguyên SHA-256 515A2E7126AD278A836A1791DCABE2C6C8395FED44AE23FEF1DEFA51F3E07697. Test dùng thư mục lưu riêng.

Chưa có phiên đánh giá với người chơi mới hoặc đo lại thời lượng chơi tự nhiên. Các kết quả trên chứng minh những đường chơi đã kiểm tra; không phải cam kết mọi tình huống của game không thể có lỗi.

## Kết quả cuối sau khi gắn đầy đủ audio

final-49-voices.xml: 4/4 đạt. Kiểm tra đủ 49 câu đúng caption/nhân vật, phát từng câu đến hết, không chồng tiếng; trao đổi trên đường có cả Nam và Hùng theo đúng thứ tự. Kiểm tra tải lại sau ca gác tiếp tục đưa Hùng về nghỉ. Thoại cứu Hùng Map 1 nghe đủ ID 01, 02, 03, 05 rồi m1_v2_rescue_approach_03 (một Rõ cuối); ID 04 xác nhận sớm không còn được phát. voiced-flow.xml xác nhận toàn tuyến có tiếng hoàn tất mười nhiệm vụ. Không đổi code gameplay sau các lượt này.

