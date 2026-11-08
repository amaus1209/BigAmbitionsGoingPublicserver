using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GpsManager;

/// <summary>Windows: the access data in a window of their own (user 2026-11-01: "the access data appear in a separate
/// window on Windows"). Plain Win32 through P/Invoke - no Windows Forms, so the Windows build is made on Linux like the
/// rest. The values sit in read-only text fields (select, Ctrl+C), each with a copy button; "Open in browser" opens the
/// web interface. Common Controls 6 through an activation context, so the buttons look like Windows 10/11. It runs on a
/// thread of its own and never holds up the server; if the window cannot be made, a message box shows the same text.</summary>
[SupportedOSPlatform("windows")]
internal static class Win32Window
{
    internal sealed record Row(string Label, string Value);

    internal static ushort UiLanguage() => GetUserDefaultUILanguage();

    /// <summary>The manager's own console window: a click into it starts QuickEdit's selection, and Windows then holds
    /// every write to the console - the manager would stand still until a key is pressed. QuickEdit off, a title on.</summary>
    internal static void PrepareConsole()
    {
        try { Console.Title = "Going Public Server"; } catch { }
        try
        {
            IntPtr input = GetStdHandle(STD_INPUT_HANDLE);
            if (GetConsoleMode(input, out uint mode))
                SetConsoleMode(input, (mode & ~ENABLE_QUICK_EDIT_MODE) | ENABLE_EXTENDED_FLAGS);
        }
        catch { }
    }

    internal static Thread ShowAsync(string title, string intro, Row[] rows, string url, string openText, string copyText,
                                     string copiedText, string closeText, string note)
    {
        var t = new Thread(() =>
        {
            try { Run(title, intro, rows, url, openText, copyText, copiedText, closeText, note); }
            catch (Exception e)
            {
                Log.Warn($"access data window: {e.Message} - a message box instead.");
                try
                {
                    string text = intro + "\n\n" + string.Join("\n", rows.Select(r => r.Label + "   " + r.Value)) + "\n\n" + note;
                    MessageBoxW(IntPtr.Zero, text, title, MB_ICONINFORMATION | MB_SETFOREGROUND | MB_TOPMOST);
                }
                catch { }
            }
        })
        { IsBackground = true, Name = "access-data-window" };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return t;
    }

    // ── the window ──────────────────────────────────────────────────────────────────────────────────────────────────

    private const string ClassName = "GoingPublicServerAccess";
    private const int IdCopy = 100, IdEdit = 300, IdOpen = 200, IDOK = 1, IDCANCEL = 2;
    private static WndProc? _wndProc;            // referenced while the window lives (the GC must not collect it)
    private static Row[] _rows = Array.Empty<Row>();
    private static string _url = "", _copiedText = "";
    private static IntPtr _font, _mono;

