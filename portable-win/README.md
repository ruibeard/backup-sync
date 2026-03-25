# WebDavSync Portable

This folder contains the new Windows-only portable implementation.

Current scope:

- WinForms tray application scaffold
- portable config and secrets layout
- DPAPI-backed password storage
- startup registration helper
- update manifest model and basic checker
- sync service skeleton for the new implementation

This code is intentionally separate from the existing C++ Win32 app in the repository root.
