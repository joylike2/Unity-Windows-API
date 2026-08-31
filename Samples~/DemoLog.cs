using System;
using System.Collections.Generic;
using System.Text;

namespace LifeLogs.WindowUtil.Samples {

    /// <summary> 화면에 뿌릴 로그 줄을 담아 두는 버퍼. 빌드에는 콘솔이 없어 화면에 직접 그린다. </summary>
    public static class DemoLog {

        private const int MAX_LINES = 300;

        private const string COLOR_INFO = "#DDDDDD";
        private const string COLOR_SUCCESS = "#5FD35F";
        private const string COLOR_WARN = "#E0A030";
        private const string COLOR_ERROR = "#E05050";
        private const string COLOR_TIME = "#808080";

        private static readonly List<string> LINES = new List<string>();
        private static readonly StringBuilder BUILDER = new StringBuilder();

        private static bool _isDirty = true;
        private static string _cachedText = string.Empty;

        /// <summary> 줄이 추가되거나 지워졌을 때 </summary>
        public static event Action Changed;

        /// <summary> 담긴 줄 수 </summary>
        public static int Count => LINES.Count;

        /// <summary> 일반 정보 </summary>
        public static void Info(string message) {
            Add(message, COLOR_INFO);
        }

        /// <summary> 성공 </summary>
        public static void Success(string message) {
            Add(message, COLOR_SUCCESS);
        }

        /// <summary> 주의가 필요한 결과 </summary>
        public static void Warn(string message) {
            Add(message, COLOR_WARN);
        }

        /// <summary> 실패 </summary>
        public static void Error(string message) {
            Add(message, COLOR_ERROR);
        }

        /// <summary> 성공 여부에 따라 색을 정해 남긴다. </summary>
        public static void Result(string message, bool isSuccess) {
            if (isSuccess) {
                Success(message);
                return;
            }

            Error(message);
        }

        /// <summary> 구분선을 넣는다. </summary>
        public static void Section(string title) {
            Add($"───── {title} ─────", COLOR_TIME);
        }

        private static bool _isCapturingUnityLogs;

        /// <summary>
        /// 라이브러리가 <see cref="UnityEngine.Debug"/> 로 남기는 경고와 오류도 화면 로그에 함께 담습니다.
        /// 빌드에는 콘솔이 없어, 켜 두지 않으면 라이브러리가 알려주는 실패 사유를 볼 수 없습니다.
        /// 일반 로그는 담지 않습니다. 양이 많아 화면 로그가 밀려나기 때문입니다.
        /// </summary>
        /// <param name="enabled">true 면 담기 시작합니다.</param>
        public static void CaptureUnityLogs(bool enabled) {
            if (_isCapturingUnityLogs == enabled) {
                return;
            }

            _isCapturingUnityLogs = enabled;

            if (enabled) {
                UnityEngine.Application.logMessageReceived += OnUnityLog;
                return;
            }

            UnityEngine.Application.logMessageReceived -= OnUnityLog;
        }

        private static void OnUnityLog(string condition, string stackTrace, UnityEngine.LogType type) {
            switch (type) {
                case UnityEngine.LogType.Warning:
                    Warn(condition);
                    return;

                case UnityEngine.LogType.Error:
                case UnityEngine.LogType.Exception:
                case UnityEngine.LogType.Assert:
                    Error(condition);
                    return;

                default:
                    return;
            }
        }

        public static void Clear() {
            LINES.Clear();
            _isDirty = true;
            Changed?.Invoke();
        }

        /// <summary> 화면에 그릴 전체 문자열. 바뀌지 않았으면 이전 결과를 그대로 준다. </summary>
        public static string GetText() {
            if (!_isDirty) {
                return _cachedText;
            }

            BUILDER.Clear();

            for (int i = 0; i < LINES.Count; i++) {
                BUILDER.AppendLine(LINES[i]);
            }

            _cachedText = BUILDER.ToString();
            _isDirty = false;

            return _cachedText;
        }

        private static void Add(string message, string color) {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LINES.Add($"<color={COLOR_TIME}>[{time}]</color> <color={color}>{message}</color>");

            if (LINES.Count > MAX_LINES) {
                LINES.RemoveAt(0);
            }

            _isDirty = true;
            Changed?.Invoke();
        }
    }
}
