# Phát hiện trong kiểm tra dev và ghép thử

Không đánh đồng lỗi test với lỗi game. Các kết quả gốc vẫn giữ trong XML và log.

| Phát hiện | Bằng chứng | Xử lý |
|---|---|---|
| Dev: hai test đòi tự chạy cutscene đầu map | `dev-gameplay.xml`, hai ca trong `OpeningCutsceneTests`; hai test luồng E sẵn có của dev đạt trong `dev-manual-opening.xml` | Giữ luồng gặp chỉ huy và bấm E đã được người dùng chốt; bản ghép dùng bộ test E riêng của thinh. |
| Dev: tay trái chỉ huy mới cách marker đỡ súng 0,111 m | `grip-diagnostics.txt`, ảnh `grip-before.png` | Gắn báng cầm vào lòng bàn tay theo mesh; đặt điểm đỡ trên ốp gỗ thật; đưa vị trí súng vào tầm với của hai tay. Giữ nguyên mesh/rig và góc nòng. |
| Ca rút quân tự nhiên trước đó đòi cả hai người cùng bắn sau khi đã hạ toàn bộ lính trên bờ | `pursuit-diagnostics.txt`, `cover-diagnostics.txt` trong Logs/Extraction của worktree dev: chỉ huy thấy địch, Nam bị bến chắn | Không cho bắn xuyên bến. Ca này kiểm tra ít nhất một thành viên có đường bắn thực hiện yểm trợ. Ca `SeatedCoverKillsExistingGuardsWithoutSpendingAmmoOrAddingLoot` vẫn đòi cả Nam và chỉ huy bắn khi địch có đường ngắm. Ba ca kiểm lại đạt trong `dev-extraction-fixed-r3.xml`. |
| Bản ghép: lớp giới hạn C3 thiếu `Velocity`/`FacesManually` của `IEnemyBody` mới | `candidate-pure.log`, lỗi CS0535 | Chuyển tiếp hai thuộc tính sang body thật, giữ bộ lọc hàng rào. Chạy lại: 83/83 đạt. |
| Bản ghép: Mosin truy cập nhiệm vụ Map 1 đã bị hủy khi chuyển Map 2 | `candidate-gameplay.log`, `Map01Mosin.Update` | Ngắt driver vũ khí gắn với Map 1 trong `ReleaseForNextMap`, trả quyền pose/scope cho đoạn chuyển cảnh. Thêm kiểm tra lifecycle vào test chuyển map. |
| Test Mosin cũ gọi Attack trên combat đã tắt; dev giờ xếp hitscan tới cuối frame | Các ca Mosin trong `candidate-gameplay.xml`; ammo giảm nhưng damage chưa resolve | Fixture tiếp tục đóng băng input, nhưng bật combat trong lúc gọi và flush đúng hàng đợi thật của dev. Không thay đường đạn game thành đồng bộ để đáp ứng test cũ. |
| Test ảnh Mosin coi chớp nòng tắt là model mất renderer | `MosinIntegrationTests.PUScope...` | Kiểm tra các renderer thân súng, bỏ renderer của `MuzzleFlash` khỏi yêu cầu luôn hiện. Chớp nòng chỉ hiện khi bắn. |
| Test kẻng gọi reflection không truyền tham số mới | `C3MissionTests.TheKiengWakes...`, TargetParameterCountException | Truyền đúng `fight=true`, `search=null` tương đương giá trị mặc định, không đổi luật thức. |
| Test vật ném cũ nhấn Q và đòi rút súng ngay khi hủy ngắm | Hai ca vật ném lỗi trong `candidate-gameplay.xml` | Giả lập chuột trái theo input dev; hủy ngắm giữ vật ném đã chọn, rút súng bằng slot; vẫn kiểm tra chỉ tốn một vật phẩm, đạn không tự đổi và vòng an toàn khi đang ném. |
| Giả lập chuột giữ trạng thái bằng `InputState.Change` ở cả Editor/Dynamic nhưng cache nút nhấn không khớp | `SupplyRequires...`: giá trị nút 1 nhưng `isPressed=false` | Bơm sự kiện chuột bình thường trước update; cách test nhận nút theo cùng đường sự kiện như input thật. |
| Test đứng yên trong thoại sai đúng 0,0000013 m so với ngưỡng 1 cm | `SeparateEPresses...` | Giữ kiểm tra đứng yên, cho phép 2 cm hiệu chỉnh tiếp đất; không đổi vị trí hay tốc độ gameplay. |
| Đổi AK sau khi bóp cò Mosin nhưng trước cuối frame khiến damage bị gắn nhãn Rifle | `candidate-queued-before.xml`: test mới tái hiện đúng Sniper→Rifle | `PlayerCombat` truyền loại khẩu đã xếp hàng vào `Health`; giữ API damage bốn tham số hiện có. Ca mới và các ca bắn Mosin/K54 kiểm lại đều đạt trong `candidate-final-fixes.xml`. |
| Fixture xóa khóa lưu trước khi Play Mode/Editor thoát xong, gây ghi ô auto thật | Hash ô auto khác snapshot, trong khi ô 0/1 nguyên vẹn | Bổ sung fallback bắt buộc vào thư mục Temp riêng cho batch editor, thêm regression kiểm đường lưu khi khóa đã xóa. Đã giữ bản auto do test tạo trong Temp, khôi phục auto gốc và kiểm hash ba slot khớp snapshot (`save_verification.json`). |

Fixture giữ chuột của luồng tiếp tế vẫn chịu soft-reset do editor/cutscene sau cách bơm sự kiện ban đầu. Bản cuối thiết lập trạng thái ngắm bằng setter sản xuất cho riêng test hủy ngắm khi mở puzzle; `AimToggleIntegrationTests` vẫn kiểm chuột phải thật. Fixture K54 flush đúng hàng đợi phát đạn, và test ném gửi cả sự kiện nhả chuột trước khi rút súng. Không bỏ các kiểm tra sát thương, hao đạn, vật phẩm hay input tương ứng.

Tham khảo kỹ thuật về chuỗi hai khớp, target, hint và trọng số: [Unity — Two Bone IK](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html). Dùng nguyên tắc đặt mục tiêu/định hướng khuỷu cho bộ giải sẵn có; không thêm package hoặc xương.

Ảnh là cảnh Unity thử riêng. Số đo tiếp xúc không thay thế đánh giá tạo hình: tư thế và độ khép ngón tay vẫn theo asset gốc; lượt ghép không dựng lại mesh hay diễn xuất ngón tay.
