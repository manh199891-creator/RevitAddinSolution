# Workflow: Multi-version Revit Compatibility
Mục tiêu: Code chạy đúng trên Revit 2022–2026 mà không cần maintain nhiều branch.

---

## Compatibility Matrix

| Revit | .NET | API namespace thay đổi đáng kể |
|---|---|---|
| 2022 | .NET Framework 4.8 | `DisplayUnitType` (deprecated) |
| 2023 | .NET Framework 4.8 | `ForgeTypeId` thay `UnitType` hoàn toàn |
| 2024 | .NET Framework 4.8 | `ParameterUtils`, `LabelUtils` API mới |
| 2025 | .NET 8.0 | Breaking: `ExternalApplication` → `ExternalDBApplication` changes |
| 2026 | .NET 8.0 | Geometry API updates |

---

## Compiler directives theo Revit year

```xml
<!-- Antigravity.Main.csproj -->
<PropertyGroup Condition="'$(RevitYear)'=='2022'">
  <DefineConstants>REVIT2022;REVIT_LEGACY_UNITS</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="'$(RevitYear)'=='2024'">
  <DefineConstants>REVIT2024</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="'$(RevitYear)'&gt;='2025'">
  <DefineConstants>REVIT2025_PLUS;REVIT_NET8</DefineConstants>
</PropertyGroup>
```

```csharp
public static double GetLengthInFeet(Parameter param)
{
#if REVIT_LEGACY_UNITS
    // Revit 2022: DisplayUnitType còn hoạt động
    return param.AsDouble();
#else
    // Revit 2023+: ForgeTypeId
    return UnitUtils.ConvertFromInternalUnits(
        param.AsDouble(), UnitTypeId.Feet);
#endif
}
```

---

## Unit API — thay đổi lớn nhất

### Revit 2022 (legacy)
```csharp
// Deprecated nhưng vẫn compile được
UnitUtils.ConvertToInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS);
```

### Revit 2023+ (current)
```csharp
// Dùng ForgeTypeId — bắt buộc từ 2023
UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
```

### Wrapper để support cả hai
```csharp
public static class RevitUnits
{
    public static double MmToFeet(double mm)
        => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

    public static double FeetToMm(double feet)
        => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
}
```
> Gói toàn bộ unit conversion vào `RevitUnits` class — khi API thay đổi chỉ sửa 1 chỗ.

---

## BuiltInParameter — cross-version safe

```csharp
// ✅ Dùng BuiltInParameter (ổn định qua versions)
var height = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble();

// ⚠️ Dùng parameter name string — locale-dependent và có thể đổi
var height = wall.Parameters["Unconnected Height"]?.AsDouble(); // Sai với Revit tiếng Việt/Nhật
```

---

## API deprecated → replacement table

| API cũ (Revit ≤2022) | API mới (Revit 2023+) | Ghi chú |
|---|---|---|
| `DisplayUnitType` | `ForgeTypeId` / `UnitTypeId` | Breaking change |
| `UnitType` | `SpecTypeId` | Parameter spec type |
| `ParameterType` | `StorageType` + `SpecTypeId` | Tách ra 2 concept |
| `Category.Id.IntegerValue` | `Category.Id.Value` | 2025+: IntegerValue deprecated |
| `ElementId.IntegerValue` | `ElementId.Value` | 2025+: use `.Value` (long) |
| `Plane.CreateByNormalAndOrigin` | `Plane.CreateByNormalAndOrigin` | Không đổi |

---

## ElementId.Value vs IntegerValue (Revit 2025+)

```csharp
// Revit 2024 và trước
int id = element.Id.IntegerValue;

// Revit 2025+ (IntegerValue deprecated, Id giờ là 64-bit)
long id = element.Id.Value;

// Cross-version safe
#if REVIT2025_PLUS
    var idValue = element.Id.Value;
#else
    var idValue = (long)element.Id.IntegerValue;
#endif
```

---

## Test compatibility checklist

Khi thêm API call mới, kiểm tra:
- [ ] API này available từ Revit version nào? (xem API docs)
- [ ] Có cần `#if` directive không?
- [ ] Smoke test trên phiên bản Revit thấp nhất support (hiện tại: 2022)
- [ ] Không dùng API nào đánh dấu `[Obsolete]` nếu có alternative

---

## Nice3point.Revit.Toolkit versioning

```
Toolkit version phải match Revit version:
Nice3point.Revit.Toolkit 2022.* → Revit 2022
Nice3point.Revit.Toolkit 2024.* → Revit 2024
Nice3point.Revit.Toolkit 2025.* → Revit 2025
```

Cập nhật Toolkit: check release notes tại https://github.com/Nice3point/RevitToolkit
