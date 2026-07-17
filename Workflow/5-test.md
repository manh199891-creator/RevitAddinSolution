# Workflow: Testing Strategy
Mục tiêu: Đảm bảo add-in ổn định qua mọi phiên bản Revit và không regress khi thêm tính năng.

---

## Tầng kiểm thử (Testing Pyramid)

```
        [Manual Smoke Test]         ← chạy trước release
       /                     \
    [Integration Tests]            ← RevitTestFramework / RevitNUnit
   /                         \
[Unit Tests — xUnit/NUnit]         ← logic thuần, không cần Revit
```

---

## Tầng 1: Unit Tests (không cần Revit)

### Mục tiêu
Test logic nghiệp vụ thuần: tính toán, parsing, validation — không phụ thuộc Revit API.

### Setup
```xml
<!-- Antigravity.Core.Tests.csproj -->
<PackageReference Include="xunit" Version="2.9.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
```

### Các class phải unit-testable
- `Services/` — business logic tách biệt khỏi Revit calls
- `Models/` — data objects, validation
- Helper/Utility classes (unit conversion, geometry math)
- ViewModel (nếu dùng MVVM)

### Pattern: tách Revit dependency ra interface

```csharp
// ✅ Testable — inject interface
public class OpeningCreator
{
    private readonly IRevitGeometryService _geo;
    public OpeningCreator(IRevitGeometryService geo) => _geo = geo;

    public XYZ CalculateCenter(double x, double y, double z)
        => new XYZ(x, y, z); // pure logic → dễ test
}

// Test
[Fact]
public void CalculateCenter_GivenValidInput_ReturnsCorrectPoint()
{
    var creator = new OpeningCreator(Substitute.For<IRevitGeometryService>());
    var pt = creator.CalculateCenter(1.0, 2.0, 3.0);
    Assert.Equal(1.0, pt.X);
}
```

### Naming convention test
```
[MethodName]_[Scenario]_[ExpectedBehavior]
CalculateCenter_GivenZeroInput_ReturnsOrigin
CreateOpening_WhenWallNotFound_ThrowsArgumentException
ConvertToFeet_GivenMillimeters_ReturnsCorrectValue
```

---

## Tầng 2: Integration Tests (cần Revit process)

### Tool: RevitTestFramework (RTF) hoặc Revit.TestRunner

```
Cài đặt:
PM> Install-Package RevitTestFramework
```

### Cấu trúc project
```
Antigravity.Tests.Integration/
├── Fixtures/
│   ├── SampleProject.rvt       ← test model
│   └── EmptyProject.rvt
├── DrawColumns/
│   └── CreateColumnCommandTests.cs
├── DrawWalls/
│   └── WallCreationTests.cs
└── RevitTestFramework.xml      ← config RTF
```

### Viết integration test

```csharp
[TestFixture]
public class CreateColumnCommandTests
{
    private UIApplication _uiApp;
    private Document _doc;

    [SetUp]
    public void Setup()
    {
        // RTF inject UIApplication tự động qua test runner
        _doc = _uiApp.ActiveUIDocument.Document;
    }

    [Test]
    [TestModel(@".\Fixtures\EmptyProject.rvt")]
    public void Execute_WhenValidInput_CreatesColumnAtCorrectLocation()
    {
        // Arrange
        var familySymbol = GetColumnFamilySymbol(_doc);
        var targetPoint = new XYZ(0, 0, 0);

        // Act
        using (var tx = new Transaction(_doc, "Test Create Column"))
        {
            tx.Start();
            var col = _doc.Create.NewFamilyInstance(
                targetPoint, familySymbol, StructuralType.Column);
            tx.Commit();

            // Assert
            var collected = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault(f => f.Id == col.Id);

            Assert.IsNotNull(collected);
            Assert.AreEqual(targetPoint.X, collected.Location... );
        }
    }
}
```

### Chạy RTF từ command line
```bash
# Chạy qua RevitTestFramework CLI
RTF.exe --revit "C:\Program Files\Autodesk\Revit 2024\Revit.exe" \
        --testAssembly "Antigravity.Tests.Integration.dll" \
        --results "TestResults.xml"
```

---

## Tầng 3: Manual Smoke Test (trước mỗi release)

### Checklist smoke test

```
### Smoke Test — v{version} — Revit {year}

#### Môi trường
- [ ] Revit 2022 ✓ / ✗
- [ ] Revit 2023 ✓ / ✗
- [ ] Revit 2024 ✓ / ✗
- [ ] Revit 2025 ✓ / ✗

#### Add-in load
- [ ] Ribbon tab hiển thị đúng
- [ ] Không có lỗi khi mở Revit (check journal)
- [ ] Icon hiển thị đúng độ phân giải

#### Core features
- [ ] [DrawColumns] Tạo cột từ DWG → columns xuất hiện đúng vị trí
- [ ] [DrawWalls] Tạo tường → không crash khi wall type không có
- [ ] [Autojoin] Join walls sau khi tạo → geometry đúng
- [ ] Settings dialog → save/load đúng

#### Edge cases
- [ ] Chạy lệnh khi không có document mở → thông báo lỗi đúng
- [ ] Cancel giữa chừng → không để lại garbage element
- [ ] File rvt read-only → thông báo phù hợp

#### Performance
- [ ] Tạo 100 columns < 10 giây
- [ ] Memory không tăng bất thường sau nhiều lần chạy
```

---

## CI/CD Integration

### GitHub Actions (nếu dùng)
```yaml
# .github/workflows/test.yml
name: Unit Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'
      - name: Run Unit Tests
        run: dotnet test Antigravity.Core.Tests/ --logger "trx;LogFileName=results.trx"
      - name: Upload Results
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: "**/*.trx"
```

> Note: Integration tests (cần Revit) chạy thủ công hoặc trên máy có Revit license.

---

## Test data management

- Lưu file `.rvt` test model trong `Tests/Fixtures/` — commit vào Git LFS
- Không dùng production model làm test model
- Đặt tên theo pattern: `[feature]_[scenario].rvt` (e.g. `columns_mixed_types.rvt`)
- Sau mỗi test integration: `tx.RollBack()` hoặc không save file
