import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

// "Process Navigator": the 8 process groups as a tree-like list.
Rectangle {
    id: root
    required property AppViewModel app
    color: Theme.dock

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 8
        spacing: 6

        SectionHeader {
            Layout.fillWidth: true
            title: "Process Navigator"
            trailing: "8대 공정"
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: Theme.field
            border.color: Theme.border

            ListView {
                id: list
                anchors.fill: parent
                anchors.margins: 4
                clip: true
                model: root.app.processes
                ScrollBar.vertical: ScrollBar {}
                section.property: "group"
                section.delegate: Label {
                    required property string section
                    width: ListView.view.width
                    text: "⌄  " + section
                    color: Theme.text
                    topPadding: 6
                    bottomPadding: 2
                    leftPadding: 6
                    font.pixelSize: 13
                }
                delegate: Rectangle {
                    width: ListView.view.width
                    height: 28
                    readonly property bool current: model.processId === root.app.currentProcessId
                    color: current ? Theme.accentSoft : mouse.containsMouse && model.enabled ? Theme.section : "transparent"
                    Label {
                        anchors.verticalCenter: parent.verticalCenter
                        x: 34
                        text: model.name + (model.enabled ? "" : "  (비활성 · 범위 밖)")
                        color: !model.enabled ? Theme.textFaint : parent.current ? "#ffffff" : Theme.text
                        font.pixelSize: 13
                    }
                    MouseArea {
                        id: mouse
                        anchors.fill: parent
                        hoverEnabled: true
                        enabled: model.enabled && !root.app.busy
                        onClicked: root.app.selectProcess(model.processId)
                    }
                    ToolTip.visible: mouse.containsMouse && model.reason !== ""
                    ToolTip.text: model.reason
                }
            }
        }

        Label {
            Layout.fillWidth: true
            visible: root.app.lastRunFailed
            text: "직전 공정이 품질 불량입니다. 다음 공정은 ‘불량 상태로 계속 학습’을 켜야 진행됩니다(초기화·마스크 제작 제외)."
            color: Theme.warn
            wrapMode: Text.Wrap
            font.pixelSize: 11
        }
    }
}
