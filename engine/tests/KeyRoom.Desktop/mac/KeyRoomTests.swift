import XCTest
import CryptoKit

final class KeyRoomTests: XCTestCase {
    private func path(_ name: String) throws -> URL {
        URL(fileURLWithPath: try XCTUnwrap(ProcessInfo.processInfo.environment[name], "Missing \(name)"))
    }

    private func waitFor(_ description: String, until condition: @escaping () -> Bool) {
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in condition() }, object: nil)
        XCTAssertEqual(XCTWaiter.wait(for: [ready], timeout: 20), .completed, description)
    }

    private func keep(_ attachment: XCTAttachment, as name: String) {
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    private func checkPlay(_ app: XCUIApplication, phase: URL, name: String, expected: Data?) throws {
        let play = app.buttons["▶  Play"]
        let stop = app.buttons["■  Stop"]
        play.click()
        XCTAssertTrue(stop.waitForExistence(timeout: 20), "Scene Play did not start")
        let profile = phase.appendingPathComponent("profile")
        let slot = profile.appendingPathComponent("Play/64fb4cd624695cecba6d9a49e61f42e7/slot-001.save")
        let keyFlag = expected == nil ? "  key: false" : "  key: true"
        let deadline = Date().addingTimeInterval(12)
        var saved = Data()
        repeat {
            app.typeKey(XCUIKeyboardKey.F5.rawValue, modifierFlags: [])
            if let data = try? Data(contentsOf: slot) { saved = data }
            if String(decoding: saved, as: UTF8.self).components(separatedBy: .newlines).contains(keyFlag) { break }
            Thread.sleep(forTimeInterval: 0.25)
        } while Date() < deadline
        let evidence = phase.appendingPathComponent(name + ".save")
        keep(XCTAttachment(data: saved, uniformTypeIdentifier: "public.data"), as: "\(phase.lastPathComponent)-\(name).save")
        XCTAssertTrue(String(decoding: saved, as: UTF8.self).components(separatedBy: .newlines).contains(keyFlag),
                      "Expected \(keyFlag) in \(evidence.path)")
        if let expected {
            let digest = SHA256.hash(data: saved).map { String(format: "%02x", $0) }.joined()
            XCTAssertEqual(saved, expected, "\(evidence.path): \(saved.count) bytes SHA-256 \(digest)")
            print("PASS \(phase.lastPathComponent)/\(name).save: \(saved.count) bytes SHA-256 \(digest)")
        }
        app.typeKey("i", modifierFlags: [])
        keep(XCTAttachment(screenshot: app.screenshot()), as: "\(phase.lastPathComponent)-\(name)-inventory")
        stop.click()
        XCTAssertTrue(play.waitForExistence(timeout: 20), "Scene Play did not stop")
    }

    func testEditAndReopenKeyRoom() throws {
        continueAfterFailure = false
        let project = try path("MONOON_TEST_PROJECT")
        let results = try path("MONOON_TEST_RESULTS")
        let reference = try Data(contentsOf: path("MONOON_TEST_REFERENCE"))
        let scene = project.appendingPathComponent("Scenes/KeyRoom.scene")
        let app = XCUIApplication(url: try path("MONOON_TEST_APP"))
        defer { if app.state != .notRunning { app.terminate() } }

        func playerAt(_ x: Int) -> Bool {
            let yaml = try? String(contentsOf: scene, encoding: .utf8)
            return yaml?.contains("\n  name: Player\n  x: \(x)\n") == true
        }

        for name in ["baseline", "edit", "reopen"] {
            let phase = results.appendingPathComponent(name)
            let profile = phase.appendingPathComponent("profile")
            XCTAssertTrue(playerAt(name == "reopen" ? 190 : 274), "Unexpected Player X before launch")
            app.launchArguments = [project.path]
            app.launchEnvironment = ["MONOON_USER_DATA": profile.path, "SDL_AUDIO_DRIVER": "dummy"]
            app.launch()
            let xField = app.descendants(matching: .any)["Position X"].firstMatch
            XCTAssertTrue(xField.waitForExistence(timeout: 30), "Player's Transform Inspector did not open")
            if name == "edit" {
                xField.coordinate(withNormalizedOffset: CGVector(dx: 0.2, dy: 0.5)).doubleClick()
                app.typeText("190")
                app.typeKey(XCUIKeyboardKey.return.rawValue, modifierFlags: [])
                keep(XCTAttachment(screenshot: app.screenshot()), as: "entered-position")
                app.menuBars.menuBarItems["File"].click()
                app.menuItems["Save Scene"].click()
                waitFor("Save Scene persisted Player X=190") { playerAt(190) }
            }
            keep(XCTAttachment(screenshot: app.screenshot()), as: "\(name)-inspector")
            try checkPlay(app, phase: phase, name: name == "baseline" ? "original" : "edited",
                          expected: name == "baseline" ? nil : reference)
            app.terminate()
            XCTAssertTrue(app.wait(for: .notRunning, timeout: 15), "Editor process did not exit")
        }
    }
}
