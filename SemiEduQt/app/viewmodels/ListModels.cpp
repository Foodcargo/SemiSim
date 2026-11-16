#include "ListModels.h"

#include <QJsonObject>

void JsonListModel::setItems(const QJsonArray &items)
{
    QVector<QVariantMap> maps;
    maps.reserve(items.size());
    for (const auto &v : items) maps.push_back(v.toObject().toVariantMap());
    setMaps(maps);
}

void JsonListModel::setMaps(const QVector<QVariantMap> &items)
{
    const bool countChange = items.size() != m_items.size();
    beginResetModel();
    m_items = items;
    endResetModel();
    if (countChange) emit countChanged();
}

QVariant JsonListModel::data(const QModelIndex &index, int role) const
{
    const int i = index.row();
    const int r = role - Qt::UserRole - 1;
    if (i < 0 || i >= m_items.size() || r < 0 || r >= m_roles.size()) return {};
    return m_items[i].value(QString::fromUtf8(m_roles[r]));
}

QHash<int, QByteArray> JsonListModel::roleNames() const
{
    QHash<int, QByteArray> h;
    for (int i = 0; i < m_roles.size(); ++i) h.insert(Qt::UserRole + 1 + i, m_roles[i]);
    return h;
}

static ParamItem parseItem(const QJsonObject &o)
{
    ParamItem p;
    p.paramId = o.value(QStringLiteral("paramId")).toString();
    p.label = o.value(QStringLiteral("label")).toString();
    p.kind = o.value(QStringLiteral("kind")).toString();
    p.unit = o.value(QStringLiteral("unit")).toString();
    p.hint = o.value(QStringLiteral("hint")).toString();
    p.error = o.value(QStringLiteral("error")).toString();
    p.text = o.value(QStringLiteral("text")).toString();
    for (const auto &v : o.value(QStringLiteral("options")).toArray()) p.options << v.toString();
    p.currentIndex = o.value(QStringLiteral("currentIndex")).toInt(-1);
    p.readOnly = o.value(QStringLiteral("readOnly")).toBool();
    return p;
}

void ParamListModel::update(const QJsonArray &fields)
{
    QVector<ParamItem> items;
    items.reserve(fields.size());
    for (const auto &v : fields) items.push_back(parseItem(v.toObject()));
    bool sameShape = items.size() == m_items.size();
    for (int i = 0; sameShape && i < items.size(); ++i)
        sameShape = items[i].paramId == m_items[i].paramId && items[i].kind == m_items[i].kind;
    if (!sameShape) {
        beginResetModel();
        m_items = items;
        endResetModel();
        return;
    }
    for (int i = 0; i < items.size(); ++i) {
        if (items[i] == m_items[i]) continue;
        m_items[i] = items[i];
        emit dataChanged(index(i), index(i));
    }
}

void ParamListModel::clear()
{
    beginResetModel();
    m_items.clear();
    endResetModel();
}

bool ParamListModel::contains(const QString &paramId) const
{
    for (const auto &p : m_items)
        if (p.paramId == paramId) return true;
    return false;
}

QStringList ParamListModel::optionsOf(const QString &paramId) const
{
    for (const auto &p : m_items)
        if (p.paramId == paramId) return p.options;
    return {};
}

QVariant ParamListModel::data(const QModelIndex &index, int role) const
{
    const int i = index.row();
    if (i < 0 || i >= m_items.size()) return {};
    const auto &p = m_items[i];
    switch (role) {
    case ParamIdRole: return p.paramId;
    case LabelRole: return p.label;
    case KindRole: return p.kind;
    case OptionsRole: return p.options;
    case CurrentIndexRole: return p.currentIndex;
    case TextRole: return p.text;
    case UnitRole: return p.unit;
    case HintRole: return p.hint;
    case ErrorRole: return p.error;
    case ReadOnlyRole: return p.readOnly;
    default: return {};
    }
}

QHash<int, QByteArray> ParamListModel::roleNames() const
{
    return {{ParamIdRole, "paramId"}, {LabelRole, "label"}, {KindRole, "kind"},   {OptionsRole, "options"},
            {CurrentIndexRole, "currentIndex"}, {TextRole, "text"}, {UnitRole, "unit"}, {HintRole, "hint"},
            {ErrorRole, "error"}, {ReadOnlyRole, "readOnly"}};
}
