using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 최상위 · 테두리 · 크기 조절 · 작업표시줄 등 창 상태 구현 </summary>
    internal sealed class DeskWindowStateService : IDeskWindowStateService {

        public bool IsTopMost => WindowDeskAPI.IsTopMostRequested;

        public bool IsBorderless => DeskPlatform.IsBorderless(WindowDeskAPI.WindowHandle);

        public bool IsResizable => WindowDeskAPI.IsResizableRequested;

        public bool IsTaskbarButtonVisible => DeskPlatform.IsTaskbarButtonVisible(WindowDeskAPI.WindowHandle);

        public bool IsCursorConfined => WindowDeskAPI.IsCursorConfined;

        public Vector2Int GetCursorPositionOnDesktop() {
            return WindowDeskAPI.GetCursorPositionOnDesktop();
        }

        public bool SetTopMost(bool topMost) {
            WindowDeskAPI.SetTopMost(topMost);
            return IsTopMost == topMost;
        }

        public bool SetBorderless(bool borderless) {
            WindowDeskAPI.SetBorderless(borderless);
            return IsBorderless == borderless;
        }

        public bool SetResizable(bool resizable) {
            WindowDeskAPI.SetResizable(resizable);
            return IsResizable == resizable;
        }

        /// <summary> 작업표시줄 버튼을 전환한다. Alt+Tab 목록도 같은 스타일 비트를 따라간다. </summary>
        public bool SetTaskbarButtonVisible(bool visible) {
            WindowDeskAPI.SetTaskbarButtonVisible(visible);
            return IsTaskbarButtonVisible == visible;
        }

        public bool SetCursorConfined(bool confined) {
            WindowDeskAPI.SetCursorConfined(confined);
            return IsCursorConfined == confined;
        }
    }
}
