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

        SectionHeader { Layout.fillWidth: true; title: "Run History"; trailing: "최신 순 · Undo해도 보존" }

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
                delegate: RowLayout {
                    id: h
                    required property string time
                    required property string name
                    required property string statusText
                    required property string statusKind
                    required property bool physical
                    width: ListView.view.width - 10
                    spacing: 10
                    Label { text: h.time; color: Theme.textFaint; font.pixelSize: 12 }
                    Label { text: h.name; color: Theme.text; font.pixelSize: 12; Layout.preferredWidth: 150; elide: Text.ElideRight }
                    Label { text: Theme.statusIcon(h.statusKind) + " " + h.statusText; color: Theme.statusColor(h.statusKind); font.pixelSize: 12; Layout.fillWidth: true; elide: Text.ElideRight }
                    Label { text: h.physical ? "상태 변경" : "기록만"; color: Theme.textFaint; font.pixelSize: 11 }
                }
            }
        }
    }
}
