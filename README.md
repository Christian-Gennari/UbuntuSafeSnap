[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23fb5f80.svg)](https://www.conventionalcommits.org/en/v1.0.0/)

# UbuntuSafeSnap

A .NET 10 console application for backing up and restoring Ubuntu system configurations, with smart exclusion of sensitive files and interactive conflict resolution.

## What it does

UbuntuSafeSnap automates the full backup and restore lifecycle for Ubuntu system configurations:

### Backup

- **Package Extraction**: Captures the list of manually installed packages via `apt-mark showmanual`.
- **Config Collection**: Recursively collects configuration files from user-defined directories.
- **Smart Exclusion**: Automatically skips sensitive files like `.env`, `.key`, `.pem`, and `secrets.*` based on configurable rules.
- **Manifest Generation**: Records the original source directory for each file, enabling accurate restoration paths.
- **Archive Generation**: Bundles everything into a timestamped `.zip` archive for easy storage or migration.

### Restore

- **Root Verification**: Requires `sudo` to run — checks that the user is root before proceeding.
- **Package Re-installation**: Parses `packages.txt` from the archive and reinstalls packages via `apt install -y`.
- **File Restoration**: Restores config files to their original locations using `manifest.txt`. Files that don't exist on the system are copied directly. Files that already exist are handled by the conflict resolver.
- **Interactive Conflict Resolution**: When a file already exists on the system and differs from the backup, an interactive menu lets you choose:
  - **Overwrite** — Replace the system file with the backup version (requires confirmation)
  - **Skip** — Keep the existing system file unchanged
  - **View Diff** — Show a git-style inline diff comparing the two versions, then re-prompt
  - **Abort Restore** — Stop the restore process immediately
- **SHA256 Comparison**: Identical files are automatically skipped without prompting.

## Requirements

- **SDK**: .NET 10.0
- **Platform**: Ubuntu/Debian (required for `apt-mark` and `apt install`)
- **Root access**: Required for `restore` (run with `sudo`)

## Quick Start

### Create a backup

```bash
dotnet build
dotnet run --project UbuntuSafeSnap backup
```

On first run, `targets.txt` and `exclusions.txt` are auto-generated. Edit them to customize which directories to back up and which files to exclude, then re-run.

### Restore from a backup

```bash
sudo dotnet run --project UbuntuSafeSnap restore ubuntusafesnap-20260508-175127.zip
```

The `<file>` argument must be a `.zip` archive created by `UbuntuSafeSnap backup`. The restore process will:

1. Verify you are running as root
2. Extract the archive to a temporary staging directory
3. Reinstall packages listed in `packages.txt`
4. Restore config files to their original locations, prompting for any conflicts

### Non-interactive backup (e.g. cron)

```bash
dotnet run --project UbuntuSafeSnap backup --non-interactive --config-path /path/to/config/
```

The `--non-interactive` flag skips prompts and exits with an error code if config files are missing. Only available for `backup` — restore is always interactive since it requires user decisions on file conflicts.

## Configuration

### Targets (`targets.txt`)

List directories to include in the backup, one per line. Supports `~` expansion for home directories and `#` for comments.

```
# Add directories here, one per line
/etc/NetworkManager
~/.config
```

The home directory (`~` or `/home/user`) is blocked as a target to prevent accidentally backing up the entire home directory.

### Exclusions (`exclusions.txt`)

Configure which files or extensions to skip.

- Prefix with `.` for extension matching (e.g., `.env`, `.key`, `.pem`)
- Use full filenames for specific file exclusion (e.g., `secrets.json`, `secrets.lua`)
- Lines starting with `#` are comments

### Manifest (`manifest.txt`)

Auto-generated during backup. Records the mapping `<source_directory>|<relative_path>` for each file, enabling restore to place files back in their original locations. Do not edit or delete this file from the archive.

## Architecture

The application follows a service-oriented architecture using .NET Dependency Injection:

| Service | Responsibility |
|---------|---------------|
| **TargetResolverService** | Resolves and expands paths from `targets.txt`; blocks home directory as target |
| **PackageService** | Runs `apt-mark showmanual` and writes `packages.txt` to the staging directory |
| **ConfigService** | Recursively collects config files from target directories, applies exclusions, writes `manifest.txt` |
| **ExclusionService** | Evaluates files against extension and filename rules in `exclusions.txt` |
| **ArchiveService** | Creates `.zip` archives from the staging directory and cleans up |
| **RestoreService** | Extracts archives, reinstalls packages, restores files using `manifest.txt` paths |
| **ConflictResolverService** | Compares SHA256 hashes of conflicting files and provides an interactive Spectre.Console menu for resolution |

## Development

For contribution guidelines, git workflow, and commit conventions, see [WORKFLOW.md](WORKFLOW.md).

## License

This project is licensed under the GNU General Public License v3.0 — see the [LICENSE.md](LICENSE.md) file for details.