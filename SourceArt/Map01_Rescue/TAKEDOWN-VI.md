# Kết liễu nhanh và báo động khi giám sát rời Hùng

Trang này mô tả bản sửa mới nhất. Thông số điều tra đá thay các số cũ trong KNIFE-CARRY-VI.md.

## Cách chơi

- Trang bị dao, ném đá ra một điểm đất có đường đi, rồi cúi vòng sau giám sát.
- Biểu tượng dao xuất hiện khi ở gần: màu xám chưa đủ điều kiện, màu xanh sáng kèm **[CHUỘT TRÁI] KẾT LIỄU** là có thể bấm. Nếu bị vật cản che thì không hiện. Đổi sang súng hoặc mở inventory sẽ ẩn biểu tượng.
- Riêng giám sát: tối đa 2 m, phía sau trong góc khoảng ±60 độ. Lính khác vẫn là 1,6 m và khoảng ±50 độ phía sau. Địch đã giao chiến không cho kết liễu lén.
- Giám sát điều tra đá tối đa 12 m trên NavMesh, tập trung 11 giây rồi trở về. Không đi xuống nước.
- Khi hắn còn tại vị trí canh, báo động vẫn dẫn tới xử bắn Hùng. Khi hắn đã rời vị trí, xác nhận phát hiện hoặc nghe súng khiến nhóm bốn lính còn sống tập hợp truy kích Nam; Hùng không bị xử bắn từ xa. Báo động combat được lưu, không chuyển lại thành xử bắn khi hắn quay về.
- Vẫn phải hạ đủ bốn lính mới cởi trói và hộ tống. Các nhiệm vụ sau giữ nguyên.

## Chỉnh animation

Hai clip chỉnh được ở `Assets/_Project/Art/Characters/Animations/KnifeCarry`:

- `Nam_Takedown_Neck.anim`
- `Enemy_Takedown_Neck_Victim.anim`

Cặp clip dài 2,2 giây, 30 fps. Khoảng 0–0,28 giây đưa tay lên; 0,28–1 giây là một nhát vào cổ và rút tay; phần còn lại buông lính, ngã và trở về điều khiển. Không phát đoạn đâm hông của FBX gốc.

Sao chép clip trước khi sửa. Mở **Window > Animation > Animation**, chọn nhân vật và clip. Trong Curves chỉnh Right Arm/Forearm/Hand cho tay dao, Left Arm/Forearm/Hand cho tay giữ; xem cả hai nhân vật cùng lúc. Giữ nguyên root/hip/leg nếu chỉ sửa tay. Kiểm tra lại khoảng chạm cổ và bàn chân trên đất thực.

Controller Nam dùng state `Act_Takedown`, lính dùng `TakedownVictim`; tên state và ID action không đổi. `Map01TakedownIK` dùng bảng tiếp xúc gốc theo hàm `Map01NamActions.TakedownSourceFrame`. Tư thế đích lúc đưa tay lên là `TakedownReadyFrame = 71`, bỏ cả nhịp dao còn thấp sát hông. Nếu đổi nhịp nhát đâm phải chỉnh các hằng `TakedownWindupSeconds`, `TakedownStrikeSeconds`, `TakedownReadyFrame` và thời điểm camera tại `Map01RescueCinematic` cho đồng bộ.

Menu **ShadowVale > Characters > Update single neck takedown** chạy `QuickTakedownSetup.Build`: tái tạo hai clip từ FBX gốc rồi nối lại đúng hai state, không xây lại cả controller. Menu sẽ ghi đè chỉnh sửa thủ công của hai clip, cần sao lưu trước. FBX, mesh và skeleton gốc giữ nguyên.

## Chỉnh luật trong Inspector

Prefab `Assets/Resources/Rescue/Map01RescueLayout.prefab`:

| Tham số | Mặc định | Tác dụng |
|---|---:|---|
| Overseer Stone Radius | 12 m | Khoảng đi điều tra đá tối đa |
| Overseer Stone Focus Seconds | 11 s | Thời gian chú ý đá |
| Overseer Stone Sweep Degrees | 8° | Biên độ nhìn lệch quanh đá |
| Overseer Hostage Radius | 1,25 m | Chỉ đứng tại vị trí canh mới có thể xử bắn |
| Overseer Backstab Range | 2 m | Tầm kết liễu riêng cho giám sát |
| Overseer Backstab Angle | 120° | Góc từ hướng nhìn địch tới Nam; lớn hơn là ở sau |
| Takedown Hint Range | 3,2 m | Khoảng hiện biểu tượng gợi ý |
| Kill Camera Seconds | 0,9 s | Khoảng zoom ngắn tại nhịp kết liễu |

Tiến trình save vẫn dùng các ID quest và lính cũ; trạng thái cứu Hùng bổ sung `combatAlarm`. Các save cũ thiếu cờ này được coi là chưa báo động combat.

Kết quả kiểm thử, ảnh và video nằm ở `Verification/QuickRescue`.
