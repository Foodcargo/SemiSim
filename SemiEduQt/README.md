# SemiEduQt — 반도체 공정 교육용 시뮬레이터 (Qt QML MVVM · C++ OpenGL · C# 엔진)

> **검증되지 않은 교육용 예시 모델입니다.** 입력 범위, 장비 한계, 계수, 품질 기준은 실제 제조 레시피·장비 운전 한계·수율 예측이 아닙니다. 테스트 통과는 소프트웨어 검증이며 실제 공정 검증이 아닙니다.

`반도체공정_시뮬레이터_3D_UI_개발명세서_QML_MVVM.md`(2026-11-16)에 따라 구현했습니다. 공정 로직은 원본 명세 v1.0(2026-11-03)을 따릅니다.

## 구성

| 계층 | 위치 | 내용 |
| --- | --- | --- |
| View | `app/qml/` | QML 13개(Main, ProcessList, LayerManager, ParamEditor, ParamField, ViewHeader, ResultPanel, ObjectTable, HistoryPanel, Theme, DockTitle, SectionHeader, ToolBtn). 바인딩과 ViewModel 호출만 하며, 검증·계산 코드는 없습니다. |
| ViewModel | `app/viewmodels/` | AppViewModel, ProcessEditorViewModel, ParamListModel, ResultViewModel, SceneViewModel, PlotViewModel, JsonListModel |
| 렌더링 | `app/render/` | `WaferView`(Qt FBO·카메라) + `BoxRenderer`(OpenGL 3.3 인스턴싱·프러스텀 컬링·버퍼 재사용), `PlotItem`(QQuickPaintedItem). 조명·외곽선·반투명·MSAA 4 유지 |
| 엔진 연결 | `app/engine/EngineClient.*` | `QLibrary`로 `SemiEdu.Engine.dll`을 불러옵니다. `abiVersion == 1`을 확인하고, 실행은 `QtConcurrent` 워커에서 합니다. |
| Model | `engine/SemiEdu.Engine/` | C# .NET 8 NativeAOT. 검증기·8단계 파이프라인·15개 공정·도체 분류(Union-Find)·MOSFET 모델·SceneBuilder·SelfTest를 담습니다. C ABI는 `engine/semiedu_engine.h`입니다. |
| 테스트 | `engine.tests/` | xUnit. SelfTest 시나리오 42개, 기존 직접 테스트 8개, 입력 타입·ABI·원문 계약 회귀 테스트 7개로 합계 57개입니다. |
| 학습 경로 | `scripts/path1~5*.json` | 원본 4개 경로와 경로 5(NMOS 제작 → 전기 검사)를 담은 재생 스크립트입니다. |

화면은 위쪽 툴바(회전·이동·확대·축소·맞춤 / 미리보기·실행·취소·Undo / 전기적 분류 / 교육용 장비 상태 토글)와 세 영역으로 구성됩니다. 왼쪽은 Design Browser(Process Navigator · Layer Manager · 공정 조건), 가운데는 3D process view, 오른쪽은 Inspector(속성·판정 / 객체 분류 / 그래프 / 이력)입니다.

## 빌드와 실행 (Windows)

```powershell
./build.ps1          # 엔진 publish → xUnit → CMake 빌드 → windeployqt → se_selftest
build/SemiEduApp.exe
build/SemiEduApp.exe --selftest                          # 배포 DLL 자가 테스트 출력 후 종료(결과: build/selftest-result.json)
build/SemiEduApp.exe --script scripts/path5-nmos.json    # 학습 경로를 재생한 상태로 시작
```

