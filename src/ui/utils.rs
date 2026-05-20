// ── Utilities ─────────────────────────────────────────────────────────────────
unsafe fn set_status_dot_color(hwnd: HWND, color: u32) {
    let st = stmut(hwnd);
    st.status_dot_color = color;
    InvalidateRect(hwnd, Some(&st.status_strip_rect), TRUE);
}

unsafe fn restore_pair_idle_controls(hwnd: HWND) {
    let st = stmut(hwnd);
    let label = if st.auth_failure_notified || is_paired(&st.config) {
        "Pair again"
    } else {
        "Pair"
    };
    let pair_hwnd = GetDlgItem(hwnd, IDC_PAIR_DEVICE as i32);
    let _ = SetWindowTextW(pair_hwnd, &hstring(label));
    ShowWindow(pair_hwnd, SW_SHOW);
}

unsafe fn restore_server_status_after_pair_cancel(hwnd: HWND) {
    let st = stmut(hwnd);
    let status = if st.auth_failure_notified {
        "Pair again required"
    } else if is_paired(&st.config) {
        if st.connected {
            "All synced \u{00B7} paired with server"
        } else {
            "Offline"
        }
    } else {
        "Pair cancelled"
    };
    let color = if st.auth_failure_notified || !st.connected {
        C_RED
    } else {
        C_GREEN
    };
    let _ = SetWindowTextW(GetDlgItem(hwnd, IDC_SERVER_STATUS as i32), &hstring(status));
    set_status_dot_color(hwnd, color);
}

fn is_root_remote_folder(folder: &str) -> bool {
    let trimmed = folder.trim();
    trimmed.is_empty() || trimmed == "/" || trimmed == "\\"
}

fn is_paired(cfg: &Config) -> bool {
    !cfg.device_token_enc.trim().is_empty()
}

fn sync_is_busy(st: &WndState) -> bool {
    st.sync_status_state == crate::sync::ActivityState::Checking as usize
        || st.sync_status_state == crate::sync::ActivityState::Syncing as usize
}

unsafe fn set_status_strip_text(hwnd: HWND, text: &str) {
    let _ = SetWindowTextW(
        GetDlgItem(hwnd, IDC_SERVER_STATUS as i32),
        &hstring(text),
    );
}

unsafe fn update_sync_footer(hwnd: HWND, state: usize, progress: (usize, usize, usize)) {
    let status_hwnd = GetDlgItem(hwnd, IDC_SYNC_STATUS as i32);
    let prog_hwnd = GetDlgItem(hwnd, IDC_SYNC_PROGRESS as i32);
    let is_checking = state == crate::sync::ActivityState::Checking as usize;
    let is_syncing = state == crate::sync::ActivityState::Syncing as usize;
    let is_busy = is_checking || is_syncing;

    if is_busy && progress.1 > 0 {
        let done = progress.0.min(progress.1);
        let pct = (done * 100) / progress.1;
        let st = stmut(hwnd);
        let text = if let Some(eta_idx) = st.sync_status_text.find("ETA ") {
            let eta = st.sync_status_text[eta_idx..].trim();
            format!("{done} / {} files \u{00B7} {pct}% \u{00B7} {eta}", progress.1)
        } else {
            format!("{done} / {} files \u{00B7} {pct}%", progress.1)
        };
        let _ = SetWindowTextW(status_hwnd, &hstring(&text));
        SendMessageW(prog_hwnd, PBM_SETPOS, WPARAM(pct), LPARAM(0));
    } else if is_busy {
        let text = if is_checking {
            "Checking..."
        } else {
            "Syncing..."
        };
        let _ = SetWindowTextW(status_hwnd, &hstring(text));
        SendMessageW(prog_hwnd, PBM_SETPOS, WPARAM(0), LPARAM(0));
    } else {
        let _ = SetWindowTextW(status_hwnd, &hstring("Ready"));
        SendMessageW(prog_hwnd, PBM_SETPOS, WPARAM(0), LPARAM(0));
    }
}

