# SemiSim

### 반도체 공정에서 트랜지스터 동작까지 연결하는 교육용 3D 시뮬레이터

SemiSim은 공정 조건을 입력하면 웨이퍼 단면의 변화와 품질 판정, 실행이 차단된 이유를 함께 보여 주는 Windows 데스크톱 앱입니다. 산화·포토·식각·증착·주입·열처리·배선 과정을 학습하고, 제작한 구조의 Gate·Source·Drain을 분석해 전류와 트랜지스터 동작을 확인합니다.

현재 개발 대상은 **`SemiEduQt/`**입니다. 2026-11-16 개정 명세의 **Qt QML MVVM · C++ OpenGL · C# NativeAOT** 구조를 사용합니다. 이전 WPF 구현과 네이티브 시각화 프로토타입도 저장소에 남아 있습니다.

> 모든 수치와 판정은 검증되지 않은 교육용 예시 모델입니다. 실제 공정 조건, 장비 운전 한계, 소자 특성이나 수율을 예측하는 도구가 아닙니다.

## 아키텍처

```text
사용자: 공정 선택 → 조건 입력 → 미리보기 / 실행
                         │
                         ▼
┌───────────────────────────────────────────────────────────┐
│ View · QML                                                │
│ 공정 목록 / 입력 폼 / 결과 / 객체 분류 / 그래프 / 이력      │
└───────────────────────────────────────────────────────────┘
        │ 명령 호출                    ▲ 속성 바인딩·NOTIFY
        ▼                              │
┌───────────────────────────────────────────────────────────┐
│ ViewModel · C++ QObject                                   │
│ AppViewModel           실행·장비 상태·이력·Undo            │
│ ProcessEditorViewModel 입력 캐시·동적 폼·사전 점검         │
│ Result / Scene / Plot  판정·씬 데이터·곡선 표시 상태       │
└───────────────────────────────────────────────────────────┘
        │ AppViewModel의 QtConcurrent 워커
        ▼
┌───────────────────────────────────────────────────────────┐
│ EngineClient · C++                                        │
│ QLibrary DLL 로드 / ABI 버전 검사 / JSON 요청·응답         │
└───────────────────────────────────────────────────────────┘
        │ C ABI: se_form / se_run / se_state / se_scene_*
        ▼
┌───────────────────────────────────────────────────────────┐
│ Model · C# NativeAOT DLL                                  │
│ Validator → Pipeline → 공정 계산 → 품질 평가 → Session    │
│ WaferState / DopingMap / MaskLibrary / 이력                │
│ Netlist(Union-Find) / MOSFET 모델 / SceneBuilder           │
└───────────────────────────────────────────────────────────┘
        │ 결과 JSON + 씬 박스 배열
        └─────────────→ ViewModel 갱신
                              │
                              ▼
                  C++ WaferView / PlotItem
                  OpenGL 3D 단면 / QPainter 그래프
```

QML은 사용자 조작과 표시를 담당하고, 검증·공정 수식·품질 판정은 C# 엔진에 둡니다. C++ ViewModel은 엔진 결과를 화면 속성과 목록 모델로 변환합니다. 렌더러는 엔진이 생성한 박스 데이터를 받아 그리므로 공정 계산과 그리기 코드를 분리할 수 있습니다.

| 영역 | 사용 기술 | 역할 |
| --- | --- | --- |
| 화면 | Qt Quick/QML, Quick Controls 2 | 입력 폼, 공정 탐색, 결과와 이력 |
| 화면 상태·실행 | C++17, QObject, QAbstractListModel, QtConcurrent | MVVM 바인딩, 비동기 실행, 취소 요청 |
| 3D·그래프 | OpenGL 3.3 Core, QQuickFramebufferObject, QQuickPaintedItem | 단면 박스, 카메라, 도핑 표시, 전류 곡선 |
| 계산 엔진 | C# .NET 8, NativeAOT | 검증, 공정 모델, 상태 관리, 전기 판정 |
| 언어 간 연결 | C ABI, UTF-8 JSON | 엔진 로드, 명령 호출, 결과 전달 |
| 빌드·검증 | CMake, Ninja, xUnit, DLL 자가 테스트 | 앱 빌드와 엔진 검증 |

