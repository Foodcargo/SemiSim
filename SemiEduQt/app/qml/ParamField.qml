import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

// One input row: boxed label on the left, control on the right. Display only — the engine validates.
ColumnLayout {
    id: f
    property ProcessEditorViewModel editor
    required property string paramId
    required property string label
    required property string kind
    required property var options
    required property int currentIndex
    required property string text
    required property string unit
    required property string hint
    required property string error
    required property bool readOnly
    spacing: 2

    RowLayout {
        Layout.fillWidth: true
        spacing: 8

        Rectangle {
            Layout.preferredWidth: 150
            Layout.alignment: Qt.AlignTop
            implicitHeight: Math.max(32, nameLabel.implicitHeight + 10)
            color: Theme.section
            border.color: f.error !== "" ? Theme.bad : Theme.border
            Label {
                id: nameLabel
                anchors.fill: parent
                anchors.margins: 5
                verticalAlignment: Text.AlignVCenter
                text: f.label
                color: Theme.text
                wrapMode: Text.Wrap
                font.pixelSize: 12
            }
        }

        ComboBox {
            Layout.fillWidth: true
            visible: f.kind === "enum"
            editable: false
            model: f.options
            currentIndex: f.currentIndex
            enabled: !f.readOnly && !(f.editor && f.editor.busy)
            onActivated: index => f.editor.setOption(f.paramId, index)
        }

        TextField {
            Layout.fillWidth: true
            visible: f.kind === "number"
            text: f.text
            readOnly: f.readOnly
            enabled: !(f.editor && f.editor.busy)
            color: f.readOnly ? Theme.textFaint : Theme.text
            selectByMouse: true
            rightPadding: unitLabel.implicitWidth + 14
            onTextEdited: f.editor.setText(f.paramId, text)
            Label {
                id: unitLabel
                anchors.right: parent.right
                anchors.rightMargin: 8
                anchors.verticalCenter: parent.verticalCenter
                text: f.unit
                color: Theme.textDim
                font.pixelSize: 12
            }
        }

        Label {
            Layout.fillWidth: true
            visible: f.kind === "info"
            text: f.text
            color: Theme.textDim
            wrapMode: Text.Wrap
            font.pixelSize: 12
        }
    }

    Label {
        Layout.fillWidth: true
        Layout.leftMargin: 158
        visible: f.hint !== "" && f.kind !== "info"
        text: f.hint + (f.readOnly ? " · 읽기 전용" : "")
        color: Theme.textFaint
        font.pixelSize: 11
        wrapMode: Text.Wrap
    }

    Label {
        Layout.fillWidth: true
        Layout.leftMargin: 158
        visible: f.error !== ""
        text: "✖ " + f.error
        color: Theme.bad
        font.pixelSize: 12
        wrapMode: Text.Wrap
    }
}
