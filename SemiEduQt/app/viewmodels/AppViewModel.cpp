#include "AppViewModel.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QFutureWatcher>
#include <QJsonArray>
#include <QJsonDocument>
#include <QMap>
#include <QSaveFile>
#include <QTextStream>
#include <QtConcurrent/QtConcurrentRun>

#include "ProcessEditorViewModel.h"
#include "ResultViewModels.h"
#include "engine/EngineClient.h"

AppViewModel::AppViewModel(EngineClient *engine, ProcessEditorViewModel *editor, ResultViewModel *result, SceneViewModel *scene,
                           PlotViewModel *plot, QObject *parent)
    : QObject(parent), m_engine(engine), m_editor(editor), m_result(result), m_scene(scene), m_plot(plot),
      m_processes(new JsonListModel(this)), m_history(new JsonListModel(this)), m_layers(new JsonListModel(this))
{
    m_layers->setRoles({"layerId", "material", "materialKo", "color", "origin", "thicknessText", "coverage"});
    m_processes->setRoles({"group", "processId", "name", "enabled", "reason"});
    m_history->setRoles({"runId", "name", "statusText", "statusKind", "time", "physical", "undone", "inputDetails"});
    connect(m_editor, &ProcessEditorViewModel::runRequested, this, &AppViewModel::execute);
    m_ready = m_engine->ready();
    if (!m_ready) {
        m_banner = QStringLiteral("엔진을 사용할 수 없어 실행을 비활성화했습니다 — %1").arg(m_engine->error());
        return;
    }
    const auto v = m_engine->versionInfo();
    m_banner = QStringLiteral("SemiEdu.Engine %1 · ABI %2 · Model %3")
                   .arg(v.value(QStringLiteral("engineVersion")).toString())
                   .arg(v.value(QStringLiteral("abiVersion")).toInt())
                   .arg(v.value(QStringLiteral("modelVersion")).toString());
    m_processes->setItems(m_engine->catalog().value(QStringLiteral("processes")).toArray());
    pushEquipment();
    refreshState();
    selectProcess(QStringLiteral("init"));
    m_result->setObjects(m_engine->objects());
}

QJsonObject AppViewModel::equipmentJson() const
{
    return QJsonObject{{QStringLiteral("doorClosed"), m_door},
                       {QStringLiteral("emergencyStop"), m_estop},
                       {QStringLiteral("available"), m_available},
                       {QStringLiteral("continueOnFailure"), m_continue}};
}

void AppViewModel::pushEquipment()
{
    m_editor->setEquipment(equipmentJson());
}

void AppViewModel::setDoorClosed(bool v)
{
    if (v == m_door) return;
    m_door = v;
    emit equipmentChanged();
    pushEquipment();
}

void AppViewModel::setEmergencyStop(bool v)
{
    if (v == m_estop) return;
    m_estop = v;
    emit equipmentChanged();
    pushEquipment();
}

void AppViewModel::setAvailable(bool v)
{
    if (v == m_available) return;
    m_available = v;
    emit equipmentChanged();
    pushEquipment();
}

void AppViewModel::setContinueOnFailure(bool v)
{
    if (v == m_continue) return;
    m_continue = v;
    emit equipmentChanged();
    pushEquipment();
}

void AppViewModel::setBusy(bool b)
{
    if (b == m_busy) return;
    m_busy = b;
    m_editor->setBusy(b);
    emit busyChanged();
    emit undoSelectionChanged();
}

void AppViewModel::selectProcess(const QString &processId)
{
    if (m_busy || !m_ready) return;
    m_current = processId;
    emit currentProcessIdChanged();
    emit undoSelectionChanged();
    m_editor->setProcess(processId);
    m_scene->setViewKind(processId == QStringLiteral("mask") ? QStringLiteral("mask")
                         : processId == QStringLiteral("transistor") ? QStringLiteral("device") : QStringLiteral("wafer"));
    if (processId == QStringLiteral("transistor")) m_scene->setClassificationView(true);
    m_scene->refresh(false);
}

