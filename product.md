# WebDavSync — Product Specification

**Version:** 1.0 (portable-win)
**Status:** Active development
**Last updated:** April 2026

---

## 1. Overview

WebDavSync is a Windows-only portable tray utility that automatically syncs one local folder to one remote WebDAV server. It is designed for users who need a simple, self-contained backup or sync tool without an installer, without a background service, and without admin rights.

The product should feel like a small internal utility — not a cloud platform. One window, one tray icon, and one job.

---

## 2. Problem Statement

Users who manage their own WebDAV servers (e.g. Nextcloud, OwnCloud, or any standards-compliant DAV endpoint) have no simple Windows-native tool that:

- runs silently in the background,
- requires no installer or admin rights,
- uses modern, clean UI controls,
- and protects credentials properly without a keychain service.

This product fills that gap with a portable `.exe` folder that can be copied anywhere, launched immediately, and left running in the tray.

---

## 3. Target User

A non-technical or semi-technical Windows user who:

- manages their own WebDAV server or uses a hosted one,
- wants file backup or sync without learning a complex tool,
- does not want to grant admin rights or run an installer,
- expects the app to be invisible once configured.

---

## 4. Success Definition

The product is successful when a user can:

1. Download a single portable folder.
2. Run the executable with no installer.
3. Enter five fields: local folder, server URL, username, password, remote folder.
4. Save and close.
5. Trust that the app sits in the tray and keeps the folder synced indefinitely.

---

## 5. Tech Stack

| Concern | Technology | Rationale |
|---|---|---|
| Language | **C# 12** | Fast to build, easy to maintain, full Windows API access |
| Runtime | **.NET 8** (self-contained, win-x64) | No runtime install required on the target machine |
| UI framework | **WPF** (`UseWPF=true`) | Native Windows feel, modern controls, good tooling |
| UI component library | **WPF-UI 4.2.0** (`Wpf.Ui` NuGet) | Fluent Design controls: `FluentWindow`, `InfoBar`, `SymbolRegular` |
| Tray integration | **WinForms** `NotifyIcon` + `ApplicationContext` | Stable, well-documented tray icon approach; mixed WPF+WinForms is standard |
| Credential protection | **Windows DPAPI** (`System.Security.Cryptography.ProtectedData`) | No extra service; ties secrets to the Windows user profile |
| Config format | **JSON** (`System.Text.Json`) | Human-readable, easy to diff, no external parser dependency |
| HTTP / WebDAV | `HttpClient` + custom WebDAV methods | Standard .NET HTTP stack; WebDAV is HTTP with extra verbs |
| Packaging | Single-file self-contained publish (`PublishSingleFile=true`, `SelfContained=true`) | One `.exe` to distribute; no framework install on target |
| Build system | **MSBuild / dotnet CLI** | Standard .NET tooling |
| Source repo layout | `portable-win/src/WebDavSync.Portable/` | Active codebase; `legacy-win32/` is C++ reference only |

### Why not the legacy C++ version?

The `legacy-win32/` folder contains a fully working C++ Win32 implementation. It is kept as a reference but is **not** the active product. The C# version under `portable-win/` is the source of truth because:

- WPF + C# is significantly faster to evolve and maintain.
- The legacy app uses raw Win32 controls and has no modern UI abstraction.
- The C# version aligns better with the portable + update model going forward.

---

## 6. Repository Structure

```
backup-sync-tool/
├── appcast.json                      # Hosted update manifest (GitHub raw URL)
├── product.md                        # This document
├── legacy-win32/                     # Reference-only C++ Win32 implementation
└── portable-win/                     # Active C# .NET 8 implementation
    ├── docs/
    │   ├── ARCHITECTURE.md
    │   ├── PRODUCT_REQUIREMENTS.md
    │   ├── WINDOWS_PORTABLE_PLAN.md
    │   └── MIGRATION.md
    └── src/
        └── WebDavSync.Portable/
            ├── Program.cs
            ├── Configuration/        # AppConfig, ConfigStore, PortablePaths
            ├── Secrets/              # SecretStore (DPAPI)
            ├── Sync/                 # SyncService, SyncState, SyncStatusSnapshot
            ├── Ui/                   # SettingsWindow, SettingsViewModel
            ├── Updates/              # UpdateService, UpdateManifest, UpdateCheckResult
            └── Windows/              # TrayAppContext, StartupRegistration
```

