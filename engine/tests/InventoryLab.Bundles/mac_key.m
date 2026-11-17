#import <AppKit/AppKit.h>
#import <CoreGraphics/CoreGraphics.h>
#include <stdlib.h>
#include <unistd.h>

// Only deliver keyboard input to the requested, frontmost SDL app.
int main(int argc, const char **argv) {
    @autoreleasepool {
        if (argc != 3) return 2;
        pid_t pid = atoi(argv[1]);
        NSRunningApplication *app = [NSRunningApplication runningApplicationWithProcessIdentifier:pid];
        if (!app) return 3;
        if (NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier != pid) {
            [app activateWithOptions:NSApplicationActivateIgnoringOtherApps];
            for (int i = 0; i < 100 && NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier != pid; ++i)
                [NSRunLoop.currentRunLoop runUntilDate:[NSDate dateWithTimeIntervalSinceNow:.01]];
        }
        if (NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier != pid) return 4;
        CGKeyCode key = (CGKeyCode)atoi(argv[2]);
        for (int i = 0; i < 2; ++i) {
            CGEventRef event = CGEventCreateKeyboardEvent(NULL, key, i == 0);
            CGEventSetFlags(event, 0);
            CGEventPost(kCGHIDEventTap, event);
            CFRelease(event);
            if (i == 0) usleep(55000);
        }
        return 0;
    }
}
