[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23fb5f80.svg)](https://www.conventionalcommits.org/en/v1.0.0/)

# UbuntuSafeSnap

A .NET 10 console application for creating safe backups of Ubuntu system configurations while excluding sensitive files.

## What it does

UbuntuSafeSnap automates the collection of system configuration files and installed package lists into a single, portable backup archive.

- **Package Extraction**: Captures the list of manually installed packages via `apt-mark showmanual`.
- **Config Collection**: Recursively collects configuration files from user-defined directories.
- **Smart Exclusion**: Automatically skips sensitive files like `.env`, `.key`, `.pem`, and `secrets.*` based on configurable rules.
- **Archive Generation**: Bundles everything into a timestamped `.zip` archive for easy storage or migration.

## Requirements

- **SDK**: .NET 10.0
- **Platform**: Ubuntu/Debian (required for `apt-mark` package list extraction)

## Quick Start

1. **Clone and Build**:
   ```bash
   git clone https://github.com/EduEdugrade/net25-kurs-5-valfri-Christian-Gennari.git
   cd net25-kurs-5-valfri-Christian-Gennari
   dotnet build
   ```

2. **Configure Targets**:
   Edit `targets.txt` to include the directories you want to back up (one per line).

3. **Run**:
   ```bash
   dotnet run --project UbuntuSafeSnap
   ```

## Configuration

### Targets (`targets.txt`)
List directories to include in the backup. Supports `~` expansion for home directories and `#` for comments.

### Exclusions (`exclusions.txt`)
Configure which files or extensions to skip. 
- Prefix with `.` for extension matching (e.g., `.log`).
- Use full filenames for specific file exclusion (e.g., `config.old`).

## Architecture

The application follows a service-oriented architecture:

- **TargetResolverService**: Resolves and expands paths from `targets.txt`.
- **PackageService**: Handles `apt-mark` process execution and output capture.
- **ConfigService**: Manages recursive file discovery and staging.
- **ExclusionService**: Evaluates files against rules in `exclusions.txt`.
- **ArchiveService**: Handles `.zip` creation and workspace cleanup.

## Development

For information on contribution guidelines, git workflow, and commit conventions, see [WORKFLOW.md](WORKFLOW.md).

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE.md](LICENSE.md) file for details.
