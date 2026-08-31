using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil.Samples {

    /// <summary> 테스트 화면을 IMGUI 로 그린다. Canvas 도 EventSystem 도 입력 모듈도 쓰지 않는다. </summary>
    [RequireComponent(typeof(PcGameTestController))]
    public sealed class PcGameDemoGUI : MonoBehaviour {

        private const int STATUS_HEIGHT = 96;
        private const int RECOVERY_HEIGHT = 40;
        private const int BUTTON_PANEL_WIDTH = 260;
        private const int BUTTON_HEIGHT = 26;
        private const int CATEGORY_HEIGHT = 24;
        private const int PADDING = 6;
        private const int FONT_SIZE = 13;
        private const float FPS_SAMPLE_SECONDS = 0.5f;

        private static readonly Color COLOR_PANEL = new Color(0.10f, 0.10f, 0.13f, 0.96f);
        private static readonly Color COLOR_CATEGORY = new Color(0.95f, 0.70f, 0.30f, 1f);
        private static readonly Color COLOR_RECOVERY = new Color(0.55f, 0.25f, 0.25f, 1f);

        /// <summary> 버튼 한 칸. 라벨만 있고 동작이 없으면 카테고리 제목이다. </summary>
        private readonly struct Entry {
            public string Label { get; }
            public Action Action { get; }

            public Entry(string label, Action action) {
                Label = label;
                Action = action;
            }

            public bool IsCategory => Action == null;
        }

        [SerializeField] private Font _font;
        [SerializeField] private bool _drawBackground = true;

        [Header("화면 배율")]
        [SerializeField, Range(0.5f, 4f)] private float _uiScale = 1.5f;
        [SerializeField] private bool _followMonitorScale = true;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _recoveryEntries = new List<Entry>();

        private PcGameTestController _controller;
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _recoveryStyle;
        private Vector2 _buttonScroll;

        private float _fpsElapsed;
        private int _fpsFrames;
        private float _measuredFps;
        private Vector2 _logScroll;
        private int _lastLogCount;
        private float _appliedScale = 1f;

        /// <summary> 배율을 적용한 화면 너비 </summary>
        private float ScaledWidth => Screen.width / _appliedScale;

        /// <summary> 배율을 적용한 화면 높이 </summary>
        private float ScaledHeight => Screen.height / _appliedScale;

        private void Awake() {
            _controller = GetComponent<PcGameTestController>();

            BuildEntries();
            BuildRecoveryEntries();
        }

        /// <summary> 실제로 그려지는 프레임을 센다. 절전으로 떨어지는 순간을 눈으로 보기 위한 것이다. </summary>
        private void Update() {
            _fpsFrames++;
            _fpsElapsed += Time.unscaledDeltaTime;

            if (_fpsElapsed < FPS_SAMPLE_SECONDS) {
                return;
            }

            _measuredFps = _fpsFrames / _fpsElapsed;
            _fpsFrames = 0;
            _fpsElapsed = 0f;
        }

        private void OnGUI() {
            EnsureStyles();
            ApplyScale();

            DrawStatus();
            DrawButtons();
            DrawLog();
            DrawRecovery();

            GUI.matrix = Matrix4x4.identity;
        }

        /// <summary> 화면 전체를 배율만큼 키운다. 고해상도 모니터에서 글씨가 너무 작아지는 것을 막는다. </summary>
        private void ApplyScale() {
            float monitorScale = _followMonitorScale ? WindowDeskAPI.CurrentDpiScale : 1f;
            _appliedScale = Mathf.Max(0.1f, _uiScale * monitorScale);

            GUI.matrix = Matrix4x4.Scale(new Vector3(_appliedScale, _appliedScale, 1f));
        }

        #region 그리기

        private void DrawStatus() {
            Rect area = new Rect(0f, 0f, ScaledWidth, STATUS_HEIGHT);
            DrawPanel(area);

            GUILayout.BeginArea(Inset(area));
            GUILayout.Label(BuildStatusText(), _labelStyle);
            GUILayout.EndArea();
        }

        private void DrawButtons() {
            Rect area = new Rect(0f, STATUS_HEIGHT, BUTTON_PANEL_WIDTH,
                ScaledHeight - STATUS_HEIGHT - RECOVERY_HEIGHT);
            DrawPanel(area);

            GUILayout.BeginArea(Inset(area));
            _buttonScroll = GUILayout.BeginScrollView(_buttonScroll);

            for (int i = 0; i < _entries.Count; i++) {
                Entry entry = _entries[i];

                if (entry.IsCategory) {
                    GUILayout.Label(entry.Label, _categoryStyle, GUILayout.Height(CATEGORY_HEIGHT));
                    continue;
                }

                if (GUILayout.Button(entry.Label, _buttonStyle, GUILayout.Height(BUTTON_HEIGHT))) {
                    Run(entry);
                }
            }

            DrawResolutionList();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary> 불러온 해상도를 버튼으로 늘어놓는다. 누르면 그 해상도가 적용된다. </summary>
        private void DrawResolutionList() {
            IReadOnlyList<DeskResolution> list = _controller.CachedResolutions;

            if (list == null) {
                GUILayout.Label("목록을 불러오십시오", _labelStyle, GUILayout.Height(CATEGORY_HEIGHT));
                return;
            }

            if (list.Count == 0) {
                GUILayout.Label("목록이 비어 있습니다", _labelStyle, GUILayout.Height(CATEGORY_HEIGHT));
                return;
            }

            DeskResolution current = WindowDeskAPI.GetAppliedResolution();

            for (int i = 0; i < list.Count; i++) {
                DeskResolution item = list[i];
                bool isCurrent = item.Width == current.Width && item.Height == current.Height;
                string label = isCurrent ? $"▶ {item}" : $"   {item}";

                if (GUILayout.Button(label, _buttonStyle, GUILayout.Height(BUTTON_HEIGHT))) {
                    ApplySelectedResolution(i);
                }
            }
        }

        /// <summary> 목록 선택도 버튼과 같은 예외 보호를 받게 한다. </summary>
        private void ApplySelectedResolution(int index) {
            Run(new Entry($"해상도 [{index}]", () => _controller.ApplyResolution(index)));
        }

        private void DrawLog() {
            Rect area = new Rect(BUTTON_PANEL_WIDTH, STATUS_HEIGHT,
                ScaledWidth - BUTTON_PANEL_WIDTH, ScaledHeight - STATUS_HEIGHT - RECOVERY_HEIGHT);
            DrawPanel(area);

            FollowNewLines();

            GUILayout.BeginArea(Inset(area));
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            GUILayout.Label(DemoLog.GetText(), _labelStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRecovery() {
            Rect area = new Rect(0f, ScaledHeight - RECOVERY_HEIGHT, ScaledWidth, RECOVERY_HEIGHT);
            DrawPanel(area);

            GUILayout.BeginArea(Inset(area));
            GUILayout.BeginHorizontal();

            for (int i = 0; i < _recoveryEntries.Count; i++) {
                if (GUILayout.Button(_recoveryEntries[i].Label, _recoveryStyle, GUILayout.Height(BUTTON_HEIGHT))) {
                    Run(_recoveryEntries[i]);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary> 새 줄이 들어오면 로그를 맨 아래로 내린다. </summary>
        private void FollowNewLines() {
            if (_lastLogCount == DemoLog.Count) {
                return;
            }

            _lastLogCount = DemoLog.Count;
            _logScroll.y = float.MaxValue;
        }

        /// <summary> 버튼 하나가 터져도 화면이 멈추지 않도록 감싼다. </summary>
        private static void Run(Entry entry) {
            try {
                entry.Action();
            }
            catch (Exception e) {
                DemoLog.Error($"{entry.Label} 실행 중 예외 : {e.Message}");
                Debug.LogException(e);
            }
        }

        private void DrawPanel(Rect area) {
            if (_drawBackground) {
                GUI.Box(area, GUIContent.none, _panelStyle);
            }
        }

        private static Rect Inset(Rect area) {
            return new Rect(area.x + PADDING, area.y + PADDING, area.width - PADDING * 2f, area.height - PADDING * 2f);
        }

        #endregion 그리기

        #region 상태 문자열

        private string BuildStatusText() {
            string line1 = $"초기화 {Mark(WindowDeskAPI.IsInitialized)}  |  프로파일 {WindowDeskAPI.ActiveProfiles}";
            string line2 = $"기능 {WindowDeskAPI.EnabledFeatures}";
            string line3 = $"표시방식 {WindowDeskAPI.CurrentDisplayMode}  |  " +
                           $"최상위 요청 {Mark(WindowDeskAPI.IsTopMostRequested)} / OS {Mark(WindowDeskAPI.IsTopMostApplied())}  " +
                           $"크기변경 {Mark(WindowDeskAPI.IsResizableRequested)}  " +
                           $"마우스가두기 {Mark(WindowDeskAPI.IsCursorConfined)}";
            string line4 = $"현재 해상도 {WindowDeskAPI.GetAppliedResolution()}  |  모니터 {WindowDeskAPI.GetMonitorResolution()}  |  " +
                           $"유니티 Screen {Screen.width}x{Screen.height}  |  " +
                           $"실측 {_measuredFps:0.0}fps  |  기준 {WindowDeskAPI.TargetFrameRate}  " +
                           $"절전 {Mark(WindowDeskAPI.IsPowerSavingEnabled)}  " +
                           $"VSync {Mark(WindowDeskAPI.IsVSyncEnabled)}  포커스 {Mark(WindowDeskAPI.HasFocus)}";

            return $"{line1}\n{line2}\n{line3}\n{line4}";
        }

        private static string Mark(bool value) {
            return value ? "O" : "X";
        }

        #endregion 상태 문자열

        #region 버튼 정의

        private void BuildEntries() {
            AddCategory("초기화");
            Add("PC 게임 초기화", _controller.InitializePcGame);

            AddCategory("표시 방식");
            Add("전체화면", _controller.ApplyFullscreenWindow);
            Add("창 모드", _controller.ApplyWindowed);
            Add("현재 방식 조회", _controller.LogDisplayMode);

            AddCategory("최상위");
            Add("최상위 ON", _controller.TopMostOn);
            Add("최상위 OFF", _controller.TopMostOff);

            AddCategory("창 크기 조절");
            Add("크기 변경 허용", _controller.ResizableOn);
            Add("크기 변경 허용 안함", _controller.ResizableOff);

            AddCategory("마우스");
            Add("창 안에 가두기", _controller.CursorConfineOn);
            Add("가두기 해제", _controller.CursorConfineOff);

            AddCategory("프레임");
            Add("30", () => _controller.ApplyFrameRate(30));
            Add("60", () => _controller.ApplyFrameRate(60));
            Add("120", () => _controller.ApplyFrameRate(120));
            Add("144", () => _controller.ApplyFrameRate(144));
            Add("무제한", () => _controller.ApplyFrameRate(1000));
            Add("절전 토글", _controller.TogglePowerSaving);
            Add("VSync 토글", _controller.ToggleVSync);
            Add("프레임 상태", _controller.LogFrameState);

            AddCategory("설정 저장");
            Add("설정 저장", _controller.SaveSettingsToFile);
            Add("저장 파일 삭제", _controller.DeleteSettingsFile);

            AddCategory("해상도");
            Add("목록 불러오기", _controller.ReloadResolutions);
            Add("현재 해상도", _controller.LogCurrentResolution);
        }

        private void BuildRecoveryEntries() {
                _recoveryEntries.Add(new Entry("창 복구", _controller.RecoverWindow));
                _recoveryEntries.Add(new Entry("로그 지우기", DemoLog.Clear));
                _recoveryEntries.Add(new Entry("게임 종료", _controller.QuitApplication));
        }

        private void AddCategory(string title) {
            _entries.Add(new Entry($"— {title} —", null));
        }

        private void Add(string label, Action action) {
            _entries.Add(new Entry(label, action));
        }

        #endregion 버튼 정의

        #region 스타일

        private void EnsureStyles() {
            if (_panelStyle != null) {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = CreateSolidTexture(COLOR_PANEL) } };

            _labelStyle = new GUIStyle(GUI.skin.label) {
                richText = true,
                fontSize = FONT_SIZE,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };

            _categoryStyle = new GUIStyle(_labelStyle) {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = COLOR_CATEGORY }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = FONT_SIZE };

            _recoveryStyle = new GUIStyle(_buttonStyle) {
                normal = { background = CreateSolidTexture(COLOR_RECOVERY), textColor = Color.white }
            };

            ApplyFont();
        }

        private void ApplyFont() {
            if (_font == null) {
                return;
            }

            _labelStyle.font = _font;
            _categoryStyle.font = _font;
            _buttonStyle.font = _font;
            _recoveryStyle.font = _font;
        }

        private static Texture2D CreateSolidTexture(Color color) {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };

            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }

        #endregion 스타일
    }
}
