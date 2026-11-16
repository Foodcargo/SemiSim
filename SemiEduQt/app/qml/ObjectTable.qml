import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

// Object classification table (conductor / semiconductor / insulator) and net list summary.
Rectangle {
    id: root
    required property ResultViewModel result
    color: Theme.dock

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 8
        spacing: 6

        SectionHeader { Layout.fillWidth: true; title: "Object Classification"; trailing: "Recomputed from Current State" }
        Label {
            Layout.fillWidth: true
            text: "Educational thresholds: |net active doping| ≥ 1e19 cm⁻³: conductive N+/P+; ≥ 1e14: semiconductor. Metals are conductors; SiO2, Si3N4 and PR are insulators."
            wrapMode: Text.Wrap
            color: Theme.textDim
            font.pixelSize: 11
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: Theme.field
            border.color: Theme.border
            ListView {
                anchors.fill: parent
                anchors.margins: 6
                clip: true
                spacing: 6
                model: root.result.objects
                ScrollBar.vertical: ScrollBar {}
                delegate: ColumnLayout {
                    id: r
                    required property string objectId
                    required property string material
                    required property string cls
                    required property string reason
                    required property string range
                    required property string net
                    width: ListView.view.width - 10
                    spacing: 1
                    RowLayout {
                        Layout.fillWidth: true
                        Label { text: r.objectId; color: Theme.text; font.bold: true; font.pixelSize: 12 }
                        Label { text: r.material; color: Theme.textDim; font.pixelSize: 12 }
                        Item { Layout.fillWidth: true }
                        Label {
                            text: Theme.english(r.cls)
                            font.bold: true
                            font.pixelSize: 12
                            color: r.cls.indexOf("도체") === 0 ? "#f2c14e" : r.cls === "절연체" ? Theme.textDim : "#8fb8ff"
                        }
                    }
                    Label {
                        Layout.fillWidth: true
                        text: r.reason + " · " + r.range + (r.net !== "—" ? " · " + r.net : "")
                        color: Theme.textFaint
                        font.pixelSize: 11
                        wrapMode: Text.Wrap
                    }
                }
            }
        }

        Label {
            Layout.fillWidth: true
            text: root.result.netsText
            wrapMode: Text.Wrap
            font.pixelSize: 11
            color: Theme.textDim
        }
    }
}
