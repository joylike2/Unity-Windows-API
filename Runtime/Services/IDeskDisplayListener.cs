namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 라이브러리가 보내는 알림 수신부. 게임은 이 인터페이스만 구현하면 된다.
    /// 필요 없는 알림은 빈 본문으로 두면 된다.
    /// </summary>
    public interface IDeskDisplayListener {

        /// <summary> 창이 다른 모니터로 넘어갔다. 해상도 목록을 다시 뽑아야 한다. </summary>
        /// <param name="monitorIndex">창이 놓인 모니터 인덱스.</param>
        void OnCurrentMonitorChanged(int monitorIndex);

        /// <summary> 해상도가 바뀌었다. </summary>
        /// <param name="resolution">적용된 해상도.</param>
        void OnResolutionChanged(DeskResolution resolution);

        /// <summary> 표시 방식이 바뀌었다. </summary>
        /// <param name="mode">적용된 표시 방식.</param>
        void OnDisplayModeChanged(DESK_DISPLAY_MODE mode);

        /// <summary> 모니터가 연결되거나 빠졌다. 배치나 해상도가 바뀌어도 온다. </summary>
        /// <param name="layout">바뀐 뒤의 모니터 구성.</param>
        void OnDisplayConfigurationChanged(DeskMonitorLayout layout);

        /// <summary> 창이 놓여 있던 모니터가 사라졌다. 이동 정책에 따라 창은 이미 옮겨졌다. </summary>
        /// <param name="lostIndex">사라진 모니터의 옛 인덱스.</param>
        void OnCurrentMonitorLost(int lostIndex);

        /// <summary> 모니터 배율이 바뀌었다. Constant Pixel Size 캔버스는 이 배수만큼 맞춰야 한다. </summary>
        /// <param name="scaleRatio">이전 대비 배수. 1.5 면 1.5배로 커진 것.</param>
        void OnDpiScaleChanged(float scaleRatio);

        /// <summary> 창이 활성 · 비활성되었다. </summary>
        /// <param name="hasFocus">true 면 포커스를 얻은 것.</param>
        void OnWindowFocusChanged(bool hasFocus);
    }
}
