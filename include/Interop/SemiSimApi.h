#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define SEMISIM_API __declspec(dllexport)
#else
#define SEMISIM_API __attribute__((visibility("default")))
#endif

extern "C" {

SEMISIM_API void *semisim_create();
SEMISIM_API void semisim_destroy(void *instance);
SEMISIM_API int semisim_reset(void *instance);
SEMISIM_API int semisim_apply_process(void *instance, int step, const char *parametersJson);
SEMISIM_API int semisim_get_state(void *instance, char *buffer, int capacity);
SEMISIM_API int semisim_camera_orbit(void *instance, double deltaYaw, double deltaPitch);
SEMISIM_API int semisim_camera_zoom(void *instance, double delta);
SEMISIM_API int semisim_render(void *instance, int width, int height, uint8_t *pixels, int capacity);

}