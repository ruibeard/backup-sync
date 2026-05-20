unsafe fn draw_activity_text(hdc: HDC, rc: &mut RECT, text: &str, format: DRAW_TEXT_FORMAT) {
    let mut wide: Vec<u16> = text.encode_utf16().collect();
    DrawTextW(hdc, &mut wide, rc, format);
}

fn activity_row_height(row: &ActivityRow) -> i32 {
    if row.kind == ActivityKind::Done || row.kind == ActivityKind::Info || row.kind == ActivityKind::Error {
        ACTIVITY_ROW_H_DONE
    } else {
        ACTIVITY_ROW_H_ACTIVE
    }
}

fn activity_icon_char(kind: ActivityKind, done: bool) -> &'static str {
    if done {
        "\u{2713}"
    } else {
        match kind {
            ActivityKind::Uploading => "\u{2B06}",
            ActivityKind::Downloading => "\u{2B07}",
            ActivityKind::Error => "!",
            _ => " ",
        }
    }
}

fn upload_replace_key(name: &str) -> String {
    format!("upload:{name}")
}

fn download_replace_key(name: &str) -> String {
    format!("download:{name}")
}

fn row_from_log_message(message: &str) -> Option<(Option<String>, ActivityRow)> {
    if let Some(rest) = message.strip_prefix("! ") {
        return Some((
            None,
            ActivityRow {
                label: rest.to_string(),
                kind: ActivityKind::Info,
                pct: None,
                replace_key: None,
            },
        ));
    }
    if message.starts_with("Checking remote files")
        || message.starts_with("Counting local files")
        || message.starts_with("Comparing local to remote")
        || message.starts_with("Checking remote changes")
        || message.ends_with(" file(s) to upload")
    {
        return Some((
            None,
            ActivityRow {
                label: message.to_string(),
                kind: ActivityKind::Info,
                pct: None,
                replace_key: None,
            },
        ));
    }
    if let Some(rest) = message.strip_prefix("Upload progress: ") {
        let (path, pct) = rest.split_once('|')?;
        let name = display_activity_name(path);
        let pct: u8 = pct.parse().ok()?;
        let key = upload_replace_key(name);
        let done = pct >= 100;
        return Some((
            Some(key.clone()),
            ActivityRow {
                label: if done {
                    format!("Uploaded {name}")
                } else {
                    format!("Uploading {name}")
                },
                kind: if done {
                    ActivityKind::Done
                } else {
                    ActivityKind::Uploading
                },
                pct: if done { None } else { Some(pct) },
                replace_key: Some(key),
            },
        ));
    }
    if let Some(path) = message.strip_prefix("Uploading: ") {
        let name = display_activity_name(path);
        let key = upload_replace_key(name);
        return Some((
            Some(key.clone()),
            ActivityRow {
                label: format!("Uploading {name}"),
                kind: ActivityKind::Uploading,
                pct: None,
                replace_key: Some(key),
            },
        ));
    }
    if let Some(path) = message.strip_prefix("Uploaded: ") {
        let name = display_activity_name(path);
        let key = upload_replace_key(name);
        return Some((
            Some(key.clone()),
            ActivityRow {
                label: format!("Uploaded {name}"),
                kind: ActivityKind::Done,
                pct: None,
                replace_key: Some(key),
            },
        ));
    }
    if let Some(rest) = message.strip_prefix("Upload failed ") {
        let name = display_activity_name(rest);
        return Some((
            None,
            ActivityRow {
                label: format!("Upload failed {name}"),
                kind: ActivityKind::Error,
                pct: None,
                replace_key: None,
            },
        ));
    }
    if let Some(path) = message.strip_prefix("Downloading: ") {
        let name = display_activity_name(path);
        let key = download_replace_key(name);
        return Some((
            Some(key.clone()),
            ActivityRow {
                label: format!("Downloading {name}"),
                kind: ActivityKind::Downloading,
                pct: None,
                replace_key: Some(key),
            },
        ));
    }
    if let Some(path) = message.strip_prefix("Downloaded: ") {
        let name = display_activity_name(path);
        let key = download_replace_key(name);
        return Some((
            Some(key),
            ActivityRow {
                label: format!("Downloaded {name}"),
                kind: ActivityKind::Done,
                pct: None,
                replace_key: None,
            },
        ));
    }
    None
}