unsafe fn update_status_strip_after_sync(hwnd: HWND, state: usize, progress: (usize, usize, usize)) {
    let st = stmut(hwnd);
    let is_checking = state == crate::sync::ActivityState::Checking as usize;
    let is_syncing = state == crate::sync::ActivityState::Syncing as usize;
    let is_idle = state == crate::sync::ActivityState::Idle as usize;
    let failed = progress.2;

    if is_checking {
        set_status_strip_text(hwnd, "Checking...");
        set_status_dot_color(hwnd, C_AMBER);
    } else if is_syncing {
        let text = if progress.1 > 0 {
            let done = progress.0.min(progress.1);
            let remaining = progress.1.saturating_sub(done);
            if remaining == 1 {
                "Syncing \u{00B7} 1 file remaining".to_string()
            } else {
                format!("Syncing \u{00B7} {remaining} files remaining")
            }
        } else {
            "Syncing".to_string()
        };
        set_status_strip_text(hwnd, &text);
        set_status_dot_color(hwnd, C_AMBER);
    } else if is_idle && is_paired(&st.config) {
        if failed > 0 {
            let text = if failed == 1 {
                "1 upload failed".to_string()
            } else {
                format!("{failed} uploads failed")
            };
            set_status_strip_text(hwnd, &text);
            set_status_dot_color(hwnd, C_AMBER);
        } else if st.connected {
            set_status_strip_text(hwnd, "All synced \u{00B7} paired with server");
            set_status_dot_color(hwnd, C_GREEN);
        } else {
            set_status_strip_text(hwnd, "Offline");
            set_status_dot_color(hwnd, C_RED);
        }
    }
    update_sync_footer(hwnd, state, progress);
}

unsafe fn update_status_strip_from_connection(hwnd: HWND) {
    let st = stmut(hwnd);
    if sync_is_busy(st) || st.auth_failure_notified {
        return;
    }
    if st.sync_last_failed > 0 {
        let text = if st.sync_last_failed == 1 {
            "1 upload failed".to_string()
        } else {
            format!("{} uploads failed", st.sync_last_failed)
        };
        set_status_strip_text(hwnd, &text);
        set_status_dot_color(hwnd, C_AMBER);
        return;
    }
    if is_paired(&st.config) {
        if st.connected {
            set_status_strip_text(hwnd, "All synced \u{00B7} paired with server");
            set_status_dot_color(hwnd, C_GREEN);
        } else {
            set_status_strip_text(hwnd, "Offline");
            set_status_dot_color(hwnd, C_RED);
        }
    } else if st.connected {
        set_status_strip_text(hwnd, "Connected");
        set_status_dot_color(hwnd, C_GREEN);
    } else {
        set_status_strip_text(hwnd, "Offline");
        set_status_dot_color(hwnd, C_RED);
    }
}

fn required_pair_field(value: Option<String>, name: &str) -> std::result::Result<String, String> {
    match value.and_then(non_empty) {
        Some(value) => Ok(value.trim().to_string()),
        None => Err(format!("Pairing approved but no {name} was returned.")),
    }
}

fn approved_remote_folder(remote_folder: Option<&str>) -> std::result::Result<String, String> {
    let Some(remote_folder) = remote_folder else {
        return Err("Pairing approved but no destination folder was returned.".to_string());
    };
    let raw = remote_folder.trim();
    if raw.is_empty() || raw == "/" || raw == "\\" {
        return Err(
            "Pairing approved without a customer destination folder. Re-pair after Laravel approves a concrete customer folder."
                .to_string(),
        );
    }
    if raw.starts_with('/')
        || raw.starts_with('\\')
        || raw.contains('/')
        || raw.contains('\\')
        || raw.contains("..")
        || raw.chars().any(char::is_control)
    {
        return Err(
            "Pairing approved with an invalid destination folder. Re-pair after Laravel approves a concrete customer folder."
                .to_string(),
        );
    }
    Ok(raw.to_string())
}

unsafe fn apply_server_readonly(hwnd: HWND) {
    update_server_tooltip(hwnd);
    let label = if is_paired(&stmut(hwnd).config) {
        "Server destination"
    } else {
        "Destination folder"
    };
    let _ = SetWindowTextW(GetDlgItem(hwnd, IDC_DEST_LABEL as i32), &hstring(label));
    stmut(hwnd).min_client_h = required_client_height(stmut(hwnd));
    layout_main(hwnd);
}

