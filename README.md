# CPonline

A Windows desktop launcher for playing Cyberpunk 2077 together with friends, built on top of
the open-source community multiplayer mod [CyberpunkMP](https://github.com/tiltedphoques/CyberpunkMP).

**This is for legitimately owned copies of the game only.** Nothing here targets, detects,
accommodates, or works around DRM/cracked copies - it's an ordinary mod manager and launcher.

## What it does

- **Mods tab** - detects your Cyberpunk 2077 install (Steam/GOG/Epic, or browse manually) and
  installs/updates the mod stack CyberpunkMP needs (RED4ext, CET, Redscript, ArchiveXL, TweakXL,
  Codeware, Input Loader, CyberpunkMP itself), downloading each fresh from its own GitHub
  Releases and verifying its checksum before extracting anything.
- **Host tab** - optionally runs the CyberpunkMP dedicated server locally via Docker, creates a
  room on the matchmaking relay, tries UPnP then STUN to make your connection reachable, and
  launches the game.
- **Join tab** - resolve a friend's room code through the relay, or connect directly by IP, and
  launch the game against that target.
- **Settings tab** - which matchmaking relay to use, and the local UDP port.

## Project layout

```
CPonline.sln
src/
  CPonline.Shared/          DTOs shared between the launcher and the matchmaking service
  CPonline.Launcher.Core/   All the actual logic (install detection, mod install, launch
                             orchestration, NAT traversal, matchmaking client) - plain net8.0,
                             no WPF dependency, so it builds and tests on any OS
  CPonline.Launcher/        The WPF app itself (net8.0-windows) - Views/ViewModels wiring
                             Launcher.Core's services together
  CPonline.Matchmaking/     The room-code signaling relay (ASP.NET Core minimal API +
                             WebSockets) - only ever carries small JSON control messages,
                             never game traffic
tests/
  CPonline.Launcher.Tests/     Unit tests for Launcher.Core (VDF parsing, checksum
                                verification, mod-manifest handling, launch-argument building,
                                STUN packet encode/decode, ...)
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

## Running the matchmaking relay locally

```
dotnet run --project src/CPonline.Matchmaking
```

or via Docker:
```
docker build -t cponline-matchmaking -f src/CPonline.Matchmaking/Dockerfile .
docker run -p 8080:8080 cponline-matchmaking
```

Point the launcher's Settings tab at `ws://<host>:8080/session`.

## Before using the launcher for real

`src/CPonline.Launcher/manifests/mods.json` ships with **placeholder** `tag`/`sha256` values
(`REPLACE_WITH_VERIFIED_TAG` / all zeros) for every mod dependency, because this repository was
built in a sandboxed environment with no access to GitHub's release API or download hosts, so
the pinned tags and checksums could not be verified or computed here. From a machine with normal
internet access, run:

```
scripts/update-mods-manifest.sh
```

This fetches each mod's latest release, downloads the matching asset, computes its real SHA-256,
and rewrites the manifest in place. Review the diff, then commit it. `ModManager` refuses to
extract anything whose checksum doesn't match the manifest, so the placeholder values simply
make every install fail closed rather than silently skip verification - the launcher is safe to
build and test before this step, just not to actually install mods with.

## Known open items (tracked as "Milestone 0" spikes)

- Exact current mod install-folder layout per pinned version (these drift across major mod
  releases) - `manifests/mods.json`'s `installTargets` are a best-effort based on documented
  convention, not yet verified against real installs.
- Whether the CyberpunkMP server binary runs standalone on Windows outside Docker.
- The `-online -ip=... -port=...` launch-parameter hand-off (confirmed from CyberpunkMP's own
  source) should be verified against a real Steam-launched, owned copy before relying on it.
- Current GOG product ID / Epic catalog display name matching for Cyberpunk 2077.
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
