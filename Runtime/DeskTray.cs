using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 트레이 아이콘에서 올라온 사용자 입력 </summary>
    internal enum DESK_TRAY_SIGNAL {
        NONE,
        LEFT_CLICK,
        RIGHT_CLICK,
    }

    /// <summary>
    /// 트레이에 쓸 그림의 출처.
    /// 파일 경로이거나 픽셀 배열이며, 둘 다 비어 있으면 실행 파일 아이콘을 쓴다는 뜻입니다.
    /// </summary>
    internal readonly struct DeskTrayIconSource {

        /// <summary> 쓸 .ico 파일 경로 </summary>
        public string FilePath { get; }

        /// <summary> 위에서 아래로 채운 BGRA 픽셀 </summary>
        public byte[] Pixels { get; }

        public int Width { get; }
        public int Height { get; }

        /// <summary> 실행 파일 아이콘을 쓰라는 뜻 </summary>
        public static DeskTrayIconSource Default => default;

        public DeskTrayIconSource(string filePath) {
            FilePath = filePath;
            Pixels = null;
            Width = 0;
            Height = 0;
        }

        public DeskTrayIconSource(byte[] pixels, int width, int height) {
            FilePath = null;
            Pixels = pixels;
            Width = width;
            Height = height;
        }

        /// <summary> 픽셀을 직접 넘겨받았는지 여부 </summary>
        public bool HasPixels => Pixels != null && Width > 0 && Height > 0;
    }

    /// <summary>
    /// 트레이 아이콘의 상태와 메뉴 목록을 들고 있는 관리자.
    /// OS 호출은 전부 DeskTrayNative 가 맡고, 이 클래스는 플랫폼을 모릅니다.
    /// </summary>
    internal static class DeskTray {

        /// <summary> 메뉴 한 칸. 라벨과 눌렀을 때 실행할 동작을 짝지어 둔다. </summary>
        private readonly struct MenuItem {
            public string Label { get; }
            public Action OnClick { get; }

            /// <summary> 라벨 왼쪽에 놓을 그림. 알파를 미리 곱한 BGRA 픽셀 </summary>
            public byte[] Icon { get; }

            public MenuItem(string label, Action onClick, byte[] icon) {
                Label = label;
                OnClick = onClick;
                Icon = icon;
            }

            /// <summary> 고를 수 없는 가로 구분선인지 여부 </summary>
            public bool IsSeparator => Label == null;
        }

        private const int MENU_LIMIT = 32;

        /// <summary> 앱 이름조차 비어 있을 때 쓸 글자 </summary>
        private const string DEFAULT_TOOLTIP = "Game";

        private static readonly List<MenuItem> MENU = new List<MenuItem>();
        private static readonly string[] EMPTY_LABELS = new string[0];

        private static readonly byte[][] EMPTY_ICONS = new byte[0][];

        private static string[] _labelCache = EMPTY_LABELS;
        private static byte[][] _iconCache = EMPTY_ICONS;
        private static int _menuIconSize;
        private static bool _isQuitHooked;

        /// <summary> 트레이 아이콘이 떠 있는지 여부 </summary>
        public static bool IsEnabled { get; private set; }

        /// <summary> 아이콘에 마우스를 올렸을 때 나오는 글자 </summary>
        public static string Tooltip { get; private set; } = string.Empty;

        /// <summary> 등록된 메뉴 항목 수 </summary>
        public static int MenuItemCount => MENU.Count;

        /// <summary> 현재 플랫폼이 트레이를 제공하는지 여부 </summary>
        public static bool IsSupported => DeskTrayNative.IsSupported;

        /// <summary> 지금 걸려 있는 메뉴 테마. 지정하지 않으면 밝게 그린다. </summary>
        public static DESK_MENU_THEME MenuTheme { get; private set; } = DESK_MENU_THEME.LIGHT;

        /// <summary> 메뉴를 밝게 그릴지 어둡게 그릴지 정한다 </summary>
        public static bool SetMenuTheme(DESK_MENU_THEME theme) {
            if (!DeskTrayNative.SetMenuTheme(theme)) {
                return false;
            }

            MenuTheme = theme;
            return true;
        }

        /// <summary> 앱 이름을 툴팁으로, 실행 파일 아이콘을 그림으로 써서 켠다 </summary>
        public static bool Enable() {
            return Enable(null, DeskTrayIconSource.Default);
        }

        /// <summary> 앱 이름을 툴팁으로 쓰고 그림만 지정해서 켠다 </summary>
        /// <param name="icon">쓸 그림. 비어 있으면 실행 파일 아이콘으로 물러난다.</param>
        public static bool Enable(Texture2D icon) {
            return Enable(null, BuildSource(icon));
        }

        /// <summary> 툴팁을 지정해서 켠다 </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        public static bool Enable(string tooltip) {
            return Enable(tooltip, DeskTrayIconSource.Default);
        }

        /// <summary>
        /// 트레이 아이콘을 띄운다. 이미 떠 있으면 툴팁만 갱신한다.
        /// </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        /// <param name="iconFilePath">쓸 .ico 파일 경로. 비우면 실행 파일 아이콘을 쓴다.</param>
        public static bool Enable(string tooltip, string iconFilePath) {
            return Enable(tooltip, new DeskTrayIconSource(iconFilePath));
        }

        /// <summary>
        /// 그림을 아이콘으로 써서 트레이를 켠다.
        /// </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        /// <param name="icon">쓸 그림. 비어 있으면 실행 파일 아이콘으로 물러난다.</param>
        public static bool Enable(string tooltip, Texture2D icon) {
            return Enable(tooltip, BuildSource(icon));
        }

        /// <summary> 툴팁과 메뉴 색을 함께 정해서 켠다 </summary>
        public static bool Enable(string tooltip, DESK_MENU_THEME theme) {
            MenuTheme = theme;
            return Enable(tooltip, DeskTrayIconSource.Default);
        }

        /// <summary> 툴팁 · 그림 · 메뉴 색을 한 번에 정해서 켠다 </summary>
        public static bool Enable(string tooltip, Texture2D icon, DESK_MENU_THEME theme) {
            MenuTheme = theme;
            return Enable(tooltip, BuildSource(icon));
        }

        private static bool Enable(string tooltip, DeskTrayIconSource icon) {
            if (!DeskTrayNative.IsSupported) {
                Debug.Log("[DeskTray] 이 플랫폼은 트레이를 제공하지 않아 요청을 넘깁니다.");
                return false;
            }

            string safeTooltip = ResolveTooltip(tooltip);

            if (IsEnabled) {
                return SetTooltip(safeTooltip);
            }

            if (!DeskTrayNative.Create(safeTooltip, icon)) {
                return false;
            }

            IsEnabled = true;
            Tooltip = safeTooltip;

            DeskTrayNative.SetMenuTheme(MenuTheme);

            HookQuit();
            DeskEventPump.Ensure();
            return true;
        }

        /// <summary>
        /// 쓸 툴팁을 정한다. 지정하지 않으면 프로젝트에 설정된 앱 이름을 쓴다.
        /// 아무 글자도 없으면 아이콘에 마우스를 올렸을 때 빈 상자만 떠서 보기 나쁘다.
        /// </summary>
        private static string ResolveTooltip(string tooltip) {
            if (!string.IsNullOrEmpty(tooltip)) {
                return tooltip;
            }

            return string.IsNullOrEmpty(Application.productName) ? DEFAULT_TOOLTIP : Application.productName;
        }

        /// <summary> 아이콘을 내리고 메뉴를 비운다. 종료 시 자동으로 불린다. </summary>
        public static void Disable() {
            if (!IsEnabled) {
                return;
            }

            DeskTrayNative.Destroy();

            MENU.Clear();
            _labelCache = EMPTY_LABELS;
            IsEnabled = false;
            Tooltip = string.Empty;
        }

        /// <summary> 툴팁 글자를 바꾼다 </summary>
        public static bool SetTooltip(string tooltip) {
            string safeTooltip = tooltip ?? string.Empty;

            if (!IsEnabled) {
                return false;
            }

            if (!DeskTrayNative.SetTooltip(safeTooltip)) {
                return false;
            }

            Tooltip = safeTooltip;
            return true;
        }

        /// <summary> 떠 있는 아이콘의 그림을 바꾼다 </summary>
        public static bool SetIcon(Texture2D icon) {
            return SetIcon(BuildSource(icon));
        }

        /// <summary> 떠 있는 아이콘을 .ico 파일로 바꾼다 </summary>
        public static bool SetIcon(string iconFilePath) {
            return SetIcon(new DeskTrayIconSource(iconFilePath));
        }

        private static bool SetIcon(DeskTrayIconSource icon) {
            if (!IsEnabled) {
                Debug.LogWarning("[DeskTray] 트레이가 켜져 있지 않아 아이콘을 바꿀 수 없습니다.");
                return false;
            }

            return DeskTrayNative.SetIcon(icon);
        }

        /// <summary>
        /// 그림을 아이콘으로 쓸 수 있는 형태로 바꾼다.
        /// 실패하면 빈 출처를 돌려주고, 그러면 실행 파일 아이콘이 쓰인다.
        /// </summary>
        private static DeskTrayIconSource BuildSource(Texture2D icon) {
            if (icon == null) {
                Debug.LogWarning("[DeskTray] 아이콘 그림이 비어 있어 실행 파일 아이콘을 씁니다.");
                return DeskTrayIconSource.Default;
            }

            int size = DeskTrayNative.GetPreferredIconSize();

            if (size <= 0) {
                size = Mathf.Max(1, icon.width);
            }

            try {
                return new DeskTrayIconSource(Capture(icon, size), size, size);
            }
            catch (Exception e) {
                Debug.LogError($"[DeskTray] '{icon.name}' 을 아이콘으로 바꾸지 못했습니다: {e}");
                return DeskTrayIconSource.Default;
            }
        }

        /// <summary>
        /// 트레이가 쓰는 크기로 그림을 다시 그려 픽셀을 읽어 온다.
        ///
        /// 텍스처에서 곧바로 픽셀을 읽으면 임포트 설정의 Read/Write 가 켜져 있어야 한다.
        /// 라이브러리를 쓰는 쪽이 그걸 알아야 하는 상황을 만들지 않으려고, GPU 에 한 번 그린 뒤
        /// 그 결과를 읽는다. 이러면 어떤 임포트 설정이든 그대로 동작한다.
        /// 줄이는 일도 GPU 가 대신 해 주므로 따로 계산하지 않는다.
        /// </summary>
        /// <returns>위에서 아래로 채운 BGRA 픽셀.</returns>
        private static byte[] Capture(Texture2D icon, int size, bool premultiply = false) {
            RenderTexture previous = RenderTexture.active;
            RenderTexture buffer = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32,
                                                              RenderTextureReadWrite.sRGB);
            Texture2D readable = null;

            try {
                Graphics.Blit(icon, buffer);
                RenderTexture.active = buffer;

                readable = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                    hideFlags = HideFlags.HideAndDontSave
                };

                readable.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                readable.Apply();

                return ToBgra(readable.GetPixels32(), size, premultiply);
            }
            finally {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(buffer);
                DestroyTemporary(readable);
            }
        }

        /// <summary>
        /// 유니티 픽셀을 윈도우 아이콘이 쓰는 차례로 바꾼다.
        /// 색은 RGBA 가 아니라 BGRA 순서이고, 줄은 아래가 아니라 위에서부터 쌓인다.
        /// </summary>
        private static byte[] ToBgra(Color32[] pixels, int size, bool premultiply) {
            byte[] result = new byte[size * size * 4];

            for (int y = 0; y < size; y++) {
                int sourceRow = (size - 1 - y) * size;
                int targetRow = y * size * 4;

                for (int x = 0; x < size; x++) {
                    Color32 pixel = pixels[sourceRow + x];
                    int index = targetRow + x * 4;

                    if (premultiply) {
                        result[index] = (byte)(pixel.b * pixel.a / 255);
                        result[index + 1] = (byte)(pixel.g * pixel.a / 255);
                        result[index + 2] = (byte)(pixel.r * pixel.a / 255);
                    }
                    else {
                        result[index] = pixel.b;
                        result[index + 1] = pixel.g;
                        result[index + 2] = pixel.r;
                    }

                    result[index + 3] = pixel.a;
                }
            }

            return result;
        }

        /// <summary> 잠깐 쓰고 버리는 텍스처를 지운다. 편집 중에는 Destroy 가 통하지 않는다. </summary>
        private static void DestroyTemporary(Texture2D texture) {
            if (texture == null) {
                return;
            }

            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(texture);
                return;
            }

            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary> 우클릭 메뉴에 항목을 하나 더한다. 등록 순서대로 위에서 아래로 놓인다. </summary>
        /// <param name="label">메뉴에 보일 글자.</param>
        /// <param name="onClick">항목을 눌렀을 때 실행할 동작.</param>
        public static bool AddMenuItem(string label, Action onClick) {
            return AddMenuItem(label, onClick, null);
        }

        /// <summary> 라벨 왼쪽에 그림이 붙은 메뉴 항목을 더한다 </summary>
        /// <param name="label">메뉴에 보일 글자.</param>
        /// <param name="onClick">항목을 눌렀을 때 실행할 동작.</param>
        /// <param name="icon">라벨 왼쪽에 놓을 그림. 없으면 글자만 나온다.</param>
        public static bool AddMenuItem(string label, Action onClick, Texture2D icon) {
            if (string.IsNullOrEmpty(label)) {
                Debug.LogWarning("[DeskTray] 메뉴 라벨이 비어 있어 등록하지 않습니다.");
                return false;
            }

            if (onClick == null) {
                Debug.LogWarning($"[DeskTray] 메뉴 '{label}' 의 동작이 없어 등록하지 않습니다.");
                return false;
            }

            if (MENU.Count >= MENU_LIMIT) {
                Debug.LogWarning($"[DeskTray] 메뉴 항목은 {MENU_LIMIT} 개까지입니다. '{label}' 을 건너뜁니다.");
                return false;
            }

            MENU.Add(new MenuItem(label, onClick, BuildMenuIcon(icon)));
            RebuildCaches();
            return true;
        }

        /// <summary>
        /// 메뉴에 가로 구분선을 넣는다. 앞선 항목들과 뒤따르는 항목들을 갈라 놓는다.
        /// 맨 앞이나 구분선 바로 뒤에 넣으면 줄만 덩그러니 남으므로 무시한다.
        /// </summary>
        public static bool AddSeparator() {
            if (MENU.Count == 0 || MENU[MENU.Count - 1].IsSeparator) {
                return false;
            }

            if (MENU.Count >= MENU_LIMIT) {
                Debug.LogWarning($"[DeskTray] 메뉴 항목은 {MENU_LIMIT} 개까지입니다. 구분선을 건너뜁니다.");
                return false;
            }

            MENU.Add(new MenuItem(null, null, null));
            RebuildCaches();
            return true;
        }

        /// <summary> 등록된 트레이 메뉴를 전부 지웁니다. 아이콘은 그대로 남습니다. </summary>
        public static void ClearMenu() {
            MENU.Clear();
            _labelCache = EMPTY_LABELS;
            _iconCache = EMPTY_ICONS;
        }

        /// <summary>
        /// 메뉴에 붙일 그림을 픽셀로 바꾼다.
        /// 메뉴 비트맵은 알파를 색에 미리 곱해 두어야 한다. 안 그러면 반투명한 가장자리가 검게 뜬다.
        /// </summary>
        private static byte[] BuildMenuIcon(Texture2D icon) {
            if (icon == null) {
                return null;
            }

            if (_menuIconSize <= 0) {
                _menuIconSize = DeskTrayNative.GetPreferredIconSize();
            }

            if (_menuIconSize <= 0) {
                return null;
            }

            try {
                return Capture(icon, _menuIconSize, true);
            }
            catch (Exception e) {
                Debug.LogError($"[DeskTray] 메뉴 그림 '{icon.name}' 을 바꾸지 못했습니다: {e}");
                return null;
            }
        }

        /// <summary>
        /// 트레이에서 올라온 입력을 처리한다. DeskEventPump 가 매 프레임 부른다.
        /// WndProc 안에서 유니티 코드를 부르면 네이티브 경계를 넘게 되므로 여기까지 미룬다.
        /// </summary>
        internal static void Tick() {
            if (!IsEnabled) {
                return;
            }

            DESK_TRAY_SIGNAL signal = DeskTrayNative.TakeSignal();

            if (signal == DESK_TRAY_SIGNAL.NONE) {
                return;
            }

            if (signal == DESK_TRAY_SIGNAL.LEFT_CLICK) {
                WindowDeskAPI.FocusGameWindow();
                return;
            }

            ShowMenu();
        }

        /// <summary> 우클릭 메뉴를 띄우고 고른 항목을 실행한다 </summary>
        private static void ShowMenu() {
            if (MENU.Count == 0) {
                return;
            }

            int index = DeskTrayNative.ShowMenu(_labelCache, _iconCache, _menuIconSize);

            if (index < 0 || index >= MENU.Count) {
                return;
            }

            MenuItem item = MENU[index];

            if (item.IsSeparator) {
                return;
            }

            try {
                item.OnClick();
            }
            catch (Exception e) {
                Debug.LogError($"[DeskTray] 메뉴 '{item.Label}' 처리 중 예외가 발생했습니다: {e}");
            }
        }

        /// <summary> 네이티브에 넘길 라벨 배열을 미리 만들어 둔다. 메뉴를 열 때마다 만들지 않기 위해서다. </summary>
        private static void RebuildCaches() {
            _labelCache = new string[MENU.Count];
            _iconCache = new byte[MENU.Count][];

            for (int i = 0; i < MENU.Count; i++) {
                _labelCache[i] = MENU[i].Label;
                _iconCache[i] = MENU[i].Icon;
            }
        }

        /// <summary>
        /// 종료 시 아이콘을 지우도록 걸어 둔다.
        /// 명시적으로 지우지 않으면 게임이 꺼져도 트레이에 죽은 아이콘이 남는다.
        /// </summary>
        private static void HookQuit() {
            if (_isQuitHooked) {
                return;
            }

            _isQuitHooked = true;
            Application.quitting += Disable;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 도메인이 갈아엎이기 전에 아이콘과 창을 먼저 정리한다.
        /// WndProc 이 사라진 코드를 가리키는 채로 남으면 에디터가 통째로 죽는다.
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditorCleanup() {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Disable;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state) {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) {
                Disable();
            }
        }
#endif
    }
}
