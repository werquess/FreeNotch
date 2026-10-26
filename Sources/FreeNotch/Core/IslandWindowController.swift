import AppKit
import SwiftUI

@MainActor
final class IslandWindowController {
    private let env: AppEnvironment
    private var vm: IslandViewModel { env.island }
    private let panel: IslandPanel
    private let sensor: IslandPanel
    private let hosting: IslandHostingView<IslandScene>
    private let sensorView = HoverSensorView(frame: .zero)
    private let dragMonitor = DragMonitor()
    private var clickMonitor: Any?
    private var lastMode: IslandViewModel.Mode = .compact
    private var lastCanvas: CGSize = .zero

    init(env: AppEnvironment) {
        self.env = env
        let base = NSWindow.Level.mainMenu.rawValue
        panel = IslandPanel(frame: .zero, level: NSWindow.Level(rawValue: base + 3), allowsKey: true)
        sensor = IslandPanel(frame: .zero, level: NSWindow.Level(rawValue: base + 2), allowsKey: false)
        hosting = IslandHostingView(rootView: IslandScene(env: env))
        hosting.sizingOptions = []

        panel.contentView = hosting
        panel.ignoresMouseEvents = true
        sensor.contentView = sensorView
        sensor.ignoresMouseEvents = false

        wireEvents()
        layoutWindows()
        panel.orderFrontRegardless()
        sensor.orderFrontRegardless()
        observeState()
        dragMonitor.start()

        NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.vm.refreshMetrics()
                self?.layoutWindows()
            }
        }
    }

    private func wireEvents() {
        sensorView.onEnter = { [weak self] in self?.vm.sensorEntered() }
        sensorView.onExit = { [weak self] in self?.vm.sensorExited() }
        sensorView.onClick = { [weak self] in self?.vm.expand() }
        sensorView.onDragEnter = { [weak self] in self?.vm.dragBegan() }

        hosting.onEnter = { [weak self] in self?.vm.panelEntered() }
        hosting.onExit = { [weak self] in self?.vm.panelExited() }
        hosting.onOutsideClick = { [weak self] in
            guard let self, self.vm.mode == .expanded else { return }
            self.vm.collapse()
        }
        panel.onCancel = { [weak self] in
            if Prefs.bool(.escCloses) { self?.vm.collapse() }
        }

        dragMonitor.onBegin = { [weak self] in self?.vm.dragBegan() }
        dragMonitor.onEnd = { [weak self] in

            DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) { self?.vm.dragEnded() }
        }
    }

    private func observeState() {
        withObservationTracking {
            _ = vm.mode
            _ = vm.isDragActive
            _ = vm.currentSize
            _ = vm.canvasSize
        } onChange: { [weak self] in
            DispatchQueue.main.async {
                MainActor.assumeIsolated {
                    self?.applyState()
                    self?.observeState()
                }
            }
        }
    }

    private func applyState() {
        if vm.canvasSize != lastCanvas { layoutWindows() }

        let expanded = vm.mode == .expanded
        let interactive = expanded || vm.isDragActive
        panel.ignoresMouseEvents = !interactive
        layoutSensor()

        hosting.interactiveSize = interactive ? vm.currentSize : .zero
        if expanded { installClickMonitor() } else { removeClickMonitor() }

        if lastMode == .expanded, !expanded, panel.isKeyWindow {
            panel.orderOut(nil)
            panel.orderFrontRegardless()
        }
        lastMode = vm.mode
    }

    private func layoutWindows() {
        guard let screen = NSScreen.islandScreen else { return }
        let canvas = vm.canvasSize
        lastCanvas = canvas
        let frame = NSRect(
            x: screen.frame.midX - canvas.width / 2,
            y: screen.frame.maxY - canvas.height,
            width: canvas.width,
            height: canvas.height
        )
        panel.setFrame(frame, display: true)
        layoutSensor()
    }

    private func layoutSensor() {
        guard let screen = NSScreen.islandScreen else { return }

        let size = vm.mode == .expanded || vm.isDragActive ? vm.size(for: .idle) : vm.currentSize
        let frame = NSRect(
            x: screen.frame.midX - size.width / 2,
            y: screen.frame.maxY - size.height,
            width: size.width,
            height: size.height
        )
        if sensor.frame != frame { sensor.setFrame(frame, display: false) }
    }

    private func installClickMonitor() {
        guard clickMonitor == nil else { return }
        clickMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] _ in
            MainActor.assumeIsolated { self?.vm.collapse() }
        }
    }

    private func removeClickMonitor() {
        if let clickMonitor { NSEvent.removeMonitor(clickMonitor) }
        clickMonitor = nil
    }
}
