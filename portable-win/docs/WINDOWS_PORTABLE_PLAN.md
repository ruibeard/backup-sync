# Windows Portable Plan

## Goal

Build a stripped-down Dropbox-style desktop app for Windows that makes file sync to remote storage as easy as possible.

Primary user flow:

1. User downloads one portable executable.
2. User opens it with no installer and no admin rights.
3. User either:
   - enters credentials in the app, or
   - preloads them through a config file and secure secret storage.
4. User chooses a local folder.
5. The app hides to the tray and keeps the folder synced.

The product should feel like a very small internal utility, not like a full cloud drive platform.

## Product Direction

This plan is for a new Windows-only portable version.

The existing C++ Win32 codebase stays in place for now. This document describes the proposed replacement direction without changing the older Markdown files.

## Core Product Shape

The new app should be:

- Windows-only
- portable
- tray-based
- one-folder sync for v1
- simple to configure
- safe enough for normal business usage
- easy to update without an installer

## Tech Stack

Recommended stack:

- C#
- .NET desktop application
- WinForms for the tray app and settings window
- self-contained single-file publish for release builds
- HttpClient-based WebDAV client
- FileSystemWatcher plus periodic full rescan
- DPAPI for local secret protection
- simple hosted update manifest plus portable updater helper

Why this stack:

- Much faster to build and maintain than raw Win32
- Native Windows feel without cross-platform overhead
- Good fit for a small tray utility
- Supports portable shipping with no separate runtime install
- Easier to evolve than the current C++ UI code

## Packaging Model

The app should be distributed as:

- one downloadable portable executable for the user

Runtime files should live beside the executable unless there is a strong reason to place them elsewhere.

Typical folder layout:

```text
WebDavSync/
  WebDavSync.exe
  config.json
  logs/
  secrets/
```

Generated at runtime:

- `config.json`
- `logs/YYYY-MM-DD.log`
- `secrets/<id>.bin`
- temporary update files

Important note:

A running executable cannot replace itself. Updates therefore need a small helper process or staged replacement flow.

## Configuration

The app should support configuration through both UI and file-based setup.

Settings in `config.json`:

- local folder
- WebDAV URL
- username
- remote folder
- start with Windows
- sync remote changes
- update manifest URL
- sync timing settings if needed later

The password should not be stored in plain text.

The config file should contain only:

- a secret identifier or secret reference
- never the raw password

## Secret Storage

Windows secret handling should use DPAPI.

Approach:

- Password entered by the user is encrypted with DPAPI under the current user profile
- Encrypted bytes are stored in a local file under `secrets/`
- `config.json` stores only the secret identifier

Benefits:

- No plain text password on disk
- No extra service or installer dependency
- Keeps the portable model simple

Tradeoff:

- If the folder is copied to another machine or another Windows user account, the secret will not decrypt there
- In that case the user should be prompted to enter the password again

## UI

The app should have one small settings window and one tray icon.

Settings window fields:

- local folder
- WebDAV URL
- username
- password
- remote folder
- start with Windows
- sync remote changes
- save
- test connection
- sync now
- close

Tray menu:

- open settings
- sync now
- check for updates
- open logs
- exit

Status area in the main window:

- current state text
- progress indicator
- recent activity list

Primary states:

- not configured
- connecting
- idle / watching
- syncing
- update available
- error

## Sync Scope For V1

V1 should stay intentionally small.

Included:

- one watched local folder
- one remote WebDAV destination
- upload new files
- upload changed files
- create remote folders as needed
- manual sync now
- optional remote-to-local sync for clearly newer or missing remote files
- simple conflict avoidance
- logging

Explicitly out of scope for the first release:

- multi-folder sync
- full two-way conflict resolution
- version history
- file sharing features
- selective sync rules UI
- team admin features
- service mode

## Sync Engine Design

The sync engine should combine:

- `FileSystemWatcher` for fast local change detection
- debouncing to avoid syncing while files are still being written
- periodic full rescan to catch missed events or edge cases