void AppViewModel::undo()
{
    if (m_busy || !m_ready) return;
    const auto response = m_engine->undo();
    if (!response.value(QStringLiteral("ok")).toBool()) return;
    m_lastRun = {};
    afterExternalChange(m_current);
}

int AppViewModel::selectedUndoIndex() const
{
    for (int i = int(m_activeRuns.size()) - 1; i >= 0; --i)
        if (m_activeRuns[i].toObject().value(QStringLiteral("processId")).toString() == m_current) return i;
    return -1;
}

bool AppViewModel::canUndoSelected() const
{
    return m_ready && !m_busy && m_engine->supportsUndoTo() && selectedUndoIndex() >= 0;
}

void AppViewModel::requestUndoSelected()
{
    if (!canUndoSelected()) return;
    const int index = selectedUndoIndex();
    const auto run = m_activeRuns[index].toObject();
    m_pendingUndoRun = run.value(QStringLiteral("runId")).toString();
    m_pendingUndoRevision = m_undoRevision;
    const int laterCount = int(m_activeRuns.size()) - index - 1;
    if (laterCount > 0) emit undoConfirmationRequested(run.value(QStringLiteral("name")).toString(), laterCount);
    else confirmUndoSelected();
}

void AppViewModel::confirmUndoSelected()
{
    if (m_busy || !m_ready || m_pendingUndoRun.isEmpty()) return;
    const auto response = m_engine->undoTo(m_pendingUndoRun, m_pendingUndoRevision);
    m_pendingUndoRun.clear();
    if (!response.value(QStringLiteral("ok")).toBool()) {
        refreshState();
        emit undoError(response.value(QStringLiteral("error")).toObject().value(QStringLiteral("MessageKo")).toString());
        return;
    }
    m_lastRun = {};
    afterExternalChange(m_current);
}

void AppViewModel::selectLayer(const QString &layerId)
{
    const QMap<QString, QString> names{
        {QStringLiteral("Si"), QStringLiteral("Silicon")},
        {QStringLiteral("SiO2"), QStringLiteral("Silicon Dioxide")},
        {QStringLiteral("Si3N4"), QStringLiteral("Silicon Nitride")},
        {QStringLiteral("PR"), QStringLiteral("Photoresist")},
        {QStringLiteral("Al"), QStringLiteral("Aluminum")},
        {QStringLiteral("Cu"), QStringLiteral("Copper")},
        {QStringLiteral("W"), QStringLiteral("Tungsten")},
        {QStringLiteral("TiN"), QStringLiteral("Titanium Nitride")},
    };
    QString selected, description;
    for (const auto &value : m_layersRaw) {
        const auto layer = value.toObject();
        if (layer.value(QStringLiteral("layerId")).toString() != layerId) continue;
        selected = layerId;
        const QString material = layer.value(QStringLiteral("material")).toString();
        description = QStringLiteral("%1 | Material: %2 (%3) | %4 | Coverage: %5")
            .arg(layerId, names.value(material, material), material,
                 layer.value(QStringLiteral("thicknessText")).toString(),
                 layer.value(QStringLiteral("coverage")).toString());
        if (layerId == QStringLiteral("SUB")) description += QStringLiteral(" | Silicon substrate; only the upper cross-section is shown.");
        else description += QStringLiteral(" | Created by: ") + layer.value(QStringLiteral("origin")).toString();
        break;
    }
    if (selected == m_selectedLayer && description == m_selectedLayerText) return;
    m_selectedLayer = selected; m_selectedLayerText = description;
    emit layerSelectionChanged();
}

void AppViewModel::cancel()
{
    if (m_busy) m_engine->cancel();
}

