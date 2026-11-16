# SemiEduQt build: C# NativeAOT engine -> xUnit -> Qt/QML app -> windeployqt -> se_selftest
param(
    [string]$QtDir = "C:/Qt/6.11.1/mingw_64",
    [string]$QtTools = "C:/Qt/Tools",
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# NativeAOT links with MSVC and locates it through vswhere.exe, which is not on PATH by default.
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$QtDir/bin;$QtTools/mingw1310_64/bin;$QtTools/Ninja;$env:PATH"

Write-Host "== 1) C# engine (NativeAOT DLL)"
dotnet publish engine/SemiEdu.Engine -c Release -r win-x64 -p:PublishAot=true -o out/engine
if ($LASTEXITCODE -ne 0) { throw "engine publish failed" }

if (-not $SkipTests) {
    Write-Host "== 2) xUnit"
    dotnet test engine.tests
    if ($LASTEXITCODE -ne 0) { throw "engine tests failed" }
}

Write-Host "== 3) C++/QML app"
cmake -S . -B build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_PREFIX_PATH=$QtDir `
      -DCMAKE_CXX_COMPILER="$QtTools/mingw1310_64/bin/g++.exe" -DSEMIEDU_ENGINE_DIR="$PSScriptRoot/out/engine"
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }
cmake --build build
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }
windeployqt --qmldir app/qml --no-translations build/SemiEduApp.exe
if ($LASTEXITCODE -ne 0) { throw "windeployqt failed" }

Write-Host "== 4) se_selftest on the deployed DLL"
$p = Start-Process build/SemiEduApp.exe -ArgumentList "--selftest" -PassThru -Wait -NoNewWindow
if ($p.ExitCode -ne 0) { throw "se_selftest failed (see build/selftest-result.json)" }
Write-Host "Done. Run: build/SemiEduApp.exe"
