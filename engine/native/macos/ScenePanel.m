#import <AppKit/AppKit.h>
#import <QuartzCore/CAMetalLayer.h>

// AppKit delivers mouse events to a native child before Avalonia can see them.
// Keep editing and capture state in the managed viewport, using view-local points.
typedef void (*ScenePointer)(int phase, double x, double y, double scroll);
typedef void (*SceneChanged)(void);
typedef int (*SceneFileDrag)(const char *path, double x, double y, int drop);

@interface BrutalScenePanel : NSView
@property(nonatomic, strong) NSTrackingArea *tileTracking;
@property(nonatomic, assign) ScenePointer pointer;
@property(nonatomic, assign) SceneChanged changed;
@property(nonatomic, assign) SceneFileDrag fileDrag;
@property(nonatomic, strong) NSCursor *gameCursor;
@end

@implementation BrutalScenePanel
- (BOOL)acceptsFirstMouse:(NSEvent *)event { return YES; }
- (void)setFrameSize:(NSSize)size {
    [super setFrameSize:size];
    if (self.changed) self.changed();
}
- (void)viewDidMoveToWindow {
    [super viewDidMoveToWindow];
    if (self.changed) self.changed();
}
- (void)viewDidChangeBackingProperties {
    [super viewDidChangeBackingProperties];
    if (self.changed) self.changed();
}
- (void)sendPointer:(NSEvent *)event phase:(int)phase {
    NSPoint p = [self convertPoint:event.locationInWindow fromView:nil];
    double steps = phase == 4 ? event.scrollingDeltaY * (event.hasPreciseScrollingDeltas ? 0.025 : 1.0) : 0;
    if (phase==0 && (event.modifierFlags & (NSEventModifierFlagShift|NSEventModifierFlagCommand|NSEventModifierFlagControl))) phase=10;
    if (self.pointer) self.pointer(phase, p.x, self.bounds.size.height - p.y, steps);
}
- (void)updateTrackingAreas {
    [super updateTrackingAreas];
    if (self.tileTracking) [self removeTrackingArea:self.tileTracking];
    self.tileTracking = [[NSTrackingArea alloc] initWithRect:NSZeroRect options:NSTrackingMouseMoved | NSTrackingMouseEnteredAndExited | NSTrackingActiveInKeyWindow | NSTrackingInVisibleRect owner:self userInfo:nil];
    [self addTrackingArea:self.tileTracking];
}
// Scene Play's custom game cursor. This view receives the mouse, so Avalonia's control cursor does not reach it.
- (void)resetCursorRects { if (self.gameCursor) [self addCursorRect:self.bounds cursor:self.gameCursor]; }
- (void)mouseMoved:(NSEvent *)event   { [self.gameCursor set]; [self sendPointer:event phase:6]; }
- (void)mouseExited:(NSEvent *)event  { [self sendPointer:event phase:7]; }
- (void)mouseDown:(NSEvent *)event    { [self sendPointer:event phase:0]; }
- (void)mouseDragged:(NSEvent *)event { [self sendPointer:event phase:1]; }
- (void)mouseUp:(NSEvent *)event      { [self sendPointer:event phase:2]; }

- (void)rightMouseDown:(NSEvent *)event { [self sendPointer:event phase:3]; }
- (void)rightMouseDragged:(NSEvent *)event { [self sendPointer:event phase:1]; }
- (void)rightMouseUp:(NSEvent *)event { [self sendPointer:event phase:5]; }
- (void)otherMouseDown:(NSEvent *)event { if (event.buttonNumber==2) [self sendPointer:event phase:8]; }
- (void)otherMouseDragged:(NSEvent *)event { [self sendPointer:event phase:1]; }
- (void)otherMouseUp:(NSEvent *)event { if (event.buttonNumber==2) [self sendPointer:event phase:9]; }
- (void)scrollWheel:(NSEvent *)event { [self sendPointer:event phase:4]; }

- (BOOL)handleFileDrag:(id<NSDraggingInfo>)sender drop:(BOOL)drop {
    NSArray<NSURL *> *urls = [sender.draggingPasteboard readObjectsForClasses:@[[NSURL class]]
        options:@{NSPasteboardURLReadingFileURLsOnlyKey: @YES}];
    if (urls.count != 1 || !self.fileDrag) return NO;
    NSURL *url = urls.firstObject;
    if ([url.pathExtension caseInsensitiveCompare:@"png"] != NSOrderedSame) return NO;
    NSPoint p = [self convertPoint:sender.draggingLocation fromView:nil];
    return self.fileDrag(url.fileSystemRepresentation, p.x, self.bounds.size.height - p.y, drop) != 0;
}
- (NSDragOperation)draggingEntered:(id<NSDraggingInfo>)sender {
    return [self handleFileDrag:sender drop:NO] ? NSDragOperationCopy : NSDragOperationNone;
}
- (NSDragOperation)draggingUpdated:(id<NSDraggingInfo>)sender { return [self draggingEntered:sender]; }
- (BOOL)performDragOperation:(id<NSDraggingInfo>)sender { return [self handleFileDrag:sender drop:YES]; }
@end

