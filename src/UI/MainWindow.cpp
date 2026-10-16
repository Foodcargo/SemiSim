#include "UI/MainWindow.h"

#include "Model/Wafer.h"
#include "Process/CMP.h"
#include "Process/Deposition.h"
#include "Process/Etching.h"
#include "Process/Implantation.h"
#include "Process/Oxidation.h"
#include "Process/Photo.h"
#include "Scene/WaferScene.h"
#include "UI/InfoPanel.h"
#include "UI/ProcessPanel.h"
#include "UI/Toolbar.h"

#include <QColor>
#include <QDockWidget>
#include <QString>
#include <QStringList>

namespace {
QColor materialColor(const QString &material)
{
    if (material.startsWith("Copper")) {
        return QColor("#c2410c");
    }
    if (material.startsWith("Tungsten")) {
        return QColor("#f59e0b");
    }
    return QColor("#2563eb");
}

void checkRange(QStringList &failures, const QVariantMap &parameters, const QString &key, const QString &label, double minimum, double maximum, const QString &unit)
{
    const double value = parameters.value(key).toDouble();
    if (value < minimum || value > maximum) {
        failures.append(QString("%1 %2 %3: allowed %4-%5 %6")
                            .arg(label)
                            .arg(value, 0, 'g', 4)
                            .arg(unit)
                            .arg(minimum, 0, 'g', 4)
                            .arg(maximum, 0, 'g', 4)
                            .arg(unit));
    }
}

void collectProcessFailures(int step, const QVariantMap &parameters, QStringList &failures)
{
    switch (step) {
    case 0:
        checkRange(failures, parameters, "substrateThicknessUm", "Wafer thickness", 500.0, 800.0, "um");
        break;
    case 1:
        checkRange(failures, parameters, "wellDose", "Well implant dose", 1.0e12, 5.0e13, "ions/cm2");
        break;
    case 2:
        checkRange(failures, parameters, "photoThicknessNm", "Photoresist thickness", 500.0, 2000.0, "nm");
        break;
    case 3:
        checkRange(failures, parameters, "gateTemperatureC", "Gate oxidation temperature", 850.0, 1100.0, "C");
        checkRange(failures, parameters, "gateTimeMin", "Gate oxidation time", 10.0, 120.0, "min");
        checkRange(failures, parameters, "gateOxideNm", "Gate oxide thickness", 5.0, 120.0, "nm");
        checkRange(failures, parameters, "polyThicknessNm", "Polysilicon thickness", 150.0, 500.0, "nm");
        break;
    case 4:
        checkRange(failures, parameters, "sourceDrainDose", "Source/drain implant dose", 5.0e14, 5.0e15, "ions/cm2");
        break;
    case 5:
        checkRange(failures, parameters, "contactEtchDepthNm", "Contact etch depth", 100.0, 500.0, "nm");
        checkRange(failures, parameters, "contactFillThicknessNm", "Contact fill thickness", 200.0, 1000.0, "nm");
        break;
    case 6:
        checkRange(failures, parameters, "metalThicknessNm", "Metal thickness", 300.0, 1200.0, "nm");
        break;
    default:
        break;
    }
}
}

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent),
      m_wafer(new Wafer(this)),
      m_scene(new WaferScene(this)),
      m_processPanel(new ProcessPanel(this)),
      m_infoPanel(new InfoPanel(this))
{
    setWindowTitle("SemiSim EDA - 3D Layout Prototype");
    resize(1440, 860);
    setStyleSheet(
        "QMainWindow, QDockWidget, QWidget { background: #202225; color: #d7dde5; }"
        "QDockWidget::title { background: #2b2e33; padding: 6px; border: 1px solid #3a3f46; }"
        "QToolBar { background: #262a2f; border-bottom: 1px solid #3a3f46; spacing: 4px; }"
        "QToolButton { background: #30343a; border: 1px solid #444b55; border-radius: 3px; padding: 5px 8px; }"
        "QToolButton:hover { background: #3a4048; }"
        "QLabel#PanelTitle { color: #f8fafc; font-weight: 600; padding: 3px 0; }"
        "QTreeWidget, QTableWidget, QListWidget, QLabel { background: #17191d; border: 1px solid #363b44; color: #d7dde5; }"
        "QHeaderView::section { background: #2b2e33; color: #d7dde5; border: 0; padding: 4px; }"
        "QPushButton { background: #30343a; border: 1px solid #47505c; border-radius: 4px; padding: 6px; }"
        "QPushButton:hover { background: #3b424c; }"
        "QCheckBox { background: transparent; }"
        "QScrollBar:vertical, QScrollBar:horizontal { background: #202225; width: 12px; height: 12px; }"
        "QScrollBar::handle { background: #4b5563; border-radius: 4px; }");

    m_scene->setWafer(m_wafer);
    m_infoPanel->setWafer(m_wafer);
    setCentralWidget(m_scene);

    auto *toolbar = new Toolbar(this);
    addToolBar(toolbar);
    connect(toolbar, &Toolbar::resetRequested, this, &MainWindow::resetProcess);

    auto *processDock = new QDockWidget("Design Browser", this);
    processDock->setWidget(m_processPanel);
    addDockWidget(Qt::LeftDockWidgetArea, processDock);
    processDock->setMinimumWidth(270);

    auto *infoDock = new QDockWidget("Inspector", this);
    infoDock->setWidget(m_infoPanel);
    addDockWidget(Qt::RightDockWidgetArea, infoDock);
    infoDock->setMinimumWidth(280);

    wireProcessActions();
}