    private static void Run(string title, string intro, Row[] rows, string url, string openText, string copyText,
                            string copiedText, string closeText, string note)
    {
        _rows = rows; _url = url; _copiedText = copiedText;
        UseVisualStyles();
        try { SetProcessDPIAware(); } catch { }
        int dpi = 96;
        try { dpi = (int)GetDpiForSystem(); } catch { }
        int S(int v) => v * dpi / 96;

        IntPtr inst = GetModuleHandleW(IntPtr.Zero);
        _wndProc = WindowProc;
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = inst,
            hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW),
            hIcon = LoadIconW(IntPtr.Zero, (IntPtr)IDI_INFORMATION),
            hIconSm = LoadIconW(IntPtr.Zero, (IntPtr)IDI_INFORMATION),
            hbrBackground = (IntPtr)(COLOR_BTNFACE + 1),
            lpszClassName = ClassName,
        };
        RegisterClassExW(ref wc);   // the second window of a run finds the class registered (error 1410) - fine

        var ncm = new NONCLIENTMETRICS { cbSize = (uint)Marshal.SizeOf<NONCLIENTMETRICS>() };
        if (SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, ncm.cbSize, ref ncm, 0))
        {
            _font = CreateFontIndirectW(ref ncm.lfMessageFont);
            var mono = ncm.lfMessageFont;
            mono.lfFaceName = "Consolas";
            mono.lfHeight = mono.lfHeight * 11 / 10;
            _mono = CreateFontIndirectW(ref mono);
        }

        int m = S(16), gap = S(8), labelW = S(100), editW = S(330), btnW = S(96), rowH = S(26), introH = S(36), noteH = S(52), barH = S(30);
        int clientW = m + labelW + gap + editW + gap + btnW + m;
        int clientH = m + introH + gap + rows.Length * (rowH + gap) + noteH + gap + barH + m;
        uint style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
        uint exStyle = WS_EX_CONTROLPARENT | WS_EX_TOPMOST;
        var rect = new RECT { right = clientW, bottom = clientH };
        AdjustWindowRectEx(ref rect, style, false, exStyle);
        int w = rect.right - rect.left, h = rect.bottom - rect.top;
        int x = Math.Max(0, (GetSystemMetrics(SM_CXSCREEN) - w) / 2), y = Math.Max(0, (GetSystemMetrics(SM_CYSCREEN) - h) / 2);
        IntPtr hwnd = CreateWindowExW(exStyle, ClassName, title, style, x, y, w, h, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);
        if (hwnd == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

        int yy = m;
        Child(hwnd, "STATIC", intro, SS_LEFT, 0, m, yy, clientW - 2 * m, introH, 0, _font);
        yy += introH + gap;
        for (int i = 0; i < rows.Length; i++)
        {
            Child(hwnd, "STATIC", rows[i].Label, SS_LEFT, 0, m, yy + S(5), labelW, rowH - S(5), 0, _font);
            Child(hwnd, "EDIT", rows[i].Value, ES_READONLY | ES_AUTOHSCROLL | WS_TABSTOP, WS_EX_CLIENTEDGE,
                  m + labelW + gap, yy, editW, rowH, IdEdit + i, _mono != IntPtr.Zero ? _mono : _font);
            Child(hwnd, "BUTTON", copyText, BS_PUSHBUTTON | WS_TABSTOP, 0, m + labelW + gap + editW + gap, yy, btnW, rowH, IdCopy + i, _font);
            yy += rowH + gap;
        }
        Child(hwnd, "STATIC", note, SS_LEFT, 0, m, yy, clientW - 2 * m, noteH, 0, _font);
        yy += noteH + gap;
        Child(hwnd, "BUTTON", openText, BS_PUSHBUTTON | WS_TABSTOP, 0, m, yy, S(190), barH, IdOpen, _font);
        IntPtr close = Child(hwnd, "BUTTON", closeText, BS_DEFPUSHBUTTON | WS_TABSTOP, 0, clientW - m - btnW, yy, btnW, barH, IDOK, _font);

        ShowWindow(hwnd, SW_SHOW);
        UpdateWindow(hwnd);
        SetForegroundWindow(hwnd);
        SetFocus(close);
        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (IsDialogMessageW(hwnd, ref msg)) continue;   // Tab, Enter (Close) and Esc
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        if (_font != IntPtr.Zero) DeleteObject(_font);
        if (_mono != IntPtr.Zero) DeleteObject(_mono);
        _font = _mono = IntPtr.Zero;
    }

    private static IntPtr Child(IntPtr parent, string cls, string text, uint style, uint exStyle, int x, int y, int w, int h, int id, IntPtr font)
    {
        IntPtr c = CreateWindowExW(exStyle, cls, text, WS_CHILD | WS_VISIBLE | style, x, y, w, h, parent, (IntPtr)id, GetModuleHandleW(IntPtr.Zero), IntPtr.Zero);
        if (c != IntPtr.Zero && font != IntPtr.Zero) SendMessageW(c, WM_SETFONT, font, (IntPtr)1);
        return c;
    }

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_COMMAND:
            {
                int id = (int)(wParam.ToInt64() & 0xFFFF), code = (int)((wParam.ToInt64() >> 16) & 0xFFFF);
                if (id == IDOK || id == IDCANCEL) { DestroyWindow(hWnd); return IntPtr.Zero; }
                if (code != BN_CLICKED) break;
                if (id == IdOpen)
                {
                    try { Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true }); } catch { }
                    return IntPtr.Zero;
                }
                if (id >= IdCopy && id < IdCopy + _rows.Length)
                {
                    Copy(hWnd, _rows[id - IdCopy].Value);
                    if (lParam != IntPtr.Zero) SetWindowTextW(lParam, _copiedText);
                    return IntPtr.Zero;
                }
                break;
            }
            case WM_CLOSE:
                DestroyWindow(hWnd);
                return IntPtr.Zero;
            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static void Copy(IntPtr owner, string text)
    {
        if (!OpenClipboard(owner)) return;
        try
        {
            EmptyClipboard();
            IntPtr mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)((text.Length + 1) * 2));
            if (mem == IntPtr.Zero) return;
            IntPtr p = GlobalLock(mem);
            if (p == IntPtr.Zero) { GlobalFree(mem); return; }
            Marshal.Copy(text.ToCharArray(), 0, p, text.Length);
            Marshal.WriteInt16(p, text.Length * 2, 0);
            GlobalUnlock(mem);
            if (SetClipboardData(CF_UNICODETEXT, mem) == IntPtr.Zero) GlobalFree(mem);
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Common Controls 6 for this thread's windows (WinForms does the same): a manifest in the temp folder
    /// and an activation context from it. Without it the buttons are drawn in the Windows 95 style - nothing else.</summary>
    private static void UseVisualStyles()
    {
        try
        {
            string file = Path.Combine(Path.GetTempPath(), "goingpublicserver-comctl6.manifest");
            File.WriteAllText(file,
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<assembly xmlns=\"urn:schemas-microsoft-com:asm.v1\" manifestVersion=\"1.0\"><dependency><dependentAssembly>" +
                "<assemblyIdentity type=\"win32\" name=\"Microsoft.Windows.Common-Controls\" version=\"6.0.0.0\" processorArchitecture=\"*\" " +
                "publicKeyToken=\"6595b64144ccf1df\" language=\"*\"/></dependentAssembly></dependency></assembly>");
            var ctx = new ACTCTX { cbSize = Marshal.SizeOf<ACTCTX>(), lpSource = file };
            IntPtr h = CreateActCtxW(ref ctx);
            if (h != IntPtr.Zero && h != new IntPtr(-1)) ActivateActCtx(h, out _);
        }
        catch { }
    }

    // ── Win32 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WS_OVERLAPPED = 0x00000000, WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000, WS_MINIMIZEBOX = 0x00020000,
                       WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_TABSTOP = 0x00010000;
    private const uint WS_EX_CLIENTEDGE = 0x00000200, WS_EX_TOPMOST = 0x00000008, WS_EX_CONTROLPARENT = 0x00010000;
    private const uint ES_AUTOHSCROLL = 0x0080, ES_READONLY = 0x0800, BS_PUSHBUTTON = 0x0, BS_DEFPUSHBUTTON = 0x1, SS_LEFT = 0x0;
    private const uint WM_DESTROY = 0x0002, WM_CLOSE = 0x0010, WM_SETFONT = 0x0030, WM_COMMAND = 0x0111;
    private const int BN_CLICKED = 0, SW_SHOW = 5, SM_CXSCREEN = 0, SM_CYSCREEN = 1, COLOR_BTNFACE = 15, IDC_ARROW = 32512, IDI_INFORMATION = 32516;
    private const uint SPI_GETNONCLIENTMETRICS = 0x0029, CF_UNICODETEXT = 13, GMEM_MOVEABLE = 0x0002;
    private const uint MB_ICONINFORMATION = 0x40, MB_SETFOREGROUND = 0x10000, MB_TOPMOST = 0x40000;
    private const int STD_INPUT_HANDLE = -10;
    private const uint ENABLE_QUICK_EDIT_MODE = 0x0040, ENABLE_EXTENDED_FLAGS = 0x0080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize, style;
        public WndProc lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public int ptX, ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LOGFONT
    {
        public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
        public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NONCLIENTMETRICS
    {
        public uint cbSize;
        public int iBorderWidth, iScrollWidth, iScrollHeight, iCaptionWidth, iCaptionHeight;
        public LOGFONT lfCaptionFont;
        public int iSmCaptionWidth, iSmCaptionHeight;
        public LOGFONT lfSmCaptionFont;
        public int iMenuWidth, iMenuHeight;
        public LOGFONT lfMenuFont, lfStatusFont, lfMessageFont;
        public int iPaddedBorderWidth;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ACTCTX
    {
        public int cbSize;
        public uint dwFlags;
        public string lpSource;
        public ushort wProcessorArchitecture, wLangId;
        public string? lpAssemblyDirectory, lpResourceName, lpApplicationName;
        public IntPtr hModule;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassExW(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h,
                                                 IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] private static extern bool IsDialogMessageW(IntPtr hDlg, ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowTextW(IntPtr hWnd, string text);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref RECT rect, uint style, bool menu, uint exStyle);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SystemParametersInfoW(uint action, uint param, ref NONCLIENTMETRICS value, uint winIni);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr mem);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateFontIndirectW(ref LOGFONT font);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandleW(IntPtr name);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr mem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr mem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr mem);
    [DllImport("kernel32.dll")] private static extern ushort GetUserDefaultUILanguage();
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int which);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr handle, uint mode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateActCtxW(ref ACTCTX ctx);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ActivateActCtx(IntPtr ctx, out IntPtr cookie);
}
