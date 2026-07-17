---
name: 4-ship
description: "Phase 4 — Nghiệm thu & Đóng gói. Dùng sau /3.code. Gộp /deploy /audit /rollback /ship."
---

# Phase 4 — Ship

**Announce:** "Starting /4.ship — Phase 4: Nghiệm thu & Đóng gói"

## Quy trình bắt buộc

### Bước 1: Full Verification
**KHÔNG tiếp tục nếu bất kỳ test nào fail.**
- Chạy toàn bộ test suite → xem output thực tế
- Check không có regression so với trước
- "All tests pass" phải được prove bằng actual command output

### Bước 2: Security Scan
Trước mọi release, scan theo MITRE ATT&CK + NIST:
- Input validation — có sanitize không?
- Authentication/Authorization — ai được làm gì?
- Sensitive data — có leak log/error không?
- Dependencies — có CVE đã biết không?
- Transaction safety (Revit) — mọi write trong Transaction không?

### Bước 3: Integration Options
Chọn một trong 4 options:

**Option 1: Merge to main**
```
git checkout main
git merge --no-ff <branch>
git push
```

**Option 2: Pull Request**
- Tạo PR với description đầy đủ
- Caveman-commit style cho PR title

**Option 3: Keep as-is**
- Giữ branch, không merge
- Document lý do

**Option 4: Discard**
- Xóa branch/worktree
- Document lý do không ship

### Cleanup
- Xóa worktree nếu dùng git worktree (option 1 hoặc 4)
- Archive plan file nếu feature đã hoàn thành
- Update CHANGELOG.md nếu project có
