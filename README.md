# Unity Windows API (v1.0.0)
　
　
## ✅ 소개
　
Unity 게임의 **데스크탑 창을 제어**하는 라이브러리입니다.
모니터 · 해상도 · 표시 방식 · 최상위 · 트레이 아이콘 같은 **PC 게임 옵션 화면에 필요한 것들**과,
투명 · 클릭 통과로 **바탕화면 위에서 도는 게임**을 함께 지원합니다.

게임 코드는 `WindowDeskAPI` **하나만 호출**하면 되고, 어느 OS 인지 알 필요가 없습니다.
플랫폼 구현은 빌드 타임에 하나만 컴파일되므로 게임 코드에 `#if` 분기가 전혀 없습니다.

　
　
　
## ⭐ 주요 특징
- **사용 목적 선언 방식**: `PC_GAME` 또는 `DESKTOP_GAME` 프로파일을 넘기면 필요한 기능이 한 번에 켜짐
- **선언하지 않은 기능은 조용히 무시되지 않음**: 호출하면 기능당 한 번 경고 로그를 남김
- **설정 자동 저장·복원**: 해상도 · 표시 방식 · 모니터 · 최상위 · 프레임 등을 JSON 파일로 관리
- **모니터 구성 변경 감시**: 모니터를 뽑거나 배율이 바뀌면 콜백으로 알림
- **바탕화면 게임**: 테두리없는 창 + DWM 투명 + **알파 판정 클릭 통과** (그려진 곳만 클릭됨)
- **트레이 아이콘**: 아이콘 · 툴팁 · 우클릭 메뉴 · 메뉴 테마
- **에디터 설정 도구**: PlayerSettings · URP · 카메라를 프로파일에 맞게 한 번에 맞추고 검사
- 인터페이스 기반 후킹: `IDeskInitializeListener` · `IDeskDisplayListener`

　
　
　
## 📌 설치 방법
- 순서
　
	Unity Package Manager 를 통해 가져올 수 있습니다.
	1. **Package Manager** 열기
	2. **Install package from git URL…** 선택
	3. 아래 URL 입력 후 설치

```none
https://github.com/joylike2/Unity-Windows-API.git
```

　
- 특정 버전을 고정하려면 태그를 붙입니다.

```none
https://github.com/joylike2/Unity-Windows-API.git#v1.0.0
```

　
- 또는 `Packages/manifest.json` 에 직접 적습니다.

```json
{
  "dependencies": {
    "dev.lifelogs.unity-windows-api": "https://github.com/joylike2/Unity-Windows-API.git"
  }
}
```

　
　
### 설치 후 반드시 할 것
　
**PlayerSettings 는 빌드 타임 설정이라 런타임 코드가 바꿀 수 없습니다.** 그래서 에디터 메뉴로 한 번 맞춰야 합니다.
실행하면 **바뀔 항목을 먼저 보여주고 확인을 묻습니다.**

| 메뉴 | 언제 |
|---|---|
| `Tools > WindowDeskAPI > Setup > PC Game` | 일반 PC 게임 |
| `Tools > WindowDeskAPI > Setup > Desktop Wallpaper` | 바탕화면 게임 |
| `Tools > WindowDeskAPI > Validate > PC Game` | 설정이 맞는지 검사 |
| `Tools > WindowDeskAPI > Validate > Desktop Wallpaper` | 설정이 맞는지 검사 |