void MainWindow::wireProcessActions()
{
    connect(m_processPanel, &ProcessPanel::processSubmitted, this, &MainWindow::applySubmittedProcess);
    connect(m_processPanel, &ProcessPanel::resetRequested, this, &MainWindow::resetProcess);
    connect(m_wafer, &Wafer::changed, this, [this]() {
        m_processPanel->setCurrentStep(m_wafer->processStep());
    });
    m_processPanel->setCurrentStep(m_wafer->processStep());
}

void MainWindow::resetProcess()
{
    m_processFailures.clear();
    m_wafer->clear();
    m_processPanel->showMessage("Process flow has been reset.");
}

void MainWindow::applySubmittedProcess(int step, const QVariantMap &parameters)
{
    if (step != m_wafer->processStep()) {
        m_processPanel->showMessage("Current process step changed. Please submit the visible step again.");
        return;
    }

    collectProcessFailures(step, parameters, m_processFailures);

    switch (step) {
    case 0:
        m_wafer->addLayer({{"Prepared Silicon", QColor("#94a3b8"), 0.28}, parameters.value("substrateThicknessUm").toDouble()});
        m_wafer->recordProcess(QString("Substrate: %1, %2 um")
                                   .arg(parameters.value("orientation").toString())
                                   .arg(parameters.value("substrateThicknessUm").toDouble()));
        break;
    case 1: {
        Implantation process;
        process.apply(*m_wafer, parameters.value("wellDose").toDouble());
        m_wafer->recordProcess(QString("Well implant: %1, dose %2 ions/cm2")
                                   .arg(parameters.value("wellDopant").toString())
                                   .arg(parameters.value("wellDose").toDouble(), 0, 'g', 3));
        break;
    }
    case 2: {
        Photo process;
        process.apply(*m_wafer, parameters.value("photoThicknessNm").toDouble());
        m_wafer->setMaskPattern(parameters.value("maskPattern").toString());
        m_wafer->recordProcess(QString("Photo mask: %1, developer %2, resist %3 nm")
                                   .arg(parameters.value("maskPattern").toString())
                                   .arg(parameters.value("developer").toString())
                                   .arg(parameters.value("photoThicknessNm").toDouble()));
        break;
    }
    case 3: {
        Oxidation oxidation;
        OxidationParameters oxidationParameters;
        oxidationParameters.mode = parameters.value("oxidationMode").toString() == "Wet" ? OxidationMode::Wet : OxidationMode::Dry;
        oxidationParameters.temperatureC = parameters.value("gateTemperatureC").toDouble();
        oxidationParameters.timeMin = parameters.value("gateTimeMin").toDouble();
        oxidationParameters.targetThicknessNm = parameters.value("gateOxideNm").toDouble();
        oxidation.apply(*m_wafer, oxidationParameters);

        Deposition deposition;
        deposition.apply(*m_wafer, parameters.value("polyThicknessNm").toDouble());
        m_wafer->recordProcess(QString("Gate stack: %1 oxidation at %2 C for %3 min, oxide %4 nm, poly %5 nm")
                                   .arg(parameters.value("oxidationMode").toString())
                                   .arg(parameters.value("gateTemperatureC").toDouble())
                                   .arg(parameters.value("gateTimeMin").toDouble())
                                   .arg(parameters.value("gateOxideNm").toDouble())
                                   .arg(parameters.value("polyThicknessNm").toDouble()));
        break;
    }
    case 4: {
        Implantation process;
        process.apply(*m_wafer, parameters.value("sourceDrainDose").toDouble());
        m_wafer->recordProcess(QString("Source/drain implant: dose %1 ions/cm2")
                                   .arg(parameters.value("sourceDrainDose").toDouble(), 0, 'g', 3));
        break;
    }
    case 5: {
        Etching etching;
        etching.apply(*m_wafer);
        const QString fillMaterial = parameters.value("contactFill").toString();
        m_wafer->addLayer({{fillMaterial, materialColor(fillMaterial), 0.88}, parameters.value("contactFillThicknessNm").toDouble()});
        CMP cmp;
        cmp.apply(*m_wafer);
        m_wafer->addLayer({{"Planarized Contact", materialColor(fillMaterial).lighter(115), 0.82}, parameters.value("contactEtchDepthNm").toDouble()});
        m_wafer->recordProcess(QString("Contact: %1 etch, %2 nm depth, %3 fill %4 nm")
                                   .arg(parameters.value("etchChemistry").toString())
                                   .arg(parameters.value("contactEtchDepthNm").toDouble())
                                   .arg(fillMaterial)
                                   .arg(parameters.value("contactFillThicknessNm").toDouble()));
        break;
    }
    case 6: {
        const QString metal = parameters.value("metalMaterial").toString();
        m_wafer->addLayer({{metal + " Interconnect", materialColor(metal), 0.84}, parameters.value("metalThicknessNm").toDouble()});
        m_wafer->recordProcess(QString("Metal: %1 interconnect, %2 nm")
                                   .arg(metal)
                                   .arg(parameters.value("metalThicknessNm").toDouble()));
        break;
    }
    default:
        m_processPanel->showMessage("Process flow is already complete.");
        return;
    }

    m_wafer->advanceProcess();
    if (m_wafer->processStep() >= 7) {
        if (m_processFailures.isEmpty()) {
            m_wafer->recordProcess("Final result: Process completed successfully.");
            m_processPanel->showMessage("Process complete: all parameters are within the allowed process limits.");
        } else {
            const QString failureText = m_processFailures.join("; ");
            m_wafer->recordProcess("Final result: Process failed. " + failureText);
            m_processPanel->showMessage("Process complete: process failed because these parameters exceeded the limits. " + failureText);
        }
        return;
    }

    m_processPanel->showMessage("Applied values and advanced to the next process step.");
}