### Runtime folder layout (beside the .exe)

```
WebDavSync/
  WebDavSync.Portable.exe
  config.json             ← non-secret settings
  logs/
    2026-04-11.log        ← one file per day
  secrets/
    <guid>.bin            ← DPAPI-encrypted password blob
  updates/                ← temporary update staging
```

---

## 7. Core Features

### 7.1 Launch and First Run

- On first run (no `config.json`), the settings window opens automatically.
- A tray icon appears as soon as the app starts successfully.
- If startup fails for any reason, the app must:
  - write a log entry,
  - show a visible error dialog (never fail silently),
  - offer to open the logs folder.

### 7.2 Settings Window

One window. No tabs, no wizard, no multi-page flow.

**Fields:**

| Field | Required | Notes |
|---|---|---|
| Local folder | Yes | Path browser + manual entry |
| Server URL | Yes | WebDAV base URL, e.g. `https://dav.example.com/` |
| Username | Yes | Plain text |
| Password | Yes (first save) | Masked; not required to re-enter after first save |
| Remote folder | Yes | Remote path, e.g. `/backups` |
| Start with Windows | No | Checkbox; uses `HKCU Run` registry key |
| Sync remote changes | No | Checkbox; enables remote-to-local download |

**Actions in the window:**

| Action | Behavior |
|---|---|
| Save configuration | Persists all fields; encrypts and stores the password via DPAPI |
| Test connection | Runs a WebDAV `OPTIONS` probe; shows success or error inline |
| Sync now | Triggers an immediate sync pass outside the normal timer |
| Check for updates | Fetches `appcast.json` and displays result inline |
| Open logs | Opens the `logs/` folder in Windows Explorer |

**Status area (top of window):**

Displays the current app state in plain language. Examples:

- `[!] Setup incomplete — enter all required fields.`
- `[OK] Ready to sync — watching C:\Users\me\Backups → /backups`
- `[~] Syncing — 3 files remaining`
- `[✕] Error — connection failed`

**Recent activity list:**

A short scrollable list of timestamped log lines, e.g.:

```
09:14  Configuration saved.
09:15  Connection test succeeded.
09:16  Watching for changes.
09:20  Synced 4 files.
```

### 7.3 Tray Icon

Always visible while the app is running.

**Context menu:**

```
WebDavSync
──────────────────
Open Settings
Sync Now
Check for Updates
Open Logs
──────────────────
Exit
```

**Icon states:**

| State | Icon |
|---|---|
| Idle / watching | Default icon |
| Syncing | Animated or alternate syncing icon |
| Error | Error indicator icon |

**Behavior:**

- Double-click opens the settings window.
- Closing the settings window hides it; the app keeps running.
- Exit fully shuts down the app and removes the tray icon.

### 7.4 Sync Engine

**Trigger model:**

- `FileSystemWatcher` detects local changes in real time.
- Changes are debounced (short delay after the last event) to avoid syncing mid-write.
- A periodic full rescan runs on a timer to catch anything the watcher missed.
- Manual "Sync Now" triggers an immediate pass.

**Sync pass logic (upload direction):**

1. Build a local snapshot (file paths + last-modified timestamps + sizes).
2. Compare against the previous snapshot to find new, changed, and (optionally) deleted files.
3. For each changed file: upload via `PUT`; create remote directories via `MKCOL` as needed.
4. Update the stored snapshot.
5. Log each uploaded file and any errors.

**Optional remote-to-local (when "Sync remote changes" is enabled):**

1. Run `PROPFIND` on the remote folder to get remote metadata.
2. For each remote file that is newer than the local copy, or is absent locally: download via `GET`.
3. Log each downloaded file.

**Conflict approach for V1:**

