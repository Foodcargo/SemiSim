#pragma once

#include <QMainWindow>
#include <QStringList>
#include <QVariantMap>

class InfoPanel;
class ProcessPanel;
class Wafer;
class WaferScene;

class MainWindow : public QMainWindow {
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = nullptr);

private:
    void wireProcessActions();
    void applySubmittedProcess(int step, const QVariantMap &parameters);
    void resetProcess();

    Wafer *m_wafer = nullptr;
    WaferScene *m_scene = nullptr;
    ProcessPanel *m_processPanel = nullptr;
    InfoPanel *m_infoPanel = nullptr;
    QStringList m_processFailures;
};
