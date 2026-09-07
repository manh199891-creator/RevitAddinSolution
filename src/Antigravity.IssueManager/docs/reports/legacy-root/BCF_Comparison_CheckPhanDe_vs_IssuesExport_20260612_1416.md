# So sánh BCF: file khối đế tốt vs file export lỗi

Ngày kiểm tra: 2026-06-12

## File được so sánh

| Nhóm | File | Dung lượng | Kết quả trong Trimble Connect |
|---|---:|---:|---|
| Tốt | `E:\GMD\BCF\Check phần đế_20260601_1519.bcfzip` | 2,941,681 bytes | View 3D hiện model |
| Lỗi | `E:\GMD\BCF\Issues_Export_20260612_1416.bcfzip` | 296,496 bytes | Snapshot hiện, View 3D chỉ thấy clip plane/scissor, model không hiện ổn định |

## Tổng quan cấu trúc

| Hạng mục | File tốt | File lỗi | Nhận xét |
|---|---:|---:|---|
| Số topic | 63 | 16 | File lỗi ít topic hơn, nhưng không phải nguyên nhân chính gây blank. |
| Snapshot trung bình | ~46,937 bytes | ~18,030 bytes | Snapshot lỗi nhỏ hơn nhiều, cho thấy vùng nhìn/hình xuất hẹp hoặc ít thông tin hơn. |
| Mỗi topic có `markup.bcf` | Có | Có | Đúng chuẩn. |
| Mỗi topic có `viewpoint.bcfv` | Có | Có | Đúng chuẩn. |
| Mỗi topic có `snapshot.png` | Có | Có | Đúng chuẩn. |
| `Comment/Viewpoint Guid` khớp `Viewpoints Guid` | Có | Có | Đúng, Trimble đọc được thumbnail. |
| XML encoding | UTF-8 | UTF-8 | Không thấy lỗi encoding. |

## So sánh mẫu topic

| Hạng mục | File tốt: `Clash58` | File lỗi: `VV-Tầng 1-Kiểm tra các chi tiết thừa` | Đánh giá |
|---|---|---|---|
| Topic folder | `3FF01771-4159-4132-B6BE-995B7F3C4CE5` | `3CB0553A-274A-4917-9E91-AC68D62F4A97` | Đều hợp lệ. |
| Header/File/Filename | Rỗng: `<Header />` | `LC1.1-IBST-ZZ-0003_04-M3-A-0001-TE.rvt` | File tốt vẫn hiện model dù thiếu filename, nên đây không phải nguyên nhân duy nhất. |
| Cảnh báo Trimble | Missing related model filename | Có thể báo missing/relink model | Cảnh báo này không nhất thiết làm model blank. |
| Camera type | `PerspectiveCamera` | `OrthogonalCamera` | Khác biệt lớn. Trimble đang ổn định hơn với perspective từ file tốt. |
| `Components` node | Không có | Có: `<Components><Visibility DefaultVisibility="true" /></Components>` | Khác biệt lớn. Với Trimble, `Components` rỗng có thể làm viewpoint xử lý visibility không giống file tốt. |
| Selection component | Không ghi trong raw XML | Không có selection trong raw XML mẫu | Không có selection thật để Trimble focus/highlight. |
| Clipping planes | 6 | 6 | Đều có section box. |
| Clip plane direction | Có cặp đối xứng, không thấy bị thiếu | Đã được export hướng đối xứng sau bản sửa | Không còn là lỗi chính trong file `1416`. |
| Snapshot size mẫu | 155,038 bytes | 24,962 bytes | Snapshot lỗi nhỏ hơn nhiều. |
| BCF Viewpoint Guid | Khớp markup | Khớp markup | Đúng. |

## Kết luận kỹ thuật

| Mức ưu tiên | Khác biệt | Khả năng gây lỗi | Hướng xử lý đề xuất |
|---:|---|---|---|
| 1 | File lỗi ghi `OrthogonalCamera`, file tốt dùng `PerspectiveCamera` | Cao | Thêm tùy chọn/export mặc định sang `PerspectiveCamera` tương thích Trimble, hoặc convert orthogonal sang perspective khi xuất. |
| 2 | File lỗi ghi `Components` rỗng, file tốt không ghi `Components` | Cao | Không ghi `<Components>` nếu không có selection IFC hợp lệ. Tránh để Trimble áp visibility rỗng. |
| 3 | Snapshot file lỗi nhỏ hơn nhiều | Trung bình | Kiểm tra vùng crop/section box khi tạo issue; snapshot hiện được nhưng view 3D có thể đang focus sai vùng. |
| 4 | Header model filename khác nhau | Thấp đến trung bình | File tốt vẫn hiện model dù Header rỗng. Chỉ nên ghi filename khi chắc chắn trùng tên model đang mở trong Trimble. |
| 5 | Clip planes | Thấp trong file `1416` | Bản export mới đã lật hướng clip plane ra ngoài; tiếp tục giữ kiểm tra tự sửa lúc export. |

## Đề xuất thay đổi tiếp theo

| Việc cần làm | Lý do |
|---|---|
| Bỏ ghi `Components` khi không có IFC Guid hợp lệ | Khớp với file tốt và giảm rủi ro Trimble ẩn model theo visibility. |
| Cho phép export `PerspectiveCamera` từ Revit current view hoặc convert khi xuất | Khớp với file tốt khối đế, giảm lỗi Trimble không render model với orthogonal + clipping planes. |
| Chỉ ghi `Header/File/Filename` khi người dùng chọn đúng file model Trimble đang mở | Tránh cảnh báo relink sai model. |
| Tạo thêm script kiểm tra BCF sau export | Tự báo các topic có camera/clip/components bất thường trước khi upload Trimble. |

## Kiểm tra bổ sung: vì sao NWC/model tự ẩn trong file mới

File mới kiểm tra: `E:\GMD\BCF\Issues_Export_20260612_1448.bcfzip`.

| Hạng mục | File khối đế tốt | File mới `1448` | Kết luận |
|---|---:|---:|---|
| Số topic | 63 | 16 | Không phải nguyên nhân chính. |
| Topic dùng `PerspectiveCamera` | 63/63 | 16/16 | Camera đã giống file tốt sau bản sửa trước. |
| Topic có `<Components>` | 0/63 | 8/16 | Đây là khác biệt chính còn lại. |
| Topic có `<Visibility>` | 0/63 | 0/16 | Không còn tag visibility trực tiếp. |
| Topic có `<Selection>` | 0/63 | 8/16 | Trimble có thể dùng selection để isolate/focus component và làm các model NWC khác bị ẩn. |
| Topic có `DefaultVisibility="false"` | 0/63 | 0/16 | Không phải nguyên nhân. |

Ví dụ raw XML trong file mới:

```xml
<Components>
  <Selection>
    <Component IfcGuid="1D479GYFDEz8qdvFfgF27s" AuthoringToolId="24639977">
      <OriginatingSystem>Autodesk Revit</OriginatingSystem>
    </Component>
  </Selection>
</Components>
```

File khối đế tốt không ghi block này. Vì vậy thay đổi code mới nhất là bỏ hẳn `Components/Selection` khi export BCF cho Trimble Connect, để tránh Trimble tự isolate component và ẩn các NWC/model khác.
