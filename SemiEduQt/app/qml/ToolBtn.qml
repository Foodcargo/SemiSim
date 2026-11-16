import QtQuick
import QtQuick.Controls

// Flat toolbar button; checkable buttons light up when on.
AbstractButton {
    id: b
    property color onColor: Theme.accentSoft
    implicitWidth: Math.max(64, label.implicitWidth + 26)
    implicitHeight: 40
    hoverEnabled: true
    background: Rectangle {
        color: !b.enabled ? Theme.toolbar : b.checked ? b.onColor : b.down ? Theme.accentSoft : b.hovered ? Theme.buttonHover : Theme.button
        border.color: b.checked ? Theme.accent : Theme.border
    }
    contentItem: Label {
        id: label
        text: b.text
        horizontalAlignment: Text.AlignHCenter
        verticalAlignment: Text.AlignVCenter
        color: b.enabled ? Theme.text : Theme.textFaint
        font.pixelSize: 13
    }
    ToolTip.visible: hovered && ToolTip.text !== ""
    ToolTip.delay: 400
}
