# SemiSim

### 공정 조건을 바꿔 보며 반도체 공정을 단계별로 체험하는 교육용 시뮬레이터

반도체 공정은 장비와 비용의 제약 때문에 학생이 직접 조건을 바꿔 보기 어렵습니다. SemiSim은 산화, 포토, 식각, 이온주입, CMP 등 공정을 순서대로 실행하고, 그 결과를 웨이퍼 단면으로 바로 확인할 수 있게 만든 교육용 시뮬레이터입니다.

C# WPF와 MVVM Toolkit으로 개발했습니다. 화면과 공정 로직을 분리하기 위해 도메인, 애플리케이션, 시뮬레이션, 인프라, UI 계층으로 프로젝트를 나누고, 의존성 주입으로 각 계층을 연결했습니다.

---

## 한눈에 보기

| | |
| --- | --- |
| **무엇을** | 웨이퍼 초기화 · 산화 · 포토 · 식각 · PR 제거 · 증착 · CMP · 이온주입 · 어닐 · 금속 배선 · 검사 |
| **어떻게** | 조건 입력 → 입력 검증 → 선행조건 → 장비 인터록 → 모델 적용 범위 → 계산 → 품질 판정 → 커밋 |
| **결과** | 웨이퍼 2D 단면, 공정별 지표(산화막 두께 등), 품질 판정, 실행 이력 |
| **기술** | C# · .NET 10 · WPF · CommunityToolkit.Mvvm · Microsoft.Extensions.DependencyInjection · System.Text.Json · xUnit |
| **검증** | 단위 테스트 49개 + 통합 테스트 14개, **63개 모두 통과** (2026-11-17 실행) |

---

## 계층 구조

```text
┌──────────────────────────────────────────────────────────┐
│ SemiconductorEdu.Wpf          View(XAML) · ViewModel      │
│   MainWindow ↔ MainViewModel   [ObservableProperty]       │
│   CrossSectionCanvas            [RelayCommand]            │
└──────────────────────────────────────────────────────────┘
        │ 명령 실행 (async)               ▲ 데이터 바인딩
        ▼                                 │
┌──────────────────────────────────────────────────────────┐
│ SemiconductorEdu.Application   ProcessSession             │
│   공정별 Executor · 품질 판정 · Undo / Redo · 실행 이력    │
└──────────────────────────────────────────────────────────┘
        │                                 │
        ▼                                 ▼
┌───────────────────────────┐   ┌──────────────────────────┐
│ SemiconductorEdu.Simulation│   │ SemiconductorEdu.        │
│   산화 · 패턴 · 증착/CMP   │   │ Infrastructure           │
│   주입/어닐 · 배선 · 검사  │   │   설정 JSON 로드 · 검증  │
│   웨이퍼 초기화            │   │   세션 저장 / 불러오기   │
└───────────────────────────┘   └──────────────────────────┘
        │                                 │
        └────────────────┬────────────────┘
                         ▼
┌──────────────────────────────────────────────────────────┐
│ SemiconductorEdu.Domain   웨이퍼 상태 · 공정 파라미터     │
│   단위 타입(길이 · 시간 · 압력 · 온도) · 장비 상태 · 검증 │
└──────────────────────────────────────────────────────────┘
```

| 프로젝트 | 역할 |
| --- | --- |
| `Domain` | 웨이퍼 상태, 공정 파라미터, 단위 타입, 장비 상태, 검증 결과처럼 다른 계층이 공유하는 모델 |
| `Simulation` | 공정별 계산 모델. UI와 저장소를 모름 |
| `Application` | 공정 실행 흐름(`ProcessSession`), 공정별 Executor, 품질 판정 |
| `Infrastructure` | 설정 JSON 로드와 검증, 세션 파일 저장과 복원 |
| `Wpf` | 화면과 ViewModel. 계산 코드를 두지 않음 |

[App.xaml.cs](src/SemiconductorEdu.Wpf/App.xaml.cs)에서 모델, Executor, `ProcessSession`, `MainViewModel`을 DI 컨테이너에 등록하고, `MainViewModel`을 `MainWindow`의 `DataContext`로 연결합니다.

---

## MVVM 구성

- **속성 바인딩:** `MainViewModel`은 `ObservableObject`를 상속하고, 공정 조건과 결과를 `[ObservableProperty]`로 공개합니다. 입력값이 바뀌면 화면이 자동으로 갱신됩니다.
- **명령:** 각 공정 실행, 초기화, Undo / Redo, 취소, 세션 저장 / 불러오기, 이력 내보내기를 `[RelayCommand]`로 노출합니다. 공정 실행 명령은 비동기(`Task`)로 동작합니다.
- **단면 표시:** [CrossSectionCanvas](src/SemiconductorEdu.Wpf/Controls/CrossSectionCanvas.cs)는 `DependencyProperty`로 단면 열 목록을 바인딩받아, 값이 바뀌면 다시 그리는 사용자 정의 컨트롤입니다.
- **결과 상태 표시:** 실행 후 입력을 바꾸면 화면에 "입력 변경됨 · 이전 레시피 결과"를 표시해, 지금 보이는 결과가 어떤 조건에서 나온 것인지 헷갈리지 않게 합니다.

---

## 공정 실행 흐름

모든 공정은 [ProcessSession](src/SemiconductorEdu.Application/Execution/ProcessSession.cs)을 거쳐 같은 단계로 실행됩니다.

```text
설정 → 입력 검증 → 선행조건 → 장비 인터록 → 모델 적용 범위
                                                    │
                                                    ▼
          커밋 ◀── 품질 판정 ◀── 결과 불변조건 검사 ◀── 계산
```

