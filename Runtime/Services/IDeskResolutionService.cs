using System.Collections.Generic;

namespace LifeLogs.WindowUtil {

    /// <summary> 해상도 조회와 적용. 옵션 UI 의 해상도 항목이 이것만 의존하면 된다. </summary>
    internal interface IDeskResolutionService {

        /// <summary> 현재 창이 놓인 모니터가 지원하는 해상도 목록 </summary>
        IReadOnlyList<DeskResolution> GetSupported();

        /// <summary> 지정한 모니터가 지원하는 해상도 목록 </summary>
        IReadOnlyList<DeskResolution> GetSupported(int monitorIndex);

        /// <summary> 현재 창이 놓인 모니터에 적용된 해상도. 창 모드에서는 창 크기가 아니라 바탕화면 해상도다. </summary>
        DeskResolution GetCurrent();

        /// <summary>
        /// 게임에 적용 중인 해상도. 옵션 화면이 "현재 값" 으로 표시하고 저장에도 이 값을 쓴다.
        /// 창 모드에서는 <see cref="GetCurrent()"/> 와 다르다.
        /// </summary>
        DeskResolution GetApplied();

        /// <summary> 지정한 모니터에 적용된 해상도 </summary>
        DeskResolution GetCurrent(int monitorIndex);

        /// <summary> 해상도를 적용한다. 표시 방식은 현재 값을 유지한다. </summary>
        DeskResolutionApplyResult Apply(DeskResolution resolution);

        /// <summary> 해상도와 표시 방식을 함께 적용한다. </summary>
        DeskResolutionApplyResult Apply(DeskResolution resolution, DESK_DISPLAY_MODE mode);

        /// <summary> 목록에 없는 값을 넘겼을 때 대신 쓸 가장 가까운 해상도를 찾는다. </summary>
        bool TryFindNearest(DeskResolution target, int monitorIndex, out DeskResolution nearest);
    }
}
