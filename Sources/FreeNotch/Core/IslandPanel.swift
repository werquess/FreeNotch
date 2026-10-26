import AppKit
import SwiftUI

final class IslandPanel: NSPanel {
    private let allowsKey: Bool
    var onCancel: (() -> Void)?

    init(frame: NSRect, level: NSWindow.Level, allowsKey: Bool) {
        self.allowsKey = allowsKey
        super.init(contentRect: frame, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        self.level = level
        collectionBehavior = [.canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle]
        isMovable = false
        isReleasedWhenClosed = false
        hidesOnDeactivate = false
        becomesKeyOnlyIfNeeded = true
        animationBehavior = .none
    }

    override var canBecomeKey: Bool { allowsKey }
    override var canBecomeMain: Bool { false }

    override func cancelOperation(_ sender: Any?) {
        onCancel?()
    }
}

final class IslandHostingView<Content: View>: NSHostingView<Content> {
    var onEnter: (() -> Void)?
    var onExit: (() -> Void)?
    var onOutsideClick: (() -> Void)?

    var interactiveSize: CGSize = .zero {
        didSet { if oldValue != interactiveSize { updateTrackingAreas() } }
    }

    private var islandArea: NSTrackingArea?

    var interactiveRect: CGRect {
        guard interactiveSize != .zero else { return .zero }
        let x = (bounds.width - interactiveSize.width) / 2
        let y = isFlipped ? 0 : bounds.height - interactiveSize.height
        return CGRect(x: x, y: y, width: interactiveSize.width, height: interactiveSize.height)
    }

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let islandArea { removeTrackingArea(islandArea) }
        islandArea = nil
        let rect = interactiveRect
        guard rect != .zero else { return }
        let area = NSTrackingArea(
            rect: rect,
            options: [.mouseEnteredAndExited, .activeAlways, .assumeInside],
            owner: self
        )
        addTrackingArea(area)
        islandArea = area
    }

    override func mouseEntered(with event: NSEvent) {
        if event.trackingArea === islandArea { onEnter?() } else { super.mouseEntered(with: event) }
    }

    override func mouseExited(with event: NSEvent) {
        if event.trackingArea === islandArea { onExit?() } else { super.mouseExited(with: event) }
    }

    override func hitTest(_ point: NSPoint) -> NSView? {
        let rect = interactiveRect
        if rect != .zero, !rect.contains(convert(point, from: superview)) { return self }
        return super.hitTest(point)
    }

    override func mouseDown(with event: NSEvent) {
        let rect = interactiveRect
        if rect != .zero, !rect.contains(convert(event.locationInWindow, from: nil)) {
            onOutsideClick?()
            return
        }
        super.mouseDown(with: event)
    }
}

final class HoverSensorView: NSView {
    var onEnter: (() -> Void)?
    var onExit: (() -> Void)?
    var onClick: (() -> Void)?
    var onDragEnter: (() -> Void)?

    private var area: NSTrackingArea?

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        registerForDraggedTypes([.fileURL, .URL, .string])
    }

    required init?(coder: NSCoder) { fatalError() }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let area { removeTrackingArea(area) }
        let a = NSTrackingArea(rect: bounds, options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect], owner: self)
        addTrackingArea(a)
        area = a
    }

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func mouseEntered(with event: NSEvent) { onEnter?() }
    override func mouseExited(with event: NSEvent) { onExit?() }
    override func mouseDown(with event: NSEvent) { onClick?() }

    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        onDragEnter?()
        return .copy
    }
}
