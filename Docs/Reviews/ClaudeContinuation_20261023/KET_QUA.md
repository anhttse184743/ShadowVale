# Hoàn tất đoạn Claude đang dở — 23/10/2026

## Phạm vi

Người dùng chọn hoàn thiện đoạn đang dở của phiên **Cập nhật code từ dev**: dàn cảnh Lời hứa, máy đàm Hùng, tổ gác bờ tre. Không mở rộng sang toàn bộ lộ trình 33 mục Ngày 1–Đêm 1.

## Đã hoàn tất

- Sửa chuỗi C# bị ngắt dòng trong test chụp máy đàm và chờ render đăng ký prop trước khi chụp: `Assets/_Project/Map01/Tests/Map02PresentationTests.cs:93`.
- Giữ phần đặt máy đàm Claude vừa sửa, kiểm chứng bằng mesh thực sau skinning và ảnh Unity. Có 12 mẫu, 4 hướng quay × một idle/hai thời điểm walk; 25 tia mỗi mẫu. Độ hở nhỏ nhất khoảng **7,8 mm** tại vùng lấy mẫu. Không coi đây là kiểm định toàn bộ mọi frame: `Assets/_Project/Map01/Tests/Map02DayOneCompletionTests.cs:206`.
- Kiểm tra prop thực sự được vẽ bằng ảnh bật/tắt trên một layer tạm riêng trong test; không để cây/NPC chuyển động tạo sai biệt pixel giả. Lưu/tải tuần tra giữ máy; sang Ngày 2 không có bản sao thừa: `Assets/_Project/Map01/Tests/Map02DayOneCompletionTests.cs:81`.
- Tổ gác dùng đúng lựa chọn người dùng: **Được. Qua chốt đầu làng kiểm tra nốt đi.**; tên người nói Tổ gác. WAV cũ giữ nguyên hash `5330365bc1430ecee427b3940ee0afd3bf03e94e10c0df7544f3ff9e508bf299`. Đã đồng bộ script, hai catalog, manifest, kế hoạch giọng và bảng thoại. Xem `guard-dialogue-receipt.json`.
- Bổ sung câu Hùng **...Ngon hơn nồi này nhiều.**, preset Minh Quân, seed 2049, PCM16 mono 48 kHz, **1,2 giây**. Người dùng đã nghe duyệt và cho đưa vào game. Hash `873d1d6f390f63a71bd0c6aa1b41586e1bbf62a21e4b49b36f01111513e65376`. Xem `promise-integration-receipt.json`.
- Kiểm tra cảnh chạy hết nhịp tự động, giữ cuối câu ít nhất 3 giây, trả điều khiển; tải trạng thái đã hoàn thành không diễn lại: `Assets/_Project/Map01/Tests/Map02DayOneCompletionTests.cs:20`.

## Kiểm thử thật

| Lượt | Tổng | Qua | Lỗi |
|---|---:|---:|---:|
| regression | 62 | 60 | 2 |
| final-voice-staging | 28 | 28 | 0 |

Lượt regression đầu có hai lỗi thật do thiếu cue `m2v3_promise_05b`. Lượt cuối chạy lại các nhóm liên quan sau khi thêm bản thu đã duyệt. Gộp theo tên ca, lấy kết quả mới nhất: **63/63 ca riêng biệt đạt**, không cộng trùng các lần chạy. Chi tiết từng ca nằm trong `verification.json`; XML và log giữ cùng thư mục.

Phép kiểm thời gian dùng driver game-clock, không phải chơi 90 giây ở thời gian thực. Có phép kiểm riêng phát đủ 50 WAV tới hết, không chồng tiếng. Số liệu game-clock:

```text
approach=91,110
supper=92,390
lastLineAndHold=6,250
completedAfterLoad=True
```

`approach` ở test tính từ vị trí fixture lúc bắt đầu; `supper` là phần sau khi tới chỗ ngồi. Không dùng số đó để suy ra thời lượng toàn Ngày 1–Đêm 1.

## Ảnh Unity

- `radio-overview.png`, `radio-back.png`, `radio-side.png`: thấy máy/anten và vị trí trên model thực.
- `radio-isolated.png`, `radio-hidden-reference.png`: ảnh phục vụ test hiển thị, đã tách môi trường.
- `family-meal.png`, `meal-two-shot.png`, `meal-nam-close.png`: cửa hiên và hai góc cận.
- `bamboo-watch.png`: một đứng, một ngồi; anh Lực ở chốt đầu làng.

Ảnh được render **1920×1080**. Lượt này **không đo hoặc chứng nhận 60 FPS**.

## Giới hạn thẩm mỹ và việc sau

Cảnh hiện vẫn dùng clip MealNam/MealHung và ghế đẩu cũ; máy đàm là prop khối đơn giản. Hoạt ảnh ngồi xếp bằng, tháo mũ/ba lô, đũa/hích vai/đẩy nồi và model chi tiết là giai đoạn art tiếp theo, chưa được đánh dấu hoàn tất. Âm nền/nhạc, UI kho và các việc chưa bắt đầu khác nằm ngoài phạm vi người dùng vừa chốt. Không xóa hoặc thay clip/model đang dùng để che việc còn thiếu.

## Bảo toàn

- Hash **20 file lưu hiện có** khớp trước lượt test, gồm các ô thủ công và ô tự động đã có.
- Map 1.unity và Map 2.unity không có thay đổi theo Git. Không sửa mô hình Nam hoặc art C3.
- Không thay cân bằng chiến đấu, không tải/merge nhánh dev mới trong lượt này.
- Chưa commit, chưa push. Before/, before.patch và before-hashes.json giữ phần Claude trước khi tiếp nhận.
- Bộ tạo giọng và model ở `Temp/Map02VoiceTools`, không đưa vào Assets hay bản build; game chỉ thêm WAV 1,2 giây.

## Tài liệu đã thực sự đọc

- [Unity — BakeMesh](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html): đo bề mặt sau skinning.
- [Unity — TransformPoint](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/transform/transformpoint): đổi đỉnh mesh sang tọa độ thế giới.
- [Unity — GPU Resident Drawer compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html): đối chiếu cập nhật đối tượng giữa các camera; không kết luận lỗi engine, không tắt tính năng toàn game.
- [VieNeu 3.8.0 trên PyPI](https://pypi.org/project/vieneu/3.8.0/), [SDK chính thức](https://docs.vieneu.io/docs/sdk/standard-mode/): dùng preset có sẵn và backend ONNX CPU, không nhân bản giọng người thật, không API trả phí.

Không xem hoặc viện dẫn video mới trong lượt sửa code/thoại này.
