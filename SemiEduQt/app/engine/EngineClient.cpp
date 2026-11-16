#include "EngineClient.h"

#include <QCoreApplication>
#include <QDir>
#include <QFileInfo>
#include <QJsonDocument>

namespace {
template <class F>
bool resolve(QLibrary &lib, const char *name, F &out)
{
    out = reinterpret_cast<F>(lib.resolve(name));
    return out != nullptr;
}

QByteArray compact(const QJsonObject &o)
{
    return QJsonDocument(o).toJson(QJsonDocument::Compact);
}
} // namespace

EngineClient::~EngineClient()
{
    if (m_h && m_destroy) m_destroy(m_h);
}

bool EngineClient::load()
{
    QStringList dirs;
    const QString env = qEnvironmentVariable("SEMIEDU_ENGINE_DIR");
    if (!env.isEmpty()) dirs << env;
    dirs << QCoreApplication::applicationDirPath();
    for (const QString &d : dirs) {
        const QString p = QDir(d).filePath(QStringLiteral("SemiEdu.Engine.dll"));
        if (QFileInfo::exists(p)) {
            m_lib.setFileName(p);
            break;
        }
    }
    if (m_lib.fileName().isEmpty()) {
        m_error = QStringLiteral("엔진 DLL(SemiEdu.Engine.dll)을 찾지 못했습니다. 찾은 위치: %1").arg(dirs.join(QStringLiteral("; ")));
        return false;
    }
    if (!m_lib.load()) {
        m_error = QStringLiteral("엔진 DLL을 불러오지 못했습니다: %1").arg(m_lib.errorString());
        return false;
    }
    const bool ok = resolve(m_lib, "se_version", m_version) && resolve(m_lib, "se_create", m_create)
                    && resolve(m_lib, "se_destroy", m_destroy) && resolve(m_lib, "se_catalog", m_catalog)
                    && resolve(m_lib, "se_form", m_form) && resolve(m_lib, "se_run", m_run)
                    && resolve(m_lib, "se_cancel", m_cancel) && resolve(m_lib, "se_undo", m_undo)
                    && resolve(m_lib, "se_state", m_state) && resolve(m_lib, "se_scene_meta", m_sceneMeta)
                    && resolve(m_lib, "se_scene_boxes", m_sceneBoxes) && resolve(m_lib, "se_objects", m_objects)
                    && resolve(m_lib, "se_selftest", m_selftest) && resolve(m_lib, "se_free", m_free);
    if (!ok) {
        m_error = QStringLiteral("엔진 DLL에 필요한 se_* 함수가 없습니다(ABI 불일치).");
        return false;
    }
    m_versionInfo = take(m_version());
    resolve(m_lib, "se_undo_to", m_undoTo); // Optional, backward-compatible ABI extension.
    const int abi = m_versionInfo.value(QStringLiteral("abiVersion")).toInt(-1);
    if (abi != 1) {
        m_error = QStringLiteral("엔진 ABI 버전 불일치: %1 (필요: 1)").arg(abi);
        return false;
    }
    m_h = m_create();
    if (!m_h) {
        m_error = QStringLiteral("엔진 세션을 만들지 못했습니다.");
        return false;
    }
    m_ready = true;
    return true;
}

bool EngineClient::resetSession()
{
    if (!m_ready) return false;
    m_destroy(m_h);
    m_h = m_create();
    m_ready = m_h != nullptr;
    if (!m_ready) m_error = QStringLiteral("엔진 세션을 다시 만들지 못했습니다.");
    return m_ready;
}

QJsonObject EngineClient::take(char *p)
{
    if (!p) {
        return QJsonObject{{QStringLiteral("ok"), false},
                           {QStringLiteral("error"), QJsonObject{{QStringLiteral("Code"), QStringLiteral("NULL_RESPONSE")},
                                                                 {QStringLiteral("MessageKo"), QStringLiteral("엔진 응답이 없습니다.")}}}};
    }
    const QByteArray bytes(p);
    m_free(p);
    QJsonParseError err{};
    const auto doc = QJsonDocument::fromJson(bytes, &err);
    if (err.error != QJsonParseError::NoError || !doc.isObject()) {
        return QJsonObject{{QStringLiteral("ok"), false},
                           {QStringLiteral("error"), QJsonObject{{QStringLiteral("Code"), QStringLiteral("BAD_JSON")},
                                                                 {QStringLiteral("MessageKo"), err.errorString()}}}};
    }
    return doc.object();
}

QJsonObject EngineClient::notReady() const
{
    return QJsonObject{{QStringLiteral("ok"), false},
                       {QStringLiteral("error"), QJsonObject{{QStringLiteral("Code"), QStringLiteral("ENGINE_NOT_READY")},
                                                             {QStringLiteral("MessageKo"), m_error}}}};
}

QJsonObject EngineClient::catalog()
{
    return m_ready ? take(m_catalog(m_h)) : notReady();
}

QJsonObject EngineClient::form(const QString &processId, const QJsonObject &values)
{
    if (!m_ready) return notReady();
    const QByteArray pid = processId.toUtf8(), v = compact(values);
    return take(m_form(m_h, pid.constData(), v.constData()));
}

QJsonObject EngineClient::run(const QString &processId, const QJsonObject &values, const QJsonObject &equipment, bool commit,
                              qint64 expectedRevision)
{
    if (!m_ready) return notReady();
    const QByteArray pid = processId.toUtf8(), v = compact(values), e = compact(equipment);
    return take(m_run(m_h, pid.constData(), v.constData(), e.constData(), commit ? 1 : 0, static_cast<long long>(expectedRevision)));
}

void EngineClient::cancel()
{
    if (m_ready) m_cancel(m_h);
}

QJsonObject EngineClient::undo()
{
    return m_ready ? take(m_undo(m_h)) : notReady();
}

QJsonObject EngineClient::undoTo(const QString &runId, qint64 expectedUndoRevision)
{
    if (!m_ready) return notReady();
    if (!m_undoTo) return QJsonObject{{QStringLiteral("ok"), false}};
    const QByteArray id = runId.toUtf8();
    return take(m_undoTo(m_h, id.constData(), expectedUndoRevision));
}

QJsonObject EngineClient::state()
{
    return m_ready ? take(m_state(m_h)) : notReady();
}

QJsonObject EngineClient::sceneMeta(const QJsonObject &options)
{
    if (!m_ready) return notReady();
    const QByteArray o = compact(options);
    return take(m_sceneMeta(m_h, o.constData()));
}

QVector<float> EngineClient::sceneBoxes(const QJsonObject &options)
{
    if (!m_ready) return {};
    const QByteArray o = compact(options);
    const int n = m_sceneBoxes(m_h, o.constData(), nullptr, 0);
    if (n <= 0) return {};
    QVector<float> buf(n * 11);
    if (m_sceneBoxes(m_h, o.constData(), buf.data(), n) != n) return {};
    return buf;
}

QJsonObject EngineClient::objects()
{
    return m_ready ? take(m_objects(m_h)) : notReady();
}

QJsonObject EngineClient::selftest()
{
    if (!m_selftest || !m_free) return notReady();
    return take(m_selftest());
}
