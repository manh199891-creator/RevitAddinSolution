# Spec v1.0 — Nối centerline tường bị hatch chia cắt bởi cửa

**Vai trò:** Chief Architect  
**Dự án:** RevitAddinSolution  
**Module chính:** `Antigravity.ArchModeling`  
**Trạng thái:** Draft — chờ phê duyệt Phase 1  
**Ngày:** 2026-07-14

## 1. Executive summary

### Goal

Từ nhiều vùng hatch biểu diễn cùng một bức tường nhưng bị cửa/cửa sổ chia cắt, tạo ra **một đường tim liên tục xuyên qua ô mở** để Revit tạo một wall host duy nhất, thay vì hai wall rời.

### Kết luận kiến trúc

Không nên sửa ở `WallFromCadBuilder` hay nối các Revit Wall sau khi tạo. Lỗi nằm ở pipeline nhận dạng hình học: source hiện tại biến từng CAD entity thành một `WallData` riêng bằng axis-aligned bounding box. Giải pháp đúng là thêm một tầng **Wall Centerline Reconstruction** giữa CAD extraction và Revit creation:

1. Đọc loop thật của từng hatch.
2. Chuẩn hóa loop sang mô hình 2D độc lập với Revit API.
3. Trích trục và bề dày của từng vùng hatch bằng oriented geometry.
4. Lập graph các trục có khả năng thuộc cùng một tường.
5. Chỉ bắc cầu qua khoảng hở khi đạt điều kiện hình học và có đủ bằng chứng ô mở.
6. Hợp nhất các interval thành một `Line` hoặc `Arc` dài, lưu provenance và diagnostic.
7. Chỉ sau đó mới chuyển sang `Autodesk.Revit.DB.Curve` và gọi `Wall.Create`.

Khuyến nghị v1 chỉ cam kết **tường thẳng**. Tường cung tròn là extension có kiểm soát ở v1.1; spline/polycurve tổng quát không nên bị ép thành một Revit Wall duy nhất.

## 2. Phạm vi

### IN scope

- Nguồn dữ liệu là hatch được chọn trong AutoCAD qua COM, đúng với pipeline hiện tại.
- Hatch tường thẳng, kể cả tường xoay góc bất kỳ trong mặt phẳng XY.
- Một hoặc nhiều cửa/cửa sổ chia một tường thành nhiều hatch rời.
- Nối nhiều đoạn đồng trục thành một centerline liên tục.
- Phát hiện và từ chối merge mơ hồ.
- Preview/diagnostic: số đoạn đầu vào, số wall sau merge, gap nào đã nối/bị từ chối và lý do.
- Unit test hình học không cần mở Revit/AutoCAD.

### OUT of scope v1

- Tự suy đoán mọi hình dạng tường cong, spline, L-shape hoặc tường có bề dày biến thiên.
- Tự tạo opening nếu không có bước đặt door/window tương ứng.
- Nối xuyên qua junction T/X hoặc qua góc tường.
- Thay thế toàn bộ COM AutoCAD bằng đọc DWG trực tiếp.
- Sửa dữ liệu gốc trong AutoCAD.
- Đồng bộ hai chiều khi CAD thay đổi sau khi đã tạo Revit Wall.

## 3. Phân tích source-code hiện tại

### 3.1. Pipeline đang chạy

Luồng Wall hiện tại là:

`DrawWallFromCadCommand` → `ArchModelingWindow.BtnScanCAD_Click` → `ArchCadInteropService.SelectAndParseByLayer` → `WallData` → `WallFromCadBuilder.BuildWall` → `Wall.Create`.

