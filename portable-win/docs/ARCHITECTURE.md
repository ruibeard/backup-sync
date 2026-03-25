# Architecture

This folder documents the new Windows-only portable implementation.

Initial layers:

- `Configuration`
  - portable file layout and config persistence
- `Secrets`
  - DPAPI secret protection
- `Ui`
  - WinForms settings window
- `Windows`
  - tray bootstrap and startup registration
- `Sync`
  - sync lifecycle and status model
- `Updates`
  - update manifest and checker

The first code pass is intentionally narrow:

- enough to establish the project structure
- enough to make the runtime flow concrete
- not yet the full WebDAV implementation