- Conflicts are avoided, not resolved.
- Local changes always win when syncing up.
- Remote changes win only if the local file is absent or clearly older.
- No merge, no version history, no conflict dialog in V1.

**Sync states:**

| State | Meaning |
|---|---|
| `NotConfigured` | No valid config yet |
| `Connecting` | Testing the initial connection |
| `Idle` | Watching; no pending changes |
| `Syncing` | Upload or download pass in progress |
| `Error` | Last sync pass failed |
| `UpdateAvailable` | A newer version was found |

### 7.5 WebDAV Client

Minimum required WebDAV verbs:

| Verb | Purpose |
|---|---|
| `OPTIONS` | Connection test / capability check |
| `PROPFIND` | List remote directory; get file metadata |
| `HEAD` | Check if a remote file exists and get its size/ETag |
| `GET` | Download a remote file |
| `PUT` | Upload a local file |
| `MKCOL` | Create a remote directory |
| `DELETE` | Delete a remote file (only if remote-delete mirroring is added) |

Security rules:

- HTTPS is the expected default; HTTP is only allowed if explicitly entered by the user.
- Certificate and hostname validation must not be suppressed.
- Authorization headers and passwords must never appear in log output.

### 7.6 Configuration Storage

`config.json` (non-secret, lives beside the executable):

```json
{
  "LocalFolder": "C:\\Users\\me\\Backups",
  "WebDavUrl": "https://dav.example.com/",
  "Username": "alice",
  "RemoteFolder": "/backups",
  "StartWithWindows": true,
  "SyncRemoteChanges": false,
  "SecretId": "a3f2...",
  "UpdateManifestUrl": "https://raw.githubusercontent.com/.../appcast.json"
}
```

Password storage:

- On save, the password is encrypted with DPAPI under the current Windows user profile.
- The encrypted bytes are written to `secrets/<SecretId>.bin`.
- `config.json` stores only the `SecretId` — never the raw password.
- On relaunch, the app decrypts the secret automatically. If decryption fails (e.g. different machine or user), the password field is cleared and the user is prompted to re-enter it.

### 7.7 Logging

- One UTF-8 text log file per calendar day: `logs/YYYY-MM-DD.log`.
- Each line is timestamped.
- Logged events: app start, config save, connection test result, sync start/complete/error, individual file uploads/downloads, update check result, app exit.
- Logs must never contain passwords, secret identifiers, or `Authorization` header values.
- The settings window shows a short tail of recent activity pulled from the in-memory log buffer.

### 7.8 Start with Windows

- Registered at `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` — no admin rights required.
- Toggled by the "Start with Windows" checkbox in the settings window.
- The registered path points to the current executable location.

### 7.9 Auto-Update

**Manifest (`appcast.json`) shape:**

```json
{
  "version": "1.2.0",
  "downloadUrl": "https://github.com/.../releases/download/v1.2.0/WebDavSync.zip",
  "sha256": "abc123...",
  "publishedAtUtc": "2026-04-08T12:00:00Z",
  "releaseNotesUrl": "https://github.com/.../releases/tag/v1.2.0"
}
```

**Update check behavior:**

- Check at startup.
- Check on a periodic schedule while the app is running.
- Manual check from tray menu or settings window.
- If a newer version is found, show a banner in the settings window status area.

**Update install flow (portable-safe):**

Because a running `.exe` cannot overwrite itself:

1. Download the new executable to `updates/`.
2. Verify the SHA-256 hash against the manifest.
3. Close the main app.
4. A small helper process (or a `.bat` script) replaces the old `.exe` with the new one.
5. Relaunch the updated executable.

---

## 8. UI Design

### Screen: First Run (not configured)

