using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil.Samples {

    /// <summary> 트레이 데모 화면. Canvas 도 EventSystem 도 쓰지 않고 IMGUI 로만 그린다. </summary>
    [RequireComponent(typeof(TrayTestController))]
    public sealed class TrayDemoGUI : MonoBehaviour {

        private const int STATUS_HEIGHT = 52;
        private const int BUTTON_PANEL_WIDTH = 220;
        private const int BUTTON_HEIGHT = 26;
        private const int CATEGORY_HEIGHT = 24;
        private const int PADDING = 6;
        private const int FONT_SIZE = 13;

        private static readonly Color COLOR_PANEL = new Color(0.10f, 0.10f, 0.13f, 0.96f);
        private static readonly Color COLOR_CATEGORY = new Color(0.95f, 0.70f, 0.30f, 1f);

        /// <summary> 버튼 한 칸. 동작이 없으면 카테고리 제목이다. </summary>
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

        [Header("화면 배율")]
        [SerializeField, Range(0.5f, 4f)] private float _uiScale = 1.5f;

        private readonly List<Entry> _entries = new List<Entry>();

        private TrayTestController _controller;
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _logScroll;
        private int _lastLogCount;
        private float _appliedScale = 1f;

        private float ScaledWidth => Screen.width / _appliedScale;
        private float ScaledHeight => Screen.height / _appliedScale;

        private void Awake() {
            _controller = GetComponent<TrayTestController>();
            BuildEntries();
        }

        private void OnGUI() {
            EnsureStyles();

            _appliedScale = Mathf.Max(0.1f, _uiScale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_appliedScale, _appliedScale, 1f));

            DrawStatus();
            DrawButtons();
            DrawLog();

            GUI.matrix = Matrix4x4.identity;
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
            Rect area = new Rect(0f, STATUS_HEIGHT, BUTTON_PANEL_WIDTH, ScaledHeight - STATUS_HEIGHT);
            DrawPanel(area);

            GUILayout.BeginArea(Inset(area));

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

            GUILayout.EndArea();
        }

        private void DrawLog() {
            Rect area = new Rect(BUTTON_PANEL_WIDTH, STATUS_HEIGHT,
                ScaledWidth - BUTTON_PANEL_WIDTH, ScaledHeight - STATUS_HEIGHT);
            DrawPanel(area);

            FollowNewLines();

            GUILayout.BeginArea(Inset(area));
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            GUILayout.Label(DemoLog.GetText(), _labelStyle);
            GUILayout.EndScrollView();
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
            GUI.Box(area, GUIContent.none, _panelStyle);
        }

        private static Rect Inset(Rect area) {
            return new Rect(area.x + PADDING, area.y + PADDING, area.width - PADDING * 2f, area.height - PADDING * 2f);
        }

        #endregion 그리기

        #region 상태 문자열

        private static string BuildStatusText() {
            string line1 = $"플랫폼 지원 {Mark(WindowDeskAPI.IsTraySupported)}  |  " +
                           $"아이콘 {Mark(WindowDeskAPI.IsTrayEnabled)}  |  " +
                           $"메뉴 {WindowDeskAPI.TrayMenuItemCount}개";
            string line2 = $"메뉴 색 {WindowDeskAPI.TrayMenuTheme}  |  " +
                           "아이콘 좌클릭 = 게임 창 앞으로, 우클릭 = 메뉴";

            return $"{line1}\n{line2}";
        }

        private static string Mark(bool value) {
            return value ? "O" : "X";
        }

        #endregion 상태 문자열

        #region 버튼 정의

        private void BuildEntries() {
            AddCategory("트레이");
            Add("트레이 켜기", _controller.EnableTray);
            Add("트레이 끄기", _controller.DisableTray);
            Add("그림 다시 적용", _controller.ApplyImageIcon);
            Add("툴팁 바꾸기", _controller.ChangeTooltip);

            AddCategory("메뉴");
            Add("기본 메뉴 등록", _controller.AddDefaultMenu);
            Add("메뉴 비우기", _controller.ClearMenu);

            AddCategory("메뉴 색");
            Add("밝게", _controller.UseLightMenuTheme);
            Add("어둡게", _controller.UseDarkMenuTheme);
            Add("시스템 설정", _controller.UseSystemMenuTheme);

            AddCategory("그 밖");
            Add("상태 보기", _controller.LogState);
            Add("로그 지우기", DemoLog.Clear);
            Add("게임 종료", Application.Quit);
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

            ApplyFont();
        }

        private void ApplyFont() {
            if (_font == null) {
                return;
            }

            _labelStyle.font = _font;
            _categoryStyle.font = _font;
            _buttonStyle.font = _font;
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
