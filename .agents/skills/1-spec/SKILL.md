---
name: 1-spec
description: "Phase 1 — Khởi tạo & Thiết kế. Dùng trước mọi tính năng mới. Gộp /init /brainstorm /design /visualize."
---

# Phase 1 — Spec

**Announce:** "Starting /1.spec — Phase 1: Khởi tạo & Thiết kế"

## Quy trình bắt buộc

KHÔNG viết bất kỳ dòng code nào trong phase này.

### Bước 1: Brainstorming
Lùi lại. Hỏi user để làm rõ intent trước khi làm gì:
- Vấn đề thực sự cần giải quyết là gì?
- Ai là người dùng cuối?
- Constraints nào đang có (tech stack, deadline, compatibility)?
- Có solution nào tương tự đã tồn tại chưa?

### Bước 2: Taste Check (nếu có UI/UX)
Nếu output có liên quan đến giao diện → áp dụng nguyên tắc thiết kế:
- Không AI slop (generic colors, boring layouts)
- Vibrant, purposeful, premium feel
- Consistent với design system hiện tại của project

### Bước 3: Spec Document
Soạn spec document tóm tắt để user approve, gồm:
- **Goal:** 1 câu mô tả mục tiêu
- **Scope:** Những gì IN và OUT of scope
- **Architecture:** Hướng tiếp cận kỹ thuật dự kiến
- **Open Questions:** Các điểm còn mơ hồ cần xác nhận

### Hard Gate
**Chờ user approve spec trước khi chuyển sang `/2.plan`.**
KHÔNG tự ý bắt đầu code hay plan khi chưa có approval.
