#!/usr/bin/env bash
# One-shot setup for a fresh Linux server: builds and starts both the matchmaking relay and the
# CyberpunkMP dedicated server via Docker, then prints the info to give your friends.
#
# Usage:  ./deploy/setup.sh
# Safe to re-run - every step is idempotent (skips work that's already done, rebuilds only
# what changed).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENDOR_DIR="$SCRIPT_DIR/vendor/CyberpunkMP"
CYBERPUNKMP_REPO="https://github.com/tiltedphoques/CyberpunkMP"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"

log() { printf '\n\033[1;33m==> %s\033[0m\n' "$1"; }

# --- 1. Docker ---
if ! command -v docker >/dev/null 2>&1; then
  log "Docker isn't installed - installing it..."
  curl -fsSL https://get.docker.com | sh
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "docker compose (the v2 plugin) is required but wasn't found even after installing Docker." >&2
  echo "See https://docs.docker.com/compose/install/ and re-run this script." >&2
  exit 1
fi

# --- 2. Fetch + patch CyberpunkMP ---
if [[ ! -d "$VENDOR_DIR/.git" ]]; then
  log "Cloning CyberpunkMP (the actual multiplayer mod/server source)..."
  git clone --depth 1 "$CYBERPUNKMP_REPO" "$VENDOR_DIR"
fi

log "Fetching CyberpunkMP's own vendored dependencies (git submodules)..."
git -C "$VENDOR_DIR" submodule update --init

if ! grep -q 'unzip' "$VENDOR_DIR/Dockerfile"; then
  log "Patching CyberpunkMP's Dockerfile - its base image is missing 'unzip', which its own build needs to extract dependencies (openssl, protobuf)."
  sed -i 's/apt install -y xmake g++/apt install -y xmake g++ unzip/' "$VENDOR_DIR/Dockerfile"
fi

if ! grep -q 'bookworm-backports cmake' "$VENDOR_DIR/Dockerfile"; then
  log "Patching CyberpunkMP's Dockerfile - its base image's CMake (3.25) is too old for some of its dependencies (entt, abseil, cryptopp, openssl3 all need 3.28+); pulling a newer one from bookworm-backports (already enabled by the Dockerfile itself)."
  sed -i '/apt install -y xmake g++/a\  && apt install -y -t bookworm-backports cmake \\' "$VENDOR_DIR/Dockerfile"
fi

if ! grep -q '"protobuf-cpp 29.3"' "$VENDOR_DIR/xmake.lua"; then
  log "Patching CyberpunkMP's xmake.lua - its own vendored protobuf codegen helpers (code/netpack/cpp/helpers.h) use APIs (EffectiveStringCType, PROTOBUF_NODISCARD) that protobuf removed in v30+; it declares protobuf-cpp with no version pin, so an unpinned build resolves to the latest (currently v36.2) and fails. Pinning to 29.3, the last release that still has those APIs."
  sed -i 's/"protobuf-cpp",/"protobuf-cpp 29.3",/' "$VENDOR_DIR/xmake.lua"
fi

# --- 3. Build + start ---
log "Building and starting both containers (this compiles CyberpunkMP's C++ server from source - can take several minutes on the first run)..."
docker compose -f "$COMPOSE_FILE" up -d --build

# --- 4. Firewall (best-effort; skipped if ufw isn't in use) ---
if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
  log "Opening firewall ports via ufw..."
  ufw allow 8080/tcp >/dev/null 2>&1 || true
  ufw allow 11778/tcp >/dev/null 2>&1 || true
  ufw allow 11778/udp >/dev/null 2>&1 || true
fi

# --- 5. Report ---
PUBLIC_IP="$(curl -fsS https://ifconfig.me 2>/dev/null || curl -fsS https://api.ipify.org 2>/dev/null || echo "<could not auto-detect - check your VPS provider's dashboard>")"

cat <<EOF

========================================================================
 Done. Both containers are running.

 Give your friends this - it's the ONLY thing they need to type into
 the launcher's Play tab:

     $PUBLIC_IP:11778

 (If you're also using the launcher's Host/Join tabs with room codes
 instead, the matchmaking relay is at ws://$PUBLIC_IP:8080/session -
 put that in Settings.)

 Useful commands:
   docker compose -f $COMPOSE_FILE logs -f       # view live logs
   docker compose -f $COMPOSE_FILE ps             # check container status
   docker compose -f $COMPOSE_FILE down           # stop everything
   ./deploy/setup.sh                              # restart / rebuild after an update
========================================================================
EOF
