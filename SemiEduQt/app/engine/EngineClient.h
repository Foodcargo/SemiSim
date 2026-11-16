#pragma once

#include <QJsonObject>
#include <QLibrary>
#include <QString>
#include <QVector>

#include "semiedu_engine.h"

// Loads SemiEdu.Engine.dll (C# NativeAOT) with QLibrary and wraps the se_* C ABI.
// run() is called from a worker thread; the engine serializes calls per session handle.
class EngineClient
{
public:
    EngineClient() = default;
    ~EngineClient();
    EngineClient(const EngineClient &) = delete;
    EngineClient &operator=(const EngineClient &) = delete;

    bool load();
    // Destroys the current session and starts a fresh one (default BareSi, empty history and MaskLibrary).
    bool resetSession();
    bool ready() const { return m_ready; }
    QString error() const { return m_error; }
    QString libraryPath() const { return m_lib.fileName(); }
    QJsonObject versionInfo() const { return m_versionInfo; }

    QJsonObject catalog();
    QJsonObject form(const QString &processId, const QJsonObject &values);
    QJsonObject run(const QString &processId, const QJsonObject &values, const QJsonObject &equipment, bool commit, qint64 expectedRevision);
    void cancel();
    QJsonObject undo();
    QJsonObject state();
    QJsonObject sceneMeta(const QJsonObject &options);
    QVector<float> sceneBoxes(const QJsonObject &options);
    QJsonObject objects();
    QJsonObject selftest();

private:
    QJsonObject take(char *p);
    QJsonObject notReady() const;

    QLibrary m_lib;
    se_handle m_h = nullptr;
    bool m_ready = false;
    QString m_error;
    QJsonObject m_versionInfo;

    se_version_fn m_version = nullptr;
    se_create_fn m_create = nullptr;
    se_destroy_fn m_destroy = nullptr;
    se_catalog_fn m_catalog = nullptr;
    se_form_fn m_form = nullptr;
    se_run_fn m_run = nullptr;
    se_cancel_fn m_cancel = nullptr;
    se_undo_fn m_undo = nullptr;
    se_state_fn m_state = nullptr;
    se_scene_meta_fn m_sceneMeta = nullptr;
    se_scene_boxes_fn m_sceneBoxes = nullptr;
    se_objects_fn m_objects = nullptr;
    se_selftest_fn m_selftest = nullptr;
    se_free_fn m_free = nullptr;
};