unsafe fn start_connection_check(hwnd: HWND) {
    let st = stmut(hwnd);
    let cfg = st.config.clone();
    let pass = st.password_plain.clone();
    if cfg.webdav_url.trim().is_empty() || cfg.username.trim().is_empty() || pass.trim().is_empty()
    {
        return;
    }
    let raw = hwnd.0 as isize;
    std::thread::spawn(move || {
        let ok = webdav::test_connection(&cfg, &pass).is_ok();
        unsafe {
            PostMessageW(
                HWND(raw as *mut _),
                WM_APP_CONNECTED,
                WPARAM(if ok { 1 } else { 0 }),
                LPARAM(0),
            )
            .ok();
        }
    });
}

fn is_sync_configured(cfg: &Config, pass: &str) -> bool {
    !cfg.watch_folder.trim().is_empty()
        && !cfg.webdav_url.trim().is_empty()
        && !cfg.username.trim().is_empty()
        && !pass.is_empty()
        && !cfg.remote_folder.trim().is_empty()
}

unsafe fn ensure_default_watch_folder(hwnd: HWND) {
    let st = stmut(hwnd);
    if !st.config.watch_folder.trim().is_empty() {
        return;
    }
    if let Some(path) = crate::xd::default_watch_folder() {
        st.config.watch_folder = path;
        let _ = SetWindowTextW(
            GetDlgItem(hwnd, IDC_WATCH_FOLDER as i32),
            &hstring(&st.config.watch_folder),
        );
    }
}

/// Stop any running engine and start a new one when credentials and folders are set.
unsafe fn restart_sync_engine(hwnd: HWND) -> std::result::Result<(), String> {
    read_ctrls(hwnd, stmut(hwnd));
    ensure_default_watch_folder(hwnd);
    let cfg = stmut(hwnd).config.clone();
    let pass = stmut(hwnd).password_plain.clone();
    if !is_sync_configured(&cfg, &pass) {
        stmut(hwnd).sync_engine = None;
        return Err(
            "Sync not started: origin folder, server credentials, and destination are required."
                .to_string(),
        );
    }
    {
        let st = stmut(hwnd);
        st.sync_status_text = "Starting...".to_string();
        st.sync_status_state = crate::sync::ActivityState::Checking as usize;
        st.sync_progress_done = 0;
        st.sync_progress_total = 0;
        st.sync_last_failed = 0;
        st.sync_started_at = None;
        st.sync_engine = None;
    }

    let raw = hwnd.0 as isize;
    let log: crate::sync::LogFn = Arc::new(move |m: String| {
        logs::append(&m);
        let s = Box::new(m);
        unsafe {
            PostMessageW(
                HWND(raw as *mut _),
                WM_APP_LOG,
                WPARAM(0),
                LPARAM(Box::into_raw(s) as isize),
            )
            .ok();
        }
    });
    let activity: crate::sync::ActivityFn = Arc::new(move |info| unsafe {
        PostMessageW(
            HWND(raw as *mut _),
            WM_APP_SYNC_ACTIVITY,
            WPARAM(info.state as usize),
            LPARAM(Box::into_raw(Box::new((info.completed, info.total, info.failed))) as isize),
        )
        .ok();
    });
    let auth_failed: crate::sync::AuthFailedFn = Arc::new(move || unsafe {
        PostMessageW(HWND(raw as *mut _), WM_APP_AUTH_FAILED, WPARAM(0), LPARAM(0)).ok();
    });

    match crate::sync::SyncEngine::start(cfg.clone(), pass, log, activity, auth_failed) {
        Ok(engine) => {
            stmut(hwnd).sync_engine = Some(engine);
            let started = format!("Sync engine started for {}", cfg.watch_folder);
            logs::append(&started);
            Ok(())
        }
        Err(err) => Err(err),
    }
}

