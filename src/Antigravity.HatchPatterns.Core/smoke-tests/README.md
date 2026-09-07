# Antigravity.HatchPatterns.Core Smoke Contract

Classification: Shared/core library

This contract is intentionally truthful: checks are requirements, not PASS claims. A check becomes PASS only after actual execution evidence exists.

Required checks:
- `S0_STRUCTURAL`
- `S1_BUILD_TEST`

Last-known-good identity is stored in aseline/last-known-good.json. Until verified, it remains PENDING_CAPTURE or PENDING_REVALIDATION.
Generated smoke output belongs in esults/ and is Git ignored.