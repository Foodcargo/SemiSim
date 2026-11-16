import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

// "Layer Manager": LayerIds present in the current wafer state.
Rectangle {
    id: root
    required property AppViewModel app
    color: Theme.dock

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 8
        spacing: 4

        SectionHeader {
            Layout.fillWidth: true
            title: "Layer Manager"
            trailing: "마스크 " + root.app.maskCount + "개"
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 8
            Label { text: "Layer"; Layout.preferredWidth: 130; color: Theme.textDim; font.pixelSize: 12; leftPadding: 22 }
            Label { text: "재료"; Layout.preferredWidth: 90; color: Theme.textDim; font.pixelSize: 12 }
            Label { text: "평균 두께"; Layout.preferredWidth: 90; color: Theme.textDim; font.pixelSize: 12 }
            Label { text: "범위"; Layout.fillWidth: true; color: Theme.textDim; font.pixelSize: 12 }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: Theme.field
            border.color: Theme.border
            ListView {
                anchors.fill: parent
                anchors.margins: 4
                clip: true
                model: root.app.layers
                ScrollBar.vertical: ScrollBar {}
                delegate: Item {
                    id: row
                    required property string layerId
                    required property string materialKo
                    required property string material
                    required property string color
                    required property string thicknessText
                    required property string coverage
                    width: ListView.view.width
                    height: 28
                    activeFocusOnTab: true
                    Keys.onReturnPressed: root.app.selectLayer(row.layerId)
                    Keys.onSpacePressed: root.app.selectLayer(row.layerId)
                    Rectangle {
                        anchors.fill: parent
                        color: root.app.selectedLayerId === row.layerId ? Theme.accentSoft
                             : layerMouse.containsMouse ? Theme.section : "transparent"
                        border.color: row.activeFocus ? Theme.accent : "transparent"
                    }
                    RowLayout {
                    anchors.fill: parent
                    spacing: 8
                    Rectangle {
                        implicitWidth: 12
                        implicitHeight: 12
                        color: row.color
                        border.color: Theme.borderLight
                        Layout.leftMargin: 4
                    }
                    Label { text: row.layerId; Layout.preferredWidth: 110; color: Theme.text; font.pixelSize: 12 }
                    Label { text: row.materialKo; Layout.preferredWidth: 90; color: Theme.textDim; font.pixelSize: 12; elide: Text.ElideRight }
                    Label { text: row.thicknessText; Layout.preferredWidth: 90; color: Theme.text; font.pixelSize: 12 }
                    Label { text: row.coverage; Layout.fillWidth: true; color: Theme.textFaint; font.pixelSize: 12 }
                    }
                    MouseArea {
                        id: layerMouse
                        anchors.fill: parent
                        hoverEnabled: true
                        onClicked: { row.forceActiveFocus(); root.app.selectLayer(row.layerId) }
                        Accessible.name: "Select layer " + row.layerId
                    }
                }
            }
        }
    }
}
