#include "BoxRenderer.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <stdexcept>

namespace {
constexpr int vertexFloats = 10; // position, normal, color multiplier
const char *vertexShader = R"(#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec4 aColor;
layout(location = 3) in vec3 iMin;
layout(location = 4) in vec3 iMax;
layout(location = 5) in vec4 iColor;
uniform mat4 uMvp;
out vec3 vNormal;
out vec4 vColor;
void main() {
    gl_Position = uMvp * vec4(mix(iMin, iMax, aPos), 1.0);
    vNormal = aNormal;
    vColor = aColor * iColor;
}
)";
const char *fragmentShader = R"(#version 330 core
in vec3 vNormal;
in vec4 vColor;
uniform int uUnlit;
out vec4 fragColor;
void main() {
    if (uUnlit == 1) { fragColor = vColor; return; }
    vec3 n = normalize(vNormal);
    vec3 l1 = normalize(vec3(0.45, -0.75, 0.85));
    vec3 l2 = normalize(vec3(-0.6, 0.45, 0.35));
    float d = 0.38 + 0.55 * max(dot(n, l1), 0.0) + 0.22 * max(dot(n, l2), 0.0);
    fragColor = vec4(vColor.rgb * min(d, 1.15), vColor.a);
}
)";

void vertex(std::vector<float> &out, const QVector3D &position, const QVector3D &normal,
            float r = 1, float g = 1, float b = 1, float a = 1)
{
    out.insert(out.end(), {position.x(), position.y(), position.z(), normal.x(), normal.y(), normal.z(), r, g, b, a});
}
}

bool BoxRenderer::initialize()
{
    if (m_ready) return true;
    initializeOpenGLFunctions();
    if (!m_program.addShaderFromSourceCode(QOpenGLShader::Vertex, vertexShader)
        || !m_program.addShaderFromSourceCode(QOpenGLShader::Fragment, fragmentShader)
        || !m_program.link()) {
        m_error = m_program.log();
        return false;
    }
    m_mvpLocation = m_program.uniformLocation("uMvp");
    m_unlitLocation = m_program.uniformLocation("uUnlit");

    struct Face { QVector3D normal; std::array<QVector3D, 4> corners; };
    const Face faces[] = {
        {{-1,0,0}, {{{0,0,0},{0,1,0},{0,1,1},{0,0,1}}}},
        {{1,0,0}, {{{1,0,0},{1,0,1},{1,1,1},{1,1,0}}}},
        {{0,-1,0}, {{{0,0,0},{0,0,1},{1,0,1},{1,0,0}}}},
        {{0,1,0}, {{{0,1,0},{1,1,0},{1,1,1},{0,1,1}}}},
        {{0,0,-1}, {{{0,0,0},{1,0,0},{1,1,0},{0,1,0}}}},
        {{0,0,1}, {{{0,0,1},{0,1,1},{1,1,1},{1,0,1}}}},
    };
    std::vector<float> vertices;
    std::vector<unsigned short> indices;
    for (const Face &face : faces) {
        const auto start = static_cast<unsigned short>(vertices.size() / vertexFloats);
        for (const auto &p : face.corners) vertex(vertices, p, face.normal);
        for (int index : {0,1,2,0,2,3}) indices.push_back(start + index);
    }
    createGeometry(m_faces, vertices, indices);

    vertices.clear(); indices.clear();
    for (int z = 0; z < 2; ++z)
        for (int y = 0; y < 2; ++y)
            for (int x = 0; x < 2; ++x) vertex(vertices, {float(x),float(y),float(z)}, {});
    for (unsigned short i = 0; i < 8; ++i)
        for (unsigned short bit : {1,2,4})
            if (!(i & bit)) { indices.push_back(i); indices.push_back(i | bit); }
    createGeometry(m_edges, vertices, indices);

    vertices.clear(); indices.clear();
    auto gridLine = [&](QVector3D a, QVector3D b, float alpha) {
        auto start = static_cast<unsigned short>(vertices.size() / vertexFloats);
        vertex(vertices, a, {}, 0.30f, 0.36f, 0.46f, alpha);
        vertex(vertices, b, {}, 0.30f, 0.36f, 0.46f, alpha);
        indices.push_back(start); indices.push_back(start + 1);
    };
    for (int i = -14; i <= 26; ++i) gridLine({float(i),-14,-0.62f}, {float(i),18,-0.62f}, i % 5 == 0 ? 0.55f : 0.28f);
    for (int i = -14; i <= 18; ++i) gridLine({-14,float(i),-0.62f}, {26,float(i),-0.62f}, i % 5 == 0 ? 0.55f : 0.28f);
    createGeometry(m_gridGeometry, vertices, indices);
    createBatch(m_opaque, m_faces);
    createBatch(m_transparent, m_faces);
    createBatch(m_lines, m_edges);
    createBatch(m_grid, m_gridGeometry);
    const Instance identity{{0,0,0},{1,1,1},{1,1,1,1}};
    upload(m_grid, &identity, 1);
    m_ready = true;
    return true;
}

