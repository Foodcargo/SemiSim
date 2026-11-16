import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

// Parameters of the selected process ("04 Gate oxide / poly gate" panel in the reference design).
Rectangle {
    id: root
    required property ProcessEditorViewModel editor
    required property ResultViewModel result
    required property AppViewModel app
    color: Theme.dock

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 8
        spacing: 6

        SectionHeader {
            Layout.fillWidth: true
            title: root.editor.title
        }

        Rectangle {
            Layout.fillWidth: true
            implicitHeight: hint.implicitHeight + 10
            color: Theme.field
            border.color: Theme.border
            Label {
                id: hint
                anchors.fill: parent
                anchors.margins: 5
                text: "조건을 입력하세요. 범위 밖 값은 실행 전에 차단됩니다. 수치는 검증되지 않은 교육용 예시입니다."
                color: Theme.textDim
                wrapMode: Text.Wrap
                font.pixelSize: 12
            }
        }

        ScrollView {
            id: scroll
            Layout.fillWidth: true
            Layout.fillHeight: true
            contentWidth: availableWidth
            clip: true

            ColumnLayout {
                width: scroll.availableWidth - 10
                spacing: 6

                Repeater {
                    model: root.editor.fields
                    delegate: ParamField {
                        Layout.fillWidth: true
                        editor: root.editor
                    }
                }

                Label {
                    visible: qualityRepeater.count > 0
                    text: "품질 목표 (판정에만 사용, 계산에 넣지 않음)"
                    color: Theme.textDim
                    font.bold: true
                    topPadding: 6
                }
                Repeater {
                    id: qualityRepeater
                    model: root.editor.qualityFields
                    delegate: ParamField {
                        Layout.fillWidth: true
                        editor: root.editor
                    }
                }

                Label { text: "실행 전 점검"; color: Theme.textDim; font.bold: true; topPadding: 6 }
                Label {
                    Layout.fillWidth: true
                    text: root.editor.precheckText
                    wrapMode: Text.Wrap
                    color: root.editor.precheckOk ? Theme.ok : Theme.bad
                    font.pixelSize: 12
                }

                Label {
                    id: effects
                    property bool open: false
                    text: (open ? "▼ " : "▶ ") + "모델에서 반영하지 않는 효과"
                    color: Theme.accent
                    topPadding: 4
                    MouseArea { anchors.fill: parent; onClicked: effects.open = !effects.open }
                }
                Label {
                    Layout.fillWidth: true
                    visible: effects.open
                    text: root.editor.unsupportedEffects
                    wrapMode: Text.Wrap
                    color: Theme.textDim
                    font.pixelSize: 12
                }
                Item { implicitHeight: 4 }
            }
        }

        // Status line (last result)
        Rectangle {
            Layout.fillWidth: true
            implicitHeight: statusLine.implicitHeight + 10
            color: Theme.field
            border.color: Theme.border
            Label {
                id: statusLine
                anchors.fill: parent
                anchors.margins: 5
                wrapMode: Text.Wrap
                font.pixelSize: 12
                color: root.result.hasResult ? Theme.statusColor(root.result.statusKind) : Theme.textDim
                text: root.app.busy ? "계산 중… (취소 가능)"
                    : !root.result.hasResult ? "아직 실행 결과가 없습니다."
                    : Theme.statusIcon(root.result.statusKind) + " " + root.result.processName + " — " + root.result.statusText
                      + (root.result.isPreview ? " (미리보기, 상태 미변경)" : "") + (root.editor.staleResult ? " · 이전 레시피 결과" : "")
            }
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 6
            Button {
                Layout.fillWidth: true
                text: "계산 미리보기"
                enabled: root.editor.canPreview
                onClicked: root.editor.preview()
            }
            Button {
                Layout.fillWidth: true
                text: "공정 실행"
                highlighted: true
                enabled: root.editor.canRun
                onClicked: root.editor.run()
            }
            Button {
                text: "취소"
                enabled: root.app.busy
                onClicked: root.app.cancel()
            }
        }
    }
}
