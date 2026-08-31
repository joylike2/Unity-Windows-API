using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 모듈이 발행하는 알림 모음. 구독한 쪽이 해제까지 책임진다. </summary>
    internal static class DeskEvents {

        private static readonly ConcurrentQueue<Action> PENDING = new ConcurrentQueue<Action>();

        private static int _mainThreadId;

        /// <summary> 모니터 연결 · 해제 · 해상도 · 배치가 바뀜 </summary>
        public static event Action<DeskMonitorLayout> DisplayConfigurationChanged;

        /// <summary> 창이 놓여 있던 모니터가 사라짐. 인자는 사라진 인덱스 </summary>
        public static event Action<int> CurrentMonitorLost;

        /// <summary> 창이 다른 모니터로 넘어감. 인자는 새 인덱스 </summary>
        public static event Action<int> CurrentMonitorChanged;

        /// <summary> 모니터 이동 또는 배율 변경. 인자는 신규 / 이전 배수 </summary>
        public static event Action<float> DpiScaleChanged;

        /// <summary> 해상도 적용 성공 </summary>
        public static event Action<DeskResolution> ResolutionChanged;

        /// <summary> 창 표시 방식 변경 성공 </summary>
        public static event Action<DESK_DISPLAY_MODE> DisplayModeChanged;

        /// <summary> 창 활성 / 비활성 </summary>
        public static event Action<bool> WindowFocusChanged;

        /// <summary> 발행을 기다리는 알림 개수 </summary>
        public static int PendingCount => PENDING.Count;

        /// <summary> 모든 구독을 끊고 대기 중인 알림을 버린다. </summary>
        public static void ClearAll() {
            DisplayConfigurationChanged = null;
            CurrentMonitorLost = null;
            CurrentMonitorChanged = null;
            DpiScaleChanged = null;
            ResolutionChanged = null;
            DisplayModeChanged = null;
            WindowFocusChanged = null;

            while (PENDING.TryDequeue(out _)) {
            }
        }

        #region 발행

        internal static void RaiseDisplayConfigurationChanged(DeskMonitorLayout layout) {
            Dispatch(() => Invoke(DisplayConfigurationChanged, layout));
        }

        internal static void RaiseCurrentMonitorLost(int lostIndex) {
            Dispatch(() => Invoke(CurrentMonitorLost, lostIndex));
        }

        internal static void RaiseCurrentMonitorChanged(int monitorIndex) {
            Dispatch(() => Invoke(CurrentMonitorChanged, monitorIndex));
        }

        internal static void RaiseDpiScaleChanged(float ratio) {
            Dispatch(() => Invoke(DpiScaleChanged, ratio));
        }

        internal static void RaiseResolutionChanged(DeskResolution resolution) {
            Dispatch(() => Invoke(ResolutionChanged, resolution));
        }

        internal static void RaiseDisplayModeChanged(DESK_DISPLAY_MODE mode) {
            Dispatch(() => Invoke(DisplayModeChanged, mode));
        }

        internal static void RaiseWindowFocusChanged(bool hasFocus) {
            Dispatch(() => Invoke(WindowFocusChanged, hasFocus));
        }

        #endregion 발행

        #region 내부 처리

        /// <summary> 큐에 쌓인 알림을 발행한다. 메인 스레드에서만 호출한다. </summary>
        internal static void Flush() {
            while (PENDING.TryDequeue(out Action pending)) {
                pending();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            ClearAll();

            DeskEventPump.Ensure();
        }

        /// <summary> OS 스레드에서 올라온 알림은 유니티 API 를 쓸 수 없으므로 다음 프레임으로 미룬다. </summary>
        private static void Dispatch(Action action) {
            if (_mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId == _mainThreadId) {
                action();
                return;
            }

            PENDING.Enqueue(action);
        }

        /// <summary> 구독자 하나가 예외를 던져도 나머지가 알림을 받도록 개별 호출한다. </summary>
        private static void Invoke<T>(Action<T> handler, T argument) {
            if (handler == null) {
                return;
            }

            Delegate[] subscribers = handler.GetInvocationList();

            for (int i = 0; i < subscribers.Length; i++) {
                try {
                    ((Action<T>)subscribers[i])(argument);
                }
                catch (Exception e) {
                    Debug.LogError($"[DeskEvents] 구독자에서 예외가 발생해 건너뜁니다: {e}");
                }
            }
        }

        /// <summary> 인자 없는 이벤트용 개별 호출 </summary>
        private static void Invoke(Action handler) {
            if (handler == null) {
                return;
            }

            Delegate[] subscribers = handler.GetInvocationList();

            for (int i = 0; i < subscribers.Length; i++) {
                try {
                    ((Action)subscribers[i])();
                }
                catch (Exception e) {
                    Debug.LogError($"[DeskEvents] 구독자에서 예외가 발생해 건너뜁니다: {e}");
                }
            }
        }

        #endregion 내부 처리
    }
}
