# Nguồn Blender hầm kho

- `build_store.py`: dựng toàn bộ model, texture, xuất FBX đóng nắp và render hai góc.
- `NightStore.blend`: nguồn chỉnh sửa, đơn vị mét.
- `blender-cutaway.png`, `blender-interior.png`: render từ model Blender thực.
- `integrate_assets.py`: chuyển prefab và metadata đã kiểm tra từ project xác thực sang project game, ghi vật liệu URP từ mẫu hiện có.
- `validate.cs`: kiểm tra Play Mode với capsule, rig Nam, địa hình và model thật.

Dựng lại từ thư mục project:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python SourceArt/Map02UndergroundStore/build_store.py
```

Trong Unity chính, chạy `ShadowVale > Map 2 > Build Blender underground store` để tạo lại prefab và vật liệu URP.
Model không chứa script điều khiển hay camera/light preview trong FBX. Nắp được xuất đóng với pivot tại cạnh dài; script runtime điều khiển pivot riêng theo hệ trục Unity.
