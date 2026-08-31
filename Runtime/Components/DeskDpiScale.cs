using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LifeLogs.WindowUtil {

    /// <summary> 모니터 배율을 UI 에 반영하기 위한 계산과 일괄 적용 </summary>
    internal static class DeskDpiScale {

        /// <summary> 배율을 확인할 수 없을 때 쓰는 기본값 </summary>
        public const float DEFAULT_SCALE = 1f;

        /// <summary> 창이 놓인 모니터의 배율. 1.0 이 100% 이며 확인할 수 없으면 1 </summary>
        public static float Current {
            get {
                if (!WindowDeskAPI.Monitors.TryGetCurrent(out DeskMonitorInfo monitor) || monitor.ScaleFactor <= 0f) {
                    return DEFAULT_SCALE;
                }

                return monitor.ScaleFactor;
            }
        }

        /// <summary> 씬에 있는 모든 CanvasScaler 에 현재 배율을 반영한다. </summary>
        /// <param name="baseScaleFactors">Canvas 별 원래 배수. 넘기지 않으면 현재 값을 기준으로 삼는다.</param>
        /// <returns>실제로 반영한 Canvas 개수.</returns>
        public static int ApplyToAllCanvasScalers(IDictionary<CanvasScaler, float> baseScaleFactors = null) {
            CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            float scale = Current;
            int applied = 0;

            for (int i = 0; i < scalers.Length; i++) {
                CanvasScaler scaler = scalers[i];

                if (!IsScaleFactorUsable(scaler)) {
                    continue;
                }

                float baseFactor = ResolveBaseFactor(scaler, baseScaleFactors);
                scaler.scaleFactor = baseFactor * scale;
                applied++;
            }

            return applied;
        }

        /// <summary> scaleFactor 는 Constant Pixel Size 모드에서만 의미가 있다. </summary>
        public static bool IsScaleFactorUsable(CanvasScaler scaler) {
            return scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        private static float ResolveBaseFactor(CanvasScaler scaler, IDictionary<CanvasScaler, float> baseScaleFactors) {
            if (baseScaleFactors == null) {
                return scaler.scaleFactor;
            }

            if (!baseScaleFactors.TryGetValue(scaler, out float baseFactor)) {
                baseFactor = scaler.scaleFactor;
                baseScaleFactors[scaler] = baseFactor;
            }

            return baseFactor;
        }
    }
}
