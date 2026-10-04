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

if ! grep -q 'gamenetworkingsockets v1.4.1' "$VENDOR_DIR/xmake.lua"; then
  log "Patching CyberpunkMP's xmake.lua - it also declares gamenetworkingsockets with no version pin, which resolves to v1.6.0; that version requires protobuf-cpp with no version constraint of its own, which conflicts with the 29.3 pin above and resolves to a mismatched abseil build, breaking the final link with 'undefined reference to ...LowLevelHashLenGt16'. CyberpunkMP's own committed Windows lock file (xmake-requires.lock) pins this same dependency to v1.4.1, the last version whose own recipe still declares protobuf-cpp<30 - compatible with the 29.3 pin. Pinning the same known-good version here."
  sed -i 's/"gamenetworkingsockets",/"gamenetworkingsockets v1.4.1",/' "$VENDOR_DIR/xmake.lua"
  sed -i 's/"gamenetworkingsockets", "catch2/"gamenetworkingsockets v1.4.1", "catch2/' "$VENDOR_DIR/code/common/xmake.lua"
fi

if ! grep -q 'nodejs.org/dist' "$VENDOR_DIR/Dockerfile"; then
  log "Patching CyberpunkMP's Dockerfile - the build also compiles its web Admin dashboard (code/server/admin), which needs Node.js/pnpm (confirmed from CyberpunkMP's own CI workflow, which installs pnpm before building); the Dockerfile never installs either, so the build fails partway through with 'pnpm: command not found'. Installing Node straight from its official tarball (not apt) so this doesn't depend on yet another package source."
  sed -i '/apt install -y -t bookworm-backports cmake/,/apt clean/{/apt clean/a\
\
RUN curl -fsSL https://nodejs.org/dist/v22.11.0/node-v22.11.0-linux-x64.tar.gz -o /tmp/node.tar.gz \\\
  \&\& tar -xzf /tmp/node.tar.gz -C /usr/local --strip-components=1 \\\
  \&\& rm /tmp/node.tar.gz \\\
  \&\& npm install -g pnpm
}' "$VENDOR_DIR/Dockerfile"
fi

if ! grep -q 'dangerously-allow-all-builds' "$VENDOR_DIR/Dockerfile"; then
  log "Patching CyberpunkMP's Dockerfile - the build runs 'pnpm install' twice more (code/server/admin and code/server/scripting/EmoteSystem); modern pnpm (10+) refuses to run dependencies' install scripts (e.g. esbuild's) unless approved, and treats that as a hard error (ERR_PNPM_IGNORED_BUILDS) rather than a warning in this context - breaking 'pnpm install' with exit code 1 the moment it's hit for real, non-interactively, inside a Docker build. Approving all build scripts globally once, right after installing pnpm, the same way its own docs suggest for CI."
  OLD_LINE='  && npm install -g pnpm'
  NEW_LINE='  && npm install -g pnpm \
  && pnpm config set dangerously-allow-all-builds true'
  export NEW_LINE
  awk -v old="$OLD_LINE" '{ if ($0==old) print ENVIRON["NEW_LINE"]; else print }' "$VENDOR_DIR/Dockerfile" > "$VENDOR_DIR/Dockerfile.tmp"
  mv "$VENDOR_DIR/Dockerfile.tmp" "$VENDOR_DIR/Dockerfile"
  unset NEW_LINE
fi

if ! grep -q 'add_requireconfs("protobuf-cpp"' "$VENDOR_DIR/xmake.lua"; then
  log "Patching CyberpunkMP's xmake.lua - the 29.3 pin above isn't being honored reliably: gamenetworkingsockets' own package recipe additionally depends on protobuf-cpp with no upper bound, and xmake's automatic conflict resolution between that and the exact 29.3 pin has resolved to a newer, unpinned version in practice (confirmed: protobuf 36.2+ removed the opt-in and made GetTypeName() always return absl::string_view, which has no .c_str() - exactly the 'has no member named c_str' error from the last build). Forcing the pin project-wide with xmake's documented override mechanism instead of relying on constraint intersection. Appending at end-of-file isn't safe here - this file ends inside an un-closed option() scope - so this inserts right after the top add_requires(...) block instead, a location already proven safe by the patches above."
  OLD_LINE='    "microsoft-gsl")'
  NEW_LINE='    "microsoft-gsl")