> **바탕화면 게임은 하나라도 어긋나면 투명이 나오지 않습니다.** 빌드해 보기 전에 `Validate` 로 전부 `[정상]` 인지 확인하십시오.
> `PlayerSettings` 는 `Ctrl+Z` 로 되돌릴 수 없습니다. 적용 전 값이 Console 에 남으니 필요하면 그걸 보고 손으로 되돌리십시오.

　
　
　
## 📌 지원 환경
　
### 엔진
- **동작 확인: Unity 6 (6000.4)**
- **스크립팅 백엔드: Mono · IL2CPP 모두 동작 확인** — 모든 네이티브 호출이 P/Invoke 이므로 IL2CPP 에서 잘려 나가지 않는지 확인했습니다
- 렌더 파이프라인: Built-in · URP (바탕화면 게임의 투명은 URP 설정을 함께 맞춰야 합니다)

　
### 플랫폼
| 플랫폼 | 지원 |
|---|---|
| **Windows** | 전 기능 지원 (Win32 · DWM · Shell) |
| **macOS** | 창 제어 · 트레이 미지원. 호출하면 경고 로그만 남고 아무 일도 일어나지 않습니다 |
| 그 외 (Android · iOS · WebGL · Linux) | 컴파일은 되지만 전부 no-op. 모바일 빌드에서 에러가 나지 않도록 자리만 채웁니다 |

　
### 에디터에서는 초기화 자체가 무시됩니다
　
**에디터에서 `Initialize()` 는 경고 로그만 남기고 `false` 를 돌려줍니다.** 아무 상태도 만들지 않습니다.
허용하면 Unity 에디터 창 자체가 변형되기 때문입니다.

그래서 에디터에서는 **조회까지 포함해 라이브러리 기능 전체가 동작하지 않습니다.** 모니터 목록 · 해상도 목록 · 배율 · 프레임 제한 · 설정 저장 모두 마찬가지입니다. 절반만 켜 두면 무엇이 되고 무엇이 안 되는지 판단하기 어려워지므로 일부러 전부 막았습니다.

**모든 확인은 빌드에서 하십시오.**

```csharp
if (!WindowDeskAPI.IsWindowControlEnabled) {
    // 에디터이거나 창 제어를 지원하지 않는 플랫폼.
    // Initialize 를 불러도 무시된다
}
```

　
### 초기화하지 않으면 전부 취소됩니다
　
초기화 전에 기능을 부르면 **실행되지 않고 경고 로그가 남습니다.** 조용히 아무 일도 일어나지 않는 상황을 없애기 위해서입니다.

- 기능을 거치는 멤버(해상도 · 모니터 · 창 상태 등)는 **기능당 한 번** 경고합니다. 매 프레임 부르는 코드에서 로그가 쏟아지지 않게 하려고요
- 기능 선언과 무관한 멤버(프레임 · 배율 · 설정 삭제)는 부를 때마다 경고합니다
- **선언하지 않은 기능**도 같은 방식으로 막힙니다. `PC_GAME` 으로 초기화한 뒤 `SetBorderless()` 를 부르면 실행되지 않습니다

**트레이는 예외입니다.** 창 기능이 아니므로 초기화 없이 단독으로 동작합니다.

　
　
　
## 📌 모드별 지원 기능
　
사용 목적을 프로파일로 선언하면 필요한 기능이 한 번에 켜집니다.

| 기능 | `PC_GAME` | `DESKTOP_GAME` | 설명 |
|---|:---:|:---:|---|
| `MONITOR_INFO` | ✅ | ✅ | 모니터 목록 · 배치 · 작업 영역 조회, 창 이동 |
| `RESOLUTION_INFO` | ✅ | ⛔ | 지원 해상도 목록과 적용 |
| `DISPLAY_MODE` | ✅ | ✅ | 전체화면 · 창 전환 |
| `TOP_MOST` | ✅ | ✅ | 항상 위에 표시 |
| `WINDOW_PLACEMENT` | ✅ | ✅ | 창 크기 조절, 창 영역 보정 |
| `CURSOR_CONFINE` | ✅ | ⛔ | 마우스를 창 안에 가두기 |
| `BORDERLESS` | ➖ | ✅ | 제목 표시줄과 테두리 제거 |
| `TRANSPARENT` | ➖ | ✅ | 창 배경을 뚫어 바탕화면이 보이게 |
| `CLICK_THROUGH` | ➖ | ✅ | 빈 곳의 클릭을 바탕화면으로 넘김 |
| `TASKBAR_BUTTON` | ➖ | ➖ | 작업표시줄 버튼 표시 여부 |