void AppViewModel::execute(bool commit)
{
    if (m_busy || !m_ready) return;
    const QString pid = m_current;
    const QJsonObject values = m_editor->runValues();
    const QJsonObject eq = equipmentJson();
    const qint64 rev = m_revision;
    setBusy(true);
    auto *watcher = new QFutureWatcher<QJsonObject>(this);
    connect(watcher, &QFutureWatcher<QJsonObject>::finished, this, [this, watcher, commit] {
        const QJsonObject res = watcher->result();
        watcher->deleteLater();
        onRunFinished(res, commit);
    });
    EngineClient *engine = m_engine;
    watcher->setFuture(QtConcurrent::run([engine, pid, values, eq, commit, rev] { return engine->run(pid, values, eq, commit, rev); }));
}

void AppViewModel::onRunFinished(const QJsonObject &result, bool commit)
{
    setBusy(false);
    m_lastRun = result;
    m_result->setFromRun(result);
    m_editor->setStale(false);
    m_plot->setFromElectrical(result.value(QStringLiteral("electrical")).toObject());
    refreshState();
    m_scene->refresh(!commit);
    m_result->setObjects(m_engine->objects());
    m_editor->refreshNow();
}

void AppViewModel::refreshState()
{
    const QJsonObject st = m_engine->state();
    m_revision = st.value(QStringLiteral("revision")).toInteger();
    m_undoRevision = st.value(QStringLiteral("undoRevision")).toInteger();
    m_activeRuns = st.value(QStringLiteral("activeRuns")).toArray();
    m_canUndo = st.value(QStringLiteral("canUndo")).toBool();
    m_lastFailed = st.value(QStringLiteral("lastRunFailed")).toBool();
    QJsonArray hist = st.value(QStringLiteral("history")).toArray();
    m_historyRaw = hist;
    QJsonArray newestFirst;
    for (qsizetype i = hist.size() - 1; i >= 0; --i) newestFirst.append(hist.at(i));
    m_history->setItems(newestFirst);
    QJsonArray layers;
    layers.append(QJsonObject{{QStringLiteral("layerId"), QStringLiteral("SUB")},
                              {QStringLiteral("material"), QStringLiteral("Si")},
                              {QStringLiteral("materialKo"), QStringLiteral("실리콘 기판")},
                              {QStringLiteral("color"), QStringLiteral("#848ea3")},
                              {QStringLiteral("origin"), QStringLiteral("init")},
                              {QStringLiteral("thicknessText"), QStringLiteral("상부만 표시")},
                              {QStringLiteral("coverage"), QStringLiteral("전체")}});
    for (const auto &v : st.value(QStringLiteral("layers")).toArray()) layers.append(v);
    m_layersRaw = layers;
    m_layers->setItems(layers);
    selectLayer(m_selectedLayer); // Clear removed layers; refresh descriptions for changed thicknesses.
    m_maskCount = int(st.value(QStringLiteral("masks")).toArray().size());
    emit stateChanged();
    emit undoSelectionChanged();
}

// ---------------- menu commands ----------------

void AppViewModel::afterExternalChange(const QString &select)
{
    refreshState();
    selectProcess(select.isEmpty() ? QStringLiteral("init") : select);
    m_result->setObjects(m_engine->objects());
    if (!m_lastRun.isEmpty()) {
        m_result->setFromRun(m_lastRun);
        m_plot->setFromElectrical(m_lastRun.value(QStringLiteral("electrical")).toObject());
    } else {
        m_result->setFromRun(QJsonObject{});
        m_plot->setFromElectrical(QJsonObject{});
    }
    m_scene->refresh(false);
}

QString AppViewModel::newSession()
{
    if (m_busy || !m_ready) return QStringLiteral("계산 중에는 새 세션을 만들 수 없습니다.");
    if (!m_engine->resetSession()) return m_engine->error();
    m_editor->clearAll();
    m_lastRun = QJsonObject{};
    afterExternalChange(QStringLiteral("init"));
    return QStringLiteral("새 세션을 시작했습니다. 웨이퍼 상태·실행 이력·마스크 라이브러리가 초기화되었습니다.");
}

