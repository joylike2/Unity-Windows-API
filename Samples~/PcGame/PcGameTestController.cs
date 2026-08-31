using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeLogs.WindowUtil.Samples {

    /// <summary> 데모 화면의 버튼이 부르는 진입점. WindowDeskAPI 를 어떻게 쓰는지 보여주는 예제다. </summary>
    public sealed class PcGameTestController : MonoBehaviour, IDeskDisplayListener {

        private const int RECOVER_WIDTH = 1280;
        private const int RECOVER_HEIGHT = 720;

        private IReadOnlyList<DeskResolution> _cachedResolutions;
        private int _listedMonitorIndex = -1;

        #region 초기화

        /// <summary> PC 게임 프로파일로 초기화 </summary>
        public void InitializePcGame() {
            DemoLog.Section("PC 게임 초기화");
            LogInitialize(WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE.PC_GAME));
        }

        private static void LogInitialize(bool result) {
            DemoLog.Result($"프로파일 {WindowDeskAPI.ActiveProfiles} / 초기화 {result}", WindowDeskAPI.IsInitialized);
            DemoLog.Info($"기능 {WindowDeskAPI.EnabledFeatures}");
            DemoLog.Info($"창 제어 가능 {WindowDeskAPI.IsWindowControlEnabled}");
        }

        #endregion 초기화

        #region 해상도

        /// <summary> 지금 적용된 해상도 </summary>
        public void LogCurrentResolution() {
            DemoLog.Section("현재 해상도");

            string current = WindowDeskAPI.TryGetCurrentMonitor(out DeskMonitorInfo monitor)
                ? $"{monitor.DeviceName} ({monitor.Bounds.width}x{monitor.Bounds.height})"
                : "모니터 미확인";

            DemoLog.Info($"창이 놓인 모니터 : {current}");
            DemoLog.Info($"게임 적용 : {WindowDeskAPI.GetAppliedResolution()}");
            DemoLog.Info($"유니티 Screen : {Screen.width}x{Screen.height}");

            LogMonitorList();
        }

        /// <summary>
        /// 연결된 모니터를 전부 찍는다.
        /// OS 열거 순서(대괄호 숫자)와 물리 배치 순서(좌N)는 다르므로 둘 다 보여준다.
        /// </summary>
        private static void LogMonitorList() {
            IReadOnlyList<DeskMonitorInfo> all = WindowDeskAPI.GetMonitors();

            if (all.Count == 0) {
                DemoLog.Warn("모니터 목록이 비어 있습니다. MONITOR_INFO 를 선언했는지 확인하십시오.");
                return;
            }

            IReadOnlyList<int> leftToRight = WindowDeskAPI.GetLeftToRightOrder();

            DemoLog.Info($"연결된 모니터 {all.Count}대");
            DemoLog.Info($"왼쪽부터 배치 순서 : {DemoMonitors.FormatOrder(leftToRight)}");

            for (int i = 0; i < all.Count; i++) {
                DeskMonitorInfo monitor = all[i];
                string mark = i == WindowDeskAPI.CurrentMonitorIndex ? "▶" : "  ";
                string role = monitor.IsPrimary ? " [주]" : string.Empty;
                string place = DemoMonitors.PlacementTag(leftToRight, i);

                DemoLog.Info($"{mark} [{i}]{place} {monitor.DeviceName}{role} : {WindowDeskAPI.GetMonitorResolution(i)} " +
                             $"/ 배율 {monitor.ScaleFactor * 100:0}% / 작업영역 {monitor.WorkArea.width}x{monitor.WorkArea.height} " +
                             $"/ 좌상단 ({monitor.Bounds.x}, {monitor.Bounds.y})");
            }
        }

        /// <summary> 마우스를 창 안에 가둔다 </summary>
        public void CursorConfineOn() {
            WindowDeskAPI.SetCursorConfined(true);
            LogToggle("마우스 가두기 ON", WindowDeskAPI.IsCursorConfined);
            DemoLog.Info("창 밖으로 커서가 나가지 않는지 확인하십시오. 포커스를 잃으면 잠시 풀립니다.");
        }

        /// <summary> 마우스 가두기를 푼다 </summary>
        public void CursorConfineOff() {
            WindowDeskAPI.SetCursorConfined(false);
            LogToggle("마우스 가두기 OFF", !WindowDeskAPI.IsCursorConfined);
        }

        /// <summary> 옵션에서 고른 프레임을 적용한다. 다른 설정은 건드리지 않는다 </summary>
        /// <param name="frameRate">적용할 기준 프레임.</param>
        public void ApplyFrameRate(int frameRate) {
            DemoLog.Section($"기준 프레임 {frameRate}");
            WindowDeskAPI.SetTargetFrameRate(frameRate);
            DemoLog.Result($"관리중 {WindowDeskAPI.IsFrameRateManaged} / 기준 {WindowDeskAPI.TargetFrameRate}",
                           WindowDeskAPI.TargetFrameRate == frameRate);
        }

        /// <summary> 옵션 토글처럼 절전을 전환한다 </summary>
        public void TogglePowerSaving() {
            bool next = !WindowDeskAPI.IsPowerSavingEnabled;

            WindowDeskAPI.SetPowerSaving(next);
            LogToggle($"절전 {(next ? "ON" : "OFF")}", WindowDeskAPI.IsPowerSavingEnabled == next);

            if (next) {
                DemoLog.Info("알탭으로 창을 벗어나면 상단 실측 프레임이 떨어지고, 돌아오면 원래대로 돌아옵니다.");
            }
        }

        /// <summary> 옵션 토글처럼 수직 동기화를 전환한다 </summary>
        public void ToggleVSync() {
            bool next = !WindowDeskAPI.IsVSyncEnabled;

            WindowDeskAPI.SetVSync(next);
            LogToggle($"VSync {(next ? "ON" : "OFF")}", WindowDeskAPI.IsVSyncEnabled == next);

            if (next) {
                DemoLog.Info("모니터 주사율을 따릅니다. 기준 프레임은 무시됩니다.");
            }
        }

        /// <summary> 프레임 관련 상태 전체 </summary>
        public void LogFrameState() {
            DemoLog.Section("프레임 상태");
            DemoLog.Info($"관리중 {WindowDeskAPI.IsFrameRateManaged} / 기준 {WindowDeskAPI.TargetFrameRate}");
            DemoLog.Info($"절전 {WindowDeskAPI.IsPowerSavingEnabled} / 배경 {WindowDeskAPI.BackgroundFrameRate}");
            DemoLog.Info($"VSync {WindowDeskAPI.IsVSyncEnabled} / 포커스 {WindowDeskAPI.HasFocus}");
            DemoLog.Info($"유니티 실제 targetFrameRate = {Application.targetFrameRate}");
        }

        /// <summary> 현재 설정을 파일에 저장한다 </summary>
        public void SaveSettingsToFile() {
            DemoLog.Section("설정 저장");
            DemoLog.Result(WindowDeskAPI.SettingsFilePath, WindowDeskAPI.SaveSettings());
        }

        /// <summary> 저장 파일을 지운다. 다음 실행에서 복원이 없는지 확인할 때 쓴다 </summary>
        public void DeleteSettingsFile() {
            DemoLog.Section("설정 파일 삭제");
            string path = WindowDeskAPI.SettingsFilePath;

            if (!File.Exists(path)) {
                DemoLog.Warn($"파일이 없습니다 : {path}");
                return;
            }

            File.Delete(path);
            DemoLog.Result($"삭제됨 : {path}", true);
        }

        /// <summary> 화면에 목록으로 띄우기 위해 캐시된 해상도. 아직 불러오지 않았으면 null </summary>
        public IReadOnlyList<DeskResolution> CachedResolutions => _cachedResolutions;

        /// <summary> 해상도 목록을 다시 불러온다 </summary>
        public void ReloadResolutions() {
            DemoLog.Section("해상도 목록 불러오기");
            _cachedResolutions = WindowDeskAPI.GetSupportedResolutions();
            _listedMonitorIndex = WindowDeskAPI.CurrentMonitorIndex;

            string device = WindowDeskAPI.TryGetCurrentMonitor(out DeskMonitorInfo monitor)
                ? monitor.DeviceName
                : "모니터 미확인";

            DemoLog.Result($"{device} / {_cachedResolutions.Count}개", _cachedResolutions.Count > 0);
        }

        /// <summary> 창이 다른 모니터로 넘어가면 목록이 옛 모니터 것으로 남으므로 다시 뽑는다 </summary>
        public void OnCurrentMonitorChanged(int monitorIndex) {
            string device = WindowDeskAPI.TryGetMonitor(monitorIndex, out DeskMonitorInfo monitor)
                ? monitor.DeviceName
                : "모니터 미확인";

            DemoLog.Success($"[알림] 모니터 이동 : {device}");

            if (_cachedResolutions != null) {
                ReloadResolutions();
            }
        }

        /// <summary> 해상도 알림 수신부 </summary>
        public void OnResolutionChanged(DeskResolution resolution) {
            DemoLog.Success($"[알림] 해상도 변경 : {resolution}");
        }

        /// <summary> 표시 방식 알림 수신부 </summary>
        public void OnDisplayModeChanged(DESK_DISPLAY_MODE mode) {
            DemoLog.Success($"[알림] 표시 방식 변경 : {mode}");
        }

        /// <summary> 목록에서 고른 해상도를 적용한다 </summary>
        /// <param name="index">캐시된 목록의 인덱스</param>
        public void ApplyResolution(int index) {
            ApplyResolutionAt(index);
        }

        private void ApplyResolutionAt(int index) {
            EnsureResolutions();

            if (_cachedResolutions.Count == 0) {
                DemoLog.Error("해상도 목록이 비어 있습니다.");
                return;
            }

            index = Mathf.Clamp(index, 0, _cachedResolutions.Count - 1);

            DemoLog.Section($"해상도 적용 [{index}]");
            DeskResolutionApplyResult result = WindowDeskAPI.ApplyResolution(_cachedResolutions[index]);
            DemoLog.Result($"{result}", result.IsSuccess);
        }

        private void EnsureResolutions() {
            if (_cachedResolutions == null) {
                _cachedResolutions = WindowDeskAPI.GetSupportedResolutions();
            }
        }

        #endregion 해상도

        #region 표시 방식

        /// <summary> 전체화면. 테두리없는 창이 모니터 전체를 덮고 고른 해상도로 그린다 </summary>
        public void ApplyFullscreenWindow() {
            ApplyDisplayMode(DESK_DISPLAY_MODE.FULLSCREEN_WINDOW);
        }

        /// <summary> 일반 창 </summary>
        public void ApplyWindowed() {
            ApplyDisplayMode(DESK_DISPLAY_MODE.WINDOWED);
        }

        /// <summary> 지금 표시 방식과 지원 여부 </summary>
        public void LogDisplayMode() {
            DemoLog.Section("표시 방식");
            DemoLog.Info($"현재 : {WindowDeskAPI.CurrentDisplayMode}");

            foreach (DESK_DISPLAY_MODE mode in Enum.GetValues(typeof(DESK_DISPLAY_MODE))) {
                DemoLog.Info($"  {mode} 지원 : {WindowDeskAPI.IsDisplayModeSupported(mode)}");
            }
        }

        private static void ApplyDisplayMode(DESK_DISPLAY_MODE mode) {
            DemoLog.Section($"표시 방식 {mode}");
            bool accepted = WindowDeskAPI.ApplyDisplayMode(mode);

            DemoLog.Result($"Apply({mode}) = {accepted}", accepted);

            if (!accepted) {
                LogDisplayModeRejection(mode);
                return;
            }

            DemoLog.Info("적용은 한 프레임 뒤에 끝납니다.");
        }

        /// <summary> false 만 보면 원인을 알 수 없으므로 거절될 수 있는 조건을 전부 찍는다 </summary>
        private static void LogDisplayModeRejection(DESK_DISPLAY_MODE mode) {
            DemoLog.Warn($"초기화 {WindowDeskAPI.IsInitialized} / {mode} 지원 {WindowDeskAPI.IsDisplayModeSupported(mode)}");
            DemoLog.Warn($"기능 {WindowDeskAPI.EnabledFeatures}");
            DemoLog.Warn($"적용된 해상도 {WindowDeskAPI.GetAppliedResolution()} / 모니터 {WindowDeskAPI.CurrentMonitorIndex}");
        }

        #endregion 표시 방식

        #region 창 상태

        /// <summary> 최상위 켜기 </summary>
        public void TopMostOn() {
            ApplyTopMost(true);
        }

        /// <summary> 최상위 끄기 </summary>
        public void TopMostOff() {
            ApplyTopMost(false);
        }

        /// <summary> 요청과 실제 적용이 다를 수 있으므로 둘 다 찍는다 </summary>
        private static void ApplyTopMost(bool topMost) {
            WindowDeskAPI.SetTopMost(topMost);

            bool isWindowed = Screen.fullScreenMode == FullScreenMode.Windowed;
            bool applied = WindowDeskAPI.IsTopMostApplied();

            DemoLog.Result($"요청 {topMost} / OS 플래그 {applied} / 창모드 {isWindowed}", applied == topMost);

            if (!isWindowed) {
                DemoLog.Warn("전체화면에서는 최상위를 걸지 않습니다. 창 모드로 바꾸면 그때 적용됩니다.");
            }
        }

        /// <summary> 드래그로 창 크기 변경을 허용한다 </summary>
        public void ResizableOn() {
            ApplyResizable(true);
        }

        /// <summary> 드래그로 창 크기 변경을 허용하지 않는다 </summary>
        public void ResizableOff() {
            ApplyResizable(false);
        }

        /// <summary> 전체화면에서는 요청만 기억하므로 요청값과 창모드를 함께 찍는다 </summary>
        private static void ApplyResizable(bool resizable) {
            DemoLog.Section($"크기 변경 {(resizable ? "허용" : "허용 안함")}");

            WindowDeskAPI.SetResizable(resizable);

            bool isWindowed = Screen.fullScreenMode == FullScreenMode.Windowed;

            DemoLog.Result($"요청 {resizable} / 창모드 {isWindowed} / 해상도 {Screen.width}x{Screen.height}",
                           WindowDeskAPI.IsResizableRequested == resizable);

            if (!isWindowed) {
                DemoLog.Warn("전체화면에서는 테두리를 걸지 않습니다. 창 모드로 바꾸면 그때 적용됩니다.");
            }
        }

        private static void LogToggle(string label, bool result) {
            DemoLog.Result($"{label} = {result}", result);
        }

        #endregion 창 상태

        #region 알림

        private void OnEnable() {
            WindowDeskAPI.AddDisplayListener(this);

            // 라이브러리가 왜 거절했는지는 Debug 로만 나간다. 빌드에는 콘솔이 없으니 화면 로그로 끌어온다.
            DemoLog.CaptureUnityLogs(true);
        }

        private void OnDisable() {
            DemoLog.CaptureUnityLogs(false);
            WindowDeskAPI.RemoveDisplayListener(this);
        }

        public void OnDisplayConfigurationChanged(DeskMonitorLayout layout) {
            DemoLog.Success($"[알림] 구성 변경 : {layout}");
        }

        public void OnCurrentMonitorLost(int lostIndex) {
            DemoLog.Warn($"[알림] 현재 모니터 사라짐 : 인덱스 {lostIndex}");
        }

        public void OnDpiScaleChanged(float scaleRatio) {
            DemoLog.Success($"[알림] 배율 변경 : x{scaleRatio:0.###}");
        }

        public void OnWindowFocusChanged(bool hasFocus) {
            DemoLog.Info($"[알림] 포커스 {(hasFocus ? "얻음" : "잃음")}");
        }

        #endregion 알림

        #region 비상 복구

        /// <summary> 창을 주 모니터 가운데 창 모드로 되돌린다 </summary>
        public void RecoverWindow() {
            int target = WindowDeskAPI.PrimaryMonitorIndex >= 0 ? WindowDeskAPI.PrimaryMonitorIndex : 0;

            Screen.SetResolution(RECOVER_WIDTH, RECOVER_HEIGHT, FullScreenMode.Windowed);

            DeskMoveResult result = WindowDeskAPI.MoveWindowToMonitor(target, DeskMoveOptions.Default);
            DemoLog.Result($"창 복구 : {result}", result.IsSuccess);
        }

        /// <summary> 게임을 끝낸다 </summary>
        public void QuitApplication() {
            DemoLog.Warn("종료합니다.");
            Application.Quit();
        }

        #endregion 비상 복구
    }
}
