/* SemiEdu.Engine C ABI (abiVersion 1). Unverified educational example model.
 * All strings are UTF-8 JSON. Strings returned by the engine must be released with se_free.
 * Every JSON response is {"ok":true,...} or {"ok":false,"error":{"Code":...,"MessageKo":...}}.
 */
#ifndef SEMIEDU_ENGINE_H
#define SEMIEDU_ENGINE_H

#ifdef __cplusplus
extern "C" {
#endif

typedef void *se_handle;

typedef char *(*se_version_fn)(void);
typedef se_handle (*se_create_fn)(void);
typedef void (*se_destroy_fn)(se_handle h);
typedef char *(*se_catalog_fn)(se_handle h);
typedef char *(*se_form_fn)(se_handle h, const char *processId, const char *valuesJson);
typedef char *(*se_run_fn)(se_handle h, const char *processId, const char *valuesJson, const char *equipmentJson,
                           int commit, long long expectedRevision);
typedef void (*se_cancel_fn)(se_handle h);
typedef char *(*se_undo_fn)(se_handle h);
typedef char *(*se_state_fn)(se_handle h);
typedef char *(*se_scene_meta_fn)(se_handle h, const char *optionsJson);
/* Fills 11 floats per box (x0,x1,y0,y1,z0,z1,r,g,b,a,outline) when capacity >= count; returns the box count, -1 on error. */
typedef int (*se_scene_boxes_fn)(se_handle h, const char *optionsJson, float *buf, int capacity);
typedef char *(*se_objects_fn)(se_handle h);
typedef char *(*se_selftest_fn)(void);
typedef void (*se_free_fn)(char *p);

#ifdef __cplusplus
}
#endif

#endif
