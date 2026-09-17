# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security

Hardening reviewed against a public-server threat model: hostile clients, not
valve-level adversaries. Each item was confirmed against the server's own code
first — several came from studying a community fork, the rest from the audit.

- **Privacy is now enforced at the join gate.** `Privacy` was published in the
  lobby metadata but never checked when a connection request arrived, so a
  "Friends only" or "Locked" server accepted anyone who learned its SteamID.
  Friends-only admits the server account's Steam friends; Locked admits nobody.
- **The rank metadata key can no longer be spoofed by clients.** The server
  announces rank changes with a PlayerMetadataResponse; relayed unfiltered, any
  client could broadcast `PermissionLevel: OWNER` for itself into everyone's UI.
  The server re-checked commands already, but every menu lied. Client-sent
  metadata messages now pass with the rank key dropped.
- **Ownership requests can no longer be framed onto another player.** The spawn
  guard purges and kicks by owner, so a griefer could claim their flood in a
  victim's name and let the server delete the victim's work and eject them.
  Requests naming a different connected player are refused; self-claims and
  Fusion's release semantics still pass.
- **A per-player relay budget** (`MaxRelayKBPerSecond`, 512 by default) caps how
  much traffic one connection may cause the relay to amplify. Previously
  anything that was not a spawn — metadata, RPC, module messages, poses — was
  forwarded without limit, so one 500 KB message at a steady rate could take
  down the whole lobby's downlink. Over-budget messages are dropped and logged
  once a minute; 0 disables the cap.
- **Connection requests are size-bounded at the gate.** Metadata dictionaries,
  equipped-item lists and barcodes were unbounded (up to the transport's 512 KB
  per message), stored, and rebroadcast to every current and future player.
  Requests beyond generous limits are now refused whole rather than trimmed.
- **Refused joins close their transport connection.** A rejected request had
  its disconnect message sent but the socket left to the peer's discretion —
  a script hammering the join path littered the poll group with dead sockets.
- **The receive queue is drained per tick**, within a bounded batch count, so a
  message flood can no longer grow the transport queue (or pin the CPU)
  without limit.
- **A corrupt `server.json` is backed up before defaults replace it.** A torn
  write after a crash used to hand back silent defaults, and the next settings
  save would then destroy the operator's bans, ranks and mod catalogue.
- **Mod-info answers are only learned from the holder who was asked.** The
  learned mod catalogue is persisted; a forged response could previously
  poison it for every future joiner.
- A kick's log line named the victim as the kicker (`Kicked by <target>`);
  it now names whoever pressed the button.

## [1.0.0] - 2026-09-17

First tagged release. A headless dedicated relay server for BONELAB Fusion:
signs in to Steam, publishes a lobby that stock Fusion clients find in their
server browser, and relays their traffic — with no game, no VR headset and no
host player on the machine.

### Added

- Steam lobby publishing over Steam Datagram Relay — no port forwarding, and
  unmodified Fusion clients cannot tell it apart from a hosted game.
- Web control panel (localhost by default): live log and metrics, moderation,
  map switching for 25 vanilla levels plus modded maps by barcode, persistent
  ranks and bans, and live-editable gameplay rules.
- Permission model mirroring Fusion's (`Guest/Default/Operator/Owner`), stored
  per SteamID, applied at join, and re-checked server-side for every moderation
  command.
- Spawn guard with graduated response — purge, then kick — sized for what
  *clients* survive rather than the server, because a relay never simulates.
- World hygiene: orphan culling, inheritance-aware cleanup of abandoned props,
  and eviction of the oldest abandoned props when the world is full, so spawns
  are refused silently never.
- Automatic recovery: the systemd stack rebuilds itself after a killed server
  process or a Steam client crash, usually in under a minute.
- Metrics history (minute rows in `logs/metrics.csv`) with graphs from ten
  minutes to a month.
- Installer for Arch, Debian/Ubuntu, Fedora/RHEL and openSUSE: detects the
  distro, builds, asks before installing any package, writes systemd user units
  and helper scripts — no root required beyond optional packages.
- `fusion-ctl.sh` for day-to-day management (`start`, `stop`, `logs`, `panel`,
  `update`), and `uninstall.sh` for a clean removal.
- Test suite covering the wire format, config persistence and migration,
  permissions, the spawn guard and player ID allocation, with CI on every push
  and tagged releases that publish a linux-x64 build.