수동으로 빌드하는 절차는 명세서 7장과 같습니다. 다만 아래 환경 차이가 있습니다.

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;C:\Qt\6.11.1\mingw_64\bin;C:\Qt\Tools\mingw1310_64\bin;C:\Qt\Tools\Ninja;$env:PATH"
dotnet publish engine/SemiEdu.Engine -c Release -r win-x64 -p:PublishAot=true -o out/engine
dotnet test engine.tests
cmake -S . -B build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_PREFIX_PATH=C:/Qt/6.11.1/mingw_64 -DSEMIEDU_ENGINE_DIR=out/engine
cmake --build build          # SemiEdu.Engine.dll을 exe 옆으로 복사합니다
windeployqt --qmldir app/qml build/SemiEduApp.exe
```

### 확인한 환경

- Windows 11, .NET SDK 10.0.403(`../global.json`으로 고정), 엔진·테스트 대상 `net8.0`(런타임 8.0.21)
- NativeAOT 링커: Visual Studio Build Tools 2019 MSVC 14.29. `vswhere.exe`가 PATH에 있어야 합니다.
- Qt 6.11.1 **MinGW 64-bit**(명세서는 MSVC 키트였지만 설치된 키트를 사용). 앱과 엔진은 C ABI로만 연결되므로 컴파일러가 섞여도 됩니다. 둘 다 x64입니다.
- 패키지: Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.1

## 검증 기록 (2026-11-16, 실제 실행)

- `dotnet test engine.tests`: **50개 통과, 실패 0**
- `SemiEduApp.exe --selftest`(배포된 NativeAOT DLL): **42/42 통과**
  - 대상: M02, M03, E01, E02, Q01–Q04, V01–V04, U01, M01, M04–M10, CMP, S01–S03, A01, K01–K04, C01–C03, T01–T09
- 주요 수치: M02 70.41594579 nm · M03 112.788206 nm · M06 평균 깊이 148.95 nm, dose 오차 3.9e-16 · M07 활성 95.52 %, 보존 오차 2.4e-14 · M05 5.6 Ω · CMP 28 nm · S01 오염 4.536
- T01(경로 5): tox 9.387155 nm, Vth 0.275845 V, Ion 6.28346e-4 A, Ioff 4.01282e-10 A, on/off 1.5658e6, Eox 1.278 MV/cm
- 학습 경로 1–5를 `--script`로 앱에서 처음부터 끝까지 실행: 모든 단계 `ProcessSuccess`. 3D 화면은 스크린샷으로 확인했습니다.
- **측정하지 않은 것:** 마우스로 직접 누르는 UI 조작, 계산 중 [취소] 버튼의 실제 응답 시간(엔진 단위 테스트 S02에서만 측정, 0–4 ms), 512열 공정 500 ms·8192셀 열처리 2초 성능 목표

## 원본 명세 대비 간소화·편차·확장 (명세 11장)

| 원본 요구 | 이 구현 | 후속 작업 |
| --- | --- | --- |
| WPF·MVVM Toolkit, 5개 프로젝트 | View=QML, ViewModel=C++ QObject, Model=C# NativeAOT. 프로젝트는 app·engine·engine.tests 3개 | 엔진을 Domain·Application·Simulation으로 분리 |
| 계수·범위·품질 기준 JSON | C# 상수(값은 원본 표와 같음) | 원본 9장 JSON 로더·검증기(J03) |
| 레시피·세션 저장, JSON/CSV 내보내기, SHA-256 | 메모리 이력과 Undo만 | 저장·복원(J01·J02) |
| 선택지가 사라지면 재선택 | ‘— 다시 선택 —’ 항목으로 구현 | — |
| 단면 확대·전후 비교 | 3D 궤도 카메라와 미리보기 | 전·후 분할 뷰 |
| ⑧ 패키징 | 비활성(임의 결과 없음) | 별도 명세 필요 |
| JSON 소스 생성기(`JsonSerializerContext`) | `EngineJsonContext`와 응답 전용 converter로 직렬화. 기존 ABI 필드 작성과 JsonDocument 입력 파싱 유지 | — |
| Qt MSVC 키트 | Qt MinGW 키트(C ABI로 연결) | — |

추가 사항(원본 v1 범위 밖, 모두 교육용 가정): 마스크 제작·MaskLibrary(8장), 도체 분류·NetList(9장), 확장 규칙 X1–X5, IdealContact, MOSFET 전기 모델(10장).

### 이전 구현의 결정 사항 (2026-11-17 변경 전)

- **T04**: B 50 keV는 Rp+4σ = 330 nm여서 300 nm W 게이트에서 X1 `MASK_TOO_THIN`에 걸립니다(명세 내부 충돌). 그래서 T04는 **B 15 keV**(Rp+4σ = 99 nm)로 NO_JUNCTION을 재현합니다.
- **오믹 접촉**: 스크린 산화막 때문에 As 피크가 표면 아래(약 40 nm)에 있습니다. 그래서 금속 컨택은 표면에서 같은 형(n)의 Si를 따라 내려가 도달한 N+ 셀과 연결된 것으로 봅니다(교육용 이상 접촉).
- **경로 4**: CMP는 최상부의 연속된 SiO2(초기 100 nm + CVD 100 nm)에서 72 nm를 제거합니다. 그래서 스크립트는 잔여 목표를 128 nm로, 두께 검사 목표(CVD 층 L2-SiO2)를 28 nm로 지정합니다.
- **‘불량 상태로 계속 학습’**: 직전 커밋이 ProcessFailure이면 초기화·마스크 제작 외 공정은 `PREVIOUS_FAILURE`로 막습니다. 이 검사는 엔진에서 합니다.
- **se_form의 장비 상태**: ABI는 명세 그대로 두었습니다. 사전 점검용 장비 상태는 valuesJson의 예약 키 `"$equipment"`로 전달합니다.

## 원문 우선 검증 (2026-11-17)

사용자 지시에 따라 명세 원문을 우선했습니다. 위 2026-11-16의 50/50 및 42/42 통과 기록은 변경 전 결과이며 현재 결과가 아닙니다.

- 14개 활성 공정에 불변 입력 레코드를 적용했습니다. 계산과 공정별 검증은 문자열 키 사전 대신 해당 레코드를 사용합니다. 비활성 숫자 필드는 null로 유지하고 필수값 누락을 0으로 대체하지 않습니다.
- ABI의 입력 문자열 변환을 예외 처리 범위 안으로 옮겼습니다. valuesJson의 파라미터는 원문 문자열만 허용하며 숫자·불리언 JSON 값은 INVALID_JSON으로 거부합니다.
- 명세 9장에 따라 금속은 직접 맞닿은 N+/P+ 표면 셀에만 연결합니다. 반도체층을 건너뛰는 기존 이상 접촉 규칙을 제거했습니다.
- T04는 원문대로 B 50 keV를 사용합니다. NO_JUNCTION 기대값은 수정하거나 건너뛰지 않았습니다.
- 품질 epsilon은 원문 수식 1e-9 × max(1, |actual|, |boundary|)를 유지했습니다. 작은 전류에서 허용오차가 큰 문제는 명세의 한계로 남습니다.

실행: `dotnet test engine.tests --no-restore --logger "trx;LogFileName=spec-alignment.trx" --results-directory TestResults`

결과: **57개 중 52개 통과, 5개 실패, 건너뜀 0**. 상세 원본은 로컬 `TestResults/spec-alignment.trx`에 있습니다.

NativeAOT `dotnet publish`와 Qt `cmake --build build`도 확인했습니다. 최신 DLL을 앱 옆에 복사한 뒤 실행한 `--selftest`는 **42개 중 38개 통과, 4개 실패(T01·T03·T04·T08), 종료 코드 1**입니다. 상세 결과는 로컬 `build/selftest-result.json`에 있습니다. GUI 상호작용은 이번에 검증하지 않았습니다.

PowerShell 실행 정책 때문에 `build.ps1` 직접 실행은 차단되어, 실행 정책을 변경하지 않고 게시·빌드·자가 테스트 명령을 각각 실행했습니다.

| 실패 테스트 | 원문 기대 | 실제 결과 |
| --- | --- | --- |
| T01 | 기본 NMOS 정상, Ion 약 6.2835e-4 A | 컨택 표면이 도체성 기준 미만이므로 OPEN_SOURCE·OPEN_DRAIN, Ion=0 |
| T03 | SD_SHORT, Ioff 실패 | 직접 접촉 규칙에 따라 OPEN_SOURCE·OPEN_DRAIN |
| T04 | B 주입 후 NO_JUNCTION | B 50 keV는 300 nm 게이트의 차폐 한계 초과로 MASK_TOO_THIN; 후속 경로도 차단 |
| T08 | 목표 변경으로 정상→비정상, 전류 동일 | 기본 구조가 이미 비정상이라 ProcessFailure→ProcessFailure, Ion=0 |
| SweepProducesCurve | 외삽 Vth 존재 | 구조 실패로 외삽 Vth 없음 |

원문 테스트의 기대값을 바꾸어 통과시키지 않았습니다. 경로 5의 정상 판정과 전체 인수 기준은 현재 충족하지 않습니다. `build.ps1`은 xUnit 실패 시 중단하는 기존 동작을 유지합니다.

응답 직렬화는 명세의 EngineJsonContext 소스 생성기를 적용했습니다. 명시적 필드 converter로 기존 JSON 계약을 유지하며 리플렉션을 사용하지 않습니다. 나머지 편차: Qt 키트와 화면 배치는 기존 구현을 유지합니다. 따라서 이번 수정이 명세 전체와의 완전 일치를 의미하지 않습니다.