unsafe fn activity_list_hwnd(hwnd: HWND) -> HWND {
    GetDlgItem(hwnd, IDC_ACTIVITY_LIST as i32)
}

unsafe fn refresh_activity_listbox(hwnd: HWND) {
    let st = stmut(hwnd);
    let hlb = activity_list_hwnd(hwnd);
    SendMessageW(hlb, LB_RESETCONTENT, WPARAM(0), LPARAM(0));
    if st.activity_rows.is_empty() && st.activity_show_empty {
        SendMessageW(hlb, LB_ADDSTRING, WPARAM(0), LPARAM(0));
    } else {
        for _ in &st.activity_rows {
            SendMessageW(hlb, LB_ADDSTRING, WPARAM(0), LPARAM(0));
        }
    }
    InvalidateRect(hlb, None, TRUE);
}

unsafe fn push_activity_row(hwnd: HWND, row: ActivityRow) {
    let st = stmut(hwnd);
    if st.activity_show_empty {
        st.activity_show_empty = false;
        st.activity_rows.clear();
    }
    st.activity_rows.insert(0, row);
    if st.activity_rows.len() > MAX_ACTIVITY_ROWS {
        st.activity_rows.truncate(MAX_ACTIVITY_ROWS);
    }
    refresh_activity_listbox(hwnd);
}

unsafe fn replace_activity_row(hwnd: HWND, replace_key: &str, row: ActivityRow) {
    let st = stmut(hwnd);
    st.activity_show_empty = false;
    if let Some(idx) = st
        .activity_rows
        .iter()
        .position(|r| r.replace_key.as_deref() == Some(replace_key))
    {
        st.activity_rows[idx] = row;
    } else {
        st.activity_rows.insert(0, row);
        if st.activity_rows.len() > MAX_ACTIVITY_ROWS {
            st.activity_rows.truncate(MAX_ACTIVITY_ROWS);
        }
    }
    refresh_activity_listbox(hwnd);
}

unsafe fn apply_activity_log(hwnd: HWND, message: &str) {
    let Some((replace_key, row)) = row_from_log_message(message) else {
        return;
    };
    if stmut(hwnd).activity_show_empty {
        stmut(hwnd).activity_show_empty = false;
        stmut(hwnd).activity_rows.clear();
    }
    if let Some(key) = replace_key {
        replace_activity_row(hwnd, &key, row);
    } else {
        push_activity_row(hwnd, row);
    }
}

unsafe fn on_measure_item(hwnd: HWND, lp: LPARAM) -> LRESULT {
    let mis = &mut *(lp.0 as *mut MEASUREITEMSTRUCT);
    if mis.CtlID != IDC_ACTIVITY_LIST as u32 {
        return LRESULT(0);
    }
    let st = state_ptr(hwnd);
    if st.is_null() {
        return LRESULT(0);
    }
    let rows = &(*st).activity_rows;
    let show_empty = (*st).activity_show_empty;
    mis.itemHeight = if rows.is_empty() && show_empty {
        MIN_ACTIVITY_LIST_H as u32
    } else if let Some(row) = rows.get(mis.itemID as usize) {
        activity_row_height(row) as u32
    } else {
        ACTIVITY_ROW_H_DONE as u32
    };
    LRESULT(1)
}

