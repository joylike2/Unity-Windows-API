using System.Collections.Generic;

namespace LifeLogs.WindowUtil {

    /// <summary> 모니터 목록 조회와 창 이동 </summary>
    internal interface IDeskMonitorService {

        /// <summary> 연결된 모니터 전체 (OS 열거 순) </summary>
        IReadOnlyList<DeskMonitorInfo> All { get; }

        /// <summary> 화면 배치 왼쪽부터의 모니터 인덱스 순서 </summary>
        IReadOnlyList<int> LeftToRightOrder { get; }

        /// <summary> 창이 놓인 모니터 인덱스. 찾지 못하면 -1 </summary>
        int CurrentIndex { get; }

        /// <summary> 주 모니터 인덱스. 없으면 -1 </summary>
        int PrimaryIndex { get; }

        /// <summary> 창이 놓인 모니터가 사라졌을 때의 대응 방식 </summary>
        DESK_MONITOR_LOST_POLICY LostPolicy { get; set; }

        /// <summary> 현재 모니터 구성 스냅샷 </summary>
        DeskMonitorLayout GetLayout();

        /// <summary> 모니터 구성 스냅샷. forceRefresh 면 캐시를 버리고 다시 열거한다. </summary>
        DeskMonitorLayout GetLayout(bool forceRefresh);

        /// <summary> 모니터 목록을 다시 열거한다. </summary>
        bool Refresh();

        /// <summary> 창이 놓인 모니터 정보를 가져온다. </summary>
        bool TryGetCurrent(out DeskMonitorInfo monitor);

        /// <summary> 인덱스로 모니터 정보를 가져온다. </summary>
        bool TryGetAt(int monitorIndex, out DeskMonitorInfo monitor);

        /// <summary> 창을 지정한 모니터로 옮긴다. </summary>
        DeskMoveResult MoveWindowTo(int monitorIndex);

        /// <summary> 배치와 배수 옵션을 지정해 창을 옮긴다. </summary>
        DeskMoveResult MoveWindowTo(int monitorIndex, DeskMoveOptions options);
    }
}