-- cponline: force protobuf-cpp to 29.3 everywhere, overriding any looser
-- constraint declared by a package dependency (e.g. gamenetworkingsockets) -
-- see deploy/setup.sh for why.
add_requireconfs("protobuf-cpp", {override = true, version = "29.3"})'
  export NEW_LINE
  awk -v old="$OLD_LINE" '{ if ($0==old) print ENVIRON["NEW_LINE"]; else print }' "$VENDOR_DIR/xmake.lua" > "$VENDOR_DIR/xmake.lua.tmp"
  mv "$VENDOR_DIR/xmake.lua.tmp" "$VENDOR_DIR/xmake.lua"
  unset NEW_LINE
fi

if ! grep -q 'installdir.failed' "$VENDOR_DIR/Dockerfile"; then
  log "Patching CyberpunkMP's Dockerfile - on a build failure, xmake only prints a truncated snippet and points at a log file inside its own cache mount, which isn't reachable from outside the build. Making it dump every failed package's full install log straight into the build output instead, so the real error is visible on the first failure instead of needing another round of digging."
  OLD_RUN_LINE='RUN --mount=type=cache,target=/root/.xmake xmake -y'
  NEW_RUN_LINE='RUN --mount=type=cache,target=/root/.xmake xmake -y || (find /root/.xmake/cache/packages -path "*/installdir.failed/logs/*" -name "*.txt" -exec sh -c '"'"'echo "=== {} ==="; cat "{}"'"'"' \; ; exit 1)'
  export NEW_RUN_LINE
  awk -v old="$OLD_RUN_LINE" '{ if ($0==old) print ENVIRON["NEW_RUN_LINE"]; else print }' "$VENDOR_DIR/Dockerfile" > "$VENDOR_DIR/Dockerfile.tmp"
  mv "$VENDOR_DIR/Dockerfile.tmp" "$VENDOR_DIR/Dockerfile"
  unset NEW_RUN_LINE
fi

# --- 3. Admin credentials ---
ENV_FILE="$SCRIPT_DIR/.env"
if [[ ! -f "$ENV_FILE" ]]; then
  log "Generating admin credentials for the CyberpunkMP server's web API (first run only) - without these, the server refuses to start at all ('You must provide admin credentials using environment variables.')."
  ADMIN_PASSWORD="$(openssl rand -hex 12 2>/dev/null || head -c 18 /dev/urandom | base64 | tr -dc 'a-zA-Z0-9' | head -c 24)"
  cat > "$ENV_FILE" <<EOF
CYBERPUNKMP_ADMIN_USERNAME=admin
CYBERPUNKMP_ADMIN_PASSWORD=$ADMIN_PASSWORD
EOF
fi

# --- 4. Build + start ---
log "Building and starting both containers (this compiles CyberpunkMP's C++ server from source - can take several minutes on the first run)..."
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --build

# --- 5. Firewall (best-effort; skipped if ufw isn't in use) ---
if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
  log "Opening firewall ports via ufw..."
  ufw allow 8080/tcp >/dev/null 2>&1 || true
  ufw allow 11778/tcp >/dev/null 2>&1 || true
  ufw allow 11778/udp >/dev/null 2>&1 || true
fi

# --- 6. Report ---
PUBLIC_IP="$(curl -fsS https://ifconfig.me 2>/dev/null || curl -fsS https://api.ipify.org 2>/dev/null || echo "<could not auto-detect - check your VPS provider's dashboard>")"
# shellcheck disable=SC1090
source "$ENV_FILE"

cat <<EOF

========================================================================
 Done. Both containers are running.

 Give your friends this - it's the ONLY thing they need to type into
 the launcher's Play tab:

     $PUBLIC_IP:11778

 (If you're also using the launcher's Host/Join tabs with room codes
 instead, the matchmaking relay is at ws://$PUBLIC_IP:8080/session -
 put that in Settings.)

 Server admin credentials (generated on first run, saved in
 deploy/.env - keep this file private):
     username: $CYBERPUNKMP_ADMIN_USERNAME
     password: $CYBERPUNKMP_ADMIN_PASSWORD

 Useful commands:
   docker compose -f $COMPOSE_FILE logs -f       # view live logs
   docker compose -f $COMPOSE_FILE ps             # check container status
   docker compose -f $COMPOSE_FILE down           # stop everything
   ./deploy/setup.sh                              # restart / rebuild after an update
========================================================================
EOF
