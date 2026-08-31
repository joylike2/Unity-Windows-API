#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// macOS(AppKit / CoreGraphics) 창 제어 구현.
    /// 현재는 뼈대만 있는 스텁입니다. 호출해도 아무 일이 일어나지 않고 한 번만 안내 로그를 남깁니다.
    /// 구현 계획은 Docs/DeskWindow.md 부록 A 를 참고하십시오.
    /// </summary>
    internal static class DeskPlatform {

        private const string NOT_IMPLEMENTED_MESSAGE =
            "[DeskPlatform.Mac] macOS implementation is not written yet. See Docs/DeskWindow.md Appendix A.";

        private static bool _isWarned;

        #region 플랫폼 지원 여부

        /// <summary>조회 기능 미구현이므로 false 입니다.</summary>
        internal static bool IsSupported => false;

        /// <summary>창 제어 미구현이므로 false 입니다.</summary>
        internal static bool IsWindowControlEnabled => false;

        /// <summary>구현 전까지는 제공하는 기능이 없습니다. 구현하면서 하나씩 채웁니다.</summary>
        internal static DESK_WINDOW_FEATURE SupportedFeatures => DESK_WINDOW_FEATURE.NONE;

        #endregion 플랫폼 지원 여부

        #region 창 핸들

        internal static IntPtr FindOwnWindowHandle() {
            WarnOnce();
            return IntPtr.Zero;
        }

        #endregion 창 핸들

        #region 창 외형

        internal static void SetBorderless(IntPtr hWnd, bool borderless) {
            WarnOnce();
        }

        /// <summary> 제목 표시줄과 테두리가 제거된 상태인지 조회한다. </summary>
        internal static bool IsBorderless(IntPtr hWnd) {
            WarnOnce();
            return false;
        }

        internal static void SetResizable(IntPtr hWnd, bool resizable) {
            WarnOnce();
        }

        /// <summary> 드래그로 크기를 바꿀 수 있는 상태인지 조회한다. </summary>
        internal static bool IsResizable(IntPtr hWnd) {
            WarnOnce();
            return false;
        }

        /// <summary> 창 배경을 뚫어 뒤가 비쳐 보이게 한다. </summary>
        internal static bool SetTransparent(IntPtr hWnd, bool transparent) {
            WarnOnce();
            return false;
        }

        /// <summary> 마지막 투명 처리 시도의 결과 </summary>
        internal static string LastTransparentReport => "이 플랫폼은 투명을 제공하지 않습니다";

        /// <summary> 클릭 통과가 다른 프로세스의 창까지 닿도록 레이어 창으로 만든다. </summary>
        internal static bool EnableLayered(IntPtr hWnd) {
            WarnOnce();
            return false;
        }

        /// <summary> 마우스 입력을 창 뒤로 흘려보낸다. </summary>
        internal static void SetClickThrough(IntPtr hWnd, bool clickThrough) {
            WarnOnce();
        }

        /// <summary> 마우스 입력을 창 뒤로 흘려보내는 상태인지 조회한다. </summary>
        internal static bool IsClickThrough(IntPtr hWnd) {
            return false;
        }

        internal static void SetTopMost(IntPtr hWnd, bool topMost) {
            WarnOnce();
        }

        /// <summary> 항상 다른 창 위에 표시되는 상태인지 조회한다. </summary>
        internal static bool IsTopMost(IntPtr hWnd) {
            WarnOnce();
            return false;
        }

        /// <summary> 작업표시줄에 버튼이 보이는 상태인지 조회한다. </summary>
        internal static bool IsTaskbarButtonVisible(IntPtr hWnd) {
            WarnOnce();
            return true;
        }

        internal static void SetTaskbarButtonVisible(IntPtr hWnd, bool visible) {
            WarnOnce();
        }

        /// <summary> 창의 원본 스타일과 영역을 읽어 스냅샷으로 만든다. </summary>
        internal static bool TryCaptureWindowStyle(IntPtr hWnd, out DeskWindowStyleSnapshot snapshot) {
            WarnOnce();
            snapshot = default;
            return false;
        }

        /// <summary> 스냅샷 시점의 창 상태로 되돌린다. </summary>
        internal static bool RestoreWindowStyle(IntPtr hWnd, DeskWindowStyleSnapshot snapshot) {
            WarnOnce();
            return false;
        }

        /// <summary> 같은 제목을 가진 다른 인스턴스의 창을 찾아 앞으로 끌어올린다. </summary>
        internal static bool TryActivateWindowByTitle(string windowTitle) {
            WarnOnce();
            return false;
        }

        /// <summary> 창을 앞으로 끌어올린다 </summary>
        internal static bool ActivateWindow(IntPtr hWnd) {
            WarnOnce();
            return false;
        }

        #endregion 창 외형

        #region 입력

        internal static bool TryGetCursorPosition(out Vector2Int position) {
            WarnOnce();
            position = Vector2Int.zero;
            return false;
        }

        #endregion 입력

        #region 창 위치와 크기

        internal static bool TryGetWindowRect(IntPtr hWnd, out RectInt rect) {
            WarnOnce();
            rect = default;
            return false;
        }

        internal static void SetPosition(IntPtr hWnd, int x, int y) {
            WarnOnce();
        }

        internal static void SetSize(IntPtr hWnd, int width, int height) {
            WarnOnce();
        }

        internal static void SetWindowRect(IntPtr hWnd, int x, int y, int width, int height) {
            WarnOnce();
        }

        internal static bool TryGetWindowChrome(IntPtr hWnd, bool hasCaption, bool isResizable,
                                                out int width, out int height) {
            width = 0;
            height = 0;
            return false;
        }

        #endregion 창 위치와 크기

        #region 모니터 정보

        internal static bool TryEnumerateMonitors(List<DeskMonitorInfo> buffer, out Exception error) {
            WarnOnce();
            error = new NotImplementedException(NOT_IMPLEMENTED_MESSAGE);
            return false;
        }

        internal static IntPtr GetMonitorFromWindow(IntPtr hWnd) {
            WarnOnce();
            return IntPtr.Zero;
        }

        #endregion 모니터 정보

        #region 지원 해상도

        internal static List<DeskResolution> GetSupportedResolutions(string deviceName) {
            WarnOnce();
            return new List<DeskResolution>();
        }

        internal static bool TryGetCurrentResolution(string deviceName, out DeskResolution resolution) {
            WarnOnce();
            resolution = default;
            return false;
        }

        #endregion 지원 해상도

        // 매 프레임 호출될 수 있는 함수가 있으므로 경고는 한 번만 남깁니다.
        private static void WarnOnce() {
            if (_isWarned) {
                return;
            }

            _isWarned = true;
            Debug.LogWarning(NOT_IMPLEMENTED_MESSAGE);
        }
    }
}

#endif
