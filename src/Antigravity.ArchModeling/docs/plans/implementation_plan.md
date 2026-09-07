# Kế hoạch Triển khai: Công cụ Dựng hình Kiến trúc từ CAD

> **Phiên bản cập nhật** — dựa trên phản hồi user và phân tích source code `DrawFloors`, `DrawWalls` hiện có.

---

## Bối cảnh & Quyết định Kỹ thuật

Dựa trên nghiên cứu source code và thông tin thực tế từ file CAD của bạn:

| Cấu kiện | Nguồn dữ liệu CAD | Logic nhận diện |
|----------|-------------------|-----------------|
| **Tường** | Layer CAD (vd: `A-WALL-PATT`) | Tên layer → map tự động sang `WallType`. Bề dày tường lấy từ dimension bounding-box của polyline/hatch. |
| **Cửa đi / Cửa sổ** | Block Reference có tên (vd: `A-DOOR`, `1 CUA DI 2 CANH - D2`) | Tên block → map sang `FamilySymbol`. Vị trí = insertion point. Góc xoay = block rotation. |
| **Sàn hoàn thiện / Trần** | Hatch pattern theo layer | **Tái dùng toàn bộ logic `AutoHatch` từ `CadInteropService`** trong `DrawFloors`. Tên hatch pattern → map sang `FloorType`/`CeilingType`. |

> [!IMPORTANT]
> **Tái sử dụng tối đa:** `CadInteropService.cs` (DrawFloors) đã có đầy đủ logic:
> - `SelectAndParseHatches()` → quét hatch theo pattern
> - `ExtractHatchBoundaries()` → trích xuất CurveLoop từ hatch
> - `BuildLoopsFromLooseCurves()` → nối các curve rời thành loop khép kín
> 
> Module `ArchModeling` mới sẽ **kế thừa/tham chiếu** service này, KHÔNG viết lại.

---

## Kiến trúc Module Mới: `Antigravity.ArchModeling`

```
src/Antigravity.ArchModeling/
├── Antigravity.ArchModeling.csproj
├── Commands/
│   ├── DrawWallFromCadCommand.cs     ← IExternalCommand (entry)
│   ├── PlaceDoorFromCadCommand.cs    ← IExternalCommand (entry)
│   └── DrawFloorCeilFromCadCommand.cs ← IExternalCommand (entry)
├── Services/
│   ├── ArchCadInteropService.cs      ← Wrapper đọc Layer + Block từ CAD (mới)
│   ├── WallFromCadBuilder.cs         ← Logic tạo Wall (kế thừa DrawWalls)
│   ├── DoorWindowPlacer.cs           ← Logic đặt Door/Window Family Instance
│   └── HatchBoundaryService.cs       ← Thin wrapper gọi DrawFloors.CadInteropService
└── UI/
    ├── ArchModelingWindow.xaml        ← Main window (Vilaiviet dark theme)
    ├── ArchModelingWindow.xaml.cs
    └── LayerMappingWindow.xaml        ← Sub-dialog mapping Layer → Revit Type
```

---

## Proposed Changes

---

### 1. Module mới: `Antigravity.ArchModeling`

#### [NEW] src/Antigravity.ArchModeling/Antigravity.ArchModeling.csproj
- Target: `net8.0-windows` (Revit 2025+)
- Reference: `Antigravity.Core`, `Antigravity.DrawFloors` (để tái dùng `CadInteropService`)

---

#### [NEW] src/Antigravity.ArchModeling/Services/ArchCadInteropService.cs
**Trách nhiệm:** Đọc dữ liệu từ AutoCAD (qua COM Interop), phân loại theo layer.

Key methods:
```csharp
// Quét selection set → trả về dict [LayerName → List<WallData>]
Dictionary<string, List<WallData>> SelectAndParseByLayer(string prompt)

// Đọc tất cả Block References → dict [BlockName → List<BlockInfo>]
Dictionary<string, List<BlockInfo>> SelectAndParseBlocks(string prompt)

// Model data:
record BlockInfo(double X, double Y, double RotationRad, string BlockName, string LayerName)
```

---

#### [NEW] src/Antigravity.ArchModeling/Services/WallFromCadBuilder.cs
**Trách nhiệm:** Tạo Revit Wall từ `WallData` + mapping table.

