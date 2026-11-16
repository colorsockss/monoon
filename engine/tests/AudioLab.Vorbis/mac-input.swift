import AppKit
import CoreGraphics
import Carbon.HIToolbox

let args = CommandLine.arguments
guard args.count == 3, let pid = pid_t(args[2]),
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
    owner($0) == pid && $0[kCGWindowLayer as String] as? Int == 0 && (bounds($0)?.width ?? 0) >= 100
}), let id = window[kCGWindowNumber as String] as? Int, let rect = bounds(window) else { exit(1) }

switch args[1] {
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
    guard focused() else { fputs("AudioLab window did not receive focus\n", stderr); exit(3) }
    print(id)
case "f", "Escape":
    guard focused() else { fputs("AudioLab lost focus before key input\n", stderr); exit(3) }
    let key = CGKeyCode(args[1] == "f" ? kVK_ANSI_F : kVK_Escape)
    for down in [true, false] {
        guard let event = CGEvent(keyboardEventSource: nil, virtualKey: key, keyDown: down) else { exit(2) }
        event.flags = []
        event.postToPid(pid)
        Thread.sleep(forTimeInterval: 0.12)
    }
default: exit(2)
}
