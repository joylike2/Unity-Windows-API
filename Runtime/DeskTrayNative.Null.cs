#if !(UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR))

using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 윈도우가 아닌 플랫폼용 트레이 구현.
    /// 호출은 전부 받아주되 아무 일도 하지 않아, 게임 코드에 플랫폼 분기가 생기지 않도록 합니다.
    /// </summary>
    internal static class DeskTrayNative {

        private static bool _hasWarned;

        /// <summary> 이 플랫폼이 트레이를 제공하는지 여부 </summary>
        internal static bool IsSupported => false;

        /// <summary> 아무것도 하지 않고 실패를 알린다 </summary>
        internal static bool Create(string tooltip, DeskTrayIconSource icon) {
            WarnOnce();
            return false;
        }

        /// <summary> 바꿀 아이콘이 없다 </summary>
        internal static bool SetIcon(DeskTrayIconSource icon) {
            return false;
        }

        /// <summary> 바꿀 메뉴가 없다 </summary>
        internal static bool SetMenuTheme(DESK_MENU_THEME theme) {
            return false;
        }

        /// <summary> 트레이가 없으므로 알맞은 크기도 없다 </summary>
        internal static int GetPreferredIconSize() {
            return 0;
        }

        /// <summary> 되돌릴 것이 없다 </summary>
        internal static void Destroy() {
        }

        /// <summary> 아무것도 하지 않고 실패를 알린다 </summary>
        internal static bool SetTooltip(string tooltip) {
            return false;
        }

        /// <summary> 올라올 입력이 없다 </summary>
        internal static DESK_TRAY_SIGNAL TakeSignal() {
            return DESK_TRAY_SIGNAL.NONE;
        }

        /// <summary> 띄울 메뉴가 없다 </summary>
        internal static int ShowMenu(string[] labels, byte[][] icons, int iconSize) {
            return -1;
        }

        /// <summary> 안내는 한 번만 남긴다. 매번 남기면 로그가 쏟아진다. </summary>
        private static void WarnOnce() {
            if (_hasWarned) {
                return;
            }

            _hasWarned = true;
            Debug.Log("[DeskTrayNative] 트레이는 Windows 에서만 동작합니다. 이 플랫폼에서는 무시됩니다.");
        }
    }
}

#endif
