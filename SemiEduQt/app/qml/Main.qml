import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Dialogs
import SemiEdu

ApplicationWindow {
    id: win
    width: Math.min(1680, Screen.desktopAvailableWidth - 40)
    height: Math.min(980, Screen.desktopAvailableHeight - 60)
    minimumWidth: 1200
    minimumHeight: 720
    // Opened maximized so long forms or result lists never grow the window past the screen.
    visibility: Window.Maximized
    visible: true
    color: Theme.window
    title: "SemiEdu — Semiconductor Process Simulator"

    required property AppViewModel app
    required property ProcessEditorViewModel editor
    required property ResultViewModel result
    required property SceneViewModel scene
    required property PlotViewModel plot

    palette.window: Theme.dock
    palette.windowText: Theme.text
    palette.base: Theme.field
    palette.alternateBase: Theme.section
    palette.text: Theme.text
    palette.button: Theme.button
    palette.buttonText: Theme.text
    palette.brightText: "#ffffff"
    palette.highlight: Theme.accent
    palette.highlightedText: "#ffffff"
    palette.placeholderText: Theme.textFaint
    palette.light: "#3a404a"
    palette.midlight: "#30353d"
    palette.mid: "#272b31"
    palette.dark: "#111316"
    palette.shadow: "#000000"
    palette.toolTipBase: Theme.section
    palette.toolTipText: Theme.text

    // ---------------- Menu bar ----------------
    menuBar: MenuBar {
        Menu {
            title: "파일(&F)"
            Action { text: "새 세션…"; shortcut: "Ctrl+N"; enabled: !win.app.busy; onTriggered: confirmNew.open() }
            Action { text: "학습 경로 열기…"; shortcut: "Ctrl+O"; enabled: !win.app.busy; onTriggered: openScript.open() }
            MenuSeparator {}
            Action { text: "결과 JSON 내보내기…"; shortcut: "Ctrl+S"; enabled: win.app.hasLastRun; onTriggered: saveJson.open() }
            Action { text: "이력 CSV 내보내기…"; shortcut: "Ctrl+Shift+S"; enabled: win.app.historyCount > 0; onTriggered: saveCsv.open() }
            Action { text: "3D 화면 이미지 저장…"; shortcut: "Ctrl+P"; onTriggered: savePng.open() }
            MenuSeparator {}
            Action { text: "종료"; shortcut: "Ctrl+Q"; onTriggered: Qt.quit() }
        }
        Menu {
            title: "편집(&E)"
            Action { text: "Undo Last Run"; shortcut: "Ctrl+Z"; enabled: win.app.canUndo && !win.app.busy; onTriggered: win.app.undo() }
            MenuSeparator {}
            Action { text: "현재 공정 입력을 기본값으로"; enabled: !win.app.busy; onTriggered: win.editor.resetInputs() }
        }
        Menu {
            title: "보기(&V)"
            Action { text: "회전 모드"; checkable: true; checked: view.dragMode === "rotate"; onTriggered: view.dragMode = "rotate" }
            Action { text: "이동 모드"; checkable: true; checked: view.dragMode === "pan"; onTriggered: view.dragMode = "pan" }
            MenuSeparator {}
            Action { text: "확대"; shortcut: "Ctrl+="; onTriggered: view.zoomBy(0.8) }
            Action { text: "축소"; shortcut: "Ctrl+-"; onTriggered: view.zoomBy(1.25) }
            Action { text: "화면 맞춤"; shortcut: "Ctrl+0"; onTriggered: view.resetCamera() }
            MenuSeparator {}
            Action {
                text: "전기적 분류 보기 (도체/반도체/절연체)"
                shortcut: "Ctrl+E"
                checkable: true
                checked: win.scene.classificationView
                onTriggered: win.scene.setClassificationView(!win.scene.classificationView)
            }
            MenuSeparator {}
            Action { text: "Design Browser 표시"; checkable: true; checked: leftDock.visible; onTriggered: leftDock.visible = !leftDock.visible }
            Action { text: "Inspector 표시"; checkable: true; checked: rightDock.visible; onTriggered: rightDock.visible = !rightDock.visible }
            MenuSeparator {}
            Action { text: "Inspector: 속성·판정"; shortcut: "Ctrl+1"; onTriggered: { rightDock.visible = true; tabs.currentIndex = 0 } }
            Action { text: "Inspector: 객체 분류"; shortcut: "Ctrl+2"; onTriggered: { rightDock.visible = true; tabs.currentIndex = 1 } }
            Action { text: "Inspector: 그래프"; shortcut: "Ctrl+3"; onTriggered: { rightDock.visible = true; tabs.currentIndex = 2 } }
            Action { text: "Inspector: 이력"; shortcut: "Ctrl+4"; onTriggered: { rightDock.visible = true; tabs.currentIndex = 3 } }
        }
        Menu {
            title: "실행(&R)"
            Action { text: "계산 미리보기 (상태 미변경)"; shortcut: "F5"; enabled: win.editor.canPreview; onTriggered: win.editor.preview() }
            Action { text: "공정 실행 (커밋)"; shortcut: "Ctrl+Return"; enabled: win.editor.canRun; onTriggered: win.editor.run() }
            Action { text: "계산 취소"; shortcut: "Esc"; enabled: win.app.busy; onTriggered: win.app.cancel() }
        }
        Menu {
            title: "관리(&M)"
            Menu {
                title: "교육용 장비 상태 (실제 장비 아님)"
                Action { text: "문 닫힘 (DoorClosed)"; checkable: true; checked: win.app.doorClosed; onTriggered: win.app.doorClosed = !win.app.doorClosed }
                Action { text: "비상정지 (EmergencyStop)"; checkable: true; checked: win.app.emergencyStop; onTriggered: win.app.emergencyStop = !win.app.emergencyStop }
                Action { text: "사용 가능 (Available)"; checkable: true; checked: win.app.available; onTriggered: win.app.available = !win.app.available }
            }
            Action { text: "불량 상태로 계속 학습"; checkable: true; checked: win.app.continueOnFailure; onTriggered: win.app.continueOnFailure = !win.app.continueOnFailure }
            MenuSeparator {}
            Action { text: "엔진 자가 테스트 실행 (se_selftest)"; enabled: !win.app.busy; onTriggered: win.notify("엔진 자가 테스트", win.app.runSelfTest()) }
            Action { text: "엔진 정보"; onTriggered: win.notify("엔진 정보", win.app.engineInfo()) }
        }
        Menu {
            title: "도움말(&H)"
            Action { text: "사용 방법"; shortcut: "F1"; onTriggered: win.notify("사용 방법", win.helpText) }
            Action { text: "현재 공정에서 반영하지 않는 효과"; onTriggered: win.notify("모델에서 반영하지 않는 효과", win.editor.unsupportedEffects) }
            Action { text: "모델 한계·면책"; onTriggered: win.notify("모델 한계·면책", win.disclaimerText) }
            MenuSeparator {}
            Action { text: "SemiEdu 정보"; onTriggered: win.notify("SemiEdu 정보", "SemiEdu — 반도체 공정 교육용 시뮬레이터\nQt Quick(QML) + C++ ViewModel(MVVM) · OpenGL 3.3 · C# NativeAOT 엔진\n\n" + win.app.engineInfo()) }
        }
    }

    // Engine and equipment status strip.
    header: Rectangle {
        implicitHeight: 28
        color: win.app.engineReady ? Theme.toolbar : "#5a1d1d"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 12
            anchors.rightMargin: 12
            spacing: 14
            Label {
                Layout.fillWidth: true
                text: (win.app.engineReady ? "" : "⛔ ") + win.app.bannerText
                color: win.app.engineReady ? Theme.textDim : "#ffb4b4"
                elide: Text.ElideRight
                font.pixelSize: 12
            }
            Label {
                text: "장비: " + (win.app.doorClosed ? "문 닫힘" : "문 열림") + " · " + (win.app.emergencyStop ? "비상정지" : "비상정지 해제")
                      + " · " + (win.app.available ? "사용 가능" : "사용 불가") + (win.app.continueOnFailure ? " · 불량 상태로 계속 학습" : "")
                color: (!win.app.doorClosed || win.app.emergencyStop || !win.app.available) ? Theme.bad : Theme.textDim
                font.pixelSize: 12
            }
            Label {
                text: view.dragMode === "rotate" ? "드래그: 회전" : "드래그: 이동"
                color: Theme.textDim
                font.pixelSize: 12
            }
        }
    }

    // ---------------- Dialogs ----------------
    readonly property string helpText: "1. 왼쪽 Process Navigator에서 공정을 고릅니다.\n"
        + "2. 공정 조건과 품질 목표를 입력합니다. 범위 밖 값은 필드 아래에 빨간 글씨로 표시되고 실행이 막힙니다.\n"
        + "3. [계산 미리보기](F5)는 상태를 바꾸지 않고, [공정 실행](Ctrl+Enter)만 웨이퍼 상태를 커밋합니다.\n"
        + "4. 오른쪽 Inspector에서 단계별 판정, 지표·목표·합부, 차단·불합격 사유를 확인합니다.\n"
        + "5. 품질 불량 뒤에는 다음 공정이 막힙니다. 관리 ▸ ‘불량 상태로 계속 학습’을 켜거나 Undo(Ctrl+Z)로 되돌리세요.\n"
        + "6. 파일 ▸ 학습 경로 열기로 scripts 폴더의 경로 1–5를 재생할 수 있습니다(경로 5: NMOS 제작 → 전기 검사).\n\n"
        + "3D 조작: 왼쪽 드래그 회전(보기 ▸ 이동 모드로 전환), 오른쪽/가운데 드래그 이동, 휠 확대, 더블클릭 화면 맞춤."
    readonly property string disclaimerText: "이 프로그램은 단순화된 공정 수식과 계수를 사용합니다.\n"
        + "실제 제조 레시피, 장비 사양·안전 운전 한계, 공정 보증값, 수율 예측으로 사용할 수 없습니다.\n"
        + "식의 형태는 문헌(예: Deal–Grove, Preston, square-law)을 참고했지만 계수는 이 제품이 임의로 정한 값입니다.\n"
        + "테스트 통과는 소프트웨어 검증이며 실제 공정 정확도 검증이 아닙니다."

    function notify(title, text) {
        notice.title = title
        noticeText.text = text
        notice.open()
    }

    Dialog {
        id: notice
        anchors.centerIn: parent
        width: Math.min(720, win.width - 80)
        modal: true
        standardButtons: Dialog.Ok
        contentItem: ScrollView {
            implicitHeight: Math.min(noticeText.implicitHeight + 10, win.height * 0.6)
            clip: true
            Label {
                id: noticeText
                width: notice.availableWidth - 10
                wrapMode: Text.Wrap
                color: Theme.text
                font.pixelSize: 13
            }
        }
    }

    Dialog {
        id: confirmNew
        anchors.centerIn: parent
        modal: true
        title: "새 세션"
        standardButtons: Dialog.Ok | Dialog.Cancel
        Label {
            text: "현재 웨이퍼 상태, 실행 이력, 마스크 라이브러리가 모두 사라집니다.\n새 세션을 시작할까요?"
            color: Theme.text
        }
        onAccepted: win.notify("새 세션", win.app.newSession())
    }

    FileDialog {
        id: openScript
        title: "학습 경로 스크립트 열기"
        fileMode: FileDialog.OpenFile
        nameFilters: ["학습 경로 스크립트 (*.json)"]
        currentFolder: win.app.scriptsFolder()
        onAccepted: win.notify("학습 경로 실행", win.app.runScript(selectedFile))
    }
    FileDialog {
        id: saveJson
        title: "마지막 실행 결과 저장"
        fileMode: FileDialog.SaveFile
        defaultSuffix: "json"
        nameFilters: ["JSON (*.json)"]
        onAccepted: win.notify("결과 내보내기", win.app.exportResultJson(selectedFile))
    }
    FileDialog {
        id: saveCsv
        title: "실행 이력 저장"
        fileMode: FileDialog.SaveFile
        defaultSuffix: "csv"
        nameFilters: ["CSV (*.csv)"]
        onAccepted: win.notify("이력 내보내기", win.app.exportHistoryCsv(selectedFile))
    }
    FileDialog {
        id: savePng
        title: "3D 화면 이미지 저장"
        fileMode: FileDialog.SaveFile
        defaultSuffix: "png"
        nameFilters: ["PNG 이미지 (*.png)"]
        onAccepted: {
            const path = win.app.toLocalFile(selectedFile)
            viewArea.grabToImage(function (r) {
                win.notify("이미지 저장", r.saveToFile(path) ? "3D 화면을 저장했습니다: " + path : "저장에 실패했습니다: " + path)
            })
        }
    }

    SplitView {
        anchors.fill: parent
        orientation: Qt.Horizontal
        handle: Rectangle { implicitWidth: 4; color: SplitHandle.hovered ? Theme.accentSoft : Theme.window }

        // ---- Left: Design Browser ----
        ColumnLayout {
            id: leftDock
            SplitView.preferredWidth: 400
            SplitView.minimumWidth: 340
            spacing: 0
            DockTitle { Layout.fillWidth: true; title: "Design Browser" }
            SplitView {
                Layout.fillWidth: true
                Layout.fillHeight: true
                orientation: Qt.Vertical
                handle: Rectangle { implicitHeight: 4; color: SplitHandle.hovered ? Theme.accentSoft : Theme.window }
                ProcessList {
                    SplitView.preferredHeight: 250
                    SplitView.minimumHeight: 140
                    app: win.app
                }
                LayerManager {
                    SplitView.preferredHeight: 140
                    SplitView.minimumHeight: 80
                    app: win.app
                }
                ParamEditor {
                    SplitView.fillHeight: true
                    SplitView.minimumHeight: 200
                    editor: win.editor
                    result: win.result
                    app: win.app
                }
            }
        }

        // ---- Center: 3D process view ----
        Rectangle {
            id: viewArea
            SplitView.fillWidth: true
            SplitView.minimumWidth: 420
            color: "#0d0f13"
            WaferView {
                id: view
                anchors.fill: parent
                scene: win.scene
            }
            ColumnLayout {
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.bottom: parent.bottom
                anchors.margins: 10
                spacing: 6
                Rectangle {
                    Layout.fillWidth: true
                    implicitHeight: selectedLayerLog.implicitHeight + 16
                    visible: win.app.selectedLayerText !== ""
                    color: Theme.dock
                    border.color: Theme.accent
                    radius: 3
                    Label {
                        id: selectedLayerLog
                        anchors.fill: parent
                        anchors.margins: 8
                        text: "Selected Layer  ›  " + win.app.selectedLayerText
                        textFormat: Text.PlainText
                        color: Theme.text
                        wrapMode: Text.Wrap
                        font.pixelSize: 12
                    }
                }
                Label {
                Layout.fillWidth: true
                text: win.scene.scaleText + "\n조작: 왼쪽 드래그 " + (view.dragMode === "rotate" ? "회전" : "이동") + " · 오른쪽/가운데 드래그 이동 · 휠 확대 · 더블클릭 화면 맞춤"
                color: Theme.textFaint
                font.pixelSize: 11
                wrapMode: Text.Wrap
                }
            }
            BusyIndicator {
                anchors.centerIn: parent
                running: win.app.busy
                visible: running
            }
        }

        // ---- Right: Inspector ----
        ColumnLayout {
            id: rightDock
            SplitView.preferredWidth: 420
            SplitView.minimumWidth: 340
            spacing: 0
            DockTitle { Layout.fillWidth: true; title: "Inspector" }
            TabBar {
                id: tabs
                Layout.fillWidth: true
                TabButton { text: "Properties" }
                TabButton { text: "Objects" }
                TabButton { text: "Graphs" }
                TabButton { text: "History" }
            }
            StackLayout {
                Layout.fillWidth: true
                Layout.fillHeight: true
                currentIndex: tabs.currentIndex
                ResultPanel {
                    result: win.result
                    editor: win.editor
                }
                ObjectTable {
                    result: win.result
                }
                Rectangle {
                    color: Theme.dock
                    PlotItem {
                        anchors.fill: parent
                        anchors.margins: 6
                        plot: win.plot
                    }
                    Label {
                        anchors.centerIn: parent
                        width: parent.width - 40
                        visible: !win.plot.visible
                        horizontalAlignment: Text.AlignHCenter
                        wrapMode: Text.Wrap
                        text: "Run IdVgSweep or IdVdSweep in Transistor Electrical Test to display a graph."
                        color: Theme.textDim
                    }
                }
                HistoryPanel {
                    app: win.app
                }
            }
        }
    }
}
