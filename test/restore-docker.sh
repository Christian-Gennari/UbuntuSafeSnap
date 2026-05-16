#!/usr/bin/env bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────
# UbuntuSafeSnap — Full restore validation with Docker
# ──────────────────────────────────────────────────────────────
# Validates ALL restore logic: manifest parsing, home remapping,
# apt sources, file restoration, packages, systemd services,
# SSH config, third-party packages, Docker daemon config.
# Uses --privileged for full system access.
# ──────────────────────────────────────────────────────────────

PROJECT_DIR="/home/dev/coding/projects/ubuntusafesnap"
# Use the latest backup (prefer the one with systemd/ssh/docker paths)
BACKUP_ZIP="${1:-$(ls -t "$PROJECT_DIR/backups"/ubuntusafesnap-*.zip 2>/dev/null | head -1)}"
BINARY="$PROJECT_DIR/UbuntuSafeSnap/bin/Release/net10.0/linux-x64/publish/UbuntuSafeSnap"

RED='\033[0;31m'; GREEN='\033[0;32m'; NC='\033[0m'
PASS=0; FAIL=0; TOTAL=0

cleanup() {
  docker rm -f uss-restore-test 2>/dev/null || true
  echo ""
  echo "═══════════════════════════════════════════════"
  echo "  Results: ${PASS}/${TOTAL} passed, ${FAIL} failed"
  echo "═══════════════════════════════════════════════"
}
trap cleanup EXIT

check() {
  local desc="$1"; shift
  TOTAL=$((TOTAL + 1))
  printf "  [CHECK] %s ... " "$desc"
  if "$@" 2>/dev/null; then
    echo -e "${GREEN}PASS${NC}"; PASS=$((PASS + 1))
  else
    echo -e "${RED}FAIL${NC}"; FAIL=$((FAIL + 1))
  fi
}

if [ ! -f "$BACKUP_ZIP" ]; then echo "Backup not found: $BACKUP_ZIP"; exit 1; fi
if [ ! -f "$BINARY" ]; then echo "Binary not found: $BINARY"; exit 1; fi

BACKUP_FILE=$(basename "$BACKUP_ZIP")
echo "═══════════════════════════════════════════════"
echo "  UbuntuSafeSnap — Full Docker Restore Test"
echo "═══════════════════════════════════════════════"
echo "  Binary: $BINARY"
echo "  Backup: $BACKUP_ZIP"
echo ""

# ── Launch privileged container ────────────────────────────
echo "▸ Starting privileged Ubuntu 24.04 container..."
CONTAINER_ID=$(docker run -d --privileged --cgroupns=host \
  -v /sys/fs/cgroup:/sys/fs/cgroup:rw \
  -v "$BINARY:/usr/local/bin/UbuntuSafeSnap:ro" \
  -v "$BACKUP_ZIP:/backup/$BACKUP_FILE:ro" \
  ubuntu:24.04 sleep infinity)

docker exec "$CONTAINER_ID" bash -c 'apt-get update -qq && apt-get install -y -qq sudo unzip libicu74 ca-certificates systemd openssh-server >/dev/null 2>&1'
# Clean home directory so restore has no file conflicts
# Clean ALL paths that the restore will write to — avoids non-interactive abort on conflicts
docker exec "$CONTAINER_ID" bash -c 'rm -f /home/ubuntu/.bashrc /home/ubuntu/.profile /home/ubuntu/.bash_logout 2>/dev/null'
docker exec "$CONTAINER_ID" bash -c 'rm -rf /home/ubuntu/.config /home/ubuntu/.ssh 2>/dev/null'
docker exec "$CONTAINER_ID" bash -c 'rm -rf /etc/systemd/system/* 2>/dev/null; mkdir -p /etc/systemd/system'
docker exec "$CONTAINER_ID" bash -c 'rm -f /etc/ssh/sshd_config /etc/ssh/ssh_config /etc/ssh/ssh_import_id /etc/ssh/ssh_host_* 2>/dev/null'
docker exec "$CONTAINER_ID" bash -c 'rm -f /etc/docker/daemon.json 2>/dev/null; mkdir -p /etc/docker'

# ── Phase 1: Dry-run ────────────────────────────────────────
echo "▸ Phase 1: Dry-run restore..."
DRY_OUT=$(docker exec -e DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 "$CONTAINER_ID" \
  UbuntuSafeSnap restore --dry-run "/backup/$BACKUP_FILE" 2>&1) || true
echo "$DRY_OUT" | grep -E "(Dry-Run Summary|Packages:|Files:)"
echo ""

