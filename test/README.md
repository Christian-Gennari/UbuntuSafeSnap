# Multipass Restore Validation

End-to-end validation of `UbuntuSafeSnap restore` using a Multipass Ubuntu VM.

## Prerequisites

- Multipass: `snap install multipass`
- UbuntuSafeSnap binary built: `dotnet build UbuntuSafeSnap`
- At least one backup archive in `./backups/` (created by `./UbuntuSafeSnap backup`)

## Usage

```bash
# Use latest backup archive
bash test/restore-multipass.sh

# Use a specific backup archive
bash test/restore-multipass.sh /path/to/backup.zip
```

## What it validates

| # | Check | What it verifies |
|---|-------|-----------------|
| 1 | Systemd services | Enabled services (ollama, pm2-dev, docker, ssh) |
| 2 | SSH config | `sshd -t` syntax validation |
| 3 | Apt sources | Third-party repo files restored |
| 4 | Third-party packages | docker-ce, azure-cli, gh, micro installed |
| 5 | Docker config | `/etc/docker/daemon.json` restored |
| 6 | Missing packages | `missing-packages.txt` report |
| 7 | Home remapping | Files restored under `/home/ubuntu/` (cross-user) |
| 8 | File count | > 0 files restored |

## Flow

1. Launch Ubuntu Noble VM (4 GB RAM, 10 GB disk)
2. Copy binary + backup archive into VM
3. Run `restore --dry-run` (no sudo required)
4. Run full `restore` with `yes | sudo`
5. Run validation checks against the restored system
6. Clean up (stop, delete, purge VM)

## Success criteria

- Restored files are present under correct (`/home/ubuntu/`) paths
- Systemd services are enabled
- `sshd -t` passes
- Third-party packages install without error
- Apt sources are restored to `/etc/apt/sources.list.d/`
- No unexpected entries in `missing-packages.txt`
