#include "Model/Wafer.h"

#include <QtGlobal>

Wafer::Wafer(QObject *parent)
    : QObject(parent)
{
    addLayer({{"Silicon", QColor("#7b8794"), 1.0}, 725000.0});
}

const QVector<Layer> &Wafer::layers() const
{
    return m_layers;
}

QString Wafer::maskPattern() const
{
    return m_maskPattern;
}

const QStringList &Wafer::processLog() const
{
    return m_processLog;
}

int Wafer::processStep() const
{
    return m_processStep;
}

void Wafer::addLayer(const Layer &layer)
{
    m_layers.push_back(layer);
    emit changed();
}

void Wafer::removeTopLayer()
{
    if (m_layers.size() <= 1) {
        return;
    }

    m_layers.removeLast();
    emit changed();
}

void Wafer::setMaskPattern(const QString &pattern)
{
    m_maskPattern = pattern;
    emit changed();
}

void Wafer::recordProcess(const QString &entry)
{
    m_processLog.push_back(entry);
    emit changed();
}

void Wafer::advanceProcess()
{
    m_processStep = qMin(m_processStep + 1, 7);
    emit changed();
}

void Wafer::clear()
{
    m_processStep = 0;
    m_layers.clear();
    m_maskPattern = QStringLiteral("Single gate line");
    m_processLog.clear();
    addLayer({{"Silicon", QColor("#7b8794"), 1.0}, 725000.0});
}
