#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// Windows(Win32) 창 제어 구현.
    /// <see cref="WindowDeskAPI"/> 만 이 클래스를 호출하며, 게임 코드는 직접 접근하지 않습니다.
    /// </summary>
    internal static class DeskPlatform {

        #region 플랫폼 지원 여부

        /// <summary>모니터·해상도 조회가 가능한지 여부.</summary>
        internal static bool IsSupported => true;

        /// <summary>이 플랫폼이 실제로 제공하는 기능. Windows 는 전 기능을 제공합니다.</summary>
        internal static DESK_WINDOW_FEATURE SupportedFeatures => DESK_WINDOW_FEATURE.ALL;

        /// <summary>
        /// 창 제어가 가능한지 여부.
        /// 에디터에서 허용하면 Unity 에디터 창 자체가 변형되므로 빌드에서만 true 입니다.
        /// </summary>
#if UNITY_EDITOR
        internal static bool IsWindowControlEnabled => false;
#else
        internal static bool IsWindowControlEnabled => true;
#endif

        #endregion 플랫폼 지원 여부

        #region 창 핸들

        private static IntPtr _foundThreadWindow = IntPtr.Zero;

        // 델리게이트를 지역 변수로 넘기면 GC 수거 시 크래시하므로 static readonly 로 고정합니다.
        private static readonly EnumThreadWindowsProc THREAD_WINDOW_PROC = ThreadWindowCallback;

        /// <summary>
        /// 이 프로세스의 메인 창 핸들을 찾습니다.
        /// GetActiveWindow 와 달리 게임이 비활성 상태여도 항상 자기 창을 가리킵니다.
        /// </summary>
        internal static IntPtr FindOwnWindowHandle() {
            _foundThreadWindow = IntPtr.Zero;
            NativeMethods.EnumThreadWindows(NativeMethods.GetCurrentThreadId(), THREAD_WINDOW_PROC, IntPtr.Zero);

            if (_foundThreadWindow != IntPtr.Zero) {
                return _foundThreadWindow;
            }

            // 스레드 창 탐색이 실패하면 프로세스 정보로 대체합니다.
            using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess()) {
                return process.MainWindowHandle;
            }
        }

        // 예외가 네이티브 경계를 넘어가면 IL2CPP 빌드에서 프로세스가 즉시 종료되므로 본문 전체를 감쌉니다.
        [AOT.MonoPInvokeCallback(typeof(EnumThreadWindowsProc))]
        private static bool ThreadWindowCallback(IntPtr hWnd, IntPtr lParam) {
            try {
                bool isOwnedWindow = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero;

                if (!isOwnedWindow && NativeMethods.IsWindowVisible(hWnd)) {
                    _foundThreadWindow = hWnd;
                    return false; // 열거 중단
                }

                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[DeskPlatform.Windows] 창 열거 중 예외가 발생해 중단합니다: {e}");
                return false; // 열거를 멈추고 프로세스 정보 폴백에 맡깁니다.
            }
        }

        #endregion 창 핸들

        #region 창 외형

        internal static void SetBorderless(IntPtr hWnd, bool borderless) {
            int style = GetStyle(hWnd, NativeMethods.GWL_STYLE);
            int frameBits = NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME;

            style = borderless ? style & ~frameBits : style | frameBits;

            SetStyle(hWnd, NativeMethods.GWL_STYLE, style);
            ApplyFrameChange(hWnd);
        }

        /// <summary> 제목 표시줄과 테두리가 제거된 상태인지 조회한다. </summary>
        internal static bool IsBorderless(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            int frameBits = NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME;
            return (GetStyle(hWnd, NativeMethods.GWL_STYLE) & frameBits) == 0;
        }

        /// <summary> 드래그로 크기를 바꿀 수 있는지 전환한다. 최대화 버튼도 함께 따라간다. </summary>
        internal static void SetResizable(IntPtr hWnd, bool resizable) {
            int style = GetStyle(hWnd, NativeMethods.GWL_STYLE);
            int resizeBits = NativeMethods.WS_THICKFRAME | NativeMethods.WS_MAXIMIZEBOX;

            style = resizable ? style | resizeBits : style & ~resizeBits;

            SetStyle(hWnd, NativeMethods.GWL_STYLE, style);
            ApplyFrameChange(hWnd);
        }

        /// <summary>
        /// 창 배경을 뚫어 뒤가 비쳐 보이게 한다.
        /// 빈 블러 영역으로 픽셀별 알파를 켜고 클라이언트 영역을 유리로 넓히는 두 경로를 함께 건다.
        /// </summary>
        internal static bool SetTransparent(IntPtr hWnd, bool transparent) {
            if (hWnd == IntPtr.Zero) {
                LastTransparentReport = "창 핸들 없음";
                return false;
            }

            try {
                int blurResult = ApplyBlurBehind(hWnd, transparent);
                int glassResult = ApplyGlassFrame(hWnd, transparent);
                int exStyle = GetStyle(hWnd, NativeMethods.GWL_EXSTYLE);
                bool layered = (exStyle & NativeMethods.WS_EX_LAYERED) != 0;

                LastTransparentReport = $"BlurBehind 0x{blurResult:X8} / ExtendFrame 0x{glassResult:X8}"
                                        + $" / ExStyle 0x{exStyle:X8} / LAYERED {layered}";

                return blurResult == 0 && glassResult == 0;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
                LastTransparentReport = $"DWM 사용 불가 : {e.Message}";
                Debug.LogWarning($"[DeskPlatform.Windows] DWM 을 사용할 수 없어 투명 처리를 건너뜁니다: {e.Message}");
                return false;
            }
        }

        /// <summary> 마지막 투명 처리 시도의 결과. 빌드에서 원인을 보려고 남긴다 </summary>
        internal static string LastTransparentReport { get; private set; } = "아직 시도한 적 없음";

        /// <summary>
        /// 비어 있는 블러 영역을 걸어 창 전체에 픽셀별 알파를 켠다.
        /// 영역이 비어 있으므로 흐림 효과는 생기지 않고 알파 합성만 남는다.
        /// </summary>
        private static int ApplyBlurBehind(IntPtr hWnd, bool enable) {
            IntPtr region = enable ? NativeMethods.CreateRectRgn(0, 0, -1, -1) : IntPtr.Zero;

            NativeBlurBehind blurBehind = new NativeBlurBehind {
                dwFlags = NativeMethods.DWM_BB_ENABLE | NativeMethods.DWM_BB_BLURREGION,
                fEnable = enable,
                hRgnBlur = region,
                fTransitionOnMaximized = false
            };

            int result = NativeMethods.DwmEnableBlurBehindWindow(hWnd, ref blurBehind);

            if (region != IntPtr.Zero) {
                NativeMethods.DeleteObject(region);
            }

            return result;
        }

        /// <summary> 클라이언트 영역 전체를 DWM 유리 영역으로 넓혀 카메라 배경 알파를 화면에 반영한다. </summary>
        private static int ApplyGlassFrame(IntPtr hWnd, bool enable) {
            int margin = enable ? NativeMethods.MARGIN_FULL_GLASS : 0;

            NativeMargins margins = new NativeMargins {
                cxLeftWidth = margin,
                cxRightWidth = margin,
                cyTopHeight = margin,
                cyBottomHeight = margin
            };

            return NativeMethods.DwmExtendFrameIntoClientArea(hWnd, ref margins);
        }

        /// <summary>
        /// 클릭 통과가 다른 프로세스의 창까지 닿도록 레이어 창으로 만든다.
        /// SetLayeredWindowAttributes 는 부르지 않는다. 부르면 창이 상수 알파로 합성되어 픽셀별 알파가 무시된다.
        /// </summary>
        internal static bool EnableLayered(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            int exStyle = GetStyle(hWnd, NativeMethods.GWL_EXSTYLE);

            if ((exStyle & NativeMethods.WS_EX_LAYERED) != 0) {
                return true;
            }

            SetStyle(hWnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_LAYERED);
            return true;
        }

        /// <summary>
        /// 마우스 입력을 창 뒤로 흘려보낸다. 켜면 바탕화면 아이콘이 창 너머로 눌린다.
        /// 알파 판정기가 매 프레임 토글하므로 레이어 비트는 건드리지 않고 프레임 재계산도 요청하지 않는다.
        /// </summary>
        internal static void SetClickThrough(IntPtr hWnd, bool clickThrough) {
            int exStyle = GetStyle(hWnd, NativeMethods.GWL_EXSTYLE);

            exStyle = clickThrough
                ? exStyle | NativeMethods.WS_EX_TRANSPARENT
                : exStyle & ~NativeMethods.WS_EX_TRANSPARENT;

            SetStyle(hWnd, NativeMethods.GWL_EXSTYLE, exStyle);
        }

        /// <summary> 마우스 입력을 창 뒤로 흘려보내는 상태인지 조회한다. </summary>
        internal static bool IsClickThrough(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            return (GetStyle(hWnd, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TRANSPARENT) != 0;
        }

        /// <summary> 드래그로 크기를 바꿀 수 있는 상태인지 조회한다. </summary>
        internal static bool IsResizable(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            return (GetStyle(hWnd, NativeMethods.GWL_STYLE) & NativeMethods.WS_THICKFRAME) != 0;
        }

        internal static void SetTopMost(IntPtr hWnd, bool topMost) {
            IntPtr insertAfter = topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST;
            uint flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE;

            NativeMethods.SetWindowPos(hWnd, insertAfter, 0, 0, 0, 0, flags);
        }

        /// <summary>
        /// 스타일 적용을 위해 창을 잠깐 숨겼다가 다시 표시합니다.
        /// 이 과정에서 최상위 상태가 풀릴 수 있으므로 TopMost 보다 먼저 호출해야 합니다.
        /// </summary>
        /// <summary> 항상 다른 창 위에 표시되는 상태인지 조회한다. </summary>
        internal static bool IsTopMost(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            return (GetStyle(hWnd, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOPMOST) != 0;
        }

        /// <summary> 작업표시줄에 버튼이 보이는 상태인지 조회한다. </summary>
        internal static bool IsTaskbarButtonVisible(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            return (GetStyle(hWnd, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOOLWINDOW) == 0;
        }

        /// <summary> 창을 숨긴다. 켤 때 창 모양을 갖추기 전과 꺼질 때 원래 창 모양으로 되돌리는 동안 화면에 보이지 않게 한다. </summary>
        internal static void HideWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return;
            }

            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
        }

        /// <summary> 숨긴 창을 다시 보이게 한다. </summary>
        internal static void ShowWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return;
            }

            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
        }

        /// <summary> 창이 보이는 상태인지 조회한다. </summary>
        internal static bool IsWindowVisible(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            return NativeMethods.IsWindowVisible(hWnd);
        }

        /// <summary>
        /// DWM 으로 창을 가리거나 다시 드러낸다.
        /// 창은 보이는 상태 그대로라 유니티는 그리기 · 포커스 · 표시 방식 적용을 평소대로 하고, 화면에만 나오지 않는다.
        /// </summary>
        /// <returns>DWM 이 받아들였으면 true.</returns>
        internal static bool SetCloaked(IntPtr hWnd, bool cloaked) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            try {
                int value = cloaked ? 1 : 0;
                return NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAK, ref value, sizeof(int)) == 0;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
                Debug.LogWarning($"[DeskPlatform.Windows] DWM 을 사용할 수 없어 창을 가리지 못했습니다: {e.Message}");
                return false;
            }
        }

        internal static void SetTaskbarButtonVisible(IntPtr hWnd, bool visible) {
            int exStyle = GetStyle(hWnd, NativeMethods.GWL_EXSTYLE);

            exStyle = visible
                ? exStyle & ~NativeMethods.WS_EX_TOOLWINDOW
                : exStyle | NativeMethods.WS_EX_TOOLWINDOW;

            // 켤 때 숨겨 둔 창은 스타일만 바꾸고 숨긴 채로 둔다. 보이게 하는 것은 시작 숨김이 맡는다.
            bool wasVisible = NativeMethods.IsWindowVisible(hWnd);

            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
            SetStyle(hWnd, NativeMethods.GWL_EXSTYLE, exStyle);

            if (wasVisible) {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }
        }

        /// <summary> 창의 원본 스타일과 영역을 읽어 스냅샷으로 만든다. </summary>
        internal static bool TryCaptureWindowStyle(IntPtr hWnd, out DeskWindowStyleSnapshot snapshot) {
            snapshot = default;

            if (hWnd == IntPtr.Zero) {
                return false;
            }

            int style = GetStyle(hWnd, NativeMethods.GWL_STYLE);
            int exStyle = GetStyle(hWnd, NativeMethods.GWL_EXSTYLE);
            RectInt rect = TryGetWindowRect(hWnd, out RectInt captured) ? captured : default;
            bool wasTopMost = (exStyle & NativeMethods.WS_EX_TOPMOST) != 0;

            snapshot = new DeskWindowStyleSnapshot(style, exStyle, rect, wasTopMost);
            return true;
        }

        /// <summary> 스냅샷 시점의 창 상태로 되돌린다. </summary>
        internal static bool RestoreWindowStyle(IntPtr hWnd, DeskWindowStyleSnapshot snapshot) {
            if (hWnd == IntPtr.Zero || !snapshot.IsValid) {
                return false;
            }

            // 스냅샷에는 보이던 때의 WS_VISIBLE 이 들어 있다. 숨긴 창에 그대로 쓰면 다시 그려지므로 지금 보이는지를 따른다.
            int style = NativeMethods.IsWindowVisible(hWnd)
                ? snapshot.Style | NativeMethods.WS_VISIBLE
                : snapshot.Style & ~NativeMethods.WS_VISIBLE;

            SetStyle(hWnd, NativeMethods.GWL_STYLE, style);
            SetStyle(hWnd, NativeMethods.GWL_EXSTYLE, snapshot.ExtendedStyle);
            ApplyFrameChange(hWnd);

            SetTopMost(hWnd, snapshot.WasTopMost);

            if (snapshot.Rect.width > 0 && snapshot.Rect.height > 0) {
                SetWindowRect(hWnd, snapshot.Rect.x, snapshot.Rect.y, snapshot.Rect.width, snapshot.Rect.height);
            }

            return true;
        }

        /// <summary> 같은 제목을 가진 다른 인스턴스의 창을 찾아 앞으로 끌어올린다. </summary>
        internal static bool TryActivateWindowByTitle(string windowTitle) {
            if (string.IsNullOrEmpty(windowTitle)) {
                return false;
            }

            IntPtr target = NativeMethods.FindWindow(null, windowTitle);

            if (target == IntPtr.Zero) {
                return false;
            }

            return ActivateWindow(target);
        }

        /// <summary>
        /// 창을 앞으로 끌어올린다. 최소화되어 있으면 먼저 복원한다.
        /// 윈도우가 포그라운드 가로채기를 막는 경우가 있어 실패할 수 있다.
        /// </summary>
        internal static bool ActivateWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            if (NativeMethods.IsIconic(hWnd)) {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }

            return NativeMethods.SetForegroundWindow(hWnd);
        }

        #endregion 창 외형

        #region 입력

        internal static bool TryGetCursorPosition(out Vector2Int position) {
            if (!NativeMethods.GetCursorPos(out NativePoint point)) {
                position = Vector2Int.zero;
                return false;
            }

            position = new Vector2Int(point.x, point.y);
            return true;
        }

        #endregion 입력

        #region 창 위치와 크기

        internal static bool TryGetWindowRect(IntPtr hWnd, out RectInt rect) {
            if (!NativeMethods.GetWindowRect(hWnd, out NativeRect nativeRect)) {
                rect = default;
                return false;
            }

            rect = ToRectInt(nativeRect);
            return true;
        }

        /// <summary>
        /// 창 크기를 1px 줄였다가 바로 되돌린다. 위치 · 그리기 순서 · 포커스는 그대로 둔다.
        /// 작업표시줄은 맨 앞 창이 모니터 전체를 덮으면 자기를 뒤로 내리는데, 가려 둔 창은 이 판단에서 빠지고 가림을 풀어도 다시 판단하지 않는다.
        /// 가림을 푼 뒤 크기 바뀜 신호를 줘 작업표시줄이 다시 판단하게 할 때 쓴다.
        /// </summary>
        internal static void NudgeWindowSize(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero || !TryGetWindowRect(hWnd, out RectInt rect)) {
                return;
            }

            SetSize(hWnd, rect.width - 1, rect.height);
            SetSize(hWnd, rect.width, rect.height);
        }

        internal static void SetPosition(IntPtr hWnd, int x, int y) {
            uint flags = NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;
            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, flags);
        }

        internal static void SetSize(IntPtr hWnd, int width, int height) {
            uint flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;
            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, width, height, flags);
        }

        internal static void SetWindowRect(IntPtr hWnd, int x, int y, int width, int height) {
            uint flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;
            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, flags);
        }

        /// <summary>
        /// 제목 표시줄과 테두리가 잡아먹는 크기. 창 바깥 크기에서 그림 영역을 뺀 값이다.
        /// 창 크기를 계산할 때 Unity 가 아직 창을 바꾸기 전이라도 쓸 수 있도록 스타일에서 직접 구한다.
        /// </summary>
        internal static bool TryGetWindowChrome(IntPtr hWnd, bool hasCaption, bool isResizable,
                                                out int width, out int height) {
            width = 0;
            height = 0;

            if (hWnd == IntPtr.Zero) {
                return false;
            }

            int style = GetStyle(hWnd, NativeMethods.GWL_STYLE);
            int exStyle = GetStyle(hWnd, NativeMethods.GWL_EXSTYLE);

            style = hasCaption ? style | NativeMethods.WS_CAPTION : style & ~NativeMethods.WS_CAPTION;

            int resizeBits = NativeMethods.WS_THICKFRAME | NativeMethods.WS_MAXIMIZEBOX;
            style = isResizable ? style | resizeBits : style & ~resizeBits;
            NativeRect rect = new NativeRect { left = 0, top = 0, right = 100, bottom = 100 };

            if (!TryAdjustWindowRect(hWnd, ref rect, style, exStyle)) {
                return false;
            }

            width = rect.right - rect.left - 100;
            height = rect.bottom - rect.top - 100;
            return true;
        }

        private static bool _isAdjustDpiWarned;

        /// <summary>
        /// 테두리 두께를 창이 놓인 모니터의 배율로 구한다.
        /// 배율을 모르는 옛 API 는 96 DPI 기준으로만 답해, 150% 모니터에서 제목 표시줄을 15px 가량 얕게 본다.
        /// 배율 대응 API 는 Windows 10 1607 부터라 없으면 옛 API 로 물러난다.
        /// </summary>
        private static bool TryAdjustWindowRect(IntPtr hWnd, ref NativeRect rect, int style, int exStyle) {
            uint dpi = GetMonitorDpi(GetMonitorFromWindow(hWnd));

            try {
                return NativeMethods.AdjustWindowRectExForDpi(ref rect, (uint)style, false, (uint)exStyle, dpi);
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
                if (!_isAdjustDpiWarned) {
                    _isAdjustDpiWarned = true;
                    Debug.LogWarning("[DeskPlatform.Windows] 배율 대응 창 크기 계산 API 를 쓸 수 없어 96 DPI 기준으로 "
                                     + $"물러납니다. 고배율 모니터에서 창이 조금 넘칠 수 있습니다: {e.Message}");
                }

                return NativeMethods.AdjustWindowRectEx(ref rect, (uint)style, false, (uint)exStyle);
            }
        }

        #endregion 창 위치와 크기

        #region 모니터 정보

        private static readonly List<DeskMonitorInfo> MONITOR_BUFFER = new List<DeskMonitorInfo>();
        private static readonly MonitorEnumProc MONITOR_ENUM_PROC = MonitorCallback;

        private static Exception _monitorEnumError;
        private static bool _isDpiApiWarned;

        /// <summary>
        /// 연결된 모니터를 열거해 <paramref name="buffer"/> 에 채웁니다.
        /// 실패하면 buffer 를 건드리지 않고 false 를 반환해, 호출한 쪽이 "0대"와 "실패"를 구분할 수 있게 합니다.
        /// </summary>
        internal static bool TryEnumerateMonitors(List<DeskMonitorInfo> buffer, out Exception error) {
            MONITOR_BUFFER.Clear();
            _monitorEnumError = null;

            bool enumerated = false;

            try {
                enumerated = NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MONITOR_ENUM_PROC, IntPtr.Zero);
            }
            catch (Exception e) {
                _monitorEnumError = e;
            }

            // 콜백이 중단을 요청해도 false 가 나옵니다. 그때는 이미 콜백이 사유를 채워 두었으므로 덮어쓰지 않습니다.
            if (_monitorEnumError == null && !enumerated) {
                _monitorEnumError = new Win32Exception(Marshal.GetLastWin32Error(), "EnumDisplayMonitors failed.");
            }

            error = _monitorEnumError;

            if (error != null) {
                return false;
            }

            buffer.AddRange(MONITOR_BUFFER);
            return true;
        }

        internal static IntPtr GetMonitorFromWindow(IntPtr hWnd) {
            return NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        }

        // 예외가 네이티브 경계를 넘어가면 IL2CPP 빌드에서 프로세스가 즉시 종료되므로 본문 전체를 감쌉니다.
        // 여기서 터지는 예외는 특정 모니터의 문제가 아니라 구조체 레이아웃 같은 구조적 결함이므로
        // 건너뛰고 계속하지 않습니다. 예외를 기록한 뒤 즉시 중단해 호출한 쪽이 실패를 보고하게 합니다.
        [AOT.MonoPInvokeCallback(typeof(MonitorEnumProc))]
        private static bool MonitorCallback(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData) {
            try {
                NativeMonitorInfoEx info = new NativeMonitorInfoEx {
                    cbSize = Marshal.SizeOf(typeof(NativeMonitorInfoEx))
                };

                // 건너뛰고 계속하면 목록이 말없이 짧아져 호출한 쪽이 정상으로 오인합니다.
                if (!NativeMethods.GetMonitorInfo(hMonitor, ref info)) {
                    _monitorEnumError = new Win32Exception(Marshal.GetLastWin32Error(),
                        $"GetMonitorInfo failed for monitor 0x{hMonitor.ToInt64():X}.");
                    return false; // 열거 중단
                }

                bool isPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;

                MONITOR_BUFFER.Add(new DeskMonitorInfo(
                    hMonitor,
                    info.szDevice,
                    ToRectInt(info.rcMonitor),
                    ToRectInt(info.rcWork),
                    GetMonitorDpi(hMonitor),
                    isPrimary));

                return true; // 계속 열거
            }
            catch (Exception e) {
                _monitorEnumError = e;
                return false; // 열거 중단. 판정과 로깅은 호출한 쪽이 담당합니다.
            }
        }

        private static uint GetMonitorDpi(IntPtr hMonitor) {
            try {
                if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0) {
                    return dpiX;
                }
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
                if (!_isDpiApiWarned) {
                    _isDpiApiWarned = true;
                    Debug.LogWarning($"[DeskPlatform.Windows] DPI 조회 API 를 사용할 수 없어 기본값 96 을 사용합니다: {e.Message}");
                }
            }

            return DeskConstants.BASE_DPI;
        }

        #endregion 모니터 정보

        #region 작업표시줄

        private const string PRIMARY_TASKBAR_CLASS = "Shell_TrayWnd";
        private const string SECONDARY_TASKBAR_CLASS = "Shell_SecondaryTrayWnd";

        // 보조 모니터 작업표시줄을 찾는 반복이 OS 이상으로 끝나지 않는 일을 막는 상한. 모니터 수보다 넉넉하다
        private const int MAX_SECONDARY_TASKBARS = 32;

        /// <summary>
        /// 주 작업표시줄(Shell_TrayWnd)과 보조 모니터 작업표시줄(Shell_SecondaryTrayWnd)을 읽어 <paramref name="buffer"/> 에 더합니다.
        /// 탐색기가 꺼져 있어 작업표시줄이 없으면 빈 채로 true 입니다. 예외가 나면 buffer 를 비우고 false 입니다.
        /// 자동 숨김은 모든 작업표시줄에 함께 걸리는 설정이라 한 번만 읽습니다.
        /// </summary>
        internal static bool TryReadTaskbars(List<DeskTaskbarSample> buffer, out Exception error) {
            error = null;

            try {
                bool isAutoHide = IsTaskbarAutoHide();
                IntPtr primary = NativeMethods.FindWindow(PRIMARY_TASKBAR_CLASS, null);

                if (primary != IntPtr.Zero) {
                    AddTaskbarSample(buffer, primary, GetPrimaryTaskbarEdge(), isAutoHide, true);
                }

                IntPtr secondary = IntPtr.Zero;

                for (int i = 0; i < MAX_SECONDARY_TASKBARS; i++) {
                    secondary = NativeMethods.FindWindowEx(IntPtr.Zero, secondary, SECONDARY_TASKBAR_CLASS, null);

                    if (secondary == IntPtr.Zero) {
                        break;
                    }

                    AddTaskbarSample(buffer, secondary, DESK_TASKBAR_EDGE.NONE, isAutoHide, false);
                }

                return true;
            }
            catch (Exception e) {
                buffer.Clear();
                error = e;
                return false;
            }
        }

        /// <summary> 보이는 작업표시줄 창이면 창 사각형과 모니터를 담아 더합니다. 숨은 창이나 사각형을 못 읽은 창은 건너뜁니다. </summary>
        private static void AddTaskbarSample(List<DeskTaskbarSample> buffer, IntPtr hWnd, DESK_TASKBAR_EDGE reportedEdge, bool isAutoHide, bool isPrimary) {
            if (!NativeMethods.IsWindowVisible(hWnd) || !NativeMethods.GetWindowRect(hWnd, out NativeRect rect)) {
                return;
            }

            IntPtr monitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            buffer.Add(new DeskTaskbarSample(monitor, ToRectInt(rect), reportedEdge, isAutoHide, isPrimary));
        }

        /// <summary> 작업표시줄 자동 숨김이 켜져 있는지 (ABM_GETSTATE). </summary>
        private static bool IsTaskbarAutoHide() {
            NativeAppBarData data = CreateAppBarData();
            ulong state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data).ToUInt64();
            return (state & NativeMethods.ABS_AUTOHIDE) != 0;
        }

        /// <summary> OS 가 알려 주는 주 작업표시줄 변 (ABM_GETTASKBARPOS). 실패하면 NONE. </summary>
        private static DESK_TASKBAR_EDGE GetPrimaryTaskbarEdge() {
            NativeAppBarData data = CreateAppBarData();

            if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data) == UIntPtr.Zero) {
                return DESK_TASKBAR_EDGE.NONE;
            }

            switch (data.uEdge) {
                case NativeMethods.ABE_LEFT:
                    return DESK_TASKBAR_EDGE.LEFT;
                case NativeMethods.ABE_TOP:
                    return DESK_TASKBAR_EDGE.TOP;
                case NativeMethods.ABE_RIGHT:
                    return DESK_TASKBAR_EDGE.RIGHT;
                case NativeMethods.ABE_BOTTOM:
                    return DESK_TASKBAR_EDGE.BOTTOM;
                default:
                    return DESK_TASKBAR_EDGE.NONE;
            }
        }

        private static NativeAppBarData CreateAppBarData() {
            return new NativeAppBarData {
                cbSize = Marshal.SizeOf(typeof(NativeAppBarData))
            };
        }

        #endregion 작업표시줄

        #region 지원 해상도

        /// <summary>
        /// 디스플레이가 보고하는 모드를 그대로 열거합니다. 중복 제거와 정렬은 호출한 쪽이 담당합니다.
        /// </summary>
        /// <param name="deviceName">디스플레이 장치명. null 이면 주 모니터.</param>
        internal static List<DeskResolution> GetSupportedResolutions(string deviceName) {
            List<DeskResolution> resolutions = new List<DeskResolution>();
            NativeDevMode devMode = CreateDevMode();

            for (int modeIndex = 0; NativeMethods.EnumDisplaySettings(deviceName, modeIndex, ref devMode); modeIndex++) {
                if (devMode.dmBitsPerPel < NativeMethods.MIN_BITS_PER_PIXEL) {
                    continue;
                }

                resolutions.Add(new DeskResolution(devMode.dmPelsWidth, devMode.dmPelsHeight, devMode.dmDisplayFrequency));
            }

            return resolutions;
        }

        internal static bool TryGetCurrentResolution(string deviceName, out DeskResolution resolution) {
            NativeDevMode devMode = CreateDevMode();

            if (!NativeMethods.EnumDisplaySettings(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode)) {
                resolution = default;
                return false;
            }

            resolution = new DeskResolution(devMode.dmPelsWidth, devMode.dmPelsHeight, devMode.dmDisplayFrequency);
            return true;
        }

        private static NativeDevMode CreateDevMode() {
            return new NativeDevMode {
                dmSize = (short)Marshal.SizeOf(typeof(NativeDevMode))
            };
        }

        #endregion 지원 해상도

        #region 내부 헬퍼

        private static int GetStyle(IntPtr hWnd, int index) {
            return NativeMethods.GetWindowLongSafe(hWnd, index).ToInt32();
        }

        private static void SetStyle(IntPtr hWnd, int index, int value) {
            NativeMethods.SetWindowLongSafe(hWnd, index, new IntPtr(value));
        }

        /// <summary>
        /// 스타일 변경을 창 프레임에 반영합니다. 이 호출이 없으면 변경이 즉시 보이지 않습니다.
        /// </summary>
        private static void ApplyFrameChange(IntPtr hWnd) {
            uint flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE
                         | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED;

            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, flags);
        }

        private static RectInt ToRectInt(NativeRect rect) {
            return new RectInt(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
        }

        #endregion 내부 헬퍼

        #region 네이티브 구조체와 델리게이트

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool EnumThreadWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMargins {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeBlurBehind {
            public uint dwFlags;

            [MarshalAs(UnmanagedType.Bool)] public bool fEnable;

            public IntPtr hRgnBlur;

            [MarshalAs(UnmanagedType.Bool)] public bool fTransitionOnMaximized;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeAppBarData {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public NativeRect rc;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeMonitorInfoEx {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeDevMode {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        #endregion 네이티브 구조체와 델리게이트

        #region 네이티브 함수

        /// <summary>
        /// P/Invoke 선언 격리 영역. 이 클래스 밖으로 노출하지 않습니다.
        /// </summary>
        private static class NativeMethods {

            public const int GWL_STYLE = -16;
            public const int GWL_EXSTYLE = -20;

            public const int WS_VISIBLE = 0x10000000;
            public const int WS_CAPTION = 0x00C00000;
            public const int WS_THICKFRAME = 0x00040000;
            public const int WS_MAXIMIZEBOX = 0x00010000;

            public const int WS_EX_TOPMOST = 0x00000008;
            public const int WS_EX_TRANSPARENT = 0x00000020;
            public const int WS_EX_TOOLWINDOW = 0x00000080;
            public const int WS_EX_LAYERED = 0x00080000;

            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOZORDER = 0x0004;
            public const uint SWP_NOACTIVATE = 0x0010;
            public const uint SWP_FRAMECHANGED = 0x0020;

            public const int SW_HIDE = 0;
            public const int SW_SHOW = 5;
            public const int SW_RESTORE = 9;

            public const int DWMWA_CLOAK = 13;

            public const uint GW_OWNER = 4;

            public const uint MONITOR_DEFAULTTONEAREST = 2;
            public const uint MONITORINFOF_PRIMARY = 1;

            public const int MDT_EFFECTIVE_DPI = 0;

            public const uint ABM_GETSTATE = 0x00000004;
            public const uint ABM_GETTASKBARPOS = 0x00000005;
            public const uint ABS_AUTOHIDE = 0x0000001;
            public const uint ABE_LEFT = 0;
            public const uint ABE_TOP = 1;
            public const uint ABE_RIGHT = 2;
            public const uint ABE_BOTTOM = 3;

            public const int ENUM_CURRENT_SETTINGS = -1;
            public const int MIN_BITS_PER_PIXEL = 32;

            /// <summary>클라이언트 영역 전체를 유리 영역으로 확장하라는 DWM 규약값.</summary>
            public const int MARGIN_FULL_GLASS = -1;

            public const uint DWM_BB_ENABLE = 0x00000001;
            public const uint DWM_BB_BLURREGION = 0x00000002;

            public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
            public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

            /// <summary>
            /// 32비트 빌드에는 SetWindowLongPtr 이 없으므로 포인터 크기에 따라 분기합니다.
            /// </summary>
            public static IntPtr SetWindowLongSafe(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
                return IntPtr.Size == 8
                    ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                    : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
            }

            /// <summary>
            /// 32비트 빌드에는 GetWindowLongPtr 이 없으므로 포인터 크기에 따라 분기합니다.
            /// </summary>
            public static IntPtr GetWindowLongSafe(IntPtr hWnd, int nIndex) {
                return IntPtr.Size == 8
                    ? GetWindowLongPtr64(hWnd, nIndex)
                    : new IntPtr(GetWindowLong32(hWnd, nIndex));
            }

            [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
            private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
            private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
            private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
            private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

            [DllImport("user32.dll")]
            public static extern bool AdjustWindowRectEx(ref NativeRect lpRect, uint dwStyle,
                                                         [MarshalAs(UnmanagedType.Bool)] bool bMenu, uint dwExStyle);

            // Windows 10 1607 부터 제공한다. 그 전 버전에서는 EntryPointNotFoundException 이 난다.
            [DllImport("user32.dll")]
            public static extern bool AdjustWindowRectExForDpi(ref NativeRect lpRect, uint dwStyle,
                                                               [MarshalAs(UnmanagedType.Bool)] bool bMenu,
                                                               uint dwExStyle, uint dpi);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool GetCursorPos(out NativePoint lpPoint);

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWindowsProc lpfn, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr FindWindow(string className, string windowName);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")]
            public static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string className, string windowName);

            [DllImport("shell32.dll")]
            public static extern UIntPtr SHAppBarMessage(uint dwMessage, ref NativeAppBarData pData);

            [DllImport("user32.dll")]
            public static extern bool IsIconic(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            // 실패 사유를 Win32Exception 으로 만들어야 하므로 마지막 오류 코드를 보존합니다.
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW", SetLastError = true)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref NativeMonitorInfoEx lpmi);

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
            public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref NativeDevMode lpDevMode);

            [DllImport("shcore.dll")]
            public static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref NativeMargins pMarInset);

            [DllImport("dwmapi.dll")]
            public static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref NativeBlurBehind pBlurBehind);

            [DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
        }

        #endregion 네이티브 함수
    }
}

#endif
