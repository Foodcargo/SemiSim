#include "UI/ProcessPanel.h"

#include <QAbstractItemView>
#include <QCheckBox>
#include <QColor>
#include <QComboBox>
#include <QDoubleSpinBox>
#include <QFormLayout>
#include <QFrame>
#include <QHeaderView>
#include <QLabel>
#include <QPushButton>
#include <QStringList>
#include <QStackedWidget>
#include <QTableWidget>
#include <QTableWidgetItem>
#include <QTreeWidget>
#include <QTreeWidgetItem>
#include <QVBoxLayout>
#include <QPair>
#include <QVector>
#include <QtGlobal>

ProcessPanel::ProcessPanel(QWidget *parent)
    : QWidget(parent)
{
    auto *layout = new QVBoxLayout(this);
    layout->setContentsMargins(8, 8, 8, 8);
    layout->setSpacing(10);

    auto *navigatorTitle = new QLabel("Cell Navigator", this);
    navigatorTitle->setObjectName("PanelTitle");
    layout->addWidget(navigatorTitle);

    auto *tree = new QTreeWidget(this);
    tree->setHeaderHidden(true);
    tree->setMinimumHeight(250);

    auto *top = new QTreeWidgetItem(tree, QStringList{"TOP_CELL"});
    top->setExpanded(true);
    auto *device = new QTreeWidgetItem(top, QStringList{"NMOS_X1"});
    auto *terminals = new QTreeWidgetItem(device, QStringList{"Terminals"});
    auto *layoutLayers = new QTreeWidgetItem(device, QStringList{"Layout Layers"});
    terminals->addChild(new QTreeWidgetItem(QStringList{"GATE - IN"}));
    terminals->addChild(new QTreeWidgetItem(QStringList{"SOURCE - VSS"}));
    terminals->addChild(new QTreeWidgetItem(QStringList{"DRAIN - OUT"}));
    terminals->addChild(new QTreeWidgetItem(QStringList{"BULK - P-WELL"}));
    layoutLayers->addChild(new QTreeWidgetItem(QStringList{"ACTIVE"}));
    layoutLayers->addChild(new QTreeWidgetItem(QStringList{"POLY_GATE"}));
    layoutLayers->addChild(new QTreeWidgetItem(QStringList{"CONTACT"}));
    layoutLayers->addChild(new QTreeWidgetItem(QStringList{"M1_SOURCE / M1_DRAIN"}));
    layoutLayers->addChild(new QTreeWidgetItem(QStringList{"M2 routing"}));
    auto *flow = new QTreeWidgetItem(top, QStringList{"Process Flow"});
    flow->addChild(new QTreeWidgetItem(QStringList{"01 Substrate preparation"}));
    flow->addChild(new QTreeWidgetItem(QStringList{"02 Well formation"}));
    flow->addChild(new QTreeWidgetItem(QStringList{"03 Active diffusion mask"}));
    flow->addChild(new QTreeWidgetItem(QStringList{"04 Gate oxide / poly gate"}));
    flow->addChild(new QTreeWidgetItem(QStringList{"05 Source-drain implant"}));
    flow->addChild(new QTreeWidgetItem(QStringList{"06 Contact etch / fill"}));
    flow->addChild(new QTreeWidgetItem(QStringList{"07 Metal interconnect"}));
    tree->expandAll();
    layout->addWidget(tree, 3);

    auto *separator = new QFrame(this);
    separator->setFrameShape(QFrame::HLine);
    layout->addWidget(separator);

    auto *layerTitle = new QLabel("Layer Manager", this);
    layerTitle->setObjectName("PanelTitle");
    layout->addWidget(layerTitle);

    auto *layerTable = new QTableWidget(9, 4, this);
    layerTable->setHorizontalHeaderLabels(QStringList{"", "Layer", "Vis", "Sel"});
    layerTable->verticalHeader()->setVisible(false);
    layerTable->horizontalHeader()->setStretchLastSection(false);
    layerTable->horizontalHeader()->setSectionResizeMode(0, QHeaderView::Fixed);
    layerTable->horizontalHeader()->setSectionResizeMode(1, QHeaderView::Stretch);
    layerTable->setColumnWidth(0, 28);
    layerTable->setColumnWidth(2, 42);
    layerTable->setColumnWidth(3, 42);
    layerTable->setSelectionMode(QAbstractItemView::NoSelection);
    layerTable->setEditTriggers(QAbstractItemView::NoEditTriggers);

    const QVector<QPair<QString, QColor>> layerRows = {
        {"N-WELL", QColor("#a855f7")},
        {"ACTIVE", QColor("#22c55e")},
        {"POLY", QColor("#ef4444")},
        {"CONTACT", QColor("#f59e0b")},
        {"M1", QColor("#2563eb")},
        {"VIA1", QColor("#06b6d4")},
        {"M2", QColor("#16a34a")},
        {"VIA2", QColor("#f97316")},
        {"M3", QColor("#38bdf8")},
    };

    for (int row = 0; row < layerRows.size(); ++row) {
        auto *swatch = new QTableWidgetItem;
        swatch->setBackground(layerRows[row].second);
        layerTable->setItem(row, 0, swatch);
        layerTable->setItem(row, 1, new QTableWidgetItem(layerRows[row].first));

        auto *visible = new QCheckBox(layerTable);
        visible->setChecked(true);
        layerTable->setCellWidget(row, 2, visible);

        auto *selectable = new QCheckBox(layerTable);
        selectable->setChecked(row >= 2);
        layerTable->setCellWidget(row, 3, selectable);
    }

    layout->addWidget(layerTable, 4);

    auto *recipeSeparator = new QFrame(this);
    recipeSeparator->setFrameShape(QFrame::HLine);
    layout->addWidget(recipeSeparator);

    m_stepTitle = new QLabel(this);
    m_stepTitle->setObjectName("PanelTitle");
    layout->addWidget(m_stepTitle);

    m_stepHint = new QLabel(this);
    m_stepHint->setWordWrap(true);
    layout->addWidget(m_stepHint);

    m_processInputs = new QStackedWidget(this);
    m_processInputs->addWidget(createSubstratePage());
    m_processInputs->addWidget(createWellPage());
    m_processInputs->addWidget(createActiveMaskPage());
    m_processInputs->addWidget(createGatePage());
    m_processInputs->addWidget(createSourceDrainPage());
    m_processInputs->addWidget(createContactPage());
    m_processInputs->addWidget(createMetalPage());
    m_processInputs->addWidget(createCompletePage());
    layout->addWidget(m_processInputs);

    m_statusLabel = new QLabel(this);
    m_statusLabel->setWordWrap(true);
    layout->addWidget(m_statusLabel);

    auto *nextButton = new QPushButton("Apply process values", this);
    nextButton->setMinimumHeight(32);
    layout->addWidget(nextButton);
    connect(nextButton, &QPushButton::clicked, this, [this]() {
        if (m_currentStep >= 7) {
            showMessage("Process flow is already complete.");
            return;
        }
        emit processSubmitted(m_currentStep, currentParameters());
    });

    auto *resetButton = new QPushButton("Reset to substrate", this);
    resetButton->setMinimumHeight(32);
    layout->addWidget(resetButton);
    connect(resetButton, &QPushButton::clicked, this, &ProcessPanel::resetRequested);

    setCurrentStep(0);
}

