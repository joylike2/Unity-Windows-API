using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 데스크탑 창 제어 브릿지.
    /// 게임과 설정 UI 는 이 클래스만 호출하고, 어느 OS 인지 알 필요가 없습니다.
    /// 실제 구현은 빌드 타임에 하나만 컴파일되는 DeskPlatform 이 담당합니다.
    /// 선언하지 않은 기능은 호출해도 실행되지 않고 안내 로그만 남습니다.
    /// </summary>
    public static class WindowDeskAPI {

        #region 기능 선언

        private const DESK_WINDOW_FEATURE PC_GAME_FEATURES =
            DESK_WINDOW_FEATURE.MONITOR_INFO | DESK_WINDOW_FEATURE.RESOLUTION_INFO
            | DESK_WINDOW_FEATURE.DISPLAY_MODE | DESK_WINDOW_FEATURE.TOP_MOST
            | DESK_WINDOW_FEATURE.WINDOW_PLACEMENT | DESK_WINDOW_FEATURE.CURSOR_CONFINE;

        /// <summary> 바탕화면 게임. 테두리없는 창 위에 투명과 클릭 통과를 함께 건다 </summary>
        private const DESK_WINDOW_FEATURE DESKTOP_GAME_FEATURES =
            DESK_WINDOW_FEATURE.BORDERLESS | DESK_WINDOW_FEATURE.TRANSPARENT
            | DESK_WINDOW_FEATURE.CLICK_THROUGH | DESK_WINDOW_FEATURE.DISPLAY_MODE
            | DESK_WINDOW_FEATURE.WINDOW_PLACEMENT | DESK_WINDOW_FEATURE.MONITOR_INFO
            | DESK_WINDOW_FEATURE.TOP_MOST;

        /// <summary>
        /// 바탕화면 게임이 일부러 쓰지 않는 기능. 다른 프로파일과 함께 선언해도 여기 있는 것은 꺼진다.
        ///
        /// CURSOR_CONFINE : 마우스 포커스가 창 밖으로 나갈 수 있어야 한다.
        /// 창이 화면 전체를 덮으므로 커서를 가두면 다른 창도 바탕화면도 쓸 수 없다.
        ///
        /// RESOLUTION_INFO : 창을 모니터 전체로 펼치므로 고를 해상도가 없다.
        /// </summary>
        private const DESK_WINDOW_FEATURE DESKTOP_GAME_BLOCKED_FEATURES =
            DESK_WINDOW_FEATURE.CURSOR_CONFINE | DESK_WINDOW_FEATURE.RESOLUTION_INFO;

        private static DESK_WINDOW_FEATURE _warnedFeatures = DESK_WINDOW_FEATURE.NONE;
        private static DeskWindowStyleSnapshot _originalStyle;
        private static bool _isQuitHooked;

        /// <summary>
        /// 선언되었고 현재 플랫폼에서 실제로 사용할 수 있는 기능.
        /// </summary>
        public static DESK_WINDOW_FEATURE EnabledFeatures { get; private set; } = DESK_WINDOW_FEATURE.NONE;

        /// <summary> 선언된 사용 목적. 선언 전에는 NONE </summary>
        public static DESK_WINDOW_PROFILE ActiveProfiles { get; private set; } = DESK_WINDOW_PROFILE.NONE;

        /// <summary> 초기화를 마쳤는지 여부 </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary> 게임이 요청한 최상위 값. 전체화면 동안에는 실제 창 상태와 다를 수 있습니다. </summary>
        public static bool IsTopMostRequested { get; private set; }

        /// <summary>
        /// 저장 파일이 없을 때 쓸 창 크기 조절 기본값. 드래그로 크기를 바꿀 수 있게 시작합니다.
        /// PlayerSettings 값을 그대로 이어받으면 프로젝트마다 첫 실행이 달라지므로 여기서 못 박습니다.
        /// </summary>
        public const bool DEFAULT_RESIZABLE = true;

        /// <summary> 게임이 요청한 창 크기 조절 값. 유니티가 창을 다시 만들어도 이 값이 기준입니다. </summary>
        public static bool IsResizableRequested { get; private set; } = DEFAULT_RESIZABLE;

        /// <summary> 마우스를 게임 창 안에 가두도록 요청했는지 여부. </summary>
        public static bool IsCursorConfined { get; private set; }

        /// <summary>
        /// 해당 기능이 사용 가능한 상태인지 조회합니다.
        /// </summary>
        public static bool IsFeatureEnabled(DESK_WINDOW_FEATURE feature) {
            return (EnabledFeatures & feature) == feature;
        }

        /// <summary>
        /// 사용할 기능을 선언하고 창 핸들을 확보합니다. 창 상태는 바꾸지 않습니다.
        /// 현재 플랫폼이 제공하지 않는 기능은 선언에서 걸러내고 그 자리에서 한 번 경고합니다.
        /// 나중에 조용히 아무 일도 일어나지 않는 상황을 없애기 위해서입니다.
        /// </summary>
        /// <param name="features">사용할 기능. 전부 쓰려면 <see cref="DESK_WINDOW_FEATURE.ALL"/>.</param>
        /// <returns>선언한 기능이 전부 사용 가능하고 창 핸들도 확보했으면 true.</returns>
        public static bool Initialize(DESK_WINDOW_FEATURE features) {
            return InitializeCore(DESK_WINDOW_PROFILE.NONE, features);
        }

        /// <summary>
        /// 사용 목적을 선언하고 창 핸들을 확보합니다. 필요한 기능이 한 번에 켜집니다.
        /// </summary>
        /// <param name="profiles">사용할 프로파일. 둘 다 쓰려면 비트 OR 로 넘깁니다.</param>
        /// <returns>선언한 기능이 전부 사용 가능하고 창 핸들도 확보했으면 true.</returns>
        public static bool Initialize(DESK_WINDOW_PROFILE profiles) {
            return InitializeCore(profiles, ResolveProfileFeatures(profiles));
        }

        /// <summary>
        /// 사용 목적에 기능을 더해 선언합니다. 트레이나 작업표시줄처럼 프로파일에 없는 기능을 쓸 때 씁니다.
        /// </summary>
        /// <param name="profiles">사용할 프로파일. 둘 다 쓰려면 비트 OR 로 넘깁니다.</param>
        /// <param name="extraFeatures">프로파일에 더할 기능.</param>
        public static bool Initialize(DESK_WINDOW_PROFILE profiles, DESK_WINDOW_FEATURE extraFeatures) {
            return InitializeCore(profiles, ResolveProfileFeatures(profiles) | extraFeatures);
        }

        /// <summary> 모든 오버로드가 모이는 실제 초기화 경로. </summary>
        private static bool InitializeCore(DESK_WINDOW_PROFILE profiles, DESK_WINDOW_FEATURE features) {
            // 창을 제어할 수 없는 환경(에디터 · 미지원 플랫폼)에서는 아무 상태도 만들지 않는다.
            // 절반만 켜 두면 조회는 되는데 창은 안 바뀌어, 무엇이 되는지 판단하기 어려워진다.
            if (!DeskPlatform.IsWindowControlEnabled) {
                Debug.LogWarning($"[WindowDeskAPI] 이 환경에서는 창을 제어할 수 없어 초기화를 건너뜁니다. "
                                 + $"({profiles} / {features}) "
                                 + "에디터에서는 에디터 창 자체가 변형되는 것을 막기 위해 일부러 막아 두었습니다. "
                                 + "빌드에서 확인하십시오.");
                return false;
            }

            DESK_WINDOW_FEATURE unsupported = features & ~DeskPlatform.SupportedFeatures;

            ActiveProfiles = profiles;
            EnabledFeatures = features & DeskPlatform.SupportedFeatures;
            _warnedFeatures = DESK_WINDOW_FEATURE.NONE;
            IsInitialized = true;

            if (unsupported != DESK_WINDOW_FEATURE.NONE) {
                Debug.LogWarning($"[WindowDeskAPI] 이 플랫폼은 {unsupported} 를 제공하지 않아 선언에서 제외했습니다.");
            }

            bool hasHandle = Initialize();

            CaptureOriginalStyle();
            RegisterServices();

            CaptureBaseResolution();
            StartDisplayWatch();
            DeskFrameRate.Setup();
            HookApplicationQuit();

            if (DISPLAY_LISTENERS.Count > 0) {
                StartListeningDisplay();
            }

            LoadSettingsFile();
            ApplyDesktopGameDefaults();
            NotifyAllInitialized();

            return hasHandle && unsupported == DESK_WINDOW_FEATURE.NONE;
        }

        #region 초기화 알림

        private static readonly List<IDeskInitializeListener> INITIALIZE_LISTENERS =
            new List<IDeskInitializeListener>();

        /// <summary>
        /// 초기화 완료 알림을 받을 대상을 등록합니다.
        /// 이미 초기화가 끝나 있으면 등록 즉시 한 번 불리므로 실행 순서를 맞출 필요가 없습니다.
        /// </summary>
        /// <param name="listener">알림을 받을 대상.</param>
        public static void AddInitializeListener(IDeskInitializeListener listener) {
            if (listener == null || INITIALIZE_LISTENERS.Contains(listener)) {
                return;
            }

            INITIALIZE_LISTENERS.Add(listener);

            if (IsInitialized) {
                NotifyInitialized(listener);
            }
        }

        /// <summary> 등록을 해제합니다. 오브젝트가 사라지기 전에 반드시 호출하십시오. </summary>
        /// <param name="listener">해제할 대상.</param>
        public static void RemoveInitializeListener(IDeskInitializeListener listener) {
            INITIALIZE_LISTENERS.Remove(listener);
        }

        /// <summary> 하나가 터져도 나머지가 알림을 받도록 각각 감싼다. </summary>
        private static void NotifyAllInitialized() {
            for (int i = INITIALIZE_LISTENERS.Count - 1; i >= 0; i--) {
                NotifyInitialized(INITIALIZE_LISTENERS[i]);
            }
        }

        private static void NotifyInitialized(IDeskInitializeListener listener) {
            try {
                listener.OnDeskInitialized(ActiveProfiles);
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskAPI] 초기화 알림을 처리하다 예외가 발생했습니다: {e}");
            }
        }

        #endregion 초기화 알림

        #region 알림 수신

        private static readonly List<IDeskDisplayListener> DISPLAY_LISTENERS = new List<IDeskDisplayListener>();

        private static bool _isListeningDisplay;

        /// <summary>
        /// 알림을 받을 대상을 등록합니다. 모니터 · 해상도 · 표시 방식 · 배율 · 포커스를 한 번에 받습니다.
        /// 같은 대상을 두 번 넣어도 한 번만 등록됩니다.
        /// </summary>
        /// <param name="listener">알림을 받을 대상.</param>
        public static void AddDisplayListener(IDeskDisplayListener listener) {
            if (listener == null || DISPLAY_LISTENERS.Contains(listener)) {
                return;
            }

            DISPLAY_LISTENERS.Add(listener);
            StartListeningDisplay();
        }

        /// <summary>
        /// 등록을 해제합니다. 오브젝트가 사라지기 전에 반드시 호출하십시오.
        /// </summary>
        /// <param name="listener">해제할 대상.</param>
        public static void RemoveDisplayListener(IDeskDisplayListener listener) {
            DISPLAY_LISTENERS.Remove(listener);
        }

        /// <summary> 등록 대상이 하나라도 생겼을 때만 이벤트를 붙인다. </summary>
        private static void StartListeningDisplay() {
            if (_isListeningDisplay) {
                return;
            }

            _isListeningDisplay = true;

            DeskEvents.CurrentMonitorChanged += NotifyCurrentMonitorChanged;
            DeskEvents.ResolutionChanged += NotifyResolutionChanged;
            DeskEvents.DisplayModeChanged += NotifyDisplayModeChanged;
            DeskEvents.DisplayConfigurationChanged += NotifyDisplayConfigurationChanged;
            DeskEvents.CurrentMonitorLost += NotifyCurrentMonitorLost;
            DeskEvents.DpiScaleChanged += NotifyDpiScaleChanged;
            DeskEvents.WindowFocusChanged += NotifyWindowFocusChanged;
        }

        private static void NotifyCurrentMonitorChanged(int monitorIndex) {
            Notify(listener => listener.OnCurrentMonitorChanged(monitorIndex), "모니터 이동");
        }

        private static void NotifyResolutionChanged(DeskResolution resolution) {
            Notify(listener => listener.OnResolutionChanged(resolution), "해상도 변경");
        }

        private static void NotifyDisplayModeChanged(DESK_DISPLAY_MODE mode) {
            Notify(listener => listener.OnDisplayModeChanged(mode), "표시 방식 변경");
        }

        private static void NotifyDisplayConfigurationChanged(DeskMonitorLayout layout) {
            Notify(listener => listener.OnDisplayConfigurationChanged(layout), "모니터 구성 변경");
        }

        private static void NotifyCurrentMonitorLost(int lostIndex) {
            Notify(listener => listener.OnCurrentMonitorLost(lostIndex), "모니터 상실");
        }

        private static void NotifyDpiScaleChanged(float scaleRatio) {
            Notify(listener => listener.OnDpiScaleChanged(scaleRatio), "배율 변경");
        }

        private static void NotifyWindowFocusChanged(bool hasFocus) {
            Notify(listener => listener.OnWindowFocusChanged(hasFocus), "포커스 변경");
        }

        /// <summary> 하나가 터져도 나머지가 알림을 받도록 각각 감싼다. </summary>
        private static void Notify(Action<IDeskDisplayListener> action, string label) {
            for (int i = DISPLAY_LISTENERS.Count - 1; i >= 0; i--) {
                try {
                    action(DISPLAY_LISTENERS[i]);
                }
                catch (Exception e) {
                    Debug.LogError($"[WindowDeskAPI] {label} 알림 처리 중 예외: {e}");
                }
            }
        }

        #endregion 알림 수신

        #region 설정 파일

        private const string PC_GAME_SETTINGS_FILE_NAME = "GameWindowSettings.bin";
        private const string DESKTOP_GAME_SETTINGS_FILE_NAME = "DesktopGameSettings.bin";

        /// <summary>
        /// 설정 파일의 전체 경로. 프로파일마다 저장 항목이 달라 파일을 나눈다.
        /// 한 파일을 공유하면 다른 프로파일로 켰을 때 안 쓰는 항목이 덮여 사라진다.
        /// </summary>
        public static string SettingsFilePath =>
            Path.Combine(Application.persistentDataPath, SettingsFileName);

        private static string SettingsFileName =>
            IsDesktopGame ? DESKTOP_GAME_SETTINGS_FILE_NAME : PC_GAME_SETTINGS_FILE_NAME;

        /// <summary>
        /// 현재 창 설정을 파일에 저장합니다. 동기로 씁니다.
        /// 내용은 암호화하지 않은 JSON 문자열입니다.
        /// </summary>
        /// <returns>저장에 성공했으면 true.</returns>
        public static bool SaveSettings() {
            if (!IsInitialized) {
                Debug.LogWarning("[WindowDeskAPI] 초기화 전에는 설정을 저장할 수 없습니다.");
                return false;
            }

            try {
                File.WriteAllText(SettingsFilePath, Settings.Export());
                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskAPI] 설정 저장 실패 ({SettingsFilePath}) : {e.Message}");
                return false;
            }
        }

        /// <summary> 저장된 설정 파일이 있는지 여부 </summary>
        public static bool HasSavedSettings => File.Exists(SettingsFilePath);

        /// <summary>
        /// 저장된 설정 파일을 지웁니다. 다음 실행은 첫 실행처럼 기본값으로 시작합니다.
        /// 지금 창 상태는 건드리지 않습니다.
        /// </summary>
        /// <returns>지웠으면 true. 파일이 없었거나 실패하면 false.</returns>
        public static bool DeleteSettings() {
            // 파일명이 프로파일마다 다르다. 초기화 전에는 PC 게임 파일로 판단해 엉뚱한 것을 지운다.
            if (!RequireInitialized(nameof(DeleteSettings))) {
                return false;
            }

            string path = SettingsFilePath;

            if (!File.Exists(path)) {
                return false;
            }

            try {
                File.Delete(path);
                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskAPI] 설정 삭제 실패 ({path}) : {e.Message}");
                return false;
            }
        }

        /// <summary> 저장된 설정을 읽어 적용한다. 첫 실행이라 파일이 없으면 기본값으로 만들어 둔다. </summary>
        private static void LoadSettingsFile() {
            string path = SettingsFilePath;

            if (!File.Exists(path)) {
                // 크기 조절이 테두리 두께를 바꾸므로 해상도보다 먼저 확정한다. 복원 경로와 같은 순서다.
                ApplyDefaultResizable();
                ApplyBaseResolution();
                SaveSettings();
                return;
            }

            string json;

            try {
                json = File.ReadAllText(path);
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskAPI] 설정 불러오기 실패 ({path}) : {e.Message}");
                return;
            }

            DeskImportResult result = Settings.Import(json, ResolveImportOptions());

            if (!result.IsSuccess) {
                Debug.LogWarning($"[WindowDeskAPI] 저장된 설정을 적용하지 못했습니다 : {result.ErrorMessage}");
            }

            RewriteIfMonitorSubstituted(result);
        }

        /// <summary>
        /// 창을 끌어 다른 모니터로 옮겼을 때 지금 상태를 파일에 남깁니다. 창을 옮긴 감시자가 부릅니다.
        ///
        /// 유저가 창을 드래그해 옮기고 나서 저장 버튼을 따로 누르지는 않으므로 여기서 대신 남깁니다.
        /// 창이 새 모니터의 작업 영역에 앉는 것이 한 프레임 뒤에 끝나므로 그때 씁니다.
        ///
        /// 바탕화면 게임은 이 경로를 타지 않습니다. 그쪽은 옵션 화면에서 모니터를 골라 옮기므로
        /// 유저가 확인을 누르는 시점에 게임이 <see cref="SaveSettings"/> 를 부르는 별도 흐름입니다.
        /// 드래그마다 저장하면 옵션에서 취소를 눌러도 값이 이미 굳어 버립니다.
        /// </summary>
        internal static void AutoSaveOnMonitorDrag() {
            // 게임이 직접 옮긴 것도 감시자 눈에는 똑같이 "모니터가 바뀐" 것으로 보인다. 표시가 있으면 넘긴다.
            if (_skipMonitorAutoSave) {
                _skipMonitorAutoSave = false;
                return;
            }

            if (!IsInitialized || IsDesktopGame) {
                return;
            }

            DeskEventPump.RunNextFrame(() => SaveSettings());
        }

        private static bool _skipMonitorAutoSave;

        /// <summary>
        /// 다음에 감지되는 모니터 변경 한 번을 자동 저장에서 제외합니다.
        ///
        /// 감시자는 창이 어느 모니터에 있는지만 보므로 드래그와 코드 호출을 구별할 수 없습니다.
        /// <see cref="MoveWindowToMonitor(int)"/> 처럼 게임이 스스로 옮기는 경로가 미리 표시해 둡니다.
        /// 옵션 화면에서 고른 모니터가 확인을 누르기 전에 파일에 굳어 버리면 취소가 무의미해집니다.
        /// </summary>
        internal static void SuppressMonitorAutoSave() {
            _skipMonitorAutoSave = true;
        }

        /// <summary>
        /// 저장해 둔 모니터가 사라져 주 모니터로 대체됐으면 지금 상태를 다시 저장한다.
        /// 그러지 않으면 다음 실행마다 없는 모니터를 찾다가 또 대체된다.
        /// 창이 옮겨진 뒤에 써야 하므로 한 프레임 미룬다.
        /// </summary>
        private static void RewriteIfMonitorSubstituted(DeskImportResult result) {
            if (!ContainsMonitorSubstitution(result)) {
                return;
            }

            DeskEventPump.RunNextFrame(() => SaveSettings());
        }

        private static bool ContainsMonitorSubstitution(DeskImportResult result) {
            for (int i = 0; i < result.Substitutions.Count; i++) {
                if (result.Substitutions[i].Field == DeskSettingsService.FIELD_MONITOR) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 선언한 기능만 복원한다. 프레임은 기능 선언과 무관하므로 항상 포함한다.
        /// </summary>
        private static DESK_IMPORT_OPTIONS ResolveImportOptions() {
            if (IsDesktopGame) {
                return ResolveDesktopImportOptions();
            }

            return ResolvePcGameImportOptions();
        }

        /// <summary>
        /// 바탕화면 게임이 되살릴 항목.
        /// 표시 방식과 해상도는 초기화가 항상 정하므로 저장값을 되살리면 서로 싸운다.
        /// </summary>
        private static DESK_IMPORT_OPTIONS ResolveDesktopImportOptions() {
            DESK_IMPORT_OPTIONS options = DESK_IMPORT_OPTIONS.FRAME_RATE;

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.MONITOR_INFO)) {
                options |= DESK_IMPORT_OPTIONS.MONITOR;
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.TOP_MOST)) {
                options |= DESK_IMPORT_OPTIONS.TOP_MOST;
            }

            return options;
        }

        private static DESK_IMPORT_OPTIONS ResolvePcGameImportOptions() {
            DESK_IMPORT_OPTIONS options = DESK_IMPORT_OPTIONS.NONE;

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.RESOLUTION_INFO)) {
                options |= DESK_IMPORT_OPTIONS.RESOLUTION;
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.DISPLAY_MODE)) {
                options |= DESK_IMPORT_OPTIONS.DISPLAY_MODE;
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.MONITOR_INFO)) {
                options |= DESK_IMPORT_OPTIONS.MONITOR;
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.TOP_MOST)) {
                options |= DESK_IMPORT_OPTIONS.TOP_MOST;
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.CURSOR_CONFINE)) {
                options |= DESK_IMPORT_OPTIONS.CURSOR_CONFINE;
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)) {
                options |= DESK_IMPORT_OPTIONS.RESIZABLE;
            }

            return options | DESK_IMPORT_OPTIONS.FRAME_RATE;
        }

        #endregion 설정 파일

        #region 기본 해상도

        private static DeskResolution _baseResolution;
        private static bool _hasBaseResolution;

        /// <summary>
        /// 유니티가 띄운 시작 해상도를 기억한다. 저장 파일이 없을 때 쓸 기본값이다.
        /// <see cref="SetResizable"/> 로 테두리 두께가 바뀌면 클라이언트 영역이 몇 px 흔들리므로,
        /// 아무것도 건드리기 전에 재 두어야 첫 실행이 항상 같은 값을 저장한다.
        /// </summary>
        private static void CaptureBaseResolution() {
            if (!IsFeatureEnabled(DESK_WINDOW_FEATURE.RESOLUTION_INFO)) {
                return;
            }

            _baseResolution = Resolution.GetApplied();
            _hasBaseResolution = true;
        }

        /// <summary>
        /// 첫 실행의 창 크기 조절 값을 <see cref="DEFAULT_RESIZABLE"/> 로 못 박는다.
        /// 그냥 두면 PlayerSettings 의 Resizable Window 를 이어받아, 그 값이 무엇이냐에 따라
        /// 첫 실행이 허용/차단으로 갈리고 파일에도 그대로 굳는다.
        /// 바탕화면 게임은 창 크기 조절을 쓰지 않으므로 건너뛴다.
        /// </summary>
        private static void ApplyDefaultResizable() {
            if (IsDesktopGame || !CanControlWindow(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)) {
                return;
            }

            ApplyResizable(DEFAULT_RESIZABLE);
        }

        /// <summary> 기억해 둔 시작 해상도를 실제로 적용한다. 첫 실행에서 저장할 값을 확정하려고 부른다. </summary>
        private static void ApplyBaseResolution() {
            if (!_hasBaseResolution) {
                return;
            }

            DeskResolutionApplyResult result = Resolution.Apply(_baseResolution);

            if (!result.IsSuccess) {
                Debug.LogWarning($"[WindowDeskAPI] 시작 해상도 {_baseResolution} 를 적용하지 못했습니다 : {result.ErrorMessage}");
            }
        }

        #endregion 기본 해상도

        /// <summary> 프로파일에 필요한 기능 조합을 구한다. </summary>
        private static DESK_WINDOW_FEATURE ResolveProfileFeatures(DESK_WINDOW_PROFILE profiles) {
            DESK_WINDOW_FEATURE features = DESK_WINDOW_FEATURE.NONE;

            if ((profiles & DESK_WINDOW_PROFILE.PC_GAME) == DESK_WINDOW_PROFILE.PC_GAME) {
                features |= PC_GAME_FEATURES;
            }

            if ((profiles & DESK_WINDOW_PROFILE.DESKTOP_GAME) != DESK_WINDOW_PROFILE.DESKTOP_GAME) {
                return features;
            }

            features |= DESKTOP_GAME_FEATURES;

            // 주석으로만 두면 다른 프로파일과 같이 선언했을 때 되살아난다. 선언 단계에서 확실히 걷어낸다.
            return features & ~DESKTOP_GAME_BLOCKED_FEATURES;
        }

        /// <summary> 종료할 때 되돌릴 수 있도록 창의 원본 상태를 남긴다. </summary>
        private static void CaptureOriginalStyle() {
            if (_originalStyle.IsValid || !DeskPlatform.IsWindowControlEnabled) {
                return;
            }

            DeskPlatform.TryCaptureWindowStyle(WindowHandle, out _originalStyle);
        }

        private static void HookApplicationQuit() {
            if (_isQuitHooked) {
                return;
            }

            _isQuitHooked = true;
            Application.quitting += Shutdown;
        }

        /// <summary>
        /// 창 스타일을 원래대로 돌리고 선언을 모두 해제합니다. 종료 시 자동 호출됩니다.
        /// </summary>
        public static void Shutdown() {
            ReleaseDesktopWindow();

            if (_originalStyle.IsValid && DeskPlatform.IsWindowControlEnabled) {
                DeskPlatform.RestoreWindowStyle(WindowHandle, _originalStyle);
            }

            _originalStyle = default;
            ActiveProfiles = DESK_WINDOW_PROFILE.NONE;
            EnabledFeatures = DESK_WINDOW_FEATURE.NONE;
            _warnedFeatures = DESK_WINDOW_FEATURE.NONE;
            IsInitialized = false;
            IsTopMostRequested = false;
            IsResizableRequested = DEFAULT_RESIZABLE;
            IsCursorConfined = false;
            _hasBaseResolution = false;
            _baseResolution = default;
            _skipMonitorAutoSave = false;
            ApplyCursorConfine();

            DeskDisplayWatcher.Disable();
            DeskFrameRate.RestoreOriginal();
            ReleaseServices();
            DeskEvents.ClearAll();
            _isListeningDisplay = false;
            DeskMonitorCache.Invalidate();
            InvalidateWindowHandle();
        }

        /// <summary> 모니터 정보를 쓰는 경우에만 구성 변경 감시를 켠다. </summary>
        private static void StartDisplayWatch() {
            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.MONITOR_INFO)) {
                DeskDisplayWatcher.Enable();
                return;
            }

            DeskDisplayWatcher.Disable();
        }

        /// <summary> 선언된 기능에 맞는 서비스 구현을 등록한다. 아직 만들지 않은 서비스는 비워 둔다. </summary>
        private static void RegisterServices() {
            _displayMode = new DeskDisplayModeService();
            _resolution = new DeskResolutionService();
            _monitors = new DeskMonitorService();
            _windowState = new DeskWindowStateService();
            _settings = new DeskSettingsService();
        }

        /// <summary> 등록된 서비스 구현을 모두 버린다. 다음 초기화에서 다시 등록된다. </summary>
        private static void ReleaseServices() {
            _resolution = null;
            _displayMode = null;
            _monitors = null;
            _windowState = null;
            _settings = null;
        }

        /// <summary>
        /// 선언 여부를 검사합니다. 미선언이면 기능당 한 번만 안내 로그를 남깁니다.
        /// 매 프레임 호출되는 기능에서 로그가 쏟아지지 않도록 한 번만 남깁니다.
        /// </summary>
        private static bool RequireFeature(DESK_WINDOW_FEATURE feature) {
            if (IsFeatureEnabled(feature)) {
                return true;
            }

            if ((_warnedFeatures & feature) != feature) {
                _warnedFeatures |= feature;
                LogUnavailable(feature.ToString());
            }

            return false;
        }

        /// <summary>
        /// 현재 모드에서 못 쓰는 기능을 불렀을 때 남기는 안내.
        /// 조용히 무시하면 왜 동작하지 않는지 알 길이 없으므로 눈에 띄게 남긴다.
        /// </summary>
        /// <param name="what">막힌 기능이나 멤버 이름.</param>
        private static void LogUnavailable(string what) {
            Debug.LogError($"[WindowDeskAPI] 현재 모드에서는 사용할 수 없는 기능입니다 : {what}"
                           + $" (프로파일 {ActiveProfiles} / 선언된 기능 {EnabledFeatures})");
        }

        /// <summary>
        /// 초기화를 마쳤는지 검사합니다. 기능 선언과 무관하게 동작하는 멤버가 쓰는 가드입니다.
        ///
        /// 기능을 거치는 멤버는 <see cref="RequireFeature"/> 가 대신 막아 줍니다.
        /// 선언 전에는 <see cref="EnabledFeatures"/> 가 NONE 이라 그쪽은 자동으로 취소되기 때문입니다.
        /// 프레임이나 배율처럼 기능 선언이 없는 멤버는 그 그물에 걸리지 않으므로 여기서 막습니다.
        /// </summary>
        /// <param name="memberName">경고에 남길 호출 지점 이름.</param>
        private static bool RequireInitialized(string memberName) {
            if (IsInitialized) {
                return true;
            }

            Debug.LogWarning($"[WindowDeskAPI] 초기화 전에는 {memberName} 가 동작하지 않습니다. "
                             + "Initialize 를 먼저 부르십시오.");
            return false;
        }

        #endregion 기능 선언

        #region 바탕화면 게임

        private static bool _isTransparentRequested;
        private static bool _isClickThroughEnabled;
        private static bool _isPassingThroughNow;

        /// <summary> 창 배경을 뚫도록 요청한 상태인지 여부. </summary>
        public static bool IsTransparent => _isTransparentRequested;

        /// <summary>
        /// 커서 아래를 보고 클릭을 흘려보낼지 정하는 판정이 걸려 있는지 여부.
        /// 켜져 있어도 그려진 것 위에서는 클릭이 그대로 들어옵니다.
        /// </summary>
        public static bool IsClickThrough => _isClickThroughEnabled;

        /// <summary> 지금 이 순간 클릭이 창 뒤로 넘어가는 상태인지 여부. 커서가 움직이면 매 프레임 바뀝니다. </summary>
        public static bool IsPassingThroughNow => _isPassingThroughNow;

        /// <summary> OS 창에 클릭 통과가 실제로 걸려 있는지 조회합니다. </summary>
        public static bool IsClickThroughApplied() {
            if (!CanQueryWindow(DESK_WINDOW_FEATURE.CLICK_THROUGH)) {
                return false;
            }

            return DeskPlatform.IsClickThrough(WindowHandle);
        }

        /// <summary>
        /// 화면 빈 자리에서 마지막으로 읽은 알파. 음수면 아직 읽은 적이 없다.
        /// 투명이 안 나올 때 0 에 가까우면 창 설정 문제, 1 에 가까우면 렌더 파이프라인이 알파를 밀고 있는 것이다.
        /// </summary>
        public static float BackgroundAlpha => DeskHitTest.LastBackgroundAlpha;

        /// <summary> 마지막 투명 처리 시도의 결과. 투명이 안 나올 때 원인을 보기 위한 값이다 </summary>
        public static string TransparentReport => DeskPlatform.LastTransparentReport;

        /// <summary> 바탕화면 게임으로 선언된 상태인지 여부. </summary>
        private static bool IsDesktopGame =>
            (ActiveProfiles & DESK_WINDOW_PROFILE.DESKTOP_GAME) == DESK_WINDOW_PROFILE.DESKTOP_GAME;

        /// <summary>
        /// 바탕화면 게임에 필요한 창 상태를 한 번에 건다.
        /// 유니티가 표시 방식을 실제로 바꾸는 것은 다음 프레임이므로, 창 스타일은 그 뒤에 얹는다.
        /// </summary>
        private static void ApplyDesktopGameDefaults() {
            if (!IsDesktopGame) {
                return;
            }

            _isTransparentRequested = true;
            _isClickThroughEnabled = true;

            // 크기 조절을 요청해 두면 RefreshResizable 이 매 프레임 테두리를 되살려 RefreshDesktopWindow 와 싸운다.
            IsResizableRequested = false;

            // 선언부의 DESKTOP_GAME_BLOCKED_FEATURES 에서 CURSOR_CONFINE 을 뺀 것과 짝을 이룬다.
            // 선언은 라이브러리가 커서를 가두지 못하게 막을 뿐이고, 게임이 이미 잠가 둔 것은 여기서 푼다.
            ReleaseDesktopCursor();

            DisplayMode.Apply(DESK_DISPLAY_MODE.BORDERLESS_WINDOWED);
            DeskEventPump.RunNextFrame(ApplyDesktopWindowStyle);
        }

        /// <summary>
        /// 테두리 · 크기 · 레이어 · 투명 순으로 건다. 클릭 통과 자체는 판정기가 매 프레임 정한다.
        /// 창 모양을 먼저 확정해야 DWM 유리 확장이 그 위에 얹힌 채로 남는다.
        /// </summary>
        private static void ApplyDesktopWindowStyle() {
            if (!IsWindowControlEnabled || WindowHandle == IntPtr.Zero) {
                return;
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.BORDERLESS)) {
                DeskPlatform.SetBorderless(WindowHandle, true);
            }

            FitWindowToDesktop(Monitors.CurrentIndex);

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.TRANSPARENT)) {
                DeskPlatform.SetTransparent(WindowHandle, _isTransparentRequested);
            }

            if (!IsFeatureEnabled(DESK_WINDOW_FEATURE.CLICK_THROUGH)) {
                return;
            }

            // WS_EX_TRANSPARENT 만으로는 다른 프로세스의 창까지 클릭이 넘어가지 않는다.
            DeskPlatform.EnableLayered(WindowHandle);

            // 판정 결과가 나오기 전까지는 바탕화면을 가리지 않도록 흘려보내는 쪽으로 둔다.
            DeskPlatform.SetClickThrough(WindowHandle, true);
            _isPassingThroughNow = true;

            DeskHitTest.Enable();
        }

        /// <summary>
        /// 커서 판정 결과를 창에 반영한다. 매 프레임 불리므로 값이 바뀔 때만 창을 건드린다.
        /// </summary>
        /// <param name="passThrough">true 면 클릭을 창 뒤로 넘긴다.</param>
        internal static void SetPassThrough(bool passThrough) {
            if (!IsInitialized
                || !_isClickThroughEnabled
                || !IsFeatureEnabled(DESK_WINDOW_FEATURE.CLICK_THROUGH)
                || !IsWindowControlEnabled
                || WindowHandle == IntPtr.Zero
                || _isPassingThroughNow == passThrough) {
                return;
            }

            _isPassingThroughNow = passThrough;
            DeskPlatform.SetClickThrough(WindowHandle, passThrough);
        }

        /// <summary>
        /// 유니티가 덮어쓴 창 스타일을 되돌린다. 매 프레임 불린다.
        /// 클릭 통과는 판정기가 맡으므로 여기서는 테두리만 보고, 테두리를 고쳤을 때만 함께 다시 건다.
        /// </summary>
        internal static void RefreshDesktopWindow() {
            if (!IsInitialized || !IsDesktopGame || !IsWindowControlEnabled || WindowHandle == IntPtr.Zero) {
                return;
            }

            if (!IsFeatureEnabled(DESK_WINDOW_FEATURE.BORDERLESS) || DeskPlatform.IsBorderless(WindowHandle)) {
                FitWindowToDesktop(Monitors.CurrentIndex);
                return;
            }

            DeskPlatform.SetBorderless(WindowHandle, true);
            FitWindowToDesktop(Monitors.CurrentIndex);

            // 테두리를 다시 없애면 유니티가 얹어 둔 확장 스타일까지 함께 밀리므로 같이 다시 건다.
            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.CLICK_THROUGH)) {
                DeskPlatform.EnableLayered(WindowHandle);
            }

            if (IsFeatureEnabled(DESK_WINDOW_FEATURE.TRANSPARENT)) {
                DeskPlatform.SetTransparent(WindowHandle, _isTransparentRequested);
            }
        }

        /// <summary> 창을 원래대로 돌리기 전에 바탕화면 전용 상태를 먼저 푼다. </summary>
        private static void ReleaseDesktopWindow() {
            DeskHitTest.Disable();

            if (IsWindowControlEnabled && WindowHandle != IntPtr.Zero) {
                if (_isClickThroughEnabled) {
                    DeskPlatform.SetClickThrough(WindowHandle, false);
                }

                if (_isTransparentRequested) {
                    DeskPlatform.SetTransparent(WindowHandle, false);
                }
            }

            _isTransparentRequested = false;
            _isClickThroughEnabled = false;
            _isPassingThroughNow = false;
        }

        #endregion 바탕화면 게임

        #region 트레이

        /// <summary> 트레이 아이콘이 떠 있는지 여부 </summary>
        public static bool IsTrayEnabled => DeskTray.IsEnabled;

        /// <summary> 현재 플랫폼이 트레이를 제공하는지 여부. Windows 가 아니면 false 입니다. </summary>
        public static bool IsTraySupported => DeskTray.IsSupported;

        /// <summary> 지금 걸려 있는 툴팁. 지정하지 않았으면 앱 이름입니다. </summary>
        public static string TrayTooltip => DeskTray.Tooltip;

        /// <summary> 등록된 트레이 메뉴 항목 수 </summary>
        public static int TrayMenuItemCount => DeskTray.MenuItemCount;

        /// <summary>
        /// 트레이 아이콘을 띄웁니다. 툴팁은 앱 이름, 그림은 실행 파일 아이콘, 메뉴 색은 밝게가 쓰입니다.
        /// <see cref="Initialize(DESK_WINDOW_PROFILE)"/> 와 무관하게 단독으로 부를 수 있습니다.
        /// 아이콘은 게임이 끝날 때 자동으로 내려갑니다.
        /// </summary>
        /// <returns>아이콘이 떴으면 true. Windows 가 아니면 false.</returns>
        public static bool EnableTray() {
            return DeskTray.Enable();
        }

        /// <summary>
        /// 그림만 지정해서 트레이 아이콘을 띄웁니다. 툴팁은 앱 이름이 쓰입니다.
        /// </summary>
        /// <param name="icon">쓸 그림. 비어 있으면 실행 파일 아이콘으로 물러납니다.</param>
        public static bool EnableTray(Texture2D icon) {
            return DeskTray.Enable(icon);
        }

        /// <summary>
        /// 툴팁을 지정해서 트레이 아이콘을 띄웁니다.
        /// </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        /// <returns>아이콘이 떴으면 true. Windows 가 아니면 false.</returns>
        public static bool EnableTray(string tooltip) {
            return DeskTray.Enable(tooltip);
        }

        /// <summary>
        /// 툴팁과 메뉴 색을 함께 정해서 트레이 아이콘을 띄웁니다.
        /// </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        /// <param name="theme">우클릭 메뉴를 밝게 그릴지 어둡게 그릴지.</param>
        public static bool EnableTray(string tooltip, DESK_MENU_THEME theme) {
            return DeskTray.Enable(tooltip, theme);
        }

        /// <summary>
        /// 툴팁 · 그림 · 메뉴 색을 한 번에 정해서 트레이 아이콘을 띄웁니다.
        /// </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        /// <param name="icon">쓸 그림. 비어 있으면 실행 파일 아이콘으로 물러납니다.</param>
        /// <param name="theme">우클릭 메뉴를 밝게 그릴지 어둡게 그릴지.</param>
        public static bool EnableTray(string tooltip, Texture2D icon, DESK_MENU_THEME theme) {
            return DeskTray.Enable(tooltip, icon, theme);
        }

        /// <summary>
        /// 아이콘 파일을 지정해 트레이 아이콘을 띄웁니다.
        /// </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        /// <param name="iconFilePath">쓸 .ico 파일의 전체 경로. 읽지 못하면 실행 파일 아이콘으로 물러납니다.</param>
        public static bool EnableTray(string tooltip, string iconFilePath) {
            return DeskTray.Enable(tooltip, iconFilePath);
        }

        /// <summary>
        /// 프로젝트에 넣어 둔 그림을 아이콘으로 써서 트레이를 띄웁니다.
        /// 그림은 트레이가 쓰는 크기로 줄여서 넘깁니다.
        /// </summary>
        /// <param name="tooltip">아이콘에 마우스를 올렸을 때 나올 글자.</param>
        /// <param name="icon">쓸 그림. 임포트 설정은 어떤 것이든 상관없습니다.</param>
        public static bool EnableTray(string tooltip, Texture2D icon) {
            return DeskTray.Enable(tooltip, icon);
        }

        /// <summary> 떠 있는 트레이 아이콘의 그림을 바꿉니다. </summary>
        /// <param name="icon">쓸 그림. 임포트 설정은 어떤 것이든 상관없습니다.</param>
        public static bool SetTrayIcon(Texture2D icon) {
            return DeskTray.SetIcon(icon);
        }

        /// <summary> 떠 있는 트레이 아이콘을 .ico 파일로 바꿉니다. </summary>
        public static bool SetTrayIcon(string iconFilePath) {
            return DeskTray.SetIcon(iconFilePath);
        }

        /// <summary>
        /// 트레이 아이콘을 내리고 메뉴를 비웁니다.
        /// 게임 종료 시 자동으로 불리므로 보통은 직접 부를 일이 없습니다.
        /// </summary>
        public static void DisableTray() {
            DeskTray.Disable();
        }

        /// <summary> 아이콘에 마우스를 올렸을 때 나올 글자를 바꿉니다. </summary>
        public static bool SetTrayTooltip(string tooltip) {
            return DeskTray.SetTooltip(tooltip);
        }

        /// <summary>
        /// 우클릭 메뉴에 항목을 하나 더합니다. 등록한 순서대로 위에서 아래로 놓입니다.
        /// </summary>
        /// <param name="label">메뉴에 보일 글자.</param>
        /// <param name="onClick">항목을 눌렀을 때 실행할 동작.</param>
        /// <returns>등록했으면 true.</returns>
        public static bool AddTrayMenuItem(string label, Action onClick) {
            return DeskTray.AddMenuItem(label, onClick);
        }

        /// <summary>
        /// 라벨 왼쪽에 그림이 붙은 메뉴 항목을 더합니다.
        /// 그림은 메뉴가 쓰는 크기로 줄여서 넘깁니다.
        /// </summary>
        /// <param name="label">메뉴에 보일 글자.</param>
        /// <param name="onClick">항목을 눌렀을 때 실행할 동작.</param>
        /// <param name="icon">라벨 왼쪽에 놓을 그림. 임포트 설정은 어떤 것이든 상관없습니다.</param>
        public static bool AddTrayMenuItem(string label, Action onClick, Texture2D icon) {
            return DeskTray.AddMenuItem(label, onClick, icon);
        }

        /// <summary> 지금 걸려 있는 트레이 메뉴 테마. 기본값은 <see cref="DESK_MENU_THEME.LIGHT"/> 입니다. </summary>
        public static DESK_MENU_THEME TrayMenuTheme => DeskTray.MenuTheme;

        /// <summary>
        /// 트레이 메뉴를 밝게 그릴지 어둡게 그릴지 정합니다. 글자색과 강조색은 윈도우가 함께 맞춰 줍니다.
        /// 트레이를 켜기 전에 불러도 됩니다.
        ///
        /// Windows 10 1809 미만에서는 동작하지 않고 경고만 한 번 남깁니다.
        /// </summary>
        /// <param name="theme">쓸 테마. 지정하지 않으면 <see cref="DESK_MENU_THEME.LIGHT"/> 로 그립니다.</param>
        /// <returns>테마가 걸렸으면 true.</returns>
        public static bool SetTrayMenuTheme(DESK_MENU_THEME theme) {
            return DeskTray.SetMenuTheme(theme);
        }

        /// <summary>
        /// 메뉴에 가로 구분선을 넣습니다. 항목 사이를 갈라 놓을 때 씁니다.
        /// 맨 앞이거나 구분선이 연달아 놓이면 넣지 않습니다.
        /// </summary>
        /// <returns>구분선을 넣었으면 true.</returns>
        public static bool AddTrayMenuSeparator() {
            return DeskTray.AddSeparator();
        }

        /// <summary> 등록된 트레이 메뉴를 전부 지웁니다. 아이콘은 그대로 남습니다. </summary>
        public static void ClearTrayMenu() {
            DeskTray.ClearMenu();
        }

        /// <summary>
        /// 게임 창을 앞으로 끌어올립니다. 최소화되어 있으면 복원합니다.
        /// 트레이 좌클릭에 기본으로 물려 있고, 메뉴 항목에서도 그대로 쓸 수 있습니다.
        /// 윈도우가 창 가로채기를 막는 상황에서는 실패하고 작업 표시줄 버튼만 깜빡일 수 있습니다.
        /// </summary>
        /// <returns>창이 앞으로 올라왔으면 true.</returns>
        public static bool FocusGameWindow() {
            if (!DeskPlatform.IsSupported || WindowHandle == IntPtr.Zero) {
                return false;
            }

            return DeskPlatform.ActivateWindow(WindowHandle);
        }

        #endregion 트레이

        #region 내부 서비스

        private const DESK_WINDOW_FEATURE WINDOW_STATE_FEATURES =
            DESK_WINDOW_FEATURE.TOP_MOST | DESK_WINDOW_FEATURE.BORDERLESS | DESK_WINDOW_FEATURE.TASKBAR_BUTTON;

        private static IDeskResolutionService _resolution;
        private static IDeskDisplayModeService _displayMode;
        private static IDeskMonitorService _monitors;
        private static IDeskWindowStateService _windowState;
        private static IDeskSettingsService _settings;

        /// <summary> 해상도 조회와 적용. 미선언이면 아무 일도 하지 않는 구현을 돌려준다. </summary>
        internal static IDeskResolutionService Resolution =>
            IsServiceUsable(_resolution, DESK_WINDOW_FEATURE.RESOLUTION_INFO) ? _resolution : DeskNullService.INSTANCE;

        /// <summary> 전체화면 · 창 · 테두리없는 창 전환 </summary>
        internal static IDeskDisplayModeService DisplayMode =>
            IsServiceUsable(_displayMode, DESK_WINDOW_FEATURE.DISPLAY_MODE) ? _displayMode : DeskNullService.INSTANCE;

        /// <summary> 모니터 목록 조회와 창 이동 </summary>
        internal static IDeskMonitorService Monitors =>
            IsServiceUsable(_monitors, DESK_WINDOW_FEATURE.MONITOR_INFO) ? _monitors : DeskNullService.INSTANCE;

        /// <summary> 최상위 · 테두리 · 크기 조절 · 작업표시줄 </summary>
        internal static IDeskWindowStateService WindowState =>
            _windowState != null && RequireAnyFeature(WINDOW_STATE_FEATURES) ? _windowState : DeskNullService.INSTANCE;

        /// <summary> 설정 JSON 내보내기와 불러오기 </summary>
        internal static IDeskSettingsService Settings =>
            _settings != null && IsInitialized ? _settings : DeskNullService.INSTANCE;

        /// <summary>
        /// 구현이 등록되어 있고 기능도 선언되었는지 검사합니다.
        /// 아직 구현하지 않은 서비스는 선언 경고 없이 조용히 Null 구현으로 넘깁니다.
        /// </summary>
        private static bool IsServiceUsable(object service, DESK_WINDOW_FEATURE feature) {
            return service != null && RequireFeature(feature);
        }

        /// <summary>
        /// 여러 기능 중 하나라도 선언되었는지 검사합니다.
        /// 서비스 하나가 여러 기능을 다룰 때, 전부 선언하지 않았다고 막으면 쓸 수 있는 기능까지 닫히기 때문입니다.
        /// </summary>
        private static bool RequireAnyFeature(DESK_WINDOW_FEATURE features) {
            if ((EnabledFeatures & features) != DESK_WINDOW_FEATURE.NONE) {
                return true;
            }

            if ((_warnedFeatures & features) != features) {
                _warnedFeatures |= features;
                LogUnavailable(features.ToString());
            }

            return false;
        }

        #endregion 내부 서비스

        #region 해상도

        /// <summary> 창이 놓인 모니터가 지원하는 해상도. 같은 크기는 가장 높은 주사율 하나만 남고 큰 것부터 정렬됩니다. </summary>
        public static IReadOnlyList<DeskResolution> GetSupportedResolutions() {
            return Resolution.GetSupported();
        }

        /// <summary> 지정한 모니터가 지원하는 해상도. </summary>
        /// <param name="monitorIndex">모니터 인덱스.</param>
        public static IReadOnlyList<DeskResolution> GetSupportedResolutions(int monitorIndex) {
            return Resolution.GetSupported(monitorIndex);
        }

        /// <summary>
        /// 게임에 적용 중인 해상도. 옵션 화면이 "현재 값" 으로 표시하고 저장에도 이 값을 씁니다.
        /// 창 모드에서 화면에 맞춰 줄어들었더라도 고른 값 그대로입니다.
        /// </summary>
        public static DeskResolution GetAppliedResolution() {
            return Resolution.GetApplied();
        }

        /// <summary> 창이 놓인 모니터의 OS 디스플레이 해상도. 창 모드에서는 바탕화면 해상도입니다. </summary>
        public static DeskResolution GetMonitorResolution() {
            return Resolution.GetCurrent();
        }

        /// <summary> 지정한 모니터의 OS 디스플레이 해상도. </summary>
        /// <param name="monitorIndex">모니터 인덱스.</param>
        public static DeskResolution GetMonitorResolution(int monitorIndex) {
            return Resolution.GetCurrent(monitorIndex);
        }

        /// <summary> 해상도를 적용합니다. 표시 방식은 그대로 둡니다. </summary>
        /// <param name="resolution">목록에서 고른 해상도.</param>
        public static DeskResolutionApplyResult ApplyResolution(DeskResolution resolution) {
            return Resolution.Apply(resolution);
        }

        /// <summary> 해상도와 표시 방식을 함께 적용합니다. </summary>
        /// <param name="resolution">목록에서 고른 해상도.</param>
        /// <param name="mode">함께 적용할 표시 방식.</param>
        public static DeskResolutionApplyResult ApplyResolution(DeskResolution resolution, DESK_DISPLAY_MODE mode) {
            return Resolution.Apply(resolution, mode);
        }

        /// <summary> 목록에 없는 값을 넘겼을 때 대신 쓸 가장 가까운 해상도를 찾습니다. </summary>
        /// <param name="target">찾고 싶은 해상도.</param>
        /// <param name="monitorIndex">기준 모니터 인덱스.</param>
        /// <param name="nearest">가장 가까운 해상도.</param>
        public static bool TryFindNearestResolution(DeskResolution target, int monitorIndex,
                                                    out DeskResolution nearest) {
            return Resolution.TryFindNearest(target, monitorIndex, out nearest);
        }

        #endregion 해상도

        #region 표시 방식

        /// <summary> 지금 적용된 표시 방식. </summary>
        public static DESK_DISPLAY_MODE CurrentDisplayMode => DisplayMode.Current;

        /// <summary> 이 환경에서 해당 표시 방식을 쓸 수 있는지 여부. </summary>
        /// <param name="mode">검사할 표시 방식.</param>
        public static bool IsDisplayModeSupported(DESK_DISPLAY_MODE mode) {
            return DisplayMode.IsSupported(mode);
        }

        /// <summary>
        /// 표시 방식을 바꿉니다. 고른 해상도는 그대로 따라갑니다.
        /// 실제 적용은 한 프레임 뒤에 끝나고 <see cref="IDeskDisplayListener.OnDisplayModeChanged"/> 로 알립니다.
        /// </summary>
        /// <param name="mode">적용할 표시 방식.</param>
        public static bool ApplyDisplayMode(DESK_DISPLAY_MODE mode) {
            // 바탕화면은 테두리없는 창으로 고정이다. 바꾸면 투명과 클릭 통과가 함께 무너진다.
            // 초기화 전에는 서비스가 없어 조용히 false 만 돌아간다. 원인을 알 수 있게 여기서 잡는다.
            if (!IsInitialized) {
                Debug.LogWarning($"[WindowDeskAPI] 초기화 전에는 {nameof(ApplyDisplayMode)} 가 동작하지 않습니다. "
                                 + "Initialize 를 먼저 부르십시오.");
                return false;
            }

            if (!RequireNotDesktopGame(nameof(ApplyDisplayMode))) {
                return false;
            }

            return DisplayMode.Apply(mode);
        }

        #endregion 표시 방식

        #region 모니터

        /// <summary> 연결된 모니터 전체 (OS 열거 순). </summary>
        public static IReadOnlyList<DeskMonitorInfo> GetMonitors() {
            return Monitors.All;
        }

        /// <summary> 화면 배치 왼쪽부터의 모니터 인덱스 순서. OS 열거 순서와 다릅니다. </summary>
        public static IReadOnlyList<int> GetLeftToRightOrder() {
            return Monitors.LeftToRightOrder;
        }

        /// <summary> 창이 놓인 모니터 인덱스. 찾지 못하면 -1. </summary>
        public static int CurrentMonitorIndex => Monitors.CurrentIndex;

        /// <summary> 주 모니터 인덱스. 없으면 -1. </summary>
        public static int PrimaryMonitorIndex => Monitors.PrimaryIndex;

        /// <summary> 창이 놓인 모니터가 사라졌을 때의 대응 방식. 기본은 주 모니터로 이동입니다. </summary>
        public static DESK_MONITOR_LOST_POLICY MonitorLostPolicy {
            get => Monitors.LostPolicy;
            set => Monitors.LostPolicy = value;
        }

        /// <summary> 창이 놓인 모니터 정보를 가져옵니다. </summary>
        /// <param name="monitor">찾은 모니터 정보.</param>
        public static bool TryGetCurrentMonitor(out DeskMonitorInfo monitor) {
            return Monitors.TryGetCurrent(out monitor);
        }

        /// <summary> 인덱스로 모니터 정보를 가져옵니다. </summary>
        /// <param name="monitorIndex">모니터 인덱스.</param>
        /// <param name="monitor">찾은 모니터 정보.</param>
        public static bool TryGetMonitor(int monitorIndex, out DeskMonitorInfo monitor) {
            return Monitors.TryGetAt(monitorIndex, out monitor);
        }

        /// <summary> 모니터 구성 스냅샷. 조회 시점의 사본입니다. </summary>
        /// <summary>
        /// 모니터의 작업표시줄 두께를 네 변으로 돌려줍니다. 단위는 물리 픽셀입니다.
        /// Screen 좌표 · Input.mousePosition · ScreenToWorldPoint 와 단위가 같아 그대로 넘길 수 있습니다.
        /// 캔버스 UI 에는 <see cref="GetScaledWorkAreaInsets"/> 를 쓰십시오.
        /// </summary>
        /// <param name="monitorIndex">대상 모니터. 음수면 창이 놓인 모니터.</param>
        /// <returns>네 변의 두께. 모니터를 찾지 못하면 전부 0.</returns>
        public static DeskEdgeInsets GetWorkAreaInsets(int monitorIndex = -1) {
            if (!TryResolveMonitor(monitorIndex, out DeskMonitorInfo monitor)) {
                return DeskEdgeInsets.ZERO;
            }

            return DeskEdgeInsets.FromBounds(monitor.Bounds, monitor.WorkArea);
        }

        /// <summary>
        /// 작업표시줄 두께를 모니터 배율로 나눠 돌려줍니다.
        /// 배율이 달라도 같은 값이 나오므로 Constant Pixel Size 캔버스 배치에 씁니다.
        /// 화면 좌표 · 월드 좌표에는 <see cref="GetWorkAreaInsets"/> 를 쓰십시오.
        /// </summary>
        /// <param name="monitorIndex">대상 모니터. 음수면 창이 놓인 모니터.</param>
        /// <returns>배율로 나눈 네 변의 두께. 모니터를 찾지 못하면 전부 0.</returns>
        public static DeskEdgeInsets GetScaledWorkAreaInsets(int monitorIndex = -1) {
            if (!TryResolveMonitor(monitorIndex, out DeskMonitorInfo monitor) || monitor.ScaleFactor <= 0f) {
                return DeskEdgeInsets.ZERO;
            }

            DeskEdgeInsets insets = DeskEdgeInsets.FromBounds(monitor.Bounds, monitor.WorkArea);

            return insets.Divide(monitor.ScaleFactor);
        }

        /// <summary>
        /// 사각형을 작업 영역 안으로 밀어 넣습니다. 좌표는 가상 데스크탑 기준입니다.
        /// 작업 영역보다 큰 사각형은 좌상단에 맞춥니다.
        /// </summary>
        /// <param name="rect">가둘 사각형.</param>
        /// <param name="monitorIndex">기준 모니터. 음수면 창이 놓인 모니터.</param>
        /// <returns>작업 영역 안으로 옮긴 사각형.</returns>
        public static RectInt ClampToWorkArea(RectInt rect, int monitorIndex = -1) {
            if (!TryResolveMonitor(monitorIndex, out DeskMonitorInfo monitor)) {
                return rect;
            }

            RectInt area = monitor.WorkArea;

            int x = Mathf.Clamp(rect.x, area.x, Mathf.Max(area.x, area.xMax - rect.width));
            int y = Mathf.Clamp(rect.y, area.y, Mathf.Max(area.y, area.yMax - rect.height));

            return new RectInt(x, y, rect.width, rect.height);
        }

        /// <summary>
        /// 점 하나를 작업 영역 안으로 가둡니다. 좌표는 가상 데스크탑 기준입니다.
        /// </summary>
        /// <param name="point">가둘 점.</param>
        /// <param name="monitorIndex">기준 모니터. 음수면 창이 놓인 모니터.</param>
        /// <returns>작업 영역 안으로 옮긴 점.</returns>
        public static Vector2Int ClampToWorkArea(Vector2Int point, int monitorIndex = -1) {
            if (!TryResolveMonitor(monitorIndex, out DeskMonitorInfo monitor)) {
                return point;
            }

            RectInt area = monitor.WorkArea;

            return new Vector2Int(
                Mathf.Clamp(point.x, area.x, area.xMax - 1),
                Mathf.Clamp(point.y, area.y, area.yMax - 1));
        }

        /// <summary> 음수면 창이 놓인 모니터를 쓴다. 두 경로를 한곳에 모아 둔다. </summary>
        private static bool TryResolveMonitor(int monitorIndex, out DeskMonitorInfo monitor) {
            return monitorIndex >= 0
                ? Monitors.TryGetAt(monitorIndex, out monitor)
                : Monitors.TryGetCurrent(out monitor);
        }

        public static DeskMonitorLayout GetMonitorLayout() {
            return Monitors.GetLayout();
        }

        /// <summary> 모니터 목록을 다시 열거합니다. </summary>
        public static bool RefreshMonitors() {
            return Monitors.Refresh();
        }

        /// <summary> 창을 지정한 모니터로 옮깁니다. </summary>
        /// <param name="monitorIndex">대상 모니터 인덱스.</param>
        public static DeskMoveResult MoveWindowToMonitor(int monitorIndex) {
            SuppressMonitorAutoSave();

            // PC 게임 경로는 작업 영역 기준으로 가운데 앉히고 DPI 로 크기를 줄인다. 바탕화면은 정반대다.
            return IsDesktopGame
                ? MoveDesktopWindowToMonitor(monitorIndex)
                : Monitors.MoveWindowTo(monitorIndex);
        }

        /// <summary>
        /// 바탕화면 창을 다른 모니터로 옮긴다.
        /// 모니터 전체를 덮어야 하므로 작업 영역 · 가운데 정렬 · DPI 크기 조정을 쓰지 않는다.
        /// </summary>
        /// <param name="monitorIndex">옮길 모니터 인덱스.</param>
        private static DeskMoveResult MoveDesktopWindowToMonitor(int monitorIndex) {
            if (!IsWindowControlEnabled || WindowHandle == IntPtr.Zero) {
                return DeskMoveResult.Fail("이 환경에서는 창을 제어할 수 없습니다.", monitorIndex);
            }

            if (!Monitors.TryGetAt(monitorIndex, out DeskMonitorInfo target)) {
                return DeskMoveResult.Fail($"모니터 인덱스 {monitorIndex} 를 찾지 못했습니다.", monitorIndex);
            }

            int fromIndex = Monitors.CurrentIndex;
            float dpiScaleRatio = ResolveDesktopDpiRatio(fromIndex, target);

            FitWindowToDesktop(monitorIndex);

            RectInt applied = DeskPlatform.TryGetWindowRect(WindowHandle, out RectInt actual)
                ? actual
                : target.Bounds;

            if (!Mathf.Approximately(dpiScaleRatio, DeskConstants.DEFAULT_DPI_SCALE_RATIO)) {
                DeskEvents.RaiseDpiScaleChanged(dpiScaleRatio);
            }

            return DeskMoveResult.Success(fromIndex, monitorIndex, dpiScaleRatio, applied);
        }

        /// <summary> 옮기기 전 모니터 대비 배율. 관찰자가 이 값으로 UI 와 오브젝트를 다시 맞춘다. </summary>
        private static float ResolveDesktopDpiRatio(int fromIndex, DeskMonitorInfo target) {
            if (!Monitors.TryGetAt(fromIndex, out DeskMonitorInfo from) || from.ScaleFactor <= 0f) {
                return DeskConstants.DEFAULT_DPI_SCALE_RATIO;
            }

            return target.ScaleFactor / from.ScaleFactor;
        }

        /// <summary> 배치 방식을 지정해 창을 옮깁니다. </summary>
        /// <param name="monitorIndex">대상 모니터 인덱스.</param>
        /// <param name="options">배치 방식과 작업 영역 기준 여부.</param>
        public static DeskMoveResult MoveWindowToMonitor(int monitorIndex, DeskMoveOptions options) {
            SuppressMonitorAutoSave();

            return Monitors.MoveWindowTo(monitorIndex, options);
        }

        #endregion 모니터

        #region 설정 문자열

        /// <summary> 현재 상태를 JSON 문자열로 내보냅니다. 파일 저장은 <see cref="SaveSettings"/> 가 합니다. </summary>
        public static string ExportSettings() {
            return Settings.Export();
        }

        /// <summary> JSON 을 검증한 뒤 전 항목을 적용합니다. </summary>
        /// <param name="json">불러올 JSON 문자열.</param>
        public static DeskImportResult ImportSettings(string json) {
            return Settings.Import(json);
        }

        /// <summary> JSON 에서 지정한 항목만 적용합니다. </summary>
        /// <param name="json">불러올 JSON 문자열.</param>
        /// <param name="options">적용할 항목.</param>
        public static DeskImportResult ImportSettings(string json, DESK_IMPORT_OPTIONS options) {
            return Settings.Import(json, options);
        }

        #endregion 설정 문자열

        #region 프레임

        /// <summary>
        /// <see cref="SetTargetFrameRate"/> 에 넘기면 프레임을 제한하지 않습니다.
        /// 0 은 "지정하지 않음" 이라 다른 뜻이므로 제한 해제에 쓸 수 없습니다.
        /// </summary>
        public const int UNLIMITED_FRAME_RATE = DeskFrameRate.UNLIMITED;

        /// <summary> 기준 프레임을 정합니다. 0 이하면 관리하지 않습니다. </summary>
        /// <param name="targetFrameRate">목표 프레임.</param>
        public static void SetTargetFrameRate(int targetFrameRate) {
            if (!RequireInitialized(nameof(SetTargetFrameRate))) {
                return;
            }

            DeskFrameRate.SetTargetFrameRate(targetFrameRate);
        }

        /// <summary>
        /// 포커스를 잃었을 때 프레임을 낮출지 정합니다. 배경 프레임은 지금 값을 그대로 씁니다.
        /// 게임은 계속 돌아가고 그리는 횟수만 줄어듭니다.
        /// </summary>
        /// <param name="enabled">true 면 절전 사용.</param>
        public static void SetPowerSaving(bool enabled) {
            if (!RequireInitialized(nameof(SetPowerSaving))) {
                return;
            }

            DeskFrameRate.SetPowerSaving(enabled);
        }

        /// <summary> 배경 프레임까지 지정해 절전을 켭니다. </summary>
        /// <param name="enabled">true 면 절전 사용.</param>
        /// <param name="backgroundFrameRate">비활성 상태에서 쓸 프레임. 1 이상.</param>
        public static void SetPowerSaving(bool enabled, int backgroundFrameRate) {
            if (!RequireInitialized(nameof(SetPowerSaving))) {
                return;
            }

            DeskFrameRate.SetPowerSaving(enabled, backgroundFrameRate);
        }

        /// <summary> 수직 동기화를 전환합니다. </summary>
        /// <param name="enabled">true 면 VSync 사용.</param>
        public static void SetVSync(bool enabled) {
            if (!RequireInitialized(nameof(SetVSync))) {
                return;
            }

            DeskFrameRate.SetVSync(enabled);
        }

        /// <summary> 포커스를 잃었을 때 쓸 프레임. </summary>
        public static int BackgroundFrameRate => DeskFrameRate.BackgroundTarget;

        /// <summary> 백그라운드 절전을 쓰는 중인지 여부. </summary>
        public static bool IsPowerSavingEnabled => DeskFrameRate.IsPowerSavingEnabled;

        /// <summary> 수직 동기화가 켜져 있는지 여부. </summary>
        public static bool IsVSyncEnabled => DeskFrameRate.IsVSyncEnabled;

        /// <summary> 창이 활성 상태인지 여부. </summary>
        public static bool HasFocus => DeskFrameRate.HasFocus;

        /// <summary>
        /// 창이 놓인 모니터의 배율. 1.0 이 100% 입니다.
        /// Constant Pixel Size 캔버스는 이 값을 곱해야 물리 크기가 유지됩니다.
        /// </summary>
        public static float CurrentDpiScale => DeskDpiScale.Current;

        /// <summary>
        /// 씬의 모든 Canvas Scaler 에 현재 배율을 반영합니다.
        /// 배율을 바꾸면 안 되는 캔버스가 섞여 있을 수 있으므로, 기본은 DeskDpiScaleBinder 컴포넌트를 쓰는 쪽입니다.
        /// </summary>
        /// <returns>반영한 캔버스 수.</returns>
        public static int ApplyDpiScaleToAllCanvases() {
            if (!RequireInitialized(nameof(ApplyDpiScaleToAllCanvases))) {
                return 0;
            }

            return DeskDpiScale.ApplyToAllCanvasScalers();
        }

        /// <summary> 라이브러리가 프레임을 관리하는 중인지 여부. </summary>
        public static bool IsFrameRateManaged => DeskFrameRate.IsManaging;

        /// <summary> 관리 중인 기준 프레임. </summary>
        public static int TargetFrameRate => DeskFrameRate.TargetFrameRate;

        #endregion 프레임


        #region 플랫폼 지원 여부

        /// <summary>
        /// 모니터·해상도 조회가 가능한 환경인지 여부. 읽기 전용 동작이라 에디터에서도 true 가 될 수 있습니다.
        /// </summary>
        public static bool IsSupported => DeskPlatform.IsSupported;

        /// <summary>
        /// 창 제어(테두리, 최상위, 투명, 이동 등)가 가능한 환경인지 여부.
        /// 에디터에서 허용하면 Unity 에디터 창 자체가 변형되므로 빌드에서만 true 입니다.
        /// </summary>
        public static bool IsWindowControlEnabled => DeskPlatform.IsWindowControlEnabled;

        /// <summary>
        /// 바탕화면 게임이 쓰면 안 되는 멤버를 막는다.
        /// 창 구성은 초기화가 정하므로 게임이 중간에 바꾸면 매 프레임 보정과 싸운다.
        /// </summary>
        /// <param name="memberName">막을 멤버 이름. 로그에 그대로 실린다.</param>
        private static bool RequireNotDesktopGame(string memberName) {
            if (!IsDesktopGame) {
                return true;
            }

            LogUnavailable(memberName);
            return false;
        }

        private static bool CanControlWindow(DESK_WINDOW_FEATURE feature) {
            return RequireFeature(feature) && IsWindowControlEnabled && WindowHandle != IntPtr.Zero;
        }

        /// <summary>
        /// 창 상태를 읽기만 하는 기능에 쓰는 검사입니다.
        /// 읽기는 에디터에서도 안전하므로 창 제어 조건(<see cref="IsWindowControlEnabled"/>)을 요구하지 않습니다.
        /// </summary>
        private static bool CanQueryWindow(DESK_WINDOW_FEATURE feature) {
            return RequireFeature(feature) && DeskPlatform.IsSupported && WindowHandle != IntPtr.Zero;
        }

        #endregion 플랫폼 지원 여부

        #region 창 핸들

        private static IntPtr _windowHandle = IntPtr.Zero;

        /// <summary>
        /// 이 프로세스의 메인 창 핸들. 최초 접근 시 한 번만 탐색해 캐싱합니다.
        /// </summary>
        internal static IntPtr WindowHandle {
            get {
                if (_windowHandle == IntPtr.Zero && DeskPlatform.IsSupported) {
                    _windowHandle = DeskPlatform.FindOwnWindowHandle();
                }

                return _windowHandle;
            }
        }

        /// <summary> 창 핸들만 확보한다. 기능 선언은 하지 않는다. </summary>
        private static bool Initialize() {
            return WindowHandle != IntPtr.Zero;
        }

        /// <summary> 캐싱된 창 핸들을 버린다. 창을 다시 만든 경우에만 필요하다. </summary>
        private static void InvalidateWindowHandle() {
            _windowHandle = IntPtr.Zero;
        }

        #endregion 창 핸들

        #region 창 외형

        /// <summary> 제목 표시줄과 테두리가 제거된 상태인지 여부. </summary>
        public static bool IsBorderless => DeskPlatform.IsBorderless(WindowHandle);

        /// <summary> 작업표시줄과 Alt+Tab 목록에 이 창이 보이는지 여부. </summary>
        public static bool IsTaskbarButtonVisible => DeskPlatform.IsTaskbarButtonVisible(WindowHandle);

        /// <summary>
        /// 제목 표시줄과 테두리를 제거하거나 되돌립니다.
        /// </summary>
        /// <param name="borderless">true 면 테두리 없는 창, false 면 일반 창.</param>
        public static void SetBorderless(bool borderless) {
            // 테두리를 되살리면 매 프레임 보정(RefreshDesktopWindow)과 싸워 창이 떨린다.
            if (!RequireNotDesktopGame(nameof(SetBorderless))) {
                return;
            }

            ApplyBorderless(borderless);
        }

        /// <summary>
        /// 테두리를 실제로 적용한다. 표시 방식 전환처럼 라이브러리가 스스로 부르는 경로가 쓴다.
        /// 공개 <see cref="SetBorderless"/> 의 바탕화면 차단을 거치지 않는다.
        /// </summary>
        internal static void ApplyBorderless(bool borderless) {
            if (!CanControlWindow(DESK_WINDOW_FEATURE.BORDERLESS)) {
                return;
            }

            DeskPlatform.SetBorderless(WindowHandle, borderless);
        }

        /// <summary>
        /// 드래그로 창 크기를 바꿀 수 있는지 전환합니다. 최대화 버튼도 함께 따라갑니다.
        /// 초기화는 이 값을 건드리지 않습니다. 저장 파일이 있으면 그 값으로 복원되고, 없으면
        /// PlayerSettings 의 Resizable Window 로 시작합니다. 이 호출은 그 위에 덮어씁니다.
        /// </summary>
        /// <param name="resizable">true 면 사용자가 크기를 바꿀 수 있습니다.</param>
        public static void SetResizable(bool resizable) {
            // 크기 조절을 켜면 RefreshResizable 이 매 프레임 테두리를 되살려 바탕화면 보정과 싸운다.
            if (!RequireNotDesktopGame(nameof(SetResizable))) {
                return;
            }

            if (!CanControlWindow(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)) {
                return;
            }

            ApplyResizable(resizable);
        }

        /// <summary>
        /// 요청값을 기억하고 창 모드일 때만 실제로 적용한다.
        /// 전체화면 창에 테두리 비트를 붙이면 프레임이 생겨 화면 가장자리에 간격이 남는다.
        /// </summary>
        private static void ApplyResizable(bool resizable) {
            IsResizableRequested = resizable;

            if (IsWindowedNow()) {
                DeskPlatform.SetResizable(WindowHandle, resizable);
            }
        }

        /// <summary>
        /// 요청값으로 창 크기 조절 상태를 다시 건다. 매 프레임 불린다.
        /// 유니티가 창을 다시 구성하는 시점이 정해져 있지 않아 한 번만 되돌려서는 다시 덮이기 때문이다.
        /// </summary>
        internal static void RefreshResizable() {
            if (!IsInitialized
                || !IsFeatureEnabled(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)
                || !IsWindowControlEnabled
                || WindowHandle == IntPtr.Zero
                || !IsWindowedNow()) {
                return;
            }

            if (DeskPlatform.IsResizable(WindowHandle) == IsResizableRequested) {
                return;
            }

            DeskPlatform.SetResizable(WindowHandle, IsResizableRequested);
        }

        /// <summary>
        /// 창을 항상 다른 창 위에 표시할지 전환합니다.
        /// </summary>
        /// <param name="topMost">true 면 최상위 고정, false 면 일반 Z-order.</param>
        public static void SetTopMost(bool topMost) {
            if (!CanControlWindow(DESK_WINDOW_FEATURE.TOP_MOST)) {
                return;
            }

            IsTopMostRequested = topMost;
            DeskPlatform.SetTopMost(WindowHandle, topMost && IsTopMostAllowedNow());

            // 최상위면 작업표시줄을 덮을 수 있어 기준 영역이 달라진다. 그만큼 창을 다시 맞춘다.
            FitWindowToMonitor(Monitors.CurrentIndex);
        }

        /// <summary>
        /// OS 창에 최상위 플래그가 실제로 걸려 있는지 조회합니다.
        /// <see cref="IsTopMostRequested"/> 는 게임이 요청한 값이라 전체화면 동안에는 서로 다를 수 있습니다.
        /// </summary>
        public static bool IsTopMostApplied() {
            if (!CanQueryWindow(DESK_WINDOW_FEATURE.TOP_MOST)) {
                return false;
            }

            return DeskPlatform.IsTopMost(WindowHandle);
        }

        /// <summary>
        /// 요청값과 실제 창 상태가 다르면 다시 건다. 매 프레임 불린다.
        /// 유니티가 표시 방식을 실제로 바꾸는 시점이 정해져 있지 않아, 한 번만 시도하면 놓친다.
        /// 전체화면을 다녀와도 옵션에서 켠 최상위가 살아나야 하기 때문이다.
        /// </summary>
        internal static void RefreshTopMost() {
            if (!IsInitialized
                || !IsFeatureEnabled(DESK_WINDOW_FEATURE.TOP_MOST)
                || !IsWindowControlEnabled
                || WindowHandle == IntPtr.Zero) {
                return;
            }

            bool desired = IsTopMostRequested && IsTopMostAllowedNow();

            if (DeskPlatform.IsTopMost(WindowHandle) == desired) {
                return;
            }

            DeskPlatform.SetTopMost(WindowHandle, desired);
        }

        /// <summary> 전체화면 계열에서 최상위를 걸면 Alt+Tab 으로 전환한 창이 가려지므로 창모드에서만 허용한다. </summary>
        private static bool IsTopMostAllowedNow() {
            return IsWindowedNow();
        }

        /// <summary> 유니티가 창 모드로 두고 있는지 여부. 테두리를 건드려도 되는 상태인지 판단한다. </summary>
        private static bool IsWindowedNow() {
            return Screen.fullScreenMode == FullScreenMode.Windowed;
        }

        /// <summary>
        /// 작업표시줄에 이 창의 버튼을 노출할지 전환합니다.
        /// 내부적으로 창을 잠깐 숨겼다 다시 표시하므로 최상위 설정보다 먼저 호출하십시오.
        /// </summary>
        /// <param name="visible">true 면 작업표시줄에 표시, false 면 숨김.</param>
        public static void SetTaskbarButtonVisible(bool visible) {
            if (!CanControlWindow(DESK_WINDOW_FEATURE.TASKBAR_BUTTON)) {
                return;
            }

            DeskPlatform.SetTaskbarButtonVisible(WindowHandle, visible);
        }

        /// <summary>
        /// 마우스를 게임 창 안에 가둘지 전환합니다.
        /// 포커스를 잃으면 OS 가 가둠을 풀고, 창으로 돌아오면 다시 걸립니다.
        /// </summary>
        /// <param name="confined">true 면 창 밖으로 못 나갑니다.</param>
        public static void SetCursorConfined(bool confined) {
            if (!RequireFeature(DESK_WINDOW_FEATURE.CURSOR_CONFINE)) {
                return;
            }

            IsCursorConfined = confined;
            ApplyCursorConfine();
        }

        /// <summary> 포커스를 되찾았을 때 가둠을 다시 건다. OS 가 포커스를 잃으면 풀어 버리기 때문이다. </summary>
        internal static void RefreshCursorConfine() {
            if (!IsInitialized || !IsFeatureEnabled(DESK_WINDOW_FEATURE.CURSOR_CONFINE)) {
                return;
            }

            ApplyCursorConfine();
        }

        /// <summary>
        /// 커서가 창 밖으로 자유롭게 나가도록 잠금을 전부 푼다.
        /// 바탕화면 게임은 화면 전체를 덮으므로, 가둬 두면 다른 창이나 바탕화면을 쓸 수 없다.
        /// Confined 뿐 아니라 Locked 도 함께 푼다.
        /// </summary>
        private static void ReleaseDesktopCursor() {
            IsCursorConfined = false;

            if (Cursor.lockState != CursorLockMode.None) {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        /// <summary> 가둠을 풀 때 다른 잠금 모드를 건드리지 않도록 Confined 였을 때만 되돌린다. </summary>
        private static void ApplyCursorConfine() {
            if (IsCursorConfined) {
                Cursor.lockState = CursorLockMode.Confined;
                return;
            }

            if (Cursor.lockState == CursorLockMode.Confined) {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        #endregion 창 외형

        #region 입력

        /// <summary>
        /// 가상 데스크탑 좌표 기준 마우스 커서 위치. 모니터가 여러 대여도 이어진 좌표계로 반환됩니다.
        /// 다른 기능 없이 좌표만 쓰는 경우가 있어 기능 선언 없이 호출할 수 있지만, 초기화는 필요합니다.
        /// </summary>
        public static Vector2Int GetCursorPositionOnDesktop() {
            if (!IsInitialized || !DeskPlatform.IsSupported) {
                return Vector2Int.zero;
            }

            return DeskPlatform.TryGetCursorPosition(out Vector2Int position) ? position : Vector2Int.zero;
        }

        #endregion 입력

        #region 창 위치와 크기

        /// <summary>
        /// 가상 데스크탑 좌표 기준 현재 창 영역.
        /// 조회에 실패하면 Unity 가 보고하는 화면 크기로 대체합니다.
        /// </summary>
        public static RectInt GetWindowRect() {
            if (!CanQueryWindow(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)
                || !DeskPlatform.TryGetWindowRect(WindowHandle, out RectInt rect)) {
                return new RectInt(0, 0, Screen.width, Screen.height);
            }

            return rect;
        }

        /// <summary>
        /// 창 좌상단 위치를 옮깁니다. 크기와 Z-order 는 유지됩니다.
        /// </summary>
        internal static void SetPosition(int x, int y) {
            if (!CanControlWindow(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)) {
                return;
            }

            DeskPlatform.SetPosition(WindowHandle, x, y);
        }

        /// <summary>
        /// 창 크기를 바꿉니다. 위치와 Z-order 는 유지됩니다.
        /// </summary>
        internal static void SetSize(int width, int height) {
            if (!CanControlWindow(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)) {
                return;
            }

            DeskPlatform.SetSize(WindowHandle, width, height);
        }

        /// <summary>
        /// 창의 위치와 크기를 한 번에 지정합니다.
        /// </summary>
        internal static void SetWindowRect(int x, int y, int width, int height) {
            if (!CanControlWindow(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)) {
                return;
            }

            DeskPlatform.SetWindowRect(WindowHandle, x, y, width, height);
        }

        /// <summary>
        /// 창 모드에서 화면에 다 들어가는 크기로 줄여 돌려줍니다. 들어가면 요청값 그대로입니다.
        /// 가로 세로를 따로 자르지 않고 하나의 배율로 함께 줄여 비율을 지킵니다.
        /// </summary>
        /// <param name="size">그림 영역 기준 요청 크기.</param>
        /// <param name="mode">적용할 표시 방식.</param>
        internal static Vector2Int FitToScreen(Vector2Int size, DESK_DISPLAY_MODE mode) {
            if (!IsWindowedMode(mode) || !Monitors.TryGetCurrent(out DeskMonitorInfo monitor)) {
                return size;
            }

            return FitToMonitor(size, mode, monitor);
        }

        /// <summary>
        /// 창을 지정한 모니터에 비율을 지킨 크기로 놓습니다.
        /// 모니터를 옮기면 작업 영역이 달라지므로 그때마다 다시 맞춰야 합니다.
        /// </summary>
        /// <param name="monitorIndex">대상 모니터. 음수면 창이 지금 놓인 모니터.</param>
        internal static void FitWindowToMonitor(int monitorIndex) {
            if (IsDesktopGame) {
                FitWindowToDesktop(monitorIndex);
                return;
            }

            DESK_DISPLAY_MODE mode = DisplayMode.Current;

            if (!IsWindowedMode(mode) || !IsFeatureEnabled(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)
                || !IsWindowControlEnabled || WindowHandle == IntPtr.Zero) {
                return;
            }

            bool found = monitorIndex >= 0
                ? Monitors.TryGetAt(monitorIndex, out DeskMonitorInfo target)
                : Monitors.TryGetCurrent(out target);

            if (!found) {
                return;
            }

            DeskResolution chosen = Resolution.GetApplied();
            Vector2Int fitted = FitToMonitor(new Vector2Int(chosen.Width, chosen.Height), mode, target);

            if (!TryGetChrome(mode, out int chromeWidth, out int chromeHeight)) {
                chromeWidth = 0;
                chromeHeight = 0;
            }

            RectInt area = GetReferenceArea(target);
            int width = fitted.x + chromeWidth;
            int height = fitted.y + chromeHeight;

            // 이미 크기가 맞고 영역 안에 온전히 들어와 있으면 건드리지 않는다.
            // 그러지 않으면 창을 옮길 때마다 가운데로 튄다.
            if (DeskPlatform.TryGetWindowRect(WindowHandle, out RectInt current)
                && current.width == width && current.height == height
                && current.x >= area.x && current.y >= area.y
                && current.xMax <= area.xMax && current.yMax <= area.yMax) {
                return;
            }

            SetWindowRect(area.x + (area.width - width) / 2, area.y + (area.height - height) / 2, width, height);
        }

        /// <summary>
        /// 바탕화면 게임의 창을 모니터 전체에 맞춘다.
        /// 작업표시줄을 뺀 작업 영역에 맞추면 창이 줄어들고 가운데로 몰리므로 전체 영역을 쓴다.
        /// </summary>
        /// <param name="monitorIndex">맞출 모니터 인덱스. 음수면 창이 놓인 모니터.</param>
        private static void FitWindowToDesktop(int monitorIndex) {
            if (!IsFeatureEnabled(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)
                || !IsWindowControlEnabled || WindowHandle == IntPtr.Zero) {
                return;
            }

            bool found = monitorIndex >= 0
                ? Monitors.TryGetAt(monitorIndex, out DeskMonitorInfo target)
                : Monitors.TryGetCurrent(out target);

            if (!found) {
                return;
            }

            RectInt bounds = target.Bounds;

            if (DeskPlatform.TryGetWindowRect(WindowHandle, out RectInt current)
                && current.x == bounds.x && current.y == bounds.y
                && current.width == bounds.width && current.height == bounds.height) {
                return;
            }

            DeskPlatform.SetWindowRect(WindowHandle, bounds.x, bounds.y, bounds.width, bounds.height);
        }

        /// <summary> 창 모드 계열인지 여부. 전체화면은 유니티가 크기를 정하므로 손대지 않는다. </summary>
        private static bool IsWindowedMode(DESK_DISPLAY_MODE mode) {
            return mode == DESK_DISPLAY_MODE.WINDOWED || mode == DESK_DISPLAY_MODE.BORDERLESS_WINDOWED;
        }

        /// <summary> 최상위 창만 작업표시줄을 덮을 수 있으므로 기준 영역이 갈린다. </summary>
        private static RectInt GetReferenceArea(DeskMonitorInfo monitor) {
            return IsTopMostRequested ? monitor.Bounds : monitor.WorkArea;
        }

        /// <summary>
        /// 지금 창이 아니라 바뀐 뒤의 스타일로 테두리 두께를 구한다.
        /// 전체화면에서 창 모드로 갈 때 지금 두께를 쓰면 제목 표시줄이 빠져 창이 다시 넘친다.
        /// </summary>
        private static bool TryGetChrome(DESK_DISPLAY_MODE mode, out int width, out int height) {
            bool hasCaption = mode == DESK_DISPLAY_MODE.WINDOWED;

            return DeskPlatform.TryGetWindowChrome(WindowHandle, hasCaption, hasCaption && IsResizableRequested,
                                                   out width, out height);
        }

        /// <summary> 가로 세로를 따로 자르지 않고 하나의 배율로 함께 줄여 비율을 지킨다. </summary>
        private static Vector2Int FitToMonitor(Vector2Int size, DESK_DISPLAY_MODE mode, DeskMonitorInfo monitor) {
            if (size.x <= 0 || size.y <= 0 || !IsFeatureEnabled(DESK_WINDOW_FEATURE.WINDOW_PLACEMENT)) {
                return size;
            }

            if (!TryGetChrome(mode, out int chromeWidth, out int chromeHeight)) {
                chromeWidth = 0;
                chromeHeight = 0;
            }

            RectInt area = GetReferenceArea(monitor);
            int roomWidth = area.width - chromeWidth;
            int roomHeight = area.height - chromeHeight;

            if (roomWidth <= 0 || roomHeight <= 0) {
                return size;
            }

            float scale = Mathf.Min(1f, roomWidth / (float)size.x, roomHeight / (float)size.y);

            if (scale >= 1f) {
                return size;
            }

            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(size.x * scale)),
                                  Mathf.Max(1, Mathf.RoundToInt(size.y * scale)));
        }

        #endregion 창 위치와 크기



    }
}