# ── Phase 2: Full restore ───────────────────────────────────
echo "▸ Phase 2: Full restore..."
RESTORE_OUT=$(docker exec -e SUDO_USER=ubuntu -e DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 "$CONTAINER_ID" \
  bash -c "yes | UbuntuSafeSnap restore '/backup/$BACKUP_FILE' 2>&1") || true
echo "$RESTORE_OUT" | grep -E "(Restore complete|aborted|file\(s\) restored|file\(s\) skipped|missing-package)"
echo ""

# ── Phase 3: Validations ────────────────────────────────────
echo "▸ Phase 3: Validations..."

# Write output to temp files for safe parsing
echo "$RESTORE_OUT" > /tmp/restore_out.txt
echo "$DRY_OUT" > /tmp/dry_out.txt

echo ""
echo "  3.1 — Apt sources restored"
check "tailscale source" docker exec "$CONTAINER_ID" test -f /etc/apt/sources.list.d/tailscale.list
check "docker source"    docker exec "$CONTAINER_ID" test -f /etc/apt/sources.list.d/docker.list

echo ""
echo "  3.2 — Home remapping (files under /home/ubuntu)"
check "~/.config has files" \
  docker exec "$CONTAINER_ID" bash -c 'ls /home/ubuntu/.config/ 2>/dev/null | wc -l | xargs test 1 -le'
check "~/.bashrc restored" \
  docker exec "$CONTAINER_ID" test -f /home/ubuntu/.bashrc
check "~/.profile restored" \
  docker exec "$CONTAINER_ID" test -f /home/ubuntu/.profile
check "~/.ssh has files" \
  docker exec "$CONTAINER_ID" bash -c 'ls /home/ubuntu/.ssh/ 2>/dev/null | wc -l | xargs test 1 -le'

echo ""
echo "  3.3 — File restoration counts"
RESTORED=$(echo "$RESTORE_OUT" | grep -oP '\d+(?= file\(s\) restored)' | head -1 || echo "0")
SKIPPED=$(echo "$RESTORE_OUT" | grep -oP '\d+(?= file\(s\) skipped)' | head -1 || echo "0")
echo "    Files restored: $RESTORED"
echo "    Files skipped:  $SKIPPED"
check "files were restored" test "$RESTORED" -gt 0

echo ""
echo "  3.4 — Missing packages report"
MISSING=$(echo "$RESTORE_OUT" | grep -oP 'Wrote \K\d+(?= missing package)' | head -1 || echo "0")
if [ "$MISSING" = "0" ]; then
  echo "    No missing packages (all packages installed)"
else
  echo "    ${MISSING} package(s) missing (third-party repos need GPG keys)"
  echo "$RESTORE_OUT" | grep "  - " || true
fi

echo ""
echo "  3.5 — Systemd services"
check "ollama.service restored" \
  docker exec "$CONTAINER_ID" test -f /etc/systemd/system/ollama.service
check "pm2-dev.service restored" \
  docker exec "$CONTAINER_ID" test -f /etc/systemd/system/pm2-dev.service

echo ""
echo "  3.6 — SSH configuration"
check "sshd_config restored" \
  docker exec "$CONTAINER_ID" test -f /etc/ssh/sshd_config
docker exec "$CONTAINER_ID" bash -c 'mkdir -p /run/sshd && ssh-keygen -A 2>/dev/null'
check "sshd -t passes" \
  docker exec "$CONTAINER_ID" bash -c 'sshd -t 2>&1'

echo ""
echo "  3.7 — Docker daemon config"
check "daemon.json exists" \
  docker exec "$CONTAINER_ID" test -f /etc/docker/daemon.json

echo ""
echo "  3.8 — Third-party packages"
check "docker-ce installed" \
  docker exec "$CONTAINER_ID" bash -c 'dpkg -l docker-ce 2>/dev/null | grep -q "^ii"'
check "azure-cli installed" \
  docker exec "$CONTAINER_ID" bash -c 'dpkg -l azure-cli 2>/dev/null | grep -q "^ii"'

echo ""
echo "  3.9 — Restore output validation"
TOTAL=$((TOTAL + 1))
printf "  [CHECK] restore completed (no abort) ... "
if grep -qi "restore aborted" /tmp/restore_out.txt; then
  echo -e "${RED}FAIL${NC}"; FAIL=$((FAIL + 1))
else
  echo -e "${GREEN}PASS${NC}"; PASS=$((PASS + 1))
fi
check "restore has summary" \
  grep -qiP 'file\(s\) (restored|skipped)' /tmp/restore_out.txt

echo ""
echo "  3.10 — Dry-run validation"
check "dry-run completed with summary" \
  grep -qiP 'Dry-Run Summary' /tmp/dry_out.txt
check "dry-run: package comparison" \
  grep -qiP '(would install|already installed)' /tmp/dry_out.txt

echo ""
echo "▸ Cleaning up..."
