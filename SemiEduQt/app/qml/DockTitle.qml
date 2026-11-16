import QtQuick
import QtQuick.Controls

// Dock panel title bar ("Design Browser", "Inspector").
Rectangle {
    id: root
    property string title
    implicitHeight: 34
    color: Theme.dockTitle
    border.color: Theme.border
    Label {
        anchors.verticalCenter: parent.verticalCenter
        x: 12
        text: root.title
        color: Theme.text
        font.pixelSize: 14
    }
}
