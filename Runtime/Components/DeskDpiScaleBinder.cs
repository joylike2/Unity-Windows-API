using UnityEngine;
using UnityEngine.UI;

namespace LifeLogs.WindowUtil {

    /// <summary> 모니터 배율이 바뀌면 이 Canvas 의 배수를 따라 바꾼다. </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class DeskDpiScaleBinder : MonoBehaviour {

        [SerializeField] private CanvasScaler _canvasScaler;

        private float _baseScaleFactor = 1f;
        private bool _isBaseCaptured;

        private void Reset() {
            _canvasScaler = GetComponent<CanvasScaler>();
        }

        private void OnEnable() {
            if (_canvasScaler == null) {
                _canvasScaler = GetComponent<CanvasScaler>();
            }

            CaptureBaseScaleFactor();
            DeskEvents.DpiScaleChanged += OnDpiScaleChanged;

            Apply();
        }

        private void OnDisable() {
            DeskEvents.DpiScaleChanged -= OnDpiScaleChanged;

            if (_isBaseCaptured && _canvasScaler != null) {
                _canvasScaler.scaleFactor = _baseScaleFactor;
            }
        }

        /// <summary> 알림에 실려 오는 배수 대신 현재 모니터의 절대 배율로 다시 계산한다. </summary>
        private void OnDpiScaleChanged(float ratio) {
            Apply();
        }

        private void Apply() {
            if (!_isBaseCaptured || !DeskDpiScale.IsScaleFactorUsable(_canvasScaler)) {
                return;
            }

            _canvasScaler.scaleFactor = _baseScaleFactor * DeskDpiScale.Current;
        }

        private void CaptureBaseScaleFactor() {
            if (_isBaseCaptured || _canvasScaler == null) {
                return;
            }

            if (!DeskDpiScale.IsScaleFactorUsable(_canvasScaler)) {
                Debug.LogWarning($"[DeskDpiScaleBinder] {name} 의 CanvasScaler 가 Constant Pixel Size 가 아니라 배율을 반영하지 않습니다.", this);
                return;
            }

            _baseScaleFactor = _canvasScaler.scaleFactor;
            _isBaseCaptured = true;
        }
    }
}
