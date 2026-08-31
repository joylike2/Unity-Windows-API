#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 트레이 아이콘의 Win32 구현.
    /// 유니티 창의 WndProc 을 가로채지 않고 보이지 않는 창을 따로 만들어 씁니다.
    /// 유니티 창을 서브클래싱하면 에디터에서 도메인이 갈아엎일 때 에디터가 통째로 죽습니다.
    /// </summary>
    internal static class DeskTrayNative {

        /// <summary> 이 플랫폼이 트레이를 제공하는지 여부 </summary>
        internal static bool IsSupported => true;

        private const int TRAY_ICON_ID = 1;

        // 0 은 TrackPopupMenu 에서 "아무것도 안 골랐다" 를 뜻하므로 메뉴 번호를 1 부터 시작합니다.
        private const int MENU_ID_BASE = 1;

        private const int TOOLTIP_MAX_LENGTH = 127;

        // 델리게이트를 지역 변수로 넘기면 GC 수거 후 OS 가 죽은 주소를 호출해 크래시합니다.
        private static readonly WndProcDelegate WND_PROC = TrayWindowProc;

        private static IntPtr _windowHandle;
        private static IntPtr _iconHandle;
        private static bool _ownsIcon;
        private static string _className;

        // WndProc 이 적고 메인 루프가 가져갑니다. 둘 다 유니티 메인 스레드라 잠금이 필요 없습니다.
        private static DESK_TRAY_SIGNAL _pendingSignal;

        #region 수명

        /// <summary> 숨은 창을 만들고 트레이에 아이콘을 등록한다 </summary>
        internal static bool Create(string tooltip, DeskTrayIconSource icon) {
            if (_windowHandle != IntPtr.Zero) {
                return true;
            }

            if (!TryCreateMessageWindow()) {
                return false;
            }

            _iconHandle = LoadIconHandle(icon, out _ownsIcon);

            if (!SendIconMessage(NativeMethods.NIM_ADD, tooltip)) {
                Debug.LogError($"[DeskTrayNative] 트레이 아이콘 등록에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                Destroy();
                return false;
            }

            return true;
        }

        /// <summary> 아이콘을 내리고 만들어 둔 자원을 전부 되돌린다 </summary>
        internal static void Destroy() {
            if (_windowHandle != IntPtr.Zero) {
                SendIconMessage(NativeMethods.NIM_DELETE, null);
                NativeMethods.DestroyWindow(_windowHandle);
                _windowHandle = IntPtr.Zero;
            }

            // 실행 파일이나 파일에서 뽑아온 아이콘만 우리 것입니다. 공용 아이콘을 지우면 안 됩니다.
            if (_ownsIcon && _iconHandle != IntPtr.Zero) {
                NativeMethods.DestroyIcon(_iconHandle);
            }

            _iconHandle = IntPtr.Zero;
            _ownsIcon = false;

            if (!string.IsNullOrEmpty(_className)) {
                NativeMethods.UnregisterClass(_className, NativeMethods.GetModuleHandle(null));
                _className = null;
            }

            _pendingSignal = DESK_TRAY_SIGNAL.NONE;
        }

        /// <summary>
        /// 트레이 메시지만 받는 창을 만든다.
        /// 클래스 이름을 매번 새로 뽑는 이유는, 앞선 등록이 남아 있을 때 그 창 함수가
        /// 이미 사라진 코드를 가리키고 있을 수 있기 때문이다.
        /// </summary>
        private static bool TryCreateMessageWindow() {
            _className = "DeskWindowsTray_" + Guid.NewGuid().ToString("N");

            IntPtr instance = NativeMethods.GetModuleHandle(null);

            NativeWndClassEx windowClass = new NativeWndClassEx {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeWndClassEx)),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WND_PROC),
                hInstance = instance,
                lpszClassName = _className,
            };

            if (NativeMethods.RegisterClassEx(ref windowClass) == 0) {
                Debug.LogError($"[DeskTrayNative] 창 클래스 등록에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                _className = null;
                return false;
            }

            // 화면에 띄우지 않습니다. ShowWindow 를 부르지 않으면 창은 보이지 않습니다.
            _windowHandle = NativeMethods.CreateWindowEx(0, _className, _className, 0, 0, 0, 0, 0,
                                                         IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero) {
                Debug.LogError($"[DeskTrayNative] 트레이용 창 생성에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                NativeMethods.UnregisterClass(_className, instance);
                _className = null;
                return false;
            }

            return true;
        }

        #endregion 수명

        #region 아이콘

        /// <summary> 툴팁 글자를 바꾼다 </summary>
        internal static bool SetTooltip(string tooltip) {
            if (_windowHandle == IntPtr.Zero) {
                return false;
            }

            return SendIconMessage(NativeMethods.NIM_MODIFY, tooltip);
        }

        private static bool SendIconMessage(uint message, string tooltip) {
            NativeNotifyIconData data = new NativeNotifyIconData {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeNotifyIconData)),
                hWnd = _windowHandle,
                uID = TRAY_ICON_ID,
            };

            if (message != NativeMethods.NIM_DELETE) {
                data.uFlags = NativeMethods.NIF_ICON | NativeMethods.NIF_MESSAGE | NativeMethods.NIF_TIP;
                data.uCallbackMessage = NativeMethods.WM_TRAY_CALLBACK;
                data.hIcon = _iconHandle;
                data.szTip = Shorten(tooltip);
            }

            return NativeMethods.Shell_NotifyIcon(message, ref data);
        }

        /// <summary> 툴팁은 길이 제한이 있어 넘치면 잘라야 한다. 안 자르면 등록 자체가 실패한다. </summary>
        private static string Shorten(string tooltip) {
            if (string.IsNullOrEmpty(tooltip)) {
                return string.Empty;
            }

            return tooltip.Length <= TOOLTIP_MAX_LENGTH ? tooltip : tooltip.Substring(0, TOOLTIP_MAX_LENGTH);
        }

        /// <summary> 떠 있는 아이콘의 그림을 바꾼다 </summary>
        internal static bool SetIcon(DeskTrayIconSource icon) {
            if (_windowHandle == IntPtr.Zero) {
                return false;
            }

            IntPtr replacement = LoadIconHandle(icon, out bool owns);

            if (replacement == IntPtr.Zero) {
                return false;
            }

            IntPtr previous = _iconHandle;
            bool ownedPrevious = _ownsIcon;

            _iconHandle = replacement;
            _ownsIcon = owns;

            if (!SendIconMessage(NativeMethods.NIM_MODIFY, null)) {
                Debug.LogError($"[DeskTrayNative] 아이콘 교체에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                return false;
            }

            // 교체가 끝난 뒤에 지웁니다. 먼저 지우면 잠깐 빈 아이콘이 보일 수 있습니다.
            if (ownedPrevious && previous != IntPtr.Zero) {
                NativeMethods.DestroyIcon(previous);
            }

            return true;
        }

        /// <summary>
        /// 트레이가 실제로 그리는 아이콘 크기. 배율이 높은 화면에서는 16 보다 큽니다.
        /// </summary>
        internal static int GetPreferredIconSize() {
            return NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        }

        /// <summary>
        /// 쓸 아이콘을 구한다. 넘겨받은 그림 → 지정한 .ico → 실행 파일 아이콘 → 윈도우 기본 아이콘 순으로 물러난다.
        /// </summary>
        /// <param name="owns">우리가 만든 아이콘이라 나중에 지워야 하면 true.</param>
        private static IntPtr LoadIconHandle(DeskTrayIconSource source, out bool owns) {
            owns = false;

            if (source.HasPixels) {
                IntPtr fromPixels = CreateIconFromPixels(source.Pixels, source.Width, source.Height);

                if (fromPixels != IntPtr.Zero) {
                    owns = true;
                    return fromPixels;
                }

                Debug.LogWarning("[DeskTrayNative] 그림을 아이콘으로 만들지 못해 실행 파일 아이콘을 씁니다.");
            }

            if (!string.IsNullOrEmpty(source.FilePath)) {
                IntPtr fromFile = NativeMethods.LoadImage(IntPtr.Zero, source.FilePath, NativeMethods.IMAGE_ICON, 0, 0,
                                                          NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);

                if (fromFile != IntPtr.Zero) {
                    owns = true;
                    return fromFile;
                }

                Debug.LogWarning($"[DeskTrayNative] 아이콘 파일을 읽지 못해 실행 파일 아이콘을 씁니다: {source.FilePath}");
            }

            IntPtr fromExecutable = ExtractExecutableIcon();

            if (fromExecutable != IntPtr.Zero) {
                owns = true;
                return fromExecutable;
            }

            // 공용 아이콘이라 DestroyIcon 대상이 아닙니다.
            return NativeMethods.LoadIcon(IntPtr.Zero, NativeMethods.IDI_APPLICATION);
        }

        /// <summary>
        /// BGRA 픽셀로 아이콘을 만든다.
        /// 색 비트맵과 마스크 비트맵을 각각 만들어 넘겨야 하며, 만든 뒤에는 둘 다 지워야 한다.
        /// 아이콘이 그 내용을 복사해 가므로 남겨 둘 이유가 없다.
        /// </summary>
        /// <summary> BGRA 픽셀을 담은 32비트 비트맵을 만든다 </summary>
        private static IntPtr CreateBgraBitmap(byte[] pixels, int width, int height, out IntPtr bits) {
            NativeBitmapInfoHeader header = new NativeBitmapInfoHeader {
                biSize = (uint)Marshal.SizeOf(typeof(NativeBitmapInfoHeader)),
                biWidth = width,

                // 음수라야 첫 줄이 위쪽이 된다. 양수면 그림이 뒤집힌다.
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
            };

            IntPtr bitmap = NativeMethods.CreateDIBSection(IntPtr.Zero, ref header, NativeMethods.DIB_RGB_COLORS,
                                                           out bits, IntPtr.Zero, 0);

            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) {
                Debug.LogError($"[DeskTrayNative] 비트맵 생성에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                return IntPtr.Zero;
            }

            Marshal.Copy(pixels, 0, bits, Mathf.Min(pixels.Length, width * height * 4));
            return bitmap;
        }

        private static IntPtr CreateIconFromPixels(byte[] pixels, int width, int height) {
            IntPtr colorBitmap = CreateBgraBitmap(pixels, width, height, out IntPtr _);

            if (colorBitmap == IntPtr.Zero) {
                return IntPtr.Zero;
            }

            // 32비트 그림은 알파가 모양을 정하므로 마스크는 비어 있어도 된다.
            IntPtr maskBitmap = NativeMethods.CreateBitmap(width, height, 1, 1, IntPtr.Zero);

            try {
                if (maskBitmap == IntPtr.Zero) {
                    Debug.LogError($"[DeskTrayNative] 아이콘 마스크 생성에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                    return IntPtr.Zero;
                }

                NativeIconInfo iconInfo = new NativeIconInfo {
                    fIcon = true,
                    hbmMask = maskBitmap,
                    hbmColor = colorBitmap,
                };

                IntPtr icon = NativeMethods.CreateIconIndirect(ref iconInfo);

                if (icon == IntPtr.Zero) {
                    Debug.LogError($"[DeskTrayNative] 아이콘 생성에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                }

                return icon;
            }
            finally {
                NativeMethods.DeleteObject(colorBitmap);

                if (maskBitmap != IntPtr.Zero) {
                    NativeMethods.DeleteObject(maskBitmap);
                }
            }
        }

        private static IntPtr ExtractExecutableIcon() {
            try {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess()) {
                    string path = process.MainModule?.FileName;

                    if (string.IsNullOrEmpty(path)) {
                        return IntPtr.Zero;
                    }

                    return NativeMethods.ExtractIcon(NativeMethods.GetModuleHandle(null), path, 0);
                }
            }
            catch (Exception e) {
                Debug.LogWarning($"[DeskTrayNative] 실행 파일 아이콘을 뽑지 못했습니다: {e.Message}");
                return IntPtr.Zero;
            }
        }

        #endregion 아이콘

        #region 입력

        /// <summary> 쌓인 입력을 하나 가져가고 비운다 </summary>
        internal static DESK_TRAY_SIGNAL TakeSignal() {
            DESK_TRAY_SIGNAL signal = _pendingSignal;
            _pendingSignal = DESK_TRAY_SIGNAL.NONE;
            return signal;
        }

        /// <summary>
        /// 마우스 위치에 메뉴를 띄우고 고른 항목의 순번을 돌려준다. 아무것도 안 고르면 -1.
        /// 메뉴가 닫힐 때까지 이 호출은 돌아오지 않으므로 그동안 게임은 멈춘 것처럼 보인다.
        /// </summary>
        internal static int ShowMenu(string[] labels, byte[][] icons, int iconSize) {
            if (_windowHandle == IntPtr.Zero || labels == null || labels.Length == 0) {
                return -1;
            }

            IntPtr menu = NativeMethods.CreatePopupMenu();

            if (menu == IntPtr.Zero) {
                Debug.LogError($"[DeskTrayNative] 메뉴 생성에 실패했습니다. (오류 {Marshal.GetLastWin32Error()})");
                return -1;
            }

            List<IntPtr> bitmaps = new List<IntPtr>();

            try {
                for (int i = 0; i < labels.Length; i++) {
                    // 라벨이 비어 있으면 구분선이다. 번호를 매기지 않아도 순번은 그대로 맞는다.
                    if (labels[i] == null) {
                        NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);
                        continue;
                    }

                    NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (IntPtr)(MENU_ID_BASE + i), labels[i]);
                    AttachMenuIcon(menu, MENU_ID_BASE + i, icons, i, iconSize, bitmaps);
                }

                if (!NativeMethods.GetCursorPos(out NativePoint cursor)) {
                    return -1;
                }

                // 우리 창을 잠깐 앞으로 올려야 메뉴 밖을 눌렀을 때 메뉴가 닫힌다. 윈도우의 오래된 규칙이다.
                NativeMethods.SetForegroundWindow(_windowHandle);

                int command = NativeMethods.TrackPopupMenuEx(menu, NativeMethods.TPM_MENU_FLAGS,
                                                             cursor.x, cursor.y, _windowHandle, IntPtr.Zero);

                // 위와 짝이 되는 처리. 빼먹으면 다음 번에 메뉴가 곧바로 닫혀 버린다.
                NativeMethods.PostMessage(_windowHandle, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

                return command >= MENU_ID_BASE ? command - MENU_ID_BASE : -1;
            }
            finally {
                // 메뉴를 먼저 없앤 뒤에 그림을 지웁니다. 쓰는 중에 지우면 마지막 그림이 깨집니다.
                NativeMethods.DestroyMenu(menu);

                for (int i = 0; i < bitmaps.Count; i++) {
                    NativeMethods.DeleteObject(bitmaps[i]);
                }
            }
        }

        /// <summary> 메뉴 한 칸의 라벨 왼쪽에 그림을 붙인다 </summary>
        private static void AttachMenuIcon(IntPtr menu, int commandId, byte[][] icons, int index, int size,
                                           List<IntPtr> created) {
            if (icons == null || index >= icons.Length || icons[index] == null || size <= 0) {
                return;
            }

            IntPtr bitmap = CreateBgraBitmap(icons[index], size, size, out IntPtr _);

            if (bitmap == IntPtr.Zero) {
                return;
            }

            created.Add(bitmap);

            NativeMenuItemInfo info = new NativeMenuItemInfo {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMenuItemInfo)),
                fMask = NativeMethods.MIIM_BITMAP,
                hbmpItem = bitmap,
            };

            if (!NativeMethods.SetMenuItemInfo(menu, (uint)commandId, false, ref info)) {
                Debug.LogWarning($"[DeskTrayNative] 메뉴 그림을 붙이지 못했습니다. (오류 {Marshal.GetLastWin32Error()})");
            }
        }

        /// <summary>
        /// 트레이 메시지만 받아 적고 곧바로 돌아간다.
        /// 여기서 유니티 코드를 부르면 네이티브 경계 안에서 실행되어 위험하므로 처리는 뒤로 미룬다.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(WndProcDelegate))]
        private static IntPtr TrayWindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam) {
            try {
                if (message == NativeMethods.WM_TRAY_CALLBACK) {
                    // 어떤 마우스 동작인지는 lParam 의 하위 16비트에 들어온다.
                    int mouseMessage = (int)(lParam.ToInt64() & 0xFFFF);

                    if (mouseMessage == NativeMethods.WM_LBUTTONUP) {
                        _pendingSignal = DESK_TRAY_SIGNAL.LEFT_CLICK;
                    }
                    else if (mouseMessage == NativeMethods.WM_RBUTTONUP) {
                        _pendingSignal = DESK_TRAY_SIGNAL.RIGHT_CLICK;
                    }

                    return IntPtr.Zero;
                }
            }
            catch (Exception e) {
                // 예외가 네이티브 경계를 넘어가면 프로세스가 그 자리에서 죽으므로 반드시 여기서 끊습니다.
                Debug.LogError($"[DeskTrayNative] 트레이 메시지 처리 중 예외가 발생했습니다: {e}");
            }

            return NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);
        }

        #endregion 입력

        #region 메뉴 테마

        // 다크 메뉴는 uxtheme.dll 안에 이름 없이 번호로만 노출된 함수로 켠다.
        // 문서에 없는 함수라 윈도우 버전을 먼저 확인하고, 안 되면 조용히 물러난다.
        private const int BUILD_DARK_MODE_MIN = 17763;       // Windows 10 1809
        private const int BUILD_PREFERRED_APP_MODE = 18362;  // Windows 10 1903

        private const int ORDINAL_ALLOW_DARK_FOR_WINDOW = 133;

        // 1809 에서는 AllowDarkModeForApp(bool), 1903 부터는 SetPreferredAppMode(int) 이다.
        private const int ORDINAL_APP_MODE = 135;
        private const int ORDINAL_FLUSH_MENU_THEMES = 136;

        private const int APP_MODE_ALLOW_DARK = 1;
        private const int APP_MODE_FORCE_DARK = 2;
        private const int APP_MODE_FORCE_LIGHT = 3;

        private delegate int SetPreferredAppModeDelegate(int mode);

        private delegate bool AllowDarkModeForAppDelegate([MarshalAs(UnmanagedType.Bool)] bool allow);

        private delegate bool AllowDarkModeForWindowDelegate(IntPtr hWnd,
                                                             [MarshalAs(UnmanagedType.Bool)] bool allow);

        private delegate void FlushMenuThemesDelegate();

        private static IntPtr _uxTheme;
        private static bool _hasWarnedTheme;

        /// <summary>
        /// 메뉴를 밝게 그릴지 어둡게 그릴지 정한다.
        /// 글자색과 강조색은 윈도우가 알아서 맞추므로 따로 지정하지 않는다.
        /// </summary>
        internal static bool SetMenuTheme(DESK_MENU_THEME theme) {
            int build = GetWindowsBuild();

            if (build < BUILD_DARK_MODE_MIN) {
                WarnThemeOnce($"이 윈도우 버전(빌드 {build})은 메뉴 테마 전환을 제공하지 않습니다.");
                return false;
            }

            if (!TryLoadUxTheme()) {
                return false;
            }

            bool applied = build >= BUILD_PREFERRED_APP_MODE
                ? ApplyPreferredAppMode(theme)
                : ApplyLegacyAppMode(theme);

            if (!applied) {
                return false;
            }

            ApplyWindowTheme(theme);
            FlushMenuThemes();

            return true;
        }

        private static bool TryLoadUxTheme() {
            if (_uxTheme != IntPtr.Zero) {
                return true;
            }

            _uxTheme = NativeMethods.LoadLibrary("uxtheme.dll");

            if (_uxTheme == IntPtr.Zero) {
                WarnThemeOnce("uxtheme.dll 을 불러오지 못했습니다.");
                return false;
            }

            return true;
        }

        private static bool ApplyPreferredAppMode(DESK_MENU_THEME theme) {
            SetPreferredAppModeDelegate setMode =
                GetOrdinal<SetPreferredAppModeDelegate>(ORDINAL_APP_MODE);

            if (setMode == null) {
                return false;
            }

            setMode(ToPreferredAppMode(theme));
            return true;
        }

        /// <summary> 1809 에는 밝게/어둡게 구분이 없어 허용 여부만 넘긴다 </summary>
        private static bool ApplyLegacyAppMode(DESK_MENU_THEME theme) {
            AllowDarkModeForAppDelegate allowDark =
                GetOrdinal<AllowDarkModeForAppDelegate>(ORDINAL_APP_MODE);

            if (allowDark == null) {
                return false;
            }

            allowDark(theme != DESK_MENU_THEME.LIGHT);
            return true;
        }

        private static void ApplyWindowTheme(DESK_MENU_THEME theme) {
            if (_windowHandle == IntPtr.Zero) {
                return;
            }

            AllowDarkModeForWindowDelegate allowForWindow =
                GetOrdinal<AllowDarkModeForWindowDelegate>(ORDINAL_ALLOW_DARK_FOR_WINDOW);

            allowForWindow?.Invoke(_windowHandle, theme != DESK_MENU_THEME.LIGHT);
        }

        private static void FlushMenuThemes() {
            FlushMenuThemesDelegate flush = GetOrdinal<FlushMenuThemesDelegate>(ORDINAL_FLUSH_MENU_THEMES);

            flush?.Invoke();
        }

        private static int ToPreferredAppMode(DESK_MENU_THEME theme) {
            switch (theme) {
                case DESK_MENU_THEME.DARK:
                    return APP_MODE_FORCE_DARK;

                case DESK_MENU_THEME.LIGHT:
                    return APP_MODE_FORCE_LIGHT;

                default:
                    return APP_MODE_ALLOW_DARK;
            }
        }

        /// <summary> 번호로만 노출된 함수를 꺼내 온다. 없으면 null </summary>
        private static T GetOrdinal<T>(int ordinal) where T : class {
            IntPtr address = NativeMethods.GetProcAddress(_uxTheme, (IntPtr)ordinal);

            if (address == IntPtr.Zero) {
                WarnThemeOnce($"uxtheme.dll 에서 {ordinal}번 함수를 찾지 못했습니다.");
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }

        /// <summary>
        /// 실제 윈도우 빌드 번호. Environment.OSVersion 은 호환성 때문에 낮은 값을 돌려줄 수 있어 쓰지 않는다.
        /// </summary>
        private static int GetWindowsBuild() {
            NativeOsVersionInfo info = new NativeOsVersionInfo {
                dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(NativeOsVersionInfo)),
            };

            if (NativeMethods.RtlGetVersion(ref info) != 0) {
                return 0;
            }

            return (int)info.dwBuildNumber;
        }

        private static void WarnThemeOnce(string reason) {
            if (_hasWarnedTheme) {
                return;
            }

            _hasWarnedTheme = true;
            Debug.LogWarning($"[DeskTrayNative] 메뉴 테마를 바꾸지 못했습니다. {reason}");
        }

        #endregion 메뉴 테마

        #region 네이티브 함수

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeWndClassEx {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeBitmapInfoHeader {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeOsVersionInfo {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szCSDVersion;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMenuItemInfo {
            public uint cbSize;
            public uint fMask;
            public uint fType;
            public uint fState;
            public uint wID;
            public IntPtr hSubMenu;
            public IntPtr hbmpChecked;
            public IntPtr hbmpUnchecked;
            public UIntPtr dwItemData;
            public IntPtr dwTypeData;
            public uint cch;
            public IntPtr hbmpItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeIconInfo {
            [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
            public uint xHotspot;
            public uint yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeNotifyIconData {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
        }

        private static class NativeMethods {

            // 응용 프로그램이 마음대로 쓸 수 있는 메시지 구간(WM_APP) 에서 하나를 골라 씁니다.
            public const uint WM_TRAY_CALLBACK = 0x8000 + 1;
            public const uint WM_NULL = 0x0000;
            public const int WM_LBUTTONUP = 0x0202;
            public const int WM_RBUTTONUP = 0x0205;

            public const uint NIM_ADD = 0x00000000;
            public const uint NIM_MODIFY = 0x00000001;
            public const uint NIM_DELETE = 0x00000002;

            public const uint NIF_MESSAGE = 0x00000001;
            public const uint NIF_ICON = 0x00000002;
            public const uint NIF_TIP = 0x00000004;

            public const uint MF_STRING = 0x00000000;
            public const uint MF_SEPARATOR = 0x00000800;

            /// <summary> 메뉴 항목의 그림만 바꾼다는 표시 </summary>
            public const uint MIIM_BITMAP = 0x00000080;

            private const uint TPM_RIGHTBUTTON = 0x0002;
            private const uint TPM_RETURNCMD = 0x0100;
            private const uint TPM_NONOTIFY = 0x0080;

            // 고른 항목 번호를 반환값으로 받습니다. 그래야 네이티브가 우리를 거꾸로 호출할 일이 없습니다.
            public const uint TPM_MENU_FLAGS = TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON;

            public const uint IMAGE_ICON = 1;
            public const uint LR_LOADFROMFILE = 0x00000010;
            public const uint LR_DEFAULTSIZE = 0x00000040;

            public static readonly IntPtr IDI_APPLICATION = (IntPtr)32512;

            public const uint BI_RGB = 0;
            public const uint DIB_RGB_COLORS = 0;

            /// <summary> 트레이가 쓰는 작은 아이콘의 너비 </summary>
            public const int SM_CXSMICON = 49;

            [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
            public static extern bool Shell_NotifyIcon(uint dwMessage, ref NativeNotifyIconData lpData);

            [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconW")]
            public static extern IntPtr ExtractIcon(IntPtr hInst, string exeFileName, int iconIndex);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW", SetLastError = true)]
            public static extern ushort RegisterClassEx(ref NativeWndClassEx lpWndClass);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "UnregisterClassW")]
            public static extern bool UnregisterClass(string className, IntPtr hInstance);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
            public static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
                                                       int x, int y, int width, int height, IntPtr parent,
                                                       IntPtr menu, IntPtr instance, IntPtr param);

            [DllImport("user32.dll")]
            public static extern bool DestroyWindow(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
            public static extern IntPtr DefWindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "PostMessageW")]
            public static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern IntPtr CreatePopupMenu();

            [DllImport("user32.dll")]
            public static extern bool DestroyMenu(IntPtr hMenu);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
            public static extern bool AppendMenu(IntPtr hMenu, uint flags, IntPtr itemId, string item);

            [DllImport("user32.dll")]
            public static extern int TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hWnd, IntPtr lptpm);

            [DllImport("user32.dll")]
            public static extern bool GetCursorPos(out NativePoint lpPoint);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadIconW")]
            public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr iconName);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW")]
            public static extern IntPtr LoadImage(IntPtr hInstance, string name, uint type,
                                                  int cx, int cy, uint load);

            [DllImport("user32.dll")]
            public static extern bool DestroyIcon(IntPtr hIcon);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetMenuItemInfoW", SetLastError = true)]
            public static extern bool SetMenuItemInfo(IntPtr hMenu, uint item,
                                                      [MarshalAs(UnmanagedType.Bool)] bool byPosition,
                                                      ref NativeMenuItemInfo info);

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int index);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr CreateIconIndirect(ref NativeIconInfo iconInfo);

            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern IntPtr CreateDIBSection(IntPtr hdc, ref NativeBitmapInfoHeader header, uint usage,
                                                         out IntPtr bits, IntPtr section, uint offset);

            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitCount, IntPtr bits);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr handle);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
            public static extern IntPtr GetModuleHandle(string moduleName);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryW")]
            public static extern IntPtr LoadLibrary(string fileName);

            // 이름이 없는 함수는 번호로 찾아야 하므로 문자열이 아닌 정수를 넘깁니다.
            [DllImport("kernel32.dll")]
            public static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);

            [DllImport("ntdll.dll", EntryPoint = "RtlGetVersion")]
            public static extern int RtlGetVersion(ref NativeOsVersionInfo info);
        }

        #endregion 네이티브 함수
    }
}

#endif
