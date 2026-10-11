# SemiSim

SemiSim은 반도체 제조 공정을 단계별로 시각화하는 Qt/C++ 기반 시뮬레이션 프로그램입니다.

현재는 NMOS 트랜지스터 제작 흐름을 단순화해서 보여주는 초기 프로토타입입니다. 마스크 패턴, 이온 주입, 산화, 식각 화학약품, 콘택트 충전, 금속 배선 같은 공정 조건을 선택하고 결과를 시각적으로 확인할 수 있습니다.

## 주요 기능

- 반도체 공정 흐름 단계별 진행
- 웨이퍼와 공정 구조의 3D 스타일 시각화
- 공정별 파라미터 선택
  - 기판 방향과 두께
  - 웰 이온 주입 도펀트와 도즈
  - 포토 마스크 패턴과 현상액
  - 게이트 산화 조건과 폴리실리콘 증착
  - 소스/드레인 이온 주입 도즈
  - 콘택트 식각 화학약품과 충전 재료
  - 금속 배선 재료
- 공정 로그와 웨이퍼 레이어 정보 표시

## 실행 환경

- CMake 3.24 이상
- C++20 지원 컴파일러
- Qt 6 Widgets

포함된 CMake preset은 Windows 환경의 Qt 6.11.1, MinGW, Ninja 기준으로 설정되어 있습니다.

## 빌드 방법

포함된 CMake preset을 사용하는 경우:

```bash
cmake --preset qt-mingw-debug
cmake --build --preset qt-mingw-debug
```

Qt 경로를 직접 지정해서 빌드하는 경우:

```bash
cmake -S . -B build -DCMAKE_PREFIX_PATH=/path/to/Qt/lib/cmake
cmake --build build
```

## 실행 방법

빌드가 끝난 뒤 생성된 실행 파일을 실행합니다.

```bash
./build-qtcreator-debug/SemiSim.exe
```

## 프로젝트 구조

```text
include/     헤더 파일
src/         소스 파일
data/        공정 데이터
assets/      시각 자료용 폴더
main.cpp     애플리케이션 진입점
```

## 현재 상태

SemiSim은 학습, 데모, UI 실험을 위한 초기 버전입니다. 실제 반도체 공정을 물리적으로 정확하게 계산하는 목적보다는 공정 흐름과 구조 변화를 직관적으로 보여주는 데 초점을 두고 있습니다.

## 라이선스

아직 별도 라이선스를 지정하지 않았습니다.