// Routed Avalonia KeyEventArgs omit AppKit's original timestamp. Read the matching
// event while its key handler is still on the AppKit dispatch stack.
double BrutalKeyEventAge(void) {
    NSEvent *event = NSApp.currentEvent;
    if (!event || event.type != NSEventTypeKeyDown) return 0;
    return MAX(0, NSProcessInfo.processInfo.systemUptime - event.timestamp);
}

void *BrutalCreateScenePanel(ScenePointer pointer, SceneChanged changed, SceneFileDrag fileDrag) {
    BrutalScenePanel *panel = [[BrutalScenePanel alloc] initWithFrame:NSZeroRect];
    panel.wantsLayer = YES;
    panel.pointer = pointer;
    panel.changed = changed;
    panel.fileDrag = fileDrag;
    [panel registerForDraggedTypes:@[NSPasteboardTypeFileURL]];
    return (__bridge_retained void *)panel;
}

void BrutalDestroyScenePanel(void *handle) {
    BrutalScenePanel *panel = (__bridge_transfer BrutalScenePanel *)handle;
    panel.pointer = NULL;
    panel.changed = NULL;
    panel.fileDrag = NULL;
    [panel removeFromSuperview];
}

// rgba: straight RGBA rows; NULL restores the arrow. The image is one point per pixel, like an SDL color cursor.
void BrutalSetScenePanelCursor(void *handle, const unsigned char *rgba, int width, int height, int hotX, int hotY) {
    BrutalScenePanel *panel = (__bridge BrutalScenePanel *)handle;
    NSCursor *cursor = nil;
    if (rgba) {
        NSBitmapImageRep *rep = [[NSBitmapImageRep alloc] initWithBitmapDataPlanes:NULL pixelsWide:width pixelsHigh:height
            bitsPerSample:8 samplesPerPixel:4 hasAlpha:YES isPlanar:NO colorSpaceName:NSDeviceRGBColorSpace
            bitmapFormat:NSBitmapFormatAlphaNonpremultiplied bytesPerRow:width * 4 bitsPerPixel:32];
        memcpy(rep.bitmapData, rgba, (size_t)width * height * 4);
        NSImage *image = [[NSImage alloc] initWithSize:NSMakeSize(width, height)];
        [image addRepresentation:rep];
        cursor = [[NSCursor alloc] initWithImage:image hotSpot:NSMakePoint(hotX, hotY)];
    }
    panel.gameCursor = cursor;
    [panel.window invalidateCursorRectsForView:panel];
    NSPoint mouse = panel.window ? [panel convertPoint:panel.window.mouseLocationOutsideOfEventStream fromView:nil] : NSZeroPoint;
    if (panel.window && NSPointInRect(mouse, panel.bounds)) [cursor ?: NSCursor.arrowCursor set];
}

int BrutalScenePanelAttached(void *handle) {
    NSView *panel = (__bridge NSView *)handle;
    return panel.window != nil && !NSIsEmptyRect(panel.bounds);
}

// A panel can resize independently of its NSWindow. SDL normally updates its
// drawable on window events; the embedded target must follow its own view.
int BrutalSizeSceneDrawable(void *handle, void *metalLayer, double panelWidth, double panelHeight, double *width, double *height) {
    NSView *panel = (__bridge NSView *)handle;
    CAMetalLayer *layer = (__bridge CAMetalLayer *)metalLayer;
    if (!panel.window || !layer || NSIsEmptyRect(panel.bounds)) return 0;
    NSView *metalView = nil;
    for (NSView *child in panel.subviews) {
        if (child.layer == layer) { metalView = child; break; }
    }
    if (!metalView) return 0;
    // Standalone SDL windows declare sRGB. The editor owns its NSWindow, so
    // tag only the embedded SDL drawable, leaving Avalonia's other panels alone.
    CGColorSpaceRef srgb = NSColorSpace.sRGBColorSpace.CGColorSpace;
    if (!layer.colorspace || !CFEqual(layer.colorspace, srgb)) layer.colorspace = srgb;
    // Avalonia can lay out a smaller Scene before its Cocoa host is attached.
    // Align the private host and child with the arranged size so neither covers
    // the adjacent Inspector or intercepts its mouse events.
    NSSize arranged = NSMakeSize(panelWidth, panelHeight);
    NSView *host = panel.superview;
    if (host && host != panel.window.contentView && host.subviews.count == 1 &&
        !NSEqualSizes(host.frame.size, arranged)) {
        [host setFrameSize:arranged];
    }
    if (!NSEqualSizes(panel.frame.size, arranged)) [panel setFrameSize:arranged];
    NSRect bounds = panel.bounds;
    if (!NSEqualRects(metalView.frame, bounds)) metalView.frame = bounds;
    CGSize pixels = [panel convertRectToBacking:bounds].size;
    if (pixels.width < 1 || pixels.height < 1) return 0;
    if (!CGSizeEqualToSize(layer.drawableSize, pixels)) {
        layer.contentsScale = panel.window.backingScaleFactor;
        layer.drawableSize = pixels;
    }
    *width = pixels.width;
    *height = pixels.height;
    return 1;
}