- ✅ 프로파일이 자동으로 켬
- ⛔ **프로파일이 일부러 막음.** 다른 프로파일과 함께 선언해도 꺼집니다
- ➖ 자동으로 켜지지 않음. 필요하면 `extraFeatures` 로 추가

　
### `DESKTOP_GAME` 이 일부러 막는 기능
　
- **`CURSOR_CONFINE`** : 창이 화면 전체를 덮으므로 커서를 가두면 다른 창도 바탕화면도 쓸 수 없습니다
- **`RESOLUTION_INFO`** : 창을 모니터 전체로 펼치므로 고를 해상도가 없습니다

　
### 트레이는 프로파일과 무관
　
트레이 아이콘은 창 기능이 아니므로 **프로파일 선언 없이 단독으로** 쓸 수 있습니다. `Initialize` 를 부르지 않아도 됩니다.

　
　
　
## 📌 사용 방법
　
### 초기화
　
```csharp
using LifeLogs.WindowUtil;

public class GameBootstrap : MonoBehaviour {

    private void Awake() {
        // PC 게임
        WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE.PC_GAME);

        // 바탕화면 게임
        // WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE.DESKTOP_GAME);

        // 프로파일에 없는 기능을 더할 때
        // WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE.PC_GAME, DESK_WINDOW_FEATURE.TASKBAR_BUTTON);

        // 프로파일 없이 필요한 것만
        // WindowDeskAPI.Initialize(DESK_WINDOW_FEATURE.MONITOR_INFO | DESK_WINDOW_FEATURE.RESOLUTION_INFO);
    }
}
```

**`Awake` 를 권합니다.** 가장 이르고, 다른 시스템이 해상도를 읽기 전에 확정되기 때문입니다.

**초기화하지 않으면 어떤 기능도 실행되지 않습니다.** 반환값은 선언한 기능이 전부 사용 가능하고 창 핸들도 확보했을 때 `true` 입니다.

> **초기화는 창 상태를 인자로 받지 않습니다.** 해상도 · 표시 방식 · 창 크기 조절 · 최상위 · 프레임은 모두 **저장 파일이 기준**이고, 런타임 전환은 개별 함수로 합니다.
> 저장 파일이 없으면 기준 프레임 `60`, 창 크기 조절 `허용`, 해상도는 유니티가 띄운 시작 크기를 그대로 파일에 적습니다.

　
### 해상도
　
```csharp
// 옵션 화면 드롭다운을 채울 목록. 큰 것부터 정렬되고 같은 크기는 최고 주사율 하나만 남는다
IReadOnlyList<DeskResolution> list = WindowDeskAPI.GetSupportedResolutions();

// 적용
DeskResolutionApplyResult result = WindowDeskAPI.ApplyResolution(list[0]);

if (!result.IsSuccess) {
    Debug.LogWarning(result.ErrorMessage);
}
else if (result.WasSubstituted) {
    // 지원하지 않는 값이라 가까운 것으로 대체됨
    Debug.Log($"{result.Requested} -> {result.Applied}");
}

// 조회
DeskResolution applied = WindowDeskAPI.GetAppliedResolution();   // 게임에 적용된 값
DeskResolution monitor = WindowDeskAPI.GetMonitorResolution();   // OS 디스플레이 해상도
```

**창 모드에서 모니터와 같은 해상도를 고르면** 제목 표시줄과 작업표시줄 때문에 창이 화면보다 커집니다. 이때 **비율을 유지한 채** 작업 영역에 맞춰 줄입니다. 전체화면으로 가면 고른 값으로 되돌아갑니다.

　
### 표시 방식
　
```csharp
WindowDeskAPI.ApplyDisplayMode(DESK_DISPLAY_MODE.FULLSCREEN_WINDOW);   // 전체화면
WindowDeskAPI.ApplyDisplayMode(DESK_DISPLAY_MODE.WINDOWED);            // 창

DESK_DISPLAY_MODE now = WindowDeskAPI.CurrentDisplayMode;
```

**게임 옵션의 "전체화면" 은 `FULLSCREEN_WINDOW` 로 제공하십시오.** 테두리없는 창이 모니터 전체를 덮고, 게임은 고른 해상도의 백버퍼로 그려 화면에 맞춰 확대됩니다.

