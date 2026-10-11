#include "Core/AppManager.h"

#include "UI/MainWindow.h"

AppManager::AppManager(QObject *parent)
    : QObject(parent)
{
}

void AppManager::initialize()
{
    m_mainWindow = new MainWindow();
    m_mainWindow->show();
}
