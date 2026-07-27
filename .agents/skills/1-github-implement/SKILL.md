---
name: 1-github-implement
description: "Quy trình thực thi feature/fix trên branch riêng, kiểm thử, commit và push lên GitHub. Không merge main."
---

# GitHub Implementation Workflow

**Announce:** "Starting /1-github-implement — Quy trình thực thi & đẩy code GitHub"

## Quy trình thực hiện

### Bước 1: Thực thi & Kiểm thử
- Viết code và tests theo yêu cầu/plan.
- Chạy **Build**: `dotnet build`
- Chạy **Test**: `dotnet test`
- Đảm bảo build thành công và tất cả tests pass trước khi tiếp tục.

### Bước 2: Commit & Push
- Kiểm tra trạng thái Git: `git status`
- Stage các file thuộc scope công việc (không stage file ngoài scope).
- Tạo commit với message rõ ràng theo chuẩn Conventional Commits (ví dụ: `feat(...)`, `fix(...)`).
- Push branch lên remote repository: `git push -u origin <branch-name>`
- **Ràng buộc:** KHÔNG merge vào `main`, KHÔNG `force push`.

### Bước 3: Báo cáo kết quả
Sau khi push thành công, trả về báo cáo theo chuẩn:

- **Branch:** [Tên branch hiện tại]
- **Commit SHA:** [Mã commit SHA]
- **Files changed:** [Danh sách các file đã sửa/tạo]
- **Build result:** [Kết quả build]
- **Test result:** [Kết quả unit tests]
- **Risks:** [Rủi ro hoặc điểm lưu ý]
- **Next step:** [Bước tiếp theo]
