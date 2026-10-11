#include "UI/InfoPanel.h"

#include "Model/Wafer.h"

#include <QLabel>
#include <QListWidget>
#include <QStringList>
#include <QVBoxLayout>
#include <QtGlobal>

InfoPanel::InfoPanel(QWidget *parent)
    : QWidget(parent),
      m_label(new QLabel(this))
{
    auto *layout = new QVBoxLayout(this);
    layout->setContentsMargins(8, 8, 8, 8);
    layout->setSpacing(10);

    auto *propertiesTitle = new QLabel("Properties", this);
    propertiesTitle->setObjectName("PanelTitle");
    layout->addWidget(propertiesTitle);

    m_label->setTextFormat(Qt::PlainText);
    m_label->setAlignment(Qt::AlignTop | Qt::AlignLeft);
    m_label->setMinimumHeight(210);
    layout->addWidget(m_label);

    auto *drcTitle = new QLabel("Errors / DRC", this);
    drcTitle->setObjectName("PanelTitle");
    layout->addWidget(drcTitle);

    auto *violations = new QListWidget(this);
    violations->addItem("M1 spacing violation @ X: 132.4 Y: 78.9");
    violations->addItem("Via1 enclosure below minimum @ X: 96.0 Y: 51.2");
    violations->addItem("Poly overlap warning @ X: 44.3 Y: 118.7");
    violations->addItem("Floating net CLK_BUF_OUT");
    violations->addItem("N-WELL tap density warning");
    layout->addWidget(violations, 1);
}

void InfoPanel::setWafer(Wafer *wafer)
{
    if (m_wafer) {
        disconnect(m_wafer, nullptr, this, nullptr);
    }

    m_wafer = wafer;
    if (m_wafer) {
        connect(m_wafer, &Wafer::changed, this, &InfoPanel::refresh);
    }
    refresh();
}

void InfoPanel::refresh()
{
    const QStringList processNames = {
        "01 Substrate preparation",
        "02 Well formation",
        "03 Active diffusion mask",
        "04 Gate oxide / poly gate",
        "05 Source-drain implant",
        "06 Contact etch / fill",
        "07 Metal interconnect"
    };
    const int lastStep = static_cast<int>(processNames.size()) - 1;
    const int step = m_wafer ? qBound(0, m_wafer->processStep(), lastStep) : 0;
    const QString maskPattern = m_wafer ? m_wafer->maskPattern() : QStringLiteral("Single gate line");
    const QString processLog = m_wafer && !m_wafer->processLog().isEmpty()
                                   ? m_wafer->processLog().join("\n")
                                   : QStringLiteral("No applied recipe values yet.");

    m_label->setText(QString(
        "Type        NMOS transistor\n"
        "Cell        NMOS_X1\n"
        "Process     %1\n"
        "Mask        %2\n"
        "Layer       POLY_GATE\n"
        "Gate net    IN\n"
        "Source net  VSS\n"
        "Drain net   OUT\n"
        "Bulk net    P-WELL\n"
        "Width       1.20 um\n"
        "Length      0.18 um\n"
        "Location    X: 0.00  Y: 0.00\n"
        "Status      Selected\n\n"
        "Transform\n"
        "Rotate      0 deg\n"
        "Mirror      None\n"
        "Scale       1.0\n\n"
        "Applied recipe\n"
        "%3").arg(processNames[step], maskPattern, processLog));
}