> **전용 전체화면(`ExclusiveFullScreen`)은 제공하지 않습니다.** Unity 가 그 모드에서 고른 해상도를 무시하고 모니터 해상도로 강제합니다. `SetResolution` 재요청 · 모드와 크기 분리 요청 · `DXGI Flip Model` 끄기 · `ChangeDisplaySettingsEx` 로 OS 모드 직접 전환까지 모두 시도했지만 해결되지 않아 열거에서 제외했습니다. `FULLSCREEN_WINDOW` 가 화면을 꽉 채우면서 해상도를 지키고 전환도 더 빠르므로 유저에게 손실은 없습니다.

> 실제 적용은 **한 프레임 뒤**에 끝납니다. Unity 가 창을 다시 만들기 때문입니다. 완료는 `OnDisplayModeChanged` 로 확인하십시오.

　
### 모니터
　
```csharp
IReadOnlyList<DeskMonitorInfo> monitors = WindowDeskAPI.GetMonitors();   // OS 열거 순
int current = WindowDeskAPI.CurrentMonitorIndex;
int primary = WindowDeskAPI.PrimaryMonitorIndex;

WindowDeskAPI.MoveWindowToMonitor(1);
WindowDeskAPI.MonitorLostPolicy = DESK_MONITOR_LOST_POLICY.MOVE_TO_PRIMARY;
```

**인덱스는 모니터를 뽑으면 밀립니다.** 사람에게 보여주거나 저장할 때는 `DeviceName` 을 쓰십시오. 저장 파일도 장치명을 먼저 찾고 인덱스는 대체 수단으로만 씁니다.

**OS 열거 순서와 물리 배치 순서는 일치하지 않습니다.** `0` 번이 오른쪽에 있을 수 있습니다. 옵션 화면에 모니터를 그림으로 늘어놓을 때는 배치 순서로 세우십시오.

```csharp
IReadOnlyList<DeskMonitorInfo> monitors = WindowDeskAPI.GetMonitors();

foreach (int monitorIndex in WindowDeskAPI.GetLeftToRightOrder()) {
    DeskMonitorInfo monitor = monitors[monitorIndex];

    // monitor.Bounds 는 가상 데스크탑 좌표라 그대로 축소하면 실제 배치 그림이 된다
    // 클릭하면 WindowDeskAPI.MoveWindowToMonitor(monitorIndex)
}
```

　
### 작업 영역과 작업표시줄
　
작업표시줄이 차지하는 두께를 네 변으로 돌려줍니다. 오브젝트가 작업표시줄 위로 넘어가지 않게 막을 때 씁니다.
작업표시줄은 아래에만 있는 것이 아니라 위 · 좌 · 우 어디든 올 수 있어 네 변을 모두 담습니다.

```csharp
DeskEdgeInsets insets = WindowDeskAPI.GetWorkAreaInsets();         // 픽셀
DeskEdgeInsets scaled = WindowDeskAPI.GetScaledWorkAreaInsets();   // DPI 배율 반영

RectInt clamped = WindowDeskAPI.ClampToWorkArea(rect);
Vector2Int point = WindowDeskAPI.ClampToWorkArea(position);
```

　
### 창 상태
　
```csharp
WindowDeskAPI.SetTopMost(true);            // 항상 위
WindowDeskAPI.SetResizable(false);         // 드래그로 크기 변경 차단
WindowDeskAPI.SetBorderless(true);         // 테두리 제거
WindowDeskAPI.SetTaskbarButtonVisible(false);
WindowDeskAPI.SetCursorConfined(true);     // 마우스를 창 안에 가두기

bool requested = WindowDeskAPI.IsTopMostRequested;   // 게임이 요청한 값
bool applied = WindowDeskAPI.IsTopMostApplied();     // OS 에 실제로 걸린 값
```

**요청값과 실제 상태는 다를 수 있습니다.** Unity 는 해상도나 표시 방식을 바꿀 때마다 창 스타일을 PlayerSettings 값으로 덮습니다. 라이브러리가 요청값을 기억해 **매 프레임 다시 걸어 주므로** 게임이 신경 쓸 필요는 없습니다. 옵션 화면 체크박스에는 `IsTopMostRequested` 를 쓰십시오.

