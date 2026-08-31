using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 선언되지 않았거나 지원하지 않을 때 대신 쓰는 구현. 아무 일도 하지 않고 기본값만 돌려준다. </summary>
    internal sealed class DeskNullService :
        IDeskResolutionService,
        IDeskDisplayModeService,
        IDeskMonitorService,
        IDeskWindowStateService,
        IDeskSettingsService {

        private const string EMPTY_JSON = "{}";
        private const string NOT_AVAILABLE = "기능이 선언되지 않았거나 이 플랫폼에서 지원하지 않습니다.";

        private static readonly IReadOnlyList<DeskResolution> EMPTY_RESOLUTIONS = new List<DeskResolution>();
        private static readonly IReadOnlyList<DeskMonitorInfo> EMPTY_MONITORS = new List<DeskMonitorInfo>();
        private static readonly IReadOnlyList<int> EMPTY_ORDER = new List<int>();
        private static readonly IReadOnlyList<DeskSettingSubstitution> EMPTY_SUBSTITUTIONS = new List<DeskSettingSubstitution>();

        /// <summary> 호출한 쪽이 null 검사를 하지 않아도 되도록 공유하는 인스턴스 </summary>
        internal static readonly DeskNullService INSTANCE = new DeskNullService();

        private DeskNullService() {
        }

        #region IDeskResolutionService

        public IReadOnlyList<DeskResolution> GetSupported() {
            return EMPTY_RESOLUTIONS;
        }

        public IReadOnlyList<DeskResolution> GetSupported(int monitorIndex) {
            return EMPTY_RESOLUTIONS;
        }

        public DeskResolution GetCurrent() {
            return new DeskResolution(Screen.width, Screen.height, 0);
        }

        public DeskResolution GetCurrent(int monitorIndex) {
            return GetCurrent();
        }

        public DeskResolution GetApplied() {
            return GetCurrent();
        }

        public DeskResolutionApplyResult Apply(DeskResolution resolution) {
            return DeskResolutionApplyResult.Fail(NOT_AVAILABLE, resolution);
        }

        public DeskResolutionApplyResult Apply(DeskResolution resolution, DESK_DISPLAY_MODE mode) {
            return DeskResolutionApplyResult.Fail(NOT_AVAILABLE, resolution);
        }

        public bool TryFindNearest(DeskResolution target, int monitorIndex, out DeskResolution nearest) {
            nearest = default;
            return false;
        }

        #endregion IDeskResolutionService

        #region IDeskDisplayModeService

        public DESK_DISPLAY_MODE Current => DESK_DISPLAY_MODE.WINDOWED;

        public bool IsSupported(DESK_DISPLAY_MODE mode) {
            return false;
        }

        public bool Apply(DESK_DISPLAY_MODE mode) {
            return false;
        }

        #endregion IDeskDisplayModeService

        #region IDeskMonitorService

        public IReadOnlyList<DeskMonitorInfo> All => EMPTY_MONITORS;

        public IReadOnlyList<int> LeftToRightOrder => EMPTY_ORDER;

        public int CurrentIndex => -1;

        public int PrimaryIndex => -1;

        public DESK_MONITOR_LOST_POLICY LostPolicy {
            get => DESK_MONITOR_LOST_POLICY.KEEP;
            set { }
        }

        public DeskMonitorLayout GetLayout() {
            return DeskMonitorLayout.Invalid;
        }

        public DeskMonitorLayout GetLayout(bool forceRefresh) {
            return DeskMonitorLayout.Invalid;
        }

        public bool Refresh() {
            return false;
        }

        public bool TryGetCurrent(out DeskMonitorInfo monitor) {
            monitor = default;
            return false;
        }

        public bool TryGetAt(int monitorIndex, out DeskMonitorInfo monitor) {
            monitor = default;
            return false;
        }

        public DeskMoveResult MoveWindowTo(int monitorIndex) {
            return DeskMoveResult.Fail(NOT_AVAILABLE, monitorIndex);
        }

        public DeskMoveResult MoveWindowTo(int monitorIndex, DeskMoveOptions options) {
            return DeskMoveResult.Fail(NOT_AVAILABLE, monitorIndex);
        }

        #endregion IDeskMonitorService

        #region IDeskWindowStateService

        public bool IsTopMost => false;

        public bool IsBorderless => false;

        public bool IsResizable => false;

        public bool IsTaskbarButtonVisible => true;

        public bool IsCursorConfined => false;

        public Vector2Int GetCursorPositionOnDesktop() {
            return Vector2Int.zero;
        }

        public bool SetTopMost(bool topMost) {
            return false;
        }

        public bool SetBorderless(bool borderless) {
            return false;
        }

        public bool SetResizable(bool resizable) {
            return false;
        }

        public bool SetTaskbarButtonVisible(bool visible) {
            return false;
        }

        public bool SetCursorConfined(bool confined) {
            return false;
        }

        #endregion IDeskWindowStateService

        #region IDeskSettingsService

        public string Export() {
            return EMPTY_JSON;
        }

        public DeskImportResult Import(string json) {
            return DeskImportResult.Fail(NOT_AVAILABLE);
        }

        public DeskImportResult Import(string json, DESK_IMPORT_OPTIONS options) {
            return DeskImportResult.Fail(NOT_AVAILABLE);
        }

        #endregion IDeskSettingsService
    }
}
