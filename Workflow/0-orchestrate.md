# Workflow: Orchestrate
Mục tiêu: Điều phối agent đúng workflow theo từng loại task.

---

## Workflow map

```
0-orchestrate  (file này — routing)
├── 1-design      → Khi cần thiết kế feature mới, UI, kiến trúc class
├── 2-code        → Khi viết / sửa code (coding standards, patterns)
├── 3-review  ✦   → Khi review code trước merge (checklist Revit API)
├── 5-test    ✦   → Khi viết test hoặc debug bằng test
├── 6-release     → Khi build và phân phối add-in
├── 7-patterns    → Khi cần advanced Revit API pattern
├── 8-logging     → Khi setup logging hoặc trace lỗi production
├── 9-compat      → Khi hỗ trợ nhiều phiên bản Revit
└── rules.md      → Global rules áp dụng cho MỌI task
```

✦ = ưu tiên cao, thêm vào sớm nhất

---

## Routing logic

| Task | Workflow |
|---|---|
| Thiết kế tính năng mới | `1-design` |
| Viết code, sửa bug | `2-code` + `rules.md` |
| Review PR / code | `3-review` |
| Debug crash / lỗi lạ | `4-debug` + `8-logging` |
| Viết unit / integration test | `5-test` |
| Build release, packaging | `6-release` |
| Cần dùng ExternalEvent, DMU, ExtStorage | `7-patterns` |
| Setup Serilog, trace production bug | `8-logging` |
| Support thêm phiên bản Revit | `9-compat` |
| Migration sang Nice3point | `migrate-to-nice3point` |

---

## Stack công nghệ project

- **Framework**: Nice3point.Revit.Toolkit
- **Language**: C# (.NET 8 cho Revit 2025+, .NET 4.8 cho Revit ≤2024)
- **Testing**: xUnit (unit) + RevitTestFramework (integration)
- **Logging**: Serilog → file `%LOCALAPPDATA%\Antigravity\Logs\`
- **CI**: GitHub Actions (unit tests only; integration tests: manual)
- **Versioning**: SemVer + `-r{RevitYear}` suffix

---

## Rules luôn áp dụng (xem rules.md)
- Transaction safety
- Thread safety (no API off main thread)
- Unit conversion qua `UnitUtils` — không hardcode
- Naming: `Antigravity.[Module]` namespace
