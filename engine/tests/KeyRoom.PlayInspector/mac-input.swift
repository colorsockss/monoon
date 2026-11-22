import AppKit
import CoreGraphics

let args = CommandLine.arguments
guard args.count >= 3, let pid = pid_t(args[1]), let app = NSRunningApplication(processIdentifier: pid) else { exit(2) }
app.activate(options: [.activateAllWindows])
let deadline = Date().addingTimeInterval(5)
while NSWorkspace.shared.frontmostApplication?.processIdentifier != pid && Date() < deadline {
    RunLoop.current.run(until: Date().addingTimeInterval(0.05))
}
guard NSWorkspace.shared.frontmostApplication?.processIdentifier == pid else { exit(3) }
let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
guard let w = windows.first(where: { $0[kCGWindowLayer as String] as? Int == 0 && $0[kCGWindowOwnerPID as String] as? Int32 == pid }),
      let id = w[kCGWindowNumber as String] as? Int else { exit(4) }
switch args[2] {
case "id": print(id)
case "click":
    guard args.count == 5, let x = Double(args[3]), let y = Double(args[4]) else { exit(2) }
    let point = CGPoint(x: x, y: y)
    guard let hit = windows.first(where: { win in
        guard win[kCGWindowLayer as String] as? Int == 0, let b = win[kCGWindowBounds as String] as? [String: Any] else { return false }
        return CGRect(dictionaryRepresentation: b as CFDictionary)?.contains(point) == true
    }), hit[kCGWindowOwnerPID as String] as? Int32 == pid else { exit(5) }
    for type in [CGEventType.mouseMoved, .leftMouseDown, .leftMouseUp] {
        let event = CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: point, mouseButton: .left)!
        event.setIntegerValueField(.mouseEventClickState, value: 1)
        event.post(tap: .cghidEventTap)
        Thread.sleep(forTimeInterval: 0.08)
    }
default:
    let keys: [String: CGKeyCode] = ["a": 0, "i": 34, "s": 1, "z": 6, "0": 29, "2": 19, "3": 20, "8": 28, "Return": 36]
    guard let key = keys[args[2]] else { exit(2) }
    for down in [true, false] {
        let e = CGEvent(keyboardEventSource: nil, virtualKey: key, keyDown: down)!
        e.flags = args.last == "cmd" ? .maskCommand : []
        e.post(tap: .cghidEventTap)
        Thread.sleep(forTimeInterval: 0.09)
    }
}
