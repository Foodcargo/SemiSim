#pragma once

#include <QAbstractListModel>
#include <QJsonArray>
#include <QStringList>
#include <QVariantMap>
#include <QVector>
#include <QtQml/qqmlregistration.h>

// Generic list of JSON objects exposed to QML by role name.
class JsonListModel : public QAbstractListModel
{
    Q_OBJECT
    QML_ELEMENT
    QML_UNCREATABLE("created by view models")
    Q_PROPERTY(int count READ count NOTIFY countChanged)
public:
    explicit JsonListModel(QObject *parent = nullptr) : QAbstractListModel(parent) {}
    void setRoles(const QList<QByteArray> &roles) { m_roles = roles; }
    void setItems(const QJsonArray &items);
    void setMaps(const QVector<QVariantMap> &items);

    int rowCount(const QModelIndex &parent = {}) const override { return parent.isValid() ? 0 : int(m_items.size()); }
    QVariant data(const QModelIndex &index, int role) const override;
    QHash<int, QByteArray> roleNames() const override;
    int count() const { return int(m_items.size()); }

signals:
    void countChanged();

private:
    QList<QByteArray> m_roles;
    QVector<QVariantMap> m_items;
};

struct ParamItem
{
    QString paramId, label, kind, unit, hint, error, text;
    QStringList options;
    int currentIndex = -1;
    bool readOnly = false;
    bool operator==(const ParamItem &o) const
    {
        return paramId == o.paramId && label == o.label && kind == o.kind && unit == o.unit && hint == o.hint && error == o.error
               && text == o.text && options == o.options && currentIndex == o.currentIndex && readOnly == o.readOnly;
    }
};

// Field list produced by se_form. Field composition changes reset the model; value/error changes emit dataChanged.
class ParamListModel : public QAbstractListModel
{
    Q_OBJECT
    QML_ELEMENT
    QML_UNCREATABLE("created by ProcessEditorViewModel")
public:
    enum Roles { ParamIdRole = Qt::UserRole + 1, LabelRole, KindRole, OptionsRole, CurrentIndexRole, TextRole, UnitRole, HintRole, ErrorRole, ReadOnlyRole };

    explicit ParamListModel(QObject *parent = nullptr) : QAbstractListModel(parent) {}
    void update(const QJsonArray &fields);
    bool contains(const QString &paramId) const;
    QStringList optionsOf(const QString &paramId) const;
    void clear();

    int rowCount(const QModelIndex &parent = {}) const override { return parent.isValid() ? 0 : int(m_items.size()); }
    QVariant data(const QModelIndex &index, int role) const override;
    QHash<int, QByteArray> roleNames() const override;

private:
    QVector<ParamItem> m_items;
};