unsafe fn draw_activity_row(
    hdc: HDC,
    rc: &RECT,
    row: Option<&ActivityRow>,
    empty: bool,
    anim_frame: usize,
    hf: HFONT,
) {
    let hbr = CreateSolidBrush(COLORREF(C_STATUS_BG));
    FillRect(hdc, rc, hbr);
    DeleteObject(hbr);

    if empty {
        let of = SelectObject(hdc, hf);
        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, COLORREF(0x00999999));
        let mut tr = *rc;
        draw_activity_text(
            hdc,
            &mut tr,
            "No recent activity",
            DT_CENTER | DT_VCENTER | DT_SINGLELINE,
        );
        SelectObject(hdc, of);
        return;
    }

    let Some(row) = row else {
        return;
    };

    let done = row.kind == ActivityKind::Done;
    let show_bar = row.kind == ActivityKind::Uploading || row.kind == ActivityKind::Downloading;
    let icon = activity_icon_char(row.kind, done);
    let of = SelectObject(hdc, hf);
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, COLORREF(C_LABEL));

    let mut top_line = *rc;
    top_line.left += 8;
    top_line.top += 4;
    top_line.bottom = if show_bar {
        rc.bottom - 10
    } else {
        rc.bottom - 4
    };

    let mut icon_rc = top_line;
    icon_rc.right = icon_rc.left + 14;
    draw_activity_text(
        hdc,
        &mut icon_rc,
        icon,
        DT_LEFT | DT_VCENTER | DT_SINGLELINE,
    );

    let mut label_rc = top_line;
    label_rc.left += 18;
    if show_bar {
        label_rc.right -= 44;
    }
    draw_activity_text(
        hdc,
        &mut label_rc,
        &row.label,
        DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS,
    );

    if show_bar {
        let status = if done {
            Some("Done".to_string())
        } else if let Some(pct) = row.pct {
            Some(format!("{pct}%"))
        } else {
            None
        };
        if let Some(status) = status {
            SetTextColor(hdc, COLORREF(if done { C_GREEN } else { 0x00999999 }));
            let mut pct_rc = top_line;
            pct_rc.left = pct_rc.right - 40;
            draw_activity_text(
                hdc,
                &mut pct_rc,
                &status,
                DT_RIGHT | DT_VCENTER | DT_SINGLELINE,
            );
        }

        let bar_left = top_line.left;
        let bar_right = top_line.right;
        let bar_top = rc.bottom - 8;
        let bar_bottom = rc.bottom - 5;
        let track = RECT {
            left: bar_left,
            top: bar_top,
            right: bar_right,
            bottom: bar_bottom,
        };
        let br_track = CreateSolidBrush(COLORREF(C_PROGRESS_TRACK));
        FillRect(hdc, &track, br_track);
        DeleteObject(br_track);

        let inner_w = (bar_right - bar_left).max(1);
        let fill_w = if let Some(pct) = row.pct {
            (inner_w * pct as i32) / 100
        } else {
            let chunk = (inner_w / 3).max(8);
            let travel = inner_w - chunk;
            let offset = if travel > 0 {
                ((anim_frame as i32 * chunk / 2) % travel).max(0)
            } else {
                0
            };
            chunk + offset
        };
        if fill_w > 0 {
            let fill = RECT {
                left: bar_left,
                top: bar_top,
                right: bar_left + fill_w.min(inner_w),
                bottom: bar_bottom,
            };
            let br_fill = CreateSolidBrush(COLORREF(C_PROGRESS_MINI));
            FillRect(hdc, &fill, br_fill);
            DeleteObject(br_fill);
        }
    } else if done {
        SetTextColor(hdc, COLORREF(C_GREEN));
        let mut done_rc = top_line;
        done_rc.left = done_rc.right - 40;
        draw_activity_text(
            hdc,
            &mut done_rc,
            "Done",
            DT_RIGHT | DT_VCENTER | DT_SINGLELINE,
        );
    }

    SelectObject(hdc, of);

    let hp = CreatePen(PS_SOLID, 1, COLORREF(0x00F0F0F0));
    let op = SelectObject(hdc, hp);
    let _ = MoveToEx(hdc, rc.left, rc.bottom - 1, None);
    let _ = LineTo(hdc, rc.right, rc.bottom - 1);
    SelectObject(hdc, op);
    DeleteObject(hp);
}

unsafe fn on_draw_activity_item(lp: LPARAM) -> LRESULT {
    let di = &*(lp.0 as *const DRAWITEMSTRUCT);
    if di.CtlID != IDC_ACTIVITY_LIST as u32 {
        return LRESULT(0);
    }

    let parent = GetParent(di.hwndItem);
    let st = state_ptr(parent);
    if st.is_null() {
        return LRESULT(0);
    }

    let rows = &(*st).activity_rows;
    let empty = rows.is_empty() && (*st).activity_show_empty;
    let row = if empty {
        None
    } else {
        rows.get(di.itemID as usize)
    };

    draw_activity_row(
        di.hDC,
        &di.rcItem,
        row,
        empty && di.itemID == 0,
        (*st).sync_anim_frame,
        (*st).hfont_small,
    );
    LRESULT(1)
}