## 디렉터리 구조

아래 트리는 주요 소스 중심이며 빌드 산출물은 생략했습니다.

```text
SemiSim/
├─ SemiEduQt/                       현재 Qt 기반 앱
│  ├─ CMakeLists.txt                C++·QML 빌드와 엔진 DLL 복사
│  ├─ build.ps1                     엔진 → 테스트 → 앱 → 배포 → 자가 테스트
│  ├─ app/
│  │  ├─ main.cpp                   OpenGL 설정, ViewModel 생성, 앱 시작
│  │  ├─ engine/                    EngineClient: DLL 로드·C ABI 호출
│  │  ├─ viewmodels/                App / ProcessEditor / Result·Scene·Plot
│  │  │                            ListModels: 화면 목록·입력 필드 모델
│  │  ├─ render/                    WaferView.cpp, PlotItem.cpp
│  │  └─ qml/                       Main, ProcessList, ParamEditor, ParamField
│  │                               LayerManager, ResultPanel, ObjectTable
│  │                               HistoryPanel, ViewHeader, 공통 UI·테마
│  ├─ engine/
│  │  ├─ semiedu_engine.h           공개 C ABI 계약
│  │  └─ SemiEdu.Engine/
│  │     ├─ Abi/                    Exports.cs, Api.cs: 경계·직렬화 처리
│  │     ├─ Domain/                 Wafer.cs, Doping.cs: 단면·도핑 상태
│  │     ├─ Engine/                 Core, Validator, Pipeline, Session
│  │     ├─ Processes/              FrontEnd, Films, Doping 공정 구현
│  │     ├─ Electrical/             Netlist.cs, Transistor.cs
│  │     ├─ Scene/                  SceneBuilder, StructureInspector
│  │     └─ SelfTest.cs             배포 DLL에서도 실행하는 시나리오
│  ├─ engine.tests/                 xUnit 시나리오·직접 테스트
│  ├─ scripts/                      학습 경로 1–5 JSON 재생 스크립트
│  └─ README.md                     상세 빌드 환경·검증 기록·구현 결정
├─ src/SemiconductorEdu.*/          이전 C# WPF 교육용 구현
├─ src/ · include/ · ui/            이전 네이티브 시각화 관련 구현
└─ docs/                           기존 명세 검토 기록
```

현재 앱은 `app`과 `engine` 경계를 C ABI로 나눕니다. 엔진 내부의 Domain·Engine·Processes·Electrical·Scene은 하나의 C# 프로젝트 안에서 폴더로 구분되어 있습니다. 기존 WPF 버전의 여러 프로젝트 구조와는 다릅니다.

## 핵심 로직

### 1. 공정 실행과 상태 반영

```text
원문 입력
   │
   ▼
입력 검증 → 웨이퍼 선행조건 → 장비 인터록 → 모델 적용 범위
                                                    │
                                                    ▼
상태 반영 ← 품질 평가 ← 결과 불변조건 검사 ← 후보 상태 계산
```

[Pipeline.cs](SemiEduQt/engine/SemiEdu.Engine/Engine/Pipeline.cs)는 공정을 같은 단계로 실행합니다. 차단된 단계 이후는 `NotRun`으로 남겨 사용자가 실패 원인을 구분할 수 있게 합니다.

- **실행 차단:** 잘못된 입력, 선행조건 미충족, 장비 한계 초과, 모델 범위 초과를 구분합니다.
- **품질 불량:** 계산이 정상적으로 끝났지만 목표를 만족하지 못한 상태도 반영합니다. 이후 진행은 ‘불량 상태로 계속 학습’ 설정으로 제어합니다.
- **미리보기:** 결과와 씬을 계산하되 현재 웨이퍼에 커밋하지 않습니다.
- **상태 충돌:** 기대 Revision과 현재 Revision이 다르면 `STALE_STATE`로 반영을 거부합니다.
- **Undo:** 부모 상태로 복원하며 실행 이력은 보존합니다.

