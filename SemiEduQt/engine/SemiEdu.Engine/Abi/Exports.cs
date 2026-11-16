using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SemiEdu.Engine;

/// <summary>
/// C ABI (semiedu_engine.h). All strings are UTF-8; returned strings are NativeMemory blocks the caller frees with se_free.
/// No exception crosses the boundary. One session handle is serialized by its lock; se_cancel does not take the lock.
/// </summary>
public static unsafe class Exports
{
    static byte* Str(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var p = (byte*)NativeMemory.Alloc((nuint)bytes.Length + 1);
        bytes.AsSpan().CopyTo(new Span<byte>(p, bytes.Length));
        p[bytes.Length] = 0;
        return p;
    }

    static string? In(byte* p) => p == null ? null : Marshal.PtrToStringUTF8((IntPtr)p);

    static Session? Get(IntPtr h)
    {
        if (h == IntPtr.Zero) return null;
        try { return GCHandle.FromIntPtr(h).Target as Session; }
        catch { return null; }
    }

    static byte* Locked(IntPtr h, Func<Session, string> f)
    {
        try
        {
            var s = Get(h);
            if (s == null) return Str(Api.Error("INVALID_HANDLE", "세션 핸들이 올바르지 않습니다."));
            lock (s.Sync) return Str(f(s));
        }
        catch (Exception ex)
        {
            return Str(Api.Error("ENGINE_ERROR", ex.Message));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "se_version", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* Version()
    {
        try { return Str(Api.Version()); }
        catch { return null; }
    }

    [UnmanagedCallersOnly(EntryPoint = "se_create", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static IntPtr Create()
    {
        try { return GCHandle.ToIntPtr(GCHandle.Alloc(new Session())); }
        catch { return IntPtr.Zero; }
    }

    [UnmanagedCallersOnly(EntryPoint = "se_destroy", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Destroy(IntPtr h)
    {
        try { if (h != IntPtr.Zero) GCHandle.FromIntPtr(h).Free(); }
        catch { /* never cross the boundary */ }
    }

    [UnmanagedCallersOnly(EntryPoint = "se_catalog", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* CatalogExport(IntPtr h) => Locked(h, Api.Catalog);

    [UnmanagedCallersOnly(EntryPoint = "se_form", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* Form(IntPtr h, byte* processId, byte* valuesJson)
    {
        string? pid = In(processId), vals = In(valuesJson);
        return Locked(h, s => Api.Form(s, pid ?? "", vals));
    }

    [UnmanagedCallersOnly(EntryPoint = "se_run", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* Run(IntPtr h, byte* processId, byte* valuesJson, byte* equipmentJson, int commit, long expectedRevision)
    {
        string? pid = In(processId), vals = In(valuesJson), eq = In(equipmentJson);
        return Locked(h, s =>
        {
            using var cts = new CancellationTokenSource();
            s.Cts = cts;
            try { return Api.Run(s, pid ?? "", vals, eq, commit != 0, expectedRevision, cts.Token); }
            finally { s.Cts = null; }
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "se_cancel", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Cancel(IntPtr h)
    {
        try { Get(h)?.Cts?.Cancel(); }
        catch { /* ignore: run may have finished */ }
    }

    [UnmanagedCallersOnly(EntryPoint = "se_undo", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* UndoExport(IntPtr h) => Locked(h, Api.Undo);

    [UnmanagedCallersOnly(EntryPoint = "se_state", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* StateExport(IntPtr h) => Locked(h, Api.State);

    [UnmanagedCallersOnly(EntryPoint = "se_scene_meta", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* SceneMeta(IntPtr h, byte* optionsJson)
    {
        string? opts = In(optionsJson);
        return Locked(h, s => Api.SceneMeta(s, opts));
    }

    [UnmanagedCallersOnly(EntryPoint = "se_scene_boxes", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int SceneBoxes(IntPtr h, byte* optionsJson, float* buf, int capacity)
    {
        try
        {
            var s = Get(h);
            if (s == null) return -1;
            string? opts = In(optionsJson);
            lock (s.Sync)
            {
                var sd = Api.Scene(s, opts);
                int count = sd.BoxCount;
                if (buf != null && capacity >= count)
                    for (int i = 0; i < sd.Boxes.Count; i++) buf[i] = sd.Boxes[i];
                return count;
            }
        }
        catch
        {
            return -1;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "se_objects", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* ObjectsExport(IntPtr h) => Locked(h, Api.Objects);

    [UnmanagedCallersOnly(EntryPoint = "se_selftest", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static byte* SelfTestExport()
    {
        try { return Str(Api.SelfTestJson()); }
        catch (Exception ex) { return Str(Api.Error("ENGINE_ERROR", ex.Message)); }
    }

    [UnmanagedCallersOnly(EntryPoint = "se_free", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Free(byte* p)
    {
        try { if (p != null) NativeMemory.Free(p); }
        catch { /* ignore */ }
    }
}
