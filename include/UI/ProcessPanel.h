#pragma once

#include <QVariantMap>
#include <QWidget>

class QLabel;
class QString;
class QStackedWidget;
class QComboBox;
class QDoubleSpinBox;

class ProcessPanel : public QWidget {
    Q_OBJECT

public:
    explicit ProcessPanel(QWidget *parent = nullptr);

public slots:
    void setCurrentStep(int step);
    void showMessage(const QString &message);

signals:
    void processSubmitted(int step, QVariantMap parameters);
    void resetRequested();

private:
    QDoubleSpinBox *createSpinBox(double minimum, double maximum, double value, const QString &suffix);
    QWidget *createSubstratePage();
    QWidget *createWellPage();
    QWidget *createActiveMaskPage();
    QWidget *createGatePage();
    QWidget *createSourceDrainPage();
    QWidget *createContactPage();
    QWidget *createMetalPage();
    QWidget *createCompletePage();
    QVariantMap currentParameters() const;

    int m_currentStep = 0;
    QLabel *m_stepTitle = nullptr;
    QLabel *m_stepHint = nullptr;
    QLabel *m_statusLabel = nullptr;
    QStackedWidget *m_processInputs = nullptr;
    QComboBox *m_orientationBox = nullptr;
    QComboBox *m_oxidationModeBox = nullptr;
    QComboBox *m_wellDopantBox = nullptr;
    QComboBox *m_maskPatternBox = nullptr;
    QComboBox *m_developerBox = nullptr;
    QComboBox *m_etchChemistryBox = nullptr;
    QComboBox *m_contactFillBox = nullptr;
    QComboBox *m_metalMaterialBox = nullptr;
    QDoubleSpinBox *m_substrateThicknessUm = nullptr;
    QDoubleSpinBox *m_wellDose = nullptr;
    QDoubleSpinBox *m_photoThicknessNm = nullptr;
    QDoubleSpinBox *m_gateTemperatureC = nullptr;
    QDoubleSpinBox *m_gateTimeMin = nullptr;
    QDoubleSpinBox *m_gateOxideNm = nullptr;
    QDoubleSpinBox *m_polyThicknessNm = nullptr;
    QDoubleSpinBox *m_sourceDrainDose = nullptr;
    QDoubleSpinBox *m_contactEtchDepthNm = nullptr;
    QDoubleSpinBox *m_contactFillThicknessNm = nullptr;
    QDoubleSpinBox *m_metalThicknessNm = nullptr;
};
