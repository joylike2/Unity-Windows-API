using System.Collections.Generic;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 작업표시줄 바뀜 알림 수신부. 작업표시줄을 피해 UI 를 두는 쪽이 구현한다.
    /// <see cref="IDeskDisplayListener"/> 에 넣지 않고 따로 둔 것은, 기존 구현체가 새 메서드 때문에 컴파일이 깨지지 않게 하기 위해서다.
    /// </summary>
    public interface IDeskTaskbarListener {

        /// <summary>
        /// 작업표시줄 자리 · 두께 · 자동 숨김이나 모니터 작업 영역이 바뀌었다 (1초마다 확인). 가릴 영역은 <see cref="WindowDeskAPI.GetSafeAreaInsets"/> 로 다시 구한다.
        /// </summary>
        /// <param name="taskbars">바뀐 뒤의 작업표시줄 목록 사본. 작업표시줄을 못 읽으면 비어 있다.</param>
        void OnTaskbarChanged(IReadOnlyList<DeskTaskbarInfo> taskbars);
    }
}