void BoxRenderer::createGeometry(Geometry &g, const std::vector<float> &vertices,
                                 const std::vector<unsigned short> &indices)
{
    // Core profile requires a VAO even while binding an element buffer for upload.
    QOpenGLVertexArrayObject setup;
    setup.create(); setup.bind();
    g.vertices.create(); g.vertices.bind();
    g.vertices.setUsagePattern(QOpenGLBuffer::StaticDraw);
    g.vertices.allocate(vertices.data(), int(vertices.size() * sizeof(float)));
    g.indices.create(); g.indices.bind();
    g.indices.setUsagePattern(QOpenGLBuffer::StaticDraw);
    g.indices.allocate(indices.data(), int(indices.size() * sizeof(unsigned short)));
    g.indexCount = int(indices.size());
    setup.release();
    g.vertices.release();
}

void BoxRenderer::createBatch(Batch &batch, Geometry &geometry)
{
    batch.vao.create(); batch.vao.bind();
    geometry.vertices.bind(); geometry.indices.bind();
    const int sizes[] = {3,3,4};
    const int offsets[] = {0,3,6};
    for (int i = 0; i < 3; ++i) {
        glEnableVertexAttribArray(i);
        glVertexAttribPointer(i, sizes[i], GL_FLOAT, GL_FALSE, vertexFloats * sizeof(float),
                              reinterpret_cast<void *>(quintptr(offsets[i] * sizeof(float))));
    }
    static_assert(sizeof(Instance) == 10 * sizeof(float), "Instance data must be tightly packed");
    batch.instances.create(); batch.instances.bind();
    batch.instances.setUsagePattern(QOpenGLBuffer::DynamicDraw);
    for (int i = 0; i < 3; ++i) {
        glEnableVertexAttribArray(3 + i);
        glVertexAttribPointer(3 + i, sizes[i], GL_FLOAT, GL_FALSE, sizeof(Instance),
                              reinterpret_cast<void *>(quintptr(offsets[i] * sizeof(float))));
        glVertexAttribDivisor(3 + i, 1);
    }
    batch.indexCount = geometry.indexCount;
    batch.vao.release(); batch.instances.release();
}

void BoxRenderer::setBoxes(const QVector<float> &packed)
{
    m_boxes.clear();
    m_boxes.reserve(size_t(packed.size() / 11));
    m_rejected = int(packed.size() % 11 != 0);
    for (qsizetype i = 0; i + 10 < packed.size(); i += 11) {
        const float *p = packed.constData() + i;
        if (!std::all_of(p, p + 11, [](float v) { return std::isfinite(v); })
            || p[0] > p[1] || p[2] > p[3] || p[4] > p[5]) { ++m_rejected; continue; }
        m_boxes.push_back({{p[0],p[2],p[4]}, {p[1],p[3],p[5]}, {p[6],p[7],p[8],p[9]}, p[10]});
    }
    m_sceneDirty = true;
}

BoxRenderer::Planes BoxRenderer::planes(const QMatrix4x4 &mvp)
{
    return {mvp.row(3) + mvp.row(0), mvp.row(3) - mvp.row(0),
            mvp.row(3) + mvp.row(1), mvp.row(3) - mvp.row(1),
            mvp.row(3) + mvp.row(2), mvp.row(3) - mvp.row(2)};
}

bool BoxRenderer::intersects(const Planes &clipPlanes, const QVector3D &minimum, const QVector3D &maximum)
{
    for (const auto &p : clipPlanes) {
        const QVector3D positive(p.x() >= 0 ? maximum.x() : minimum.x(),
                                 p.y() >= 0 ? maximum.y() : minimum.y(),
                                 p.z() >= 0 ? maximum.z() : minimum.z());
        const float distance = QVector3D::dotProduct(p.toVector3D(), positive) + p.w();
        // Keep boundary-touching/thin boxes instead of popping on rounding error.
        const float epsilon = 1e-5f * (1 + std::abs(p.w()) + p.toVector3D().length() * positive.length());
        if (distance < -epsilon) return false;
    }
    return true;
}

bool BoxRenderer::intersectsFrustum(const QMatrix4x4 &mvp, const QVector3D &minimum, const QVector3D &maximum)
{
    return intersects(planes(mvp), minimum, maximum);
}

void BoxRenderer::upload(Batch &batch, const Instance *instances, int count)
{
    const qint64 byteCount = qint64(count) * sizeof(Instance);
    if (byteCount > std::numeric_limits<int>::max()) throw std::length_error("Instance buffer exceeds Qt buffer limit");
    batch.count = count;
    if (!count) return;
    batch.instances.bind();
    if (batch.capacity < byteCount) {
        const qint64 grown = std::max<qint64>({byteCount, qint64(batch.capacity) * 2, 4096});
        batch.capacity = int(std::min<qint64>(grown, std::numeric_limits<int>::max()));
        batch.instances.allocate(nullptr, batch.capacity);
        ++m_stats.bufferAllocations;
    }
    batch.instances.write(0, instances, int(byteCount));
    batch.instances.release();
    m_stats.instanceUploadBytes += byteCount;
}

