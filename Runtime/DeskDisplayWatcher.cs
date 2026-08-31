using System.Text;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 모니터 구성이 바뀌었는지 주기적으로 확인하고 알림을 낸다. </summary>
    internal static class DeskDisplayWatcher {

        private const float CHECK_INTERVAL_SECONDS = 1f;
        private const char FIELD_SEPARATOR = '|';
        private const char MONITOR_SEPARATOR = ';';

        private static readonly StringBuilder SIGNATURE_BUILDER = new StringBuilder();

        private static bool _isEnabled;
        private static float _nextCheckTime;
        private static string _lastSignature;
        private static string _lastCurrentDeviceName;
        private static int _lastCurrentIndex = -1;
        private static float _lastCurrentScaleFactor;

        /// <summary> 감시를 켠다. 현재 구성을 기준으로 삼는다. </summary>
        internal static void Enable() {
            _isEnabled = true;
            _nextCheckTime = 0f;

            CaptureBaseline();
        }

        /// <summary> 감시를 끄고 기준값을 버린다. </summary>
        internal static void Disable() {
            _isEnabled = false;
            _lastSignature = null;
            _lastCurrentDeviceName = null;
            _lastCurrentIndex = -1;
            _lastCurrentScaleFactor = 0f;
        }

        /// <summary> 펌프가 매 프레임 부른다. 실제 검사는 정해진 간격으로만 한다. </summary>
        internal static void Tick() {
            if (!_isEnabled || Time.unscaledTime < _nextCheckTime) {
                return;
            }

            _nextCheckTime = Time.unscaledTime + CHECK_INTERVAL_SECONDS;
            CheckConfiguration();
        }

        /// <summary> 구성 서명을 비교해 바뀌었으면 알림을 내고 정책을 적용한다. </summary>
        private static void CheckConfiguration() {
            DeskMonitorLayout current = DeskMonitorCache.GetLayout(true);
            string signature = BuildSignature(current);

            if (signature == _lastSignature) {
                return;
            }

            string previousDeviceName = _lastCurrentDeviceName;
            int previousIndex = _lastCurrentIndex;
            float previousScaleFactor = _lastCurrentScaleFactor;

            _lastSignature = signature;

            bool isCurrentLost = !string.IsNullOrEmpty(previousDeviceName)
                                 && FindDeviceIndex(current, previousDeviceName) < 0;

            if (isCurrentLost) {
                DeskEvents.RaiseCurrentMonitorLost(previousIndex);
                ApplyLostPolicy(current);
                current = DeskMonitorCache.GetLayout(false);
            }
            else {
                HandleDpiChange(current, previousScaleFactor);
            }

            UpdateCurrentMonitorBaseline(current);
            DeskEvents.RaiseDisplayConfigurationChanged(current);
        }

        /// <summary> 모니터를 잃었을 때 창을 어디로 보낼지 정책대로 처리한다. </summary>
        private static void ApplyLostPolicy(DeskMonitorLayout current) {
            IDeskMonitorService monitors = WindowDeskAPI.Monitors;
            DESK_MONITOR_LOST_POLICY policy = monitors.LostPolicy;

            if (policy == DESK_MONITOR_LOST_POLICY.KEEP || !current.IsValid || current.All.Count == 0) {
                return;
            }

            int targetIndex = policy == DESK_MONITOR_LOST_POLICY.MOVE_TO_PRIMARY
                ? current.PrimaryIndex
                : FindNearestIndex(current);

            if (targetIndex < 0) {
                targetIndex = 0;
            }

            DeskMoveResult result = monitors.MoveWindowTo(targetIndex);

            if (!result.IsSuccess) {
                Debug.LogWarning($"[DeskDisplayWatcher] 모니터를 잃은 뒤 창을 옮기지 못했습니다: {result.ErrorMessage}");
            }
        }

        /// <summary> 창 중심에서 가장 가까운 모니터를 찾는다. </summary>
        private static int FindNearestIndex(DeskMonitorLayout layout) {
            RectInt window = WindowDeskAPI.GetWindowRect();
            Vector2 windowCenter = new Vector2(window.x + window.width * 0.5f, window.y + window.height * 0.5f);

            int nearest = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < layout.All.Count; i++) {
                RectInt bounds = layout.All[i].Bounds;
                Vector2 center = new Vector2(bounds.x + bounds.width * 0.5f, bounds.y + bounds.height * 0.5f);
                float distance = (center - windowCenter).sqrMagnitude;

                if (distance >= bestDistance) {
                    continue;
                }

                bestDistance = distance;
                nearest = i;
            }

            return nearest;
        }

        /// <summary> 현재 모니터의 배율이 달라졌으면 배수를 알린다. </summary>
        private static void HandleDpiChange(DeskMonitorLayout current, float previousScaleFactor) {
            if (!current.TryGetCurrent(out DeskMonitorInfo monitor) || monitor.ScaleFactor <= 0f) {
                return;
            }

            if (previousScaleFactor <= 0f || Mathf.Approximately(previousScaleFactor, monitor.ScaleFactor)) {
                return;
            }

            DeskEvents.RaiseDpiScaleChanged(monitor.ScaleFactor / previousScaleFactor);
        }

        private static int FindDeviceIndex(DeskMonitorLayout layout, string deviceName) {
            if (!layout.IsValid) {
                return -1;
            }

            for (int i = 0; i < layout.All.Count; i++) {
                if (layout.All[i].DeviceName == deviceName) {
                    return i;
                }
            }

            return -1;
        }

        private static void CaptureBaseline() {
            DeskMonitorLayout layout = DeskMonitorCache.GetLayout(true);

            _lastSignature = BuildSignature(layout);
            UpdateCurrentMonitorBaseline(layout);
        }

        private static void UpdateCurrentMonitorBaseline(DeskMonitorLayout layout) {
            if (layout.TryGetCurrent(out DeskMonitorInfo monitor)) {
                _lastCurrentDeviceName = monitor.DeviceName;
                _lastCurrentIndex = layout.CurrentIndex;
                _lastCurrentScaleFactor = monitor.ScaleFactor;
                return;
            }

            _lastCurrentDeviceName = null;
            _lastCurrentIndex = -1;
            _lastCurrentScaleFactor = 0f;
        }

        /// <summary> 장치명 · 영역 · DPI 를 이어 붙인 구성 서명. 이 값이 달라지면 구성이 바뀐 것이다. </summary>
        private static string BuildSignature(DeskMonitorLayout layout) {
            if (!layout.IsValid) {
                return string.Empty;
            }

            SIGNATURE_BUILDER.Clear();

            for (int i = 0; i < layout.All.Count; i++) {
                DeskMonitorInfo monitor = layout.All[i];

                SIGNATURE_BUILDER.Append(monitor.DeviceName).Append(FIELD_SEPARATOR)
                    .Append(monitor.Bounds.x).Append(FIELD_SEPARATOR)
                    .Append(monitor.Bounds.y).Append(FIELD_SEPARATOR)
                    .Append(monitor.Bounds.width).Append(FIELD_SEPARATOR)
                    .Append(monitor.Bounds.height).Append(FIELD_SEPARATOR)
                    .Append(monitor.Dpi).Append(MONITOR_SEPARATOR);
            }

            return SIGNATURE_BUILDER.ToString();
        }
    }
}
