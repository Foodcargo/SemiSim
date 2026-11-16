import QtQuick
import QtQuick.Controls

// Boxed section heading ("Cell Navigator", "Properties").
Rectangle {
    id: root
    property string title
    property string trailing: ""
    implicitHeight: 32
    color: Theme.section
    border.color: Theme.border
    Label {
        anchors.verticalCenter: parent.verticalCenter
        x: 8
        text: root.title
        font.bold: true
        font.pixelSize: 14
        color: Theme.text
    }
    Label {
        anchors.verticalCenter: parent.verticalCenter
        anchors.right: parent.right
        anchors.rightMargin: 8
        text: root.trailing
        color: Theme.textDim
        font.pixelSize: 12
    }
}
