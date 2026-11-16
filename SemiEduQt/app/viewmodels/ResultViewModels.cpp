#include "ResultViewModels.h"

#include <QJsonArray>

#include "engine/EngineClient.h"

ResultViewModel::ResultViewModel(QObject *parent)
    : QObject(parent), m_stages(new JsonListModel(this)), m_metrics(new JsonListModel(this)), m_errors(new JsonListModel(this)),
      m_objects(new JsonListModel(this))
{
    m_stages->setRoles({"name", "verdict"});
    m_metrics->setRoles({"name", "value", "target", "range", "pass", "note"});
    m_errors->setRoles({"stage", "code", "message", "action", "actual", "expected"});
    m_objects->setRoles({"objectId", "material", "cls", "reason", "range", "net"});
}

void ResultViewModel::setFromRun(const QJsonObject &run)
{
    m_has = true;
    if (!run.value(QStringLiteral("ok")).toBool()) {
        m_kind = QStringLiteral("error");
        m_text = QStringLiteral("[오류] ") + run.value(QStringLiteral("error")).toObject().value(QStringLiteral("MessageKo")).toString();
        m_process.clear();
        m_preview = false;
        m_stages->setItems({});
        m_metrics->setItems({});
        m_errors->setItems({});
        m_verdict.clear();
        m_extracted.clear();
        m_warnings.clear();
        emit resultChanged();
        return;
    }
    m_kind = run.value(QStringLiteral("statusKind")).toString();
    m_text = run.value(QStringLiteral("statusText")).toString();
    m_process = run.value(QStringLiteral("processName")).toString();
    m_preview = run.value(QStringLiteral("isPreview")).toBool();
    m_stages->setItems(run.value(QStringLiteral("stages")).toArray());
    m_metrics->setItems(run.value(QStringLiteral("metrics")).toArray());
    QVector<QVariantMap> errs;
    for (const auto &v : run.value(QStringLiteral("errors")).toArray()) {
        const auto e = v.toObject();
        errs.push_back({{QStringLiteral("stage"), e.value(QStringLiteral("Stage")).toString()},
                        {QStringLiteral("code"), e.value(QStringLiteral("Code")).toString()},
                        {QStringLiteral("message"), e.value(QStringLiteral("MessageKo")).toString()},
                        {QStringLiteral("action"), e.value(QStringLiteral("SuggestedAction")).toString()},
                        {QStringLiteral("actual"), e.value(QStringLiteral("Actual")).toString()},
                        {QStringLiteral("expected"), e.value(QStringLiteral("Expected")).toString()}});
    }
    m_errors->setMaps(errs);
    QStringList w;
    for (const auto &v : run.value(QStringLiteral("warnings")).toArray()) w << QStringLiteral("• ") + v.toString();
    m_warnings = w.join(QLatin1Char('\n'));
    const auto el = run.value(QStringLiteral("electrical")).toObject();
    m_verdict = el.value(QStringLiteral("verdict")).toString();
    m_normal = el.value(QStringLiteral("normal")).toBool();
    QStringList ex;
    for (const auto &v : el.value(QStringLiteral("extracted")).toArray()) {
        const auto o = v.toObject();
        ex << o.value(QStringLiteral("name")).toString() + QStringLiteral(": ") + o.value(QStringLiteral("value")).toString();
    }
    const QString region = el.value(QStringLiteral("region")).toString();
    if (!region.isEmpty()) ex.prepend(QStringLiteral("동작 영역: ") + region);
    m_extracted = ex.join(QStringLiteral("   ·   "));
    emit resultChanged();
}

void ResultViewModel::setObjects(const QJsonObject &objects)
{
    m_objects->setItems(objects.value(QStringLiteral("objects")).toArray());
    QStringList nets;
    for (const auto &v : objects.value(QStringLiteral("nets")).toArray()) {
        const auto n = v.toObject();
        const QString kind = n.value(QStringLiteral("kind")).toString();
        if (kind == QStringLiteral("p-body") || kind == QStringLiteral("n-body") || kind == QStringLiteral("Metal") || kind.endsWith('+'))
            nets << QStringLiteral("%1 %2 (열 %3–%4, 셀 %5)%6")
                        .arg(n.value(QStringLiteral("netId")).toString(), kind)
                        .arg(n.value(QStringLiteral("colMin")).toInt())
                        .arg(n.value(QStringLiteral("colMax")).toInt())
                        .arg(n.value(QStringLiteral("cells")).toInt())
                        .arg(n.value(QStringLiteral("objects")).toString().isEmpty() ? QString() : QStringLiteral(" ← ") + n.value(QStringLiteral("objects")).toString());
    }
    m_nets = QStringLiteral("도체 네트 %1개 · ").arg(objects.value(QStringLiteral("conductorNets")).toInt()) + nets.join(QStringLiteral(" | "));
    emit objectsChanged();
}

SceneViewModel::SceneViewModel(EngineClient *engine, QObject *parent) : QObject(parent), m_engine(engine) {}

void SceneViewModel::refresh(bool preview)
{
    m_lastPreview = preview;
    const QJsonObject opts{{QStringLiteral("preview"), preview},
                           {QStringLiteral("classificationView"), m_cls},
                           {QStringLiteral("view"), m_view}};
    const QJsonObject meta = m_engine->sceneMeta(opts);
    m_boxes = m_engine->sceneBoxes(opts);
    m_legend = meta.value(QStringLiteral("legendHtml")).toString();
    m_scale = meta.value(QStringLiteral("scaleText")).toString();
    m_terminals = meta.value(QStringLiteral("terminals")).toString();
    m_isPreview = meta.value(QStringLiteral("isPreview")).toBool();
    m_view = meta.value(QStringLiteral("viewKind")).toString(m_view);
    ++m_rev;
    emit sceneChanged();
}

void SceneViewModel::setClassificationView(bool on)
{
    if (on == m_cls) return;
    m_cls = on;
    emit classificationViewChanged();
    refresh(m_lastPreview);
}

void PlotViewModel::setFromElectrical(const QJsonObject &el)
{
    m_series.clear();
    m_points.clear();
    const auto sweep = el.value(QStringLiteral("sweep")).toArray();
    for (const auto &v : sweep) {
        const auto p = v.toArray();
        const QPointF pt(p.at(0).toDouble(), p.at(1).toDouble());
        m_points.push_back(pt);
        m_series.push_back(pt);
    }
    m_visible = !m_points.isEmpty();
    const QString kind = el.value(QStringLiteral("sweepKind")).toString();
    m_title = kind == QStringLiteral("IdVgSweep") ? QStringLiteral("Id–Vg (로그 Y)") : kind == QStringLiteral("IdVdSweep") ? QStringLiteral("Id–Vd") : QString();
    m_x = el.value(QStringLiteral("xLabel")).toString();
    m_y = el.value(QStringLiteral("yLabel")).toString();
    m_logY = el.value(QStringLiteral("logY")).toBool();
    const auto vth = el.value(QStringLiteral("vthExt"));
    m_hasVth = vth.isDouble();
    m_vth = vth.toDouble();
    emit plotChanged();
}
