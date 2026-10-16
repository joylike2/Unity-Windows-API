using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 작업표시줄 창 사각형으로 붙은 변 · 두께 · 펼친 자리를 구하고, 작업 영역 두께에 작업표시줄 두께를 합치는 계산.
    /// OS 호출 없이 값만 다루므로 Windows 가 없어도 테스트할 수 있다.
    /// </summary>
    internal static class DeskTaskbarMath {

        /// <summary>
        /// 작업표시줄 창이 모니터 어느 변에 붙었는지. 가로가 길면 위 · 아래, 세로가 길면 왼쪽 · 오른쪽이고, 모니터 가운데보다 어느 쪽에 있는지로 고른다.
        /// 자동 숨김으로 화면 밖에 밀려 있어도 크기와 쪽은 그대로라 같은 답이 나온다. 빈 사각형이면 NONE.
        /// </summary>
        /// <param name="taskbarRect">작업표시줄 창 사각형 (가상 데스크탑 좌표, 좌상단 원점).</param>
        /// <param name="monitorBounds">작업표시줄이 놓인 모니터 전체 영역.</param>
        internal static DESK_TASKBAR_EDGE FindEdge(RectInt taskbarRect, RectInt monitorBounds) {
            if (taskbarRect.width <= 0 || taskbarRect.height <= 0) {
                return DESK_TASKBAR_EDGE.NONE;
            }

            // 가운데 비교는 두 배 좌표로 해 반 픽셀을 버리지 않는다
            if (taskbarRect.width >= taskbarRect.height) {
                int taskbarCenterY = taskbarRect.y * 2 + taskbarRect.height;
                int monitorCenterY = monitorBounds.y * 2 + monitorBounds.height;
                return taskbarCenterY < monitorCenterY ? DESK_TASKBAR_EDGE.TOP : DESK_TASKBAR_EDGE.BOTTOM;
            }

            int taskbarCenterX = taskbarRect.x * 2 + taskbarRect.width;
            int monitorCenterX = monitorBounds.x * 2 + monitorBounds.width;
            return taskbarCenterX < monitorCenterX ? DESK_TASKBAR_EDGE.LEFT : DESK_TASKBAR_EDGE.RIGHT;
        }

        /// <summary> 붙은 변에서 화면 안쪽으로 차지하는 두께. 위 · 아래면 높이, 왼쪽 · 오른쪽이면 너비. 변을 모르면 0. </summary>
        internal static int GetThickness(RectInt taskbarRect, DESK_TASKBAR_EDGE edge) {
            switch (edge) {
                case DESK_TASKBAR_EDGE.TOP:
                case DESK_TASKBAR_EDGE.BOTTOM:
                    return taskbarRect.height;
                case DESK_TASKBAR_EDGE.LEFT:
                case DESK_TASKBAR_EDGE.RIGHT:
                    return taskbarRect.width;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 펼쳤을 때 작업표시줄이 덮는 사각형: 모니터 변에 붙여 그 변 길이 전체에 두께만큼.
        /// 자동 숨김으로 숨은 창 사각형은 화면 밖에 밀려 있어 그대로 쓸 수 없다. 변을 모르면 빈 사각형.
        /// </summary>
        internal static RectInt GetShownBounds(RectInt monitorBounds, DESK_TASKBAR_EDGE edge, int thickness) {
            switch (edge) {
                case DESK_TASKBAR_EDGE.LEFT:
                    return new RectInt(monitorBounds.x, monitorBounds.y, thickness, monitorBounds.height);
                case DESK_TASKBAR_EDGE.TOP:
                    return new RectInt(monitorBounds.x, monitorBounds.y, monitorBounds.width, thickness);
                case DESK_TASKBAR_EDGE.RIGHT:
                    return new RectInt(monitorBounds.xMax - thickness, monitorBounds.y, thickness, monitorBounds.height);
                case DESK_TASKBAR_EDGE.BOTTOM:
                    return new RectInt(monitorBounds.x, monitorBounds.yMax - thickness, monitorBounds.width, thickness);
                default:
                    return new RectInt(0, 0, 0, 0);
            }
        }

        /// <summary>
        /// 작업 영역 두께에 그 모니터 작업표시줄 두께를 합친 네 변 두께 (변마다 큰 값).
        /// 자동 숨김 작업표시줄은 작업 영역을 차지하지 않아 작업 영역 두께가 0 이므로, 펼쳤을 때 덮는 두께가 여기서 더해진다.
        /// </summary>
        /// <param name="workAreaInsets">모니터 전체 영역과 작업 영역의 차이.</param>
        /// <param name="taskbars">작업표시줄 목록. 다른 모니터 것은 건너뛴다.</param>
        /// <param name="monitorIndex">대상 모니터 인덱스.</param>
        internal static DeskEdgeInsets AddTaskbars(DeskEdgeInsets workAreaInsets, IReadOnlyList<DeskTaskbarInfo> taskbars, int monitorIndex) {
            int left = workAreaInsets.Left;
            int top = workAreaInsets.Top;
            int right = workAreaInsets.Right;
            int bottom = workAreaInsets.Bottom;

            for (int i = 0; i < taskbars.Count; i++) {
                DeskTaskbarInfo taskbar = taskbars[i];

                if (taskbar.MonitorIndex != monitorIndex) {
                    continue;
                }

                DeskEdgeInsets insets = taskbar.ToInsets();
                left = Mathf.Max(left, insets.Left);
                top = Mathf.Max(top, insets.Top);
                right = Mathf.Max(right, insets.Right);
                bottom = Mathf.Max(bottom, insets.Bottom);
            }

            return new DeskEdgeInsets(left, top, right, bottom);
        }
    }
}