QString AppViewModel::runScript(const QUrl &file)
{
    return runScriptFile(file.toLocalFile());
}

// Replays a learning-path script through the engine on the current session.
// Format: {"steps":[{"processId":..., "values":{...}, "continueOnFailure":bool}], "select":"processId"}
QString AppViewModel::runScriptFile(const QString &path)
{
    if (m_busy || !m_ready) return QStringLiteral("계산 중에는 스크립트를 실행할 수 없습니다.");
    QFile f(path);
    if (!f.open(QIODevice::ReadOnly)) return QStringLiteral("파일을 열 수 없습니다: ") + path;
    QJsonParseError err{};
    const QJsonObject doc = QJsonDocument::fromJson(f.readAll(), &err).object();
    if (err.error != QJsonParseError::NoError) return QStringLiteral("JSON 형식 오류: ") + err.errorString();
    const QJsonArray steps = doc.value(QStringLiteral("steps")).toArray();
    if (steps.isEmpty()) return QStringLiteral("스크립트에 steps가 없습니다.");
    QStringList log;
    int ok = 0;
    for (const auto &v : steps) {
        const QJsonObject step = v.toObject();
        QJsonObject eq = equipmentJson();
        if (step.contains(QStringLiteral("continueOnFailure")))
            eq.insert(QStringLiteral("continueOnFailure"), step.value(QStringLiteral("continueOnFailure")).toBool());
        const QString pid = step.value(QStringLiteral("processId")).toString();
        m_lastRun = m_engine->run(pid, step.value(QStringLiteral("values")).toObject(), eq, true, -1);
        const QString status = m_lastRun.value(QStringLiteral("ok")).toBool()
                                   ? m_lastRun.value(QStringLiteral("statusText")).toString()
                                   : QStringLiteral("[오류] ") + m_lastRun.value(QStringLiteral("error")).toObject().value(QStringLiteral("MessageKo")).toString();
        if (m_lastRun.value(QStringLiteral("statusKind")).toString() == QStringLiteral("success")) ++ok;
        log << QStringLiteral("%1  %2").arg(pid, -12).arg(status);
    }
    afterExternalChange(doc.value(QStringLiteral("select")).toString());
    const QString name = QFileInfo(path).fileName();
    return QStringLiteral("%1 실행: %2/%3 단계 성공\n%4\n\n%5")
        .arg(name).arg(ok).arg(steps.size())
        .arg(doc.value(QStringLiteral("description")).toString(), log.join(QLatin1Char('\n')));
}

QString AppViewModel::exportResultJson(const QUrl &file)
{
    if (m_lastRun.isEmpty()) return QStringLiteral("내보낼 실행 결과가 없습니다.");
    QSaveFile f(file.toLocalFile());
    if (!f.open(QIODevice::WriteOnly)) return QStringLiteral("파일을 쓸 수 없습니다: ") + file.toLocalFile();
    QJsonObject out = m_lastRun;
    out.insert(QStringLiteral("exportNote"), QStringLiteral("검증되지 않은 교육용 예시 모델 결과입니다. 실제 제조·장비 제어·수율 예측용이 아닙니다."));
    f.write(QJsonDocument(out).toJson(QJsonDocument::Indented));
    if (!f.commit()) return QStringLiteral("저장에 실패했습니다.");
    return QStringLiteral("마지막 실행 결과를 저장했습니다: ") + file.toLocalFile();
}

static QString csvCell(QString s)
{
    s.replace(QLatin1Char('"'), QStringLiteral("\"\""));
    return QLatin1Char('"') + s + QLatin1Char('"');
}