### 2. 엔진이 결정하는 동적 입력 폼

[Validator.cs](SemiEduQt/engine/SemiEdu.Engine/Engine/Validator.cs)와 공정별 파라미터 정의가 표시할 필드, 선택지, 범위, 오류를 결정합니다. 숫자도 원문 문자열로 전달하므로 잘못된 입력을 UI가 임의로 잘라내지 않습니다.

입력 변경은 C++ ViewModel에서 150 ms 디바운스한 뒤 `se_form`으로 평가합니다. 실행 시에도 같은 검증기를 거치며, 선택한 공정 방식에서 사용하지 않는 입력은 활성 레시피에서 제외합니다. 공정 조건과 품질 목표를 구분해 목표값 변경이 물리 계산 결과를 바꾸지 않도록 구성했습니다.

### 3. 계산과 화면 갱신 분리

[AppViewModel.cpp](SemiEduQt/app/viewmodels/AppViewModel.cpp)는 `QtConcurrent::run`에서 엔진을 호출하고 `QFutureWatcher` 완료 시 결과·씬·이력을 갱신합니다. 실행 중 중복 조작을 제한하고 취소 요청을 엔진에 전달합니다.

엔진이 반환한 문자열은 `se_free`로 해제합니다. 세션 호출은 내부 잠금으로 직렬화하고 취소는 별도 경로로 처리합니다. 씬 박스 배열은 ViewModel에 복사한 뒤 렌더러의 `synchronize()`에서 전달합니다.

### 4. 웨이퍼 상태에서 3D 단면 생성

[SceneBuilder.cs](SemiEduQt/engine/SemiEdu.Engine/Scene/SceneBuilder.cs)는 열별 적층 상태를 연속 구간으로 묶어 박스와 범례를 만듭니다. [WaferView.cpp](SemiEduQt/app/render/WaferView.cpp)는 박스 면·외곽선·반투명 영역을 OpenGL로 표시합니다.

표시는 단면 데이터를 깊이 방향으로 펼친 교육용 3D 표현입니다. 수직 과장과 기판 깊이 생략을 안내하며, 실제 3차원 공정 형상을 계산하는 모델은 아닙니다.

### 5. 제작한 구조의 전기적 동작 판정

```text
단면·활성 도핑 농도
   → 재료·전기적 분류
   → Union-Find 연결 영역 분석
   → Gate / Source / Drain 및 채널 구조 확인
   → 장채널 MOSFET 전류 계산
   → Ion / Ioff / on-off 비 / 게이트 전류 / 문턱 전압 판정
```

[Netlist.cs](SemiEduQt/engine/SemiEdu.Engine/Electrical/Netlist.cs)는 금속과 도핑 영역의 연결 관계를 분석합니다. [Transistor.cs](SemiEduQt/engine/SemiEdu.Engine/Electrical/Transistor.cs)는 단자 누락, 단락, 개방, 접합 부재 등을 확인하고 구조 실패에 따른 교육용 전기 모델도 적용합니다. 단일 전압점과 Id–Vg·Id–Vd 스위프를 지원합니다.

## 학습 경로

| 경로 | 흐름 | 확인할 결과 |
| --- | --- | --- |
| 1. 패턴 형성 | 초기화 → 산화 → 마스크·포토 → 식각 → PRStrip → 검사 | 개구부 식각과 PR 제거 |
| 2. 도핑 | 초기화 → 이온주입 → RTA → 검사 | 농도 프로파일과 활성화 |
| 3. 배선 | UniformSiO2 → 세정 → Al 배선 → 검사 | 배선 저항 |
| 4. 평탄화 | UniformSiO2 → CVD → CMP → 검사 | 잔여 막 두께 |
| 5. NMOS | 마스크 제작 → 초기화 → 게이트 산화·전극 → 주입 → RTA → 컨택 → 전기 검사 | 구조 판정과 on/off 동작 |

실행 예제는 [scripts](SemiEduQt/scripts)에 있습니다. UI는 8대 공정 그룹으로 구성하며, 별도 모델이 없는 **⑧ 패키징은 비활성화**합니다.

