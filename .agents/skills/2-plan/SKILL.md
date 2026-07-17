---
name: 2-plan
description: "Phase 2 — Lên kế hoạch thực thi. Dùng sau khi /1.spec được approve. Gộp /plan."
---

# Phase 2 — Plan

**Announce:** "Starting /2.plan — Phase 2: Lên kế hoạch thực thi"

## Quy trình bắt buộc

### Bước 1: Scope Check
Nếu spec cover nhiều subsystem độc lập → tách thành nhiều plan riêng.
Mỗi plan phải produce working, testable software on its own.

### Bước 2: File Structure
Map ra file nào sẽ được tạo/sửa và trách nhiệm của từng file.
- Exact paths, không dùng placeholder
- Nếu library/framework lạ → đọc official docs trước khi plan

### Bước 3: Implementation Plan Document
Lưu tại: `docs/superpowers/plans/YYYY-MM-DD-<feature-name>.md`

Format bắt buộc cho mỗi task:
```
### Task N: [Tên]
**Files:** Create/Modify/Test
**Steps:**
- [ ] Write failing test (RED)
- [ ] Run test → verify FAIL
- [ ] Write minimal implementation (GREEN)  
- [ ] Run test → verify PASS
- [ ] Commit
```

**Iron Laws:**
- KHÔNG placeholder (TBD, TODO, "implement later")
- KHÔNG step mô tả mà không có code
- DRY + YAGNI + TDD trong mọi task

### Bước 4: Parallel Detection
Nếu có 2+ task hoàn toàn độc lập → đánh dấu để dispatch parallel agents ở phase sau.

### Kết thúc
Offer user 2 lựa chọn execution:
1. **Subagent-Driven** (recommended) — fresh subagent/task, review giữa tasks
2. **Inline** — execute trong session này với checkpoints
