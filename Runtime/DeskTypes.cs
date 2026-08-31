using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 선언하지 않은 기능은 호출해도 실행되지 않고 안내 로그만 남습니다.
    /// </summary>
    [Flags]
    public enum DESK_WINDOW_FEATURE {
        NONE                   = 0,
        BORDERLESS             = 1 << 0,
        TOP_MOST               = 1 << 1,
        TASKBAR_BUTTON         = 1 << 2,
        TRANSPARENT            = 1 << 3,
        CLICK_THROUGH          = 1 << 4,
        WINDOW_PLACEMENT       = 1 << 5,
        MONITOR_INFO           = 1 << 7,
        RESOLUTION_INFO        = 1 << 8,
        DISPLAY_MODE           = 1 << 9,
        CURSOR_CONFINE         = 1 << 10,

        ALL = BORDERLESS | TOP_MOST | TASKBAR_BUTTON | TRANSPARENT | CLICK_THROUGH
              | WINDOW_PLACEMENT | MONITOR_INFO | RESOLUTION_INFO | DISPLAY_MODE | CURSOR_CONFINE,
    }

    /// <summary> 모듈 사용 목적. 선언하면 필요한 기능이 한 번에 켜진다. </summary>
    [Flags]
    public enum DESK_WINDOW_PROFILE {
        NONE    = 0,

        /// <summary> 일반 PC 게임. 해상도와 디스플레이 모드 설정 </summary>
        PC_GAME = 1 << 0,

        /// <summary> 바탕화면 위에서 도는 게임. 테두리없는 창 · 투명 · 클릭 통과가 함께 걸린다 </summary>
        DESKTOP_GAME = 1 << 1,
    }

    /// <summary>
    /// 모니터 전체 영역에서 작업 영역을 뺀 네 변의 두께. 작업표시줄이 차지하는 만큼이다.
    /// 작업표시줄은 아래에만 있는 것이 아니라 위 · 좌 · 우 어디든 올 수 있어 네 변을 모두 담는다.
    /// 단위는 픽셀이며 가상 데스크탑 좌표 기준이다.
    /// </summary>
    public readonly struct DeskEdgeInsets {

        /// <summary> 아무 곳도 가려지지 않은 상태 </summary>
        public static readonly DeskEdgeInsets ZERO = new DeskEdgeInsets(0, 0, 0, 0);

        /// <summary>왼쪽 두께.</summary>
        public int Left { get; }

        /// <summary>위쪽 두께.</summary>
        public int Top { get; }

        /// <summary>오른쪽 두께.</summary>
        public int Right { get; }

        /// <summary>아래쪽 두께.</summary>
        public int Bottom { get; }

        /// <summary>네 변 중 하나라도 가려져 있는지 여부.</summary>
        public bool HasAny => Left > 0 || Top > 0 || Right > 0 || Bottom > 0;

        public DeskEdgeInsets(int left, int top, int right, int bottom) {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary> 모니터 전체 영역과 작업 영역의 차이로 두께를 구한다. </summary>
        /// <param name="bounds">모니터 전체 영역.</param>
        /// <param name="workArea">작업표시줄을 제외한 영역.</param>
        public static DeskEdgeInsets FromBounds(RectInt bounds, RectInt workArea) {
            return new DeskEdgeInsets(
                Mathf.Max(0, workArea.x - bounds.x),
                Mathf.Max(0, workArea.y - bounds.y),
                Mathf.Max(0, bounds.xMax - workArea.xMax),
                Mathf.Max(0, bounds.yMax - workArea.yMax));
        }

        /// <summary> 네 변을 배율로 나눈다. 물리 픽셀을 논리 픽셀로 옮길 때 쓴다. </summary>
        /// <param name="scale">나눌 배율. 0 이하면 그대로 돌려준다.</param>
        public DeskEdgeInsets Divide(float scale) {
            if (scale <= 0f) {
                return this;
            }

            return new DeskEdgeInsets(
                Mathf.RoundToInt(Left / scale),
                Mathf.RoundToInt(Top / scale),
                Mathf.RoundToInt(Right / scale),
                Mathf.RoundToInt(Bottom / scale));
        }

        public override string ToString() {
            return $"좌 {Left} 상 {Top} 우 {Right} 하 {Bottom}";
        }
    }

    /// <summary> 화면 배율을 무엇에 맞출지 </summary>
    public enum DESK_SCALE_BASIS {
        /// <summary>
        /// 윈도우 디스플레이 배율을 그대로 따라간다. 화면에서의 물리적 크기가 일정해진다.
        /// 바탕화면 아이콘 · 창과 크기감이 맞으므로 바탕화면 게임의 기본값이다.
        /// </summary>
        DPI,

        /// <summary>
        /// 실제 세로 해상도를 기준 세로로 나눈 값을 쓴다. 화면에서 차지하는 비율이 일정해진다.
        /// 화면을 독점하는 전체화면 게임에 어울린다.
        /// </summary>
        REFERENCE_RESOLUTION,
    }

    /// <summary> 트레이 메뉴를 어떤 색으로 그릴지 </summary>
    public enum DESK_MENU_THEME {
        /// <summary> 밝은 배경에 검은 글자. 지정하지 않았을 때의 기본값 </summary>
        LIGHT,

        /// <summary> 어두운 배경에 흰 글자 </summary>
        DARK,

        /// <summary> 윈도우 설정을 따라간다 </summary>
        SYSTEM,
    }

    /// <summary>
    /// 창 표시 방식.
    ///
    /// 전용 전체화면(ExclusiveFullScreen)은 제공하지 않습니다. 유니티가 그 모드에서 고른 해상도를
    /// 무시하고 모니터 해상도로 강제해, 유저가 정한 해상도를 지킬 수 없었습니다.
    /// 게임 옵션의 "전체화면" 은 <see cref="FULLSCREEN_WINDOW"/> 로 제공하십시오.
    /// 화면을 꽉 채우면서 고른 해상도를 그대로 지키고, 전환도 더 빠릅니다.
    /// </summary>
    public enum DESK_DISPLAY_MODE {
        /// <summary> 전체화면. 테두리없는 창이 모니터 전체를 덮고 고른 해상도로 그린다 </summary>
        FULLSCREEN_WINDOW,

        /// <summary> 일반 창 </summary>
        WINDOWED,

        /// <summary> 테두리없는 창 </summary>
        BORDERLESS_WINDOWED,
    }

    /// <summary> 창이 놓인 모니터가 사라졌을 때의 대응 방식 </summary>
    public enum DESK_MONITOR_LOST_POLICY {
        /// <summary> 아무것도 하지 않음 </summary>
        KEEP,

        /// <summary> 주 모니터로 이동 </summary>
        MOVE_TO_PRIMARY,

        /// <summary> 가장 가까운 모니터로 이동 </summary>
        MOVE_TO_NEAREST,
    }

    /// <summary>
    /// 모니터 한 대의 정보.
    /// </summary>
    public readonly struct DeskMonitorInfo {
        /// <summary>모니터 핸들 (Windows 는 HMONITOR).</summary>
        public IntPtr Handle { get; }

        /// <summary>디스플레이 장치명 (예: \\.\DISPLAY1).</summary>
        public string DeviceName { get; }

        /// <summary>가상 데스크탑 좌표 기준 전체 영역. 좌상단 원점입니다.</summary>
        public RectInt Bounds { get; }

        /// <summary>작업표시줄을 제외한 작업 영역.</summary>
        public RectInt WorkArea { get; }

        /// <summary>모니터 DPI (Windows 기본 96).</summary>
        public uint Dpi { get; }

        /// <summary>디스플레이 배율. 1.0 이 100% 입니다.</summary>
        public float ScaleFactor { get; }

        /// <summary>주 모니터 여부.</summary>
        public bool IsPrimary { get; }

        public DeskMonitorInfo(IntPtr handle, string deviceName, RectInt bounds, RectInt workArea, uint dpi, bool isPrimary) {
            Handle = handle;
            DeviceName = deviceName;
            Bounds = bounds;
            WorkArea = workArea;
            Dpi = dpi;
            ScaleFactor = dpi / (float)DeskConstants.BASE_DPI;
            IsPrimary = isPrimary;
        }

        public override string ToString() {
            return $"{DeviceName} {Bounds.width}x{Bounds.height} @({Bounds.x},{Bounds.y}) {ScaleFactor * 100:0}%";
        }
    }

    /// <summary>
    /// 모니터 구성 스냅샷. 주 모니터, 보조 모니터 목록, 현재 창이 놓인 모니터를 한 번에 전달합니다.
    /// 조회 시점의 사본이므로 이후 모니터 목록이 갱신되어도 이 값은 변하지 않습니다.
    /// </summary>
    public readonly struct DeskMonitorLayout {

        private static readonly IReadOnlyList<DeskMonitorInfo> EMPTY_MONITORS = new List<DeskMonitorInfo>();
        private static readonly IReadOnlyList<int> EMPTY_ORDER = new List<int>();

        /// <summary>모니터 열거에 성공했는지 여부. false 면 목록이 비어 있습니다.</summary>
        public bool IsValid { get; }

        /// <summary>연결된 모니터 전체 (인덱스 순).</summary>
        public IReadOnlyList<DeskMonitorInfo> All { get; }

        /// <summary>주 모니터를 제외한 나머지.</summary>
        public IReadOnlyList<DeskMonitorInfo> Secondaries { get; }

        /// <summary>주 모니터의 인덱스. 없으면 -1.</summary>
        public int PrimaryIndex { get; }

        /// <summary>게임 창이 놓인 모니터의 인덱스. 찾지 못하면 -1.</summary>
        public int CurrentIndex { get; }

        /// <summary> 화면 배치 왼쪽부터의 모니터 인덱스 순서. OS 열거 순서와 물리 배치는 일치하지 않는다. </summary>
        public IReadOnlyList<int> LeftToRightOrder { get; }

        /// <summary>열거에 실패했음을 나타내는 빈 스냅샷.</summary>
        public static DeskMonitorLayout Invalid => new DeskMonitorLayout(false, EMPTY_MONITORS, EMPTY_MONITORS, EMPTY_ORDER, -1, -1);

        /// <summary>
        /// 모니터 목록으로 스냅샷을 만듭니다. 목록은 사본으로 보관합니다.
        /// </summary>
        /// <param name="monitors">인덱스 순으로 정렬된 모니터 목록.</param>
        /// <param name="currentIndex">게임 창이 놓인 모니터의 인덱스. 모르면 -1.</param>
        public DeskMonitorLayout(IReadOnlyList<DeskMonitorInfo> monitors, int currentIndex) {
            List<DeskMonitorInfo> all = new List<DeskMonitorInfo>(monitors.Count);
            List<DeskMonitorInfo> secondaries = new List<DeskMonitorInfo>(monitors.Count);
            int primaryIndex = -1;

            for (int i = 0; i < monitors.Count; i++) {
                all.Add(monitors[i]);

                if (monitors[i].IsPrimary && primaryIndex < 0) {
                    primaryIndex = i;
                }
                else {
                    secondaries.Add(monitors[i]);
                }
            }

            IsValid = true;
            All = all;
            Secondaries = secondaries;
            PrimaryIndex = primaryIndex;
            CurrentIndex = currentIndex;
            LeftToRightOrder = BuildLeftToRightOrder(all);
        }

        private DeskMonitorLayout(bool isValid, IReadOnlyList<DeskMonitorInfo> all,
                                  IReadOnlyList<DeskMonitorInfo> secondaries, IReadOnlyList<int> leftToRightOrder,
                                  int primaryIndex, int currentIndex) {
            IsValid = isValid;
            All = all;
            Secondaries = secondaries;
            LeftToRightOrder = leftToRightOrder;
            PrimaryIndex = primaryIndex;
            CurrentIndex = currentIndex;
        }

        /// <summary> 가상 데스크탑 좌표로 모니터 인덱스를 왼쪽부터 정렬한다. </summary>
        private static IReadOnlyList<int> BuildLeftToRightOrder(List<DeskMonitorInfo> monitors) {
            List<int> order = new List<int>(monitors.Count);

            for (int i = 0; i < monitors.Count; i++) {
                order.Add(i);
            }

            order.Sort((left, right) => {
                int compared = monitors[left].Bounds.x.CompareTo(monitors[right].Bounds.x);

                if (compared != 0) {
                    return compared;
                }

                compared = monitors[left].Bounds.y.CompareTo(monitors[right].Bounds.y);
                return compared != 0 ? compared : left.CompareTo(right);
            });

            return order;
        }

        /// <summary>주 모니터를 가져옵니다.</summary>
        /// <returns>주 모니터를 찾았으면 true.</returns>
        public bool TryGetPrimary(out DeskMonitorInfo monitor) {
            return TryGetAt(PrimaryIndex, out monitor);
        }

        /// <summary>게임 창이 놓인 모니터를 가져옵니다.</summary>
        /// <returns>현재 모니터를 찾았으면 true.</returns>
        public bool TryGetCurrent(out DeskMonitorInfo monitor) {
            return TryGetAt(CurrentIndex, out monitor);
        }

        private bool TryGetAt(int index, out DeskMonitorInfo monitor) {
            if (!IsValid || index < 0 || index >= All.Count) {
                monitor = default;
                return false;
            }

            monitor = All[index];
            return true;
        }

        public override string ToString() {
            if (!IsValid) {
                return "DeskMonitorLayout(invalid)";
            }

            return $"DeskMonitorLayout(count:{All.Count} primary:{PrimaryIndex} current:{CurrentIndex})";
        }
    }

    /// <summary>
    /// 다른 모니터로 옮길 때 창을 어디에 놓을지.
    /// </summary>
    public enum DESK_MOVE_PLACEMENT {
        /// <summary>대상 영역의 좌상단.</summary>
        TOP_LEFT,

        /// <summary>대상 영역의 중앙.</summary>
        CENTER,

        /// <summary>대상 영역을 가득 채움.</summary>
        FILL,
    }

    /// <summary>
    /// 모니터 이동 옵션.
    /// </summary>
    public readonly struct DeskMoveOptions {
        /// <summary>창을 놓을 위치.</summary>
        public DESK_MOVE_PLACEMENT Placement { get; }

        /// <summary>true 면 작업표시줄을 제외한 영역을 기준으로 삼습니다.</summary>
        public bool UseWorkArea { get; }

        /// <summary>true 면 모니터 DPI 비율만큼 창 크기를 조정해 물리적 크기를 유지합니다.</summary>
        public bool ScaleByDpi { get; }

        public DeskMoveOptions(DESK_MOVE_PLACEMENT placement, bool useWorkArea, bool scaleByDpi) {
            Placement = placement;
            UseWorkArea = useWorkArea;
            ScaleByDpi = scaleByDpi;
        }

        /// <summary>중앙 배치 + 작업 영역 기준 + DPI 배수 적용.</summary>
        public static DeskMoveOptions Default => new DeskMoveOptions(DESK_MOVE_PLACEMENT.CENTER, true, true);

        public override string ToString() {
            return $"{Placement} workArea:{UseWorkArea} scaleByDpi:{ScaleByDpi}";
        }
    }

    /// <summary>
    /// 모니터 이동 결과.
    /// </summary>
    public readonly struct DeskMoveResult {
        /// <summary>이동에 성공했는지 여부.</summary>
        public bool IsSuccess { get; }

        /// <summary>실패 사유. 성공 시 null.</summary>
        public string ErrorMessage { get; }

        /// <summary>이동 전 창이 있던 모니터 인덱스. 모르면 -1.</summary>
        public int FromMonitorIndex { get; }

        /// <summary>이동 대상 모니터 인덱스. 판정 전에 실패했으면 -1.</summary>
        public int ToMonitorIndex { get; }

        /// <summary>대상 배율 / 출발 배율. 출발 모니터를 모르면 1.</summary>
        public float DpiScaleRatio { get; }

        /// <summary>이동 후 실제 창 영역. 실패 시 기본값.</summary>
        public RectInt WindowRect { get; }

        private DeskMoveResult(bool isSuccess, string errorMessage, int fromMonitorIndex, int toMonitorIndex,
                               float dpiScaleRatio, RectInt windowRect) {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            FromMonitorIndex = fromMonitorIndex;
            ToMonitorIndex = toMonitorIndex;
            DpiScaleRatio = dpiScaleRatio;
            WindowRect = windowRect;
        }

        /// <summary>실패 결과를 만듭니다.</summary>
        /// <param name="errorMessage">실패 사유.</param>
        /// <param name="toMonitorIndex">이동하려던 대상 인덱스. 알 수 없으면 -1.</param>
        public static DeskMoveResult Fail(string errorMessage, int toMonitorIndex = -1) {
            return new DeskMoveResult(false, errorMessage, -1, toMonitorIndex,
                DeskConstants.DEFAULT_DPI_SCALE_RATIO, default);
        }

        /// <summary>성공 결과를 만듭니다.</summary>
        public static DeskMoveResult Success(int fromMonitorIndex, int toMonitorIndex, float dpiScaleRatio, RectInt windowRect) {
            return new DeskMoveResult(true, null, fromMonitorIndex, toMonitorIndex, dpiScaleRatio, windowRect);
        }

        public override string ToString() {
            if (!IsSuccess) {
                return $"DeskMoveResult(failed: {ErrorMessage})";
            }

            return $"DeskMoveResult({FromMonitorIndex} -> {ToMonitorIndex}  dpi x{DpiScaleRatio:0.###}  " +
                   $"rect:({WindowRect.x},{WindowRect.y}) {WindowRect.width}x{WindowRect.height})";
        }
    }

    /// <summary>
    /// 디스플레이가 지원하는 해상도 모드 한 개.
    /// </summary>
    public readonly struct DeskResolution : IEquatable<DeskResolution> {
        /// <summary>가로 픽셀 수.</summary>
        public int Width { get; }

        /// <summary>세로 픽셀 수.</summary>
        public int Height { get; }

        /// <summary>주사율 (Hz). 알 수 없으면 0.</summary>
        public int RefreshRate { get; }

        public DeskResolution(int width, int height, int refreshRate) {
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
        }

        public bool Equals(DeskResolution other) {
            return Width == other.Width && Height == other.Height && RefreshRate == other.RefreshRate;
        }

        public override bool Equals(object obj) {
            return obj is DeskResolution other && Equals(other);
        }

        public override int GetHashCode() {
            return (Width * 397 ^ Height) * 397 ^ RefreshRate;
        }

        public override string ToString() {
            return $"{Width} x {Height} @ {RefreshRate}Hz";
        }
    }

    /// <summary> 설정을 불러올 때 적용할 항목 </summary>
    [Flags]
    public enum DESK_IMPORT_OPTIONS {
        NONE            = 0,
        RESOLUTION      = 1 << 0,
        DISPLAY_MODE    = 1 << 1,
        MONITOR         = 1 << 2,
        TOP_MOST        = 1 << 3,
        FRAME_RATE      = 1 << 5,
        RESIZABLE       = 1 << 6,
        CURSOR_CONFINE  = 1 << 7,

        ALL = RESOLUTION | DISPLAY_MODE | MONITOR | TOP_MOST | FRAME_RATE | RESIZABLE | CURSOR_CONFINE,

        /// <summary> 해상도와 표시 방식만. 옵션 UI 의 화면 설정 탭에서 사용 </summary>
        SCREEN_ONLY = RESOLUTION | DISPLAY_MODE,
    }

    /// <summary> 해상도 적용 결과 </summary>
    public readonly struct DeskResolutionApplyResult {
        /// <summary> 적용에 성공했는지 여부 </summary>
        public bool IsSuccess { get; }

        /// <summary> 실패 사유. 성공 시 null </summary>
        public string ErrorMessage { get; }

        /// <summary> 호출한 쪽이 요청한 해상도 </summary>
        public DeskResolution Requested { get; }

        /// <summary> 실제로 적용된 해상도 </summary>
        public DeskResolution Applied { get; }

        /// <summary> 지원하지 않는 값이라 다른 해상도로 대체했는지 여부 </summary>
        public bool WasSubstituted { get; }

        /// <summary> 함께 적용된 창 표시 방식 </summary>
        public DESK_DISPLAY_MODE DisplayMode { get; }

        private DeskResolutionApplyResult(bool isSuccess, string errorMessage, DeskResolution requested,
                                          DeskResolution applied, bool wasSubstituted, DESK_DISPLAY_MODE displayMode) {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Requested = requested;
            Applied = applied;
            WasSubstituted = wasSubstituted;
            DisplayMode = displayMode;
        }

        /// <summary> 실패 결과를 만든다. </summary>
        public static DeskResolutionApplyResult Fail(string errorMessage, DeskResolution requested) {
            return new DeskResolutionApplyResult(false, errorMessage, requested, default, false, DESK_DISPLAY_MODE.WINDOWED);
        }

        /// <summary> 성공 결과를 만든다. </summary>
        public static DeskResolutionApplyResult Success(DeskResolution requested, DeskResolution applied,
                                                        DESK_DISPLAY_MODE displayMode) {
            return new DeskResolutionApplyResult(true, null, requested, applied, !requested.Equals(applied), displayMode);
        }

        public override string ToString() {
            if (!IsSuccess) {
                return $"DeskResolutionApplyResult(failed: {ErrorMessage})";
            }

            return WasSubstituted
                ? $"DeskResolutionApplyResult({Requested} -> {Applied} 대체, {DisplayMode})"
                : $"DeskResolutionApplyResult({Applied}, {DisplayMode})";
        }
    }

    /// <summary> 저장값을 그대로 쓰지 못해 다른 값으로 대체한 항목 한 건 </summary>
    public readonly struct DeskSettingSubstitution {
        /// <summary> 대체된 설정 이름 </summary>
        public string Field { get; }

        /// <summary> 저장되어 있던 값 </summary>
        public string SavedValue { get; }

        /// <summary> 실제로 적용한 값 </summary>
        public string AppliedValue { get; }

        public DeskSettingSubstitution(string field, string savedValue, string appliedValue) {
            Field = field;
            SavedValue = savedValue;
            AppliedValue = appliedValue;
        }

        public override string ToString() {
            return $"{Field} : {SavedValue} -> {AppliedValue}";
        }
    }

    /// <summary> 설정 불러오기 결과 </summary>
    public readonly struct DeskImportResult {

        private static readonly IReadOnlyList<DeskSettingSubstitution> EMPTY_SUBSTITUTIONS = new List<DeskSettingSubstitution>();

        /// <summary> 적용에 성공했는지 여부 </summary>
        public bool IsSuccess { get; }

        /// <summary> 실패 사유. 성공 시 null </summary>
        public string ErrorMessage { get; }

        /// <summary> 실제로 적용한 설정 개수 </summary>
        public int AppliedCount { get; }

        /// <summary> 다른 값으로 대체한 항목 목록. 옵션 UI 안내에 사용 </summary>
        public IReadOnlyList<DeskSettingSubstitution> Substitutions { get; }

        private DeskImportResult(bool isSuccess, string errorMessage, int appliedCount,
                                 IReadOnlyList<DeskSettingSubstitution> substitutions) {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            AppliedCount = appliedCount;
            Substitutions = substitutions;
        }

        /// <summary> 실패 결과를 만든다. 아무것도 적용하지 않은 상태다. </summary>
        public static DeskImportResult Fail(string errorMessage) {
            return new DeskImportResult(false, errorMessage, 0, EMPTY_SUBSTITUTIONS);
        }

        /// <summary> 성공 결과를 만든다. </summary>
        public static DeskImportResult Success(int appliedCount, IReadOnlyList<DeskSettingSubstitution> substitutions) {
            return new DeskImportResult(true, null, appliedCount, substitutions ?? EMPTY_SUBSTITUTIONS);
        }

        public override string ToString() {
            if (!IsSuccess) {
                return $"DeskImportResult(failed: {ErrorMessage})";
            }

            return $"DeskImportResult(applied:{AppliedCount} substituted:{Substitutions.Count})";
        }
    }

    /// <summary> 초기화 시점의 창 원본 상태. 종료할 때 이 값으로 되돌린다. </summary>
    internal readonly struct DeskWindowStyleSnapshot {
        /// <summary> 값을 실제로 읽어왔는지 여부 </summary>
        public bool IsValid { get; }

        /// <summary> 창 스타일 (Windows GWL_STYLE) </summary>
        public int Style { get; }

        /// <summary> 확장 창 스타일 (Windows GWL_EXSTYLE) </summary>
        public int ExtendedStyle { get; }

        /// <summary> 가상 데스크탑 좌표 기준 창 영역 </summary>
        public RectInt Rect { get; }

        /// <summary> 최상위 표시 상태였는지 여부 </summary>
        public bool WasTopMost { get; }

        public DeskWindowStyleSnapshot(int style, int extendedStyle, RectInt rect, bool wasTopMost) {
            IsValid = true;
            Style = style;
            ExtendedStyle = extendedStyle;
            Rect = rect;
            WasTopMost = wasTopMost;
        }

        public override string ToString() {
            return IsValid
                ? $"style:0x{Style:X} exStyle:0x{ExtendedStyle:X} rect:({Rect.x},{Rect.y}) {Rect.width}x{Rect.height}"
                : "DeskWindowStyleSnapshot(invalid)";
        }
    }

    /// <summary>
    /// 플랫폼 구현이 공유하는 상수.
    /// </summary>
    internal static class DeskConstants {
        /// <summary>배율 1.0 에 해당하는 기준 DPI.</summary>
        public const uint BASE_DPI = 96;

        /// <summary>출발 모니터를 알 수 없을 때 사용할 DPI 배수.</summary>
        public const float DEFAULT_DPI_SCALE_RATIO = 1f;

        /// <summary> 현재 창이 놓인 모니터를 뜻하는 인덱스 </summary>
        public const int CURRENT_MONITOR = -1;
    }
}