```
+-----------------------------------------------------------------------------------+
| WebDavSync                                                                        |
| Portable WebDAV folder sync for Windows                                           |
+-----------------------------------------------------------------------------------+
| STATUS                                                                            |
| [!] Setup incomplete                                                              |
| Enter a local folder, server URL, username, password, and remote folder.          |
+--------------------------------------+--------------------------------------------+
| LOCAL FOLDER                         | ACTIONS                                    |
| Local folder *                       | [ Save configuration ]                     |
| [ C:\Users\me\Backups            ]   | [ Test connection    ]                     |
| [ Browse ]                           | [ Sync now           ]                     |
|                                      | [ Check for updates  ]                     |
| SERVER CONNECTION                    | [ Open logs          ]                     |
| Server URL *                         |                                            |
| [ https://dav.example.com/...    ]   | RECENT ACTIVITY                            |
| Username *                           | No activity yet.                           |
| [ username                       ]   |                                            |
| Password *                           |                                            |
| [ ••••••••                       ]   |                                            |
| Remote folder *                      |                                            |
| [ /backups                       ]   |                                            |
|                                      |                                            |
| PREFERENCES                          |                                            |
| [ ] Start with Windows               |                                            |
| [ ] Sync remote changes              |                                            |
+-----------------------------------------------------------------------------------+
```

### Screen: Configured and Ready

```
+-----------------------------------------------------------------------------------+
| WebDavSync                                                                        |
| Portable WebDAV folder sync for Windows                                           |
+-----------------------------------------------------------------------------------+
| STATUS                                                                            |
| [OK] Ready to sync                                                                |
| Connected. Watching C:\Users\me\Backups → /backups                                |
+--------------------------------------+--------------------------------------------+
| LOCAL FOLDER                         | ACTIONS                                    |
| Local folder *                       | [ Save configuration ]                     |
| [ C:\Users\me\Backups            ]   | [ Test connection    ]                     |
| [ Browse ]                           | [ Sync now           ]                     |
|                                      | [ Check for updates  ]                     |
| SERVER CONNECTION                    | [ Open logs          ]                     |
| Server URL *                         |                                            |
| [ https://dav.example.com/...    ]   | RECENT ACTIVITY                            |
| Username *                           | 09:14  Configuration saved.               |
| [ username                       ]   | 09:15  Connection test succeeded.         |
| Password                             | 09:16  Watching for changes.              |
| [ ••••••••  (saved)              ]   | 09:20  Synced 4 files.                    |
| Remote folder *                      |                                            |
| [ /backups                       ]   |                                            |
|                                      |                                            |
| PREFERENCES                          |                                            |
| [x] Start with Windows               |                                            |
| [ ] Sync remote changes              |                                            |
+-----------------------------------------------------------------------------------+
```

### Screen: Startup Error Dialog

```
+---------------------------------------------------------------+
| WebDavSync                                                    |
+---------------------------------------------------------------+
| The app could not start correctly.                            |
|                                                               |
| A startup error was written to the logs folder.               |
| Please reopen the app or contact support with the log file.   |
|                                                               |
| [ Open Logs ]                            [ Close ]           |
+---------------------------------------------------------------+
```

### Design Rules

- Use modern WPF controls (WPF-UI library).
- Use standard OS window chrome. Do not use custom title-bar chrome.
- Group fields into clear labeled sections (Local Folder, Server Connection, Preferences, Actions, Recent Activity).
- Mark required fields with `*`.
- Disable "Save" if required fields are missing.
- Status area must always be visible; it is the primary feedback surface.
- Error messages should be short and written in plain English — no stack traces in the UI.

---

## 9. Security Requirements

| Requirement | Implementation |
|---|---|
| No plain-text password on disk | DPAPI-encrypted blob in `secrets/` |
| No password in logs | Logger must strip auth headers and password values |
| HTTPS by default | `HttpClient` base address validation; HTTP only if user explicitly enters `http://` |
| TLS certificate validation | No `ServerCertificateCustomValidationCallback` suppression in production |
| No admin rights required | DPAPI, `HKCU` registry, and portable folder model all operate as standard user |
| Secrets tied to user profile | DPAPI `DataProtectionScope.CurrentUser` |

---

## 10. Implementation Constraints

