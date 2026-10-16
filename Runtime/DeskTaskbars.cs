using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 플랫폼이 읽어 온 작업표시줄 창 하나. 모니터 인덱스 · 변 · 두께는 <see cref="DeskTaskbars"/> 가 구한다. </summary>
    internal readonly struct DeskTaskbarSample {

        /// <summary>작업표시줄 창이 놓인 모니터 핸들.</summary>
        internal IntPtr MonitorHandle { get; }

        /// <summary>작업표시줄 창 사각형. 자동 숨김으로 숨어 있으면 화면 밖으로 밀려 있다.</summary>
        internal RectInt WindowRect { get; }

        /// <summary>OS 가 알려 준 변. 주 작업표시줄만 알 수 있고 모르면 NONE.</summary>
        internal DESK_TASKBAR_EDGE ReportedEdge { get; }

        /// <summary>자동 숨김이 켜져 있는지.</summary>
        internal bool IsAutoHide { get; }

        /// <summary>주 작업표시줄인지.</summary>
        internal bool IsPrimary { get; }

        internal DeskTaskbarSample(IntPtr monitorHandle, RectInt windowRect, DESK_TASKBAR_EDGE reportedEdge, bool isAutoHide, bool isPrimary) {
            MonitorHandle = monitorHandle;
            WindowRect = windowRect;
            ReportedEdge = reportedEdge;
            IsAutoHide = isAutoHide;
            IsPrimary = isPrimary;
        }
    }

    /// <summary> 작업표시줄을 읽어 모니터 인덱스 · 변 · 두께 · 펼친 자리를 갖춘 정보로 만든다. 부를 때마다 새로 읽는다. </summary>
    internal static class DeskTaskbars {

        private static readonly List<DeskTaskbarSample> SAMPLES = new List<DeskTaskbarSample>();

        private static bool _isReadErrorLogged;
        private static bool _isEdgeMismatchLogged;

        /// <summary>
        /// 모든 작업표시줄을 읽어 <paramref name="buffer"/> 를 채운다. 지원하지 않는 플랫폼이거나 읽지 못하면 비운 채 false.
        /// 감시가 1초마다 부르므로 같은 실패 로그는 다시 성공할 때까지 한 번만 남긴다.
        /// </summary>
        internal static bool TryRead(List<DeskTaskbarInfo> buffer) {
            buffer.Clear();

            if (!DeskPlatform.IsSupported) {
                return false;
            }

            SAMPLES.Clear();

            if (!DeskPlatform.TryReadTaskbars(SAMPLES, out Exception error)) {
                if (!_isReadErrorLogged) {
                    _isReadErrorLogged = true;
                    Debug.LogError($"[DeskTaskbars] 작업표시줄을 읽지 못했습니다: {error}");
                }

                return false;
            }

            _isReadErrorLogged = false;
            IReadOnlyList<DeskMonitorInfo> monitors = DeskMonitorCache.GetMonitors();
            bool isMonitorRefreshed = false;

            for (int i = 0; i < SAMPLES.Count; i++) {
                DeskTaskbarSample sample = SAMPLES[i];
                int monitorIndex = FindMonitorIndex(monitors, sample.MonitorHandle);

                // 모니터 구성이 바뀐 뒤 아직 목록을 다시 읽지 않았으면 한 번만 새로 읽는다
                if (monitorIndex < 0 && !isMonitorRefreshed) {
                    isMonitorRefreshed = true;
                    DeskMonitorCache.Refresh();
                    monitors = DeskMonitorCache.GetMonitors();
                    monitorIndex = FindMonitorIndex(monitors, sample.MonitorHandle);
                }

                if (monitorIndex < 0) {
                    continue;
                }

                if (TryBuild(sample, monitorIndex, monitors[monitorIndex].Bounds, out DeskTaskbarInfo taskbar)) {
                    buffer.Add(taskbar);
                }
            }

            return true;
        }

        /// <summary>
        /// 창 사각형으로 변을 정하고, 사각형으로 알 수 없을 때만 OS 가 알려 준 변을 쓴다.
        /// Windows 11 의 위 · 옆 작업표시줄은 새 기능이라 OS 가 알려 주는 변이 맞는지 확인되지 않아, 실제 창 자리를 먼저 믿는다.
        /// </summary>
        private static bool TryBuild(DeskTaskbarSample sample, int monitorIndex, RectInt monitorBounds, out DeskTaskbarInfo taskbar) {
            DESK_TASKBAR_EDGE edge = DeskTaskbarMath.FindEdge(sample.WindowRect, monitorBounds);

            if (edge == DESK_TASKBAR_EDGE.NONE) {
                edge = sample.ReportedEdge;
            }
            else if (sample.ReportedEdge != DESK_TASKBAR_EDGE.NONE && sample.ReportedEdge != edge && !_isEdgeMismatchLogged) {
                _isEdgeMismatchLogged = true;
                Debug.LogWarning($"[DeskTaskbars] 작업표시줄 창 자리로 구한 변({edge})과 OS 가 알려 준 변({sample.ReportedEdge})이 다릅니다. "
                                 + $"창 자리를 따릅니다. 창 {sample.WindowRect} 모니터 {monitorBounds}");
            }

            if (edge == DESK_TASKBAR_EDGE.NONE) {
                taskbar = default;
                return false;
            }

            int thickness = DeskTaskbarMath.GetThickness(sample.WindowRect, edge);
            RectInt shownBounds = DeskTaskbarMath.GetShownBounds(monitorBounds, edge, thickness);
            taskbar = new DeskTaskbarInfo(monitorIndex, edge, shownBounds, thickness, sample.IsAutoHide, sample.IsPrimary);
            return true;
        }

        private static int FindMonitorIndex(IReadOnlyList<DeskMonitorInfo> monitors, IntPtr monitorHandle) {
            for (int i = 0; i < monitors.Count; i++) {
                if (monitors[i].Handle == monitorHandle) {
                    return i;
                }
            }

            return -1;
        }
    }
}
