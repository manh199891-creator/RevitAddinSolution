---
name: 3-code
description: "Phase 3 — Thực thi, Test & Debug. Dùng sau khi /2.plan xong. Gộp /code /test /debug /refactor /review /run."
---

# Phase 3 — Code

**Announce:** "Starting /3.code — Phase 3: Thực thi, Test & Debug"

## Quy trình bắt buộc (vòng lặp tự động)

### Pre-flight
1. Isolate workspace — dùng git worktree hoặc branch mới
2. Đọc plan tại `docs/superpowers/plans/` nếu có

### TDD Iron Law — RED → GREEN → REFACTOR
```
KHÔNG có production code nếu chưa có failing test.
Viết code trước test? Xóa đi, làm lại.
```

**Mỗi task chạy theo vòng:**
1. **RED:** Viết failing test → chạy → verify FAIL
2. **GREEN:** Viết minimal code để pass → chạy → verify PASS  
3. **REFACTOR:** Dọn dẹp code, không thay đổi behavior
4. **Commit** với caveman-commit style (≤50 chars)

### Subagent Execution
- Task phức tạp → dispatch fresh subagent per task
- 2+ independent tasks → dispatch parallel
- Subagent báo cáo → review → fix nếu cần → next task

### Debug Protocol
Nếu có lỗi → **KHÔNG fix ngay**:
1. Root cause investigation trước
2. Pattern analysis (lỗi có lặp lại không?)
3. Hypothesis → test hypothesis
4. Implement fix sau khi xác nhận root cause

### Production/Security Code
Nếu chạm vào production hoặc code nhạy cảm → adversarial self-review:
- Giả định attacker đọc code này sẽ exploit chỗ nào?
- Các edge case nào chưa được cover?

### Verification (trước khi báo xong)
**KHÔNG claim "done" nếu chưa:**
- Chạy full test suite → thấy output thực tế
- Kiểm tra không có regression
- "Should work" / "probably" = violation

### Sau mỗi task
Request code review trước khi chuyển task tiếp theo.
Nhận feedback → đánh giá kỹ thuật → không comply blindly.
