pragma Singleton
import QtQuick

// Dark EDA-style palette (display only).
QtObject {
    readonly property color window: "#16181c"
    readonly property color dock: "#1c1f24"
    readonly property color dockTitle: "#23272d"
    readonly property color section: "#262a31"
    readonly property color field: "#121418"
    readonly property color border: "#363b44"
    readonly property color borderLight: "#4a515c"
    readonly property color toolbar: "#202328"
    readonly property color button: "#2b3038"
    readonly property color buttonHover: "#353b45"
    readonly property color text: "#d7dde5"
    readonly property color textDim: "#98a2b0"
    readonly property color textFaint: "#6c7480"
    readonly property color accent: "#3d7be0"
    readonly property color accentSoft: "#2b4f8a"
    readonly property color ok: "#5cc06a"
    readonly property color warn: "#f0a43a"
    readonly property color bad: "#ef5f5f"
    readonly property color muted: "#9e9e9e"
    readonly property color purple: "#c084fc"
    readonly property color banner: "#3a2f12"
    readonly property color bannerText: "#f3d27a"

    function statusColor(kind) {
        return kind === "success" ? ok : kind === "failure" ? warn : kind === "blocked" ? bad : kind === "cancelled" ? muted : purple
    }
    function statusIcon(kind) {
        return kind === "success" ? "✔" : kind === "failure" ? "✖" : kind === "blocked" ? "⛔" : kind === "cancelled" ? "⏹" : "⚠"
    }
}