- **실패 원인 분리:** 결과를 `InvalidInput` / `PreconditionFailed` / `Interlock` / `ModelOutOfDomain` / `CalculationError` / `Cancelled` / `ProcessFailure` / `ProcessSuccess`로 나눕니다. 학습자는 어느 단계에서 막혔는지 바로 확인할 수 있습니다.
- **장비 인터록:** 장비 상태(도어 닫힘, 비상정지, 사용 가능 여부, 허용 웨이퍼 직경)를 확인한 뒤에만 계산합니다.
- **품질 판정:** 공정 지표를 목표값과 허용 오차(비율 또는 절대값)로 비교해 Pass / Fail을 정합니다.
- **상태 충돌 방지:** 실행 중에 세션이 바뀌면 `STALE_SESSION_REVISION`으로 커밋을 거부합니다.
- **비동기 실행과 취소:** 계산은 `Task.Run`으로 백그라운드에서 돌고, 취소 요청은 `CancellationToken`으로 전달합니다.

---

## 교육용 기능

| 기능 | 설명 |
| --- | --- |
| 단계별 공정 실행 | 웨이퍼 초기화부터 검사까지 10개 공정을 원하는 순서로 실행 |
| Undo / Redo | 이전 웨이퍼 상태로 돌아가 다른 조건으로 다시 진행 |
| 실행 이력 | 공정 종류, 실행 상태, 품질 판정, 지표를 History에 기록 |
| 이력 내보내기 | 실행 이력을 CSV로 저장해 수업 자료나 비교 분석에 활용 |
| 세션 저장 / 불러오기 | 진행 중인 웨이퍼 상태와 이력을 JSON 파일로 저장하고 복원. 저장은 임시 파일에 쓴 뒤 교체해 파일 손상을 막음 |

`Infrastructure`에는 공정 설정 JSON을 읽고 검증하는 로더(`SimulationConfigurationLoader`)도 있습니다. 통합 테스트로 검증했지만, 현재 앱은 아직 이 로더 대신 `App.xaml.cs`에 등록한 기본값을 사용합니다.

---

## 검증

```powershell
dotnet test SemiconductorEdu.sln
```

| 대상 | 내용 | 결과 (2026-11-17) |
| --- | --- | --- |
| 단위 테스트 | 산화 · 패턴 · 증착/CMP · 주입/어닐 · 배선 · 검사 모델, 초기화 검증, 세션 동작 | 49개 통과 |
| 통합 테스트 | 설정 로드와 세션 저장, 세션 복구, 주입/어닐 · 배선/검사 세션 흐름 | 14개 통과 |

---

## 빌드와 실행 (Windows)

**필요한 것:** .NET SDK 10.0.403 ([global.json](global.json)으로 고정)

```powershell
dotnet build SemiconductorEdu.sln
dotnet run --project src/SemiconductorEdu.Wpf
```

---

## 다른 구현

같은 저장소에 SemiSim을 다른 구조로 구현한 버전이 두 개 더 있습니다.

### WPF + C++ 네이티브 DLL 프로토타입 (`ui/SemiSim.Wpf`, 루트 `src/`의 C++ 코드)

C# WPF 화면에서 C++ 네이티브 DLL(`SemiSimNative.dll`)을 P/Invoke로 호출하는 구조를 검증한 초기 프로토타입입니다.

- C++ 쪽은 `extern "C"` 함수 8개(생성 · 초기화 · 공정 적용 · 상태 조회 · 카메라 회전/확대 · 렌더링)를 내보냅니다.
- 공정 파라미터는 UTF-8 JSON 문자열로 전달합니다.
- C++에서 웨이퍼를 3D 투영으로 그려 픽셀 버퍼로 넘기면, WPF가 이미지로 표시합니다.
- 연동 검증이 목적이라 WPF 쪽은 MVVM이 아닌 코드 비하인드 방식입니다.

### Qt 6 / C++ 3D 버전 (`SemiEduQt/`)

![Qt 버전에서 NMOS 제작 경로를 실행한 화면](images/NMOS_image.png)

C++17과 Qt 6(QML)로 다시 만든 3D 버전입니다. 공정 계산은 C# 엔진을 NativeAOT DLL로 빌드해 C ABI로 연결했습니다.

- QML View와 C++ `QObject` ViewModel을 속성 바인딩으로 연결한 MVVM 구조
- OpenGL 3.3 인스턴싱 렌더러로 웨이퍼 단면을 3D로 표시
- 공정 결과로 만든 구조를 Union-Find로 연결 분석해 트랜지스터 단락 · 개방 · Vth 등을 판정
- 현재 명세 원문 규칙을 우선 적용하면서 일부 테스트가 실패한 상태입니다(xUnit 63개 중 58개, 자가 테스트 42개 중 38개 통과). 자세한 내용은 [SemiEduQt/README.md](SemiEduQt/README.md)에 있습니다.

---

## 디렉터리

```text
SemiSim/
├─ src/SemiconductorEdu.Domain/          도메인 모델
├─ src/SemiconductorEdu.Application/     공정 실행 흐름 · Executor · 품질 판정
├─ src/SemiconductorEdu.Simulation/      공정 계산 모델
├─ src/SemiconductorEdu.Infrastructure/  설정 · 세션 저장
├─ src/SemiconductorEdu.Wpf/             WPF · MVVM 화면
├─ tests/                                단위 · 통합 테스트
├─ ui/SemiSim.Wpf/ + src/ · include/     WPF + C++ 네이티브 프로토타입
├─ data/                                 프로토타입용 공정 기본값 JSON
├─ SemiEduQt/                            Qt 6 / C++ 3D 버전
└─ docs/                                 명세 검토 기록
```

> 모든 계수, 장비 한계, 품질 기준은 교육용 예시 값입니다. 실제 제조 레시피, 장비 운전 한계, 수율을 예측하는 데 쓸 수 없습니다.
