# Lan can cầu và vị trí Bé Tí

Ngày sửa: 22/10/2026.

- Lưu sáu BoxCollider hai bên của cả ba cầu vào scene Map 2. Collider theo trục và tỷ lệ mặt cầu; hai đầu không bị chặn. Hệ thống kiểm tra lại lúc tải Map 2, không sinh collider trùng.
- Bé Tí đứng cạnh chị Lành trong sinh hoạt bình thường, không còn ngồi cạnh Hùng ở trạm trong Ngày 2. Load save cũ tự chuyển bé về cạnh mẹ. Đoạn bé bị lạc và cần cứu ở Đêm 3 vẫn hoạt động; cứu xong bé trở lại cạnh mẹ.
- Unity **4/4 đạt** tại `Logs/Map02Chapter/bridge-child-results.xml`: collider được lưu trong scene, không tạo trùng, CharacterController cùng layer nhân vật bị chặn ở cả hai bên từng cầu, đi được giữa cầu và NavMesh vẫn nối qua cầu; kiểm tra model, vị trí bé trong Ngày 2/Đêm 2, tải save cũ và trạng thái cứu bé ở Đêm 3.

File chính: `Assets/_Project/Scenes/Maps/Map 2.unity`, `Runtime/Map02BridgeRails.cs` và `Runtime/Map02ChapterDirector.cs` trong `Assets/_Project/Map01`.

Dừng Play rồi chạy lại Map 2 để kiểm tra va chạm mới và vị trí nhân vật.
