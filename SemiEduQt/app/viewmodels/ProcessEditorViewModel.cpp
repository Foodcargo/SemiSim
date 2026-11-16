#include "ProcessEditorViewModel.h"

#include <QJsonArray>

#include "engine/EngineClient.h"

ProcessEditorViewModel::ProcessEditorViewModel(EngineClient *engine, QObject *parent)
    : QObject(parent), m_engine(engine), m_fields(new ParamListModel(this)), m_quality(new ParamListModel(this))
{
    m_debounce.setSingleShot(true);
    m_debounce.setInterval(150);
    connect(&m_debounce, &QTimer::timeout, this, &ProcessEditorViewModel::refreshNow);
}

void ProcessEditorViewModel::setProcess(const QString &processId)
{
    m_pid = processId;
    m_fields->clear();
    m_quality->clear();
    refreshNow();
}

void ProcessEditorViewModel::setEquipment(const QJsonObject &equipment)
{
    m_equipment = equipment;
    if (!m_busy) refreshNow();
}

void ProcessEditorViewModel::setBusy(bool busy)
{
    if (busy == m_busy) return;
    m_busy = busy;
    if (busy) m_debounce.stop();
    emit busyChanged();
    emit formChanged();
}

void ProcessEditorViewModel::setStale(bool stale)
{
    if (stale == m_stale) return;
    m_stale = stale;
    emit staleResultChanged();
}

void ProcessEditorViewModel::refreshNow()
{
    m_debounce.stop();
    if (m_busy || !m_engine->ready() || m_pid.isEmpty()) return;
    QJsonObject values;
    const auto cache = m_cache.value(m_pid);
    for (auto it = cache.cbegin(); it != cache.cend(); ++it) values.insert(it.key(), it.value());
    values.insert(QStringLiteral("$equipment"), m_equipment);
    const QJsonObject f = m_engine->form(m_pid, values);
    if (!f.value(QStringLiteral("ok")).toBool()) {
        m_precheck = f.value(QStringLiteral("error")).toObject().value(QStringLiteral("MessageKo")).toString();
        m_precheckOk = false;
        m_canPreview = m_canRun = false;
        emit formChanged();
        return;
    }
    m_title = f.value(QStringLiteral("title")).toString();
    m_fields->update(f.value(QStringLiteral("fields")).toArray());
    m_quality->update(f.value(QStringLiteral("qualityFields")).toArray());
    m_precheck = f.value(QStringLiteral("precheckText")).toString();
    m_precheckOk = f.value(QStringLiteral("precheckErrors")).toArray().isEmpty() && f.value(QStringLiteral("canRun")).toBool();
    QStringList effects;
    for (const auto &v : f.value(QStringLiteral("unsupportedEffects")).toArray()) effects << QStringLiteral("• ") + v.toString();
    m_unsupported = effects.join(QLatin1Char('\n'));
    m_canPreview = f.value(QStringLiteral("canPreview")).toBool();
    m_canRun = f.value(QStringLiteral("canRun")).toBool();
    m_active.clear();
    for (const auto &v : f.value(QStringLiteral("activeParams")).toArray()) m_active << v.toString();
    emit formChanged();
}

QJsonObject ProcessEditorViewModel::runValues() const
{
    // Only fields that are active in the latest form evaluation go into the recipe.
    QJsonObject o;
    const auto cache = m_cache.value(m_pid);
    for (auto it = cache.cbegin(); it != cache.cend(); ++it)
        if (m_active.contains(it.key())) o.insert(it.key(), it.value());
    return o;
}

void ProcessEditorViewModel::setText(const QString &paramId, const QString &text)
{
    if (m_busy) return;
    m_cache[m_pid][paramId] = text;
    setStale(true);
    m_debounce.start();
}

void ProcessEditorViewModel::setOption(const QString &paramId, int index)
{
    if (m_busy) return;
    QStringList opts = m_fields->contains(paramId) ? m_fields->optionsOf(paramId) : m_quality->optionsOf(paramId);
    if (index < 0 || index >= opts.size()) return;
    const QString v = opts[index];
    if (v.startsWith(QStringLiteral("—"))) return; // placeholder ("— 다시 선택 —")
    m_cache[m_pid][paramId] = v;
    setStale(true);
    refreshNow();
}

void ProcessEditorViewModel::resetInputs()
{
    if (m_busy) return;
    m_cache.remove(m_pid);
    setStale(true);
    m_fields->clear();
    m_quality->clear();
    refreshNow();
}

void ProcessEditorViewModel::preview()
{
    refreshNow();
    if (canPreview()) emit runRequested(false);
}

void ProcessEditorViewModel::run()
{
    refreshNow();
    if (canRun()) emit runRequested(true);
}
