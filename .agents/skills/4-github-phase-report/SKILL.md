---
name: 4-github-phase-report
description: "Quy trình tổng hợp báo cáo kết thúc phase phát triển và đề xuất bước tiếp theo."
---

# GitHub Phase Report Workflow

**Announce:** "Starting /4-github-phase-report — Tổng hợp Báo cáo Phase"

## Quy trình thực hiện

1. Thu thập thông tin repo, branch, commit SHA hiện tại và commit SHA trước đó.
2. Tổng hợp danh sách file đã thay đổi, trạng thái build, kết quả unit tests.
3. Liệt kê các hạn chế/vấn đề tồn tại chưa xử lý ở phase hiện tại.
4. Đề xuất kế hoạch và hướng tích hợp cho phase tiếp theo.

## Định dạng báo cáo bắt buộc

===================================

PHASE REPORT

===================================

Repository: [Tên Repository]

Branch: [Tên Branch]

Commit SHA: [Mã Commit SHA mới nhất]

Previous SHA: [Mã Commit SHA trước đó]

Files changed:
[Danh sách file đã thay đổi]

Build: [Trạng thái build: SUCCESS / FAILED]

Tests: [Kết quả chạy tests: X/Y Passed]

Known issues:
[Các rủi ro, hạn chế hoặc vấn đề chưa xử lý ở phase này]

Next phase:
[Đề xuất điểm tích hợp và kế hoạch triển khai cho phase tiếp theo]

===================================
