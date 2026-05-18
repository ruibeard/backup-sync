// ── Background paint ──────────────────────────────────────────────────────────
// Paints window bg, divider lines, and inline status dot + text.
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

    if (*st).sync_icon.0 != 0 {
        let r = (*st).sync_icon_rect;
        let _ = DrawIconEx(
            hdc,
            r.left,
            r.top,
            (*st).sync_icon,
            r.right - r.left,
            r.bottom - r.top,
            0,
            HBRUSH(0),
            DI_NORMAL,
        );
    }

    // Subtle divider lines between sections
    for &dy in &(*st).dividers {
        let hp = CreatePen(PS_SOLID, 1, COLORREF(C_DIVIDER));
        let op = SelectObject(hdc, hp);
        MoveToEx(hdc, M, dy, None);
        LineTo(hdc, WIN_W - M, dy);
        SelectObject(hdc, op);
        DeleteObject(hp);
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

