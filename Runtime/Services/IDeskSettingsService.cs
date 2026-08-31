namespace LifeLogs.WindowUtil {

    /// <summary> 설정을 JSON 으로 주고받는다. 파일 저장은 사용하는 쪽이 맡는다. </summary>
    internal interface IDeskSettingsService {

        /// <summary> 현재 상태를 JSON 문자열로 내보낸다. </summary>
        string Export();

        /// <summary> JSON 을 검증한 뒤 전 항목을 적용한다. </summary>
        DeskImportResult Import(string json);

        /// <summary> JSON 에서 지정한 항목만 적용한다. </summary>
        DeskImportResult Import(string json, DESK_IMPORT_OPTIONS options);
    }
}
