# Antigravity.AutoJoin — Refactor Specification for Codex

> **Mục tiêu**: Refactor module `Antigravity.Autojoin` theo chuẩn `.agents/workflows` (3-review, 5-test, 7-patterns, 8-logging, 9-compat) mà KHÔNG thay đổi logic nghiệp vụ hiện tại.

---

## 1. Project Overview

**Chức năng**: Tự động Join/Unjoin geometry giữa các cấu kiện kết cấu (Cột, Dầm, Sàn, Vách) trong Revit theo bảng quy tắc cặp (pair-based rules).

**Tính năng đã có**:
- Join hàng loạt theo Active View hoặc Selection
- Unjoin hàng loạt
- DMU Realtime (tự động join khi vẽ mới/chỉnh sửa)
- ExternalEvent pattern cho modeless WPF window
- Config JSON lưu tại `%APPDATA%\Antigravity\AutoJoin\rules.json`
- Dark theme UI với rule management (add/remove/swap/enable-disable)

---

## 2. Current File Structure

```
src/Antigravity.Autojoin/
├── Antigravity.Autojoin.csproj      ← hardcode Revit 2023 path, thiếu NuGet
├── AutoJoinCommand.cs               ← IExternalCommand (chưa dùng Toolkit)
├── Models/
│   ├── JoinRule.cs                  ← pair-based rule model (GIỮ NGUYÊN)
│   └── CategoryPriority.cs         ← DEAD CODE — không dùng ở đâu → XÓA
├── Services/
│   ├── AutoJoinService.cs           ← join engine chính (398 dòng)
│   ├── AutoJoinUpdater.cs           ← DMU IUpdater
│   ├── JoinConfigService.cs         ← JSON load/save (regex parser tự viết)
│   └── JoinEventHandler.cs          ← IExternalEventHandler (GIỮ NGUYÊN)
└── UI/
    ├── MainWindow.xaml              ← WPF dark theme UI (GIỮ NGUYÊN)
    └── MainWindow.xaml.cs           ← code-behind (GIỮ NGUYÊN)
```

**File bên ngoài module cũng cần sửa:**
```
src/Antigravity.Main/App.cs          ← dòng 130: catch {} rỗng khi register DMU
```

---

## 3. Issues to Fix (theo workflow checklist)

### 3.1 Transaction Safety (3-review.md §1) — CRITICAL

**Vị trí**: `Services/AutoJoinService.cs`

**Method 1: `ExecuteJoin` (dòng 46-67)**

Hiện tại (SAI — không có rollback):
```csharp
using (var tx = new Transaction(doc, "AutoJoin - Join Geometry"))
{
    tx.Start();
    foreach (var rule in enabled)
    {
        // ... ProcessJoin có thể throw
    }
    tx.Commit();
}
```

Sửa thành:
```csharp
using (var tx = new Transaction(doc, "AutoJoin - Join Geometry"))
{
    if (tx.Start() != TransactionStatus.Started) return result;
    try
    {
        foreach (var rule in enabled)
        {
            var elemsA = GetElements(doc, uidoc, rule.CategoryA, scope);
            var elemsB = GetElements(doc, uidoc, rule.CategoryB, scope);
            if (elemsA.Count == 0 || elemsB.Count == 0) continue;

            if (rule.SwapPriority)
                ProcessJoin(doc, elemsB, elemsA, result);
            else
                ProcessJoin(doc, elemsA, elemsB, result);
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        if (tx.HasStarted()) tx.RollBack();
        result.Errors.Add($"Transaction failed: {ex.Message}");
    }
}
```

**Method 2: `ExecuteUnjoin` (dòng 80-97)** — áp dụng CÁC SỬA TƯƠNG TỰ.

---

### 3.2 Empty Catch Blocks (3-review.md §9) — HIGH

Thay TẤT CẢ `catch { }` rỗng bằng `System.Diagnostics.Debug.WriteLine`. Dưới đây là danh sách chính xác:

#### File: `Services/AutoJoinService.cs`

| Dòng | Code hiện tại | Sửa thành |
|:---:|:---|:---|
| 144-145 | `catch { // Fallback… }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin] ElementIntersectsFilter failed for {element.Id}: {ex.Message}"); }` |
| 185 | `catch { /* skip */ }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin] Cannot join {element.Id}-{partner.Id}: {ex.Message}"); }` |
| 238-241 | `catch { continue; }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin] Winner {w.Id} has no geometry: {ex.Message}"); continue; }` |
| 300 | `catch { continue; }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin] GetJoinedElements failed for {a.Id}: {ex.Message}"); continue; }` |
| 343 | `catch { /* Bỏ qua */ }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin] SwitchJoinOrder failed {winner.Id}-{loser.Id}: {ex.Message}"); }` |
| 385-388 | `catch { return empty; }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin] GetElements failed for {builtInCategoryName}: {ex.Message}"); return new List<Element>(); }` |

#### File: `Services/AutoJoinUpdater.cs`

