[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23fb5f80.svg)](https://www.conventionalcommits.org/en/v1.0.0/)

# UbuntuSafeSnap

A .NET 10 self-contained executable for backing up and restoring Ubuntu system configurations. Back up your packages and dotfiles on one machine, fresh-install Ubuntu, and restore everything as it was.

## Features

### Backup

- Captures manually installed packages via `apt-mark showmanual`
- Recursively collects config files from user-defined directories
- Skips sensitive files (`.env`, `.key`, `.pem`, `secrets.*`) and directories (`node_modules/`, `.cache/`, `.git/`) based on configurable rules
- Skips symlinks, broken symlinks, sockets, and pipes gracefully
- Records source directories in `manifest.txt` for accurate restore paths
- Bundles everything into a timestamped `.zip` archive in `./backups/`
- Prunes old backups beyond the `--keep` count (default: 5)
- Warns if run as root without `sudo` (home directory targets would resolve to `/root`)

### Restore

- Requires `sudo` — verifies root privileges before proceeding
- Reinstalls packages from `packages.txt` via `apt install -y`
- Restores config files to their original locations using `manifest.txt`
- Skips identical files automatically (SHA256 comparison)
- Presents an interactive backup selection menu if no file is specified

<details>
<summary>Conflict resolution details</summary>

When a file already exists on the system and differs from the backup, an interactive menu offers:

- **Overwrite** — Replace the system file with the backup version (requires confirmation)
- **Skip** — Keep the existing system file unchanged
- **View Diff** — Show an inline diff comparing the two versions (powered by DiffPlex), then re-prompt
- **Abort Restore** — Stop the restore process immediately

For files larger than 1 MB, diffs are truncated to the first 50 lines. In non-interactive terminals, conflicts that require user input cause an automatic abort with a message to re-run interactively.

</details>

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
gh release download --repo EduEdugrade/net25-kurs-5-valfri-Christian-Gennari \
  --pattern UbuntuSafeSnap --dir ~/UbuntuSafeSnap
chmod +x ~/UbuntuSafeSnap/UbuntuSafeSnap
```

<details>
<summary>Alternative download methods (wget / curl)</summary>

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

</details>

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

The home directory (`~` or `/home/user`) is blocked as a target to prevent accidentally backing up the entire home directory.

<details>
<summary>Default targets.txt</summary>

```
# Directories to back up, one per line. ~ expands to your home directory.
# Lines starting with # are comments and will be ignored.

~/.config
~/.bashrc
~/.profile
~/.ssh
/etc/NetworkManager
```

</details>

### Exclusions (`exclusions.txt`)

Configure which files, extensions, and directories to skip:

- **Extension rules** start with `.` — match any file with that extension
- **Filename rules** are just the name — match any file with that exact name
- **Directory rules** end with `/` — skip entire directory trees by name

<details>
<summary>Default exclusions.txt</summary>

```
# Files matching these patterns will be excluded from backups.
# Extension rules start with .  (e.g. .env, .key, .pem)
# Filename rules are just the name (e.g. secrets.json)
# Directory rules end with /  (e.g. node_modules/) to skip entire trees
# Lines starting with # are comments and will be ignored.

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
node_modules/
.cache/
__pycache__/
.git/
```

</details>

### Manifest (`manifest.txt`)

Auto-generated during backup. Records the mapping `<source_directory>|<relative_path>` for each file, enabling restore to place files back in their original locations. Do not edit or delete this file from the archive.

<details>
<summary>Architecture</summary>

The application uses a service-oriented architecture with .NET Dependency Injection, organized into the following namespaces:

**`Commands/`** — CLI command handlers (System.CommandLine)

| Command | Description |
|---------|-------------|
| `BackupCommand` | Orchestrates the backup pipeline: resolve targets → extract packages → collect configs → create archive → prune old backups |
| `RestoreCommand` | Handles backup selection (interactive or via argument), then delegates to `RestoreService` |
| `InitCommand` | Scaffolds `targets.txt` and `exclusions.txt` with default values |

**`Services/Backup/`** — Backup pipeline services

| Service | Description |
|---------|-------------|
| `PackageService` | Runs `apt-mark showmanual` and writes `packages.txt` |
| `ConfigService` | Recursively collects config files, applies exclusions, skips symlinks/special files, writes `manifest.txt` |
| `ArchiveService` | Creates `.zip` archives, cleans up staging, and prunes old backups beyond the `--keep` limit |

**`Services/Shared/`** — Shared utilities

| Service | Description |
|---------|-------------|
| `TargetResolverService` | Resolves and expands paths from `targets.txt`; blocks home directory as target; resolves `~` to real user home under `sudo` |
| `ExclusionService` | Evaluates files against extension, filename, and directory rules; `ShouldExclude` for files, `ShouldExcludeDirectory` for directory trees |

**`Services/Restore/`** — Restore pipeline services

| Service | Description |
|---------|-------------|
| `RestoreService` | Extracts archives, reinstalls packages, restores files using `manifest.txt` paths |
| `ConflictResolverService` | Compares SHA256 hashes of conflicting files; provides interactive Spectre.Console menu with inline DiffPlex diffs; handles large files (>1MB) with partial diffs; auto-aborts in non-interactive terminals |

**`UI/`** — Console interface utilities

| Class | Description |
|-------|-------------|
| `Log` | Structured console output using Spectre.Console markup |
| `ConsolePrompt` | Interactive selection prompts, confirmations, and terminal capability detection |

**`Models/`** — Data models

| Model | Description |
|-------|-------------|
| `ConflictResolution` | Enum: `Overwrite`, `Skip`, `Identical`, `Abort` |

</details>

## Development

For contribution guidelines, git workflow, and commit conventions, see [WORKFLOW.md](WORKFLOW.md).

## License

This project is licensed under the GNU General Public License v3.0 — see the [LICENSE.md](LICENSE.md) file for details.