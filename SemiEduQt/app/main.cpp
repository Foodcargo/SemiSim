#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QGuiApplication>
#include <QJsonArray>
#include <QJsonDocument>
#include <QQmlApplicationEngine>
#include <QQuickStyle>
#include <QQuickWindow>
#include <QSurfaceFormat>
#include <cstdio>

#ifdef Q_OS_WIN
#include <windows.h>
#endif

#include "engine/EngineClient.h"
#include "viewmodels/AppViewModel.h"
#include "viewmodels/ProcessEditorViewModel.h"
#include "viewmodels/ResultViewModels.h"

static int runSelfTest(int argc, char *argv[])
{
#ifdef Q_OS_WIN
    // GUI-subsystem exe: attach to the parent console only when stdout is not already redirected.
    HANDLE out = GetStdHandle(STD_OUTPUT_HANDLE);
    if ((out == nullptr || out == INVALID_HANDLE_VALUE) && AttachConsole(ATTACH_PARENT_PROCESS)) {
        freopen("CONOUT$", "w", stdout);
        freopen("CONOUT$", "w", stderr);
    }
#endif
    QCoreApplication app(argc, argv);
    EngineClient engine;
    if (!engine.load()) {
        std::fprintf(stderr, "ENGINE LOAD FAILED: %s\n", engine.error().toUtf8().constData());
        return 2;
    }
    const QJsonObject r = engine.selftest();
    const QByteArray json = QJsonDocument(r).toJson(QJsonDocument::Indented);
    QFile f(QDir(QCoreApplication::applicationDirPath()).filePath(QStringLiteral("selftest-result.json")));
    if (f.open(QIODevice::WriteOnly)) f.write(json);
    int passed = 0, total = 0;
    for (const auto &v : r.value(QStringLiteral("tests")).toArray()) {
        const auto t = v.toObject();
        ++total;
        const bool ok = t.value(QStringLiteral("pass")).toBool();
        passed += ok ? 1 : 0;
        std::printf("%s %-4s %s\n", ok ? "PASS" : "FAIL", t.value(QStringLiteral("id")).toString().toUtf8().constData(),
                    t.value(QStringLiteral("actual")).toString().toUtf8().constData());
    }
    std::printf("se_selftest: %d/%d passed (engine %s)\n", passed, total, engine.libraryPath().toUtf8().constData());
    std::fflush(stdout);
    return passed == total && total > 0 ? 0 : 1;
}

int main(int argc, char *argv[])
{
    QString scriptPath;
    for (int i = 1; i < argc; ++i) {
        if (QByteArray(argv[i]) == "--selftest") return runSelfTest(argc, argv);
        if (QByteArray(argv[i]) == "--script" && i + 1 < argc) scriptPath = QString::fromLocal8Bit(argv[++i]);
    }

    QQuickWindow::setGraphicsApi(QSGRendererInterface::OpenGL);
    QSurfaceFormat fmt;
    fmt.setRenderableType(QSurfaceFormat::OpenGL);
    fmt.setVersion(3, 3);
    fmt.setProfile(QSurfaceFormat::CoreProfile);
    fmt.setDepthBufferSize(24);
    fmt.setStencilBufferSize(8);
    fmt.setSamples(4);
    QSurfaceFormat::setDefaultFormat(fmt);

    QGuiApplication app(argc, argv);
    QGuiApplication::setApplicationName(QStringLiteral("SemiEduApp"));
    QQuickStyle::setStyle(QStringLiteral("Fusion"));

    EngineClient engine;
    engine.load();
    ProcessEditorViewModel editor(&engine);
    ResultViewModel result;
    SceneViewModel scene(&engine);
    PlotViewModel plot;
    AppViewModel appVm(&engine, &editor, &result, &scene, &plot);
    // --script <file.json>: replay a learning path (same code path as File ▸ 학습 경로 열기).
    if (!scriptPath.isEmpty() && engine.ready()) {
        std::printf("%s\n", appVm.runScriptFile(scriptPath).toUtf8().constData());
        std::fflush(stdout);
    }

    QQmlApplicationEngine qml;
    qml.setInitialProperties({{QStringLiteral("app"), QVariant::fromValue(&appVm)},
                              {QStringLiteral("editor"), QVariant::fromValue(&editor)},
                              {QStringLiteral("result"), QVariant::fromValue(&result)},
                              {QStringLiteral("scene"), QVariant::fromValue(&scene)},
                              {QStringLiteral("plot"), QVariant::fromValue(&plot)}});
    QObject::connect(&qml, &QQmlApplicationEngine::objectCreationFailed, &app, [] { QCoreApplication::exit(-1); }, Qt::QueuedConnection);
    qml.loadFromModule("SemiEdu", "Main");
    return app.exec();
}
