# ANTIGRAVITY — Spec tái thiết kế Hatch Pattern Extraction sau blocker COM

**Trạng thái:** Proposed — chờ phê duyệt `/1.spec`  
**Ngày:** 2026-07-15  
**Vai trò quyết định:** Chief Architect  
**Phạm vi module:** `Antigravity.DrawFloors`  
**Tài liệu nền:** `HATCH_PATTERN_SIGNATURE_IMPLEMENTATION_PLAN.md`

---

## 1. Executive decision

### Quyết định đề xuất

Không bỏ mục tiêu `PatternSignature`, nhưng thay kiến trúc extractor đơn nguồn bằng **multi-provider extraction pipeline**:

1. **AutoCAD Managed .NET Hatch Bridge** chạy in-process trong AutoCAD là nguồn chính để lấy pattern definition lines.
2. **COM/ActiveX** tiếp tục dùng cho kết nối, selection và boundary trong giai đoạn chuyển tiếp; không còn là nguồn authoritative cho pattern definition.
3. **AutoLISP/DXF probe** là đường chẩn đoán và contingency có kiểm soát, không là production primary path.
4. **Manual UI Grouping** là degraded mode bắt buộc khi không có bridge hoặc definition vẫn không đọc được. Manual group được persist bằng ID riêng; tuyệt đối không gắn nhãn `Exact` và không tự suy đoán geometry theo tên.

### Kết luận blocker

Blocker chỉ làm hỏng adapter ActiveX hiện tại, không chứng minh rằng custom hatch không chứa definition. Autodesk mô tả HATCH DXF có code `78` là số pattern definition lines, sau đó là các code pattern line `53`, `43`, `44`, `45`, `46`, `79` và dash items. Trên máy phát triển, kiểm tra reflection với `C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll` xác nhận managed type `Hatch` có:

- `NumberOfPatternDefinitions`
- `GetPatternDefinitionAt(int)`
- `PatternDefinition.Angle`, `BaseX`, `BaseY`, `OffsetX`, `OffsetY`
- `PatternDefinition.GetDashes()`

Do đó, lựa chọn có xác suất thành công và độ ổn định cao nhất là đọc dữ liệu trong AutoCAD database transaction, thay vì tiếp tục ép ActiveX projection trả dữ liệu mà nó không expose đúng cho `PatternType = 2`.

### Goal

Tiếp tục triển khai nhận dạng hatch theo hình học với zero false-positive auto-group, đồng thời luôn có workflow thủ công an toàn nếu không lấy được definition từ môi trường AutoCAD thực tế.

---

## 2. Bối cảnh và bằng chứng

### 2.1 Hiện trạng code

`Antigravity.DrawFloors` đang target `net48`, `x64`. Luồng hiện tại:

```text
Revit WPF
  -> CadInteropService.SelectAndParseHatches()
  -> Dictionary<PatternName, List<object COM>>
  -> HatchMapping giữ List<object> COM
  -> HatchMappingWindow group theo PatternName
  -> CadInteropService.ExtractHatchBoundaries(object COM)
  -> Revit ExternalEvent tạo floor
```

Các coupling cần loại bỏ:

- `PatternName` vừa là display value vừa là identity/grouping key.
- ViewModel giữ raw COM object, phụ thuộc vòng đời của AutoCAD process/document.
- Selection, metadata extraction, boundary extraction và command injection nằm chung một service.
- Lỗi từng hatch đang bị bỏ qua bằng `catch` rỗng trong batch generation, không tạo diagnostic/actionable status.
- Không có capability negotiation để biết máy người dùng có bridge, LISP hay chỉ manual mode.

### 2.2 Blocker Phase 0

Với custom hatch (`PatternType = 2`), lời gọi ActiveX/COM `NumberOfPatternLines` trả `null`/lỗi. Vì vậy:

- COM không đủ điều kiện là `IHatchDescriptorExtractor` authoritative.
- Chưa thể kết luận `FP_503` và `FP_713` là exact clone.
- Mọi group dựa trên name trong thời gian này chỉ được là `NameFallback` hoặc `Manual`.
- Phase signature không được dùng dữ liệu rỗng để tạo hash; `hash(empty)` sẽ gây false positive nghiêm trọng.

### 2.3 Bằng chứng từ Autodesk

