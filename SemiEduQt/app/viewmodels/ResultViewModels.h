#pragma once

#include <QJsonObject>
#include <QObject>
#include <QPointF>
#include <QVariantList>
#include <QVector>
#include <QtQml/qqmlregistration.h>

#include "ListModels.h"

class EngineClient;

class ResultViewModel : public QObject
{
    Q_OBJECT
    QML_ELEMENT
    QML_UNCREATABLE("created in main.cpp")
    Q_PROPERTY(bool hasResult READ hasResult NOTIFY resultChanged)
    Q_PROPERTY(QString statusKind READ statusKind NOTIFY resultChanged)
    Q_PROPERTY(QString statusText READ statusText NOTIFY resultChanged)
    Q_PROPERTY(QString processName READ processName NOTIFY resultChanged)
    Q_PROPERTY(bool isPreview READ isPreview NOTIFY resultChanged)
    Q_PROPERTY(JsonListModel *stages READ stages CONSTANT)
    Q_PROPERTY(JsonListModel *metrics READ metrics CONSTANT)
    Q_PROPERTY(JsonListModel *errors READ errors CONSTANT)
    Q_PROPERTY(JsonListModel *objects READ objects CONSTANT)
    Q_PROPERTY(QString deviceVerdict READ deviceVerdict NOTIFY resultChanged)
    Q_PROPERTY(bool deviceNormal READ deviceNormal NOTIFY resultChanged)
    Q_PROPERTY(QString extractedText READ extractedText NOTIFY resultChanged)
    Q_PROPERTY(QString warningsText READ warningsText NOTIFY resultChanged)
    Q_PROPERTY(QString netsText READ netsText NOTIFY objectsChanged)
public:
    explicit ResultViewModel(QObject *parent = nullptr);

    bool hasResult() const { return m_has; }
    QString statusKind() const { return m_kind; }
    QString statusText() const { return m_text; }
    QString processName() const { return m_process; }
    bool isPreview() const { return m_preview; }
    JsonListModel *stages() const { return m_stages; }
    JsonListModel *metrics() const { return m_metrics; }
    JsonListModel *errors() const { return m_errors; }
    JsonListModel *objects() const { return m_objects; }
    QString deviceVerdict() const { return m_verdict; }
    bool deviceNormal() const { return m_normal; }
    QString extractedText() const { return m_extracted; }
    QString warningsText() const { return m_warnings; }
    QString netsText() const { return m_nets; }

    void setFromRun(const QJsonObject &run);
    void setObjects(const QJsonObject &objects);

signals:
    void resultChanged();
    void objectsChanged();

private:
    JsonListModel *m_stages, *m_metrics, *m_errors, *m_objects;
    bool m_has = false, m_preview = false, m_normal = false;
    QString m_kind, m_text, m_process, m_verdict, m_extracted, m_warnings, m_nets;
};

class SceneViewModel : public QObject
{
    Q_OBJECT
    QML_ELEMENT
    QML_UNCREATABLE("created in main.cpp")
    Q_PROPERTY(int sceneRevision READ sceneRevision NOTIFY sceneChanged)
    Q_PROPERTY(QString legendHtml READ legendHtml NOTIFY sceneChanged)
    Q_PROPERTY(QString scaleText READ scaleText NOTIFY sceneChanged)
    Q_PROPERTY(QString terminalsText READ terminalsText NOTIFY sceneChanged)
    Q_PROPERTY(bool isPreview READ isPreview NOTIFY sceneChanged)
    Q_PROPERTY(bool classificationView READ classificationView WRITE setClassificationView NOTIFY classificationViewChanged)
    Q_PROPERTY(QString viewKind READ viewKind NOTIFY sceneChanged)
    Q_PROPERTY(int boxCount READ boxCount NOTIFY sceneChanged)
public:
    explicit SceneViewModel(EngineClient *engine, QObject *parent = nullptr);

    int sceneRevision() const { return m_rev; }
    QString legendHtml() const { return m_legend; }
    QString scaleText() const { return m_scale; }
    QString terminalsText() const { return m_terminals; }
    bool isPreview() const { return m_isPreview; }
    bool classificationView() const { return m_cls; }
    QString viewKind() const { return m_view; }
    int boxCount() const { return int(m_boxes.size() / 11); }
    QVector<float> boxes() const { return m_boxes; }

    void refresh(bool preview);
    void setViewKind(const QString &kind) { m_view = kind; }

    Q_INVOKABLE void setClassificationView(bool on);
    Q_INVOKABLE void resetCamera() { emit cameraResetRequested(); }

signals:
    void sceneChanged();
    void classificationViewChanged();
    void cameraResetRequested();

private:
    EngineClient *m_engine;
    QVector<float> m_boxes;
    int m_rev = 0;
    bool m_cls = false, m_isPreview = false, m_lastPreview = false;
    QString m_legend, m_scale, m_terminals, m_view = QStringLiteral("wafer");
};

class PlotViewModel : public QObject
{
    Q_OBJECT
    QML_ELEMENT
    QML_UNCREATABLE("created in main.cpp")
    Q_PROPERTY(bool visible READ visible NOTIFY plotChanged)
    Q_PROPERTY(QString title READ title NOTIFY plotChanged)
    Q_PROPERTY(QString xLabel READ xLabel NOTIFY plotChanged)
    Q_PROPERTY(QString yLabel READ yLabel NOTIFY plotChanged)
    Q_PROPERTY(bool logY READ logY NOTIFY plotChanged)
    Q_PROPERTY(QVariantList series READ series NOTIFY plotChanged)
    Q_PROPERTY(bool hasVthExt READ hasVthExt NOTIFY plotChanged)
    Q_PROPERTY(double vthExt READ vthExt NOTIFY plotChanged)
public:
    explicit PlotViewModel(QObject *parent = nullptr) : QObject(parent) {}
    bool visible() const { return m_visible; }
    QString title() const { return m_title; }
    QString xLabel() const { return m_x; }
    QString yLabel() const { return m_y; }
    bool logY() const { return m_logY; }
    QVariantList series() const { return m_series; }
    QVector<QPointF> points() const { return m_points; }
    bool hasVthExt() const { return m_hasVth; }
    double vthExt() const { return m_vth; }
    void setFromElectrical(const QJsonObject &electrical);

signals:
    void plotChanged();

private:
    bool m_visible = false, m_logY = false, m_hasVth = false;
    double m_vth = 0;
    QString m_title, m_x, m_y;
    QVariantList m_series;
    QVector<QPointF> m_points;
};
