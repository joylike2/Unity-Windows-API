using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 다른 스레드에서 올라온 알림을 메인 스레드에서 발행하기 위한 펌프 </summary>
    internal sealed class DeskEventPump : MonoBehaviour {

        private const string OBJECT_NAME = "[DeskEventPump]";

        private static readonly Queue<Action> NEXT_FRAME = new Queue<Action>();

        private static DeskEventPump _instance;

        private int _lastMonitorIndex = -1;

        /// <summary> 펌프가 없으면 만든다. 씬 전환에도 살아남는다. </summary>
        internal static void Ensure() {
            if (_instance != null) {
                return;
            }

            GameObject holder = new GameObject(OBJECT_NAME) { hideFlags = HideFlags.DontSave };

            _instance = holder.AddComponent<DeskEventPump>();
        }

        /// <summary> 다음 프레임에 실행할 작업을 예약한다. 메인 스레드에서만 호출한다. </summary>
        internal static void RunNextFrame(Action action) {
            if (action == null) {
                return;
            }

            Ensure();
            NEXT_FRAME.Enqueue(action);
        }

        private void Update() {
            DeskEvents.Flush();
            FlushNextFrame();
            DeskDisplayWatcher.Tick();
            WatchCurrentMonitor();
            WindowDeskAPI.RefreshResizable();
            WindowDeskAPI.RefreshTopMost();
            WindowDeskAPI.RefreshDesktopWindow();
            DeskTray.Tick();
        }

        /// <summary> 창을 끌어 옮기는 즉시 반응해야 하므로 1초 폴링이 아니라 매 프레임 본다. </summary>
        private void WatchCurrentMonitor() {
            if (!WindowDeskAPI.IsInitialized) {
                return;
            }

            int index = DeskMonitorCache.GetCurrentIndex();

            if (index < 0 || index == _lastMonitorIndex) {
                return;
            }

            bool isFirst = _lastMonitorIndex < 0;
            _lastMonitorIndex = index;

            if (isFirst) {
                return;
            }

            // 모니터마다 작업 영역이 달라서 창 크기를 다시 맞춰야 한다.
            WindowDeskAPI.FitWindowToMonitor(index);

            // 드래그로 옮긴 것은 유저가 저장 버튼을 누르지 않으므로 여기서 파일에 남긴다.
            WindowDeskAPI.AutoSaveOnMonitorDrag();

            DeskEvents.RaiseCurrentMonitorChanged(index);
        }

        /// <summary> 이번 프레임에 예약된 작업만 실행한다. 실행 중 새로 쌓인 것은 다음 프레임으로 넘긴다. </summary>
        private static void FlushNextFrame() {
            int count = NEXT_FRAME.Count;

            for (int i = 0; i < count; i++) {
                Action action = NEXT_FRAME.Dequeue();

                try {
                    action();
                }
                catch (Exception e) {
                    Debug.LogError($"[DeskEventPump] 예약 작업에서 예외가 발생했습니다: {e}");
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus) {
            DeskFrameRate.SetFocus(hasFocus);

            if (hasFocus) {
                WindowDeskAPI.RefreshCursorConfine();
            }
        }

        private void OnDestroy() {
            if (_instance == this) {
                _instance = null;
            }
        }
    }
}
