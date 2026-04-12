# Backup Sync Tool

Windows tray app for syncing one local folder to a WebDAV destination.

It is built as a native Rust Win32 application using `windows-rs`, with blocking HTTP via `ureq`, local config in `backupsynctool.json`, and DPAPI-encrypted password storage.

## Features

- Native Windows tray app
- Watches one local folder continuously
- Uploads new and changed files to WebDAV
- Streams uploads from disk instead of loading entire files into memory first
- Bounded parallel uploads via `parallel_uploads` in `backupsynctool.json`
- Optional remote-to-local sync polling
- Silent GitHub release update check on startup
- DPAPI password protection
- Start with Windows support
- Compact Recent Activity feed
- Sync progress bar in the main window
- Animated tray icon plus progress tooltip while syncing

## Current Stack

- Rust 2021
- `windows` crate for raw Win32 UI
- `ureq` for blocking HTTPS/WebDAV
- `notify` for filesystem watching
- `serde` + `serde_json` for config
- Windows DPAPI for password encryption

## Project Layout

| Path | Purpose |
|---|---|
| `src/main.rs` | Entry point, single-instance handling, startup |
| `src/ui.rs` | All Win32 UI, controls, layout, progress, recent activity |
| `src/config.rs` | Load/save `backupsynctool.json` |
| `src/secret.rs` | DPAPI encrypt/decrypt |
| `src/webdav.rs` | WebDAV client |
| `src/sync.rs` | Watcher, startup scan, upload worker pool |
| `src/tray.rs` | Tray icon and context menu |
| `src/updater.rs` | GitHub release update check/download/restart |
| `src/xd.rs` | XD local detection for default paths/folders |
| `src/logs.rs` | Log file support |
| `build.rs` | Embeds icons and manifest |
| `assets/` | ICO/SVG assets used for the app and sync animation |

## Configuration

Config is stored next to the exe as `backupsynctool.json`.

Example:

```json
{
  "watch_folder": "C:\\XDSoftware\\backups",
  "webdav_url": "https://example.com",
  "username": "user",
  "password_enc": "...",
  "remote_folder": "XDPT.59655_Palmeira-Minimercado",
  "start_with_windows": true,
  "sync_remote_changes": true,
  "parallel_uploads": 10
}
```

Notes:

- `password_enc` is DPAPI-encrypted, not plain text
- `parallel_uploads` defaults to `10`
- config must live next to `backupsynctool.exe`

## Build

From repo root:

```powershell
$env:PATH += ";$env:USERPROFILE\.cargo\bin"
cargo build --release
Copy-Item "target\release\backupsynctool.exe" "backupsynctool.exe" -Force
Start-Process "backupsynctool.exe"
```

Always launch from the repo root so the app finds `backupsynctool.json` next to the exe.

## Runtime Behavior

- Closing the window hides to tray
- Double-clicking the tray icon reopens the window
- Startup performs a background check of remote files and local files
- Recent Activity shows high-level checking steps plus compact transfer entries
- Sync progress is shown both in the main window and in the tray tooltip

## XD Defaults

If XD is installed locally, the app can prefill:

- local watch folder: `C:\XDSoftware\backups`
- remote folder: derived from XD licence data via `src/xd.rs`

The current app uses a direct PowerShell + DLL inspection flow through `src/xd.rs`.

## Auto Update

The app checks:

`https://api.github.com/repos/ruibeard/backup-sync-tool/releases/latest`

If a newer version is found, the UPDATE button appears. Download/install then replaces the exe in place and restarts the app.

## Assets

The exe currently embeds only the icons referenced by `build.rs`:

- `assets/app-idle.ico`
- `assets/syncing.ico`
- `assets/complete.ico`
- `assets/syncing1.ico` through `assets/syncing6.ico`

SVG files are kept as editable source assets.

## Legacy Code

`legacy-cpp-code/` is kept only as historical/reference material. The active app is the Rust implementation in the repo root.