void ProcessPanel::setCurrentStep(int step)
{
    m_currentStep = qBound(0, step, 7);
    m_processInputs->setCurrentIndex(m_currentStep);

    const QStringList titles = {
        "01 Substrate preparation",
        "02 Well formation",
        "03 Active diffusion mask",
        "04 Gate oxide / poly gate",
        "05 Source-drain implant",
        "06 Contact etch / fill",
        "07 Metal interconnect",
        "Process complete"
    };

    const QStringList hints = {
        "Enter substrate preparation values before starting well formation.",
        "Enter ion implantation dose for the well region.",
        "Enter photoresist coating thickness for the active diffusion mask.",
        "Enter oxidation recipe and polysilicon gate deposition thickness.",
        "Enter source-drain implantation dose.",
        "Enter contact etch depth and contact fill thickness.",
        "Enter metal thickness for interconnect formation.",
        "All process steps have been applied."
    };

    m_stepTitle->setText(titles[m_currentStep]);
    m_stepHint->setText(hints[m_currentStep]);
    m_statusLabel->clear();
}

void ProcessPanel::showMessage(const QString &message)
{
    m_statusLabel->setText(message);
}

QDoubleSpinBox *ProcessPanel::createSpinBox(double minimum, double maximum, double value, const QString &suffix)
{
    auto *spinBox = new QDoubleSpinBox(this);
    spinBox->setRange(minimum, maximum);
    spinBox->setValue(value);
    spinBox->setDecimals(2);
    spinBox->setSuffix(suffix);
    return spinBox;
}