QString AppViewModel::exportHistoryCsv(const QUrl &file)
{
    if (m_historyRaw.isEmpty()) return QStringLiteral("내보낼 실행 이력이 없습니다.");
    QSaveFile f(file.toLocalFile());
    if (!f.open(QIODevice::WriteOnly)) return QStringLiteral("파일을 쓸 수 없습니다: ") + file.toLocalFile();
    QByteArray data("\xEF\xBB\xBF"); // UTF-8 BOM so spreadsheet apps read Korean correctly
    data += "# 검증되지 않은 교육용 예시 모델 · SemiEdu 실행 이력\n";
    data += "runId,time,processId,process,status,statusKind,stateChanged,afterStateId\n";
    for (const auto &v : m_historyRaw) {
        const QJsonObject h = v.toObject();
        const QStringList cells{csvCell(h.value(QStringLiteral("runId")).toString()),
                                csvCell(h.value(QStringLiteral("time")).toString()),
                                csvCell(h.value(QStringLiteral("processId")).toString()),
                                csvCell(h.value(QStringLiteral("name")).toString()),
                                csvCell(h.value(QStringLiteral("status")).toString()),
                                csvCell(h.value(QStringLiteral("statusKind")).toString()),
                                h.value(QStringLiteral("physical")).toBool() ? QStringLiteral("true") : QStringLiteral("false"),
                                csvCell(h.value(QStringLiteral("afterStateId")).toString())};
        data += cells.join(QLatin1Char(',')).toUtf8() + '\n';
    }
    f.write(data);
    if (!f.commit()) return QStringLiteral("저장에 실패했습니다.");
    return QStringLiteral("실행 이력 %1건을 저장했습니다: %2").arg(m_historyRaw.size()).arg(file.toLocalFile());
}

QString AppViewModel::runSelfTest()
{
    if (m_busy || !m_ready) return QStringLiteral("계산 중에는 자가 테스트를 실행할 수 없습니다.");
    const QJsonObject r = m_engine->selftest();
    if (!r.value(QStringLiteral("ok")).toBool())
        return QStringLiteral("자가 테스트 실패: ") + r.value(QStringLiteral("error")).toObject().value(QStringLiteral("MessageKo")).toString();
    QStringList fails;
    for (const auto &v : r.value(QStringLiteral("tests")).toArray()) {
        const QJsonObject t = v.toObject();
        if (!t.value(QStringLiteral("pass")).toBool())
            fails << QStringLiteral("✖ %1 — 기대: %2 / 실제: %3")
                         .arg(t.value(QStringLiteral("id")).toString(), t.value(QStringLiteral("expected")).toString(),
                              t.value(QStringLiteral("actual")).toString());
    }
    const int total = r.value(QStringLiteral("total")).toInt(), passed = r.value(QStringLiteral("passed")).toInt();
    return QStringLiteral("엔진 자가 테스트(se_selftest): %1/%2 통과\n%3\n\n현재 세션에는 영향이 없습니다(별도 세션에서 실행). 테스트 통과는 소프트웨어 검증이며 실제 공정 검증이 아닙니다.")
        .arg(passed).arg(total)
        .arg(fails.isEmpty() ? QStringLiteral("모든 시나리오 통과 (M·E·Q·V·U·S·A·K·C·T)") : fails.join(QLatin1Char('\n')));
}

QString AppViewModel::engineInfo() const
{
    if (!m_ready) return m_banner;
    const auto v = m_engine->versionInfo();
    return QStringLiteral("SemiEdu.Engine %1\nABI 버전 %2 · 모델 버전 %3\nDLL: %4\n\n%5")
        .arg(v.value(QStringLiteral("engineVersion")).toString())
        .arg(v.value(QStringLiteral("abiVersion")).toInt())
        .arg(v.value(QStringLiteral("modelVersion")).toString(), m_engine->libraryPath(), v.value(QStringLiteral("note")).toString());
}

QUrl AppViewModel::scriptsFolder() const
{
    const QDir d(QCoreApplication::applicationDirPath() + QStringLiteral("/scripts"));
    return QUrl::fromLocalFile(d.exists() ? d.absolutePath() : QCoreApplication::applicationDirPath());
}
