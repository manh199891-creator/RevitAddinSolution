# Superpowers Framework — RevitAddinSolution
# Inherits: ~/.gemini/GEMINI.md (4-phase pipeline, caveman, TDD, subagent rules)
# This file: project-specific context only

---

## Project Context

**Stack:**
- Framework: Nice3point.Revit.Toolkit
- Language: C# (.NET 8 / Revit 2025+, .NET 4.8 / Revit ≤2024)
- Testing: xUnit (unit) + RevitTestFramework (integration)
- Logging: Serilog → `%LOCALAPPDATA%\Antigravity\Logs\`
- CI: GitHub Actions (unit tests only; integration: manual)
- Versioning: SemVer + `-r{RevitYear}` suffix

**MCP tools available:** `rvt-mcp`, `zfenix-revit` — use for Revit model queries, element inspection, parameter reading. Prefer MCP over manual API calls when querying model data.

**Plans location:** `docs/superpowers/plans/YYYY-MM-DD-<feature>.md`

---

## Revit API Rules (always apply)

- Inherit from `Autodesk.Revit.Api.Plugins.AddInPlugin`
- Declare `[PluginAttribute]` và `[AddInPlugin(AddInLocation.AddIn)]`
- Set DLL references: Copy Local = False
- Wrap Execute in `try-catch`
- **Transaction safety:** mọi write phải trong `Transaction` — không write ngoài transaction
- **Thread safety:** không gọi Revit API ngoài main thread — dùng `ExternalEvent` hoặc `IExternalEventHandler`
- **Unit conversion:** luôn dùng `UnitUtils` — không hardcode unit values
- **Namespace:** `Antigravity.[Module]` pattern

---

## Routing Context (replaces old 0-orchestrate.md)

| Task type | Phase to invoke | Additional context |
|-----------|----------------|-------------------|
| Feature mới / UI redesign | `/spec` | Load `1-design.md` pattern nếu cần Revit API research |
| Viết code / sửa bug | `/code` | Dùng `source-driven-development` cho API ít quen |
| Debug crash / lỗi lạ | `/code` (systematic-debugging auto-loads) | Kết hợp `8-logging` pattern |
| Build + deploy | `/ship` | Chạy `DeployToRevit.ps1` sau verify |
| Migration Nice3point | `/plan` | Đọc `.agents/examples/StandardAddInPlugin.cs` trước |
| Multi-version Revit support | `/plan` | Note .NET target framework per version |

---

## Key Files

- `DeployToRevit.ps1` — deploy to Revit
- `.agents/examples/StandardAddInPlugin.cs` — chuẩn plugin template
- `docs/superpowers/plans/` — tất cả implementation plans
- `docs/superpowers/tools/Generate-ArchitectureGraph.ps1` — tái tạo dependency graph từ `src/`
  - Run khi `/recap` hoặc khi cần hiểu nhanh module dependencies
  - Output: `docs/superpowers/tools/architecture_map.json` (gitignored, regenerated each time)
  - Config: `docs/superpowers/tools/understand_config.json`