unsafe fn read_ctrls(hwnd: HWND, st: &mut WndState) {
    st.config.watch_folder = gettext(hwnd, IDC_WATCH_FOLDER);
    st.config.start_with_windows = checked(hwnd, IDC_START_WINDOWS);
    st.config.sync_remote_changes = checked(hwnd, IDC_SYNC_REMOTE);
}

unsafe fn gettext(hwnd: HWND, id: u16) -> String {
    let h = GetDlgItem(hwnd, id as i32);
    let n = GetWindowTextLengthW(h);
    if n == 0 {
        return String::new();
    }
    let mut b = vec![0u16; (n + 1) as usize];
    GetWindowTextW(h, &mut b);
    String::from_utf16_lossy(&b[..n as usize])
}

unsafe fn checked(hwnd: HWND, id: u16) -> bool {
    SendMessageW(
        GetDlgItem(hwnd, id as i32),
        BM_GETCHECK,
        WPARAM(0),
        LPARAM(0),
    )
    .0 == BST_CHECKED.0 as isize
}

unsafe fn stmut(hwnd: HWND) -> &'static mut WndState {
    &mut *(GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut WndState)
}
unsafe fn state_ptr(hwnd: HWND) -> *mut WndState {
    GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut WndState
}

unsafe fn mkfont(name: &str, pt: i32, weight: i32) -> HFONT {
    let hdc = GetDC(None);
    let dpi = GetDeviceCaps(hdc, LOGPIXELSY);
    ReleaseDC(None, hdc);
    let h = -(pt * dpi / 72);
    let nw: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
    let mut lf = LOGFONTW {
        lfHeight: h,
        lfWeight: weight,
        ..Default::default()
    };
    let n = nw.len().min(lf.lfFaceName.len());
    lf.lfFaceName[..n].copy_from_slice(&nw[..n]);
    CreateFontIndirectW(&lf)
}

unsafe fn mkfont_underline(name: &str, pt: i32, weight: i32) -> HFONT {
    let hdc = GetDC(None);
    let dpi = GetDeviceCaps(hdc, LOGPIXELSY);
    ReleaseDC(None, hdc);
    let h = -(pt * dpi / 72);
    let nw: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
    let mut lf = LOGFONTW {
        lfHeight: h,
        lfWeight: weight,
        lfUnderline: 1,
        ..Default::default()
    };
    let n = nw.len().min(lf.lfFaceName.len());
    lf.lfFaceName[..n].copy_from_slice(&nw[..n]);
    CreateFontIndirectW(&lf)
}

fn hstring(s: &str) -> HSTRING {
    HSTRING::from(s)
}

unsafe fn msgbox_yn(hwnd: HWND, text: &str, title: &str) -> bool {
    MessageBoxW(
        hwnd,
        &hstring(text),
        &hstring(title),
        MB_YESNO | MB_ICONQUESTION,
    )
    .0 == IDYES.0
}

/// Non-blocking notice: writes to `logs/` and Recent Activity (does not freeze the UI).
unsafe fn notify_user(hwnd: HWND, message: &str) {
    logs::append(message);
    let s = Box::new(format!("! {message}"));
    PostMessageW(
        hwnd,
        WM_APP_LOG,
        WPARAM(0),
        LPARAM(Box::into_raw(s) as isize),
    )
    .ok();
}

unsafe fn notify_user_status(hwnd: HWND, status: &str, dot_color: u32, message: &str) {
    set_status_strip_text(hwnd, status);
    set_status_dot_color(hwnd, dot_color);
    notify_user(hwnd, message);
}

fn display_activity_name(path: &str) -> &str {
    path.rsplit(['/', '\\']).next().unwrap_or(path)
}

fn format_eta(seconds: u64) -> String {
    if seconds < 60 {
        format!("{}s", seconds)
    } else {
        format!("{}m {:02}s", seconds / 60, seconds % 60)
    }
}

fn validate_webdav_url(url: &str) -> std::result::Result<(), String> {
    if url.trim().to_ascii_lowercase().starts_with("https://") {
        Ok(())
    } else {
        Err("Server URL must start with https://".to_string())
    }
}

fn non_empty(value: String) -> Option<String> {
    if value.trim().is_empty() {
        None
    } else {
        Some(value)
    }
}
