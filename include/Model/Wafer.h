#pragma once

#include "Model/Layer.h"

#include <QObject>
#include <QString>
#include <QStringList>
#include <QVector>

class Wafer : public QObject {
    Q_OBJECT

public:
    explicit Wafer(QObject *parent = nullptr);

    const QVector<Layer> &layers() const;
    int processStep() const;
    QString maskPattern() const;
    const QStringList &processLog() const;
    void addLayer(const Layer &layer);
    void removeTopLayer();
    void setMaskPattern(const QString &pattern);
    void recordProcess(const QString &entry);
    void advanceProcess();
    void clear();

signals:
    void changed();

private:
    QVector<Layer> m_layers;
    QString m_maskPattern = QStringLiteral("Single gate line");
    QStringList m_processLog;
    int m_processStep = 0;
};