void BoxRenderer::updateBatch(Batch &batch, const std::vector<int> &ids, bool outline, bool force)
{
    if (!force && batch.ids == ids) return;
    batch.ids = ids;
    m_scratch.clear(); m_scratch.reserve(ids.size());
    for (int id : ids) {
        const auto &box = m_boxes[size_t(id)];
        Instance instance{{box.minimum.x(),box.minimum.y(),box.minimum.z()},
                          {box.maximum.x(),box.maximum.y(),box.maximum.z()},
                          {box.color[0],box.color[1],box.color[2],box.color[3]}};
        if (outline) {
            const bool bright = box.outline > 1.5f;
            const float k = bright ? 1 : 0.45f;
            for (int i = 0; i < 3; ++i) instance.color[i] *= k;
            instance.color[3] = bright ? 1 : std::max(0.6f, instance.color[3]);
        }
        m_scratch.push_back(instance);
    }
    upload(batch, m_scratch.data(), int(m_scratch.size()));
}

void BoxRenderer::select(const QMatrix4x4 &view, const QMatrix4x4 &mvp)
{
    if (!m_sceneDirty && m_selectionValid && view == m_lastView && mvp == m_lastMvp) return;
    ++m_stats.selectionUpdates;
    m_opaqueIds.clear(); m_transparentIds.clear(); m_lineIds.clear();
    m_depths.resize(m_boxes.size());
    const auto clipPlanes = planes(mvp);
    m_visible = 0;
    for (int i = 0; i < int(m_boxes.size()); ++i) {
        const auto &box = m_boxes[size_t(i)];
        if (!intersects(clipPlanes, box.minimum, box.maximum)) continue;
        ++m_visible;
        if (box.color[3] >= 0.999f) m_opaqueIds.push_back(i);
        else {
            m_transparentIds.push_back(i);
            m_depths[size_t(i)] = (view * QVector4D((box.minimum + box.maximum) * 0.5f, 1)).z();
        }
        if (box.outline > 0.5f) m_lineIds.push_back(i);
    }
    // Back-to-front in camera space. Equal depths use stable scene IDs, avoiding upload churn.
    std::sort(m_transparentIds.begin(), m_transparentIds.end(), [this](int a, int b) {
        return m_depths[size_t(a)] == m_depths[size_t(b)] ? a < b : m_depths[size_t(a)] < m_depths[size_t(b)];
    });
    updateBatch(m_opaque, m_opaqueIds, false, m_sceneDirty);
    updateBatch(m_transparent, m_transparentIds, false, m_sceneDirty);
    updateBatch(m_lines, m_lineIds, true, m_sceneDirty);
    m_lastView = view; m_lastMvp = mvp;
    m_selectionValid = true; m_sceneDirty = false;
}

void BoxRenderer::draw(Batch &batch, GLenum primitive)
{
    if (!batch.count) return;
    batch.vao.bind();
    glDrawElementsInstanced(primitive, batch.indexCount, GL_UNSIGNED_SHORT, nullptr, batch.count);
    batch.vao.release();
    ++m_stats.drawCalls;
}

void BoxRenderer::render(const QMatrix4x4 &view, const QMatrix4x4 &projection, const QSize &size)
{
    m_stats = {};
    m_stats.totalBoxes = int(m_boxes.size()); m_stats.rejectedBoxes = m_rejected;
    if (!m_ready || size.isEmpty()) return;
    const QMatrix4x4 mvp = projection * view;
    select(view, mvp);
    m_stats.visibleBoxes = m_visible;
    glViewport(0, 0, size.width(), size.height());
    glDisable(GL_SCISSOR_TEST);
    glColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
    glDepthMask(GL_TRUE);
    glClearColor(0.050f, 0.058f, 0.075f, 1);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);
    glEnable(GL_DEPTH_TEST); glDepthFunc(GL_LEQUAL); glDisable(GL_CULL_FACE);
    m_program.bind(); m_program.setUniformValue(m_mvpLocation, mvp);
    glEnable(GL_BLEND);
    // Keep the opaque FBO background alpha at 1 while blending straight-alpha box colors.
    // Using the RGB factors for alpha as well creates holes in Qt's composited FBO texture.
    glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
    m_program.setUniformValue(m_unlitLocation, 1); draw(m_grid, GL_LINES);
    glDisable(GL_BLEND);
    glEnable(GL_POLYGON_OFFSET_FILL); glPolygonOffset(1, 1);
    m_program.setUniformValue(m_unlitLocation, 0); draw(m_opaque, GL_TRIANGLES);
    glDisable(GL_POLYGON_OFFSET_FILL);
    glEnable(GL_BLEND);
    m_program.setUniformValue(m_unlitLocation, 1); draw(m_lines, GL_LINES);
    glDepthMask(GL_FALSE);
    m_program.setUniformValue(m_unlitLocation, 0); draw(m_transparent, GL_TRIANGLES);
    glDepthMask(GL_TRUE);
    m_program.release();
    glDisable(GL_BLEND); glDisable(GL_DEPTH_TEST);
}