| File | Hiện trạng | Tác động đến yêu cầu |
|---|---|---|
| `src/Antigravity.ArchModeling/Commands/DrawWallFromCadCommand.cs` | Chỉ mở dialog | Không phải nơi xử lý geometry. |
| `src/Antigravity.ArchModeling/UI/ArchModelingWindow.xaml.cs` | Scan trả về danh sách `WallData`; khi Create lặp từng item và tạo từng Wall | Không có aggregation/preview/confidence; một input luôn dẫn tới một wall. |
| `src/Antigravity.ArchModeling/Services/ArchCadInteropService.cs` | Mỗi entity lấy `GetBoundingBox`; cạnh ngắn là thickness, cạnh dài là centerline; chỉ phân biệt ngang/dọc | Đây là root cause. Hai hatch hai bên cửa tạo hai bounding box và hai centerline. Tường xiên cũng bị suy sai chiều dài/bề dày. |
| `src/Antigravity.ArchModeling/Models/ArchModels.cs` | `WallData` chỉ chứa `Boundaries`, `ThicknessMm`, một `Curve CenterLine` | Thiếu source handle, layer/pattern, confidence, gap, provenance và trạng thái merge. Model phụ thuộc trực tiếp Revit API nên khó unit test thuần. |
| `src/Antigravity.ArchModeling/Services/WallFromCadBuilder.cs` | Nhận một centerline và gọi `Wall.Create`; tự duplicate WallType theo thickness | Lớp này nên tiếp tục chỉ làm Revit creation. Không nên chứa thuật toán nối. |
| `src/Antigravity.ArchModeling/Services/HatchBoundaryService.cs` | Adapter mỏng dùng `Antigravity.DrawFloors.Services.CadInteropService` | Đang chỉ dùng cho Floor/Ceiling, chưa tham gia luồng Wall. Catch rỗng làm mất diagnostic. |
| `src/Antigravity.DrawFloors/Services/CadInteropService.cs` | Đã có `SelectAndParseHatches`, `GetLoopAt`, fallback `HATCHGENERATEBOUNDARY`, polyline/bulge extraction và dựng loose loops | Có tài sản tái sử dụng được, nhưng service quá lớn, gắn chặt UI/COM/Revit curve và có side effect tạo boundary tạm trong CAD. |
| `src/Antigravity.ArchModeling/Services/DoorWindowPlacer.cs` | Tìm wall gần nhất bằng `Curve.Project`; chấp nhận tới 5 feet | Một wall xuyên suốt giúp host cửa đúng, nhưng ngưỡng 5 feet hiện quá rộng và chưa kiểm tra projection nằm trong extents/gần block. |
| `src/Antigravity.Core/Services/CoordinateService.cs` | Chuyển mm → feet và cộng translation offset | Chưa hỗ trợ rotation/scale; mọi phép so sánh nên làm trong CAD-mm trước khi convert để tolerance dễ hiểu và ổn định. |
| `Directory.Build.props` | Mặc định Revit 2024; các project chính dùng .NET Framework 4.8 | Thiết kế phải tương thích net48/Revit 2024 và tránh dependency chỉ hỗ trợ .NET mới. |

### 3.2. Các defect/rủi ro kỹ thuật đã thấy trực tiếp

1. **Axis-aligned bounding box:** `widthMm > heightMm` chỉ đúng cho tường gần ngang/dọc; tường 45° có bounding box gần vuông và thickness sai lớn.
2. **Một entity = một wall:** không có group, topology hay gap bridging.
3. **Mất topology hatch:** `ArchCadInteropService` không đọc outer/inner loop khi scan Wall.
4. **Silent failure:** nhiều `catch { }` làm hatch lỗi biến mất thay vì trả diagnostic cho user.
5. **CAD mutation:** fallback `HATCHGENERATEBOUNDARY` tạo entity mới; code hiện giữ lại entity debug, làm bẩn bản vẽ và lần scan sau có thể đọc nhầm.
6. **Async COM command:** `SendCommand` là luồng phụ thuộc trạng thái document/command line; polling theo `Block.Count` có race condition và khó phân biệt entity do user/add-in khác tạo.
7. **Tolerance rải rác:** source dùng `0.001`, `0.003`, `0.01` feet và `5.0` feet, không có policy thống nhất.
8. **Transaction all-or-nothing:** toàn bộ create chạy trong một transaction; một exception có thể rollback mọi wall dù phần lớn input hợp lệ.
9. **Type mutation heuristic:** duplicate wall type rồi sửa first core layer có thể làm sai tổng bề dày compound structure nhiều lớp.
10. **Thiếu test:** `Antigravity.ArchModeling` chưa có test project cho geometry.

## 4. Research API và hệ quả thiết kế

