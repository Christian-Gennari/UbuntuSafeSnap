[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23fb5f80.svg)](https://www.conventionalcommits.org/en/v1.0.0/)

# UbuntuSafeSnap

A .NET 10 self-contained executable for backing up and restoring Ubuntu system configurations. Designed for machine migration — back up your packages and dotfiles on one machine, fresh-install Ubuntu, and restore everything as it was.

## What it does

### Backup

- **Package Extraction**: Captures the list of manually installed packages via `apt-mark showmanual`.
- **Config Collection**: Recursively collects configuration files from user-defined directories.
- **Smart Exclusion**: Automatically skips sensitive files like `.env`, `.key`, `.pem`, and `secrets.*` based on configurable rules.
- **Symlink and Special File Handling**: Skips symlinks, broken symlinks, sockets, and pipes gracefully instead of crashing.
- **Manifest Generation**: Records the original source directory for each file, enabling accurate restoration paths.
- **Archive Generation**: Bundles everything into a timestamped `.zip` archive stored in `./backups/`.
- **Backup Pruning**: The `--keep` option (default: 5) automatically removes old backups beyond the specified count.

### Restore

- **Root Verification**: Requires `sudo` to run — checks that the user is root before proceeding.
- **Package Re-installation**: Parses `packages.txt` from the archive and reinstalls packages via `apt install -y`.
- **File Restoration**: Restores config files to their original locations using `manifest.txt`. Files that don't exist on the system are copied directly. Files that already exist are handled by the conflict resolver.
- **Interactive Conflict Resolution**: When a file already exists on the system and differs from the backup, an interactive menu lets you choose:
  - **Overwrite** — Replace the system file with the backup version (requires confirmation)
  - **Skip** — Keep the existing system file unchanged
  - **View Diff** — Show a git-style inline diff comparing the two versions, then re-prompt
  - **Abort Restore** — Stop the restore process immediately
- **SHA256 Comparison**: Identical files are automatically skipped and logged as "Skipped (identical)", while user-initiated skips are logged separately.
- **Interactive Backup Selection**: If you run restore without specifying a file, a menu lets you pick from available backups in `./backups/`.

## Requirements

- **Platform**: Ubuntu/Debian (required for `apt-mark` and `apt install`)
- **Root access**: Required for `restore` (run with `sudo`). The `backup` command does not require `sudo` and will warn if run as root.
- **SDK**: .NET 10.0 (only required when building from source)

## Installation

### Option 1: Download the binary (recommended)

No .NET SDK required — just download and run.

Using GitHub CLI:
```bash
mkdir -p ~/UbuntuSafeSnap
gh release download --repo EduEdugrade/net25-kurs-5-valfri-Christian-Gennari --pattern UbuntuSafeSnap --dir ~/UbuntuSafeSnap
chmod +x ~/UbuntuSafeSnap/UbuntuSafeSnap
```

Using wget:
```bash
mkdir -p ~/UbuntuSafeSnap
wget -qO ~/UbuntuSafeSnap/UbuntuSafeSnap https://github.com/EduEdugrade/net25-kurs-5-valfri-Christian-Gennari/releases/latest/download/UbuntuSafeSnap
chmod +x ~/UbuntuSafeSnap/UbuntuSafeSnap
```

Using curl:
```bash
mkdir -p ~/UbuntuSafeSnap
curl -sL -o ~/UbuntuSafeSnap/UbuntuSafeSnap https://github.com/EduEdugrade/net25-kurs-5-valfri-Christian-Gennari/releases/latest/download/UbuntuSafeSnap
chmod +x ~/UbuntuSafeSnap/UbuntuSafeSnap
```

> **Note:** If the repository has OAuth App access restrictions, `wget` and `curl` may return a 404 error. In that case, use the GitHub CLI method or download the binary manually from [GitHub Releases](https://github.com/EduEdugrade/net25-kurs-5-valfri-Christian-Gennari/releases/latest).

### Option 2: Build from source

Requires .NET 10.0 SDK. Run the install script to clean, build, and deploy:

```bash
bash install.sh                # installs to ~/UbuntuSafeSnap/
bash install.sh /custom/path   # installs to a custom directory
```

## Quick Start

### 1. Initialize config files

```bash
cd ~/UbuntuSafeSnap
./UbuntuSafeSnap init
# Edit targets.txt and exclusions.txt to your needs
```

### 2. Create a backup

```bash
./UbuntuSafeSnap backup
```

Creates `backups/ubuntusafesnap-YYYYMMdd-HHmmss.zip`.

You can limit the number of backups kept by using the `--keep` option (defaults to 5):

```bash
./UbuntuSafeSnap backup --keep 3    # keep only the 3 most recent backups
```

### 3. Restore from a backup

```bash
sudo ./UbuntuSafeSnap restore
# Interactive selection from ./backups/
```

Or specify a file directly:

```bash
sudo ./UbuntuSafeSnap restore backups/ubuntusafesnap-20260509-123456.zip
```

### 4. Scheduled backups

To run automatic weekly backups, add a cron entry:

```bash
crontab -e
```

Add the following line for a weekly backup every Sunday at 02:00, keeping the 5 most recent:

```
0 2 * * 0 ~/UbuntuSafeSnap/UbuntuSafeSnap backup --keep 5 >> ~/UbuntuSafeSnap/cron.log 2>&1
```

## Configuration

### Targets (`targets.txt`)

List directories to include in the backup, one per line. Supports `~` expansion for home directories and `#` for comments. When running under `sudo`, `~` resolves to the real user's home directory (not `/root`).

```
# Directories to back up, one per line. ~ expands to your home directory.
~/.config
~/.bashrc
~/.profile
~/.ssh
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
.log
.lock
.pid
.db
.sqlite
secrets.json
secrets.lua
id_rsa
id_ed25519
id_ecdsa
```

### Manifest (`manifest.txt`)

Auto-generated during backup. Records the mapping `<source_directory>|<relative_path>` for each file, enabling restore to place files back in their original locations. Do not edit or delete this file from the archive.

## Architecture

The application follows a service-oriented architecture using .NET Dependency Injection:

| Service | Responsibility |
|---------|---------------|
| **InitService** | Scaffolds `targets.txt` and `exclusions.txt` with sensible defaults |
| **TargetResolverService** | Resolves and expands paths from `targets.txt`; blocks home directory as target; resolves `~` to real user home under `sudo` |
| **PackageService** | Runs `apt-mark showmanual` and writes `packages.txt` to the staging directory |
| **ConfigService** | Recursively collects config files from target directories, applies exclusions, skips symlinks and special files, writes `manifest.txt` |
| **ExclusionService** | Evaluates files against extension and filename rules in `exclusions.txt` |
| **ArchiveService** | Creates `.zip` archives in `./backups/`, cleans up `./staging/`, and prunes old backups beyond the `--keep` limit |
| **RestoreService** | Extracts archives, reinstalls packages, restores files using `manifest.txt` paths |
| **ConflictResolverService** | Compares SHA256 hashes of conflicting files and provides an interactive Spectre.Console menu for resolution |

## Development

For contribution guidelines, git workflow, and commit conventions, see [WORKFLOW.md](WORKFLOW.md).

## License

This project is licensed under the GNU General Public License v3.0 — see the [LICENSE.md](LICENSE.md) file for details.
