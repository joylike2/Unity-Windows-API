using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 최상위 · 테두리 · 크기 조절 · 작업표시줄 등 창 상태 </summary>
    internal interface IDeskWindowStateService {

        /// <summary> 최상위로 요청된 상태인지 여부. 전체화면 동안에는 실제로 걸리지 않는다. </summary>
        bool IsTopMost { get; }

        /// <summary> 제목 표시줄과 테두리가 제거된 상태인지 여부 </summary>
        bool IsBorderless { get; }

        /// <summary> 드래그로 창 크기를 바꿀 수 있게 요청된 상태인지 여부 </summary>
        bool IsResizable { get; }

        /// <summary> 작업표시줄과 Alt+Tab 목록에 보이는지 여부 </summary>
        bool IsTaskbarButtonVisible { get; }

        /// <summary> 마우스가 게임 창 밖으로 못 나가게 가둔 상태인지 여부 </summary>
        bool IsCursorConfined { get; }

        /// <summary> 가상 데스크탑 좌표 기준 마우스 커서 위치 </summary>
        Vector2Int GetCursorPositionOnDesktop();

        /// <summary> 최상위 표시를 전환한다. 창 모드에서만 실제로 걸린다. </summary>
        bool SetTopMost(bool topMost);

        /// <summary> 테두리 제거를 전환한다. </summary>
        bool SetBorderless(bool borderless);

        /// <summary> 드래그로 창 크기를 바꿀 수 있는지 전환한다. </summary>
        bool SetResizable(bool resizable);

        /// <summary> 작업표시줄 버튼 표시를 전환한다. Alt+Tab 목록도 함께 따라간다. </summary>
        bool SetTaskbarButtonVisible(bool visible);

        /// <summary> 마우스를 게임 창 안에 가둘지 전환한다. 포커스를 잃으면 OS 가 풀고, 되찾으면 다시 걸린다. </summary>
        bool SetCursorConfined(bool confined);
    }
}
