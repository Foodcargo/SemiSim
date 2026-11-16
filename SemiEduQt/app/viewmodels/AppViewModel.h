#pragma once

#include <QJsonArray>
#include <QJsonObject>
#include <QObject>
#include <QUrl>
#include <QtQml/qqmlregistration.h>

#include "ListModels.h"

class EngineClient;
class ProcessEditorViewModel;
class ResultViewModel;
class SceneViewModel;
class PlotViewModel;

// Process list, educational equipment state, history and Undo. Runs se_run on a worker thread (QtConcurrent)
// and pushes the result into the other view models on the GUI thread.
class AppViewModel : public QObject
{
    Q_OBJECT
    QML_ELEMENT
    QML_UNCREATABLE("created in main.cpp")
    Q_PROPERTY(JsonListModel *processes READ processes CONSTANT)
    Q_PROPERTY(QString currentProcessId READ currentProcessId NOTIFY currentProcessIdChanged)
    Q_PROPERTY(bool doorClosed READ doorClosed WRITE setDoorClosed NOTIFY equipmentChanged)
    Q_PROPERTY(bool emergencyStop READ emergencyStop WRITE setEmergencyStop NOTIFY equipmentChanged)
    Q_PROPERTY(bool available READ available WRITE setAvailable NOTIFY equipmentChanged)
    Q_PROPERTY(bool continueOnFailure READ continueOnFailure WRITE setContinueOnFailure NOTIFY equipmentChanged)
    Q_PROPERTY(bool canUndo READ canUndo NOTIFY stateChanged)
    Q_PROPERTY(bool lastRunFailed READ lastRunFailed NOTIFY stateChanged)
    Q_PROPERTY(bool busy READ busy NOTIFY busyChanged)
    Q_PROPERTY(bool engineReady READ engineReady CONSTANT)
    Q_PROPERTY(QString bannerText READ bannerText CONSTANT)
    Q_PROPERTY(JsonListModel *history READ history CONSTANT)
    Q_PROPERTY(JsonListModel *layers READ layers CONSTANT)
    Q_PROPERTY(int maskCount READ maskCount NOTIFY stateChanged)
    Q_PROPERTY(bool hasLastRun READ hasLastRun NOTIFY stateChanged)
    Q_PROPERTY(int historyCount READ historyCount NOTIFY stateChanged)
public:
    AppViewModel(EngineClient *engine, ProcessEditorViewModel *editor, ResultViewModel *result, SceneViewModel *scene,
                 PlotViewModel *plot, QObject *parent = nullptr);

    JsonListModel *processes() const { return m_processes; }
    QString currentProcessId() const { return m_current; }
    bool doorClosed() const { return m_door; }
    bool emergencyStop() const { return m_estop; }
    bool available() const { return m_available; }
    bool continueOnFailure() const { return m_continue; }
    bool canUndo() const { return m_canUndo; }
    bool lastRunFailed() const { return m_lastFailed; }
    bool busy() const { return m_busy; }
    bool engineReady() const { return m_ready; }
    QString bannerText() const { return m_banner; }
    JsonListModel *history() const { return m_history; }
    JsonListModel *layers() const { return m_layers; }
    int maskCount() const { return m_maskCount; }
    bool hasLastRun() const { return !m_lastRun.isEmpty(); }
    int historyCount() const { return m_history->count(); }

    void setDoorClosed(bool v);
    void setEmergencyStop(bool v);
    void setAvailable(bool v);
    void setContinueOnFailure(bool v);

    Q_INVOKABLE void selectProcess(const QString &processId);
    Q_INVOKABLE void undo();
    Q_INVOKABLE void cancel();

    // File / Manage menu commands. Each returns a Korean message for the notice dialog.
    Q_INVOKABLE QString newSession();
    Q_INVOKABLE QString runScript(const QUrl &file);
    QString runScriptFile(const QString &path);
    Q_INVOKABLE QString exportResultJson(const QUrl &file);
    Q_INVOKABLE QString exportHistoryCsv(const QUrl &file);
    Q_INVOKABLE QString runSelfTest();
    Q_INVOKABLE QString engineInfo() const;
    Q_INVOKABLE QString toLocalFile(const QUrl &url) const { return url.toLocalFile(); }
    Q_INVOKABLE QUrl scriptsFolder() const;

public slots:
    void execute(bool commit);

signals:
    void currentProcessIdChanged();
    void equipmentChanged();
    void stateChanged();
    void busyChanged();

private:
    QJsonObject equipmentJson() const;
    void pushEquipment();
    void refreshState();
    void onRunFinished(const QJsonObject &result, bool commit);
    void setBusy(bool b);

    EngineClient *m_engine;
    ProcessEditorViewModel *m_editor;
    ResultViewModel *m_result;
    SceneViewModel *m_scene;
    PlotViewModel *m_plot;
    JsonListModel *m_processes;
    JsonListModel *m_history;
    JsonListModel *m_layers;
    int m_maskCount = 0;
    QJsonObject m_lastRun;
    QJsonArray m_historyRaw;
    void afterExternalChange(const QString &select);
    QString m_current, m_banner;
    qint64 m_revision = 0;
    bool m_door = true, m_estop = false, m_available = true, m_continue = false;
    bool m_canUndo = false, m_lastFailed = false, m_busy = false, m_ready = false;
};
