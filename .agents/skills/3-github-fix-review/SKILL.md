---
name: 3-github-fix-review
description: "Quy trình tiếp nhận review feedback, sửa lỗi đúng scope, re-test, commit fix(...) và push branch."
---

# GitHub Fix Review Workflow

**Announce:** "Starting /3-github-fix-review — Quy trình xử lý Review Feedback & Push Fix"

## Quy trình thực hiện

### Bước 1: Tiếp nhận & Phân tích Review Findings
- Đọc kỹ danh sách nhận xét review từ user/reviewer.
- Xác định nguyên nhân gốc rễ và giải pháp sửa đổi tối thiểu, chính xác.
- **Ràng buộc cứng:** KHÔNG sửa bất kỳ file nào ngoài scope công việc được yêu cầu.

### Bước 2: Sửa code & Xử lý Edge Cases
- Thực hiện chỉnh sửa code và bổ sung các test cases tương ứng với review findings.
- Đảm bảo không nuốt exception (catch rỗng) và giữ code theo nguyên tắc YAGNI/DRY.

### Bước 3: Build & Test Validation
- Chạy build: `dotnet build`
- Chạy toàn bộ test suite: `dotnet test`
- Đảm bảo 100% tests PASS và 0 lỗi build.

### Bước 4: Commit & Push
- Kiểm tra `git status` đảm bảo chỉ stage các file thuộc scope.
- Commit với message định dạng `fix(...)`:
  ```bash
  git commit -m "fix(<component>): <brief description of fix>"
  ```
- Push lên branch hiện tại: `git push`
- **Ràng buộc:** Không force push, không merge main.

### Bước 5: Báo cáo kết quả
Xuất báo cáo gồm:

- **Current Branch:** [Tên branch]
- **New Commit SHA:** [Mã commit SHA mới]
- **Review Findings Addressed:** [Tóm tắt các vấn đề đã sửa theo feedback]
- **Files Modified:** [Danh sách file đã sửa]
- **Build / Test Result:** [Kết quả chạy build & test]
