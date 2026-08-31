namespace LifeLogs.WindowUtil {

    /// <summary> 전체화면 · 창 · 테두리없는 창 전환 </summary>
    internal interface IDeskDisplayModeService {

        /// <summary> 현재 표시 방식 </summary>
        DESK_DISPLAY_MODE Current { get; }

        /// <summary> 이 플랫폼에서 해당 방식을 쓸 수 있는지 여부 </summary>
        bool IsSupported(DESK_DISPLAY_MODE mode);

        /// <summary> 표시 방식을 바꾼다. 지원하지 않으면 WINDOWED 로 대체하고 경고한다. </summary>
        bool Apply(DESK_DISPLAY_MODE mode);
    }
}