- **One bootstrap path.** `Program.cs` is the single entry point. No dependency injection container.
- **One settings window.** No tabs, no multi-page flows.
- **Small, focused classes.** `ConfigStore`, `SecretStore`, `SyncService`, `UpdateService`, `TrayAppContext`, `StartupRegistration` — each does one thing.
- **Prefer code-behind for simple UI logic.** MVVM is acceptable where it reduces complexity, not where it adds it.
- **No abstraction for abstraction's sake.** Do not introduce interfaces, factories, or patterns without a concrete current need.
- **Self-contained publish.** The release binary must not require a .NET runtime on the target machine.
- **Portable by design.** All runtime files (`config.json`, `logs/`, `secrets/`) live beside the executable.

---

## 11. Out of Scope (V1)

The following are explicitly excluded from the first release:

- Multiple watched folders
- Full bidirectional conflict resolution
- Version history or file versioning
- File sharing or collaboration features
- Service mode (no Windows service)
- Cross-platform support
- Enterprise administration or policy management
- Selective sync rules
- Multi-account support
- Installer-first deployment

---

## 12. Delivery Milestones

### Milestone 1 — Foundation (partially complete)
- [x] Scaffold Windows tray application
- [x] Load and save `config.json`
- [x] DPAPI secret storage
- [x] WPF settings window with live validation
- [x] Tray icon and context menu
- [x] Update manifest check
- [ ] WebDAV `OPTIONS` connection test (currently a placeholder)

### Milestone 2 — Sync Core
- [ ] `FileSystemWatcher` + debounce
- [ ] Local snapshot builder
- [ ] Upload changed files via `PUT`
- [ ] Create remote folders via `MKCOL`
- [ ] Activity log in UI
- [ ] Manual "Sync Now" wired end-to-end
- [ ] Start with Windows toggle

### Milestone 3 — Hardening
- [ ] Optional remote-to-local download (`PROPFIND` + `GET`)
- [ ] Retry logic with exponential backoff
- [ ] File-write stabilization (wait until file handle is closed before uploading)
- [ ] Error state recovery
- [ ] Better user-facing error messages

### Milestone 4 — Update Flow
- [ ] Download new executable to `updates/`
- [ ] SHA-256 verification
- [ ] Portable update helper (replaces binary after app exit)
- [ ] Relaunch after update
- [ ] Release packaging and signing

---

## 13. Acceptance Criteria

### Launch
- The published executable launches reliably from any writable folder.
- First run opens the settings window automatically.
- A tray icon is visible after successful startup.
- Startup failure shows an error dialog and never silently dies.

### Configuration
- All five required fields can be entered and saved.
- Config reloads correctly on relaunch.
- Password is recovered automatically via DPAPI on the same Windows user account.
- If secret decryption fails, the user is prompted to re-enter the password.

### Tray
- The tray icon is always visible while the app is running.
- The settings window can be reopened from the tray at any time.
- Exit from the tray menu fully closes the app.

### Sync
- New and changed local files are uploaded to the WebDAV server.
- Remote folders are created automatically as needed.
- "Sync Now" triggers an immediate upload pass.
- Sync errors are shown in the status area and written to the log.

### Security
- No password appears in `config.json`, any log file, or any UI label.
- DPAPI is used for all credential storage.
- HTTPS connections enforce certificate validation.

### Portability
- The entire app can be copied as a folder and run on any Windows machine with the same user account (DPAPI caveat documented for cross-machine moves).
- No installer is required.
- No admin rights are required.

---

## 14. Reference Documents

| Document | Location | Purpose |
|---|---|---|
| Product Requirements | `portable-win/docs/PRODUCT_REQUIREMENTS.md` | Full functional and UX requirements |
| Technical Plan | `portable-win/docs/WINDOWS_PORTABLE_PLAN.md` | Stack rationale, sync engine design, updater design |
| Architecture | `portable-win/docs/ARCHITECTURE.md` | Layer breakdown |
| Migration Notes | `portable-win/docs/MIGRATION.md` | Notes on migration from C++ to C# |
| Legacy C++ README | `legacy-win32/README.md` | C++ implementation reference |
| Update Manifest | `appcast.json` | Live update manifest (GitHub-hosted) |
