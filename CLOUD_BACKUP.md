# Cloud Backup

Backups in `./backups/` are local to the machine. If the drive fails, they're gone. This guide covers syncing backups to cloud storage.

## Prerequisites

- [rclone](https://rclone.org/) installed (`sudo apt install rclone`)
- A remote configured (`rclone config`)

## Manual sync

```bash
# copy: uploads new/changed files only, never deletes from remote
#       (old backups survive in the cloud even after local --keep pruning)
rclone copy ./backups/ myremote:ubuntusafesnap-backups/

# sync: mirrors the local backups/ directory exactly
#       (deleted locally = deleted from remote — keeps cloud storage tidy)
rclone sync ./backups/ myremote:ubuntusafesnap-backups/
```

## Automated sync (via cron)

Append to your existing backup cron entry (see README):

```
0 2 * * 0 ~/UbuntuSafeSnap/UbuntuSafeSnap backup --keep 5 \
  && rclone copy ~/UbuntuSafeSnap/backups/ myremote:ubuntusafesnap-backups/ \
  >> ~/UbuntuSafeSnap/cron.log 2>&1
```

The `&&` ensures rclone only runs if the backup succeeds. Replace `copy` with `sync` if you want the remote to mirror exactly what's local (deletions included).

## Alternatives

| Tool | Use case |
|------|----------|
| `rsync` | Sync to another machine over SSH |
| `restic` | Encrypted, deduplicated backups |
| `s3cmd` / `awscli` | Direct S3 sync |
