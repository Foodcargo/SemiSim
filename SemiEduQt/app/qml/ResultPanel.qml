import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

// Inspector "Properties" + "Errors / 판정 사유".
Rectangle {
    id: root
    required property ResultViewModel result
    required property ProcessEditorViewModel editor
    color: Theme.dock

    // Show each new result from the top.
    Connections {
        target: root.result
        function onResultChanged() { scroll.contentItem.contentY = 0 }
    }

    ScrollView {
        id: scroll
        anchors.fill: parent
        anchors.margins: 8
        contentWidth: availableWidth
        clip: true

        ColumnLayout {
            width: scroll.availableWidth - 8
            spacing: 6

            SectionHeader { Layout.fillWidth: true; title: "Properties" }

            Rectangle {
                Layout.fillWidth: true
                implicitHeight: props.implicitHeight + 16
                color: Theme.field
                border.color: Theme.border

                ColumnLayout {
                    id: props
                    anchors.left: parent.left
                    anchors.right: parent.right
                    anchors.top: parent.top
                    anchors.margins: 8
                    spacing: 3

                    Label {
                        visible: !root.result.hasResult
                        text: "아직 실행 결과가 없습니다. 조건을 넣고 [계산 미리보기] 또는 [공정 실행]을 누르세요."
                        color: Theme.textDim
                        wrapMode: Text.Wrap
                        Layout.fillWidth: true
                    }

                    GridLayout {
                        visible: root.result.hasResult
                        columns: 2
                        columnSpacing: 14
                        rowSpacing: 3
                        Layout.fillWidth: true
                        Label { text: "Process"; color: Theme.textDim }
                        Label { text: root.result.processName; color: Theme.text; Layout.fillWidth: true; wrapMode: Text.Wrap }
                        Label { text: "Status"; color: Theme.textDim }
                        Label {
                            text: Theme.statusIcon(root.result.statusKind) + " " + root.result.statusText
                            color: Theme.statusColor(root.result.statusKind)
                            font.bold: true
                        }
                        Label { text: "Mode"; color: Theme.textDim }
                        Label {
                            text: (root.result.isPreview ? "미리보기(상태 미변경)" : "실행(커밋)") + (root.editor.staleResult ? " · ⟲ 이전 레시피 결과" : "")
                            color: root.editor.staleResult ? Theme.warn : Theme.text
                        }
                    }

                    Label {
                        Layout.fillWidth: true
                        visible: root.result.deviceVerdict !== ""
                        text: root.result.deviceVerdict
                        color: root.result.deviceNormal ? Theme.ok : Theme.bad
                        font.bold: true
                        font.pixelSize: 14
                        wrapMode: Text.Wrap
                        topPadding: 4
                    }
                    Label {
                        Layout.fillWidth: true
                        visible: root.result.extractedText !== ""
                        text: root.result.extractedText
                        color: Theme.text
                        wrapMode: Text.Wrap
                        font.pixelSize: 12
                    }

                    Flow {
                        Layout.fillWidth: true
                        visible: root.result.hasResult
                        spacing: 4
                        Repeater {
                            model: root.result.stages
                            delegate: Rectangle {
                                required property string name
                                required property string verdict
                                color: verdict === "Pass" ? "#1f3a24" : verdict === "Fail" ? "#4a1f1f" : Theme.section
                                border.color: Theme.border
                                implicitWidth: st.implicitWidth + 10
                                implicitHeight: st.implicitHeight + 4
                                Label {
                                    id: st
                                    anchors.centerIn: parent
                                    font.pixelSize: 11
                                    color: verdict === "Pass" ? Theme.ok : verdict === "Fail" ? Theme.bad : Theme.textFaint
                                    text: (verdict === "Pass" ? "✔ " : verdict === "Fail" ? "✖ " : "· ") + name
                                }
                            }
                        }
                    }
                }
            }

            SectionHeader {
                Layout.fillWidth: true
                visible: root.result.metrics.count > 0
                title: "지표 · 목표 · 판정"
            }
            Repeater {
                model: root.result.metrics
                delegate: Rectangle {
                    id: m
                    required property string name
                    required property string value
                    required property string target
                    required property string range
                    required property string pass
                    required property string note
                    readonly property bool judged: pass !== "—"
                    readonly property bool ok: pass === "합격"
                    Layout.fillWidth: true
                    implicitHeight: mcol.implicitHeight + 10
                    color: Theme.field
                    border.color: !judged ? Theme.border : ok ? "#2f5a37" : "#6b2c2c"
                    ColumnLayout {
                        id: mcol
                        anchors.left: parent.left
                        anchors.right: parent.right
                        anchors.top: parent.top
                        anchors.margins: 5
                        spacing: 1
                        RowLayout {
                            Layout.fillWidth: true
                            Label { text: m.name; color: Theme.text; Layout.fillWidth: true; wrapMode: Text.Wrap; font.pixelSize: 12 }
                            Label { text: m.value; color: Theme.text; font.bold: true; font.pixelSize: 12 }
                            Label {
                                visible: m.judged
                                text: m.ok ? "✔ 합격" : "✖ " + m.pass
                                color: m.ok ? Theme.ok : Theme.bad
                                font.bold: true
                                font.pixelSize: 12
                            }
                        }
                        Label {
                            Layout.fillWidth: true
                            visible: m.judged || m.note !== "정보"
                            text: (m.judged ? "목표 " + m.target + " · 허용구간 " + m.range : "") + (m.note !== "" && m.note !== "정보" ? (m.judged ? " · " : "") + m.note : "")
                            color: Theme.textFaint
                            font.pixelSize: 11
                            wrapMode: Text.Wrap
                        }
                    }
                }
            }

            SectionHeader { Layout.fillWidth: true; title: "Errors / 판정 사유" }
            Rectangle {
                Layout.fillWidth: true
                implicitHeight: errCol.implicitHeight + 16
                color: Theme.field
                border.color: Theme.border
                ColumnLayout {
                    id: errCol
                    anchors.left: parent.left
                    anchors.right: parent.right
                    anchors.top: parent.top
                    anchors.margins: 8
                    spacing: 8
                    Label {
                        visible: root.result.errors.count === 0
                        text: root.result.hasResult ? "오류·불합격 사유 없음" : "—"
                        color: Theme.textFaint
                    }
                    Repeater {
                        model: root.result.errors
                        delegate: Label {
                            required property string stage
                            required property string code
                            required property string message
                            required property string action
                            Layout.fillWidth: true
                            wrapMode: Text.Wrap
                            font.pixelSize: 12
                            color: code === "QUALITY_FAIL" || code === "QUALITY_NOT_EVALUABLE" ? Theme.warn : Theme.bad
                            text: code + " @ " + stage + " — " + message + (action !== "" ? "  → " + action : "")
                        }
                    }
                    Label {
                        Layout.fillWidth: true
                        visible: root.result.warningsText !== ""
                        text: root.result.warningsText
                        wrapMode: Text.Wrap
                        color: Theme.textDim
                        font.pixelSize: 12
                    }
                }
            }

            Label {
                Layout.fillWidth: true
                visible: root.result.hasResult
                text: "모든 수치는 검증되지 않은 교육용 예시 모델의 결과입니다. 테스트 통과는 실제 공정 검증이 아닙니다."
                font.pixelSize: 11
                color: Theme.bannerText
                wrapMode: Text.Wrap
            }
            Item { implicitHeight: 6 }
        }
    }
}
