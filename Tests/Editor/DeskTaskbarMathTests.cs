using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LifeLogs.WindowUtil.Tests {

    /// <summary>
    /// 작업표시줄 창 사각형으로 붙은 변 · 두께 · 펼친 자리를 구하고 안전 두께를 합치는 계산.
    /// 좌표는 가상 데스크탑 기준 좌상단 원점이다. 주 모니터 1920×1080, 오른쪽 보조 모니터 2560×1440(1920, 0).
    /// </summary>
    public sealed class DeskTaskbarMathTests {

        private const int TASKBAR_HEIGHT = 48;
        private const int VERTICAL_TASKBAR_WIDTH = 62;
        // 자동 숨김으로 숨으면 화면 안에 남는 줄
        private const int HIDDEN_STRIP = 2;

        private static readonly RectInt PRIMARY = new RectInt(0, 0, 1920, 1080);
        private static readonly RectInt SECONDARY = new RectInt(1920, 0, 2560, 1440);
        private static readonly RectInt PORTRAIT = new RectInt(0, 0, 1080, 1920);

        [Test]
        public void 아래_작업표시줄은_BOTTOM이다() {
            RectInt taskbar = new RectInt(0, PRIMARY.yMax - TASKBAR_HEIGHT, PRIMARY.width, TASKBAR_HEIGHT);

            Assert.AreEqual(DESK_TASKBAR_EDGE.BOTTOM, DeskTaskbarMath.FindEdge(taskbar, PRIMARY));
        }

        [Test]
        public void 위_작업표시줄은_TOP이다() {
            RectInt taskbar = new RectInt(0, 0, PRIMARY.width, TASKBAR_HEIGHT);

            Assert.AreEqual(DESK_TASKBAR_EDGE.TOP, DeskTaskbarMath.FindEdge(taskbar, PRIMARY));
        }

        [Test]
        public void 왼쪽_세로_작업표시줄은_LEFT다() {
            RectInt taskbar = new RectInt(0, 0, VERTICAL_TASKBAR_WIDTH, PRIMARY.height);

            Assert.AreEqual(DESK_TASKBAR_EDGE.LEFT, DeskTaskbarMath.FindEdge(taskbar, PRIMARY));
        }

        [Test]
        public void 오른쪽_세로_작업표시줄은_RIGHT다() {
            RectInt taskbar = new RectInt(PRIMARY.xMax - VERTICAL_TASKBAR_WIDTH, 0, VERTICAL_TASKBAR_WIDTH, PRIMARY.height);

            Assert.AreEqual(DESK_TASKBAR_EDGE.RIGHT, DeskTaskbarMath.FindEdge(taskbar, PRIMARY));
        }

        [Test]
        public void 자동_숨김으로_아래_밖에_밀려도_BOTTOM이다() {
            RectInt hidden = new RectInt(0, PRIMARY.yMax - HIDDEN_STRIP, PRIMARY.width, TASKBAR_HEIGHT);

            Assert.AreEqual(DESK_TASKBAR_EDGE.BOTTOM, DeskTaskbarMath.FindEdge(hidden, PRIMARY));
        }

        [Test]
        public void 자동_숨김으로_왼쪽_밖에_밀려도_LEFT다() {
            RectInt hidden = new RectInt(HIDDEN_STRIP - VERTICAL_TASKBAR_WIDTH, 0, VERTICAL_TASKBAR_WIDTH, PRIMARY.height);

            Assert.AreEqual(DESK_TASKBAR_EDGE.LEFT, DeskTaskbarMath.FindEdge(hidden, PRIMARY));
        }

        [Test]
        public void 보조_모니터_왼쪽_작업표시줄은_그_모니터_기준으로_LEFT다() {
            RectInt taskbar = new RectInt(SECONDARY.x, 0, VERTICAL_TASKBAR_WIDTH, SECONDARY.height);

            Assert.AreEqual(DESK_TASKBAR_EDGE.LEFT, DeskTaskbarMath.FindEdge(taskbar, SECONDARY));
        }

        [Test]
        public void 세로_모니터_아래_작업표시줄도_BOTTOM이다() {
            RectInt taskbar = new RectInt(0, PORTRAIT.yMax - TASKBAR_HEIGHT, PORTRAIT.width, TASKBAR_HEIGHT);

            Assert.AreEqual(DESK_TASKBAR_EDGE.BOTTOM, DeskTaskbarMath.FindEdge(taskbar, PORTRAIT));
        }

        [Test]
        public void 빈_사각형은_변을_모른다() {
            Assert.AreEqual(DESK_TASKBAR_EDGE.NONE, DeskTaskbarMath.FindEdge(new RectInt(0, 0, 0, 0), PRIMARY));
        }

        [Test]
        public void 아래_작업표시줄_두께는_높이다() {
            RectInt taskbar = new RectInt(0, PRIMARY.yMax - TASKBAR_HEIGHT, PRIMARY.width, TASKBAR_HEIGHT);

            Assert.AreEqual(TASKBAR_HEIGHT, DeskTaskbarMath.GetThickness(taskbar, DESK_TASKBAR_EDGE.BOTTOM));
        }

        [Test]
        public void 세로_작업표시줄_두께는_너비다() {
            RectInt taskbar = new RectInt(0, 0, VERTICAL_TASKBAR_WIDTH, PRIMARY.height);

            Assert.AreEqual(VERTICAL_TASKBAR_WIDTH, DeskTaskbarMath.GetThickness(taskbar, DESK_TASKBAR_EDGE.LEFT));
        }

        [Test]
        public void 펼친_아래_작업표시줄은_모니터_아래_변에_붙는다() {
            RectInt shown = DeskTaskbarMath.GetShownBounds(SECONDARY, DESK_TASKBAR_EDGE.BOTTOM, TASKBAR_HEIGHT);

            Assert.AreEqual(new RectInt(SECONDARY.x, SECONDARY.yMax - TASKBAR_HEIGHT, SECONDARY.width, TASKBAR_HEIGHT), shown);
        }

        [Test]
        public void 펼친_오른쪽_작업표시줄은_모니터_오른쪽_변에_붙는다() {
            RectInt shown = DeskTaskbarMath.GetShownBounds(PRIMARY, DESK_TASKBAR_EDGE.RIGHT, VERTICAL_TASKBAR_WIDTH);

            Assert.AreEqual(new RectInt(PRIMARY.xMax - VERTICAL_TASKBAR_WIDTH, 0, VERTICAL_TASKBAR_WIDTH, PRIMARY.height), shown);
        }

        [Test]
        public void 자동_숨김_아래_작업표시줄은_안전_두께_아래에_더한다() {
            List<DeskTaskbarInfo> taskbars = new List<DeskTaskbarInfo> { CreateTaskbar(0, DESK_TASKBAR_EDGE.BOTTOM, TASKBAR_HEIGHT, true) };

            DeskEdgeInsets safe = DeskTaskbarMath.AddTaskbars(DeskEdgeInsets.ZERO, taskbars, 0);

            Assert.AreEqual(TASKBAR_HEIGHT, safe.Bottom);
        }

        [Test]
        public void 자동_숨김_오른쪽_작업표시줄은_안전_두께_오른쪽에_더한다() {
            List<DeskTaskbarInfo> taskbars = new List<DeskTaskbarInfo> { CreateTaskbar(0, DESK_TASKBAR_EDGE.RIGHT, VERTICAL_TASKBAR_WIDTH, true) };

            DeskEdgeInsets safe = DeskTaskbarMath.AddTaskbars(DeskEdgeInsets.ZERO, taskbars, 0);

            Assert.AreEqual(VERTICAL_TASKBAR_WIDTH, safe.Right);
        }

        [Test]
        public void 보이는_작업표시줄은_작업_영역_두께와_겹쳐_더하지_않는다() {
            List<DeskTaskbarInfo> taskbars = new List<DeskTaskbarInfo> { CreateTaskbar(0, DESK_TASKBAR_EDGE.BOTTOM, TASKBAR_HEIGHT, false) };
            DeskEdgeInsets workArea = new DeskEdgeInsets(0, 0, 0, TASKBAR_HEIGHT);

            DeskEdgeInsets safe = DeskTaskbarMath.AddTaskbars(workArea, taskbars, 0);

            Assert.AreEqual(TASKBAR_HEIGHT, safe.Bottom);
        }

        [Test]
        public void 다른_모니터_작업표시줄은_건너뛴다() {
            List<DeskTaskbarInfo> taskbars = new List<DeskTaskbarInfo> { CreateTaskbar(1, DESK_TASKBAR_EDGE.BOTTOM, TASKBAR_HEIGHT, true) };

            DeskEdgeInsets safe = DeskTaskbarMath.AddTaskbars(DeskEdgeInsets.ZERO, taskbars, 0);

            Assert.IsFalse(safe.HasAny);
        }

        private static DeskTaskbarInfo CreateTaskbar(int monitorIndex, DESK_TASKBAR_EDGE edge, int thickness, bool isAutoHide) {
            RectInt monitor = monitorIndex == 0 ? PRIMARY : SECONDARY;
            RectInt bounds = DeskTaskbarMath.GetShownBounds(monitor, edge, thickness);
            return new DeskTaskbarInfo(monitorIndex, edge, bounds, thickness, isAutoHide, monitorIndex == 0);
        }
    }
}
