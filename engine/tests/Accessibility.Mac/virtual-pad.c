// GUI test fixture, loaded in the test process only. No driver, physical HID, or product hooks.
#include <SDL3/SDL.h>
#include <dlfcn.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <mach-o/dyld.h>
#include <string.h>
#define LOAD(n) __typeof__(&SDL_##n) n = dlsym(lib, "SDL_" #n); if (!n) return NULL
static void *reader(void *unused) {
    const char *path=getenv("MONOON_QA_PAD");
    if(!path) return NULL;
    void *lib=NULL;
    for(int t=0;t<1200 && !lib;t++){
        for(uint32_t i=0;i<_dyld_image_count();i++){
            const char *s=_dyld_get_image_name(i);
            if(s && strstr(s,"SDL3") && strstr(s,".dylib")){lib=dlopen(s,RTLD_NOLOAD|RTLD_LAZY);if(lib)break;}
        }
        if(!lib)usleep(50000);
    }
    if(!lib)return NULL;
    LOAD(WasInit);LOAD(AttachVirtualJoystick);LOAD(OpenJoystick);LOAD(SetJoystickVirtualButton);LOAD(SetJoystickVirtualAxis);
    while(!WasInit(SDL_INIT_GAMEPAD))usleep(50000);
    SDL_VirtualJoystickDesc desc={0};SDL_INIT_INTERFACE(&desc);desc.type=SDL_JOYSTICK_TYPE_GAMEPAD;desc.naxes=6;desc.nbuttons=15;
    desc.button_mask=0x7fff;desc.axis_mask=63;desc.name="MoNoon UI verification controller";
    SDL_JoystickID id=AttachVirtualJoystick(&desc);SDL_Joystick *joy=id?OpenJoystick(id):NULL;
    if(!joy)return NULL;
    FILE *f=NULL;while(!f){f=fopen(path,"r");if(!f)usleep(50000);}
    char line[96];while(1){
        if(!fgets(line,sizeof line,f)){clearerr(f);usleep(20000);continue;}
        int idx,v;char kind;if(sscanf(line,"%c %d %d",&kind,&idx,&v)!=3)continue;
        if(kind=='b')SetJoystickVirtualButton(joy,idx,v!=0);else if(kind=='a')SetJoystickVirtualAxis(joy,idx,v);
    }
    return NULL;
}
__attribute__((constructor)) static void init(void){if(getenv("MONOON_QA_PAD")){pthread_t t;if(!pthread_create(&t,NULL,reader,NULL))pthread_detach(t);}}
