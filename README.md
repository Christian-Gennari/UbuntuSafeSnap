[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23fb5f80.svg)](https://www.conventionalcommits.org/en/v1.0.0/)

# UbuntuSafeSnap

A .NET 10 self-contained executable for backing up and restoring Ubuntu system configurations. Designed for machine migration — back up your packages and dotfiles on one machine, fresh-install Ubuntu, and restore everything as it was.

## What it does

### Backup

- **Package Extraction**: Captures the list of manually installed packages via `apt-mark showmanual`.
- **Config Collection**: Recursively collects configuration files from user-defined directories.
- **Smart Exclusion**: Automatically skips sensitive files like `.env`, `.key`, `.pem`, and `secrets.*` based on configurable rules.
- **Manifest Generation**: Records the original source directory for each file, enabling accurate restoration paths.
- **Archive Generation**: Bundles everything into a timestamped `.zip` archive stored in `./backups/`.

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
- **Interactive Backup Selection**: If you run restore without specifying a file, a menu lets you pick from available backups in `./backups/`.

## Requirements

- **SDK**: .NET 10.0 (for building from source)
- **Platform**: Ubuntu/Debian (required for `apt-mark` and `apt install`)
- **Root access**: Required for `restore` (run with `sudo`)

## Build from source

Run the install script to clean, build, and deploy the self-contained binary:

```bash
chmod +x install.sh
./install.sh                # installs to ~/UbuntuSafeSnap/
./install.sh /custom/path   # installs to a custom directory
```

## Quick Start

### 1. Initialize config files

```bash
cd ~/UbuntuSafeSnap
./ubuntusafesnap init
# Edit targets.txt and exclusions.txt to your needs
```

### 2. Create a backup

```bash
./ubuntusafesnap backup
```

Creates `backups/ubuntusafesnap-YYYYMMdd-HHmmss.zip`.

### 3. Restore from a backup

```bash
sudo ./ubuntusafesnap restore
# Interactive selection from ./backups/
```

Or specify a file directly:

```bash
sudo ./ubuntusafesnap restore backups/ubuntusafesnap-20260509-123456.zip
```

## Configuration

### Targets (`targets.txt`)

List directories to include in the backup, one per line. Supports `~` expansion for home directories and `#` for comments.

```
# Directories to back up, one per line. ~ expands to your home directory.
~/.config
~/.local/share
~/.bashrc
~/.profile
/etc/NetworkManager
```

The home directory (`~` or `/home/user`) is blocked as a target to prevent accidentally backing up the entire home directory.

### Exclusions (`exclusions.txt`)

Configure which files or extensions to skip.

```
# Files matching these patterns will be excluded from backups.
# Extension rules start with .  (e.g. .env, .key, .pem)
# Filename rules are just the name (e.g. secrets.json)

.env
.key
.pem
secrets.json
secrets.lua
```

### Manifest (`manifest.txt`)

Auto-generated during backup. Records the mapping `<source_directory>|<relative_path>` for each file, enabling restore to place files back in their original locations. Do not edit or delete this file from the archive.

## Architecture

The application follows a service-oriented architecture using .NET Dependency Injection:

| Service | Responsibility |
|---------|---------------|
| **InitService** | Scaffolds `targets.txt` and `exclusions.txt` with sensible defaults |
| **TargetResolverService** | Resolves and expands paths from `targets.txt`; blocks home directory as target |
| **PackageService** | Runs `apt-mark showmanual` and writes `packages.txt` to the staging directory |
| **ConfigService** | Recursively collects config files from target directories, applies exclusions, writes `manifest.txt` |
| **ExclusionService** | Evaluates files against extension and filename rules in `exclusions.txt` |
| **ArchiveService** | Creates `.zip` archives in `./backups/` and cleans up `./staging/` |
| **RestoreService** | Extracts archives, reinstalls packages, restores files using `manifest.txt` paths |
| **ConflictResolverService** | Compares SHA256 hashes of conflicting files and provides an interactive Spectre.Console menu for resolution |

## Development

For contribution guidelines, git workflow, and commit conventions, see [WORKFLOW.md](WORKFLOW.md).

## License

This project is licensed under the GNU General Public License v3.0 — see the [LICENSE.md](LICENSE.md) file for details.
