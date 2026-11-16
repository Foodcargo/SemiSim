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


    // Presentation labels only; engine values and validation stay unchanged.
    readonly property var englishLabels: {
        "① 웨이퍼 제조": "① Wafer Preparation",
        "② 산화": "② Oxidation",
        "③ 포토": "③ Photolithography",
        "④ 식각": "④ Etching",
        "⑤ 증착·이온주입": "⑤ Deposition & Implantation",
        "⑥ 금속 배선": "⑥ Metallization",
        "⑦ EDS(검사)": "⑦ EDS / Inspection",
        "⑧ 패키징": "⑧ Packaging",
        "웨이퍼 초기화": "Wafer Initialization",
        "열산화": "Thermal Oxidation",
        "마스크 제작": "Mask Fabrication",
        "포토(도포·노광·현상)": "Photolithography",
        "식각": "Etching",
        "세정·PR 제거": "Cleaning / PR Strip",
        "증착(CVD/PVD/ALD)": "Deposition (CVD/PVD/ALD)",
        "이온주입": "Ion Implantation",
        "열처리(활성화)": "Annealing / Activation",
        "금속 배선(이상적 패턴)": "Metal Patterning",
        "컨택 형성(IdealContact)": "Ideal Contact",
        "CMP(화학적 기계 연마)": "CMP",
        "검사": "Inspection",
        "트랜지스터 전기 검사": "Transistor Electrical Test",
        "패키징": "Packaging",
        "입력 검증": "Input Validation",
        "웨이퍼 선행조건": "Preconditions",
        "장비 인터록": "Interlocks",
        "모델 적용 범위": "Model Domain",
        "계산": "Calculation",
        "결과 불변조건": "Invariants",
        "품질 평가": "Quality",
        "상태 커밋": "Commit",
        "도체": "Conductor",
        "도체성(N+)": "Conductive (N+)",
        "도체성(P+)": "Conductive (P+)",
        "반도체(n형)": "Semiconductor (n-type)",
        "반도체(p형)": "Semiconductor (p-type)",
        "반도체(진성에 가까움)": "Near-intrinsic Semiconductor",
        "절연체": "Insulator",
        "합격": "Pass",
        "불합격": "Fail",
        "정보": "Info"
}
    function english(value) { return englishLabels[value] || value }
    function statusLabel(kind) {
        const labels = { success: "Success", failure: "Quality Failure", blocked: "Blocked", cancelled: "Cancelled", error: "Error" }
        return labels[kind] || kind
    }

    function statusColor(kind) {
        return kind === "success" ? ok : kind === "failure" ? warn : kind === "blocked" ? bad : kind === "cancelled" ? muted : purple
    }
    function statusIcon(kind) {
        return kind === "success" ? "✔" : kind === "failure" ? "✖" : kind === "blocked" ? "⛔" : kind === "cancelled" ? "⏹" : "⚠"
    }
}