**전체화면에서는 최상위를 걸지 않습니다.** Alt+Tab 으로 전환한 창이 가려지기 때문입니다. 창 모드로 돌아오면 그때 다시 걸립니다.

　
### 프레임과 절전
　
```csharp
WindowDeskAPI.SetTargetFrameRate(144);
WindowDeskAPI.SetTargetFrameRate(WindowDeskAPI.UNLIMITED_FRAME_RATE);   // 제한 없음

WindowDeskAPI.SetPowerSaving(true);        // 창이 뒤로 가면 15fps
WindowDeskAPI.SetPowerSaving(true, 30);    // 배경 프레임 지정
WindowDeskAPI.SetVSync(true);
```

**기본값은 60 입니다.** 저장 파일이 없으면 60 으로 시작해 파일에 적고, 있으면 저장값으로 복원합니다.

**VSync 를 켜면 기준 프레임은 무시됩니다.** Unity 는 `vSyncCount > 0` 이면 `targetFrameRate` 를 보지 않습니다. 값은 기억해 두었다가 VSync 를 끄면 그때 적용되고, 부를 때 경고 로그로 알려줍니다.

　
### 설정 저장
　
```csharp
WindowDeskAPI.SaveSettings();               // 옵션 화면에서 확인을 눌렀을 때
bool has = WindowDeskAPI.HasSavedSettings;
string path = WindowDeskAPI.SettingsFilePath;
WindowDeskAPI.DeleteSettings();

// JSON 문자열로 직접 다루기
string json = WindowDeskAPI.ExportSettings();
WindowDeskAPI.ImportSettings(json, DESK_IMPORT_OPTIONS.SCREEN_ONLY);
```

암호화하지 않은 JSON 입니다. 복원 대상은 **해상도 · 표시 방식 · 모니터 · 최상위 · 창 크기 조절 · 마우스 가두기 · 프레임** 이고, 선언한 기능만 복원합니다.

**PC 게임에서 창을 끌어 다른 모니터로 옮기면 자동으로 저장합니다.** 유저가 드래그하고 나서 저장 버튼을 따로 누르지는 않기 때문입니다.

| 옮긴 방법 | 자동 저장 |
|---|:---:|
| PC 게임 · 창을 드래그 | ✅ |
| PC 게임 · `MoveWindowToMonitor()` 호출 | ⛔ |
| 바탕화면 게임 · 옵션에서 선택 | ⛔ |
| 저장 파일 복원 중 모니터 이동 | ⛔ |

코드로 옮긴 이동은 저장하지 않습니다. **옵션 화면에서 고른 값이 확인을 누르기 전에 굳어 버리면 취소가 무의미해지기** 때문입니다. 그쪽은 게임이 `SaveSettings()` 를 부르십시오.

　
### 바탕화면 게임
　
```csharp
WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE.DESKTOP_GAME);

bool transparent = WindowDeskAPI.IsTransparent;
bool clickThrough = WindowDeskAPI.IsClickThrough;
bool passingNow = WindowDeskAPI.IsPassingThroughNow;   // 지금 커서 아래가 빈 곳인지
float alpha = WindowDeskAPI.BackgroundAlpha;           // 커서 아래 픽셀 알파
string report = WindowDeskAPI.TransparentReport;       // 투명이 안 나올 때 원인
```

초기화만 하면 **테두리없는 창 → 모니터 전체 덮기 → 투명 → 클릭 통과**가 순서대로 걸립니다.

**클릭 통과는 알파 판정입니다.** 커서 아래 픽셀이 투명하면 클릭을 바탕화면으로 넘기고, 그려진 곳이면 게임이 받습니다. 매 프레임 커서 위치의 알파를 읽어 판정합니다.

**빠져나올 수단을 반드시 남기십시오.** 창이 화면 전체를 덮고 클릭이 통과되므로, 트레이 메뉴나 단축키 없이는 게임을 끌 수 없습니다.

`Runtime/Prefabs/DesktopGameObserver.prefab` 를 씬에 넣으면 모니터 배율이 바뀔 때 캔버스와 카메라를 자동으로 다시 맞춥니다. `Tools > WindowDeskAPI > Setup Scene Scale` 로도 붙일 수 있습니다.

　
### 트레이 아이콘
　
```csharp
// 가장 짧은 사용법. Initialize 와 무관하게 단독으로 부를 수 있다
WindowDeskAPI.EnableTray("내 게임");
WindowDeskAPI.AddTrayMenuItem("게임 창 열기", () => WindowDeskAPI.FocusGameWindow());
WindowDeskAPI.AddTrayMenuSeparator();
WindowDeskAPI.AddTrayMenuItem("종료", Application.Quit);
```

