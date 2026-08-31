using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil.Samples {

    /// <summary>
    /// 바탕화면 게임 데모.
    /// <see cref="DESK_WINDOW_PROFILE.DESKTOP_GAME"/> 하나로 테두리없는 창 · 투명 · 알파 판정이 함께 걸립니다.
    /// 화면 UI 는 전부 IMGUI 로 그립니다. 캔버스가 없어도 빌드에서 바로 확인할 수 있습니다.
    /// </summary>
    public sealed class DesktopGameTestController : MonoBehaviour {

        private const string TRAY_TOOLTIP = "DeskWindows 바탕화면 데모";
        private const string MENU_QUIT = "종료";

        private const int BUTTON_WIDTH = 150;
        private const int BUTTON_HEIGHT = 34;
        private const int MONITOR_BUTTON_WIDTH = 420;
        private const int SMALL_BUTTON_WIDTH = 98;

        /// <summary> 프레임 제한 버튼에 올릴 값 </summary>
        private static readonly int[] FRAME_RATE_CHOICES =
            { 30, 60, 120, WindowDeskAPI.UNLIMITED_FRAME_RATE };

        /// <summary> 이 시간마다 측정값을 갱신한다. 매 프레임 갱신하면 숫자가 튀어 읽을 수 없다 </summary>
        private const float FPS_SAMPLE_SECONDS = 0.5f;

        [Header("켜 두면 씬이 시작하자마자 바탕화면 모드로 들어간다")]
        [SerializeField] private bool _initializeOnStart;

        [Header("화면 로그 (IMGUI). 배경을 그리지 않아 투명을 가리지 않는다")]
        [SerializeField] private bool _drawLog = true;

        [Tooltip("비워 두면 기본 폰트를 쓴다. 한글이 네모로 나오면 한글 폰트를 넣으십시오")]
        [SerializeField] private Font _font;

        [SerializeField] private int _fontSize = 16;
        [SerializeField] private int _margin = 24;

        [SerializeField] private Color _textColor = Color.white;

        [Header("고해상도에서 IMGUI 가 작게 나오지 않도록 모니터 배율을 건다")]
        [SerializeField] private bool _scaleGui = true;

        private GUIStyle _logStyle;
        private GUIStyle _statusStyle;

        private float _fpsElapsed;
        private int _fpsFrames;
        private float _fps;

        private void Start() {
            // 라이브러리가 왜 거절했는지는 Debug 로만 나간다. 빌드에는 콘솔이 없으니 화면 로그로 끌어온다.
            DemoLog.CaptureUnityLogs(true);

            DemoLog.Section("바탕화면 게임 데모");

            if (!WindowDeskAPI.IsWindowControlEnabled) {
                DemoLog.Warn("에디터에서는 창을 건드리지 않습니다. Windows 빌드에서 확인하십시오.");
            }

            DemoLog.Info("에디터 메뉴 Tools/WindowDeskAPI/Setup/Desktop Wallpaper 를 먼저 실행해야 합니다.");
            DemoLog.Info("초기화하면 그려진 곳에서는 클릭이 먹히고 빈 곳은 바탕화면으로 넘어갑니다.");

            if (_initializeOnStart) {
                InitializeDesktopGame();
            }
        }

        /// <summary> 실제 프레임을 재어 둔다. 일정 시간마다 평균을 내야 숫자가 읽을 만하다. </summary>
        private void Update() {
            _fpsElapsed += Time.unscaledDeltaTime;
            _fpsFrames++;

            if (_fpsElapsed < FPS_SAMPLE_SECONDS) {
                return;
            }

            _fps = _fpsFrames / _fpsElapsed;
            _fpsFrames = 0;
            _fpsElapsed = 0f;
        }

        #region 초기화

        /// <summary> 바탕화면 게임 프로파일로 초기화한다. 필요한 창 설정이 한 번에 걸린다 </summary>
        public void InitializeDesktopGame() {
            DemoLog.Section("바탕화면 게임 초기화");

            bool result = WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE.DESKTOP_GAME);

            DemoLog.Result($"프로파일 {WindowDeskAPI.ActiveProfiles} / 초기화 {result}", WindowDeskAPI.IsInitialized);
            DemoLog.Info($"기능 {WindowDeskAPI.EnabledFeatures}");
            DemoLog.Info($"창 제어 가능 {WindowDeskAPI.IsWindowControlEnabled}");
            DemoLog.Info("창 스타일은 다음 프레임에 걸립니다. [상태 보기] 로 확인하십시오.");

            EnableExitTray();
        }

        /// <summary> 테두리와 작업표시줄 버튼이 없어 창을 닫을 길이 없으므로 트레이에 종료 수단을 남긴다 </summary>
        private void EnableExitTray() {
            if (!WindowDeskAPI.IsTraySupported) {
                DemoLog.Warn("이 플랫폼은 트레이를 제공하지 않아 종료 수단을 만들지 못했습니다.");
                return;
            }

            if (!WindowDeskAPI.EnableTray(TRAY_TOOLTIP)) {
                DemoLog.Warn("트레이를 띄우지 못했습니다. 작업 관리자로 종료해야 할 수 있습니다.");
                return;
            }

            WindowDeskAPI.ClearTrayMenu();
            WindowDeskAPI.AddTrayMenuItem(MENU_QUIT, Application.Quit);

            DemoLog.Success($"트레이 아이콘을 우클릭해 [{MENU_QUIT}] 로 빠져나올 수 있습니다.");
        }

        #endregion 초기화

        #region 그 밖

        /// <summary> 현재 창 상태를 한 번에 남긴다 </summary>
        public void LogState() {
            DemoLog.Section("바탕화면 창 상태");
            DemoLog.Info($"초기화 {WindowDeskAPI.IsInitialized} / 프로파일 {WindowDeskAPI.ActiveProfiles}");
            DemoLog.Info($"표시 방식 {WindowDeskAPI.CurrentDisplayMode}");
            DemoLog.Info($"테두리 없음 {WindowDeskAPI.IsBorderless}");
            DemoLog.Info($"투명 요청 {WindowDeskAPI.IsTransparent}");
            DemoLog.Info($"알파 판정 {WindowDeskAPI.IsClickThrough} / 지금 통과 중 {WindowDeskAPI.IsPassingThroughNow}");
            DemoLog.Info($"클릭 통과 실제 적용 {WindowDeskAPI.IsClickThroughApplied()}");
            DemoLog.Info($"빈 자리 알파 {WindowDeskAPI.BackgroundAlpha:F3}  (0 이면 창 문제, 1 이면 렌더 문제)");
            DemoLog.Info($"투명 처리 {WindowDeskAPI.TransparentReport}");
            DemoLog.Info($"최상위 요청 {WindowDeskAPI.IsTopMostRequested}");
            DemoLog.Info($"창 크기 {Screen.width}x{Screen.height} / 표시 {Screen.fullScreenMode}");
            DemoLog.Info($"배율 {WindowDeskAPI.CurrentDpiScale:0.##} 배");
            DemoLog.Info($"프레임 {DescribeFrameRate(WindowDeskAPI.TargetFrameRate)}"
                         + $" / 절전 {WindowDeskAPI.IsPowerSavingEnabled}"
                         + $" / 수직 동기화 {WindowDeskAPI.IsVSyncEnabled}");
            LogWorkArea();
            DemoLog.Info($"설정 파일 {(WindowDeskAPI.HasSavedSettings ? "있음" : "없음")} : {WindowDeskAPI.SettingsFilePath}");
            DemoLog.Info($"그래픽 API {SystemInfo.graphicsDeviceType}");
        }

        /// <summary> 현재 모니터의 작업표시줄 두께를 남긴다. 오브젝트 이동 제한에 쓸 값이다 </summary>
        public void LogWorkArea() {
            DeskEdgeInsets insets = WindowDeskAPI.GetWorkAreaInsets();
            DeskEdgeInsets scaled = WindowDeskAPI.GetScaledWorkAreaInsets();

            DemoLog.Info($"작업표시줄 높이 {insets.Bottom} px (물리 - 화면 · 월드 좌표용)");
            DemoLog.Info($"작업표시줄 높이 {scaled.Bottom} px (배율 적용 - 캔버스 UI 용)");
            DemoLog.Info($"물리 두께 {insets}");
            DemoLog.Info($"배율 적용 두께 {scaled}");

            if (!WindowDeskAPI.TryGetCurrentMonitor(out DeskMonitorInfo monitor)) {
                DemoLog.Warn("현재 모니터를 찾지 못해 영역을 남기지 못했습니다.");
                return;
            }

            DemoLog.Info($"모니터 전체 {monitor.Bounds}");
            DemoLog.Info($"작업 영역 {monitor.WorkArea}");
        }

        /// <summary> 최상위를 뒤집는다. 저장은 하지 않는다. 남기려면 [설정 저장] 을 눌러야 한다 </summary>
        public void ToggleTopMost() {
            if (!RequireInitialized("최상위")) {
                return;
            }

            bool next = !WindowDeskAPI.IsTopMostRequested;

            WindowDeskAPI.SetTopMost(next);

            // 요청이 막혔을 수 있으므로 결과를 되읽는다. 눌렀다고 적용된 것이 아니다.
            bool applied = WindowDeskAPI.IsTopMostRequested == next;

            DemoLog.Result($"최상위 {next} / 적용 {applied}"
                           + $" / OS 반영 {WindowDeskAPI.IsTopMostApplied()} (저장 안 함)", applied);
        }

        /// <summary> 초기화 전에는 대부분의 기능이 막힌다. 조용히 무시되지 않도록 먼저 알린다. </summary>
        private static bool RequireInitialized(string what) {
            if (WindowDeskAPI.IsInitialized) {
                return true;
            }

            DemoLog.Error($"{what} : 초기화 전에는 쓸 수 없습니다. [초기화] 를 먼저 누르십시오.");
            return false;
        }

        /// <summary> 지금 창 상태를 파일에 남긴다 </summary>
        public void SaveSettings() {
            bool saved = WindowDeskAPI.SaveSettings();

            DemoLog.Result($"설정 저장 {saved} : {WindowDeskAPI.SettingsFilePath}", saved);
        }

        /// <summary> 저장 파일을 지운다. 다음 실행이 첫 실행처럼 시작한다 </summary>
        public void DeleteSettings() {
            bool deleted = WindowDeskAPI.DeleteSettings();

            if (deleted) {
                DemoLog.Success("설정 파일을 지웠습니다. 다음 실행은 기본값으로 시작합니다.");
                return;
            }

            DemoLog.Warn("지울 설정 파일이 없습니다.");
        }

        /// <summary> 연결된 모니터를 전부 남긴다. 왼쪽부터의 물리 배치 순서도 함께 찍는다 </summary>
        public void LogMonitors() {
            DemoLog.Section("연결된 모니터");

            IReadOnlyList<DeskMonitorInfo> monitors = WindowDeskAPI.GetMonitors();
            IReadOnlyList<int> leftToRight = WindowDeskAPI.GetLeftToRightOrder();

            DemoLog.Info($"왼쪽부터 배치 순서 : {DemoMonitors.FormatOrder(leftToRight)}");

            for (int i = 0; i < monitors.Count; i++) {
                DemoLog.Info($"[{i}] {DescribeMonitor(i, monitors[i])}");
            }

            DemoLog.Info($"현재 {WindowDeskAPI.CurrentMonitorIndex} / 주 모니터 {WindowDeskAPI.PrimaryMonitorIndex}");
        }

        /// <summary> 창을 지정한 모니터로 옮긴다. 다음 실행에 같은 모니터로 뜨도록 위치를 저장한다 </summary>
        public void MoveToMonitor(int monitorIndex) {
            if (!RequireInitialized("모니터 이동")) {
                return;
            }

            DeskMoveResult result = WindowDeskAPI.MoveWindowToMonitor(monitorIndex);

            DemoLog.Result($"모니터 {monitorIndex} 로 이동 : {result.IsSuccess}", result.IsSuccess);

            if (result.IsSuccess) {
                WindowDeskAPI.SaveSettings();
            }
        }

        /// <summary> 프레임 제한을 바꾼다. <see cref="WindowDeskAPI.UNLIMITED_FRAME_RATE"/> 면 제한 없음 </summary>
        public void SetTargetFrameRate(int targetFrameRate) {
            WindowDeskAPI.SetTargetFrameRate(targetFrameRate);

            bool applied = WindowDeskAPI.TargetFrameRate == targetFrameRate;

            DemoLog.Result($"프레임 제한 {DescribeFrameRate(WindowDeskAPI.TargetFrameRate)} / 적용 {applied}", applied);
        }

        /// <summary> 백그라운드 절전을 뒤집는다. 창이 뒤로 가면 프레임을 낮춘다 </summary>
        public void TogglePowerSaving() {
            bool next = !WindowDeskAPI.IsPowerSavingEnabled;

            WindowDeskAPI.SetPowerSaving(next);

            bool applied = WindowDeskAPI.IsPowerSavingEnabled == next;

            DemoLog.Result($"절전 {next} / 적용 {applied}"
                           + $" (뒤에 있을 때 {WindowDeskAPI.BackgroundFrameRate} 프레임)", applied);
        }

        /// <summary> 수직 동기화를 뒤집는다 </summary>
        public void ToggleVSync() {
            bool next = !WindowDeskAPI.IsVSyncEnabled;

            WindowDeskAPI.SetVSync(next);

            bool applied = WindowDeskAPI.IsVSyncEnabled == next;

            DemoLog.Result($"수직 동기화 {next} / 적용 {applied}", applied);
        }

        /// <summary> 프레임 관련 상태를 남긴다 </summary>
        public void LogFrameRate() {
            DemoLog.Section("프레임");
            DemoLog.Info($"관리 중 {WindowDeskAPI.IsFrameRateManaged}");
            DemoLog.Info($"프레임 제한 {DescribeFrameRate(WindowDeskAPI.TargetFrameRate)}");
            DemoLog.Info($"절전 {WindowDeskAPI.IsPowerSavingEnabled} / 뒤에 있을 때 {WindowDeskAPI.BackgroundFrameRate}");
            DemoLog.Info($"수직 동기화 {WindowDeskAPI.IsVSyncEnabled}");
            DemoLog.Info($"포커스 {WindowDeskAPI.HasFocus}");
        }

        private static string DescribeFrameRate(int targetFrameRate) {
            if (targetFrameRate == WindowDeskAPI.UNLIMITED_FRAME_RATE) {
                return "제한 없음";
            }

            return targetFrameRate > 0 ? $"{targetFrameRate}" : "지정 안 함";
        }

        /// <summary> 로그를 비운다 </summary>
        public void ClearLog() {
            DemoLog.Clear();
        }

        /// <summary> 게임을 끝낸다 </summary>
        public void Quit() {
            Application.Quit();
        }

        private static string DescribeMonitor(int index, DeskMonitorInfo monitor) {
            string primary = monitor.IsPrimary ? " (주)" : string.Empty;
            string here = index == WindowDeskAPI.CurrentMonitorIndex ? " <- 지금" : string.Empty;
            string place = DemoMonitors.PlacementTag(WindowDeskAPI.GetLeftToRightOrder(), index);

            return $"{place} {monitor.DeviceName} {monitor.Bounds.width}x{monitor.Bounds.height} "
                   + $"{monitor.ScaleFactor * 100:0}%{primary}{here}";
        }

        #endregion 그 밖

        #region 화면 UI

        /// <summary>
        /// 조작 버튼과 로그를 화면에 직접 그린다. 빌드에는 콘솔이 없다.
        /// 상자나 스크롤뷰를 쓰면 배경 텍스처가 알파를 채워 투명이 깨지므로 버튼과 글자만 그린다.
        /// </summary>
        private void OnGUI() {
            EnsureLogStyle();

            float scale = ResolveGuiScale();
            Matrix4x4 previous = GUI.matrix;

            // IMGUI 는 CanvasScaler 를 거치지 않아 관찰자가 못 건드린다. 여기서 직접 배율을 건다.
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

            DrawControls(scale);

            if (_drawLog) {
                DrawLog(scale);
            }

            GUI.matrix = previous;
        }

        /// <summary> 배율을 건 뒤의 논리 화면 크기. GUI.matrix 를 걸면 좌표계가 그만큼 줄어든다. </summary>
        private static Vector2 GetLogicalScreen(float scale) {
            return new Vector2(Screen.width / scale, Screen.height / scale);
        }

        /// <summary> 배율을 못 구하면 1 로 둔다. 0 을 넣으면 화면이 사라진다. </summary>
        private float ResolveGuiScale() {
            if (!_scaleGui) {
                return 1f;
            }

            float scale = WindowDeskAPI.CurrentDpiScale;

            return scale > 0f ? scale : 1f;
        }

        private void DrawControls(float scale) {
            Vector2 screen = GetLogicalScreen(scale);

            GUILayout.BeginArea(new Rect(_margin, _margin, MONITOR_BUTTON_WIDTH, screen.y - (_margin * 2)));

            DrawFrameStatus();
            DrawActionButtons();
            DrawMonitorButtons();

            GUILayout.EndArea();
        }

        private void DrawActionButtons() {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("초기화", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                InitializeDesktopGame();
            }

            if (GUILayout.Button("상태 보기", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                LogState();
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            string topMostLabel = WindowDeskAPI.IsTopMostRequested ? "최상위 끄기" : "최상위 켜기";

            if (GUILayout.Button(topMostLabel, GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                ToggleTopMost();
            }

            if (GUILayout.Button("모니터 목록", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                LogMonitors();
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("작업표시줄", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                LogWorkArea();
            }

            if (GUILayout.Button("프레임 상태", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                LogFrameRate();
            }

            GUILayout.EndHorizontal();

            DrawFrameRateButtons();

            GUILayout.BeginHorizontal();

            string powerLabel = WindowDeskAPI.IsPowerSavingEnabled ? "절전 끄기" : "절전 켜기";

            if (GUILayout.Button(powerLabel, GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                TogglePowerSaving();
            }

            string vSyncLabel = WindowDeskAPI.IsVSyncEnabled ? "VSync 끄기" : "VSync 켜기";

            if (GUILayout.Button(vSyncLabel, GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                ToggleVSync();
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("설정 저장", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                SaveSettings();
            }

            if (GUILayout.Button("설정 삭제", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                DeleteSettings();
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("로그 지우기", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                ClearLog();
            }

            if (GUILayout.Button("종료", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                Quit();
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 실제 프레임을 실시간으로 보여 준다.
        /// 제한 · 절전 · 수직 동기화를 바꿨을 때 정말 반영됐는지 눈으로 확인하려면 이 값이 있어야 한다.
        /// </summary>
        private void DrawFrameStatus() {
            string limit = DescribeFrameRate(WindowDeskAPI.TargetFrameRate);
            string power = WindowDeskAPI.IsPowerSavingEnabled
                ? $"절전 {WindowDeskAPI.BackgroundFrameRate}"
                : "절전 끔";
            string vSync = WindowDeskAPI.IsVSyncEnabled ? "VSync 켬" : "VSync 끔";
            string focus = WindowDeskAPI.HasFocus ? string.Empty : "  [뒤에 있음]";

            GUILayout.Label($"{_fps:0} FPS   제한 {limit}   {power}   {vSync}{focus}", _statusStyle);
        }

        /// <summary> 프레임 제한 선택. 지금 값은 눌린 것처럼 보이게 이름에 표시한다. </summary>
        private void DrawFrameRateButtons() {
            GUILayout.BeginHorizontal();

            for (int i = 0; i < FRAME_RATE_CHOICES.Length; i++) {
                int choice = FRAME_RATE_CHOICES[i];
                bool isCurrent = WindowDeskAPI.TargetFrameRate == choice;
                string label = isCurrent ? $"[{DescribeFrameRate(choice)}]" : DescribeFrameRate(choice);

                if (GUILayout.Button(label, GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                    SetTargetFrameRate(choice);
                }
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 모니터마다 버튼 하나. 누르면 그 모니터로 창을 옮긴다.
        /// 버튼은 왼쪽부터의 물리 배치 순서로 세운다. 옵션 화면에서 유저가 보는 배치와 같아야
        /// 어느 모니터를 고르는지 헷갈리지 않기 때문이다. 이동은 OS 열거 인덱스로 한다.
        /// </summary>
        private void DrawMonitorButtons() {
            if (!WindowDeskAPI.IsInitialized) {
                return;
            }

            GUILayout.Space(BUTTON_HEIGHT / 2);

            IReadOnlyList<DeskMonitorInfo> monitors = WindowDeskAPI.GetMonitors();
            IReadOnlyList<int> leftToRight = WindowDeskAPI.GetLeftToRightOrder();

            for (int i = 0; i < leftToRight.Count; i++) {
                int monitorIndex = leftToRight[i];

                if (monitorIndex < 0 || monitorIndex >= monitors.Count) {
                    continue;
                }

                if (GUILayout.Button(DescribeMonitor(monitorIndex, monitors[monitorIndex]),
                                     GUILayout.Width(MONITOR_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT))) {
                    MoveToMonitor(monitorIndex);
                }
            }
        }

        /// <summary> 아래쪽 정렬이라 줄이 넘쳐도 최근 줄이 남는다. </summary>
        private void DrawLog(float scale) {
            Vector2 screen = GetLogicalScreen(scale);

            float left = _margin + MONITOR_BUTTON_WIDTH + _margin;
            float width = screen.x - left - _margin;
            float height = screen.y - (_margin * 2);

            if (width <= 0f || height <= 0f) {
                return;
            }

            GUI.Label(new Rect(left, _margin, width, height), DemoLog.GetText(), _logStyle);
        }

        /// <summary> 배경 없는 스타일을 한 번만 만든다. 새로 만든 GUIStyle 은 배경 텍스처가 비어 있다. </summary>
        private void EnsureLogStyle() {
            if (_logStyle != null) {
                return;
            }

            _statusStyle = new GUIStyle {
                richText = true,
                alignment = TextAnchor.UpperLeft,
                fontSize = _fontSize,
                font = _font
            };

            _statusStyle.normal.textColor = _textColor;
            _statusStyle.normal.background = null;

            _logStyle = new GUIStyle {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.LowerLeft,
                fontSize = _fontSize,
                font = _font
            };

            _logStyle.normal.textColor = _textColor;
            _logStyle.normal.background = null;
        }

        #endregion 화면 UI
    }
}
