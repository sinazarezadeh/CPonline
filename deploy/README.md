# Deploying the server side on a Linux VPS

One command gets both pieces running: the matchmaking relay (this repo) and the CyberpunkMP
dedicated server (built fresh from its own upstream source - never redistributed here).

```bash
git clone https://github.com/sinazarezadeh/CPonline
cd CPonline
./deploy/setup.sh
```

Requirements: a Linux server with a public IP and enough RAM/CPU to compile a C++ project
(2 GB RAM minimum; the CyberpunkMP build is the only slow, resource-heavy step). Docker is
installed automatically if it isn't already there.

The script is safe to re-run any time - to pick up an update, just `git pull` in both this repo
and `deploy/vendor/CyberpunkMP`, then run `./deploy/setup.sh` again.

## What it does

1. Installs Docker if needed.
2. Clones `tiltedphoques/CyberpunkMP` into `deploy/vendor/CyberpunkMP` and pulls its submodules.
3. Patches that clone's `Dockerfile` and `xmake.lua` (never touches upstream):
   - adds `unzip` - its base image is missing it, which otherwise breaks the build partway
     through (it can't extract its own downloaded dependencies).
   - installs `cmake` from `bookworm-backports` - the base image's stock CMake (3.25) is too
     old for several dependencies (`entt`, `abseil`, `cryptopp`, `openssl3` all require 3.28+).
   - pins `protobuf-cpp` to `29.3` - it's declared with no version pin, so an unpinned build
     resolves to the latest release (currently v36.2), whose C++ codegen API is incompatible
     with CyberpunkMP's own vendored `code/netpack/cpp/helpers.h` (it calls
     `EffectiveStringCType`/`PROTOBUF_NODISCARD`, both removed from protobuf in v30+). 29.3 is
     the last release before that break.
   - installs Node.js + pnpm - the build also compiles CyberpunkMP's web Admin dashboard
     (`code/server/admin`), which needs both (its own CI installs pnpm before building for
     exactly this reason); the Dockerfile never did, so the build failed on
     `pnpm: command not found`. Installed from Node's own release tarball rather than another
     apt source.
   - pins `gamenetworkingsockets` to `v1.4.1` - also declared with no version pin, so an
     unpinned build resolves to v1.6.0, which itself depends on `protobuf-cpp` with no version
     constraint of its own; that conflicts with the `29.3` pin above and resolves to a
     mismatched abseil build, breaking the final link with
     `undefined reference to ...LowLevelHashLenGt16`. CyberpunkMP's own committed Windows lock
     file (`xmake-requires.lock`) pins this same dependency to `v1.4.1` - the last version whose
     own recipe still declares `protobuf-cpp<30`, compatible with the `29.3` pin.
   - forces the `protobuf-cpp` pin project-wide - the version pin above isn't actually honored
     reliably on its own: `gamenetworkingsockets` additionally depends on `protobuf-cpp` with no
     upper bound, and xmake's automatic conflict resolution between that and the exact pin has
     in practice resolved to a newer, unpinned protobuf anyway. Newer protobuf (30+) made
     `GetTypeName()` always return `absl::string_view`, which has no `.c_str()` - exactly the
     `has no member named 'c_str'` error compiling `GameNetworkingSockets`. Uses xmake's
     documented `add_requireconfs(..., {override = true, ...})` to force a single, deterministic
     resolution instead of relying on constraint intersection.
   - makes a build failure print the real error - by default, when any dependency fails to
     build, xmake only prints a short, often unhelpful snippet and points at a log file inside
     its own Docker build-cache mount, which isn't reachable from outside the build. This makes
     it dump every failed package's full install log straight into the build output instead.
4. Builds and starts both containers via `deploy/docker-compose.yml`, restarting automatically
   on reboot (`restart: unless-stopped`).
5. Opens the relevant firewall ports if `ufw` is active.
6. Prints your server's public IP and the exact values to give your friends.

All seven patches are idempotent and independent, so re-running the script always applies
whichever of them a given clone is still missing.

## What to give your friends

Just the `ip:port` for the CyberpunkMP server (default port `11778`) - that's the one thing the
launcher's **Play** tab asks for. The matchmaking relay (`ws://<ip>:8080/session`, for the
Host/Join tabs' room-code flow) is optional and only needed if you want ad-hoc rooms instead of
this always-on server.

## Common operations

```bash
docker compose -f deploy/docker-compose.yml logs -f     # tail logs from both containers
docker compose -f deploy/docker-compose.yml ps           # check status
docker compose -f deploy/docker-compose.yml down         # stop everything
./deploy/setup.sh                                         # rebuild + restart after any update
```

## Ports

| Service      | Port        | Protocol  |
|--------------|-------------|-----------|
| CyberpunkMP  | 11778       | TCP + UDP |
| Matchmaking  | 8080        | TCP       |

Override with `CYBERPUNKMP_PORT` / `MATCHMAKING_PORT` environment variables before running
`docker compose`, if you need different ports (e.g. behind a reverse proxy).
