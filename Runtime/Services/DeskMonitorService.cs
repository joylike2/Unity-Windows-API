using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 모니터 목록 조회와 창 이동 구현 </summary>
    internal sealed class DeskMonitorService : IDeskMonitorService {

        private static readonly IReadOnlyList<int> EMPTY_ORDER = new List<int>();

        public IReadOnlyList<DeskMonitorInfo> All => DeskMonitorCache.GetMonitors();

        /// <summary> 화면 배치 왼쪽부터의 모니터 인덱스 순서 </summary>
        public IReadOnlyList<int> LeftToRightOrder {
            get {
                DeskMonitorLayout layout = DeskMonitorCache.GetLayout(false);
                return layout.IsValid ? layout.LeftToRightOrder : EMPTY_ORDER;
            }
        }

        public int CurrentIndex => DeskMonitorCache.GetCurrentIndex();

        public int PrimaryIndex => DeskMonitorCache.GetLayout(false).PrimaryIndex;

        /// <summary> 창이 놓인 모니터가 사라졌을 때의 대응 방식 </summary>
        public DESK_MONITOR_LOST_POLICY LostPolicy { get; set; } = DESK_MONITOR_LOST_POLICY.MOVE_TO_PRIMARY;

        public DeskMonitorLayout GetLayout() {
            return DeskMonitorCache.GetLayout(false);
        }

        public DeskMonitorLayout GetLayout(bool forceRefresh) {
            return DeskMonitorCache.GetLayout(forceRefresh);
        }

        public bool Refresh() {
            return DeskMonitorCache.Refresh();
        }

        public bool TryGetCurrent(out DeskMonitorInfo monitor) {
            return TryGetAt(CurrentIndex, out monitor);
        }

        public bool TryGetAt(int monitorIndex, out DeskMonitorInfo monitor) {
            IReadOnlyList<DeskMonitorInfo> monitors = All;

            if (monitorIndex < 0 || monitorIndex >= monitors.Count) {
                monitor = default;
                return false;
            }

            monitor = monitors[monitorIndex];
            return true;
        }

        public DeskMoveResult MoveWindowTo(int monitorIndex) {
            return MoveWindowTo(monitorIndex, DeskMoveOptions.Default);
        }

        /// <summary> 창을 지정한 모니터로 옮긴다. </summary>
        public DeskMoveResult MoveWindowTo(int monitorIndex, DeskMoveOptions options) {
            if (!DeskPlatform.IsWindowControlEnabled) {
                return DeskMoveResult.Fail("이 환경에서는 창을 제어할 수 없습니다.", monitorIndex);
            }

            DeskMonitorLayout layout = DeskMonitorCache.GetLayout(false);

            if (!layout.IsValid) {
                return DeskMoveResult.Fail("모니터 열거에 실패했습니다.", monitorIndex);
            }

            if (monitorIndex < 0 || monitorIndex >= layout.All.Count) {
                return DeskMoveResult.Fail($"모니터 인덱스 {monitorIndex} 가 범위를 벗어났습니다 (0..{layout.All.Count - 1}).", monitorIndex);
            }

            DeskMonitorInfo target = layout.All[monitorIndex];
            float dpiScaleRatio = GetDpiScaleRatio(layout, target);
            RectInt area = options.UseWorkArea ? target.WorkArea : target.Bounds;
            RectInt destination = CalculateDestination(area, dpiScaleRatio, options);

            WindowDeskAPI.SetWindowRect(destination.x, destination.y, destination.width, destination.height);

            RectInt appliedRect = DeskPlatform.TryGetWindowRect(WindowDeskAPI.WindowHandle, out RectInt actual)
                ? actual
                : destination;

            if (!Mathf.Approximately(dpiScaleRatio, DeskConstants.DEFAULT_DPI_SCALE_RATIO)) {
                DeskEvents.RaiseDpiScaleChanged(dpiScaleRatio);
            }

            return DeskMoveResult.Success(layout.CurrentIndex, monitorIndex, dpiScaleRatio, appliedRect);
        }

        /// <summary> 대상 배율 / 출발 배율. 출발 모니터를 찾지 못하면 1 로 두고 경고한다. </summary>
        private static float GetDpiScaleRatio(DeskMonitorLayout layout, DeskMonitorInfo target) {
            if (!layout.TryGetCurrent(out DeskMonitorInfo current) || current.ScaleFactor <= 0f) {
                Debug.LogWarning("[DeskMonitor] 출발 모니터를 확인할 수 없어 DPI 배수를 1 로 둡니다.");
                return DeskConstants.DEFAULT_DPI_SCALE_RATIO;
            }

            return target.ScaleFactor / current.ScaleFactor;
        }

        /// <summary> 대상 영역 안에서 창이 놓일 최종 영역을 계산한다. FILL 은 대상 영역을 그대로 쓴다. </summary>
        private static RectInt CalculateDestination(RectInt area, float dpiScaleRatio, DeskMoveOptions options) {
            if (options.Placement == DESK_MOVE_PLACEMENT.FILL) {
                return area;
            }

            RectInt current = WindowDeskAPI.GetWindowRect();
            int width = options.ScaleByDpi ? Mathf.RoundToInt(current.width * dpiScaleRatio) : current.width;
            int height = options.ScaleByDpi ? Mathf.RoundToInt(current.height * dpiScaleRatio) : current.height;

            width = Mathf.Min(width, area.width);
            height = Mathf.Min(height, area.height);

            if (options.Placement == DESK_MOVE_PLACEMENT.TOP_LEFT) {
                return new RectInt(area.x, area.y, width, height);
            }

            return new RectInt(area.x + (area.width - width) / 2, area.y + (area.height - height) / 2, width, height);
        }
    }
}
