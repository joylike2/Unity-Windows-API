using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace LifeLogs.WindowUtil.Editor {

    /// <summary> 데스크탑용 바탕화면 게임에서 정상 동작하는 데 필요한 프로젝트 설정을 적용하고 검사하는 에디터 도구. (별도 도구로 분리) </summary>
    public static class WindowDeskSetup {
        private const string MENU_ROOT = "Tools/WindowDeskAPI/";
        private const string MENU_SETUP_WALLPAPER = "Setup/Desktop Wallpaper";
        private const string MENU_SETUP_PC_GAME = "Setup/PC Game";
        private const string MENU_SETUP_CAMERA = "Setup Camera";
        private const string MENU_SETUP_SCENE = "Setup Scene Scale";
        private const string MENU_VALIDATE_WALLPAPER = "Validate/Desktop Wallpaper";
        private const string MENU_VALIDATE_PC_GAME = "Validate/PC Game";

        private const int MENU_PRIORITY_SETUP_PC_GAME = 1;
        private const int MENU_PRIORITY_SETUP_WALLPAPER = 2;
        private const int MENU_PRIORITY_SETUP_CAMERA = 3;
        private const int MENU_PRIORITY_SETUP_SCENE = 4;
        private const int MENU_PRIORITY_VALIDATE_PC_GAME = 5;
        private const int MENU_PRIORITY_VALIDATE_WALLPAPER = 6;

        private const string TITLE_SETUP = "WindowDeskAPI 설정 적용";
        private const string TITLE_SETUP_CAMERA = "WindowDeskAPI 카메라 설정";
        private const string TITLE_VALIDATE = "WindowDeskAPI 설정 검사";

        private const string BUTTON_APPLY = "적용";
        private const string BUTTON_REAPPLY = "다시 적용";
        private const string BUTTON_CANCEL = "취소";
        private const string BUTTON_CLOSE = "닫기";
        private const string BUTTON_OK = "확인";

        /// <summary> 모든 안내창 상단에 붙는 경고문. 바탕화면 게임 전용 설정임을 매번 알림 </summary>
        private const string WARNING_BLOCK =
            "※ 주의 : 데스크탑 바탕화면 게임 전용 설정입니다.\n" +
            "  프로젝트 전역 설정(PlayerSettings)과 현재 씬의 카메라를 변경합니다.\n" +
            "  모바일 / 콘솔 / VR 등 다른 플랫폼에서는 제공되지 않는 기능입니다.\n\n";

        // Console 로그 전용 색상 정의.
        private const string COLOR_TITLE = "#2E9E8F";
        private const string COLOR_SECTION = "#C77B00";
        private const string COLOR_CHANGED = "#3C9A3C";
        private const string COLOR_SKIP = "#D13438";

        private const string UNDO_LABEL = "WindowDeskAPI Setup Camera";
        private const string MAIN_CAMERA_TAG = "MainCamera";

        private const CameraClearFlags TARGET_CLEAR_FLAGS = CameraClearFlags.SolidColor;

        /// <summary>
        /// 직교 투영을 요구한다. 관찰자가 모니터 배율을 반영할 때 orthographicSize 하나로 정할 수 있어야 한다.
        /// 원근은 거리와 화각이 함께 얽혀 있어 배율 하나로 결정되지 않는다.
        /// </summary>
        private const bool TARGET_ORTHOGRAPHIC = true;
        /// <summary>
        /// DWM 은 창을 프리멀티플라이드 알파로 합성한다.
        /// 알파가 0 인데 RGB 가 남아 있으면 그 색이 화면 전체에 더해져 색 끼가 낀다.
        /// </summary>
        private static readonly Color TARGET_BACKGROUND_COLOR = new Color(0f, 0f, 0f, 0f);
        private const ScriptingImplementation TARGET_SCRIPTING_BACKEND = ScriptingImplementation.IL2CPP;

        private static readonly NamedBuildTarget STANDALONE_TARGET = NamedBuildTarget.Standalone;

        /// <summary> PlayerSettings 실물 에셋 경로. 더티 표시 없이는 값을 대입해도 파일에 남지 않는다. </summary>
        private const string PLAYER_SETTINGS_ASSET_PATH = "ProjectSettings/ProjectSettings.asset";

        private const BuildTarget STANDALONE_BUILD_TARGET = BuildTarget.StandaloneWindows64;

        /// <summary> DWM 배경 투명은 Direct3D11 에서만 동작한다. Direct3D12 · Vulkan 에서는 전혀 나오지 않는다. </summary>
        private const GraphicsDeviceType TARGET_GRAPHICS_API = GraphicsDeviceType.Direct3D11;

        private const string LABEL_GRAPHICS_API = "Graphics API (Windows)";

        /// <summary> Windows 빌드가 실제로 쓸 그래픽 API. 자동이면 유니티가 고르므로 무엇이 될지 알 수 없다. </summary>
        private static string DescribeGraphicsApis() {
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(STANDALONE_BUILD_TARGET)) {
                return "Auto (무엇이 선택될지 보장되지 않음)";
            }

            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(STANDALONE_BUILD_TARGET);

            return apis.Length == 0 ? "없음" : string.Join(", ", apis);
        }

        /// <summary> 목록의 첫 번째가 실제로 쓰이므로 D3D11 하나만 남았는지 본다. </summary>
        private static bool IsGraphicsApiFixedToTarget() {
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(STANDALONE_BUILD_TARGET)) {
                return false;
            }

            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(STANDALONE_BUILD_TARGET);

            return apis.Length == 1 && apis[0] == TARGET_GRAPHICS_API;
        }

        private static void ApplyGraphicsApi() {
            PlayerSettings.SetUseDefaultGraphicsAPIs(STANDALONE_BUILD_TARGET, false);
            PlayerSettings.SetGraphicsAPIs(STANDALONE_BUILD_TARGET, new[] { TARGET_GRAPHICS_API });
        }

        /// <summary> PC 게임 셋업 안내창에 붙는 경고문 </summary>
        private const string WARNING_PC_GAME =
            "※ 주의 : 데스크탑 PC 게임 전용 설정입니다.\n" +
            "  프로젝트 전역 설정(PlayerSettings)을 변경합니다.\n" +
            "  모바일 / 콘솔 / VR 등 다른 플랫폼에서는 제공되지 않는 기능입니다.\n\n";
        
        /// <summary> 셋업 종류별 목표값 </summary>
        private readonly struct SetupProfile {
            public string Name { get; }
            public string Warning { get; }
            public FullScreenMode FullScreenMode { get; }
            /// <summary> null 이면 셋업이 이 값을 건드리지 않는다. </summary>
            public bool? ResizableWindow { get; }
            public bool FlipModelSwapchain { get; }
            public bool RunInBackground { get; }
            public bool RequiresTransparentCamera { get; }

            /// <summary> 렌더 파이프라인이 최종 출력에 알파를 남기도록 요구하는지 여부 </summary>
            public bool RequiresAlphaOutput { get; }

            public SetupProfile(string name, string warning, FullScreenMode fullScreenMode, bool? resizableWindow,
                                bool flipModelSwapchain, bool runInBackground, bool requiresTransparentCamera,
                                bool requiresAlphaOutput) {
                Name = name;
                Warning = warning;
                FullScreenMode = fullScreenMode;
                ResizableWindow = resizableWindow;
                FlipModelSwapchain = flipModelSwapchain;
                RunInBackground = runInBackground;
                RequiresTransparentCamera = requiresTransparentCamera;
                RequiresAlphaOutput = requiresAlphaOutput;
            }
        }

        /// <summary>
        /// 바탕화면 게임. 투명 창을 위해 창 모드와 알파 0 카메라가 필요하다.
        /// 창 영역은 프리셋이 정하므로 크기 조절은 처음부터 끈다.
        /// </summary>
        private static readonly SetupProfile WALLPAPER_PROFILE = new SetupProfile(
            "바탕화면 게임", WARNING_BLOCK, FullScreenMode.Windowed, false, false, true, true, true);

        /// <summary>
        /// 일반 PC 게임. 투명이 필요 없어 Flip Model 을 켜고 카메라도 건드리지 않는다.
        /// Resizable Window 는 밑바탕만 켜둔다. 꺼두면 런타임에 스타일 비트를 켜도 유니티가 크기 변경을 막는다.
        /// </summary>
        private static readonly SetupProfile PC_GAME_PROFILE = new SetupProfile(
            "PC 게임", WARNING_PC_GAME, FullScreenMode.FullScreenWindow, true, true, true, false, false);

        /// <summary> "실제로 반영된 결과"를 로그로 표기. </summary>
        private readonly struct SettingState {
            public string Label { get; }
            public string Value { get; }

            public SettingState(string label, string value) {
                Label = label;
                Value = value;
            }
        }

        /// <summary> 변경 대상 설정 한 건. 다이얼로그에 "현재값 → 목표값" 형태로 표시 </summary>
        private readonly struct SettingChange {
            private string Label { get; }
            private string Current { get; }
            private string Target { get; }

            public SettingChange(string label, string current, string target) {
                Label = label;
                Current = current;
                Target = target;
            }

            public override string ToString() {
                return $"  - {Label} : {Current} -> {Target}";
            }
        }

        #region 메뉴 1. Setup
        /// <summary> PlayerSettings 와 현재 씬 카메라를 데스크탑 창 모드에 맞게 한 번에 설정. </summary>
        [MenuItem(MENU_ROOT + MENU_SETUP_WALLPAPER, false, MENU_PRIORITY_SETUP_WALLPAPER)]
        public static void SetupWallpaper() {
            if (!Setup(WALLPAPER_PROFILE)) {
                return;
            }

            // 프로젝트 설정만 맞추고 씬을 두면 관찰자가 없어 배율이 반영되지 않는다. 한 번에 끝낸다.
            ApplyDesktopScene(SceneManager.GetActiveScene());
        }

        /// <summary> 일반 PC 게임에 맞게 PlayerSettings 를 설정. 카메라는 건드리지 않는다. </summary>
        [MenuItem(MENU_ROOT + MENU_SETUP_PC_GAME, false, MENU_PRIORITY_SETUP_PC_GAME)]
        public static void SetupPcGame() {
            Setup(PC_GAME_PROFILE);
        }

        /// <returns>실제로 적용했으면 true. 사용자가 취소했거나 실패하면 false.</returns>
        private static bool Setup(SetupProfile profile) {
            try {
                List<SettingChange> playerChanges = CollectPlayerSettingChanges(profile);
                playerChanges.AddRange(CollectRenderPipelineChanges(profile));

                bool needsCamera = profile.RequiresTransparentCamera;
                Camera camera = null;
                string cameraError = null;
                bool cameraResolved = needsCamera && TryResolveTargetCamera(out camera, out cameraError);
                List<SettingChange> cameraChanges = cameraResolved ? CollectCameraChanges(camera) : new List<SettingChange>();

                bool confirmed = playerChanges.Count == 0 && cameraChanges.Count == 0
                    ? ConfirmReapply(profile, cameraResolved, cameraError)
                    : ConfirmApply(profile, playerChanges, cameraChanges, camera, cameraResolved, cameraError);

                if (!confirmed) {
                    return false;
                }

                List<SettingState> playerBefore = CapturePlayerSettings(profile);
                playerBefore.AddRange(CaptureRenderPipelineSettings());
                List<SettingState> cameraBefore = cameraResolved ? CaptureCameraSettings(camera) : new List<SettingState>();

                ApplyPlayerSettings(profile);
                ApplyRenderPipelineSettings(profile);

                // 재적용 요청도 받으므로 변경 건수와 무관하게 대상이 확정되면 카메라를 덮어씁니다.
                bool cameraApplied = cameraResolved;
                if (cameraApplied) {
                    ApplyCameraSettings(camera);
                }

                List<SettingState> playerAfter = CapturePlayerSettings(profile);
                playerAfter.AddRange(CaptureRenderPipelineSettings());
                List<SettingState> cameraAfter = cameraApplied ? CaptureCameraSettings(camera) : new List<SettingState>();

                string skipReason = cameraResolved ? null : cameraError;
                LogCompletion(profile, playerBefore, playerAfter, cameraBefore, cameraAfter,
                              camera, cameraApplied, skipReason);

                if (needsCamera && !cameraApplied) {
                    NotifyCameraSkipped(cameraError);
                }

                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskSetup] 설정 적용 중 예외가 발생했습니다: {e}");
                EditorUtility.DisplayDialog(TITLE_SETUP,
                    $"설정 적용에 실패했습니다.\n\n{e.Message}\n\n자세한 내용은 Console 을 확인하십시오.", BUTTON_OK);
                return false;
            }
        }

        /// <summary> 바탕화면 셋업은 프로젝트 설정에 이어 현재 씬까지 맡는다는 것을 알린다. </summary>
        private static string DescribeSceneStep(SetupProfile profile) {
            if (!profile.RequiresTransparentCamera) {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine();
            builder.AppendLine($"이어서 현재 씬의 카메라 · 캔버스를 고치고 {OBSERVER_OBJECT_NAME} 를 만듭니다.");
            builder.AppendLine($"씬만 다시 하려면 {MENU_SETUP_SCENE} 를 쓰십시오.");

            return builder.ToString();
        }

        /// <summary> 카메라를 건드리지 못한 채 적용이 끝났을 때 띄우는 안내창. </summary>
        private static void NotifyCameraSkipped(string cameraError) {
            string message = $"카메라 건너뜀 : {cameraError}\n\n" +
                             $"플레이어 설정만 적용했습니다.\n" +
                             $"'{MENU_SETUP_CAMERA}' 를 실행하십시오.";

            EditorUtility.DisplayDialog(TITLE_SETUP_CAMERA, message, BUTTON_OK);
        }

        /// <summary> 변경할 항목이 있을 때 내역을 보여주고 적용 여부 확인 </summary>
        private static bool ConfirmApply(SetupProfile profile, List<SettingChange> playerChanges,
                                         List<SettingChange> cameraChanges, Camera camera,
                                         bool cameraResolved, string cameraError) {
            string message = BuildSetupMessage(profile, playerChanges, cameraChanges, camera, cameraResolved, cameraError);
            return EditorUtility.DisplayDialog(BuildTitle(profile), message, BUTTON_APPLY, BUTTON_CANCEL);
        }

        /// <summary> 안내창 제목에 셋업 종류를 붙인다. </summary>
        private static string BuildTitle(SetupProfile profile) {
            return $"{TITLE_SETUP} - {profile.Name}";
        }

        /// <summary> 이미 모든 값이 맞을 때 재적용 여부를 묻기. </summary>
        private static bool ConfirmReapply(SetupProfile profile, bool cameraResolved, string cameraError) {
            StringBuilder builder = new StringBuilder();
            builder.Append(profile.Warning);
            builder.AppendLine("모든 설정이 이미 올바릅니다.");

            if (profile.RequiresTransparentCamera && !cameraResolved) {
                builder.AppendLine();
                builder.AppendLine("[카메라] 건너뜀");
                builder.AppendLine($"  - {cameraError}");
            }

            builder.AppendLine();
            builder.AppendLine("같은 값으로 다시 적용하시겠습니까?");

            return EditorUtility.DisplayDialog(BuildTitle(profile), builder.ToString(), BUTTON_REAPPLY, BUTTON_CLOSE);
        }

        private static string BuildSetupMessage(SetupProfile profile, List<SettingChange> playerChanges,
                                                List<SettingChange> cameraChanges, Camera camera,
                                                bool cameraResolved, string cameraError) {
            StringBuilder builder = new StringBuilder();
            builder.Append(profile.Warning);
            builder.AppendLine("아래 항목이 변경됩니다.");

            if (playerChanges.Count > 0) {
                builder.AppendLine();
                builder.AppendLine("[플레이어 설정] (되돌릴 수 없음)");
                AppendChanges(builder, playerChanges);
            }

            if (profile.RequiresTransparentCamera && !cameraResolved) {
                builder.AppendLine();
                builder.AppendLine("[카메라] 건너뜀");
                builder.AppendLine($" - {cameraError}");
            }
            else if (cameraChanges.Count > 0) {
                builder.AppendLine();
                builder.AppendLine($"[카메라] {camera.name} (Ctrl+Z 로 되돌릴 수 있음)");
                AppendChanges(builder, cameraChanges);
            }

            builder.Append(DescribeSceneStep(profile));

            return builder.ToString();
        }

        /// <summary> 처리 결과 출력. - 카메라 미적용도 표시 </summary>
        private static void LogCompletion(SetupProfile profile,
                                          List<SettingState> playerBefore, List<SettingState> playerAfter,
                                          List<SettingState> cameraBefore, List<SettingState> cameraAfter,
                                          Camera camera, bool cameraApplied, string cameraSkipReason) {
            int totalCount = playerAfter.Count + cameraAfter.Count;
            int changedCount = CountChanged(playerBefore, playerAfter) + CountChanged(cameraBefore, cameraAfter);

            Debug.Log(Title($"[WindowDeskSetup] {profile.Name} 설정 적용 완료 (전체 {totalCount}건 중 {changedCount}건 변경)"));

            Debug.Log(Section("[WindowDeskSetup] 플레이어 설정 (되돌릴 수 없음)"));
            LogResults(playerBefore, playerAfter);

            if (!cameraApplied) {
                if (profile.RequiresTransparentCamera) {
                    Debug.Log(Bold(Colorize(
                        $"[WindowDeskSetup] 카메라 건너뜀 : {cameraSkipReason ?? "대상 카메라를 확정하지 못했습니다."}", COLOR_SKIP)));
                }

                return;
            }

            Debug.Log(Section($"[WindowDeskSetup] 카메라 {camera.name} (Ctrl+Z 로 되돌릴 수 있음)"));
            LogResults(cameraBefore, cameraAfter);
        }

        /// <summary> 처리 결과 출력. </summary>
        private static void LogResults(List<SettingState> before, List<SettingState> after) {
            for (int i = 0; i < after.Count; i++) {
                string previous = i < before.Count ? before[i].Value : null;
                string current = after[i].Value;
                bool changed = previous != current;

                string state = changed ? "[변경]" : "[유지]";
                string detail = changed ? $"{previous} -> {current}" : current;
                string line = $"{state} {after[i].Label} : {detail}";

                Debug.Log(changed ? Bold(Colorize(line, COLOR_CHANGED)) : line);
            }
        }

        private static string Colorize(string text, string color) => $"<color={color}>{text}</color>";

        private static string Bold(string text) => $"<b>{text}</b>";

        private static string Title(string text) => Bold(Colorize(text, COLOR_TITLE));

        private static string Section(string text) => Bold(Colorize(text, COLOR_SECTION));

        private static int CountChanged(List<SettingState> before, List<SettingState> after) {
            int count = 0;
            for (int i = 0; i < after.Count && i < before.Count; i++) {
                if (before[i].Value != after[i].Value) {
                    count++;
                }
            }
            return count;
        }

        private static void AppendChanges(StringBuilder builder, List<SettingChange> changes) {
            for (int i = 0; i < changes.Count; i++) {
                builder.AppendLine(changes[i].ToString());
            }
        }

        #endregion 메뉴 1. Setup

        #region 메뉴 2. Setup Camera

        /// <summary> 현재 씬의 대상 카메라 투명 배경용으로 설정 </summary>
        [MenuItem(MENU_ROOT + MENU_SETUP_CAMERA, false, MENU_PRIORITY_SETUP_CAMERA)]
        public static void SetupCamera() {
            try {
                Camera camera;
                string error;
                if (!TryResolveTargetCamera(out camera, out error)) {
                    Debug.LogWarning($"[WindowDeskSetup] 카메라 설정을 건너뛰었습니다: {error}");
                    EditorUtility.DisplayDialog(TITLE_SETUP_CAMERA, $"{WARNING_BLOCK}{error}", BUTTON_OK);
                    return;
                }

                List<SettingChange> changes = CollectCameraChanges(camera);
                if (changes.Count == 0) {
                    string message = $"{WARNING_BLOCK}{camera.name} 은(는) 이미 설정되어 있습니다.\n\n같은 값으로 다시 적용하시겠습니까?";
                    if (!EditorUtility.DisplayDialog(TITLE_SETUP_CAMERA, message, BUTTON_REAPPLY, BUTTON_CLOSE)) {
                        return;
                    }
                }

                List<SettingState> before = CaptureCameraSettings(camera);
                ApplyCameraSettings(camera);
                List<SettingState> after = CaptureCameraSettings(camera);

                LogCameraCompletion(before, after, camera);
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskSetup] 카메라 설정 중 예외가 발생했습니다: {e}");
                EditorUtility.DisplayDialog(TITLE_SETUP_CAMERA, $"카메라 설정에 실패했습니다.\n\n{e.Message}", BUTTON_OK);
            }
        }

        /// <summary> 현재 씬 카메라 체크 결정. </summary>
        private static bool TryResolveTargetCamera(out Camera camera, out string error) {
            camera = null;
            error = null;

            if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
                error = "프리팹 모드에서는 실행할 수 없습니다. 씬으로 빠져나온 뒤 다시 시도하십시오.";
                return false;
            }

            List<Camera> sceneCameras = CollectSceneCameras();
            if (sceneCameras.Count == 0) {
                error = "현재 씬에 카메라가 없습니다.";
                return false;
            }

            List<Camera> tagged = new List<Camera>();
            for (int i = 0; i < sceneCameras.Count; i++) {
                if (sceneCameras[i].CompareTag(MAIN_CAMERA_TAG)) {
                    tagged.Add(sceneCameras[i]);
                }
            }

            if (tagged.Count == 1) {
                camera = tagged[0];
                return true;
            }

            if (tagged.Count > 1) {
                error = $"MainCamera 태그가 붙은 카메라가 {tagged.Count} 개입니다. 하나만 남기십시오.\n{JoinNames(tagged)}";
                return false;
            }

            if (sceneCameras.Count == 1) {
                camera = sceneCameras[0];
                return true;
            }

            error = $"카메라가 {sceneCameras.Count} 개 있지만 MainCamera 태그가 붙은 카메라가 없습니다. 대상 카메라에 태그를 지정하십시오.\n{JoinNames(sceneCameras)}";
            return false;
        }

        private static List<Camera> CollectSceneCameras() {
            Camera[] found = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<Camera> result = new List<Camera>();

            for (int i = 0; i < found.Length; i++) {
                Camera candidate = found[i];

                // 실제 로드된 씬 카메라만 남기기.
                if (!candidate.gameObject.scene.IsValid()) {
                    continue;
                }

                if (candidate.hideFlags != HideFlags.None) {
                    continue;
                }

                result.Add(candidate);
            }

            return result;
        }

        private static string JoinNames(List<Camera> cameras) {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < cameras.Count; i++) {
                builder.AppendLine($"  - {cameras[i].name}");
            }
            return builder.ToString();
        }

        private static List<SettingChange> CollectCameraChanges(Camera camera) {
            List<SettingChange> changes = new List<SettingChange>();

            if (camera.clearFlags != TARGET_CLEAR_FLAGS) {
                changes.Add(new SettingChange("Clear Flags", camera.clearFlags.ToString(), TARGET_CLEAR_FLAGS.ToString()));
            }

            if (camera.orthographic != TARGET_ORTHOGRAPHIC) {
                changes.Add(new SettingChange("Projection",
                    DescribeProjection(camera.orthographic), DescribeProjection(TARGET_ORTHOGRAPHIC)));
            }

            if (!IsBackgroundColorReady(camera)) {
                changes.Add(new SettingChange("Background Color",
                    DescribeColor(camera.backgroundColor), DescribeColor(TARGET_BACKGROUND_COLOR)));
            }

            return changes;
        }

        /// <summary> 카메라 설정 항목 전체의 현재 값 수집. </summary>
        private static List<SettingState> CaptureCameraSettings(Camera camera) {
            return new List<SettingState> {
                new SettingState("Clear Flags", camera.clearFlags.ToString()),
                new SettingState("Background Color", DescribeColor(camera.backgroundColor)),
                new SettingState("Projection", DescribeProjection(camera.orthographic))
            };
        }

        /// <summary> 카메라만 설정했을 때의 완료 로그. 플레이어 설정 섹션 없이 카메라 결과만 남김. </summary>
        private static void LogCameraCompletion(List<SettingState> before, List<SettingState> after, Camera camera) {
            Debug.Log(Title($"[WindowDeskSetup] 카메라 설정 완료 (전체 {after.Count}건 중 {CountChanged(before, after)}건 변경)"));
            Debug.Log(Section($"[WindowDeskSetup] 카메라 {camera.name} (Ctrl+Z 로 되돌릴 수 있음)"));
            LogResults(before, after);
        }

        private static void ApplyCameraSettings(Camera camera) {
            Undo.RecordObject(camera, UNDO_LABEL);

            camera.clearFlags = TARGET_CLEAR_FLAGS;
            camera.backgroundColor = TARGET_BACKGROUND_COLOR;
            camera.orthographic = TARGET_ORTHOGRAPHIC;

            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        }

        #endregion 메뉴 2. Setup Camera

        #region 메뉴 2-2. Setup Desktop Scene

        private const string TITLE_SETUP_SCENE = "WindowDeskAPI 씬 설정";

        private const string OBSERVER_OBJECT_NAME = "DesktopGameObserver";

        private const string OBSERVER_PREFAB_PATH =
            "Assets/DeskWindows/Runtime/Prefabs/DesktopGameObserver.prefab";

        private const string SCENE_UNDO_LABEL = "WindowDeskAPI 씬 설정";

        private const string FIELD_CAMERAS = "_cameras";
        private const string FIELD_CANVAS_SCALERS = "_canvasScalers";

        /// <summary>
        /// 현재 씬의 카메라와 캔버스를 바탕화면 게임에 맞게 고치고, 관찰자 오브젝트에 전부 물려 준다.
        /// 기존 오브젝트를 지우지 않는다. 값만 고치고 없는 것만 새로 만든다.
        /// </summary>
        [MenuItem(MENU_ROOT + MENU_SETUP_SCENE, false, MENU_PRIORITY_SETUP_SCENE)]
        public static void SetupDesktopScene() {
            try {
                Scene scene = SceneManager.GetActiveScene();

                if (!scene.IsValid()) {
                    EditorUtility.DisplayDialog(TITLE_SETUP_SCENE, "열려 있는 씬이 없습니다.", BUTTON_OK);
                    return;
                }

                List<Camera> cameras = CollectInScene<Camera>(scene);
                List<CanvasScaler> scalers = CollectInScene<CanvasScaler>(scene);

                if (!ConfirmSceneSetup(scene, cameras, scalers)) {
                    return;
                }

                ApplyDesktopScene(scene);
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskSetup] 씬 설정 중 예외가 발생했습니다: {e}");
                EditorUtility.DisplayDialog(TITLE_SETUP_SCENE,
                    $"씬 설정에 실패했습니다.\n\n{e.Message}\n\n자세한 내용은 Console 을 확인하십시오.", BUTTON_OK);
            }
        }

        /// <summary>
        /// 씬의 카메라 · 캔버스를 고치고 관찰자에 물린다. 확인 다이얼로그는 부르는 쪽이 맡는다.
        /// </summary>
        /// <param name="scene">대상 씬.</param>
        private static void ApplyDesktopScene(Scene scene) {
            if (!scene.IsValid()) {
                Debug.LogWarning("[WindowDeskSetup] 열려 있는 씬이 없어 씬 설정을 건너뜁니다.");
                return;
            }

            List<Camera> cameras = CollectInScene<Camera>(scene);
            List<CanvasScaler> scalers = CollectInScene<CanvasScaler>(scene);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(SCENE_UNDO_LABEL);

            FixCameras(cameras);
            FixCanvasScalers(scalers);

            DeskSceneObserver observer = ResolveObserver(scene);
            Vector2Int bound = BindObserver(observer, cameras, scalers);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            LogSceneSetup(scene, cameras, scalers, bound);
        }

        /// <summary> 비활성 오브젝트까지 포함해 현재 씬에서만 모은다. 다른 씬이나 프리팹 스테이지는 건드리지 않는다. </summary>
        private static List<T> CollectInScene<T>(Scene scene) where T : Component {
            List<T> found = new List<T>();

            foreach (GameObject root in scene.GetRootGameObjects()) {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found;
        }

        private static bool ConfirmSceneSetup(Scene scene, List<Camera> cameras, List<CanvasScaler> scalers) {
            StringBuilder message = new StringBuilder();
            message.Append(WARNING_BLOCK);
            message.AppendLine($"씬 : {scene.name}");
            message.AppendLine();
            message.AppendLine($"카메라 {cameras.Count} 개 : Clear Flags 와 배경색을 투명용으로 맞춥니다.");
            message.AppendLine($"캔버스 {scalers.Count} 개 : Constant Pixel Size 로 맞춥니다.");
            message.AppendLine();
            message.AppendLine($"'{OBSERVER_OBJECT_NAME}' 오브젝트를 만들어 위 참조를 전부 물립니다.");
            message.AppendLine($"이미 있으면 지우고 새로 만듭니다. 다른 '{OBSERVER_OBJECT_NAME}' 는 남기지 않습니다.");
            message.AppendLine();
            message.AppendLine("카메라와 캔버스는 지우지 않습니다. 값만 고칩니다.");
            message.AppendLine("Ctrl+Z 로 전부 되돌릴 수 있습니다.");

            return EditorUtility.DisplayDialog(TITLE_SETUP_SCENE, message.ToString(), BUTTON_APPLY, BUTTON_CANCEL);
        }

        private static void FixCameras(List<Camera> cameras) {
            foreach (Camera target in cameras) {
                ApplyCameraSettings(target);
            }
        }

        /// <summary> scaleFactor 로 배율을 반영하려면 Constant Pixel Size 여야 한다. </summary>
        private static void FixCanvasScalers(List<CanvasScaler> scalers) {
            foreach (CanvasScaler scaler in scalers) {
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) {
                    continue;
                }

                Undo.RecordObject(scaler, SCENE_UNDO_LABEL);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
            }
        }

        /// <summary>
        /// 관찰자를 새로 만든다. 이미 있으면 지우고 다시 만든다.
        /// 도구가 만든 오브젝트라 지워도 잃을 것이 없고, 낡은 참조가 남는 편이 더 위험하다.
        /// </summary>
        private static DeskSceneObserver ResolveObserver(Scene scene) {
            RemoveExistingObservers(scene);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OBSERVER_PREFAB_PATH);
            GameObject holder;

            if (prefab != null) {
                holder = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                UnpackIfPrefabInstance(holder);
            }
            else {
                Debug.LogWarning($"[WindowDeskSetup] 프리팹을 찾지 못해 오브젝트를 새로 만듭니다: {OBSERVER_PREFAB_PATH}");
                holder = new GameObject(OBSERVER_OBJECT_NAME);
                holder.AddComponent<DeskSceneObserver>();
            }

            holder.name = OBSERVER_OBJECT_NAME;
            Undo.RegisterCreatedObjectUndo(holder, SCENE_UNDO_LABEL);

            return holder.GetComponent<DeskSceneObserver>();
        }

        /// <summary> 씬에 있던 관찰자를 전부 지운다. 여러 개 쌓여 있으면 어느 것이 도는지 알 수 없다. </summary>
        private static void RemoveExistingObservers(Scene scene) {
            List<DeskSceneObserver> existing = CollectInScene<DeskSceneObserver>(scene);

            foreach (DeskSceneObserver observer in existing) {
                if (observer == null) {
                    continue;
                }

                Debug.Log($"[WindowDeskSetup] 기존 {observer.name} 를 지우고 다시 만듭니다.");
                Undo.DestroyObjectImmediate(observer.gameObject);
            }
        }

        /// <summary>
        /// 프리팹 연결을 끊는다.
        /// 프리팹 에셋은 씬 오브젝트를 참조할 수 없어, 연결된 채로 두면 카메라 · 캔버스가 오버라이드로만 남는다.
        /// </summary>
        private static void UnpackIfPrefabInstance(GameObject holder) {
            if (!PrefabUtility.IsPartOfPrefabInstance(holder)) {
                return;
            }

            PrefabUtility.UnpackPrefabInstance(holder, PrefabUnpackMode.Completely,
                                               InteractionMode.AutomatedAction);
        }

        /// <summary>
        /// 참조는 private 직렬화 필드라 SerializedObject 로 넣는다.
        /// 넣은 뒤 다시 읽어 실제로 물린 개수를 돌려준다. 물렸다고 가정하지 않는다.
        /// </summary>
        private static Vector2Int BindObserver(DeskSceneObserver observer, List<Camera> cameras,
                                               List<CanvasScaler> scalers) {
            if (observer == null) {
                throw new InvalidOperationException($"{OBSERVER_OBJECT_NAME} 에 관찰자 컴포넌트가 없습니다.");
            }

            Undo.RecordObject(observer, SCENE_UNDO_LABEL);

            SerializedObject serialized = new SerializedObject(observer);

            AssignReferences(serialized.FindProperty(FIELD_CAMERAS), cameras);
            AssignReferences(serialized.FindProperty(FIELD_CANVAS_SCALERS), scalers);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(observer);

            SerializedObject verify = new SerializedObject(observer);

            return new Vector2Int(CountReferences(verify.FindProperty(FIELD_CAMERAS)),
                                  CountReferences(verify.FindProperty(FIELD_CANVAS_SCALERS)));
        }

        private static void AssignReferences<T>(SerializedProperty array, List<T> values) where T : Component {
            if (array == null) {
                Debug.LogError("[WindowDeskSetup] 관찰자에서 참조 배열을 찾지 못했습니다. 필드 이름이 바뀌었는지 확인하십시오.");
                return;
            }

            array.arraySize = values.Count;

            for (int i = 0; i < values.Count; i++) {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        /// <summary> 비어 있지 않은 칸만 센다. 참조가 날아갔는지 알아야 한다. </summary>
        private static int CountReferences(SerializedProperty array) {
            if (array == null) {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < array.arraySize; i++) {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue != null) {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 씬 설정 결과를 한 줄씩 남긴다.
        /// 한 덩어리로 남기면 Console 에서 접혀 어느 오브젝트가 물렸는지 보이지 않는다.
        /// </summary>
        private static void LogSceneSetup(Scene scene, List<Camera> cameras, List<CanvasScaler> scalers,
                                          Vector2Int bound) {
            bool allBound = bound.x == cameras.Count && bound.y == scalers.Count;

            Debug.Log(Section($"[WindowDeskSetup] ===== 씬 설정 완료 : {scene.name} (Ctrl+Z 로 되돌릴 수 있음) ====="));

            string summary = $"[WindowDeskSetup]   {OBSERVER_OBJECT_NAME} 에 실제로 물린 참조 : "
                             + $"카메라 {bound.x}/{cameras.Count}, 캔버스 {bound.y}/{scalers.Count}";

            if (allBound) {
                Debug.Log(summary);
            }
            else {
                Debug.LogError(summary);
            }

            foreach (Camera target in cameras) {
                Debug.Log($"[WindowDeskSetup]   - 카메라 {target.name}", target);
            }

            foreach (CanvasScaler scaler in scalers) {
                Debug.Log($"[WindowDeskSetup]   - 캔버스 {scaler.name}", scaler);
            }

            if (allBound) {
                return;
            }

            Debug.LogError("[WindowDeskSetup]   일부 참조가 물리지 않았습니다. "
                           + "관찰자가 프리팹 인스턴스로 남아 있는지 확인하십시오.");
        }

        #endregion 메뉴 2-2. Setup Desktop Scene

        #region 메뉴 3. Validate

        /// <summary> 바탕화면 게임 셋팅 검증. </summary>
        [MenuItem(MENU_ROOT + MENU_VALIDATE_WALLPAPER, false, MENU_PRIORITY_VALIDATE_WALLPAPER)]
        public static void ValidateWallpaper() {
            Validate(WALLPAPER_PROFILE);
        }

        /// <summary> PC 게임 셋팅 검증. </summary>
        [MenuItem(MENU_ROOT + MENU_VALIDATE_PC_GAME, false, MENU_PRIORITY_VALIDATE_PC_GAME)]
        public static void ValidatePcGame() {
            Validate(PC_GAME_PROFILE);
        }

        private static void Validate(SetupProfile profile) {
            try {
                StringBuilder report = new StringBuilder();
                report.AppendLine($"[WindowDeskSetup] 설정 검사 결과 ({profile.Name})");

                Debug.Log($"[WindowDeskSetup] ===== 설정 검사 시작 : {profile.Name} =====");

                bool allPassed = true;
                allPassed &= AppendCheck(report, "Fullscreen Mode",
                    PlayerSettings.fullScreenMode == profile.FullScreenMode,
                    PlayerSettings.fullScreenMode.ToString(), profile.FullScreenMode.ToString());
                if (profile.ResizableWindow.HasValue) {
                    allPassed &= AppendCheck(report, "Resizable Window",
                        PlayerSettings.resizableWindow == profile.ResizableWindow.Value,
                        PlayerSettings.resizableWindow.ToString(), profile.ResizableWindow.Value.ToString());
                }

                allPassed &= AppendCheck(report, "DXGI Flip Model Swapchain",
                    PlayerSettings.useFlipModelSwapchain == profile.FlipModelSwapchain,
                    PlayerSettings.useFlipModelSwapchain.ToString(), profile.FlipModelSwapchain.ToString());
                allPassed &= AppendCheck(report, "Run In Background",
                    PlayerSettings.runInBackground == profile.RunInBackground,
                    PlayerSettings.runInBackground.ToString(), profile.RunInBackground.ToString());

                ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(STANDALONE_TARGET);
                allPassed &= AppendCheck(report, "Scripting Backend (Standalone)",
                    backend == TARGET_SCRIPTING_BACKEND,
                    backend.ToString(), TARGET_SCRIPTING_BACKEND.ToString());

                allPassed &= AppendRenderPipelineChecks(report, profile);

                if (profile.RequiresTransparentCamera) {
                    allPassed &= AppendCameraChecks(report);
                }

                if (allPassed) {
                    Debug.Log($"[WindowDeskSetup] ===== 검사 통과 : {profile.Name} =====");
                }
                else {
                    Debug.LogError($"[WindowDeskSetup] ===== 검사 실패 : {profile.Name}."
                                   + " 위의 [실패] 줄을 확인하십시오 =====");
                }

                EditorUtility.DisplayDialog($"{TITLE_VALIDATE} - {profile.Name}", $"{profile.Warning}{report}", BUTTON_OK);
            }
            catch (Exception e) {
                Debug.LogError($"[WindowDeskSetup] 설정 검사 중 예외가 발생했습니다: {e}");
            }
        }

        private static bool AppendCameraChecks(StringBuilder report) {
            Camera camera;
            string error;
            if (!TryResolveTargetCamera(out camera, out error)) {
                report.AppendLine($"[실패] 카메라 : {error}");
                return false;
            }

            bool clearFlagsPassed = AppendCheck(report, $"카메라({camera.name}) Clear Flags",
                camera.clearFlags == TARGET_CLEAR_FLAGS,
                camera.clearFlags.ToString(), TARGET_CLEAR_FLAGS.ToString());

            bool alphaPassed = AppendCheck(report, $"카메라({camera.name}) Background Color",
                IsBackgroundColorReady(camera),
                DescribeColor(camera.backgroundColor), DescribeColor(TARGET_BACKGROUND_COLOR));

            bool projectionPassed = AppendCheck(report, $"카메라({camera.name}) Projection",
                camera.orthographic == TARGET_ORTHOGRAPHIC,
                DescribeProjection(camera.orthographic), DescribeProjection(TARGET_ORTHOGRAPHIC));

            return clearFlagsPassed && alphaPassed && projectionPassed;
        }

        /// <summary> 프리멀티플라이드 합성에 맞으려면 알파와 함께 RGB 도 0 이어야 한다. </summary>
        private static bool IsBackgroundColorReady(Camera camera) {
            Color background = camera.backgroundColor;

            return Mathf.Approximately(background.r, TARGET_BACKGROUND_COLOR.r)
                   && Mathf.Approximately(background.g, TARGET_BACKGROUND_COLOR.g)
                   && Mathf.Approximately(background.b, TARGET_BACKGROUND_COLOR.b)
                   && Mathf.Approximately(background.a, TARGET_BACKGROUND_COLOR.a);
        }

        private static string DescribeProjection(bool orthographic) {
            return orthographic ? "Orthographic" : "Perspective";
        }

        private static string DescribeColor(Color color) {
            return $"RGBA({color.r:0.###}, {color.g:0.###}, {color.b:0.###}, {color.a:0.###})";
        }

        private static bool AppendCheck(StringBuilder report, string label, bool passed, string current, string target) {
            string mark = passed ? "[정상]" : "[실패]";
            string detail = passed ? current : $"{current} (필요값: {target})";
            string line = $"  {mark} {label} : {detail}";

            report.AppendLine(line);
            LogCheckLine(line, passed);

            return passed;
        }

        /// <summary>
        /// 검사 결과를 한 줄씩 남긴다.
        /// 한 덩어리로 남기면 Console 에서 접혀 어느 항목이 걸렸는지 찾기 어렵다.
        /// </summary>
        private static void LogCheckLine(string line, bool passed) {
            if (passed) {
                Debug.Log($"[WindowDeskSetup]{line}");
                return;
            }

            Debug.LogError($"[WindowDeskSetup]{line}");
        }

        #endregion 메뉴 3. Validate

        #region 렌더 파이프라인 (URP)

        private const string PROP_ALPHA_OUTPUT = "m_AllowPostProcessAlphaOutput";

        private const string PROP_SUPPORTS_HDR = "m_SupportsHDR";

        private const string LABEL_ALPHA_OUTPUT = "Alpha Processing";

        private const string LABEL_HDR = "HDR";

        /// <summary>

        /// 알파 출력을 요구하지 않는 프로파일이 되돌릴 값. URP 에셋을 새로 만들었을 때의 값과 같다.

        /// 바꾸기 전 값을 기억하지 않는 이유는, 이 도구가 PlayerSettings 도 원본 없이 프로파일 값으로 덮기 때문이다.

        /// </summary>

        private const bool DEFAULT_HDR = true;

        private const bool DEFAULT_ALPHA_OUTPUT = false;

        /// <summary>

        /// 그래픽스 · 품질 설정이 참조하는 렌더 파이프라인 에셋을 모두 모은다.

        /// 품질 단계마다 다른 에셋을 쓸 수 있어 하나만 고쳐서는 빌드에서 어긋난다.

        /// </summary>

        private static List<RenderPipelineAsset> CollectRenderPipelineAssets() {

            List<RenderPipelineAsset> assets = new List<RenderPipelineAsset>();

            AddRenderPipelineAsset(assets, GraphicsSettings.defaultRenderPipeline);

            int originalLevel = QualitySettings.GetQualityLevel();

            try {

                for (int level = 0; level < QualitySettings.names.Length; level++) {

                    QualitySettings.SetQualityLevel(level, false);

                    AddRenderPipelineAsset(assets, QualitySettings.renderPipeline);

                }

            }

            finally {

                QualitySettings.SetQualityLevel(originalLevel, false);

            }

            return assets;

        }

        private static void AddRenderPipelineAsset(List<RenderPipelineAsset> assets, RenderPipelineAsset asset) {

            if (asset != null && !assets.Contains(asset)) {

                assets.Add(asset);

            }

        }

        /// <summary> URP 에셋이 아니면 알파 출력 항목이 없으므로 건너뛴다. </summary>

        private static bool TryGetAlphaOutputProperties(RenderPipelineAsset asset, out SerializedObject serialized,

                                                        out SerializedProperty alphaOutput) {

            serialized = new SerializedObject(asset);

            alphaOutput = serialized.FindProperty(PROP_ALPHA_OUTPUT);

            return alphaOutput != null;

        }

        /// <summary> 이 프로파일이 원하는 Alpha Processing 값. </summary>

        private static bool ResolveTargetAlphaOutput(SetupProfile profile) {

            return profile.RequiresAlphaOutput || DEFAULT_ALPHA_OUTPUT;

        }

        /// <summary>

        /// 이 프로파일이 원하는 HDR 값.

        /// 알파 출력이 필요하면 꺼야 한다. Unity 6 URP 는 HDR 이면 최종 출력 알파를 살리지 못한다.

        /// </summary>

        private static bool ResolveTargetHdr(SetupProfile profile) {

            return !profile.RequiresAlphaOutput && DEFAULT_HDR;

        }

        private static bool IsHdrEnabled(SerializedObject serialized) {

            SerializedProperty supportsHdr = serialized.FindProperty(PROP_SUPPORTS_HDR);

            return supportsHdr != null && supportsHdr.boolValue;

        }

        /// <summary> 렌더 파이프라인 쪽 변경 예정 목록. 다이얼로그에 그대로 실린다. </summary>

        private static List<SettingChange> CollectRenderPipelineChanges(SetupProfile profile) {

            List<SettingChange> changes = new List<SettingChange>();

            if (profile.RequiresAlphaOutput && !IsGraphicsApiFixedToTarget()) {

                changes.Add(new SettingChange(LABEL_GRAPHICS_API, DescribeGraphicsApis(),

                    TARGET_GRAPHICS_API.ToString()));

            }

            bool targetAlphaOutput = ResolveTargetAlphaOutput(profile);

            bool targetHdr = ResolveTargetHdr(profile);

            foreach (RenderPipelineAsset asset in CollectRenderPipelineAssets()) {

                if (!TryGetAlphaOutputProperties(asset, out SerializedObject serialized,

                                                 out SerializedProperty alphaOutput)) {

                    continue;

                }

                if (alphaOutput.boolValue != targetAlphaOutput) {

                    changes.Add(new SettingChange($"{asset.name} : {LABEL_ALPHA_OUTPUT}",

                        alphaOutput.boolValue.ToString(), targetAlphaOutput.ToString()));

                }

                if (IsHdrEnabled(serialized) != targetHdr) {

                    changes.Add(new SettingChange($"{asset.name} : {LABEL_HDR}",

                        IsHdrEnabled(serialized).ToString(), targetHdr.ToString()));

                }

            }

            return changes;

        }

        /// <summary>

        /// 렌더 파이프라인 에셋을 프로파일 값으로 맞춘다.

        /// 바탕화면은 알파를 남기도록, PC 게임은 기본값으로 되돌린다.

        /// </summary>

        private static void ApplyRenderPipelineSettings(SetupProfile profile) {

            List<RenderPipelineAsset> assets = CollectRenderPipelineAssets();

            if (assets.Count == 0) {

                if (profile.RequiresAlphaOutput) {

                    Debug.LogWarning("[WindowDeskSetup] 렌더 파이프라인 에셋을 찾지 못했습니다. " +

                                     "빌트인 파이프라인이라면 카메라 설정만으로 투명이 동작합니다.");

                }

                return;

            }

            bool targetAlphaOutput = ResolveTargetAlphaOutput(profile);

            bool targetHdr = ResolveTargetHdr(profile);

            bool changed = false;

            foreach (RenderPipelineAsset asset in assets) {

                if (!TryGetAlphaOutputProperties(asset, out SerializedObject serialized,

                                                 out SerializedProperty alphaOutput)) {

                    if (profile.RequiresAlphaOutput) {

                        Debug.LogWarning($"[WindowDeskSetup] {asset.name} 은 URP 에셋이 아니라 알파 출력 설정을 건너뜁니다.");

                    }

                    continue;

                }

                alphaOutput.boolValue = targetAlphaOutput;

                SerializedProperty supportsHdr = serialized.FindProperty(PROP_SUPPORTS_HDR);

                if (supportsHdr != null) {

                    supportsHdr.boolValue = targetHdr;

                }

                changed |= serialized.ApplyModifiedProperties();

            }

            if (changed) {

                AssetDatabase.SaveAssets();

            }

        }

        /// <summary> 렌더 파이프라인 항목의 현재 값 수집. </summary>

        private static List<SettingState> CaptureRenderPipelineSettings() {

            List<SettingState> states = new List<SettingState>();

            foreach (RenderPipelineAsset asset in CollectRenderPipelineAssets()) {

                if (!TryGetAlphaOutputProperties(asset, out SerializedObject serialized,

                                                 out SerializedProperty alphaOutput)) {

                    continue;

                }

                states.Add(new SettingState($"{asset.name} : {LABEL_ALPHA_OUTPUT}", alphaOutput.boolValue.ToString()));

                states.Add(new SettingState($"{asset.name} : {LABEL_HDR}", IsHdrEnabled(serialized).ToString()));

            }

            return states;

        }

        /// <summary> 렌더 파이프라인이 프로파일 값에 맞는지 검사한다. </summary>

        private static bool AppendRenderPipelineChecks(StringBuilder report, SetupProfile profile) {

            bool allPassed = true;

            if (profile.RequiresAlphaOutput) {

                allPassed = AppendCheck(report, LABEL_GRAPHICS_API, IsGraphicsApiFixedToTarget(),

                    DescribeGraphicsApis(), TARGET_GRAPHICS_API.ToString());

            }

            List<RenderPipelineAsset> assets = CollectRenderPipelineAssets();

            if (assets.Count == 0) {

                report.AppendLine("  [정상] 렌더 파이프라인 : 빌트인 (알파 출력 설정 없음)");

                return allPassed;

            }

            bool targetAlphaOutput = ResolveTargetAlphaOutput(profile);

            bool targetHdr = ResolveTargetHdr(profile);

            foreach (RenderPipelineAsset asset in assets) {

                if (!TryGetAlphaOutputProperties(asset, out SerializedObject serialized,

                                                 out SerializedProperty alphaOutput)) {

                    report.AppendLine($"  [정상] {asset.name} : URP 가 아니라 검사 대상이 아닙니다");

                    continue;

                }

                allPassed &= AppendCheck(report, $"{asset.name} : {LABEL_ALPHA_OUTPUT}",

                    alphaOutput.boolValue == targetAlphaOutput,

                    alphaOutput.boolValue.ToString(), targetAlphaOutput.ToString());

                allPassed &= AppendCheck(report, $"{asset.name} : {LABEL_HDR}",

                    IsHdrEnabled(serialized) == targetHdr,

                    IsHdrEnabled(serialized).ToString(), targetHdr.ToString());

            }

            return allPassed;

        }

        #endregion 렌더 파이프라인 (URP)

        #region PlayerSettings 공용

        private static List<SettingChange> CollectPlayerSettingChanges(SetupProfile profile) {
            List<SettingChange> changes = new List<SettingChange>();

            if (PlayerSettings.fullScreenMode != profile.FullScreenMode) {
                changes.Add(new SettingChange("Fullscreen Mode",
                    PlayerSettings.fullScreenMode.ToString(), profile.FullScreenMode.ToString()));
            }

            if (profile.ResizableWindow.HasValue && PlayerSettings.resizableWindow != profile.ResizableWindow.Value) {
                changes.Add(new SettingChange("Resizable Window",
                    PlayerSettings.resizableWindow.ToString(), profile.ResizableWindow.Value.ToString()));
            }

            if (PlayerSettings.useFlipModelSwapchain != profile.FlipModelSwapchain) {
                changes.Add(new SettingChange("DXGI Flip Model Swapchain",
                    PlayerSettings.useFlipModelSwapchain.ToString(), profile.FlipModelSwapchain.ToString()));
            }

            if (PlayerSettings.runInBackground != profile.RunInBackground) {
                changes.Add(new SettingChange("Run In Background",
                    PlayerSettings.runInBackground.ToString(), profile.RunInBackground.ToString()));
            }

            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(STANDALONE_TARGET);
            if (backend != TARGET_SCRIPTING_BACKEND) {
                changes.Add(new SettingChange("Scripting Backend (Standalone)",
                    backend.ToString(), TARGET_SCRIPTING_BACKEND.ToString()));
            }

            return changes;
        }

        /// <summary> 플레이어 설정 항목 전체의 현재 값 수집. </summary>
        private static List<SettingState> CapturePlayerSettings(SetupProfile profile) {
            List<SettingState> states = new List<SettingState> {
                new SettingState("Fullscreen Mode", PlayerSettings.fullScreenMode.ToString())
            };

            if (profile.ResizableWindow.HasValue) {
                states.Add(new SettingState("Resizable Window", PlayerSettings.resizableWindow.ToString()));
            }

            states.Add(new SettingState("DXGI Flip Model Swapchain", PlayerSettings.useFlipModelSwapchain.ToString()));

            if (profile.RequiresAlphaOutput) {
                states.Add(new SettingState(LABEL_GRAPHICS_API, DescribeGraphicsApis()));
            }

            states.Add(new SettingState("Run In Background", PlayerSettings.runInBackground.ToString()));
            states.Add(new SettingState("Scripting Backend (Standalone)",
                PlayerSettings.GetScriptingBackend(STANDALONE_TARGET).ToString()));

            return states;
        }

        private static void ApplyPlayerSettings(SetupProfile profile) {
            PlayerSettings.fullScreenMode = profile.FullScreenMode;

            if (profile.ResizableWindow.HasValue) {
                PlayerSettings.resizableWindow = profile.ResizableWindow.Value;
            }

            PlayerSettings.useFlipModelSwapchain = profile.FlipModelSwapchain;
            PlayerSettings.runInBackground = profile.RunInBackground;
            PlayerSettings.SetScriptingBackend(STANDALONE_TARGET, TARGET_SCRIPTING_BACKEND);

            if (profile.RequiresAlphaOutput) {
                ApplyGraphicsApi();
            }

            SavePlayerSettings();
            WarnIfNotApplied(profile);
        }

        /// <summary>
        /// 바꾼 PlayerSettings 를 파일에 남긴다.
        /// 대입만으로는 에셋이 더티로 잡히지 않아 저장도 인스펙터 갱신도 되지 않는다.
        /// </summary>
        private static void SavePlayerSettings() {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath(PLAYER_SETTINGS_ASSET_PATH);

            if (settings == null || settings.Length == 0 || settings[0] == null) {
                Debug.LogWarning($"[WindowDeskSetup] {PLAYER_SETTINGS_ASSET_PATH} 를 열지 못해 저장을 건너뜁니다. " +
                                 "File > Save Project 로 직접 저장하십시오.");
                return;
            }

            EditorUtility.SetDirty(settings[0]);
            AssetDatabase.SaveAssets();
        }

        /// <summary> 저장 뒤에도 목표값과 다르면 알린다. 조용히 넘어가면 원인을 찾을 수 없다. </summary>
        private static void WarnIfNotApplied(SetupProfile profile) {
            if (PlayerSettings.fullScreenMode != profile.FullScreenMode) {
                Debug.LogWarning($"[WindowDeskSetup] Fullscreen Mode 를 {profile.FullScreenMode} 로 바꾸지 못했습니다. " +
                                 $"현재 값 {PlayerSettings.fullScreenMode}");
            }

            if (profile.ResizableWindow.HasValue && PlayerSettings.resizableWindow != profile.ResizableWindow.Value) {
                Debug.LogWarning($"[WindowDeskSetup] Resizable Window 를 {profile.ResizableWindow.Value} 로 바꾸지 못했습니다. " +
                                 $"현재 값 {PlayerSettings.resizableWindow}");
            }

            if (PlayerSettings.useFlipModelSwapchain != profile.FlipModelSwapchain) {
                Debug.LogWarning($"[WindowDeskSetup] DXGI Flip Model Swapchain 을 {profile.FlipModelSwapchain} 로 바꾸지 못했습니다. " +
                                 $"현재 값 {PlayerSettings.useFlipModelSwapchain}");
            }

            if (profile.RequiresAlphaOutput && !IsGraphicsApiFixedToTarget()) {
                Debug.LogWarning($"[WindowDeskSetup] Graphics API 를 {TARGET_GRAPHICS_API} 로 고정하지 못했습니다. " +
                                 $"현재 값 {DescribeGraphicsApis()}");
            }
        }

        #endregion PlayerSettings 공용
    }
}