- Autodesk xác nhận `Hatch.GetLoopAt(index, out loop)` trả về object hoặc mảng object cấu thành loop và `NumberOfLoops` là member chính thức của Hatch ActiveX. Vì vậy **đọc loop trực tiếp phải là đường chính**, không phải tạo boundary tạm bằng command. [Autodesk AutoCAD ActiveX — GetLoopAt](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-LT-ActiveX-Reference/files/GUID-93E21F08-C55A-4D56-9A3B-DEADB29838C0.htm), [Hatch Object](https://help.autodesk.com/cloudhelp/2022/ENU/AutoCAD-ActiveX-Reference/files/GUID-92A2B30F-1B74-4894-850A-5505F3B5944B.htm)
- `HATCHGENERATEBOUNDARY` tạo **non-associated polyline** mới quanh hatch. Đây chỉ nên là fallback có cleanup bắt buộc; không phải API extraction mặc định. [Autodesk — HATCHGENERATEBOUNDARY](https://help.autodesk.com/view/ACDLTM/2025/ENU/?guid=GUID-B441A558-F685-4CE8-950A-C368678B60E1)
- Autodesk cũng ghi nhận boundary generation đôi khi trả các line rời thay vì closed polyline, đặc biệt với 3D line/elevation khác 0. Vì vậy fallback vẫn phải có graph loop builder và coplanarity validation. [Autodesk Support — hatch boundary creates lines](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Generation-of-hatch-boundary-creates-lines-instead-of-closed-polylines.html)
- Revit cung cấp `Curve.Project` và `Curve.Intersect` để đo khoảng cách, phát hiện collinear/overlap; có thể dùng ở adapter Revit, nhưng core stitching nên dùng toán 2D thuần để test được. [Autodesk Revit API — Curve analysis](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Geometry/GeometryObject_Class/Curves/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Geometry_GeometryObject_Class_Curves_Curve_analysis_html.html)
- Revit từ chối tạo curve ngắn hơn tolerance nội bộ, được expose qua `Application.ShortCurveTolerance`. Không được hard-code `0.001/0.003 ft` làm chuẩn hợp lệ cuối cùng. [Autodesk Revit API — Curve creation](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Geometry/GeometryObject_Class/Curves/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Geometry_GeometryObject_Class_Curves_Curve_creation_html.html)
- `Wall.Create(Document, Curve, ...)` chính là API phù hợp cho rectangular-profile wall theo một curve; location line của wall có thể là centerline/core/finish face. Đầu ra reconstruction cần ghi rõ axis đang là centerline nào. [Autodesk Revit API — Walls](https://help.autodesk.com/cloudhelp/2024/CHS/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Walls_Floors_Ceilings_Roofs_and_Openings/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Walls_Floors_Ceilings_Roofs_and_Openings_Walls_html.html)
- Revit hỗ trợ wall line/arc/ellipse ở mức sản phẩm, nhưng elliptical wall có nhiều giới hạn. v1 không nên mở rộng loại curve chỉ vì API nhận base `Curve`. [Autodesk — Best Practices: Elliptical Walls](https://help.autodesk.com/cloudhelp/2022/ENU/Revit-ArchDesign/files/GUID-77AB37AE-41E1-4F22-A42A-4F795566B629.htm)

## 5. Các phương án thuật toán

### Phương án A — Nối theo endpoint distance

Nối hai centerline nếu hai đầu mút gần nhau hơn `MaxGap`.

- Ưu: rất đơn giản, nhanh.
- Nhược: nối nhầm tường song song, khe kỹ thuật, hai phòng độc lập; không sửa lỗi tường xiên; ngưỡng lớn đủ qua cửa sẽ quá nguy hiểm.
- Kết luận: **loại**.

### Phương án B — Union polygon các hatch rồi skeleton/medial axis

Buffer các hatch qua khoảng cửa, union polygon và lấy medial axis/straight skeleton.

- Ưu: tổng quát cho footprint phức tạp.
- Nhược: cần polygon kernel mạnh; skeleton dễ sinh nhánh ở junction/góc; khó đảm bảo một Revit curve; buffer có thể lấp nhầm khe; chi phí dependency và tuning cao.
- Kết luận: **không dùng cho v1**; cân nhắc khi scope thật sự gồm wall network/L-shape.

### Phương án C — Oriented axis + interval graph + evidence-gated bridging

Mỗi hatch được trích một trục cục bộ và thickness; các candidate tương thích được chiếu lên một trục chung thành interval 1D. Gap giữa interval chỉ được lấp nếu đạt geometry gate và evidence gate.

- Ưu: giải đúng use case cửa chia hatch; deterministic, testable, giải thích được; chạy nhanh; không cần thư viện ngoài; hỗ trợ tường xiên.
- Nhược: cần từ chối L-shape/junction; phải có confidence policy.
- Kết luận: **chọn cho v1**.

### Phương án D — Door-first

Đọc block cửa trước, dùng mỗi block làm seed để tìm hai hatch hai phía rồi nối.

- Ưu: false-positive thấp khi CAD block chuẩn.
- Nhược: thất bại khi cửa không phải block hoặc user chỉ chọn hatch.
- Kết luận: dùng như **strong evidence**, không phải pipeline duy nhất.

## 6. Thuật toán đề xuất

### 6.1. Mô hình tọa độ và tolerance

Core geometry làm việc trong **CAD millimetre, XY**, không dùng `XYZ`/feet. Chỉ `RevitCurveFactory` mới convert sang Revit internal unit.

`WallTraceOptions` phải gom toàn bộ tolerance và có preset mặc định; không rải magic number. Giá trị khởi đầu để thử nghiệm, chưa phải contract cuối:

| Tham số | Default khảo sát | Ý nghĩa |
|---|---:|---|
| `AngleToleranceDeg` | 1.0° | Sai khác hướng tối đa, xét modulo 180°. |
| `AxisOffsetToleranceMm` | max(5 mm, 3% thickness) | Khoảng cách vuông góc giữa hai trục. |
| `ThicknessToleranceMm` | max(10 mm, 5% thickness) | Chênh bề dày cho phép. |
| `EndpointSnapToleranceMm` | 2 mm | Nhiễu điểm/loop. |
| `OverlapToleranceMm` | 5 mm | Cho phép interval chồng nhẹ. |
| `MaxOpeningGapMm` | 2400 mm | Chỉ là geometry gate; không tự động merge nếu thiếu evidence. Cần cấu hình theo dự án. |
| `StrongEvidenceMarginMm` | 100 mm | Nới envelope block cửa khi đối chiếu gap. |
| `MinElongationRatio` | 3.0 | Dưới tỷ lệ dài/dày này không tự nhận là wall segment. |
| `MinConfidenceToAutoMerge` | 0.85 | Dưới ngưỡng chuyển sang review, không auto-create. |

Giá trị sau convert sang Revit phải đồng thời lớn hơn `Application.ShortCurveTolerance` cho bước tạo curve.

### 6.2. Bước 1 — Trích hatch loop không gây side effect

Với mỗi `AcDbHatch` được chọn:

1. Lưu `Handle`, `Layer`, `PatternName`, elevation/normal và selection batch id.
2. Gọi `NumberOfLoops` + `GetLoopAt`.
3. Chuyển Line/Arc/Polyline bulge thành primitive 2D; chuẩn hóa thứ tự và chiều loop.
4. Phân outer loop/inner loop bằng diện tích có dấu và containment, không mặc định loop đầu luôn là outer.
5. Validate closed, coplanar, không self-intersect nghiêm trọng.
6. Chỉ khi COM loop không đọc được mới dùng `HATCHGENERATEBOUNDARY`; mọi entity tạm phải được track theo handle và xóa trong `finally`.

Mỗi hatch lỗi tạo một diagnostic record, không bị nuốt bằng catch rỗng.

### 6.3. Bước 2 — Trích axis của từng vùng hatch

Đối với outer polygon đã chuẩn hóa:

1. Tessellate arc chỉ cho mục đích fit/validation; giữ primitive gốc cho output nếu hỗ trợ.
2. Tạo convex hull và oriented bounding rectangle tối thiểu; cạnh dài cho hướng `u`, cạnh ngắn cho normal `v`.
3. Tính các projection `s = dot(point, u)` và `t = dot(point, v)`.
4. Length là `max(s)-min(s)`; thickness là robust span theo `t`; axis sơ bộ đi qua median/mid-span của hai long faces.
5. Kiểm tra các cạnh dài đối diện có song song và separation gần thickness. Nếu không, hạ confidence hoặc reject `IrregularFootprint`.
6. Centerline candidate có hai endpoint tại `sMin/sMax`, direction chuẩn hóa modulo 180°, thickness và extraction confidence.

Oriented rectangle sửa được tường xiên mà bounding box X/Y hiện tại không xử lý. Với footprint chữ L hoặc junction, elongation/residual sẽ không đạt và phải bị từ chối thay vì tạo wall sai.

### 6.4. Bước 3 — Tạo candidate pairs hiệu quả

Không so mọi cặp O(n²). Bucket theo:

- pattern/layer mapping;
- orientation bin;
- thickness bin;
- elevation/plane;
- spatial grid quanh endpoint, cell size xấp xỉ `MaxOpeningGapMm`.

Chỉ các candidate trong bucket lân cận mới đi qua compatibility gate.

### 6.5. Bước 4 — Geometry gate

Hai candidate A/B chỉ có thể nối nếu đồng thời:

1. Cùng CAD semantic group đã map về cùng WallType.
2. Chênh orientation ≤ `AngleToleranceDeg`.
3. Chênh thickness ≤ `ThicknessToleranceMm`.
4. Lateral axis offset ≤ `AxisOffsetToleranceMm`.
5. Hai endpoint “facing” nhau theo trục chung, không phải hai đầu phía ngoài.
6. Axial gap nằm trong `[-OverlapToleranceMm, MaxOpeningGapMm]`.
7. Corridor giữa hai đoạn không cắt candidate tường khác theo hướng xung đột.
8. Pair không đi qua junction T/X được nhận diện từ endpoint graph.

Sau gate, fit một common axis có trọng số theo chiều dài; project A/B lên trục đó thành interval `[sMin, sMax]`.

### 6.6. Bước 5 — Evidence gate và confidence

Mức bằng chứng:

| Evidence | Điểm định hướng | Chính sách |
|---|---:|---|
| Block door/window có tâm nằm trong gap envelope, rotation phù hợp và kích thước hợp lý | Rất mạnh | Có thể auto-merge nếu geometry pass. |
| Hai jamb/cạnh đứng đối diện tạo opening rectangle, gap thuộc range đã cấu hình | Mạnh | Có thể auto-merge. |
| Hai hatch cùng source group, trục/bề dày khớp rất chặt, gap nhỏ | Trung bình | Chỉ auto-merge nếu project bật `AllowGeometryOnlyMerge`; mặc định đưa review. |
| Chỉ gần endpoint | Yếu | Không merge. |

Confidence không nên chỉ là tổng điểm tùy ý. Phải lưu từng feature: angle delta, thickness delta, lateral offset, gap, evidence type và rejection reason để QA truy vết.

### 6.7. Bước 6 — Graph merge có kiểm tra transitive

Mỗi candidate là một node; mỗi pair qua gate là một edge. Lấy connected component, nhưng không merge mù theo tính bắc cầu:

1. Re-fit common axis cho toàn component.
2. Revalidate mọi member với common axis.
3. Sort các interval theo `s`.
4. Kiểm tra từng gap liên tiếp đều có edge/evidence hợp lệ.
5. Nếu một edge yếu hoặc junction xuất hiện, cắt component tại đó.
6. Output từ `min(s)` đến `max(s)` trên common axis.

Kết quả giữ `SourceHandles`, danh sách `BridgedGaps`, confidence thấp nhất và diagnostics. Nhờ vậy ba hatch A–B–C không bị merge toàn bộ chỉ vì A gần B và B gần C trong khi C đã lệch trục.

### 6.8. Bước 7 — Tạo Revit curve và wall

- Với linear component: tạo đúng một `Line.CreateBound(start, end)`.
- Với arc extension v1.1: chỉ merge khi center, radius, normal và direction tương thích; union angular intervals rồi tạo một Arc mới.
- Mỗi merged result tạo một Wall qua `WallFromCadBuilder`.
- Set `WALL_KEY_REF_PARAM`/location line nhất quán với cách trích axis (mặc định Wall Centerline).
- Door/window được đặt sau wall để family host cắt opening. Nếu không có door creation trong cùng workflow, kết quả đúng về wall continuity nhưng mô hình sẽ chưa có opening.

## 7. Kiến trúc hệ thống đề xuất

Luồng trách nhiệm:

`CAD COM adapter` → `Normalized 2D loops` → `Wall axis extraction` → `Opening evidence` → `Stitch graph` → `Review/diagnostics` → `Revit curve adapter` → `Wall builder`.

### Nguyên tắc

- **Functional core, imperative shell:** thuật toán 2D là pure C#; COM/Revit chỉ ở biên.
- **Không phụ thuộc ngược:** `ArchModeling` không nên gọi một service Floor khổng lồ để xử lý Wall lâu dài.
- **Fail closed:** input mơ hồ bị skip/review, không tự nối.
- **Provenance-first:** mọi wall output biết được các hatch handle nguồn và gap đã bắc cầu.
- **Không mutate CAD:** đường chính read-only; fallback phải cleanup.

## 8. File cần tạo/sửa

### Tạo mới

| File | Trách nhiệm |
|---|---|
| `src/Antigravity.ArchModeling/Geometry/Point2.cs` | Vector/point 2D theo mm, toán dot/cross/projection; không tham chiếu Revit. |
| `src/Antigravity.ArchModeling/Geometry/Curve2.cs` | Primitive Line/Arc/Polyline 2D chuẩn hóa. |
| `src/Antigravity.ArchModeling/Models/HatchWallRegion.cs` | Loop, handle, layer, pattern, plane và extraction diagnostics. |
| `src/Antigravity.ArchModeling/Models/WallAxisCandidate.cs` | Axis, interval, thickness, residual, confidence, provenance. |
| `src/Antigravity.ArchModeling/Models/WallStitchResult.cs` | Merged axis, bridged gaps, source handles, confidence/status. |
| `src/Antigravity.ArchModeling/Models/WallTraceOptions.cs` | Toàn bộ tolerance/policy có đơn vị rõ ràng. |
| `src/Antigravity.ArchModeling/Services/CadHatchBoundaryReader.cs` | Đọc ActiveX hatch loop; fallback boundary có cleanup; không dựng Wall. |
| `src/Antigravity.ArchModeling/Services/WallAxisExtractor.cs` | Oriented rectangle/long-edge fit và reject footprint bất thường. |
| `src/Antigravity.ArchModeling/Services/OpeningEvidenceCollector.cs` | Thu block cửa/cửa sổ và/hoặc jamb evidence theo selection batch. |
| `src/Antigravity.ArchModeling/Services/WallCenterlineStitcher.cs` | Bucket, compatibility gate, graph, interval union và diagnostics. |
| `src/Antigravity.ArchModeling/Services/RevitCurveFactory.cs` | Convert mm-2D → feet/XYZ; kiểm `ShortCurveTolerance`; tạo Line/Arc hỗ trợ. |
| `src/Antigravity.ArchModeling.Tests/Antigravity.ArchModeling.Tests.csproj` | Test project net48, không cần chạy Revit nếu core không tham chiếu Autodesk type. |
| `src/Antigravity.ArchModeling.Tests/WallAxisExtractorTests.cs` | Test horizontal/vertical/rotated/noisy/irregular hatch. |
| `src/Antigravity.ArchModeling.Tests/WallCenterlineStitcherTests.cs` | Test merge/reject, graph transitive, multi-opening, junction. |
| `src/Antigravity.ArchModeling.Tests/Fixtures/*.json` | Geometry fixture đã ẩn thông tin dự án, theo mm. |

### Sửa

| File | Thay đổi dự kiến |
|---|---|
| `src/Antigravity.ArchModeling/Services/ArchCadInteropService.cs` | Bỏ path Wall dựa trên bounding box; điều phối reader và trả normalized regions/evidence. Block parsing có thể giữ lại. |
| `src/Antigravity.ArchModeling/Models/ArchModels.cs` | Thu hẹp `WallData` thành DTO creation hoặc thay bằng `WallStitchResult`; không để core model giữ `Autodesk.Revit.DB.Curve`. |
| `src/Antigravity.ArchModeling/Services/WallFromCadBuilder.cs` | Nhận merged result/curve đã validate; trả structured creation result thay vì `null`; không tự stitch. |
| `src/Antigravity.ArchModeling/UI/ArchModelingWindow.xaml` | Thêm Wall Trace settings tối thiểu, checkbox geometry-only, cột Merged/Review/Error và Preview/Details. |
| `src/Antigravity.ArchModeling/UI/ArchModelingWindow.xaml.cs` | Điều phối scan → extract → stitch → preview; chỉ create result Approved; hiển thị diagnostics. |
| `src/Antigravity.ArchModeling/Services/DoorWindowPlacer.cs` | Dùng opening evidence/merged wall mapping; thay ngưỡng 5 ft bằng policy mm và kiểm tra projection/extents. |
| `src/Antigravity.ArchModeling/Services/HatchBoundaryService.cs` | Không dùng cho Wall; về sau chuyển sang shared boundary reader hoặc giữ adapter Floor tạm thời. |
| `src/Antigravity.ArchModeling/Antigravity.ArchModeling.csproj` | Thêm testable architecture/dependency nếu cần; ưu tiên không thêm geometry package ở v1. |
| `Antigravity.sln` | Đăng ký `Antigravity.ArchModeling.Tests`. |

### Không nên sửa trong v1

- `DrawWallFromCadCommand.cs`: command shell đã đủ.
- `Antigravity.Main/App.cs`: ribbon command không đổi.
- `Antigravity.DrawFloors/Services/CadInteropService.cs`: tránh gây regression Floor trong cùng change. Sau khi v1 ổn định mới lập task tách shared CAD boundary reader và migrate Floor.

## 9. UX tối thiểu

Không cần redesign dialog. Với Wall mode, thêm:

- `Bridge openings` mặc định bật.
- `Use door/window blocks as evidence` mặc định bật.
- `Allow geometry-only merge` mặc định tắt.
- `Maximum opening width (mm)` có preset theo dự án.
- Summary: `12 hatch regions → 7 walls; 5 gaps bridged; 2 need review`.
- Mỗi row có status `Ready`, `Review`, `Rejected`; Details cho biết cụ thể “gap 900 mm, angle 0.2°, door block D01”.

Preview là control an toàn quan trọng, không chỉ là tiện ích UI. Nếu chưa có temporary graphics ổn định, v1 có thể bắt đầu bằng bảng diagnostic và highlight CAD handle; không được auto-create các case `Review`.

## 10. Rủi ro và mitigation

| Rủi ro | Mức | Mitigation / quyết định |
|---|---|---|
| Nối nhầm qua khe không phải cửa | Rất cao | Evidence gate; geometry-only mặc định tắt; max gap cấu hình; preview. |
| Tường xiên bị sai thickness | Cao, đang tồn tại | Oriented rectangle/long-edge fit; fixture 15°, 30°, 45°, 89°. |
| L/J/T/X shape sinh trục giả | Cao | Elongation/residual gate; junction detection; fail closed. |
| Hatch loop hỏng/non-associative/3D | Cao | GetLoopAt trước; coplanarity validation; fallback isolated + cleanup; diagnostic. |
| COM `SendCommand` race/reentrancy | Cao | Không dùng trong primary path; lock selection batch; track handles; cleanup `finally`. |
| Tạo wall liền nhưng không có opening | Cao | Workflow wall-first rồi door placement; cảnh báo nếu bridged gap không có family sẽ cắt host. |
| Door host nhầm wall | Cao | Map opening evidence → stitch result → created ElementId; không global nearest-wall 5 ft. |
| Tolerance không phù hợp scale/drawing quality | Cao | Tất cả option theo mm; preset theo dự án; diagnostic feature values; corpus thực tế. |
| Merge bắc cầu dây chuyền quá mức | Trung bình-cao | Re-fit và revalidate toàn component; split tại từng weak edge. |
| Performance nhiều nghìn hatch | Trung bình | Orientation/thickness/spatial bucket; mục tiêu gần O(n log n + k), tránh O(n²). |
| WallType duplicate sai compound layers | Trung bình | Tách khỏi stitching; ưu tiên map exact type; nếu resize phải có policy và test riêng. |
| Regression Floor do shared service | Trung bình | Không sửa DrawFloors trong v1; contract test trước khi refactor shared extraction. |
| Khác version Revit | Trung bình | Baseline Revit 2024/net48; adapter mỏng; smoke test đúng Revit target. |
| Origin offset có rotation/scale | Trung bình | Core làm CAD coordinates; ghi rõ translation-only hiện tại; rotation/scale là feature riêng. |
| Silent data loss | Cao, đang tồn tại | Structured result + rejection reason; cấm catch rỗng trong path mới. |

## 11. Acceptance criteria

### Functional

1. Hai hatch chữ nhật đồng trục, cách 900 mm, có block cửa hợp lệ → một centerline từ đầu hatch A tới cuối hatch B.
2. Ba hatch cùng trục với hai cửa → một centerline, hai bridged gaps được ghi lại.
3. Tường xoay 45° → thickness và endpoints trong tolerance, không bị biến thành ngang/dọc.
4. Hai hatch gần nhau nhưng lệch trục quá ngưỡng → không merge.
5. Hai hatch đồng trục, gap dưới max nhưng không có evidence và geometry-only tắt → `Review`, không auto-create.
6. Hai đoạn tạo junction T → không nối xuyên tùy tiện.
7. Mixed thickness vượt tolerance → tách wall.
8. Một input loop lỗi không làm mất các result hợp lệ khác và phải có rejection reason.
9. Sau create và place door, door host là wall được tạo từ stitch result tương ứng.
10. Không để lại polyline/line tạm trong CAD sau scan thành công, lỗi hoặc cancel.

### Non-functional

- Core tests chạy không cần Revit/AutoCAD process.
- 1.000 hatch đơn giản hoàn tất reconstruction trong mục tiêu ≤ 2 giây trên workstation chuẩn, chưa tính thời gian COM selection.
- Không có tolerance không tên trong thuật toán mới.
- Không có catch rỗng trong primary extraction/stitching path.
- Mỗi output/reject đều truy được về CAD handles nguồn.

## 12. Kế hoạch kiểm chứng dữ liệu

Trước implementation production, cần một corpus đã ẩn danh tối thiểu:

- 10 tường thẳng ngang/dọc;
- 10 tường xiên;
- 10 tường có một cửa;
- 5 tường nhiều cửa;
- 5 case cửa đôi/gap lớn;
- 10 negative cases: khe kỹ thuật, hai tường độc lập, T/X junction, L-shape, thickness đổi, hatch hỏng.

Với mỗi case lưu expected grouping, expected axis, thickness và gap classification. Dùng fixture JSON cho unit test; dùng một DWG/RVT integration fixture riêng cho smoke test COM + Revit.

## 13. Open questions cần phê duyệt

1. Nguồn thật có đúng là **AutoCAD `AcDbHatch` qua COM** hay còn gồm hatch/filled region trong Revit và CAD ImportInstance?
2. CAD cửa/cửa sổ có block chuẩn, và có thể chọn cùng lúc với hatch không? Nếu có, tên layer/block/dynamic width có convention nào?
3. Khoảng mở tối đa theo dự án là bao nhiêu: 1.200, 2.400 hay lớn hơn cho cửa đôi/cửa cuốn?
4. Có cho phép auto-merge khi không có block cửa, chỉ dựa hình học không?
5. v1 có bắt buộc hỗ trợ tường cung tròn không, hay tường thẳng là đủ?
6. Khi một gap được bridge nhưng chưa map được door family, workflow phải chặn tạo wall hay cho tạo và cảnh báo?
7. Revit hỗ trợ mục tiêu chỉ 2024 hay cần 2022–2025/2026?

## 14. Quyết định đề nghị phê duyệt

- Chọn Phương án C, kết hợp door-first evidence từ Phương án D.
- v1: linear walls, CAD hatch qua COM, fail-closed, geometry-only merge mặc định tắt.
- Giữ `WallFromCadBuilder` là creation adapter; thuật toán mới nằm trong pure 2D core.
- Không sửa module DrawFloors trong change đầu để hạn chế regression.
- Chỉ chuyển sang Phase 2 sau khi trả lời các open questions và duyệt spec này.
