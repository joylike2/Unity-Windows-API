using UnityEngine;
using UnityEngine.UI;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 씬의 카메라와 캔버스를 모아 모니터 배율을 대신 맞춰 주는 관찰자.
    /// 참조는 에디터 메뉴가 채우므로 게임 쪽에서 따로 붙일 일이 없다.
    /// </summary>
    public sealed class DeskSceneObserver : MonoBehaviour, IDeskInitializeListener {

        /// <summary> 투명 배경에 필요한 카메라 배경색. 알파만 0 이면 남은 RGB 가 화면에 더해진다 </summary>
        private static readonly Color TRANSPARENT_BLACK = new Color(0f, 0f, 0f, 0f);

        [Header("셋업 메뉴가 씬을 훑어 채운다. 필요하면 직접 더 넣어도 된다")]
        [SerializeField] private Camera[] _cameras = new Camera[0];

        [SerializeField] private CanvasScaler[] _canvasScalers = new CanvasScaler[0];

        [Header("투명 배경에 맞지 않는 카메라를 실행 중에 바로잡는다")]
        [SerializeField] private bool _fixCameraBackground = true;

        [Header("무엇에 배율을 걸지")]
        [Tooltip("캔버스 UI 에 배율을 건다")]
        [SerializeField] private bool _scaleCanvases = true;

        [Tooltip("직교 카메라로 보는 월드 오브젝트에 배율을 건다. 기본은 꺼짐 - 월드 크기는 게임이 정한다")]
        [SerializeField] private bool _scaleCameras;

        [Header("배율 기준. 바탕화면 게임은 DPI 가 맞다")]
        [SerializeField] private DESK_SCALE_BASIS _scaleBasis = DESK_SCALE_BASIS.DPI;

        [Tooltip("제작 기준 해상도. 두 방식 모두 카메라 배율 계산에 쓴다")]
        [SerializeField] private Vector2Int _referenceResolution = new Vector2Int(1920, 1080);

        private float[] _baseOrthographicSizes;
        private float[] _baseScaleFactors;
        private bool _isBaseCaptured;

        private void OnEnable() {
            CaptureBaseValues();

            WindowDeskAPI.AddInitializeListener(this);

            DeskEvents.DpiScaleChanged += OnDpiScaleChanged;

            // 배율이 같은 모니터끼리 옮기면 DpiScaleChanged 가 오지 않아 이것도 함께 본다.
            DeskEvents.CurrentMonitorChanged += OnCurrentMonitorChanged;
        }

        private void OnDisable() {
            DeskEvents.CurrentMonitorChanged -= OnCurrentMonitorChanged;
            DeskEvents.DpiScaleChanged -= OnDpiScaleChanged;
            WindowDeskAPI.RemoveInitializeListener(this);

            RestoreBaseValues();
        }

        /// <summary> 초기화가 끝났다. 이 시점에 모니터 정보가 확정되므로 배율을 처음 반영한다. </summary>
        /// <param name="profiles">초기화에 쓰인 프로파일.</param>
        public void OnDeskInitialized(DESK_WINDOW_PROFILE profiles) {
            Apply();
        }

        /// <summary> 알림에 실려 오는 배수 대신 현재 모니터의 절대 배율로 다시 계산한다. </summary>
        private void OnDpiScaleChanged(float ratio) {
            Apply();
        }

        /// <summary> 배율이 같은 모니터로 옮겨도 해상도가 다를 수 있어 다시 계산한다. </summary>
        private void OnCurrentMonitorChanged(int monitorIndex) {
            Apply();
        }

        private void Apply() {
            if (!_isBaseCaptured) {
                return;
            }

            float scale = ResolveScale();

            if (scale <= 0f) {
                return;
            }

            if (_scaleCanvases) {
                ApplyCanvasScale(scale);
            }

            if (_scaleCameras) {
                ApplyCameraScale(scale);
            }

            if (_fixCameraBackground) {
                FixCameraBackgrounds();
            }
        }

        /// <summary>
        /// 적용할 배율. 기준에 따라 계산 방식이 갈린다.
        /// DPI 는 물리적 크기를, 기준 해상도는 화면에서 차지하는 비율을 일정하게 만든다.
        /// </summary>
        private float ResolveScale() {
            if (_scaleBasis == DESK_SCALE_BASIS.DPI) {
                return DeskDpiScale.Current;
            }

            return ResolveResolutionRatio();
        }

        /// <summary> 배율만큼 UI 를 키운다. Constant Pixel Size 가 아니면 scaleFactor 가 의미 없어 건너뛴다. </summary>
        private void ApplyCanvasScale(float scale) {
            for (int i = 0; i < _canvasScalers.Length; i++) {
                CanvasScaler scaler = _canvasScalers[i];

                if (!DeskDpiScale.IsScaleFactorUsable(scaler)) {
                    continue;
                }

                scaler.scaleFactor = _baseScaleFactors[i] * scale;
            }
        }

        /// <summary>
        /// 배율만큼 월드 오브젝트를 키운다.
        /// 직교 카메라는 해상도가 커지면 오브젝트가 이미 그만큼 커지므로, 그 몫을 먼저 상쇄한 뒤 배율을 건다.
        /// 상쇄하지 않으면 UI 보다 훨씬 크게 나온다.
        /// 원근 카메라는 거리와 화각이 함께 얽혀 있어 손대지 않는다.
        /// </summary>
        private void ApplyCameraScale(float scale) {
            float resolutionRatio = ResolveResolutionRatio();

            for (int i = 0; i < _cameras.Length; i++) {
                Camera target = _cameras[i];

                if (target == null || !target.orthographic) {
                    continue;
                }

                target.orthographicSize = _baseOrthographicSizes[i] * resolutionRatio / scale;
            }
        }

        /// <summary> 실제 세로 해상도가 기준 세로의 몇 배인지. 직교 카메라가 자동으로 받는 배율이다. </summary>
        private float ResolveResolutionRatio() {
            if (_referenceResolution.y <= 0) {
                return 1f;
            }

            return Screen.height / (float)_referenceResolution.y;
        }

        /// <summary> 바탕화면 게임일 때만 배경을 바로잡는다. 다른 프로파일에서는 투명이 필요 없다. </summary>
        private void FixCameraBackgrounds() {
            if ((WindowDeskAPI.ActiveProfiles & DESK_WINDOW_PROFILE.DESKTOP_GAME)
                != DESK_WINDOW_PROFILE.DESKTOP_GAME) {
                return;
            }

            for (int i = 0; i < _cameras.Length; i++) {
                Camera target = _cameras[i];

                if (target == null || IsBackgroundReady(target)) {
                    continue;
                }

                Debug.LogWarning($"[DeskSceneObserver] {target.name} 의 배경이 투명용이 아니라 바로잡습니다. " +
                                 "에디터 메뉴 Setup Camera 로 미리 맞춰 두십시오.", target);

                target.clearFlags = CameraClearFlags.SolidColor;
                target.backgroundColor = TRANSPARENT_BLACK;
            }
        }

        private static bool IsBackgroundReady(Camera target) {
            if (target.clearFlags != CameraClearFlags.SolidColor) {
                return false;
            }

            Color background = target.backgroundColor;

            return Mathf.Approximately(background.r, 0f) && Mathf.Approximately(background.g, 0f)
                   && Mathf.Approximately(background.b, 0f) && Mathf.Approximately(background.a, 0f);
        }

        /// <summary> 배율을 곱하기 전의 값. 이걸 기준으로 매번 다시 계산해야 값이 누적되지 않는다. </summary>
        private void CaptureBaseValues() {
            if (_isBaseCaptured) {
                return;
            }

            _baseOrthographicSizes = new float[_cameras.Length];
            _baseScaleFactors = new float[_canvasScalers.Length];

            for (int i = 0; i < _cameras.Length; i++) {
                _baseOrthographicSizes[i] = _cameras[i] != null ? _cameras[i].orthographicSize : 0f;
            }

            for (int i = 0; i < _canvasScalers.Length; i++) {
                _baseScaleFactors[i] = _canvasScalers[i] != null ? _canvasScalers[i].scaleFactor : 1f;
            }

            _isBaseCaptured = true;
        }

        private void RestoreBaseValues() {
            if (!_isBaseCaptured) {
                return;
            }

            if (_scaleCameras) {
                for (int i = 0; i < _cameras.Length; i++) {
                    if (_cameras[i] != null && _cameras[i].orthographic) {
                        _cameras[i].orthographicSize = _baseOrthographicSizes[i];
                    }
                }
            }

            if (!_scaleCanvases) {
                return;
            }

            for (int i = 0; i < _canvasScalers.Length; i++) {
                if (DeskDpiScale.IsScaleFactorUsable(_canvasScalers[i])) {
                    _canvasScalers[i].scaleFactor = _baseScaleFactors[i];
                }
            }
        }
    }
}
