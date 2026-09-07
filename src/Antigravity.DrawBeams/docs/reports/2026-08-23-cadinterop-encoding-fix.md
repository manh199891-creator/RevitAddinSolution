# DrawBeams CadInterop Encoding Fix

Date: 2026-08-23
Owner: Antigravity.DrawBeams
Status: PASS / CLOSED

## Scope

Correct UTF-8 mojibake in `Services/CadInteropService.cs` without changing beam-recognition logic, COM control flow, namespace, assembly identity, project references or Revit beam creation behavior.

## Corrected production text

Nine corrupted Vietnamese occurrences were repaired, including AutoCAD connection errors, selection prompts, the V12 error prefix, and explanatory comments.

Examples:

- `KhÃ´ng thá»ƒ káº¿t ná»‘i...` -> `Không thể kết nối...`
- `QuÃ©t chá»n vÃ¹ng dáº§m cáº§n váº½...` -> `Quét chọn vùng dầm cần vẽ...`
- `Lá»—i V12` -> `Lỗi V12`

## Regression protection

Added `tests/Antigravity.DrawBeams.Tests/CadInteropEncodingRegressionTests.cs`.

The test scans the production source for characteristic UTF-8 mojibake sequences while avoiding false positives for valid Vietnamese characters such as `Ã` in `ĐÃ`.

## Verification

- RED proof: encoding regression test failed before the production text fix.
- Mojibake scan under `src/Antigravity.DrawBeams`: no characteristic mojibake sequences remain.
- DrawBeams focused tests: 76/76 PASS.
- Full solution build: PASS with propagated exit code 0.
- Last-known-good remains `PENDING_REVALIDATION`; no S2-S4 promotion is claimed.

## Verification-side governance correction

Full-solution verification exposed that `src/Antigravity.ZoneSplit/docs/design/reference/ZoneSplit_Core.cs` was being picked up by SDK default `**/*.cs` compilation despite being documented as reference-only. `Antigravity.ZoneSplit.csproj` now explicitly removes `docs/**/*.cs` from `Compile`; ZoneSplit project build and full solution build both pass afterward.

## Safety

Existing unrelated DrawBeams WIP in `CadInteropService.cs` was preserved. This task changed only the nine corrupted text/comment occurrences plus the encoding regression test and the ZoneSplit compile exclusion required to restore the intended documentation-only boundary.
