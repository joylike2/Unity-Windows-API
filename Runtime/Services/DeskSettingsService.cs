using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeLogs.WindowUtil {

    /// <summary> 설정을 JSON 으로 주고받는 구현. 파일 저장은 사용하는 쪽이 맡는다. </summary>
    internal sealed class DeskSettingsService : IDeskSettingsService {

        /// <summary> 모니터 대체를 알리는 항목 이름. 재저장 판단에 쓰인다 </summary>
        internal const string FIELD_MONITOR = "monitor";
        private const string FIELD_RESOLUTION = "resolution";
        private const string FIELD_DISPLAY_MODE = "displayMode";

        /// <summary> 현재 상태를 JSON 문자열로 내보낸다. </summary>
        public string Export() {
            DeskResolution resolution = WindowDeskAPI.Resolution.GetApplied();
            DeskSettingsDto dto = new DeskSettingsDto {
                monitorIndex = WindowDeskAPI.Monitors.CurrentIndex,
                width = resolution.Width,
                height = resolution.Height,
                refreshRate = resolution.RefreshRate,
                displayMode = WindowDeskAPI.DisplayMode.Current.ToString(),
                topMost = WindowDeskAPI.WindowState.IsTopMost,
                resizable = WindowDeskAPI.WindowState.IsResizable,
                cursorConfined = WindowDeskAPI.WindowState.IsCursorConfined,
                targetFrameRate = DeskFrameRate.TargetFrameRate,
                backgroundFrameRate = DeskFrameRate.BackgroundTarget,
                powerSaving = DeskFrameRate.IsPowerSavingEnabled,
                vSync = DeskFrameRate.IsVSyncEnabled
            };

            if (WindowDeskAPI.Monitors.TryGetCurrent(out DeskMonitorInfo monitor)) {
                dto.monitorDeviceName = monitor.DeviceName;
            }

            return JsonUtility.ToJson(dto, true);
        }

        public DeskImportResult Import(string json) {
            return Import(json, DESK_IMPORT_OPTIONS.ALL);
        }

        /// <summary> JSON 을 검증한 뒤 지정한 항목만 적용한다. </summary>
        public DeskImportResult Import(string json, DESK_IMPORT_OPTIONS options) {
            if (string.IsNullOrEmpty(json)) {
                return DeskImportResult.Fail("불러올 JSON 이 비어 있습니다.");
            }

            DeskSettingsDto dto;

            try {
                dto = JsonUtility.FromJson<DeskSettingsDto>(json);
            }
            catch (Exception e) {
                return DeskImportResult.Fail($"JSON 을 읽지 못했습니다: {e.Message}");
            }

            if (dto == null) {
                return DeskImportResult.Fail("JSON 형식이 올바르지 않습니다.");
            }

            List<DeskSettingSubstitution> substitutions = new List<DeskSettingSubstitution>();
            int applied = 0;

            int targetMonitor = ApplyMonitor(dto, options, substitutions, ref applied);

            applied += ApplyResizable(dto, options);
            applied += ApplyTopMost(dto, options);
            applied += ApplyScreen(dto, options, substitutions);
            applied += ApplyCursorConfine(dto, options);
            applied += ApplyFrameRate(dto, options);

            RestoreMonitorAfterScreen(targetMonitor);

            return DeskImportResult.Success(applied, substitutions);
        }

        /// <summary>
        /// 해상도를 적용하면 유니티가 창을 자기 기준으로 다시 놓기 때문에 모니터 이동이 풀린다.
        /// 그래서 창이 자리를 잡은 다음 프레임에 저장된 모니터로 다시 옮기고 크기를 맞춘다.
        /// </summary>
        private static void RestoreMonitorAfterScreen(int monitorIndex) {
            if (monitorIndex < 0) {
                return;
            }

            DeskEventPump.RunNextFrame(() => WindowDeskAPI.FitWindowToMonitor(monitorIndex));
        }

        /// <summary> 장치명으로 모니터를 찾고, 없으면 주 모니터로 대체한다. 찾은 인덱스를 돌려준다. </summary>
        private static int ApplyMonitor(DeskSettingsDto dto, DESK_IMPORT_OPTIONS options,
                                        List<DeskSettingSubstitution> substitutions, ref int applied) {
            if ((options & DESK_IMPORT_OPTIONS.MONITOR) != DESK_IMPORT_OPTIONS.MONITOR) {
                return -1;
            }

            IDeskMonitorService monitors = WindowDeskAPI.Monitors;
            int targetIndex = FindMonitorIndex(monitors, dto, out bool wasSubstituted);

            if (targetIndex < 0) {
                substitutions.Add(new DeskSettingSubstitution(FIELD_MONITOR, dto.monitorDeviceName ?? "없음", "적용 안 함"));
                return -1;
            }

            if (wasSubstituted) {
                monitors.TryGetAt(targetIndex, out DeskMonitorInfo replacement);
                substitutions.Add(new DeskSettingSubstitution(FIELD_MONITOR,
                    dto.monitorDeviceName ?? $"인덱스 {dto.monitorIndex}", replacement.DeviceName));
            }

            // 프로파일별 경로를 타야 한다. 서비스에서 바로 부르면 바탕화면도 PC 게임 배치 규칙을 쓰게 된다.
            if (WindowDeskAPI.MoveWindowToMonitor(targetIndex).IsSuccess) {
                applied++;
            }

            return targetIndex;
        }

        /// <summary>
        /// 저장된 장치명으로만 찾는다. 없으면 주 모니터로 보낸다.
        /// 인덱스는 모니터를 다시 꽂으면 순서가 바뀌어 엉뚱한 모니터를 가리키므로 대체 수단으로도 쓰지 않는다.
        /// </summary>
        private static int FindMonitorIndex(IDeskMonitorService monitors, DeskSettingsDto dto, out bool wasSubstituted) {
            wasSubstituted = false;

            IReadOnlyList<DeskMonitorInfo> all = monitors.All;

            if (all.Count == 0) {
                return -1;
            }

            for (int i = 0; i < all.Count; i++) {
                if (all[i].DeviceName == dto.monitorDeviceName) {
                    return i;
                }
            }

            wasSubstituted = true;

            return monitors.PrimaryIndex >= 0 ? monitors.PrimaryIndex : 0;
        }

        /// <summary> 해상도와 표시 방식을 적용한다. 지원하지 않는 값은 대체된 사실을 남긴다. </summary>
        private static int ApplyScreen(DeskSettingsDto dto, DESK_IMPORT_OPTIONS options,
                                       List<DeskSettingSubstitution> substitutions) {
            bool wantsResolution = (options & DESK_IMPORT_OPTIONS.RESOLUTION) == DESK_IMPORT_OPTIONS.RESOLUTION;
            bool wantsMode = (options & DESK_IMPORT_OPTIONS.DISPLAY_MODE) == DESK_IMPORT_OPTIONS.DISPLAY_MODE;

            if (!wantsResolution && !wantsMode) {
                return 0;
            }

            DESK_DISPLAY_MODE mode = ResolveDisplayMode(dto, substitutions);

            if (!wantsResolution) {
                return WindowDeskAPI.DisplayMode.Apply(mode) ? 1 : 0;
            }

            if (dto.width <= 0 || dto.height <= 0) {
                substitutions.Add(new DeskSettingSubstitution(FIELD_RESOLUTION,
                    $"{dto.width}x{dto.height}", "적용 안 함"));
                return 0;
            }

            DeskResolution requested = new DeskResolution(dto.width, dto.height, dto.refreshRate);
            DeskResolutionApplyResult result = WindowDeskAPI.Resolution.Apply(requested, mode);

            if (!result.IsSuccess) {
                substitutions.Add(new DeskSettingSubstitution(FIELD_RESOLUTION, requested.ToString(), "적용 안 함"));
                return 0;
            }

            if (result.WasSubstituted) {
                substitutions.Add(new DeskSettingSubstitution(FIELD_RESOLUTION,
                    result.Requested.ToString(), result.Applied.ToString()));
            }

            return 1;
        }

        /// <summary> 전용 전체화면을 제공하던 시절의 저장값. 지금은 테두리없는 전체화면으로 읽는다 </summary>
        private const string LEGACY_FULLSCREEN = "FULLSCREEN";

        /// <summary> 저장된 표시 방식을 읽는다. 알 수 없는 값이면 창 모드로 대체한다. </summary>
        private static DESK_DISPLAY_MODE ResolveDisplayMode(DeskSettingsDto dto, List<DeskSettingSubstitution> substitutions) {
            // 열거에서 빠진 옛 값을 그대로 두면 파싱이 실패해 창 모드로 떨어진다. 전체화면 의도를 살린다.
            if (dto.displayMode == LEGACY_FULLSCREEN) {
                return DESK_DISPLAY_MODE.FULLSCREEN_WINDOW;
            }

            if (Enum.TryParse(dto.displayMode, out DESK_DISPLAY_MODE parsed)
                && WindowDeskAPI.DisplayMode.IsSupported(parsed)) {
                return parsed;
            }

            substitutions.Add(new DeskSettingSubstitution(FIELD_DISPLAY_MODE,
                string.IsNullOrEmpty(dto.displayMode) ? "없음" : dto.displayMode,
                DESK_DISPLAY_MODE.WINDOWED.ToString()));

            return DESK_DISPLAY_MODE.WINDOWED;
        }

        private static int ApplyTopMost(DeskSettingsDto dto, DESK_IMPORT_OPTIONS options) {
            if ((options & DESK_IMPORT_OPTIONS.TOP_MOST) != DESK_IMPORT_OPTIONS.TOP_MOST) {
                return 0;
            }

            return WindowDeskAPI.WindowState.SetTopMost(dto.topMost) ? 1 : 0;
        }

        /// <summary> 테두리 두께가 달라져 해상도 보정에 영향을 주므로 해상도보다 먼저 적용한다. </summary>
        private static int ApplyResizable(DeskSettingsDto dto, DESK_IMPORT_OPTIONS options) {
            if ((options & DESK_IMPORT_OPTIONS.RESIZABLE) != DESK_IMPORT_OPTIONS.RESIZABLE) {
                return 0;
            }

            return WindowDeskAPI.WindowState.SetResizable(dto.resizable) ? 1 : 0;
        }

        private static int ApplyCursorConfine(DeskSettingsDto dto, DESK_IMPORT_OPTIONS options) {
            if ((options & DESK_IMPORT_OPTIONS.CURSOR_CONFINE) != DESK_IMPORT_OPTIONS.CURSOR_CONFINE) {
                return 0;
            }

            return WindowDeskAPI.WindowState.SetCursorConfined(dto.cursorConfined) ? 1 : 0;
        }

        private static int ApplyFrameRate(DeskSettingsDto dto, DESK_IMPORT_OPTIONS options) {
            if ((options & DESK_IMPORT_OPTIONS.FRAME_RATE) != DESK_IMPORT_OPTIONS.FRAME_RATE) {
                return 0;
            }

            // 옛 파일이나 손댄 파일에서 0 이 들어올 수 있으므로 기본값으로 되돌린다.
            // 제한 없음(-1) 은 정상 값이라 그대로 살린다.
            int target = DeskFrameRate.IsValidTarget(dto.targetFrameRate)
                ? dto.targetFrameRate
                : DeskFrameRate.DEFAULT_TARGET;

            int background = dto.backgroundFrameRate > 0
                ? dto.backgroundFrameRate
                : DeskFrameRate.DEFAULT_BACKGROUND_TARGET;

            // 수직 동기화를 먼저 켜면 기준 프레임을 정할 때마다 "지금은 무시된다" 경고가 난다.
            DeskFrameRate.SetTargetFrameRate(target);
            DeskFrameRate.SetPowerSaving(dto.powerSaving, background);
            DeskFrameRate.SetVSync(dto.vSync);
            return 1;
        }
    }
}
