// GUI test fixture, loaded in the test process only with DYLD_INSERT_LIBRARIES. No driver, physical HID or product hooks.
// Attaches an SDL virtual gamepad once the game opens SDL's gamepad subsystem and appends every rumble SDL sends to it to
// MONOON_QA_RUMBLE: "<milliseconds since attach> <low> <high>". SDL sends 0 0 itself when a rumble's duration ends.
// The same pad presses buttons and axes read from MONOON_QA_PAD ("b <button> <0|1>" or "a <axis> <value>" per line),
// which reach Scene Play and the players through SDL's gamepad state without OS input events.
#include <SDL3/SDL.h>
#include <dlfcn.h>
#include <mach-o/dyld.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>
#define LOAD(n) __typeof__(&SDL_##n) n = dlsym(lib, "SDL_" #n); if (!n) return NULL

static FILE *out;
static struct timespec started;

static bool rumble(void *unused, Uint16 low, Uint16 high) {
    struct timespec now; clock_gettime(CLOCK_MONOTONIC, &now);
    long ms = (now.tv_sec - started.tv_sec) * 1000 + (now.tv_nsec - started.tv_nsec) / 1000000;
    fprintf(out, "%ld %u %u\n", ms, low, high); fflush(out);
    return true;
}

static void *attach(void *unused) {
    void *lib = NULL;
    for (int t = 0; t < 1200 && !lib; t++) {
        for (uint32_t i = 0; i < _dyld_image_count(); i++) {
            const char *s = _dyld_get_image_name(i);
            if (s && strstr(s, "SDL3") && strstr(s, ".dylib")) { lib = dlopen(s, RTLD_NOLOAD | RTLD_LAZY); if (lib) break; }
        }
        if (!lib) usleep(50000);
    }
    if (!lib) return NULL;
    LOAD(WasInit); LOAD(AttachVirtualJoystick); LOAD(OpenJoystick); LOAD(SetJoystickVirtualButton); LOAD(SetJoystickVirtualAxis);
    while (!WasInit(SDL_INIT_GAMEPAD)) usleep(50000);
    SDL_VirtualJoystickDesc desc = {0}; SDL_INIT_INTERFACE(&desc);
    desc.type = SDL_JOYSTICK_TYPE_GAMEPAD; desc.naxes = 6; desc.nbuttons = 15; desc.button_mask = 0x7fff; desc.axis_mask = 63;
    desc.name = "MoNoon rumble verification pad"; desc.Rumble = rumble;
    clock_gettime(CLOCK_MONOTONIC, &started);
    SDL_JoystickID id = AttachVirtualJoystick(&desc);
    SDL_Joystick *joy = id ? OpenJoystick(id) : NULL;
    if (!joy) return NULL;
    fprintf(out, "attached\n"); fflush(out);
    const char *path = getenv("MONOON_QA_PAD");
    FILE *in = NULL;
    while (path && !in) { in = fopen(path, "r"); if (!in) usleep(50000); }
    char line[96];
    while (in) {
        if (!fgets(line, sizeof line, in)) { clearerr(in); usleep(20000); continue; }
        int index, value; char kind;
        if (sscanf(line, "%c %d %d", &kind, &index, &value) != 3) continue;
        if (kind == 'b') SetJoystickVirtualButton(joy, index, value != 0);
        else if (kind == 'a') SetJoystickVirtualAxis(joy, index, (Sint16)value);
    }
    return NULL;
}

__attribute__((constructor)) static void init(void) {
    const char *path = getenv("MONOON_QA_RUMBLE");
    if (!path || !(out = fopen(path, "a"))) return;
    pthread_t t; if (!pthread_create(&t, NULL, attach, NULL)) pthread_detach(t);
}
