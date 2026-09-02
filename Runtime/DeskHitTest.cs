using System;
using System.Collections;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary>
    /// 커서 아래 픽셀의 알파를 읽어 클릭을 받을지 흘려보낼지 매 프레임 정하는 판정기.
    /// 그려진 것이 있으면 클릭을 받고, 비어 있으면 바탕화면으로 넘긴다.
    /// </summary>
    internal sealed class DeskHitTest : MonoBehaviour {

        private const string OBJECT_NAME = "[DeskHitTest]";

        /// <summary> 이 알파부터 "그려진 것" 으로 본다. 반투명 그림자까지 클릭을 먹지 않도록 낮게 잡는다. </summary>
        private const float OPAQUE_ALPHA = 0.1f;

        private static DeskHitTest _instance;
        private static bool _isProbeEnabled;

        private Texture2D _sample;
        private Coroutine _routine;

        /// <summary> 판정기가 돌고 있는지 여부 </summary>
        internal static bool IsRunning => _instance != null;

        /// <summary> 화면 왼쪽 아래 빈 자리에서 마지막으로 읽은 알파. 음수면 아직 읽은 적이 없다 </summary>
        internal static float LastBackgroundAlpha { get; private set; } = -1f;

        /// <summary>
        /// 빈 자리 알파 진단을 돌릴지 여부. 기본은 꺼져 있다.
        /// 클릭 통과 판정과 무관한 진단 전용인데 켜 두면 매 프레임 ReadPixels 가 한 번 더 돌아
        /// GPU 동기화 비용이 두 배가 되므로, 투명이 안 나오는 원인을 볼 때만 켠다.
        /// </summary>
        internal static bool IsBackgroundAlphaProbeEnabled {
            get => _isProbeEnabled;
            set {
                if (_isProbeEnabled == value) {
                    return;
                }

                _isProbeEnabled = value;

                // 끈 뒤에도 마지막 값이 남아 있으면 지금 읽은 값으로 오인한다.
                if (!value) {
                    LastBackgroundAlpha = -1f;
                }
            }
        }

        /// <summary> 판정기가 없으면 만든다. 씬 전환에도 살아남는다. </summary>
        internal static void Enable() {
            if (_instance != null) {
                return;
            }

            GameObject holder = new GameObject(OBJECT_NAME) { hideFlags = HideFlags.DontSave };

            _instance = holder.AddComponent<DeskHitTest>();
        }

        /// <summary> 판정기를 내린다. 창은 마지막으로 걸린 상태 그대로 남는다. </summary>
        internal static void Disable() {
            if (_instance == null) {
                return;
            }

            Destroy(_instance.gameObject);
            _instance = null;
        }

        private void OnEnable() {
            _routine = StartCoroutine(SampleEachFrame());
        }

        private void OnDisable() {
            if (_routine != null) {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private void OnDestroy() {
            if (_sample != null) {
                Destroy(_sample);
                _sample = null;
            }

            if (_instance == this) {
                _instance = null;
            }
        }

        /// <summary> 화면이 다 그려진 뒤라야 픽셀을 읽을 수 있으므로 프레임 끝까지 기다린다. </summary>
        private IEnumerator SampleEachFrame() {
            WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

            while (true) {
                yield return endOfFrame;

                try {
                    if (_isProbeEnabled) {
                        SampleBackgroundAlpha();
                    }

                    WindowDeskAPI.SetPassThrough(!IsCursorOnDrawnPixel());
                }
                catch (Exception e) {
                    Debug.LogError($"[DeskHitTest] 커서 판정 중 예외가 발생했습니다: {e}");
                }
            }
        }

        /// <summary>
        /// 화면 왼쪽 아래 구석의 알파를 남긴다.
        /// 투명이 안 나올 때 유니티가 알파를 1로 밀고 있는지 창 쪽 문제인지 가르기 위한 값이다.
        /// 판정에는 쓰이지 않으므로 <see cref="IsBackgroundAlphaProbeEnabled"/> 가 켜졌을 때만 부른다.
        /// </summary>
        private void SampleBackgroundAlpha() {
            EnsureSampleTexture();

            _sample.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            _sample.Apply(false);

            LastBackgroundAlpha = _sample.GetPixel(0, 0).a;
        }

        /// <summary> 커서가 무언가 그려진 픽셀 위에 있는지 본다. 창 밖이면 그려진 것이 없는 것으로 친다. </summary>
        private bool IsCursorOnDrawnPixel() {
            if (!TryGetCursorPixel(out int x, out int y)) {
                return false;
            }

            EnsureSampleTexture();

            _sample.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            _sample.Apply(false);

            return _sample.GetPixel(0, 0).a >= OPAQUE_ALPHA;
        }

        /// <summary>
        /// 커서 위치를 화면 픽셀 좌표로 옮긴다.
        /// 클릭 통과 중에는 창에 마우스 메시지가 오지 않아 유니티 입력이 멈추므로 OS 좌표를 직접 읽는다.
        /// </summary>
        private static bool TryGetCursorPixel(out int x, out int y) {
            x = 0;
            y = 0;

            if (!DeskPlatform.TryGetCursorPosition(out Vector2Int cursor)
                || !DeskPlatform.TryGetWindowRect(WindowDeskAPI.WindowHandle, out RectInt window)) {
                return false;
            }

            int localX = cursor.x - window.x;
            int localY = cursor.y - window.y;

            // ReadPixels 는 좌하단이 원점이라 세로를 뒤집는다.
            x = localX;
            y = Screen.height - 1 - localY;

            return x >= 0 && x < Screen.width && y >= 0 && y < Screen.height;
        }

        /// <summary> 판정에 쓸 1픽셀 텍스처. 코드로 만들었으므로 임포트 설정과 무관하게 읽을 수 있다. </summary>
        private void EnsureSampleTexture() {
            if (_sample != null) {
                return;
            }

            _sample = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }
}
