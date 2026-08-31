using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 해상도 조회와 적용 구현 </summary>
    internal sealed class DeskResolutionService : IDeskResolutionService {

        private static readonly IReadOnlyList<DeskResolution> EMPTY = new List<DeskResolution>();

        private DeskResolution _applied;
        private bool _hasApplied;

        public IReadOnlyList<DeskResolution> GetSupported() {
            return GetSupported(DeskMonitorCache.GetCurrentIndex());
        }

        /// <summary> 지정한 모니터가 지원하는 해상도. 같은 크기는 가장 높은 주사율 하나만 남기고 큰 것부터 정렬한다. </summary>
        public IReadOnlyList<DeskResolution> GetSupported(int monitorIndex) {
            if (!DeskPlatform.IsSupported) {
                return EMPTY;
            }

            List<DeskResolution> raw = DeskPlatform.GetSupportedResolutions(GetDeviceName(monitorIndex));
            raw.Sort(CompareDescending);

            List<DeskResolution> resolutions = new List<DeskResolution>(raw.Count);

            for (int i = 0; i < raw.Count; i++) {
                if (i > 0 && IsSameSize(raw[i], raw[i - 1])) {
                    continue;
                }

                resolutions.Add(raw[i]);
            }

            return resolutions;
        }

        private static bool IsSameSize(DeskResolution left, DeskResolution right) {
            return left.Width == right.Width && left.Height == right.Height;
        }

        public DeskResolution GetCurrent() {
            return GetCurrent(DeskMonitorCache.GetCurrentIndex());
        }

        public DeskResolution GetCurrent(int monitorIndex) {
            if (!DeskPlatform.IsSupported
                || !DeskPlatform.TryGetCurrentResolution(GetDeviceName(monitorIndex), out DeskResolution resolution)) {
                return new DeskResolution(Screen.width, Screen.height, 0);
            }

            return resolution;
        }

        /// <summary>
        /// 한 번도 바꾸지 않았으면 유니티가 띄운 크기를 목록에 있는 값으로 맞춰 돌려준다.
        /// 창 테두리를 뺀 크기는 목록에 없을 수 있어 그대로 두면 저장값이 어긋난다.
        /// </summary>
        public DeskResolution GetApplied() {
            if (_hasApplied) {
                return _applied;
            }

            return ResolveApplicable(new DeskResolution(Screen.width, Screen.height, GetCurrent().RefreshRate));
        }

        public DeskResolutionApplyResult Apply(DeskResolution resolution) {
            return Apply(resolution, WindowDeskAPI.DisplayMode.Current);
        }

        /// <summary> 해상도와 표시 방식을 함께 적용한다. </summary>
        public DeskResolutionApplyResult Apply(DeskResolution resolution, DESK_DISPLAY_MODE mode) {
            if (resolution.Width <= 0 || resolution.Height <= 0) {
                return DeskResolutionApplyResult.Fail($"해상도 값이 올바르지 않습니다: {resolution}", resolution);
            }

            DeskResolution applied = ResolveApplicable(resolution);

            // 유니티 모드가 같아도 WINDOWED 와 BORDERLESS_WINDOWED 는 서로 다르므로 우리 열거로 비교한다.
            bool modeChanged = WindowDeskAPI.DisplayMode.Current != mode;

            _applied = applied;
            _hasApplied = true;

            ApplySize(applied, mode);

            DeskEventPump.RunNextFrame(() => {
                if (modeChanged) {
                    DeskDisplayModeService.FinishModeChange(mode);
                }

                DeskEvents.RaiseResolutionChanged(applied);
            });

            return DeskResolutionApplyResult.Success(resolution, applied, mode);
        }

        /// <summary>
        /// 크기를 적용한다.
        /// 창 모드 계열은 작업 영역을 넘지 않도록 비율을 지켜 줄인 크기를 쓴다.
        /// </summary>
        private static void ApplySize(DeskResolution applied, DESK_DISPLAY_MODE mode) {
            FullScreenMode unityMode = DeskDisplayModeService.ToUnityMode(mode);
            Vector2Int size = WindowDeskAPI.FitToScreen(new Vector2Int(applied.Width, applied.Height), mode);

            if (applied.RefreshRate > 0) {
                RefreshRate rate = new RefreshRate { numerator = (uint)applied.RefreshRate, denominator = 1 };
                Screen.SetResolution(size.x, size.y, unityMode, rate);
                return;
            }

            Screen.SetResolution(size.x, size.y, unityMode);
        }

        /// <summary> 목록에 없는 값을 넘겼을 때 대신 쓸 가장 가까운 해상도를 찾는다. </summary>
        public bool TryFindNearest(DeskResolution target, int monitorIndex, out DeskResolution nearest) {
            nearest = default;

            IReadOnlyList<DeskResolution> candidates = GetSupported(monitorIndex);

            if (candidates.Count == 0) {
                return false;
            }

            int bestScore = int.MaxValue;
            int bestRateGap = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++) {
                DeskResolution candidate = candidates[i];
                int score = Mathf.Abs(candidate.Width - target.Width) + Mathf.Abs(candidate.Height - target.Height);
                int rateGap = target.RefreshRate > 0 ? Mathf.Abs(candidate.RefreshRate - target.RefreshRate) : 0;

                if (score > bestScore || (score == bestScore && rateGap >= bestRateGap)) {
                    continue;
                }

                bestScore = score;
                bestRateGap = rateGap;
                nearest = candidate;
            }

            return true;
        }

        /// <summary> 실제로 적용할 해상도를 정한다. 목록을 못 읽으면 요청값을 그대로 쓴다. </summary>
        private DeskResolution ResolveApplicable(DeskResolution requested) {
            int monitorIndex = DeskMonitorCache.GetCurrentIndex();
            IReadOnlyList<DeskResolution> supported = GetSupported(monitorIndex);

            if (supported.Count == 0) {
                return requested;
            }

            for (int i = 0; i < supported.Count; i++) {
                if (supported[i].Equals(requested)) {
                    return requested;
                }
            }

            return TryFindNearest(requested, monitorIndex, out DeskResolution nearest) ? nearest : requested;
        }

        /// <summary> 인덱스가 범위를 벗어나면 null 을 돌려 플랫폼 구현이 주 모니터로 처리하게 한다. </summary>
        private static string GetDeviceName(int monitorIndex) {
            IReadOnlyList<DeskMonitorInfo> monitors = DeskMonitorCache.GetMonitors();

            if (monitorIndex < 0 || monitorIndex >= monitors.Count) {
                return null;
            }

            return monitors[monitorIndex].DeviceName;
        }

        private static int CompareDescending(DeskResolution left, DeskResolution right) {
            if (left.Width != right.Width) {
                return right.Width.CompareTo(left.Width);
            }

            if (left.Height != right.Height) {
                return right.Height.CompareTo(left.Height);
            }

            return right.RefreshRate.CompareTo(left.RefreshRate);
        }
    }
}