Basic behavior:

1. Watch the local folder for changes.
2. Queue a sync pass after a short debounce.
3. Build a fresh local snapshot.
4. Compare against the previous local snapshot.
5. Compare against remote metadata when needed.
6. Upload changed files.
7. Optionally download remote changes if that mode is enabled.
8. Update the tray and activity log.

## WebDAV Layer

The WebDAV client should support at minimum:

- `OPTIONS` for connection checks
- `PROPFIND` for remote listing and metadata
- `HEAD` for remote file checks where useful
- `GET` for remote download
- `PUT` for upload
- `MKCOL` for remote folder creation
- `DELETE` if remote delete support is added later

Security rules:

- HTTPS only by default
- validate certificates and hostnames
- do not suppress TLS errors by default
- never log passwords or authorization headers

## Logging

The app should write simple text logs for support and troubleshooting.

Log requirements:

- one log file per day
- UTF-8 text
- timestamped entries
- no secrets in logs
- keep recent activity visible in the UI

Example log events:

- app started
- config saved
- connection test succeeded or failed
- sync started
- file uploaded
- file downloaded
- sync error
- update available
- update installed

## Start With Windows

This should be supported without admin rights.

Implementation direction:

- register under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

This fits the portable model and avoids machine-wide installation requirements.

## Updater

Updates are a required feature even in the portable model.

### Update Design

The app should use a lightweight manifest-based updater.

Pieces:

- hosted manifest endpoint such as `appcast.json`
- current app version embedded in the executable
- update check on startup and on a schedule
- staged download to a temp location
- helper updater process that swaps binaries after app exit
- relaunch after successful update

### Manifest Fields

The manifest should include:

- latest version
- download URL
- file hash
- publish date
- optional minimum supported version
- release notes URL

Example shape:

```json
{
  "version": "1.2.0",
  "downloadUrl": "https://updates.example.com/WebDavSync-1.2.0.exe",
  "sha256": "...",
  "publishedAtUtc": "2026-04-08T12:00:00Z",
  "releaseNotesUrl": "https://updates.example.com/releases/1.2.0"
}
```

### Update Behavior

V1 update behavior:

- check at startup
- check periodically while running
- notify the user if an update exists
- download the new executable
- close the app
- run helper updater
- replace old executable
- relaunch

Later, after the product becomes stable, the app can reduce how often it checks for updates.

## Release Strategy

Recommended early release model:

- internal download page or direct file link
- portable exe only
- no installer
- signed binaries when possible
- hosted update manifest on a simple HTTPS endpoint

## Proposed Folder Structure For New Code

```text
portable-win/
  README.md
  docs/
    ARCHITECTURE.md
    MIGRATION.md
  src/
    WebDavSync.Portable/
      Program.cs
      Ui/
      Sync/
      Updates/
      Secrets/
      Configuration/
      Windows/
```

## First Implementation Milestones

### Milestone 1

- scaffold Windows tray application
- load and save config
- store secrets with DPAPI
- test WebDAV connection
- tray icon and settings window

### Milestone 2

- local watcher
- upload changed files
- activity log
- manual sync now
- startup with Windows

### Milestone 3

- optional remote download sync
- stronger retry and backoff
- file-write stabilization
- better error reporting

### Milestone 4

- portable updater helper
- hosted manifest integration
- release packaging
- signing and validation

## Non-Goals For The New Portable Version

Not a goal for the first portable release:

- cross-platform support
- installer-first deployment
- enterprise management features
- multiple remote targets
- advanced permissions model
- collaborative file sharing product features

## Summary

The direction is:

- keep the old C++ version as-is for now
- build a new Windows-only portable tray app
- use C# and WinForms for speed and simplicity
- keep config file based setup
- protect secrets with DPAPI
- support WebDAV sync with a small, focused feature set
- support portable app updates through a manifest plus helper process

This is the smallest architecture that matches the product goal of being extremely easy to set up while still leaving room to harden and grow later.
