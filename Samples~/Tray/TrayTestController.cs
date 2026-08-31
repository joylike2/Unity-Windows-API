using UnityEngine;

namespace LifeLogs.WindowUtil.Samples {

    /// <summary>
    /// 트레이 기능만 확인하는 데모.
    /// 해상도·표시 방식은 다루지 않으므로 <see cref="WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE)"/> 를 부르지 않습니다.
    /// </summary>
    public sealed class TrayTestController : MonoBehaviour {

        private const string TOOLTIP_PREFIX = "DeskWindows 트레이 데모";

        [Header("켜 두면 씬이 시작하자마자 트레이가 등록된다")]
        [SerializeField] private bool _enableOnStart;

        [Header("트레이 아이콘으로 쓸 그림. 비워 두면 실행 파일 아이콘이 쓰인다")]
        [SerializeField] private Texture2D _icon;

        private int _tooltipCounter;

        private void Start() {
            DemoLog.Section("트레이 데모");

            if (!WindowDeskAPI.IsTraySupported) {
                DemoLog.Warn("이 플랫폼은 트레이를 제공하지 않습니다. Windows 빌드에서 확인하십시오.");
                return;
            }

            DemoLog.Info("인스펙터 Icon 칸에 그림을 넣고 [트레이 켜기] 를 누르십시오.");
            DemoLog.Info("좌클릭 = 게임 창 앞으로, 우클릭 = 메뉴");

            if (_enableOnStart) {
                EnableTray();
            }
        }

        #region 아이콘

        /// <summary> 인스펙터에 넣은 그림으로 트레이를 띄운다. 툴팁과 메뉴 색은 기본값을 쓴다 </summary>
        public void EnableTray() {
            DemoLog.Section("트레이 켜기");

            bool result = WindowDeskAPI.EnableTray(_icon);
            DemoLog.Result($"EnableTray → {result} / 켜짐 {WindowDeskAPI.IsTrayEnabled}", result);

            if (!result) {
                return;
            }

            DemoLog.Info($"툴팁은 앱 이름 '{WindowDeskAPI.TrayTooltip}', 메뉴 색은 {WindowDeskAPI.TrayMenuTheme} 입니다.");
            DemoLog.Info("메뉴는 아직 없습니다. [기본 메뉴 등록] 을 눌러야 우클릭 메뉴가 뜹니다.");
        }

        /// <summary> 트레이 아이콘을 내린다. 등록된 메뉴도 함께 사라진다 </summary>
        public void DisableTray() {
            DemoLog.Section("트레이 끄기");

            WindowDeskAPI.DisableTray();
            DemoLog.Result($"켜짐 {WindowDeskAPI.IsTrayEnabled} / 메뉴 {WindowDeskAPI.TrayMenuItemCount}개",
                           !WindowDeskAPI.IsTrayEnabled);
        }

        /// <summary> 떠 있는 아이콘의 그림을 다시 넣는다. 실행 중 교체가 되는지 본다 </summary>
        public void ApplyImageIcon() {
            DemoLog.Section("아이콘 그림 다시 적용");

            if (!HasIcon()) {
                return;
            }

            bool result = WindowDeskAPI.SetTrayIcon(_icon);
            DemoLog.Result($"SetTrayIcon({_icon.name}) → {result}", result);
        }

        /// <summary> 툴팁은 아이콘에 마우스를 올렸을 때 뜨는 글자다. 바뀌는지 번호를 붙여 본다 </summary>
        public void ChangeTooltip() {
            _tooltipCounter++;

            string tooltip = $"{TOOLTIP_PREFIX} ({_tooltipCounter})";
            bool result = WindowDeskAPI.SetTrayTooltip(tooltip);

            DemoLog.Result($"툴팁 → {tooltip}", result);
        }

        private bool HasIcon() {
            if (_icon != null) {
                return true;
            }

            DemoLog.Warn("인스펙터의 Icon 칸이 비어 있습니다. 쓸 그림을 넣으십시오.");
            return false;
        }

        #endregion 아이콘

        #region 메뉴

        /// <summary> 우클릭 메뉴를 구성한다. 등록한 순서대로 위에서 아래로 놓인다 </summary>
        public void AddDefaultMenu() {
            DemoLog.Section("메뉴 등록");

            WindowDeskAPI.ClearTrayMenu();

            // 첫 칸만 그림을 붙여 둔다. 붙은 칸과 안 붙은 칸이 같이 보여야 비교가 된다.
            WindowDeskAPI.AddTrayMenuItem("게임 창 열기", OnMenuOpen, _icon);
            WindowDeskAPI.AddTrayMenuItem("로그 지우기", OnMenuClearLog);
            WindowDeskAPI.AddTrayMenuSeparator();
            WindowDeskAPI.AddTrayMenuItem("종료", OnMenuQuit);

            DemoLog.Result($"항목 3개 + 구분선 1개 = {WindowDeskAPI.TrayMenuItemCount}칸 등록",
                           WindowDeskAPI.TrayMenuItemCount == 4);
        }

        /// <summary> 메뉴를 비운다. 항목이 없으면 우클릭해도 아무것도 뜨지 않는다 </summary>
        public void ClearMenu() {
            DemoLog.Section("메뉴 비우기");

            WindowDeskAPI.ClearTrayMenu();
            DemoLog.Result($"메뉴 {WindowDeskAPI.TrayMenuItemCount}개", WindowDeskAPI.TrayMenuItemCount == 0);
        }

        private void OnMenuOpen() {
            DemoLog.Result("메뉴: 게임 창 열기", WindowDeskAPI.FocusGameWindow());
        }

        private void OnMenuClearLog() {
            DemoLog.Clear();
            DemoLog.Info("메뉴: 로그를 지웠습니다.");
        }

        private void OnMenuQuit() {
            DemoLog.Info("메뉴: 종료합니다. 아이콘이 자동으로 사라져야 정상입니다.");
            Application.Quit();
        }

        /// <summary> 메뉴를 밝게 그린다. 지정하지 않았을 때의 기본값이다 </summary>
        public void UseLightMenuTheme() {
            ApplyMenuTheme(DESK_MENU_THEME.LIGHT);
        }

        /// <summary> 메뉴를 어둡게 그린다 </summary>
        public void UseDarkMenuTheme() {
            ApplyMenuTheme(DESK_MENU_THEME.DARK);
        }

        /// <summary> 메뉴 색을 윈도우 설정에 맡긴다 </summary>
        public void UseSystemMenuTheme() {
            ApplyMenuTheme(DESK_MENU_THEME.SYSTEM);
        }

        private void ApplyMenuTheme(DESK_MENU_THEME theme) {
            DemoLog.Section($"메뉴 테마 {theme}");

            bool result = WindowDeskAPI.SetTrayMenuTheme(theme);
            DemoLog.Result($"SetTrayMenuTheme({theme}) → {result} / 지금 {WindowDeskAPI.TrayMenuTheme}", result);

            if (result) {
                DemoLog.Info("아이콘을 우클릭해서 색이 바뀌었는지 보십시오.");
            }
        }

        #endregion 메뉴

        #region 그 밖

        /// <summary> 현재 상태를 한 번에 남긴다 </summary>
        public void LogState() {
            DemoLog.Section("트레이 상태");
            DemoLog.Info($"플랫폼 지원 {WindowDeskAPI.IsTraySupported}");
            DemoLog.Info($"켜짐 {WindowDeskAPI.IsTrayEnabled}");
            DemoLog.Info($"메뉴 항목 {WindowDeskAPI.TrayMenuItemCount}개");
        }

        #endregion 그 밖
    }
}
