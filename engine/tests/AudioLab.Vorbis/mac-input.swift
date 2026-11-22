import AppKit
import CoreGraphics
import Carbon.HIToolbox

let args = CommandLine.arguments
guard args.count >= 3, let pid = pid_t(args[2]),
      let app = NSRunningApplication(processIdentifier: pid) else { exit(1) }

func visibleWindows() -> [[String: Any]] {
    CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID)
        as? [[String: Any]] ?? []
}
func owner(_ info: [String: Any]) -> pid_t? { info[kCGWindowOwnerPID as String] as? Int32 }
func bounds(_ info: [String: Any]) -> CGRect? {
    guard let data = info[kCGWindowBounds as String] as? [String: Any] else { return nil }
    return CGRect(dictionaryRepresentation: data as CFDictionary)
}
func focused() -> Bool { NSWorkspace.shared.frontmostApplication?.processIdentifier == pid }

guard let window = visibleWindows().first(where: {
    owner($0) == pid && $0[kCGWindowLayer as String] as? Int == 0 && (bounds($0)?.width ?? 0) >= 100 && (bounds($0)?.height ?? 0) >= 100
}), let id = window[kCGWindowNumber as String] as? Int, let rect = bounds(window) else { exit(1) }

switch args[1] {
case "geometry":
    let display = NSScreen.screens.first { screen in
        guard let number = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber else { return false }
        return CGDisplayBounds(number.uint32Value).contains(CGPoint(x: rect.midX, y: rect.midY))
    }
    guard let screen = display else { exit(2) }
    print(id, Int(rect.width), Int(rect.height), Int(screen.frame.width), Int(screen.frame.height), Int(screen.safeAreaInsets.top))
case "resize":
    guard args.count == 5, let w = Double(args[3]), let h = Double(args[4]) else { exit(2) }
    var target: CFTypeRef?
    let root = AXUIElementCreateApplication(pid)
    guard AXUIElementCopyAttributeValue(root, kAXFocusedWindowAttribute as CFString, &target) == .success,
          let target else { exit(2) }
    var size = CGSize(width: w, height: h)
    guard let value = AXValueCreate(.cgSize, &size),
          AXUIElementSetAttributeValue(target as! AXUIElement, kAXSizeAttribute as CFString, value) == .success else { exit(2) }
case "focus":
    guard CGPreflightPostEventAccess() else { fputs("No keyboard event permission\n", stderr); exit(2) }
    // A freshly mapped SDL window can appear before Launch Services grants it focus.
    // Pump the run loop so NSWorkspace receives activation changes.
    let deadline = Date().addingTimeInterval(10)
    let point = CGPoint(x: rect.midX, y: rect.midY)
    while !focused() && Date() < deadline {
        app.activate(options: [.activateAllWindows])
        RunLoop.current.run(until: Date().addingTimeInterval(0.25))
        if !focused(), let hit = visibleWindows().first(where: { bounds($0)?.contains(point) == true }),
           owner(hit) == pid {
            for type in [CGEventType.leftMouseDown, .leftMouseUp] {
                CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: point,
                        mouseButton: .left)?.post(tap: .cghidEventTap)
                RunLoop.current.run(until: Date().addingTimeInterval(0.08))
            }
        }
    }
    guard focused() else { fputs("Game window did not receive focus\n", stderr); exit(3) }
    print(id)
case "click", "click-window":
    guard args.count == 5, let x = Double(args[3]), let y = Double(args[4]), focused() else { exit(2) }
    // AudioLab initially has an 800 × 450 pt client area below the native title bar.
    let point = CGPoint(x: rect.minX + x, y: (args[1] == "click-window" ? rect.minY : rect.maxY - 450) + y)
    guard let hit = visibleWindows().first(where: { $0[kCGWindowLayer as String] as? Int == 0 && bounds($0)?.contains(point) == true }), owner(hit) == pid else { exit(3) }
    CGWarpMouseCursorPosition(point)
    for type in [CGEventType.mouseMoved, .leftMouseDown, .leftMouseUp] {
        guard let event = CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: point,
                                  mouseButton: .left) else { exit(2) }
        event.setIntegerValueField(.mouseEventClickState, value: 1)
        event.post(tap: .cghidEventTap)
        Thread.sleep(forTimeInterval: 0.12)
    }
case "f", "Escape", "Tab", "Left", "Down", "Return", "Space":
    guard focused() else { fputs("Game window lost focus before key input\n", stderr); exit(3) }
    let keys = ["f": kVK_ANSI_F, "Escape": kVK_Escape, "Tab": kVK_Tab, "Left": kVK_LeftArrow,
                "Down": kVK_DownArrow, "Return": kVK_Return, "Space": kVK_Space]
    let key = CGKeyCode(keys[args[1]]!)
    for down in [true, false] {
        guard let event = CGEvent(keyboardEventSource: nil, virtualKey: key, keyDown: down) else { exit(2) }
        event.flags = []
        event.postToPid(pid)
        Thread.sleep(forTimeInterval: 0.12)
    }
default: exit(2)
}
