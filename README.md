# CPonline

A Windows desktop launcher for playing Cyberpunk 2077 together with friends, built on top of
the open-source community multiplayer mod [CyberpunkMP](https://github.com/tiltedphoques/CyberpunkMP).

**This is for legitimately owned copies of the game only.** Nothing here targets, detects,
accommodates, or works around DRM/cracked copies - it's an ordinary mod manager and launcher.

## What it does

- **Play tab** - the only screen a non-technical friend needs. Detects the game, installs/
  updates mods, and launches, all automatically - the one thing to type in is the server address
  (`ip:port`) someone gave you.
- **Mods tab** - detects your Cyberpunk 2077 install (Steam/GOG/Epic, or browse manually) and
  installs/updates the mod stack CyberpunkMP needs (RED4ext, CET, Redscript, ArchiveXL, TweakXL,
  Codeware, Input Loader, CyberpunkMP itself), downloading each fresh from its own GitHub
  Releases and verifying its checksum before extracting anything, with live progress bars.
- **Host tab** - optionally runs the CyberpunkMP dedicated server locally via Docker, creates a
  room on the matchmaking relay, tries UPnP then STUN to make your connection reachable, and
  launches the game. (For a friend-group server that's just always on, prefer deploying it to a
  VPS instead - see `deploy/README.md` - and have everyone use the Play tab.)
- **Join tab** - resolve a friend's room code through the relay, or connect directly by IP, and
  launch the game against that target.
- **Settings tab** - which matchmaking relay to use, and the local UDP port.

## Project layout

```
CPonline.sln
src/
  CPonline.Shared/          DTOs shared between the launcher and the matchmaking service
  CPonline.Launcher.Core/   All the actual logic (install detection, mod install + progress
                             reporting, launch orchestration, NAT traversal, matchmaking
                             client) - plain net8.0, no WPF dependency, builds/tests on any OS
  CPonline.Launcher/        The WPF app itself (net8.0-windows) - Views/ViewModels wiring
                             Launcher.Core's services together, Cyberpunk-themed UI
  CPonline.Matchmaking/     The room-code signaling relay (ASP.NET Core minimal API +
                             WebSockets) - only ever carries small JSON control messages,
                             never game traffic
deploy/
  setup.sh                  One-shot script: builds and runs both the matchmaking relay and a
                             real CyberpunkMP dedicated server on a Linux VPS - see deploy/README.md
  docker-compose.yml
tests/
  CPonline.Launcher.Tests/     Unit tests for Launcher.Core (VDF parsing, checksum
                                verification, mod-manifest handling, launch-argument building,
                                STUN packet encode/decode, progress reporting, ...)
  CPonline.Matchmaking.Tests/  Unit + integration tests for the matchmaking service
scripts/
  update-mods-manifest.sh   Regenerates manifests/mods.json with real, verified tags/hashes
```

## Building and testing

**`CPonline.Launcher` (the WPF app) can only be built on Windows** - the XAML/WPF build tooling
(`Microsoft.NET.Sdk.WindowsDesktop`) isn't available on Linux/macOS at all, so a Linux CI runner
or dev container can't even restore that one project, let alone build it. Everything else is
plain, cross-platform .NET and builds/tests anywhere.

**On Windows** (build and run everything, including the launcher):
```
dotnet build CPonline.sln
dotnet test
```

**On Linux/macOS/CI** (everything except the WPF app - use the provided solution filter):
```
dotnet build CPonline.CrossPlatform.slnf
dotnet test CPonline.CrossPlatform.slnf
```

## Running a server

For an always-on friend-group server (recommended - lets everyone just use the Play tab with a
fixed `ip:port`, no local Docker/hosting needed on anyone's PC), deploy to a Linux VPS:

```
git clone https://github.com/sinazarezadeh/CPonline
cd CPonline
./deploy/setup.sh
```

See `deploy/README.md` for details - it builds and runs both the CyberpunkMP dedicated server
(compiled fresh from its own upstream source) and the matchmaking relay, and prints the exact
address to give your friends.

To just run the matchmaking relay locally instead:
```
dotnet run --project src/CPonline.Matchmaking
```
or via Docker:
```
docker build -t cponline-matchmaking -f src/CPonline.Matchmaking/Dockerfile .
docker run -p 8080:8080 cponline-matchmaking
```

## Mod manifest

`src/CPonline.Launcher/manifests/mods.json` is pinned to real, verified tags and SHA-256 hashes
for all 8 mod dependencies. To refresh it after an upstream mod update, run
`scripts/update-mods-manifest.sh` (needs `curl`, `jq`, `sha256sum`) and review the diff before
committing - `ModManager` refuses to extract anything whose checksum doesn't match the manifest.

## Known open items

- Exact mod install-folder layout is verified for the currently-pinned versions; it can drift on
  a future upstream mod release, so re-check `installTargets` after running the update script.
- Whether the CyberpunkMP server binary runs standalone outside Docker is still unconfirmed -
  Docker (via `deploy/setup.sh`, or manually per that repo's README) is the verified path.
- Current GOG product ID / Epic catalog display-name matching for Cyberpunk 2077 hasn't been
  verified against a real GOG/Epic install yet (Steam detection has been).
- No code-signing certificate is set up, so the built launcher .exe will trigger a Windows
  SmartScreen "unknown publisher" warning until one is added.

## Architecture notes

- Mod dependencies are always downloaded fresh from their own GitHub Releases at install time,
  never redistributed in this repo, to avoid third-party license/redistribution questions.
- The matchmaking relay only brokers short-lived JSON control messages (room create/resolve/
  report-address) to exchange connection info - actual CyberpunkMP game traffic always goes
  directly between the two players' machines. A TURN-style relay for game traffic itself is
  deliberately not built; if UPnP/STUN NAT traversal proves unreliable enough in practice to
  matter, that's a follow-up, not part of this MVP.
