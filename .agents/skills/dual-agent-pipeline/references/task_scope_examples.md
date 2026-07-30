# Task Scope Examples

When mapping human-readable intents into proper scope boundaries (`--allowed` parameter) during the initialization step (`dual-init`), use the following guidelines:

## Example 1: Specific Feature in a Subdirectory

**Intent:** "Fix the BCF export issue metadata in Navisworks."
**Command Translation:**
```text
Project: navis
Task: bcf_export_fix
Allowed: src/Navisworks/BCFExport/**, tests/Navisworks/BCFExport/**
```

## Example 2: Broad Refactoring (Using Default)

**Intent:** "Refactor the entire codebase to use the new logging pattern."
**Command Translation:**
```text
Project: revit
Task: logging_refactor
Allowed: (Omitted - let it fall back to project_profile.json/stage_patterns)
```

## Example 3: Narrow File-Specific Scope

**Intent:** "Update the configuration schema to support MaxCycles."
**Command Translation:**
```text
Project: test_project
Task: schema_update
Allowed: src/config/schema.json, src/config/parser.py, tests/test_parser.py
```

## Guidelines
- **Always be as narrow as possible** to prevent scope creep.
- **Include tests** inside the allowed boundary so TDD can be enforced.
- **Do not include build artifacts** or `.agent` directories in allowed scopes.
