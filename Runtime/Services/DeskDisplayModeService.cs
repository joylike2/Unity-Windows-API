using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 전체화면 · 창 · 테두리없는 창 전환 구현 </summary>
    internal sealed class DeskDisplayModeService : IDeskDisplayModeService {

        /// <summary> 현재 표시 방식. 유니티 모드와 실제 창 스타일을 함께 보고 판단한다. </summary>
        public DESK_DISPLAY_MODE Current {
            get {
                // PlayerSettings 가 전용 전체화면으로 시작했을 수 있다. 우리는 그 모드를 쓰지 않으므로
                // 전체화면 계열은 모두 FULLSCREEN_WINDOW 로 본다.
                if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen
                    || Screen.fullScreenMode == FullScreenMode.FullScreenWindow) {
                    return DESK_DISPLAY_MODE.FULLSCREEN_WINDOW;
                }

                return DeskPlatform.IsBorderless(WindowDeskAPI.WindowHandle)
                    ? DESK_DISPLAY_MODE.BORDERLESS_WINDOWED
                    : DESK_DISPLAY_MODE.WINDOWED;
            }
        }

        /// <summary> 테두리없는 창은 창 스타일을 직접 고쳐야 하므로 BORDERLESS 를 선언한 환경에서만 쓸 수 있다. </summary>
        public bool IsSupported(DESK_DISPLAY_MODE mode) {
            if (mode != DESK_DISPLAY_MODE.BORDERLESS_WINDOWED) {
                return true;
            }

            return DeskPlatform.IsWindowControlEnabled
                   && WindowDeskAPI.IsFeatureEnabled(DESK_WINDOW_FEATURE.BORDERLESS);
        }

        /// <summary>
        /// 표시 방식을 바꾼다. 고른 해상도를 그대로 들고 가야 하므로 해상도 적용 경로로 넘긴다.
        /// 창 모드에서 작업 영역에 맞춰 줄었더라도 전체화면에서는 고른 값으로 되돌아간다.
        /// </summary>
        public bool Apply(DESK_DISPLAY_MODE mode) {
            DESK_DISPLAY_MODE target = mode;

            if (!IsSupported(target)) {
                Debug.LogWarning($"[DeskDisplayModeService] 이 환경에서 {mode} 를 쓸 수 없어 {DESK_DISPLAY_MODE.WINDOWED} 로 대체합니다.");
                target = DESK_DISPLAY_MODE.WINDOWED;
            }

            // 해상도 기능을 안 쓰는 게임은 고를 해상도 자체가 없으므로 모드만 바꾼다.
            if (!WindowDeskAPI.IsFeatureEnabled(DESK_WINDOW_FEATURE.RESOLUTION_INFO)) {
                bool modeChanged = Current != target;

                Screen.fullScreenMode = ToUnityMode(target);

                if (modeChanged) {
                    DeskEventPump.RunNextFrame(() => FinishModeChange(target));
                }

                return true;
            }

            return WindowDeskAPI.Resolution.Apply(WindowDeskAPI.Resolution.GetApplied(), target).IsSuccess;
        }

        /// <summary> 유니티가 창을 다시 만든 뒤 창 스타일과 최상위를 맞추고 알림을 낸다. </summary>
        internal static void FinishModeChange(DESK_DISPLAY_MODE mode) {
            bool isWindowed = mode == DESK_DISPLAY_MODE.WINDOWED || mode == DESK_DISPLAY_MODE.BORDERLESS_WINDOWED;

            if (isWindowed) {
                WindowDeskAPI.ApplyBorderless(mode == DESK_DISPLAY_MODE.BORDERLESS_WINDOWED);
            }

            // 최상위 여부가 기준 영역을 정하므로 창을 맞추기 전에 확정해야 한다.
            WindowDeskAPI.RefreshTopMost();

            if (isWindowed) {
                // 유니티가 창을 자기 기준으로 놓으므로 작업 영역에 맞춰 다시 앉힌다.
                WindowDeskAPI.FitWindowToMonitor(WindowDeskAPI.Monitors.CurrentIndex);
            }

            DeskEvents.RaiseDisplayModeChanged(mode);
        }

        /// <summary> 표시 방식을 유니티 전체화면 모드로 옮긴다. </summary>
        internal static FullScreenMode ToUnityMode(DESK_DISPLAY_MODE mode) {
            switch (mode) {
                case DESK_DISPLAY_MODE.FULLSCREEN_WINDOW:
                    return FullScreenMode.FullScreenWindow;

                default:
                    return FullScreenMode.Windowed;
            }
        }
    }
}
