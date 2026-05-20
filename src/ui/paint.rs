// ── Background paint ──────────────────────────────────────────────────────────
// Paints window bg, divider lines, quiet status strip, and inline status dot.
unsafe fn paint_bg(hwnd: HWND, hdc: HDC) {
    let mut cr = RECT::default();
    GetClientRect(hwnd, &mut cr).ok();

    // Window fill
    let br = CreateSolidBrush(COLORREF(C_WIN_BG));
    FillRect(hdc, &cr, br);
    DeleteObject(br);

    let st = state_ptr(hwnd);
    if st.is_null() {
        return;
    }

    let accent_color = (*st).status_dot_color;
    let sr = (*st).status_strip_rect;
    if sr.right > sr.left && sr.bottom > sr.top {
        let br_strip = CreateSolidBrush(COLORREF(C_STATUS_BG));
        FillRect(hdc, &sr, br_strip);
        DeleteObject(br_strip);

        let accent = RECT {
            left: sr.left,
            top: sr.top,
            right: sr.left + STATUS_ACCENT_W,
            bottom: sr.bottom,
        };
        let br_accent = CreateSolidBrush(COLORREF(accent_color));
        FillRect(hdc, &accent, br_accent);
        DeleteObject(br_accent);
    }

    // Status dot (colored; spinner spoke when busy)
    let dot_r = (*st).server_status_rect;
    if dot_r.right > dot_r.left && dot_r.bottom > dot_r.top {
        let br_dot = CreateSolidBrush(COLORREF(accent_color));
        let op_br = SelectObject(hdc, br_dot);
        Ellipse(hdc, dot_r.left, dot_r.top, dot_r.right, dot_r.bottom);
        SelectObject(hdc, op_br);
        DeleteObject(br_dot);

        let is_busy = (*st).sync_status_state == crate::sync::ActivityState::Checking as usize
            || (*st).sync_status_state == crate::sync::ActivityState::Syncing as usize;
        if is_busy {
            let cx = (dot_r.left + dot_r.right) / 2;
            let cy = (dot_r.top + dot_r.bottom) / 2;
            let r = ((dot_r.right - dot_r.left) / 2) as f64;
            let frame = (*st).sync_anim_frame as f64;
            let angle = frame * std::f64::consts::PI / 3.0;
            let x2 = cx + (r * angle.cos()) as i32;
            let y2 = cy - (r * angle.sin()) as i32;
            let pen = CreatePen(PS_SOLID, 2, COLORREF(0x00FFFFFF));
            let op_pen = SelectObject(hdc, pen);
            let _ = MoveToEx(hdc, cx, cy, None);
            let _ = LineTo(hdc, x2, y2);
            SelectObject(hdc, op_pen);
            DeleteObject(pen);
        }
    }

    // Subtle divider lines between sections
    for &dy in &(*st).dividers {
        if dy <= 0 {
            continue;
        }
        let hp = CreatePen(PS_SOLID, 1, COLORREF(C_DIVIDER));
        let op = SelectObject(hdc, hp);
        let _ = MoveToEx(hdc, M, dy, None);
        let _ = LineTo(hdc, WIN_W - M, dy);
        SelectObject(hdc, op);
        let _ = DeleteObject(hp);
    }
}

// ── Edit subclass: flat 1px border ───────────────────────────────────────────
unsafe extern "system" fn edit_sub(
    hwnd: HWND,
    msg: u32,
    wp: WPARAM,
    lp: LPARAM,
    _uid: usize,
    _ref: usize,
) -> LRESULT {
    let id = GetDlgCtrlID(hwnd) as u16;

    match msg {
        WM_SETFOCUS | WM_KILLFOCUS => {
            let st = state_ptr(GetParent(hwnd));
            if !st.is_null() {
                (*st).focused_edit = if msg == WM_SETFOCUS { id } else { 0 };
            }
            let r = DefSubclassProc(hwnd, msg, wp, lp);
            SetWindowPos(
                hwnd,
                None,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED,
            )
            .ok();
            r
        }
        WM_NCPAINT => {
            let st = state_ptr(GetParent(hwnd));
            let focused = !st.is_null() && (*st).focused_edit == id;
            let hdc = GetWindowDC(hwnd);
            let mut wr = RECT::default();
            GetWindowRect(hwnd, &mut wr).ok();
            let (w, h) = (wr.right - wr.left, wr.bottom - wr.top);
            let border_clr = if focused {
                C_INPUT_FOCUS
            } else {
                C_INPUT_BORDER
            };

            let hpen = CreatePen(PS_SOLID, 1, COLORREF(border_clr));
            let op = SelectObject(hdc, hpen);
            let ob = SelectObject(hdc, GetStockObject(NULL_BRUSH));
            Rectangle(hdc, 0, 0, w, h);
            SelectObject(hdc, op);
            SelectObject(hdc, ob);
            DeleteObject(hpen);

            ReleaseDC(hwnd, hdc);
            LRESULT(0)
        }
        _ => DefSubclassProc(hwnd, msg, wp, lp),
    }
}