| Dòng | Code hiện tại | Sửa thành |
|:---:|:---|:---|
| 126 | `catch { /* skip */ }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin DMU] Element {id} error: {ex.Message}"); }` |
| 128-132 | `catch { /* không throw */ }` | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin DMU] Execute error: {ex.Message}"); }` |

#### File: `Services/JoinConfigService.cs`

| Dòng | Code hiện tại | Sửa thành |
|:---:|:---|:---|
| 86-87 | `catch { }` (LoadDmuEnabled) | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin Config] LoadDmuEnabled error: {ex.Message}"); }` |
| 96-97 | `catch { }` (SaveDmuEnabled) | `catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoJoin Config] SaveDmuEnabled error: {ex.Message}"); }` |

#### File: `src/Antigravity.Main/App.cs`

| Dòng | Code hiện tại | Sửa thành |
|:---:|:---|:---|
| 130 | `try { ...Register(); } catch { }` | `try { Antigravity.Autojoin.Services.AutoJoinUpdater.Register(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Antigravity] AutoJoin DMU register failed: {ex.Message}"); }` |

---

### 3.3 API Deprecated — IntegerValue (9-compat.md) — MEDIUM

`ElementId.IntegerValue` deprecated từ Revit 2025+. Project hiện chỉ target net48/Revit 2023 nên chưa cần sửa ngay, nhưng PHẢI thêm comment để đánh dấu.

**Vị trí cần thêm comment** (tất cả trong `Services/AutoJoinService.cs`):

| Dòng | Code | Thêm comment |
|:---:|:---|:---|
| 115 | `int catId = element.Category.Id.IntegerValue;` | `// TODO[Revit2025]: Replace .IntegerValue with .Value (long)` |
| 169 | `$"JOIN-{element.Id.IntegerValue}-{partner.Id.IntegerValue}"` | `// TODO[Revit2025]: .IntegerValue → .Value` |
| 170 | `$"JOIN-{partner.Id.IntegerValue}-{element.Id.IntegerValue}"` | (cùng dòng trên) |
| 336 | `$"SWITCH-{winner.Id.IntegerValue}-{loser.Id.IntegerValue}"` | `// TODO[Revit2025]: .IntegerValue → .Value` |
| 373 | `e.Category.Id.IntegerValue == (int)bic` | `// TODO[Revit2025]: .IntegerValue → .Value` |

---

### 3.4 .csproj Hardcode Path (rules.md) — HIGH

**File**: `Antigravity.Autojoin.csproj`

Hiện tại hardcode `C:\Program Files\Autodesk\Revit 2023\`:
```xml
<Reference Include="RevitAPI">
  <HintPath>C:\Program Files\Autodesk\Revit 2023\RevitAPI.dll</HintPath>
  <Private>False</Private>
</Reference>
<Reference Include="RevitAPIUI">
  <HintPath>C:\Program Files\Autodesk\Revit 2023\RevitAPIUI.dll</HintPath>
  <Private>False</Private>
</Reference>
```

**Yêu cầu**: Trước khi sửa, ĐỌC file `src/Antigravity.DrawBeams/Antigravity.DrawBeams.csproj` và COPY CÙNG PATTERN reference RevitAPI. Giữ nguyên phần còn lại của .csproj (UseWPF, OutputPath, etc.).

---

### 3.5 Dead Code — CategoryPriority.cs — LOW

**File**: `Models/CategoryPriority.cs`

Grep confirm: class `CategoryPriority` không được import hay sử dụng ở bất kỳ file nào khác. Đây là remnant từ phương án Priority List cũ.

**Yêu cầu**: XÓA file này.

---

## 4. Logic Flow Hiện Tại (KHÔNG thay đổi)

### Manual Join/Unjoin Flow:
```
User click "JOIN GEOMETRY" button
    │
    ├─ MainWindow.xaml.cs → BtnJoin_Click()
    │   ├─ Set _handler.Action = JoinAction.Join
    │   ├─ Set _handler.Rules = current rules list
    │   ├─ Set _handler.Scope = ActiveView | Selection
    │   └─ _exEvent.Raise()  ← ExternalEvent (safe thread pattern)
    │
    ├─ JoinEventHandler.Execute(UIApplication app)
    │   └─ AutoJoinService.ExecuteJoin(doc, uidoc, rules, scope)
    │       │
    │       ├─ Transaction("AutoJoin - Join Geometry")
    │       ├─ Foreach enabled rule:
    │       │   ├─ GetElements(CategoryA, scope) → winners
    │       │   ├─ GetElements(CategoryB, scope) → losers
    │       │   ├─ (SwapPriority ? swap winners↔losers)
    │       │   └─ ProcessJoin(doc, winners, losers, result)
    │       │       │
    │       │       └─ Foreach winner:
    │       │           ├─ ElementIntersectsElementFilter(winner) → intersecting losers
    │       │           ├─ Already joined? → EnsureWinnerCuts (SwitchJoinOrder if needed)
    │       │           └─ Not joined? → JoinGeometry + EnsureWinnerCuts
    │       │
    │       └─ Commit Transaction
    │
    └─ OnJoinCompleted callback → Dispatcher.Invoke → update status bar
