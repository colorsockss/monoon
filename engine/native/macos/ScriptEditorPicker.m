#import <AppKit/AppKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#include <string.h>

// The storage provider returns ordinary files. A macOS editor is usually a directory bundle.
char *BrutalChooseScriptEditor(void) {
    NSOpenPanel *panel = [NSOpenPanel openPanel];
    panel.title = @"Choose a script editor";
    panel.directoryURL = [NSURL fileURLWithPath:@"/Applications" isDirectory:YES];
    panel.allowedContentTypes = @[UTTypeApplicationBundle, UTTypeUnixExecutable];
    panel.canChooseDirectories = NO;
    panel.treatsFilePackagesAsDirectories = NO;
    if ([panel runModal] != NSModalResponseOK) return NULL;
    return strdup(panel.URL.fileSystemRepresentation);
}
