#pragma once

#include <QHash>
#include <QJsonObject>
#include <QObject>
#include <QTimer>
#include <QtQml/qqmlregistration.h>

#include "ListModels.h"

class EngineClient;

// Input form for the selected process. Every edit goes to a per-process cache and is re-evaluated by se_form
// (debounced 150 ms). No parsing or range checks happen here; the engine is the single validator.
class ProcessEditorViewModel : public QObject
{
    Q_OBJECT
    QML_ELEMENT
    QML_UNCREATABLE("created in main.cpp")
    Q_PROPERTY(QString processId READ processId NOTIFY formChanged)
    Q_PROPERTY(QString title READ title NOTIFY formChanged)
    Q_PROPERTY(ParamListModel *fields READ fields CONSTANT)
    Q_PROPERTY(ParamListModel *qualityFields READ qualityFields CONSTANT)
    Q_PROPERTY(QString precheckText READ precheckText NOTIFY formChanged)
    Q_PROPERTY(bool precheckOk READ precheckOk NOTIFY formChanged)
    Q_PROPERTY(QString unsupportedEffects READ unsupportedEffects NOTIFY formChanged)
    Q_PROPERTY(bool canPreview READ canPreview NOTIFY formChanged)
    Q_PROPERTY(bool canRun READ canRun NOTIFY formChanged)
    Q_PROPERTY(bool staleResult READ staleResult NOTIFY staleResultChanged)
    Q_PROPERTY(bool busy READ busy NOTIFY busyChanged)
public:
    explicit ProcessEditorViewModel(EngineClient *engine, QObject *parent = nullptr);

    QString processId() const { return m_pid; }
    QString title() const { return m_title; }
    ParamListModel *fields() const { return m_fields; }
    ParamListModel *qualityFields() const { return m_quality; }
    QString precheckText() const { return m_precheck; }
    bool precheckOk() const { return m_precheckOk; }
    QString unsupportedEffects() const { return m_unsupported; }
    bool canPreview() const { return m_canPreview && !m_busy; }
    bool canRun() const { return m_canRun && !m_busy; }
    bool staleResult() const { return m_stale; }
    bool busy() const { return m_busy; }

    void setProcess(const QString &processId);
    void setEquipment(const QJsonObject &equipment);
    void setBusy(bool busy);
    void setStale(bool stale);
    void refreshNow();
    QJsonObject runValues() const;

    Q_INVOKABLE void setText(const QString &paramId, const QString &text);
    Q_INVOKABLE void setOption(const QString &paramId, int index);
    Q_INVOKABLE void resetInputs();
    void clearAll() { m_cache.clear(); }
    Q_INVOKABLE void preview();
    Q_INVOKABLE void run();

signals:
    void formChanged();
    void staleResultChanged();
    void busyChanged();
    void runRequested(bool commit);

private:
    EngineClient *m_engine;
    ParamListModel *m_fields;
    ParamListModel *m_quality;
    QTimer m_debounce;
    QHash<QString, QHash<QString, QString>> m_cache;
    QJsonObject m_equipment;
    QStringList m_active;
    QString m_pid, m_title, m_precheck, m_unsupported;
    bool m_precheckOk = false, m_canPreview = false, m_canRun = false, m_stale = false, m_busy = false;
};
