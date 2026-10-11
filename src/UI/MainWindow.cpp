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
    connect(toolbar, &Toolbar::resetRequested, m_wafer, &Wafer::clear);

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
    connect(m_processPanel, &ProcessPanel::resetRequested, m_wafer, &Wafer::clear);
    connect(m_wafer, &Wafer::changed, this, [this]() {
        m_processPanel->setCurrentStep(m_wafer->processStep());
    });
    m_processPanel->setCurrentStep(m_wafer->processStep());
}

void MainWindow::applySubmittedProcess(int step, const QVariantMap &parameters)
{
    if (step != m_wafer->processStep()) {
        m_processPanel->showMessage("Current process step changed. Please submit the visible step again.");
        return;
    }

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
    m_processPanel->showMessage("Applied values and advanced to the next process step.");
}
