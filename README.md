[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23fb5f80.svg)](https://www.conventionalcommits.org/en/v1.0.0/)

# UbuntuSafeSnap

A .NET 10 console application for creating safe, encrypted backups of Ubuntu system configurations while excluding sensitive files.

## Git Workflow Rules (CRITICAL)

NEVER push directly to `main`. All changes must come through PRs.

1. Create GitHub Issue with type label (feature, bug, etc.)
2. Create branch from issue using GitHub's "Create a branch" feature
3. Develop and commit following Conventional Commits
4. Open Pull Request with descriptive title and body
5. Merge via PR only

Note: Enabled branch protection for main branch

## Conventional Commits Reference

Format: `<type>[optional scope]: <description>`

| Type     | Purpose                                           | Example                                    |
|----------|---------------------------------------------------|--------------------------------------------|
| feat     | Introduces a new feature                           | feat: add package extraction service       |
| fix      | Patches a bug in your codebase                     | fix: resolve permission denied error        |
| docs     | Documentation only changes                         | docs: update installation guide            |
| style    | Code style (formatting, semicolons, etc.)        | style: format indentation in Program.cs    |
| refactor | Code change that neither fixes bug nor adds feat | refactor: simplify exclusion logic         |
| perf     | Performance improvement                           | perf: optimize file traversal speed        |
| test     | Adding or correcting tests                        | test: add unit tests for ArchiveService    |
| build    | Build system or external dependencies              | build: update .NET SDK version             |
| ci       | CI configuration files and scripts                 | ci: add GitHub Actions workflow            |
| chore    | Other changes not modifying src or test files     | chore: update .gitignore                   |

## Assignment Requirements Checklist

- [ ] Minimum 5 issues created, used, and closed via PRs
- [ ] Minimum 15 meaningful commits
- [ ] Each issue has: title, description, type label
- [ ] Each PR has: descriptive title and body
- [ ] All changes via branches and PRs (zero direct pushes to main)
- [ ] Main branch named `main`