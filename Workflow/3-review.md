# Workflow: Code Review
Mục tiêu: Đảm bảo code Revit API an toàn, đúng pattern, không gây crash Revit.

---

## Checklist Review bắt buộc

### 1. Transaction Safety
- [ ] Mọi thay đổi model đều nằm trong `using (Transaction tx = ...)` hoặc `SubTransaction`
- [ ] Không có nested `Transaction` (dùng `TransactionGroup` nếu cần nhóm)
- [ ] Có kiểm tra `tx.Start()` trả về `TransactionStatus.Started` trước khi thực thi
- [ ] Mọi nhánh exception đều gọi `tx.RollBack()` — không để transaction treo
- [ ] Không gọi Revit API ngoài transaction khi đang modify model

```csharp
// ✅ Đúng
using (var tx = new Transaction(doc, "Create Opening"))
{
    if (tx.Start() != TransactionStatus.Started) return Result.Failed;
    try
    {
        // thao tác model
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        Logger.Error(ex);
        return Result.Failed;
    }
}

// ❌ Sai — không có rollback khi lỗi
var tx = new Transaction(doc);
tx.Start();
DoSomething(); // nếu throw → transaction treo
tx.Commit();
```

---

### 2. Element Validity
- [ ] Mọi `Element` lấy từ collector/input đều được kiểm tra `null` trước khi dùng
- [ ] Không lưu `ElementId` hoặc `Element` qua nhiều transaction (có thể bị invalidate)
- [ ] Dùng `doc.GetElement(id)` thay vì cache reference lâu dài
- [ ] Kiểm tra `element.IsValidObject` trước khi thao tác sau transaction khác

```csharp
// ✅ Đúng
var wall = doc.GetElement(wallId) as Wall;
if (wall == null || !wall.IsValidObject) return;

// ❌ Sai — cache Element object qua nhiều event/transaction
private Wall _cachedWall; // → có thể trở thành invalid object
```

---

### 3. Thread Safety
- [ ] Không gọi Revit API từ background thread (`Task`, `Thread`, `Dispatcher`)
- [ ] Dùng `IExternalEventHandler` + `ExternalEvent` cho async operations
- [ ] Không block Revit UI thread với `Thread.Sleep` hoặc blocking I/O
- [ ] WPF/WinForms dialog chạy trên UI thread, không tạo modal dialog từ event handler

---

### 4. Performance
- [ ] `FilteredElementCollector` dùng Quick Filter trước Slow Filter
- [ ] Không dùng `.Cast<T>().Where(...)` trực tiếp trên collector chưa filter
- [ ] Không gọi `doc.Regenerate()` trong vòng lặp
- [ ] Không lặp qua toàn bộ elements nhiều lần — collect một lần, xử lý nhiều lần

```csharp
// ✅ Đúng: Quick Filter trước
new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Walls)   // Quick Filter
    .OfClass(typeof(Wall))                    // Quick Filter
    .WhereElementIsNotElementType()            // Quick Filter
    .Where(w => w.Name.Contains("CL"));       // Slow Filter — sau cùng

// ❌ Sai: Slow Filter trước
new FilteredElementCollector(doc)
    .Where(e => e is Wall)  // → duyệt toàn bộ model
    .Cast<Wall>()
    ...
```

---

### 5. Unit Handling
- [ ] Mọi giá trị nhập từ user đều được convert từ mm → feet trước khi gán vào Revit parameter
- [ ] Dùng `UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters)` — không hardcode hệ số
- [ ] Giá trị hiển thị ra UI phải convert ngược về mm/m

---

### 6. Naming Convention (Antigravity project)
- [ ] Namespace: `Antigravity.[TênModule]` (e.g. `Antigravity.DrawColumns`)
- [ ] Command class: `[TênTínhNăng]Command` implement `IExternalCommand`
- [ ] Event handler: `[TênSựKiện]Handler` implement `IExternalEventHandler`
- [ ] Public properties: PascalCase
- [ ] Local variables: camelCase
- [ ] Async methods: suffix `Async`

---

### 7. Nice3point.Revit.Toolkit
- [ ] Command class extends `ExternalCommand` từ Toolkit, không implement `IExternalCommand` trực tiếp
- [ ] Dùng `RibbonController` cho UI — không dùng raw `RibbonPanel` API
- [ ] `OptionsClass` dùng cho settings thay vì custom serialization
- [ ] Không duplicate logic đã có trong Toolkit

---

### 8. Error Handling & UX
- [ ] Mọi exception đều được log với context đủ (method name, element id, parameter values)
- [ ] User nhìn thấy thông báo lỗi có ý nghĩa — không show raw stack trace
- [ ] Command trả về đúng `Result.Succeeded / Failed / Cancelled`
- [ ] `TaskDialog` dùng đúng icon (Warning/Error/Information)

---

### 9. Code Smell kiêng kị
| Pattern | Lý do |
|---|---|
| `doc.Delete(ids)` trong loop | Dùng batch `doc.Delete(ICollection<ElementId>)` |
| `catch (Exception e) {}` rỗng | Phải log ít nhất |
| Hardcode string path | Dùng config/settings |
| Magic number đơn vị (e.g. `/ 304.8`) | Dùng `UnitUtils` |
| `Thread.Sleep` | Dùng `ExternalEvent` |
| `Application.OpenDocumentFile` không đóng | Memory leak |

---

## Review Checklist nhanh (paste vào PR comment)

```
### Revit API Review Checklist
- [ ] Transactions: wrapped, rollback on exception
- [ ] Element validity: null-check, IsValidObject
- [ ] Thread safety: no API call off main thread
- [ ] Performance: Quick Filter → Slow Filter
- [ ] Units: UnitUtils for all conversions
- [ ] Naming: Antigravity convention
- [ ] Nice3point Toolkit: used where applicable
- [ ] Error handling: logged, user-friendly message
- [ ] No code smells (see 3-review.md §9)
```
