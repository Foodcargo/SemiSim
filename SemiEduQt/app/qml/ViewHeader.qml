import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import SemiEdu

// Overlay at the top of the 3D view: title, preview badge, legend, terminal voltages.
Item {
    id: root
    required property SceneViewModel scene
    required property ProcessEditorViewModel editor
    implicitHeight: col.implicitHeight + 16

    ColumnLayout {
        id: col
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        anchors.margins: 14
        spacing: 4

        RowLayout {
            Layout.maximumWidth: col.width
            spacing: 12
            Label {
                Layout.fillWidth: true
                elide: Text.ElideRight
                text: (root.scene.viewKind === "mask" ? "마스크 단면 view (축척 4×, 웨이퍼 환산)" : "국소 웨이퍼 단면 3D process view")
                      + (root.editor.title !== "" ? "  ·  " + root.editor.title : "")
                color: Theme.text
                font.pixelSize: 15
            }
            Rectangle {
                visible: root.scene.isPreview
                color: "#5a3410"
                border.color: Theme.warn
                implicitWidth: previewLabel.implicitWidth + 12
                implicitHeight: previewLabel.implicitHeight + 4
                Label {
                    id: previewLabel
                    anchors.centerIn: parent
                    text: "미리보기 — 커밋되지 않음"
                    color: Theme.warn
                    font.bold: true
                    font.pixelSize: 12
                }
            }
        }
        Label {
            Layout.maximumWidth: col.width
            textFormat: Text.RichText
            text: root.scene.legendHtml
            color: Theme.textDim
            wrapMode: Text.Wrap
            font.pixelSize: 12
        }
        Label {
            Layout.maximumWidth: col.width
            visible: root.scene.terminalsText !== ""
            textFormat: Text.RichText
            text: "단자 " + root.scene.terminalsText
            color: Theme.text
            wrapMode: Text.Wrap
            font.pixelSize: 12
        }
    }
}