```csharp
// 아이콘과 테마 지정
WindowDeskAPI.EnableTray("내 게임", myTexture, DESK_MENU_THEME.DARK);
WindowDeskAPI.EnableTray("내 게임", "C:/icons/tray.ico");

WindowDeskAPI.SetTrayIcon(otherTexture);
WindowDeskAPI.SetTrayTooltip("바뀐 툴팁");
WindowDeskAPI.SetTrayMenuTheme(DESK_MENU_THEME.SYSTEM);
WindowDeskAPI.ClearTrayMenu();
WindowDeskAPI.DisableTray();

bool supported = WindowDeskAPI.IsTraySupported;
```

메뉴 테마는 `LIGHT` · `DARK` · `SYSTEM` 중에 고릅니다. `SYSTEM` 은 윈도우 설정을 따라갑니다.

　
#### 클릭 동작

| 동작 | 결과 |
|---|---|
| **좌클릭** | 게임 창을 앞으로 가져옵니다 (`FocusGameWindow()`). 별도 등록이 필요 없습니다 |
| **우클릭** | 등록한 메뉴를 띄웁니다. 항목이 없으면 아무 일도 일어나지 않습니다 |

메뉴 항목의 `Action` 은 **유니티 메인 스레드에서** 불립니다. 그대로 게임 로직을 호출하면 됩니다.

　
#### 바탕화면 게임의 탈출구

**바탕화면 게임은 트레이 메뉴를 반드시 두십시오.** 창이 화면 전체를 덮고 클릭이 바탕화면으로 통과되므로, 트레이 없이는 게임을 끌 방법이 없습니다.

```csharp
private void Awake() {
    WindowDeskAPI.Initialize(DESK_WINDOW_PROFILE.DESKTOP_GAME);
    EnableExitTray();
}

private void EnableExitTray() {
    if (!WindowDeskAPI.IsTraySupported) {
        return;
    }

    WindowDeskAPI.EnableTray("내 바탕화면 게임", DESK_MENU_THEME.SYSTEM);
    WindowDeskAPI.AddTrayMenuItem("게임 창 앞으로", () => WindowDeskAPI.FocusGameWindow());
    WindowDeskAPI.AddTrayMenuSeparator();
    WindowDeskAPI.AddTrayMenuItem("종료", Application.Quit);
}
```

　
> 트레이 아이콘의 **표시 순서는 지정할 수 없습니다.** `Shell_NotifyIcon` 에 순서 필드가 없고 위치는 Explorer 가 정합니다. 유저가 드래그로만 바꿀 수 있습니다.

> 트레이는 **Windows 전용**입니다. 다른 플랫폼에서는 `IsTraySupported` 가 `false` 이고 모든 호출이 no-op 입니다.

　
　
　
## 📌 후킹 — 인터페이스와 이벤트 콜백
　
### 초기화 완료 후킹
　
`IDeskInitializeListener` 를 구현하고 등록합니다.
**등록 시점에 이미 초기화가 끝나 있으면 등록 즉시 한 번 불립니다.** 그래서 실행 순서를 맞출 필요가 없습니다.

```csharp
using LifeLogs.WindowUtil;
using UnityEngine;

public class OptionScreen : MonoBehaviour, IDeskInitializeListener {

    private void OnEnable() {
        WindowDeskAPI.AddInitializeListener(this);
    }

    private void OnDisable() {
        // 오브젝트가 사라지기 전에 반드시 해제하십시오
        WindowDeskAPI.RemoveInitializeListener(this);
    }

    public void OnDeskInitialized(DESK_WINDOW_PROFILE profiles) {
        // 이 시점에 모니터 정보와 해상도 목록이 확정된다
        RefreshResolutionDropdown();
    }
}
```

　
### 화면 변경 후킹
　
`IDeskDisplayListener` 는 알림 7종을 한 인터페이스로 받습니다. **필요 없는 알림은 빈 본문으로 두십시오.**

