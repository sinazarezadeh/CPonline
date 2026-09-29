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
3. Patches that clone's `Dockerfile` to add `unzip` - its base image is missing it, which
   otherwise breaks the build partway through (it can't extract its own downloaded
   dependencies). This only touches the local clone, never upstream.
4. Builds and starts both containers via `deploy/docker-compose.yml`, restarting automatically
   on reboot (`restart: unless-stopped`).
5. Opens the relevant firewall ports if `ufw` is active.
6. Prints your server's public IP and the exact values to give your friends.

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
