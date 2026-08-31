using System.Collections.Generic;

namespace LifeLogs.WindowUtil.Samples {

    /// <summary>
    /// 모니터를 화면에 보여줄 때 쓰는 문자열 도우미.
    /// PC 데모와 바탕화면 데모가 같은 표기를 쓰도록 한곳에 모아 둔다.
    ///
    /// OS 열거 순서와 물리 배치 순서는 일치하지 않는다. 0번이 오른쪽에 있을 수 있다.
    /// 유저에게는 배치 순서로 보여주고, 이동은 열거 인덱스로 해야 하므로 둘을 함께 표기한다.
    /// </summary>
    public static class DemoMonitors {

        private const string UNKNOWN = "확인 불가";

        /// <summary> 배치 순서를 "[2] → [1] → [0]" 처럼 잇는다. </summary>
        /// <param name="leftToRightOrder"><see cref="WindowDeskAPI.GetLeftToRightOrder"/> 결과.</param>
        public static string FormatOrder(IReadOnlyList<int> leftToRightOrder) {
            if (leftToRightOrder.Count == 0) {
                return UNKNOWN;
            }

            string[] labels = new string[leftToRightOrder.Count];

            for (int i = 0; i < leftToRightOrder.Count; i++) {
                labels[i] = $"[{leftToRightOrder[i]}]";
            }

            return string.Join(" → ", labels);
        }

        /// <summary> 해당 모니터가 왼쪽부터 몇 번째인지. 1 부터 센다. 못 찾으면 0. </summary>
        /// <param name="leftToRightOrder"><see cref="WindowDeskAPI.GetLeftToRightOrder"/> 결과.</param>
        /// <param name="monitorIndex">OS 열거 인덱스.</param>
        public static int PlacementOf(IReadOnlyList<int> leftToRightOrder, int monitorIndex) {
            for (int i = 0; i < leftToRightOrder.Count; i++) {
                if (leftToRightOrder[i] == monitorIndex) {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary> 배치 위치를 " [좌2]" 처럼 붙일 수 있는 꼬리표로 만든다. 못 찾으면 빈 문자열. </summary>
        /// <param name="leftToRightOrder"><see cref="WindowDeskAPI.GetLeftToRightOrder"/> 결과.</param>
        /// <param name="monitorIndex">OS 열거 인덱스.</param>
        public static string PlacementTag(IReadOnlyList<int> leftToRightOrder, int monitorIndex) {
            int placement = PlacementOf(leftToRightOrder, monitorIndex);

            return placement > 0 ? $" [좌{placement}]" : string.Empty;
        }
    }
}