```csharp
using System.Collections.Generic;
using LifeLogs.WindowUtil;
using UnityEngine;

public class ScreenOptionPanel : MonoBehaviour, IDeskDisplayListener {

    private IReadOnlyList<DeskResolution> _resolutions;

    private void OnEnable() {
        WindowDeskAPI.AddDisplayListener(this);
    }

    private void OnDisable() {
        WindowDeskAPI.RemoveDisplayListener(this);
    }

    /// <summary> 창이 다른 모니터로 넘어갔다. 해상도 목록을 다시 뽑아야 한다 </summary>
    public void OnCurrentMonitorChanged(int monitorIndex) {
        _resolutions = WindowDeskAPI.GetSupportedResolutions();
    }

    /// <summary> 해상도가 바뀌었다 </summary>
    public void OnResolutionChanged(DeskResolution resolution) {
        Debug.Log($"해상도 {resolution}");
    }

    /// <summary> 표시 방식이 바뀌었다. 전환이 끝난 뒤에 온다 </summary>
    public void OnDisplayModeChanged(DESK_DISPLAY_MODE mode) {
        Debug.Log($"표시 방식 {mode}");
    }

    /// <summary> 모니터가 연결되거나 빠졌다. 배치나 해상도가 바뀌어도 온다 </summary>
    public void OnDisplayConfigurationChanged(DeskMonitorLayout layout) {
        Debug.Log($"모니터 {layout.All.Count}대");
    }

    /// <summary> 창이 놓여 있던 모니터가 사라졌다. 정책에 따라 창은 이미 옮겨졌다 </summary>
    public void OnCurrentMonitorLost(int lostIndex) {
        Debug.LogWarning($"모니터 {lostIndex} 사라짐");
    }

    /// <summary> 모니터 배율이 바뀌었다. Constant Pixel Size 캔버스는 이 배수만큼 맞춘다 </summary>
    public void OnDpiScaleChanged(float scaleRatio) {
        Debug.Log($"배율 x{scaleRatio:0.###}");
    }

    /// <summary> 창이 활성 · 비활성되었다 </summary>
    public void OnWindowFocusChanged(bool hasFocus) {
        // 예: 포커스를 잃으면 게임 일시정지
    }
}
```

　
### 후킹 시 지켜야 할 것
　
- **`OnDisable` 에서 반드시 해제하십시오.** 파괴된 오브젝트가 목록에 남으면 알림을 보내다 예외가 납니다
- 알림 처리 중 예외가 나도 **나머지 수신자는 정상적으로 받습니다.** 각각 따로 감싸 두었습니다
- `OnDpiScaleChanged` 는 **배율이 같은 모니터끼리 옮기면 오지 않습니다.** 해상도만 달라진 경우까지 잡으려면 `OnCurrentMonitorChanged` 도 함께 보십시오
- 표시 방식과 해상도 적용은 **한 프레임 뒤**에 끝납니다. 적용 직후 `Screen.width` 를 읽지 말고 콜백을 기다리십시오

　
### DPI 배율을 캔버스에 반영하기
　
직접 후킹하지 않고 컴포넌트에 맡길 수도 있습니다.

```csharp
// 코드로 한 번에
int count = WindowDeskAPI.ApplyDpiScaleToAllCanvases();
float scale = WindowDeskAPI.CurrentDpiScale;
```

| 컴포넌트 | 역할 |
|---|---|
| `DeskSceneObserver` | 씬 전체의 캔버스 · 카메라를 배율 변경에 맞춰 다시 계산 |
| `DeskDpiScaleBinder` | 캔버스 하나만 맡김 |

`DeskSceneObserver` 는 기준을 고를 수 있습니다.

