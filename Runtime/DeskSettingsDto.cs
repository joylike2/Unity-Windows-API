using System;

namespace LifeLogs.WindowUtil {

    /// <summary> 설정 JSON 직렬화 전용 데이터. JsonUtility 가 다루려면 public 필드여야 한다. </summary>
    [Serializable]
    internal sealed class DeskSettingsDto {

        public string monitorDeviceName;
        public int monitorIndex = -1;

        public int width;
        public int height;
        public int refreshRate;

        public string displayMode;
        public bool topMost;
        public bool resizable = true;
        public bool cursorConfined;

        public int targetFrameRate = DeskFrameRate.DEFAULT_TARGET;
        public int backgroundFrameRate = DeskFrameRate.DEFAULT_BACKGROUND_TARGET;
        public bool powerSaving;
        public bool vSync;
    }
}
