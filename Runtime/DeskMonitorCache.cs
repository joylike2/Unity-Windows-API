using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 모니터 열거 결과를 담아 두는 캐시. 모니터 목록이 필요한 서비스들이 함께 쓴다. </summary>
    internal static class DeskMonitorCache {

        private static readonly List<DeskMonitorInfo> MONITORS = new List<DeskMonitorInfo>();

        private static bool _isReady;
        private static Exception _enumError;

        /// <summary> 마지막 열거에서 발생한 예외. 성공했거나 아직 열거하지 않았으면 null </summary>
        internal static Exception LastEnumError => _enumError;

        /// <summary> 모니터 목록. 아직 열거하지 않았으면 이 자리에서 열거한다. </summary>
        internal static IReadOnlyList<DeskMonitorInfo> GetMonitors() {
            if (!_isReady) {
                Refresh();
            }

            return MONITORS;
        }

        /// <summary> 모니터 목록을 다시 열거한다. </summary>
        internal static bool Refresh() {
            MONITORS.Clear();
            _isReady = true;
            _enumError = null;

            if (!DeskPlatform.IsSupported) {
                return false;
            }

            if (!DeskPlatform.TryEnumerateMonitors(MONITORS, out _enumError)) {
                MONITORS.Clear();
                Debug.LogError($"[DeskMonitorCache] 모니터 열거에 실패해 목록을 비웁니다: {_enumError}");
                return false;
            }

            return true;
        }

        /// <summary> 창이 놓인 모니터의 인덱스. 찾지 못하면 -1 </summary>
        internal static int GetCurrentIndex() {
            if (!DeskPlatform.IsSupported) {
                return -1;
            }

            IntPtr handle = WindowDeskAPI.WindowHandle;

            if (handle == IntPtr.Zero) {
                return -1;
            }

            IntPtr currentMonitor = DeskPlatform.GetMonitorFromWindow(handle);
            IReadOnlyList<DeskMonitorInfo> monitors = GetMonitors();

            for (int i = 0; i < monitors.Count; i++) {
                if (monitors[i].Handle == currentMonitor) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary> 모니터 구성 스냅샷. 열거에 실패했으면 무효 스냅샷을 돌려준다. </summary>
        internal static DeskMonitorLayout GetLayout(bool forceRefresh) {
            if (!DeskPlatform.IsSupported) {
                return DeskMonitorLayout.Invalid;
            }

            if (forceRefresh && !Refresh()) {
                return DeskMonitorLayout.Invalid;
            }

            IReadOnlyList<DeskMonitorInfo> monitors = GetMonitors();

            if (_enumError != null) {
                return DeskMonitorLayout.Invalid;
            }

            return new DeskMonitorLayout(monitors, GetCurrentIndex());
        }

        /// <summary> 캐시를 버린다. 다음 조회에서 다시 열거한다. </summary>
        internal static void Invalidate() {
            MONITORS.Clear();
            _isReady = false;
            _enumError = null;
        }
    }
}