QWidget *ProcessPanel::createSubstratePage()
{
    auto *page = new QWidget(this);
    auto *form = new QFormLayout(page);
    m_orientationBox = new QComboBox(page);
    m_orientationBox->addItems(QStringList{"<100>", "<110>", "<111>"});
    m_substrateThicknessUm = createSpinBox(100.0, 1000.0, 725.0, " um");
    form->addRow("Orientation", m_orientationBox);
    form->addRow("Wafer thickness", m_substrateThicknessUm);
    return page;
}

QWidget *ProcessPanel::createWellPage()
{
    auto *page = new QWidget(this);
    auto *form = new QFormLayout(page);
    m_wellDopantBox = new QComboBox(page);
    m_wellDopantBox->addItems(QStringList{"Boron B11", "Phosphorus P31", "Arsenic As75"});
    m_wellDose = createSpinBox(1.0e11, 1.0e16, 1.0e13, " ions/cm2");
    m_wellDose->setDecimals(0);
    form->addRow("Dopant species", m_wellDopantBox);
    form->addRow("Well implant dose", m_wellDose);
    return page;
}

QWidget *ProcessPanel::createActiveMaskPage()
{
    auto *page = new QWidget(this);
    auto *form = new QFormLayout(page);
    m_maskPatternBox = new QComboBox(page);
    m_maskPatternBox->addItems(QStringList{
        "Single gate line",
        "Two finger gate",
        "Contact array",
        "Guard ring"
    });
    m_developerBox = new QComboBox(page);
    m_developerBox->addItems(QStringList{"TMAH 2.38%", "KOH developer", "Solvent develop"});
    m_photoThicknessNm = createSpinBox(100.0, 3000.0, 1200.0, " nm");
    form->addRow("Mask pattern", m_maskPatternBox);
    form->addRow("Developer", m_developerBox);
    form->addRow("Photoresist thickness", m_photoThicknessNm);
    return page;
}

QWidget *ProcessPanel::createGatePage()
{
    auto *page = new QWidget(this);
    auto *form = new QFormLayout(page);
    m_oxidationModeBox = new QComboBox(page);
    m_oxidationModeBox->addItems(QStringList{"Dry", "Wet"});
    m_gateTemperatureC = createSpinBox(700.0, 1200.0, 1000.0, " C");
    m_gateTimeMin = createSpinBox(1.0, 240.0, 30.0, " min");
    m_gateOxideNm = createSpinBox(0.0, 500.0, 100.0, " nm");
    m_polyThicknessNm = createSpinBox(50.0, 1000.0, 300.0, " nm");
    form->addRow("Oxidation mode", m_oxidationModeBox);
    form->addRow("Temperature", m_gateTemperatureC);
    form->addRow("Oxidation time", m_gateTimeMin);
    form->addRow("Target oxide", m_gateOxideNm);
    form->addRow("Poly thickness", m_polyThicknessNm);
    return page;
}

