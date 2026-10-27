---
name: move-usings-to-global
description: Consolidates per-file C# using statements into a GlobalUsings.cs file per project. Use when the user asks to move usings to global usings.
disable-model-invocation: true
argument-hint: "[root-path]"
allowed-tools: Bash(python3 *), Bash(python *)
---

# Move usings to GlobalUsings.cs

Root path: `$ARGUMENTS` (use the current directory if empty).

## Steps

1. Run a preview first. Do not skip this step:
   `python3 ${CLAUDE_SKILL_DIR}/move_usings_to_global.py --root-path "<root>" --whatif`
2. Summarize the preview: projects found, number of files to update, number of global usings per project.
3. Ask the user to confirm before making changes.
4. After confirmation, run the same command without `--whatif`.
5. Run `dotnet build` and report any errors.

## Rules

- Do not edit the .cs files manually. The script handles all changes.
- Alias usings and `using static` directives are intentionally not moved.
- Any `Migrations` folder (at any depth, in every project, including Clean Architecture and CQRS layouts) is excluded by default. Generated EF migration files are never touched.