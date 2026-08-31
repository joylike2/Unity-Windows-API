namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 초기화 완료 알림 수신부.
    /// 등록 시점에 이미 초기화가 끝나 있었다면 등록 즉시 불리므로, 실행 순서를 신경 쓸 필요가 없다.
    /// </summary>
    public interface IDeskInitializeListener {

        /// <summary> 초기화가 끝났다. </summary>
        /// <param name="profiles">초기화에 쓰인 프로파일.</param>
        void OnDeskInitialized(DESK_WINDOW_PROFILE profiles);
    }
}