import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

Rectangle {
    id: root
    required property AppViewModel app
    color: Theme.dock

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 8
        spacing: 6

        SectionHeader { Layout.fillWidth: true; title: "Run History"; trailing: "Newest first · Preserved after Undo" }

        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: Theme.field
            border.color: Theme.border
            ListView {
                anchors.fill: parent
                anchors.margins: 6
                clip: true
                spacing: 4
                model: root.app.history
                ScrollBar.vertical: ScrollBar {}
                delegate: ItemDelegate {
                    id: h
                    required property string time
                    required property string name
                    required property string statusText
                    required property string statusKind
                    required property bool physical
                    required property bool undone
                    required property string inputDetails
                    width: ListView.view.width - 10
                    onClicked: {
                        details.title = Theme.english(h.name) + " — " + h.time
                        detailsText.text = h.inputDetails || "No input values recorded."
                        details.open()
                    }
                    contentItem: RowLayout {
                    spacing: 10
                    Label { text: h.time; color: Theme.textFaint; font.pixelSize: 12 }
                    Label { text: Theme.english(h.name); color: Theme.text; font.pixelSize: 12; Layout.preferredWidth: 150; elide: Text.ElideRight }
                    Label { text: h.undone ? "↶ Undone" : Theme.statusIcon(h.statusKind) + " " + Theme.statusLabel(h.statusKind); color: h.undone ? Theme.textFaint : Theme.statusColor(h.statusKind); font.pixelSize: 12; Layout.fillWidth: true; elide: Text.ElideRight }
                    Label { text: h.physical ? "State Changed" : "Record Only"; color: Theme.textFaint; font.pixelSize: 11 }
                    }
                }
            }
        }
    }
    Dialog {
        id: details
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(620, parent ? parent.width - 40 : 620)
        height: Math.min(560, parent ? parent.height - 40 : 560)
        modal: true
        standardButtons: Dialog.Close
        contentItem: ScrollView {
            clip: true
            TextArea {
                id: detailsText
                readOnly: true
                selectByMouse: true
                textFormat: TextEdit.PlainText
                wrapMode: TextEdit.Wrap
                color: Theme.text
                font.pixelSize: 13
                background: Rectangle { color: Theme.field }
            }
        }
    }
}