```

### DMU Realtime Flow:
```
Revit fires Element Added/Modified trigger
    │
    ├─ AutoJoinUpdater.Execute(UpdaterData)
    │   ├─ Guard: _isProcessing? return (re-entrancy lock)
    │   ├─ Guard: IsEnabled? return if false
    │   ├─ Get addedIds + modifiedIds
    │   └─ Foreach element ID:
    │       └─ AutoJoinService.JoinSingleElement(doc, element, rules)
    │           ├─ Match element's category to rules
    │           ├─ Find intersecting partners via ElementIntersectsElementFilter
    │           ├─ Time-based cache check (chống ping-pong, 2s cooldown)
    │           ├─ JoinGeometry if not already joined
    │           └─ EnsureWinnerCuts (SwitchJoinOrder if needed)
    │
    └─ (Runs INSIDE Revit's internal transaction — DO NOT create new Transaction)
```

---

## 5. Refactor Task List (Thứ tự thực hiện)

### Phase 1: Safety — PHẢI LÀM TRƯỚC
- [ ] **T1**: `AutoJoinService.cs` — Wrap `ExecuteJoin` transaction trong try-catch, thêm `tx.RollBack()` trong catch
- [ ] **T2**: `AutoJoinService.cs` — Wrap `ExecuteUnjoin` transaction tương tự T1
- [ ] **T3**: Thay 11 vị trí `catch { }` rỗng bằng `Debug.WriteLine` (xem bảng §3.2 chi tiết)
- [ ] **T4**: Xóa file `Models/CategoryPriority.cs`

### Phase 2: Compatibility — NÊN LÀM
- [ ] **T5**: Sửa `Antigravity.Autojoin.csproj` — bỏ hardcode path, dùng cùng pattern .csproj với DrawBeams
- [ ] **T6**: Thêm 5 comment `// TODO[Revit2025]` tại các vị trí dùng `.IntegerValue`

### Phase 3: Polish — LÀM SAU
- [ ] **T7**: Kiểm tra base class của Command trong DrawBeams/DrawColumns — nếu dùng Nice3point thì migrate `AutoJoinCommand` theo
- [ ] **T8**: Khi project setup Serilog (theo `8-logging.md`), chuyển `Debug.WriteLine` → `Log.Warning/Error`

---

## 6. Files Modified Summary

| File | Action | Tasks |
|:---|:---|:---:|
| `Services/AutoJoinService.cs` | EDIT: transaction safety + logging + comments | T1,T2,T3,T6 |
| `Services/AutoJoinUpdater.cs` | EDIT: logging cho catch blocks | T3 |
| `Services/JoinConfigService.cs` | EDIT: logging cho catch blocks | T3 |
| `Antigravity.Autojoin.csproj` | EDIT: bỏ hardcode path | T5 |
| `Models/CategoryPriority.cs` | DELETE | T4 |
| `src/Antigravity.Main/App.cs` dòng 130 | EDIT: logging | T3 |

**Files KHÔNG SỬA**:
- `AutoJoinCommand.cs` (chờ Phase 3)
- `Models/JoinRule.cs` (logic đúng)
- `Services/JoinEventHandler.cs` (pattern chuẩn)
- `UI/MainWindow.xaml` (UI hoàn chỉnh)
- `UI/MainWindow.xaml.cs` (code-behind đúng)

---

## 7. Reference Workflows (đọc trước khi code)

| File | Đường dẫn | Mục đích |
|:---|:---|:---|
| Review checklist | `.agents/workflows/3-review.md` | 9 mục kiểm tra bắt buộc |
| Patterns | `.agents/workflows/7-patterns.md` | ExternalEvent, DMU, FailureHandler |
| Logging | `.agents/workflows/8-logging.md` | Serilog setup, LoggedCommand |
| Compatibility | `.agents/workflows/9-compat.md` | IntegerValue deprecation |
| Rules | `.agents/workflows/rules.md` | Global coding standards |

---

## 8. Post-Refactor Validation

```
### Verification Checklist
- [ ] Build thành công: `dotnet build src/Antigravity.Autojoin/Antigravity.Autojoin.csproj`
- [ ] Solution build: `dotnet build Antigravity.sln`
- [ ] Grep confirm: KHÔNG còn `catch { }` hoặc `catch (Exception) { }` rỗng
- [ ] Grep confirm: Mọi `new Transaction(` đều có `RollBack` trong cùng method
- [ ] Grep confirm: KHÔNG còn hardcode `C:\Program Files\Autodesk\Revit` trong .csproj
- [ ] Grep confirm: File `CategoryPriority.cs` đã xóa
- [ ] Grep confirm: Có ít nhất 5 comment `TODO[Revit2025]`
- [ ] Logic KHÔNG thay đổi: JoinRule model giữ nguyên, UI giữ nguyên
```