## 빌드와 실행

Windows x64 환경에서 실행합니다. 저장소는 .NET SDK **10.0.403**을 사용하며 엔진 타깃은 `net8.0`입니다. Qt Quick·OpenGL·Concurrent 모듈, CMake/Ninja, MinGW C++ 도구와 NativeAOT용 MSVC 빌드 도구가 필요합니다.

기본 스크립트는 Qt `C:/Qt/6.11.1/mingw_64`, 도구 `C:/Qt/Tools`를 기준으로 합니다. 설치 환경과 수동 빌드 절차는 [상세 README](SemiEduQt/README.md)를 확인하세요.

저장소 루트에서:

```powershell
cd SemiEduQt
./build.ps1
./build/SemiEduApp.exe
```

학습 경로 재생과 개별 검증:

```powershell
./build/SemiEduApp.exe --script scripts/path5-nmos.json
dotnet test engine.tests
./build/SemiEduApp.exe --selftest
```

`build.ps1`은 엔진 NativeAOT 게시, xUnit, Qt 앱 빌드, Qt 런타임 배포, 배포 DLL 자가 테스트를 순서대로 수행합니다. 자가 테스트 결과 파일은 `SemiEduQt/build/selftest-result.json`입니다.

## 검증 기록과 확인 범위

아래는 **기존 상세 README에 기록된 2026-11-16 실행 결과**입니다. 이번 README 개편에서 빌드·테스트를 다시 실행한 결과는 아닙니다.

| 검증 | 기록 |
| --- | --- |
| xUnit | 50개 통과, 실패 0 |
| 배포 NativeAOT DLL 자가 테스트 | 42/42 통과 |
| 학습 경로 1–5 | 스크립트 재생에서 모든 단계 ProcessSuccess, 화면 스크린샷 확인 기록 |
| 기본 산화 | 약 70.415946 nm |
| 기본 Al 배선 / CMP | 5.6 Ω / 잔여 28 nm |
| 경로 5 NMOS | Vth 약 0.275845 V, Ion 약 6.28346e-4 A |

xUnit 시나리오 테스트는 DLL 자가 테스트와 같은 `SelfTest` 시나리오를 재사용합니다. 따라서 두 테스트 수를 서로 독립적인 테스트 개수로 합산하지 않습니다.

직접 마우스로 수행하는 전체 UI 경로, UI 취소 응답 시간, 최대 규모 성능 목표는 기존 기록에서도 측정하지 않은 항목입니다.

## 명세와의 차이 및 남은 개선점

| 항목 | 현재 구현·범위 |
| --- | --- |
| 저장·복원·내보내기 | 새 명세에서 후속 작업으로 분리. 현재 Qt 앱은 메모리 이력과 Undo 중심 |
| 설정 파일화 | 계수·범위·품질 기준은 C# 코드에 정의. JSON 로더와 설정 스냅샷은 후속 범위 |
| 전후 비교 | 카메라와 미리보기 제공. 전후 분할 뷰는 후속 범위 |
| JSON 직렬화 | 명세의 JsonSerializerContext 대신 Utf8JsonWriter·JsonDocument 사용 |
| 공정 입력 타입 | 공정별 typed record 요구와 달리 현재 Recipe는 문자열 키 기반 사전 사용 |
| T04 시험 조건 | B 50 keV의 차폐 두께 충돌을 피하기 위해 구현은 B 15 keV 사용 |
| 이상 접촉 | 같은 도핑형 Si를 따라 표면 아래 N+/P+에 연결하는 교육용 가정 추가 |
| 전류 품질 비교 | 공통 epsilon의 절대 하한 1e-9가 작은 전류 기준에 비해 큼. 지표별 허용오차 개선 필요 |

설계상 핵심은 계산 엔진에 검증과 판정을 모으고, 실행 상태와 품질 결과를 구분하며, 같은 엔진을 GUI와 테스트에서 사용하는 점입니다. 후속 개선에서는 공정별 입력 타입, 전류 판정 허용오차, 명세와 구현의 접촉 규칙 일치, 실제 UI 상호작용 검증을 우선합니다.