- HATCH DXF lưu pattern type ở code `76`, scale ở `41`, angle ở `52`, số definition lines ở `78`: [HATCH DXF reference](https://help.autodesk.com/cloudhelp/2020/ENU/AutoCAD-DXF/files/GUID-C6C71CED-CE0F-4184-82A5-07AD6241F15B.htm).
- Mỗi definition line có angle/base/offset/dash count trong các code `53/43/44/45/46/79`: [Pattern Data DXF reference](https://help.autodesk.com/cloudhelp/2020/ENU/AutoCAD-DXF/files/GUID-7C05C0EC-B0FB-4A86-A164-B9E5C6C03990.htm).
- AutoLISP `entget` trả entity definition dưới dạng association list theo DXF group codes: [entget reference](https://help.autodesk.com/cloudhelp/2017/ENU/AutoCAD-AutoLISP-Reference/files/GUID-12540DAE-C84B-4BDB-AEEC-DDFE5BE3C42A.htm).
- `SendCommand` có thể chạy AutoLISP nhưng có thể trở thành asynchronous khi command cần user interaction hoặc được gọi từ event handler: [SendCommand reference](https://help.autodesk.com/view/ACDLT/2024/ENU/?guid=GUID-E13A580D-04CA-46C1-B807-95BB461A0A57).
- Plugin bundle chịu ảnh hưởng của `SECURELOAD`, `TRUSTEDPATHS` và `APPAUTOLOAD`: [Autodesk plug-in deployment](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-LT-Customization/files/GUID-5E50A846-C80B-4FFD-8DD3-C20B22098008.htm).
- Runtime plugin phải tách theo thế hệ: AutoCAD 2018 dùng .NET Framework 4.6, 2021–2024 dùng 4.8, AutoCAD 2025 dùng .NET 8: [Managed .NET compatibility](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm).

---

## 3. Scope

### 3.1 Trong scope

- Tách source selection, source identity, definition extraction, signature, grouping và Revit generation thành các boundary rõ ràng.
- Thêm AutoCAD managed bridge để đọc custom/predefined/user-defined hatch trong database transaction.
- Giao tiếp cross-process bằng request/response JSON versioned và atomic file handoff.
- Capability detection và provider fallback.
- Manual grouping UI với audit trail và persistence.
- Shadow mode trước khi signature thay mapping hiện tại.
- Diagnostic cho từng hatch, không bỏ qua exception im lặng.
- Build packaging theo AutoCAD version được duyệt.

### 3.2 Ngoài scope của spec này

- Đọc DWG offline bằng RealDWG/ODA.
- Computer vision hoặc raster comparison.
- Sửa PAT/DWG nguồn.
- Named pipe/service Windows trong MVP.
- Tự động coi hai hatch cùng tên là cùng geometry.
- Tự động merge `Transformed`, `Similar`, `NameFallback`, `Unsupported`.
- Thay toàn bộ boundary extraction hiện tại trong cùng release; phần này được giữ sau facade để giảm blast radius.

---

## 4. Đánh giá phương án

Thang điểm 1–5; điểm cao tốt hơn.

| Tiêu chí | COM hiện tại | AutoLISP injection | AutoCAD .NET bridge | Manual UI only |
|---|---:|---:|---:|---:|
| Đọc definition custom hatch | 1 | 4 | 5 | 1 |
| Deterministic/typed | 1 | 2 | 5 | 3 |
| Độ ổn định production | 2 | 2 | 5 | 5 |
| Chi phí triển khai ban đầu | 5 | 4 | 2 | 4 |
| Khả năng diagnostic | 2 | 3 | 5 | 3 |
| Deploy đa phiên bản | 5 | 3 | 2 | 5 |
| UX không gián đoạn | 4 | 3 | 4 | 2 |
| Bảo mật/IT governance | 3 | 2 | 4 | 5 |
| Có thể tạo `Exact` signature | 1 | 4 | 5 | 1 |

### 4.1 AutoLISP injection

Ưu điểm:

- `entget` cho phép nhìn trực tiếp DXF association list.
- Spike nhanh để xác nhận custom hatch thật sự chứa code `78` và line data.
- Không cần reference `acdbmgd.dll` khi chỉ chạy probe.

Nhược điểm:

- Quoting/escaping command string và đường dẫn dễ lỗi.
- `SendCommand` có semantics synchronous/asynchronous phụ thuộc context.
- Bị ảnh hưởng bởi `SECURELOAD`, trusted paths, LISP policy của doanh nghiệp.
- Data parsing từ repeated DXF codes là stateful; test và maintain kém hơn typed managed API.
- Injection tùy ý tạo bề mặt bảo mật không cần thiết.

Quyết định: **dùng làm Diagnostic Provider và emergency fallback có feature flag**, không dùng làm production default.

### 4.2 AutoCAD NETLOAD/managed plugin

Ưu điểm:

- Chạy trong AutoCAD main command context và database transaction.
- Dùng typed API `Hatch`, `PatternDefinition` và `ObjectId`.
- Có thể batch theo handle, trả lỗi riêng cho từng entity.
- Dễ version schema, telemetry và test contract.
- Không buộc Revit add-in reference AutoCAD assemblies.

Nhược điểm:

- Phải build/deploy theo AutoCAD runtime family.
- Cần bundle/trusted deployment và kiểm tra plugin availability.
- Cross-process orchestration cần timeout, correlation ID và atomic IO.

Quyết định: **production primary provider**.

Lưu ý: development có thể `NETLOAD` thủ công, nhưng production nên deploy `.bundle` autoloader. Không nên gọi `NETLOAD` cho mỗi lần scan.

### 4.3 Manual UI Grouping

Ưu điểm:

- Không phụ thuộc API definition.
- Cho dự án tiếp tục ngay cả khi bridge chưa được IT deploy.
- Người dùng kiểm soát các trường hợp mơ hồ.

Nhược điểm:

- Không chứng minh geometry equivalence.
- Tốn thao tác và có nguy cơ human error.
- Rule theo alias có thể không an toàn khi cùng tên đổi geometry.

Quyết định: **degraded mode bắt buộc**, không phải thay thế signature. Manual groups phải có audit metadata và phạm vi áp dụng rõ ràng.

---

## 5. Kiến trúc mục tiêu

```text
┌──────────────────────── Revit process ─────────────────────────┐
│ HatchScanOrchestrator                                          │
│   ├─ IAcadHatchSelectionService (COM, trả immutable refs)       │
│   ├─ IHatchDefinitionProvider                                  │
│   │    ├─ AutoCadManagedBridgeProvider  ───────────────┐        │
│   │    ├─ AutoLispDxfProvider (feature flag)           │        │
│   │    └─ UnavailableProvider                           │        │
│   ├─ Pattern canonicalizer/signature (pure .NET)       │        │
│   ├─ Grouping + mapping resolver                       │        │
│   ├─ Manual decision repository                        │        │
│   └─ WPF Preview / RequiresReview                      │        │
└────────────────────────────────────────────────────────┼────────┘
                                                         │
                         versioned request/response JSON  │
                                                         ▼
┌────────────────────── AutoCAD process ──────────────────────────┐
│ AGHATCHEXPORT command                                           │
│   -> validate request/path/document fingerprint                 │
│   -> resolve ObjectId from handles                              │
│   -> open Hatch ForRead in Transaction                          │
│   -> NumberOfPatternDefinitions / GetPatternDefinitionAt        │
│   -> return raw definition + instance transform metadata        │
│   -> atomic response write                                      │
└──────────────────────────────────────────────────────────────────┘
```

### 5.1 Nguyên tắc dependency

- `HatchPatterns.Contracts`: DTO/schema thuần .NET, không reference Revit/AutoCAD/WPF.
- `HatchPatterns.Core`: canonicalization, signature, grouping, validation; chỉ reference Contracts.
- `AutoCAD.HatchBridge`: reference AutoCAD managed assemblies và Contracts; không reference Revit.
- `DrawFloors`: reference Revit API, Contracts và Core; không reference AutoCAD managed assemblies.
- COM object không đi qua ViewModel, Core hoặc persistence.
- AutoCAD API chỉ được gọi trong AutoCAD command/document context; Revit API chỉ được gọi trong Revit main API context/ExternalEvent.

### 5.2 Source identity thay cho raw COM object

Mỗi hatch được snapshot thành `HatchSelectionRef`:

```text
SourceDocumentFingerprint
SourceDocumentPathHash
Handle
PatternName
PatternType
PatternScale
PatternAngle
IsSolid
Layer
SelectionOrdinal
```

`Handle` là locator trong cùng DWG snapshot, không là global identity. Trước khi dùng response phải so khớp document fingerprint và request ID. Boundary service có thể reacquire COM entity theo handle trong giai đoạn chuyển tiếp.

### 5.3 Provider contract

Mỗi provider trả một trong các status:

| Status | Ý nghĩa | Được hash/group Exact |
|---|---|---:|
| `Extracted` | Definition hợp lệ, transform semantics đã biết | Có |
| `Solid` | Solid hatch, policy riêng | Có, theo solid policy |
| `Unsupported` | Gradient/proxy/corrupt/không hỗ trợ | Không |
| `ProviderUnavailable` | Bridge/LISP không có | Không |
| `DefinitionMissing` | API chạy nhưng không có definition | Không |
| `DocumentMismatch` | Active DWG khác request | Không |
| `EntityNotFound` | Handle hết hạn/đã xóa | Không |
| `TimedOut` | Không nhận response đúng hạn | Không |
| `Failed` | Exception có diagnostic | Không |

Provider chain không được biến failure thành empty definition. Chỉ `Extracted` mới đi vào line-based canonicalizer.

---

## 6. AutoCAD Hatch Bridge

### 6.1 Command contract

Tên command đề xuất: `AGHATCHEXPORT`.

Workflow:

1. Revit COM selection chỉ thu handle và snapshot metadata.
2. Revit ghi request JSON vào `%LOCALAPPDATA%\Antigravity\HatchBridge\requests\<requestId>.json` bằng temp + atomic rename.
3. Revit gửi command không tương tác tới AutoCAD, chứa duy nhất `requestId` hoặc file name đã validate.
4. Bridge đọc request trong thư mục cố định, kiểm tra schema/document/size.
5. Bridge mở transaction read-only và extract từng handle.
6. Bridge ghi response temp rồi atomic rename vào `responses`.
7. Revit poll response có cancellation và timeout; không giả định `SendCommand` luôn synchronous.
8. Revit validate `requestId`, schema, source fingerprint và payload limits trước khi dùng.

### 6.2 Request schema tối thiểu

```json
{
  "schemaVersion": 1,
  "requestId": "uuid",
  "requestedAtUtc": "ISO-8601",
  "sourceDocumentFingerprint": "sha256:...",
  "entityHandles": ["1A2B", "1A2C"]
}
```

Không truyền arbitrary output path, command text hoặc LISP expression trong request.

### 6.3 Response schema tối thiểu

```json
{
  "schemaVersion": 1,
  "requestId": "uuid",
  "bridgeVersion": "1.0.0",
  "autoCadVersion": "24.3",
  "sourceDocumentFingerprint": "sha256:...",
  "items": [
    {
      "handle": "1A2B",
      "status": "Extracted",
      "patternName": "FP_503",
      "patternType": "CustomDefined",
      "patternScale": 1.0,
      "patternAngleRadians": 0.0,
      "patternDouble": false,
      "isSolid": false,
      "unitContext": "InsUnits:4",
      "definitionSemantics": "RawPatternDefinition",
      "definitionLines": [
        {
          "angleRadians": 0.7853981633974483,
          "baseX": 0.0,
          "baseY": 0.0,
          "offsetX": 0.0,
          "offsetY": 100.0,
          "dashLengths": []
        }
      ],
      "diagnostics": []
    }
  ]
}
```

Con số trên chỉ minh họa schema, không là fixture xác nhận của `FP_503`.

### 6.4 Guardrails

- Read-only transaction; không `EvaluateHatch`, không sửa DWG.
- Batch tối đa có cấu hình, ví dụ 5.000 handles/request.
- Giới hạn definition lines, dash items, file size và thời gian.
- Reject path traversal, request ID sai định dạng và symlink/reparse path ngoài root.
- Không log full DWG path mặc định; dùng hash/redaction.
- Ghi lỗi theo từng handle, không fail cả batch vì một hatch hỏng.
- Response luôn có `definitionSemantics`; không để Revit đoán raw/transformed.

### 6.5 Multi-version packaging

Không hứa một DLL chạy từ AutoCAD 2018 đến 2026.

Đề xuất release đầu chỉ target phiên bản AutoCAD thực tế đang dùng tại dự án. Sau đó tách artifact:

```text
Antigravity.HatchBridge.Acad2018   -> .NET Framework 4.6 / SDK 2018
Antigravity.HatchBridge.Acad2021   -> .NET Framework 4.8 / SDK 2021–2024
Antigravity.HatchBridge.Acad2025   -> .NET 8 / SDK 2025+
```

`PackageContents.xml` chọn component theo series. Contracts phải dùng schema JSON chung thay vì chia sẻ runtime object.

---

## 7. AutoLISP/DXF contingency

### 7.1 Mục tiêu

`AG_HatchProbe.lsp` phục vụ hai việc:

- Chứng minh trên hatch mẫu rằng `entget` có code `78` và line codes dù COM trả null.
- Là provider tạm thời nếu managed bridge bị chặn bởi deployment, nhưng chỉ khi IT cho phép LISP.

### 7.2 Thiết kế an toàn

- LISP file cố định, versioned và hash-checked; không sinh arbitrary LISP string từ user input.
- Command nhận danh sách handle từ request file trong root cố định.
- Dùng `handent` + `entget`, parse repeated groups bằng state machine đã test.
- Ghi cùng response schema, nhưng `provider = AutoLispDxfV1`.
- Revit vẫn poll response, validate correlation và timeout như managed bridge.
- Feature flag mặc định `false` trong production sau khi bridge ổn định.

### 7.3 Exit gate của LISP probe

- `FP_503` và `FP_713` đều xuất được raw association list.
- Code `78` khớp số nhóm line parse được.
- Mỗi line có đủ `53/43/44/45/46/79` và đúng số dash item.
- So sánh metadata angle/scale với Properties palette.
- Không sửa `CMDECHO`, `FILEDIA`, selection hay document state sau khi command kết thúc.

Nếu probe cũng không có code `78`, chuyển hatch đó thành `DefinitionMissing`; không parse PAT file theo tên một cách âm thầm vì tên custom pattern có thể trùng hoặc PAT nguồn đã mất.

---

## 8. Manual UI Grouping degraded mode

### 8.1 UX/state model

Mỗi row hiển thị:

- Preview/boundary thumbnail nếu có.
- Alias và count.
- Extraction source/status.
- Confidence badge: `Exact`, `Manual`, `NameFallback`, `Unsupported`, `Conflict`.
- Revit Floor Type và offset.
- Diagnostic ngắn + action `Retry extraction`.

Các action:

- `Merge manually`: chọn từ hai row trở lên, tạo manual group.
- `Split`: tách một alias/instance khỏi manual group.
- `Use name mapping for this run`: session-only, luôn có warning.
- `Remember decision`: persist manual rule có scope.
- `Reset manual decision`.

### 8.2 Manual identity và scope

Manual group dùng `manualGroupId` UUID, không dùng hash giả.

```text
matchMode: Manual
manualGroupId: UUID
confirmedBy: user/machine identity phù hợp policy
confirmedAtUtc: timestamp
scope: ThisDrawingFingerprint | Project | GlobalAliasRule
members: pattern aliases + optional source fingerprints
reason: optional note
```

Default scope là `ThisDrawingFingerprint`. `GlobalAliasRule` chỉ được bật qua UI cảnh báo vì cùng alias có thể trỏ tới geometry khác trong file khác.

### 8.3 Generation policy

| Match mode | Cho auto-create sau confirm | Persist được | Auto-reuse file khác |
|---|---:|---:|---:|
| `Exact` | Có | Có | Có, theo signature version |
| `Manual` | Có | Có | Chỉ theo scope |
| `NameFallback` | Có cho current run sau confirm | Có hạn chế | Không mặc định |
| `Unsupported` | Không trước confirm | Không thành Exact | Không |
| `Conflict` | Không | Chỉ sau resolve | Không |

Manual mode giúp workflow tiếp tục nhưng không làm ô nhiễm kho signature.

---

## 9. Pipeline và state machine mới

```text
Selected
  -> SnapshotCreated
  -> ProviderNegotiated
      -> BridgeExtracted ---------> Validated -> Signed -> Grouped
      -> LispExtracted -----------> Validated -> Signed -> Grouped
      -> DefinitionUnavailable ---> RequiresReview -> ManualGrouped
  -> MappingResolved
  -> UserConfirmed
  -> BoundaryResolved
  -> RevitGenerationQueued
  -> Created | SkippedWithDiagnostic | Failed
```

Rules:

- Signature extraction chạy trước mapping UI, nhưng boundary extraction có thể lazy sau confirm để giữ UX nhanh.
- Boundary và pattern definition là hai capability độc lập.
- Retry extraction không làm mất manual decision; khi có Exact mới, UI đề nghị migrate và yêu cầu xác nhận nếu kết quả xung đột.
- Generation nhận immutable `FloorCreationItem`; không nhận COM objects.

---

## 10. Persistence và migration

Nâng schema mapping dự kiến lên version 3 vì cần provenance và manual groups:

```json
{
  "schemaVersion": 3,
  "signatureVersion": "hatch-v1",
  "providerContractVersion": 1,
  "patternGroups": [],
  "manualGroups": [],
  "legacyNameMappings": []
}
```

Migration rules:

1. Schema v1/name-only được giữ trong `legacyNameMappings`.
2. Khi bridge trả Exact, legacy name có một signature duy nhất có thể migrate sau preview.
3. Name map tới nhiều signatures trở thành `Conflict`.
4. Manual rule không tự nâng thành Exact; có migration event lưu `fromManualGroupId` khi user chấp nhận.
5. Export deterministic và atomic; không ghi response bridge tạm vào file mapping.

---

## 11. Observability và failure handling

Mỗi scan có `scanId/requestId`; structured log tối thiểu:

```text
ScanId
Provider
BridgeVersion
AutoCadVersion
DocumentFingerprint
Handle
PatternName
ExtractionStatus
DefinitionLineCount
SignatureVersion
MatchMode
DurationMs
ErrorCode
```

Không còn `catch { }` trong đường hatch. Exception phải:

- Được phân loại thành error code ổn định.
- Log kèm correlation nhưng không leak path nhạy cảm.
- Hiện count lỗi và action export diagnostic.
- Không chặn toàn bộ batch nếu lỗi chỉ thuộc một entity.

Health/capability panel:

```text
AutoCAD connection: Connected / Unavailable
Managed bridge: Ready / Not installed / Version mismatch / Blocked
AutoLISP provider: Enabled / Disabled / Blocked
Current mode: Exact-capable / Manual degraded
```

---

## 12. Security và operational constraints

- Không cho Revit gửi arbitrary command payload ngoài command cố định và request ID.
- Bridge chỉ đọc request dưới `%LOCALAPPDATA%\Antigravity\HatchBridge`.
- Plugin bundle nên được ký số và triển khai vào trusted location theo IT policy.
- Request/response cleanup theo TTL; không xóa file ngoài root đã resolve.
- Chỉ active document có fingerprint khớp mới được xử lý.
- Không truy cập AutoCAD database từ file watcher/background thread; command context thực hiện database work.
- Không gọi Revit API trong polling/background task; chỉ marshal kết quả sang UI/Revit context phù hợp.
- Timeout không đồng nghĩa command đã bị hủy; response trễ phải bị bỏ qua bằng request ID.

---

## 13. Files cần tạo/sửa

Danh sách này là architecture file map, chưa phải authorization để code trong `/1.spec`.

### 13.1 Tạo mới — shared contracts/core

| File | Trách nhiệm |
|---|---|
| `src/Antigravity.HatchPatterns.Contracts/Antigravity.HatchPatterns.Contracts.csproj` | DTO/schema dùng giữa Revit và AutoCAD runtimes |
| `src/Antigravity.HatchPatterns.Contracts/Bridge/HatchBridgeRequest.cs` | Request envelope/version/correlation |
| `src/Antigravity.HatchPatterns.Contracts/Bridge/HatchBridgeResponse.cs` | Response envelope và per-item status |
| `src/Antigravity.HatchPatterns.Contracts/Models/RawHatchDescriptor.cs` | Descriptor không phụ thuộc Autodesk API |
| `src/Antigravity.HatchPatterns.Contracts/Models/RawPatternLine.cs` | Raw definition line |
| `src/Antigravity.HatchPatterns.Contracts/Models/HatchExtractionStatus.cs` | Status/error taxonomy |
| `src/Antigravity.HatchPatterns.Core/Antigravity.HatchPatterns.Core.csproj` | Pure signature/grouping library |
| `src/Antigravity.HatchPatterns.Core/Canonicalization/PatternCanonicalizerV1.cs` | Canonicalization `hatch-v1` |
| `src/Antigravity.HatchPatterns.Core/Signatures/PatternSignatureService.cs` | Deterministic serialization + SHA-256 |
| `src/Antigravity.HatchPatterns.Core/Grouping/PatternGroupingService.cs` | Exact/manual/conflict grouping |
| `src/Antigravity.HatchPatterns.Core/Validation/HatchDescriptorValidator.cs` | Không cho empty/invalid definition đi vào hash |

### 13.2 Tạo mới — AutoCAD bridge

| File | Trách nhiệm |
|---|---|
| `src/Antigravity.AutoCAD.HatchBridge/Antigravity.AutoCAD.HatchBridge.csproj` | AutoCAD managed plugin; target tách theo release family |
| `src/Antigravity.AutoCAD.HatchBridge/Commands/ExportHatchDefinitionsCommand.cs` | Command `AGHATCHEXPORT` |
| `src/Antigravity.AutoCAD.HatchBridge/Services/ManagedHatchDefinitionReader.cs` | Transaction + `GetPatternDefinitionAt` |
| `src/Antigravity.AutoCAD.HatchBridge/Services/BridgeFileExchange.cs` | Validate/atomic request-response IO |
| `src/Antigravity.AutoCAD.HatchBridge/Services/AutoCadDocumentFingerprint.cs` | Fingerprint active DWG |
| `src/Antigravity.AutoCAD.HatchBridge/Properties/AssemblyInfo.cs` | Command/plugin metadata nếu target cũ yêu cầu |
| `deploy/Antigravity.HatchBridge.bundle/PackageContents.xml` | Autoloader/version routing |
| `deploy/Antigravity.HatchBridge.bundle/Contents/` | Output layout; binary được build/copy, không commit file build nếu policy cấm |

### 13.3 Tạo mới — Revit adapter/orchestration

| File | Trách nhiệm |
|---|---|
| `src/Antigravity.DrawFloors/Models/HatchSelectionRef.cs` | Immutable snapshot, thay `object COM` trong ViewModel |
| `src/Antigravity.DrawFloors/Models/HatchPatternGroupViewModel.cs` | Alias/count/status/manual actions |
| `src/Antigravity.DrawFloors/Services/IHatchDefinitionProvider.cs` | Provider abstraction |
| `src/Antigravity.DrawFloors/Services/HatchScanOrchestrator.cs` | End-to-end scan state machine |
| `src/Antigravity.DrawFloors/Services/AutoCadHatchBridgeClient.cs` | Request, command trigger, polling, timeout/cancel |
| `src/Antigravity.DrawFloors/Services/AutoCadBridgeCapabilityService.cs` | Detect command/version/readiness |
| `src/Antigravity.DrawFloors/Services/ComHatchSelectionService.cs` | Selection + snapshot only |
| `src/Antigravity.DrawFloors/Services/ComHatchBoundaryService.cs` | Reacquire handle + existing boundary logic |
| `src/Antigravity.DrawFloors/Services/ManualGroupingService.cs` | Create/split/reset decisions |
| `src/Antigravity.DrawFloors/Services/PatternMappingRepository.cs` | Schema v3 load/save/migrate |
| `src/Antigravity.DrawFloors/Configuration/HatchExtractionSettings.cs` | Provider order, timeout, feature flags, limits |

### 13.4 Tạo mới — optional AutoLISP contingency

| File | Trách nhiệm |
|---|---|
| `tools/autocad/AG_HatchProbe.lsp` | Fixed diagnostic command dùng `entget` |
| `src/Antigravity.DrawFloors/Services/AutoLispDxfDefinitionProvider.cs` | Trigger/poll/parse provider response |
| `docs/runbooks/HatchBridgeDeployment.md` | Install/trust/version troubleshooting |

### 13.5 Tạo mới — tests/fixtures

| File | Trách nhiệm |
|---|---|
| `tests/Antigravity.HatchPatterns.Core.Tests/Antigravity.HatchPatterns.Core.Tests.csproj` | Pure xUnit tests |
| `tests/Antigravity.HatchPatterns.Core.Tests/CanonicalizationTests.cs` | Existing `hatch-v1` matrix |
| `tests/Antigravity.HatchPatterns.Core.Tests/GroupingPolicyTests.cs` | Exact/manual/name/conflict policy |
| `tests/Antigravity.HatchPatterns.Core.Tests/BridgeContractTests.cs` | Schema/version/limits/invalid payload |
| `tests/fixtures/hatch/fp_503.bridge.json` | Runtime output thật, chỉ thêm sau diagnostic |
| `tests/fixtures/hatch/fp_713.bridge.json` | Runtime output thật, chỉ thêm sau diagnostic |
| `tests/fixtures/hatch/custom_missing_definition.json` | Negative fixture |
| `tests/fixtures/hatch/document_mismatch.json` | Correlation/fingerprint negative fixture |

### 13.6 Sửa file hiện hữu

| File | Thay đổi |
|---|---|
| `Antigravity.sln` | Add Contracts, Core, Bridge, Tests projects |
| `src/Antigravity.DrawFloors/Antigravity.DrawFloors.csproj` | Reference Contracts/Core; package/config cần thiết |
| `src/Antigravity.DrawFloors/Services/CadInteropService.cs` | Thu hẹp thành facade hoặc tách selection/boundary; bỏ pattern-definition responsibility |
| `src/Antigravity.DrawFloors/Models/HatchMapping.cs` | Bỏ `List<object> ConnectedCadHatches`; chuyển sang group/member refs và confidence |
| `src/Antigravity.DrawFloors/UI/HatchMappingWindow.xaml` | Grouped rows, badges, diagnostics, merge/split/retry |
| `src/Antigravity.DrawFloors/UI/HatchMappingWindow.xaml.cs` | Không group theo `PatternName`; bind ViewModel/orchestrator result |
| `src/Antigravity.DrawFloors/UI/MainWindow.xaml.cs` | Gọi orchestrator; generation dùng immutable refs/items; bỏ silent catches |
| `src/Antigravity.DrawFloors/Services/FloorCreationHandler.cs` | Báo per-item result nếu hiện chỉ báo tổng batch |
| `docs/README.md` hoặc README module tương ứng | Mô tả bridge requirement và degraded mode |
| Tài liệu implementation gốc | Thêm ADR link, thay Phase 0 exit gate và provider strategy sau khi spec được duyệt |

Không sửa `Antigravity.ArchModeling` trong MVP dù cũng dùng hatch COM; chỉ xem xét reuse sau khi DrawFloors ổn định.

---

## 14. Revised delivery gates

Đây là gates để định nghĩa thành công, không phải implementation plan chi tiết của `/2.plan`.

### Gate A — Source-data proof

- Managed bridge đọc được `FP_503` và `FP_713` trên DWG thật.
- Dump đủ line count/base/offset/dashes.
- Xác nhận raw/transformed semantics bằng so sánh angle/scale và một PAT fixture biết trước.
- Nếu managed bridge thất bại, chạy LISP probe để phân biệt API failure với missing entity data.

### Gate B — Contract/core isolation

- Contracts load được từ Revit `net48` và AutoCAD runtime đã chọn.
- Core tests không load Autodesk assemblies.
- Empty definition không bao giờ sinh Exact signature.

### Gate C — Shadow mode

- Existing floor generation không đổi output.
- Báo Exact/Manual/Unsupported cạnh legacy name mapping.
- Zero false-positive trên corpus được QA duyệt.

### Gate D — Manual degraded mode

- Máy không cài bridge vẫn scan/group/map thủ công được.
- UI ghi rõ không có geometric proof.
- Manual decisions round-trip và respect scope.

### Gate E — Signature cutover

- Chỉ `Exact` hoặc user-confirmed `Manual` được generation resolver dùng.
- Rerun không duplicate mapping.
- Bridge unavailable không làm crash hoặc silently fall back sang hash/name.

---

## 15. Acceptance criteria

- Custom hatch COM `null` không còn block toàn bộ scan.
- `FP_503`/`FP_713` chỉ auto-group khi bridge/LISP provider trả definition hợp lệ và `AppearanceSignature` bằng nhau.
- Không definition => `RequiresReview`; không có empty hash.
- WPF/ViewModel không giữ COM objects.
- Revit project không reference `acdbmgd.dll`; AutoCAD bridge không reference Revit API.
- Request/response có version, request ID, document fingerprint, limits và atomic IO.
- Timeout/version mismatch/document mismatch đều có UX và log rõ.
- Manual grouping chạy được khi bridge không cài, được persist với scope/audit và không giả làm Exact.
- Không còn silent catch trên scan/extract/generation hatch path.
- Release package chỉ tuyên bố hỗ trợ các AutoCAD versions đã build/test thật.

---

## 16. Rủi ro và kiểm soát

| Rủi ro | Kiểm soát |
|---|---|
| Managed API cũng lỗi trên custom hatch mẫu | Gate A + LISP `entget`; giữ Manual mode |
| Plugin chưa load/SECURELOAD chặn | Bundle ký số/trusted path + capability panel + runbook |
| AutoCAD 2025 chuyển .NET 8 | Artifact theo runtime family; contract JSON chung |
| `SendCommand` trả trước khi command xong | Correlation + response polling + timeout, không dựa return timing |
| Active DWG đổi giữa scan | Document fingerprint check ở cả bridge và client |
| Entity bị sửa/xóa | Per-handle status; retry/rescan |
| Một name có nhiều definitions | Signature per handle; show Conflict |
| Manual global rule map sai | Default drawing scope; explicit warning cho global alias rule |
| Response bị sửa/chèn | Fixed root, schema validation, size limits; cân nhắc HMAC nếu threat model yêu cầu |
| Definition đã transform và bị transform lần hai | Explicit `definitionSemantics` + known PAT verification |
| Bridge deployment làm dự án chậm | Manual degraded mode cho phép rollout độc lập |

---

## 17. Open questions cần phê duyệt

1. **AutoCAD version đầu tiên cần support là phiên bản nào?** Khuyến nghị target đúng phiên bản dự án đang dùng trước; không mở đầu bằng matrix 2018–2026.
2. **IT có cho phép deploy signed `.bundle` vào trusted ApplicationPlugins location không?** Nếu không, LISP provider hoặc manual mode sẽ là đường tạm.
3. **Manual rule scope mặc định có chấp nhận `ThisDrawingFingerprint` không?** Khuyến nghị có; project/global alias rule phải opt-in.
4. **Có cần support AutoCAD LT không?** Managed .NET bridge và deployment capability có khác biệt; MVP nên yêu cầu full AutoCAD như code hiện tại.
5. **Document fingerprint nên dựa trên full path + file metadata hay DWG database GUID/custom property?** Cần kiểm chứng dữ liệu ổn định có sẵn trong file thật.
6. **Thời gian timeout UX chấp nhận được cho batch scan là bao nhiêu?** Khuyến nghị cấu hình và progress/cancel, không hard-code trong spec.
7. **Có cho phép AutoLISP provider trong production hay chỉ diagnostic build?** Khuyến nghị diagnostic/emergency flag mặc định off.

---

## 18. Approval gate

Đề nghị phê duyệt các quyết định sau trước khi chuyển sang `/2.plan`:

- Managed AutoCAD Hatch Bridge là production primary extractor.
- COM chỉ giữ selection/boundary trong transition.
- AutoLISP là diagnostic/contingency, không là default.
- Manual UI Grouping là degraded mode bắt buộc và không tạo Exact signature.
- Release đầu target một AutoCAD runtime family đã xác nhận.
- Kiến trúc tách Contracts/Core/AutoCAD Bridge/Revit Adapter như file map ở Mục 13.

Sau approval, `/2.plan` mới phân rã thứ tự commit, test-first cases, build matrix, spike commands và rollout/rollback.