Logic (cải tiến từ `DrawWalls.RevitWallBuilder`):
- Thay vì bounding-box đơn giản → **dùng cả hai nét song song** nếu layer có hatch/polyline đôi.
- Hàm `GetOrCreateWallType(string layerName, double thicknessMm)`: tra cứu bảng mapping `LayerName → WallType`.

---

#### [NEW] src/Antigravity.ArchModeling/Services/DoorWindowPlacer.cs
**Trách nhiệm:** Đặt `FamilyInstance` (cửa đi, cửa sổ) từ `BlockInfo`.

Key method:
```csharp
// Tìm Wall gần nhất làm Host, đặt FamilyInstance tại insertion point
FamilyInstance PlaceDoorOrWindow(
    Document doc, BlockInfo block,
    Dictionary<string, FamilySymbol> blockToSymbolMap,
    Level targetLevel)
```

Logic:
1. Lấy `insertion point` từ `BlockInfo`.
2. Tìm `Wall` gần nhất (ray-cast hoặc `GetElementNearest`).
3. Gọi `doc.Create.NewFamilyInstance(XYZ, FamilySymbol, hostWall, StructuralType.NonStructural)`.
4. Áp dụng `rotation` từ block.

---

#### [NEW] src/Antigravity.ArchModeling/Services/HatchBoundaryService.cs
**Trách nhiệm:** Thin wrapper — gọi lại `DrawFloors.CadInteropService` để trích xuất boundary từ hatch cho Sàn/Trần.

```csharp
// Quét hatch → trả về dict [PatternName → List<List<Curve>>]  
Dictionary<string, List<List<Curve>>> GetHatchBoundaries()
```

---

#### [NEW] src/Antigravity.ArchModeling/UI/ArchModelingWindow.xaml
UI theo chuẩn **Vilaiviet dark theme** (giống `DrawFloors/MainWindow.xaml`):
- Background `#202226`, font `Segoe UI`
- Header: Logo Vilaiviet ▼ + tiêu đề `VILAIVIET · ARCH MODELING`
- Panel `Set Coordinate Origin (CAD ↔ Revit)` (tái dùng nút giống DrawFloors)
- Tabs hoặc Section buttons:
  - **Tường** — Layer list + WallType map
  - **Cửa/Cửa sổ** — Block list + FamilySymbol map
  - **Sàn/Trần** — Hatch pattern list + FloorType/CeilingType map
- Buttons: `🔄 Scan CAD`, `✅ Create Elements`, `❌ Cancel`

---

#### [NEW] src/Antigravity.ArchModeling/Commands/DrawWallFromCadCommand.cs
```csharp
[Transaction(TransactionMode.Manual)]
public class DrawWallFromCadCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var window = new ArchModelingWindow(commandData.Application, ArchModelingMode.Wall);
        window.Show();
        return Result.Succeeded;
    }
}
```

---

### 2. Cập nhật Ribbon (`Antigravity.Main`)

#### [MODIFY] src/Antigravity.Main/ (App.cs hoặc file ribbon registration)
Thêm 3 PushButton vào panel **`DỰNG HÌNH`** hiện có:

| Tên nút | Icon | Command |
|---------|------|---------|
| Vẽ Vách | 🧱 | `DrawWallFromCadCommand` |
| Vẽ Sàn HT | 🟦 | `DrawFloorCeilFromCadCommand` |
| Đặt Cửa | 🚪 | `PlaceDoorFromCadCommand` |

---

## Verification Plan

### Automated Tests
```bash
# Unit test: DoorWindowPlacer, WallFromCadBuilder (mock Document)
dotnet test src/Antigravity.ArchModeling.Tests/
```

### Manual Verification
1. Mở Revit + AutoCAD cùng lúc với file thực tế (`LITE-ON QN-B03...dwg`).
2. Click `Vẽ Vách` → Scan → kiểm tra layer `A-WALL-PATT` nhận diện đúng.
3. Click `Đặt Cửa` → Scan → kiểm tra block `A-DOOR` map sang FamilySymbol cửa đi.
4. Click `Vẽ Sàn HT` → Auto Hatch → kiểm tra profile sàn khép kín.
5. Kiểm tra ESC giữa chừng không crash.

---

## Open Questions (không còn blocking)

> [!NOTE]
> Cả 2 câu hỏi đã được giải quyết:
> - **Tường**: Layer-based detection + bounding-box/parallel-line analysis.
> - **Cửa**: Block name → FamilySymbol mapping (exact block name = key).
> - **Sàn/Trần**: Tái dùng hatch boundary logic từ `DrawFloors.CadInteropService`.
