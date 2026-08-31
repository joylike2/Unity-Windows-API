using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 게임의 기준 프레임과 백그라운드 절전을 관리한다. </summary>
    internal static class DeskFrameRate {

        /// <summary> 기준 프레임을 지정하지 않았음을 뜻하는 값 </summary>
        public const int NOT_SPECIFIED = 0;

        /// <summary> 초기화에서 값을 주지 않았을 때 쓰는 기준 프레임 </summary>
        public const int DEFAULT_TARGET = 60;

        /// <summary> 프레임을 제한하지 않음을 뜻하는 값. 유니티 규약과 같다 </summary>
        public const int UNLIMITED = -1;

        /// <summary> 뒤로 갔을 때의 기본 목표 프레임 </summary>
        public const int DEFAULT_BACKGROUND_TARGET = 15;

        private static bool _hasFocus = true;
        private static bool _isVSyncRequested;
        private static int _originalTargetFrameRate;
        private static int _originalVSyncCount;
        private static bool _hasOriginal;

        /// <summary> 초기화 때 받은 기준 프레임. 지정하지 않았으면 NOT_SPECIFIED </summary>
        public static int TargetFrameRate { get; private set; } = NOT_SPECIFIED;

        /// <summary> 창이 뒤에 있을 때 쓸 목표 프레임 </summary>
        public static int BackgroundTarget { get; private set; } = DEFAULT_BACKGROUND_TARGET;

        /// <summary> 창이 뒤에 있을 때 프레임을 낮출지 여부 </summary>
        public static bool IsPowerSavingEnabled { get; private set; }

        /// <summary> 라이브러리가 프레임을 관리하는 중인지 여부. 제한 없음도 관리하는 상태다 </summary>
        public static bool IsManaging => TargetFrameRate != NOT_SPECIFIED;

        /// <summary> 쓸 수 있는 값인지. 1 이상이거나 제한 없음이어야 한다 </summary>
        public static bool IsValidTarget(int frameRate) {
            return frameRate >= 1 || frameRate == UNLIMITED;
        }

        /// <summary>
        /// 게임이 요청한 수직 동기화 값. 켜면 목표 프레임 대신 모니터 주사율을 따른다.
        /// 절전으로 배경 프레임을 물리는 동안에는 잠시 꺼지므로 실제 QualitySettings 값과 다를 수 있다.
        /// </summary>
        public static bool IsVSyncEnabled => _isVSyncRequested;

        /// <summary> 창이 지금 앞에 있는지 여부 </summary>
        public static bool HasFocus => _hasFocus;

        /// <summary> 기준 프레임을 바꾼다. <see cref="UNLIMITED"/> 를 넘기면 제한하지 않는다. </summary>
        public static void SetTargetFrameRate(int frameRate) {
            if (!IsValidTarget(frameRate)) {
                Debug.LogWarning($"[DeskFrameRate] 기준 프레임은 1 이상이거나 {UNLIMITED}(제한 없음) 여야 합니다. "
                                 + $"받은 값 {frameRate}");
                return;
            }

            CaptureOriginal();

            TargetFrameRate = frameRate;

            if (_isVSyncRequested) {
                Debug.LogWarning($"[DeskFrameRate] 수직 동기화가 켜져 있어 기준 프레임 {frameRate} 는 지금 적용되지 않습니다. " +
                                 "SetVSync(false) 를 부르면 적용됩니다.");
            }

            Apply();
        }

        /// <summary> 백그라운드 절전을 켜거나 끈다. 목표 프레임은 지금 값을 그대로 쓴다. </summary>
        public static void SetPowerSaving(bool enabled) {
            SetPowerSaving(enabled, BackgroundTarget);
        }

        /// <summary> 백그라운드 절전을 켜거나 끄고 그때 쓸 목표 프레임을 정한다. </summary>
        public static void SetPowerSaving(bool enabled, int backgroundTarget) {
            BackgroundTarget = Mathf.Max(1, backgroundTarget);
            IsPowerSavingEnabled = enabled;

            if (enabled && !IsManaging) {
                Debug.LogWarning("[DeskFrameRate] 기준 프레임이 없어 절전이 동작하지 않습니다. " +
                                 "SetTargetFrameRate 를 먼저 부르십시오.");
            }

            Apply();
        }

        /// <summary> 수직 동기화를 켜거나 끈다. 종료할 때 되돌리려고 이전 값을 남겨 둔다. </summary>
        public static void SetVSync(bool enabled) {
            CaptureOriginal();

            _isVSyncRequested = enabled;
            Apply();
        }

        /// <summary> 지금 상태에 맞는 목표 프레임을 적용한다. </summary>
        /// <summary>
        /// 지금 상태에 맞는 프레임을 건다.
        /// 배경으로 내려갈 때는 수직 동기화를 잠시 꺼야 한다. 켜져 있으면 목표 프레임이 무시되어
        /// 뒤에서도 모니터 주사율만큼 계속 그리기 때문이다.
        /// </summary>
        public static void Apply() {
            if (!IsManaging) {
                return;
            }

            if (IsPowerSavingEnabled && !_hasFocus) {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = BackgroundTarget;
                return;
            }

            QualitySettings.vSyncCount = _isVSyncRequested ? 1 : 0;
            Application.targetFrameRate = _isVSyncRequested ? UNLIMITED : TargetFrameRate;
        }

        /// <summary> 초기화 때 기본 60 으로 시작한다. 저장 파일이 있으면 그 뒤에 저장값으로 덮인다. </summary>
        internal static void Setup() {
            BackgroundTarget = DEFAULT_BACKGROUND_TARGET;

            CaptureOriginal();

            // 게임이 아직 요청한 적이 없으므로 프로젝트 품질 설정을 그대로 이어받는다.
            // 이걸 안 하면 초기화가 말없이 수직 동기화를 꺼버린다.
            _isVSyncRequested = QualitySettings.vSyncCount > 0;

            TargetFrameRate = DEFAULT_TARGET;
            Apply();
        }

        /// <summary> 게임이 원래 쓰던 프레임 설정으로 되돌린다. </summary>
        internal static void RestoreOriginal() {
            TargetFrameRate = NOT_SPECIFIED;
            IsPowerSavingEnabled = false;
            _isVSyncRequested = false;
            _hasFocus = true;

            if (!_hasOriginal) {
                return;
            }

            Application.targetFrameRate = _originalTargetFrameRate;
            QualitySettings.vSyncCount = _originalVSyncCount;
            _hasOriginal = false;
        }

        /// <summary> 창 포커스가 바뀌었음을 알린다. 펌프가 부른다. </summary>
        internal static void SetFocus(bool hasFocus) {
            if (_hasFocus == hasFocus) {
                return;
            }

            _hasFocus = hasFocus;

            Apply();
            DeskEvents.RaiseWindowFocusChanged(hasFocus);
        }

        private static void CaptureOriginal() {
            if (_hasOriginal) {
                return;
            }

            _originalTargetFrameRate = Application.targetFrameRate;
            _originalVSyncCount = QualitySettings.vSyncCount;
            _hasOriginal = true;
        }
    }
}