QWidget *ProcessPanel::createSourceDrainPage()
{
    auto *page = new QWidget(this);
    auto *form = new QFormLayout(page);
    m_sourceDrainDose = createSpinBox(1.0e12, 1.0e16, 1.0e15, " ions/cm2");
    m_sourceDrainDose->setDecimals(0);
    form->addRow("S/D implant dose", m_sourceDrainDose);
    return page;
}

QWidget *ProcessPanel::createContactPage()
{
    auto *page = new QWidget(this);
    auto *form = new QFormLayout(page);
    m_etchChemistryBox = new QComboBox(page);
    m_etchChemistryBox->addItems(QStringList{"CHF3 / CF4 plasma", "Buffered HF", "Cl2 / BCl3 plasma"});
    m_contactFillBox = new QComboBox(page);
    m_contactFillBox->addItems(QStringList{"Tungsten CVD", "Copper electrofill", "Aluminum sputter"});
    m_contactEtchDepthNm = createSpinBox(10.0, 1000.0, 250.0, " nm");
    m_contactFillThicknessNm = createSpinBox(50.0, 1500.0, 500.0, " nm");
    form->addRow("Etch chemistry", m_etchChemistryBox);
    form->addRow("Etch depth", m_contactEtchDepthNm);
    form->addRow("Fill material", m_contactFillBox);
    form->addRow("Fill thickness", m_contactFillThicknessNm);
    return page;
}

QWidget *ProcessPanel::createMetalPage()
{
    auto *page = new QWidget(this);
    auto *form = new QFormLayout(page);
    m_metalMaterialBox = new QComboBox(page);
    m_metalMaterialBox->addItems(QStringList{"Aluminum", "Copper", "Tungsten"});
    m_metalThicknessNm = createSpinBox(50.0, 2000.0, 600.0, " nm");
    form->addRow("Metal material", m_metalMaterialBox);
    form->addRow("Metal thickness", m_metalThicknessNm);
    return page;
}

QWidget *ProcessPanel::createCompletePage()
{
    auto *page = new QWidget(this);
    auto *form = new QVBoxLayout(page);
    auto *label = new QLabel("Use reset to run the process flow again.", page);
    label->setWordWrap(true);
    form->addWidget(label);
    return page;
}

QVariantMap ProcessPanel::currentParameters() const
{
    QVariantMap parameters;
    parameters["orientation"] = m_orientationBox->currentText();
    parameters["substrateThicknessUm"] = m_substrateThicknessUm->value();
    parameters["wellDopant"] = m_wellDopantBox->currentText();
    parameters["wellDose"] = m_wellDose->value();
    parameters["maskPattern"] = m_maskPatternBox->currentText();
    parameters["developer"] = m_developerBox->currentText();
    parameters["photoThicknessNm"] = m_photoThicknessNm->value();
    parameters["oxidationMode"] = m_oxidationModeBox->currentText();
    parameters["gateTemperatureC"] = m_gateTemperatureC->value();
    parameters["gateTimeMin"] = m_gateTimeMin->value();
    parameters["gateOxideNm"] = m_gateOxideNm->value();
    parameters["polyThicknessNm"] = m_polyThicknessNm->value();
    parameters["sourceDrainDose"] = m_sourceDrainDose->value();
    parameters["etchChemistry"] = m_etchChemistryBox->currentText();
    parameters["contactEtchDepthNm"] = m_contactEtchDepthNm->value();
    parameters["contactFill"] = m_contactFillBox->currentText();
    parameters["contactFillThicknessNm"] = m_contactFillThicknessNm->value();
    parameters["metalMaterial"] = m_metalMaterialBox->currentText();
    parameters["metalThicknessNm"] = m_metalThicknessNm->value();
    return parameters;
}
