---
name: 2-github-review
description: "Quy trình code review so sánh commit cũ và mới trên feature branch, kiểm tra scope, logic, và regression trước khi merge."
---

# GitHub Code Review Workflow

**Announce:** "Starting /2-github-review — Quy trình Code Review commit"

## Input Parameters
- **Repository:** Tên repository
- **Branch:** Tên feature branch đang review
- **Previous Commit:** SHA commit trước
- **Current Commit:** SHA commit hiện tại
- **Review Request:** Yêu cầu kiểm tra chi tiết

## Quy trình thực hiện

### Bước 1: Diff & Scope Inspection
- Thực hiện `git diff <previous_commit> <current_commit>`
- Kiểm tra danh sách file đã thay đổi.
- **Scope Check:** Đảm bảo các file thay đổi nằm đúng phạm vi công việc, không có thay đổi thừa ngoài scope.

### Bước 2: Logic & Architecture Review
- Rà soát logic thay đổi theo các tiêu chuẩn:
  - Có đúng thuật toán/nghiệp vụ yêu cầu không?
  - Xử lý các edge cases (null, zero-length, duplicate, skews) có ổn định không?
  - Có hard-code các tham số không?
  - Có nuốt exception (catch rỗng) không?

### Bước 3: Regression Check
- Kiểm tra xem các hàm hoặc module xung quanh có bị ảnh hưởng không.
- Đảm bảo các test case cũ và mới đều pass.
- Đảm bảo không thay đổi API contract hoặc hành vi phía ngoài khi chưa được duyệt.

### Bước 4: Báo cáo Review Result
Xuất báo cáo theo định dạng:

- **Review Summary:** Tóm tắt thay đổi giữa Previous → Current SHA.
- **Scope Audit:** Đã thay đổi đúng scope hay chưa.
- **Logic & Correctness:** Đánh giá thuật toán, edge cases.
- **Regression Analysis:** Đánh giá nguy cơ gây lỗi liên đới.
- **Status:** PASS / FAIL / NEEDS_FIX.
- **Next Recommendation:** Đề xuất bước tiếp theo (chưa merge main).
