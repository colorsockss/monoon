import AppKit
import CoreGraphics
import Carbon.HIToolbox

// Address the spawned SDL process and its on-screen window.
let args = CommandLine.arguments
guard args.count == 3, let pid = pid_t(args[2]),
      let app = NSRunningApplication(processIdentifier: pid) else { exit(1) }
let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID)
    as? [[String: Any]] ?? []
guard let window = windows.first(where: { entry in
    entry[kCGWindowOwnerPID as String] as? Int32 == pid && entry[kCGWindowLayer as String] as? Int == 0
}), let id = window[kCGWindowNumber as String] as? Int else { exit(1) }
if args[1] == "window" { print(id); exit(0) }
let key: CGKeyCode
switch args[1] {
case "f": key = CGKeyCode(kVK_ANSI_F)
case "Escape": key = CGKeyCode(kVK_Escape)
default: exit(2)
}
if !app.isActive {
    app.activate(options: [.activateIgnoringOtherApps])
    Thread.sleep(forTimeInterval: 0.3)
}
guard app.isActive else { fatalError("AudioLab did not acquire focus") }
guard CGPreflightPostEventAccess() else { fatalError("Keyboard event permission is unavailable") }
for down in [true, false] {
    guard let event = CGEvent(keyboardEventSource: nil, virtualKey: key, keyDown: down) else { exit(2) }
    event.flags = []
    event.postToPid(pid)
    Thread.sleep(forTimeInterval: 0.12)
}
