# Migration Notes

The repository currently has:

- the original C++ Win32 app in the root project
- the new Windows-only portable implementation under `portable-win/`

Migration approach:

1. Build the new portable implementation in parallel.
2. Port config behavior and UI intent from the current app.
3. Re-implement sync and WebDAV logic in the new codebase.
4. Add the portable updater flow.
5. Decide later whether the old C++ app should be archived or retained.
