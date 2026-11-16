#pragma once

#include <QMatrix4x4>
#include <QOpenGLBuffer>
#include <QOpenGLExtraFunctions>
#include <QOpenGLShaderProgram>
#include <QOpenGLVertexArrayObject>
#include <QSize>
#include <QVector>
#include <array>
#include <vector>

// OpenGL resources belong to the render thread and its current context.
// All boxes share an indexed unit cube. Only bounds and colors vary per instance.
class BoxRenderer : protected QOpenGLExtraFunctions
{
public:
    struct Stats {
        int totalBoxes = 0, visibleBoxes = 0, rejectedBoxes = 0;
        int drawCalls = 0, bufferAllocations = 0, selectionUpdates = 0;
        qint64 instanceUploadBytes = 0;
    };

    bool initialize();
    QString error() const { return m_error; }
    void setBoxes(const QVector<float> &packed);
    void render(const QMatrix4x4 &view, const QMatrix4x4 &projection, const QSize &size);
    const Stats &stats() const { return m_stats; }

    // Conservative world-space AABB / clip-volume intersection, also tested without GL.
    static bool intersectsFrustum(const QMatrix4x4 &mvp, const QVector3D &minimum,
                                  const QVector3D &maximum);

private:
    struct Box { QVector3D minimum, maximum; std::array<float, 4> color; float outline; };
    struct Instance { float minimum[3], maximum[3], color[4]; };
    struct Geometry {
        QOpenGLBuffer vertices{QOpenGLBuffer::VertexBuffer};
        QOpenGLBuffer indices{QOpenGLBuffer::IndexBuffer};
        int indexCount = 0;
    };
    struct Batch {
        QOpenGLVertexArrayObject vao;
        QOpenGLBuffer instances{QOpenGLBuffer::VertexBuffer};
        int capacity = 0, count = 0, indexCount = 0;
        std::vector<int> ids;
    };
    using Planes = std::array<QVector4D, 6>;
    static Planes planes(const QMatrix4x4 &mvp);
    static bool intersects(const Planes &planes, const QVector3D &minimum, const QVector3D &maximum);
    void createGeometry(Geometry &geometry, const std::vector<float> &vertices,
                        const std::vector<unsigned short> &indices);
    void createBatch(Batch &batch, Geometry &geometry);
    void upload(Batch &batch, const Instance *instances, int count);
    void updateBatch(Batch &batch, const std::vector<int> &ids, bool outline, bool force);
    void select(const QMatrix4x4 &view, const QMatrix4x4 &mvp);
    void draw(Batch &batch, GLenum primitive);

    QOpenGLShaderProgram m_program;
    Geometry m_faces, m_edges, m_gridGeometry;
    Batch m_opaque, m_transparent, m_lines, m_grid;
    std::vector<Box> m_boxes;
    std::vector<int> m_opaqueIds, m_transparentIds, m_lineIds;
    std::vector<Instance> m_scratch;
    std::vector<float> m_depths;
    QMatrix4x4 m_lastMvp, m_lastView;
    Stats m_stats;
    int m_rejected = 0, m_visible = 0;
    int m_mvpLocation = -1, m_unlitLocation = -1;
    bool m_ready = false, m_sceneDirty = true, m_selectionValid = false;
    QString m_error;
};