- **`DPI`** : 윈도우 디스플레이 배율을 따라가 **화면에서의 물리적 크기**가 일정해집니다. 바탕화면 아이콘·창과 크기감이 맞으므로 바탕화면 게임의 기본값입니다
- **`REFERENCE_RESOLUTION`** : 실제 세로 해상도를 기준 세로로 나눈 값을 써 **화면에서 차지하는 비율**이 일정해집니다. 화면을 독점하는 전체화면 게임에 어울립니다

　
　
　
## 📌 주요 API 요약
　
| 분류 | 멤버 |
|---|---|
| 초기화 | `Initialize()` / `IsInitialized` / `ActiveProfiles` / `EnabledFeatures` / `IsFeatureEnabled()` / `Shutdown()` |
| 해상도 | `GetSupportedResolutions()` / `ApplyResolution()` / `GetAppliedResolution()` / `GetMonitorResolution()` / `TryFindNearestResolution()` |
| 표시 방식 | `ApplyDisplayMode()` / `CurrentDisplayMode` / `IsDisplayModeSupported()` |
| 모니터 | `GetMonitors()` / `GetLeftToRightOrder()` / `CurrentMonitorIndex` / `PrimaryMonitorIndex` / `MoveWindowToMonitor()` / `MonitorLostPolicy` / `GetMonitorLayout()` / `RefreshMonitors()` |
| 작업 영역 | `GetWorkAreaInsets()` / `GetScaledWorkAreaInsets()` / `ClampToWorkArea()` |
| 창 상태 | `SetTopMost()` / `IsTopMostRequested` / `IsTopMostApplied()` / `SetResizable()` / `IsResizableRequested` / `SetBorderless()` / `IsBorderless` / `SetTaskbarButtonVisible()` / `SetCursorConfined()` / `GetWindowRect()` |
| 프레임 | `SetTargetFrameRate()` / `TargetFrameRate` / `SetPowerSaving()` / `SetVSync()` / `IsVSyncEnabled` / `HasFocus` / `UNLIMITED_FRAME_RATE` |
| 설정 | `SaveSettings()` / `HasSavedSettings` / `DeleteSettings()` / `SettingsFilePath` / `ExportSettings()` / `ImportSettings()` |
| 바탕화면 | `IsTransparent` / `IsClickThrough` / `IsPassingThroughNow` / `BackgroundAlpha` / `TransparentReport` |
| 트레이 | `EnableTray()` / `DisableTray()` / `SetTrayIcon()` / `SetTrayTooltip()` / `AddTrayMenuItem()` / `AddTrayMenuSeparator()` / `ClearTrayMenu()` / `SetTrayMenuTheme()` / `FocusGameWindow()` / `IsTraySupported` |
| DPI | `CurrentDpiScale` / `ApplyDpiScaleToAllCanvases()` |
| 후킹 | `AddInitializeListener()` / `AddDisplayListener()` / 및 각 `Remove...()` |
| 플랫폼 | `IsSupported` / `IsWindowControlEnabled` |

　
　
　
## 📌 샘플
　
데모는 `Samples~` 에 들어 있어 **설치만으로는 프로젝트에 들어오지 않습니다.** 필요할 때만 가져오십시오.

1. **Package Manager** 열기
2. 목록에서 **Unity Windows API** 선택
3. **Samples > Demos** 의 **Import into Project** 클릭

가져오면 `Assets/Samples/` 아래에 복사됩니다.

| 씬 | 내용 |
|---|---|
| `PcGame/PcGameDemo.unity` | 해상도 · 표시 방식 · 최상위 · 창 크기 조절 · 마우스 · 프레임 · 설정 저장 |
| `DesktopGame/DesktopGameDemo.unity` | 투명 · 클릭 통과 · 모니터 이동 · 배율 |
| `Tray/TrayDemo.unity` | 트레이 아이콘 · 메뉴 · 테마 |

세 데모 모두 IMGUI 로 화면에 로그를 그립니다. **빌드에는 콘솔이 없어** 라이브러리 경고까지 화면 로그로 끌어옵니다.

> **에디터에서는 초기화가 무시되므로 데모도 아무것도 보여주지 않습니다.** 반드시 빌드해서 확인하십시오.

　
　
　
## 🎉
This package is licensed under The MIT License (MIT)

Copyright © 2026 joylike2 (https://github.com/joylike2)

[https://github.com/joylike2/Unity-Windows-API](https://github.com/joylike2/Unity-Windows-API)
　

See full copyrights in LICENSE inside repository
