import AppKit

@MainActor
final class DragMonitor {
    var onBegin: (() -> Void)?
    var onEnd: (() -> Void)?

    private var monitors: [Any] = []
    private var baseline = NSPasteboard(name: .drag).changeCount
    private var active = false
    private var endTimer: DispatchSourceTimer?

    func start() {
        guard monitors.isEmpty else { return }
        if let down = NSEvent.addGlobalMonitorForEvents(matching: .leftMouseDown, handler: { [weak self] _ in
            MainActor.assumeIsolated { self?.baseline = NSPasteboard(name: .drag).changeCount }
        }) { monitors.append(down) }

        if let drag = NSEvent.addGlobalMonitorForEvents(matching: .leftMouseDragged, handler: { [weak self] _ in
            MainActor.assumeIsolated { self?.checkDrag() }
        }) { monitors.append(drag) }
    }

    func stop() {
        monitors.forEach(NSEvent.removeMonitor)
        monitors.removeAll()
        finish()
    }

    private func checkDrag() {
        guard !active else { return }
        let pb = NSPasteboard(name: .drag)
        guard pb.changeCount != baseline else { return }
        let types = pb.types ?? []
        guard types.contains(.fileURL) || types.contains(NSPasteboard.PasteboardType("NSFilenamesPboardType")) else { return }
        active = true
        onBegin?()
        watchForRelease()
    }

    private func watchForRelease() {
        endTimer?.cancel()
        let t = DispatchSource.makeTimerSource(queue: .main)
        t.schedule(deadline: .now() + 0.25, repeating: .milliseconds(200), leeway: .milliseconds(50))
        t.setEventHandler { [weak self] in
            MainActor.assumeIsolated {
                if NSEvent.pressedMouseButtons & 1 == 0 { self?.finish() }
            }
        }
        t.resume()
        endTimer = t
    }

    private func finish() {
        endTimer?.cancel()
        endTimer = nil
        guard active else { return }
        active = false
        baseline = NSPasteboard(name: .drag).changeCount
        onEnd?()
    }
}
