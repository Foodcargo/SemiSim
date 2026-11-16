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

        RowLayout {
            Layout.fillWidth: true
            spacing: 4
            SectionHeader {
                Layout.fillWidth: true
                title: "Process Navigator"
            }
            ToolButton {
                text: "↶"
                font.pixelSize: 22
                implicitWidth: 34
                implicitHeight: 32
                enabled: root.app.canUndoSelected
                Accessible.name: "Undo selected process"
                ToolTip.visible: hovered
                ToolTip.text: "Undo the latest completed run of the selected process"
                onClicked: root.app.requestUndoSelected()
            }
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
                    text: "⌄  " + Theme.english(section)
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
                        text: Theme.english(model.name) + (model.enabled ? "" : "  (Out of Scope)")
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
                    ToolTip.text: model.enabled ? model.reason : "Packaging is outside the current specification."
                }
            }
        }

        Label {
            Layout.fillWidth: true
            visible: root.app.lastRunFailed
            text: "The previous process failed quality checks. Enable Continue on Failure to proceed (except initialization and mask fabrication)."
            color: Theme.warn
            wrapMode: Text.Wrap
            font.pixelSize: 11
        }
    }

    Connections {
        target: root.app
        function onUndoConfirmationRequested(processName, laterCount) {
            confirmUndo.processName = Theme.english(processName)
            confirmUndo.laterCount = laterCount
            confirmUndo.open()
        }
        function onUndoError(message) {
            undoError.message = message
            undoError.open()
        }
    }
    Dialog {
        id: confirmUndo
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(480, parent ? parent.width - 40 : 480)
        modal: true
        title: "Undo Process"
        property string processName: ""
        property int laterCount: 0
        standardButtons: Dialog.Ok | Dialog.Cancel
        closePolicy: Popup.CloseOnEscape
        contentItem: Label {
            text: "Undo " + confirmUndo.processName + " and " + confirmUndo.laterCount
                  + " later process run(s)?\n\nTheir layers, masks and results may be removed. Earlier processes will be kept."
            wrapMode: Text.Wrap
            color: Theme.text
        }
        onAccepted: root.app.confirmUndoSelected()
        onRejected: root.app.discardUndoRequest()
    }
    Dialog {
        id: undoError
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(480, parent ? parent.width - 40 : 480)
        modal: true
        title: "Unable to Undo"
        property string message: ""
        standardButtons: Dialog.Ok
        contentItem: Label { text: undoError.message; wrapMode: Text.Wrap; color: Theme.text }
    }
}
