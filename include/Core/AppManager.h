#pragma once

#include <QObject>

class MainWindow;

class AppManager : public QObject {
    Q_OBJECT

public:
    explicit AppManager(QObject *parent = nullptr);
    void initialize();

private:
    MainWindow *m_mainWindow = nullptr;
};
