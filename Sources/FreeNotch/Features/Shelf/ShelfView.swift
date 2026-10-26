import AppKit
import SwiftUI
import UniformTypeIdentifiers

struct ShelfView: View {
    @Environment(ShelfStore.self) private var shelf
    @Environment(Appearance.self) private var appearance
    @State private var airDropTargeted = false
    @State private var hovered: URL?
    @State private var frames: [URL: CGRect] = [:]

    var body: some View {
        HStack(spacing: 10) {
            if shelf.items.isEmpty {
                emptyDropZone
                    .transition(.opacity)
            } else {
                itemsStrip
                    .transition(.opacity)
            }
            airDropZone
        }
        .animation(Motion.tab, value: shelf.items.isEmpty)
    }

    private var emptyDropZone: some View {
        RoundedRectangle(cornerRadius: Theme.cardRadius, style: .continuous)
            .strokeBorder(.white.opacity(0.22), style: StrokeStyle(lineWidth: 1.5, dash: [6, 5]))
            .overlay {
                VStack(spacing: 8) {
                    Image(systemName: "tray.and.arrow.down.fill")
                        .font(.system(size: 26))
                        .foregroundStyle(.white.opacity(0.5))
                    Text(String(localized: "Drag files here"))
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.white.opacity(0.6))
                    Text(String(localized: "The shelf holds files until you drag them somewhere"))
                        .font(.system(size: 11))
                        .foregroundStyle(.white.opacity(0.35))
                }
            }
    }

    private var itemsStrip: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(String(localized: "\(shelf.items.count) items"))
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(Theme.secondary)
                    .contentTransition(.numericText())
                Spacer()
                Text(String(localized: "Drag to move all · ⌘ for one"))
                    .font(.system(size: 10))
                    .foregroundStyle(Theme.tertiary)
                Button(String(localized: "Clear")) { withAnimation(Motion.tab) { shelf.clear() } }
                    .buttonStyle(.plain)
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(Theme.secondary)
            }
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 6) {
                    ForEach(shelf.items) { item in
                        ShelfTile(item: item,
                                  image: shelf.icon(for: item),
                                  tint: shelf.tints[item.url].map(Color.init(nsColor:)) ?? appearance.accent,
                                  hovered: hovered == item.url,
                                  dimmed: shelf.isDraggingOut)
                            .background {
                                GeometryReader { geo in
                                    Color.clear.preference(key: TileFramesKey.self,
                                                           value: [item.url: geo.frame(in: .named("shelfStrip"))])
                                }
                            }
                            .transition(.asymmetric(insertion: .scale(scale: 0.4).combined(with: .opacity),
                                                    removal: .scale(scale: 0.6).combined(with: .opacity)))
                    }
                }
                .padding(4)
                .frame(maxWidth: .infinity, alignment: .leading)
                .coordinateSpace(name: "shelfStrip")
                .onPreferenceChange(TileFramesKey.self) { frames = $0 }
                .overlay {
                    ShelfDragSource(
                        entries: shelf.items.compactMap { item in
                            frames[item.url].map { ShelfDragSource.Entry(url: item.url, frame: $0, image: shelf.icon(for: item)) }
                        },
                        onHover: { url in withAnimation(.spring(response: 0.25, dampingFraction: 0.75)) { hovered = url } },
                        onDraggingChange: { dragging in
                            withAnimation(.spring(response: 0.35, dampingFraction: 0.8)) { shelf.isDraggingOut = dragging }
                        },
                        onOpen: { url in shelf.items.first { $0.url == url }.map(shelf.open) },
                        onReveal: { url in shelf.items.first { $0.url == url }.map(shelf.reveal) },
                        onAirDrop: { url in shelf.airDrop(shelf.items.filter { $0.url == url }) },
                        onRemove: { url in
                            withAnimation(Motion.tab) { shelf.items.first { $0.url == url }.map(shelf.remove) }
                        }
                    )
                }
                .animation(.spring(response: 0.38, dampingFraction: 0.78), value: shelf.items)
            }
        }
        .padding(10)
        .islandCard()
    }

    private var airDropZone: some View {
        Button { shelf.airDrop() } label: {
            VStack(spacing: 8) {
                Image(systemName: "airplayaudio")
                    .font(.system(size: 26, weight: .medium))
                    .symbolEffect(.pulse, isActive: airDropTargeted)
                Text(verbatim: "AirDrop")
                    .font(.system(size: 12, weight: .semibold))
            }
            .foregroundStyle(airDropTargeted ? .black : .white)
            .frame(width: 96)
            .frame(maxHeight: .infinity)
            .background(
                RoundedRectangle(cornerRadius: Theme.cardRadius, style: .continuous)
                    .fill(airDropTargeted ? Color.cyan : Color.black)
            )
            .overlay(
                RoundedRectangle(cornerRadius: Theme.cardRadius, style: .continuous)
                    .strokeBorder(Theme.hairline, lineWidth: 1)
            )
            .scaleEffect(airDropTargeted ? 1.04 : 1)
            .animation(.spring(response: 0.3, dampingFraction: 0.7), value: airDropTargeted)
        }
        .buttonStyle(.plain)
        .help(String(localized: "Send the shelf with AirDrop, or drop a file right here"))
        .onDrop(of: [.fileURL], isTargeted: $airDropTargeted) { providers in
            URLLoader.load(providers) { urls in
                guard let service = NSSharingService(named: .sendViaAirDrop) else { return }
                service.perform(withItems: urls)
            }
            return true
        }
    }
}

private struct TileFramesKey: PreferenceKey {
    static var defaultValue: [URL: CGRect] = [:]
    static func reduce(value: inout [URL: CGRect], nextValue: () -> [URL: CGRect]) {
        value.merge(nextValue()) { $1 }
    }
}

private struct ShelfTile: View {
    let item: ShelfStore.Item
    let image: NSImage
    let tint: Color
    let hovered: Bool
    let dimmed: Bool

    var body: some View {
        VStack(spacing: 5) {
            ZStack {
                Circle()
                    .fill(tint.opacity(hovered ? 0.55 : 0.32))
                    .frame(width: 44, height: 44)
                    .blur(radius: 14)
                Image(nsImage: image)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: 50, height: 50)
                    .shadow(color: .black.opacity(0.35), radius: 4, y: 2)
                    .scaleEffect(hovered ? 1.08 : 1)
                    .rotationEffect(.degrees(hovered ? -2 : 0))
            }
            .frame(width: 56, height: 54)
            Text(item.name)
                .font(.system(size: 10, weight: .medium))
                .lineLimit(1)
                .truncationMode(.middle)
                .frame(width: 66)
                .foregroundStyle(.white.opacity(0.85))
        }
        .padding(.vertical, 6)
        .padding(.horizontal, 3)
        .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(.white.opacity(hovered ? 0.09 : 0)))
        .overlay(alignment: .topTrailing) {
            if hovered && !dimmed {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 13))
                    .foregroundStyle(.white, Color(white: 0.3))
                    .transition(.scale.combined(with: .opacity))
            }
        }
        .opacity(dimmed ? 0.3 : 1)
        .scaleEffect(dimmed ? 0.9 : 1)
        .blur(radius: dimmed ? 1.5 : 0)
    }
}

struct ShelfDragSource: NSViewRepresentable {
    struct Entry: Equatable {
        let url: URL
        let frame: CGRect
        let image: NSImage
    }

    let entries: [Entry]
    let onHover: (URL?) -> Void
    let onDraggingChange: (Bool) -> Void
    let onOpen: (URL) -> Void
    let onReveal: (URL) -> Void
    let onAirDrop: (URL) -> Void
    let onRemove: (URL) -> Void

    func makeNSView(context: Context) -> ShelfDragSourceView { ShelfDragSourceView() }

    func updateNSView(_ view: ShelfDragSourceView, context: Context) {
        view.entries = entries
        view.onHover = onHover
        view.onDraggingChange = onDraggingChange
        view.onOpen = onOpen
        view.onReveal = onReveal
        view.onAirDrop = onAirDrop
        view.onRemove = onRemove
    }
}

final class ShelfDragSourceView: NSView, NSDraggingSource {
    var entries: [ShelfDragSource.Entry] = []
    var onHover: ((URL?) -> Void)?
    var onDraggingChange: ((Bool) -> Void)?
    var onOpen: ((URL) -> Void)?
    var onReveal: ((URL) -> Void)?
    var onAirDrop: ((URL) -> Void)?
    var onRemove: ((URL) -> Void)?

    private var trackingArea: NSTrackingArea?
    private var pressLocation: CGPoint?
    private var pressedEntry: ShelfDragSource.Entry?
    private var hoveredURL: URL?
    private var menuTarget: URL?

    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let trackingArea { removeTrackingArea(trackingArea) }
        let area = NSTrackingArea(rect: bounds, options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect], owner: self)
        addTrackingArea(area)
        trackingArea = area
    }

    private func entry(at point: CGPoint) -> ShelfDragSource.Entry? {
        entries.first { $0.frame.contains(point) }
    }

    private func setHovered(_ url: URL?) {
        guard url != hoveredURL else { return }
        hoveredURL = url
        onHover?(url)
    }

    override func mouseMoved(with event: NSEvent) {
        setHovered(entry(at: convert(event.locationInWindow, from: nil))?.url)
    }

    override func mouseExited(with event: NSEvent) {
        setHovered(nil)
    }

    override func mouseDown(with event: NSEvent) {
        let p = convert(event.locationInWindow, from: nil)
        guard let hit = entry(at: p) else { return }
        let closeRect = CGRect(x: hit.frame.maxX - 18, y: hit.frame.minY, width: 18, height: 18)
        if closeRect.contains(p) {
            onRemove?(hit.url)
            return
        }
        if event.clickCount == 2 {
            onOpen?(hit.url)
            return
        }
        pressLocation = p
        pressedEntry = hit
    }

    override func mouseDragged(with event: NSEvent) {
        guard let start = pressLocation, let hit = pressedEntry else { return }
        let p = convert(event.locationInWindow, from: nil)
        guard hypot(p.x - start.x, p.y - start.y) > 4 else { return }
        pressLocation = nil
        pressedEntry = nil

        let group = event.modifierFlags.contains(.command) ? entries.filter { $0.url == hit.url } : entries
        let items: [NSDraggingItem] = group.map { entry in
            let item = NSDraggingItem(pasteboardWriter: entry.url as NSURL)
            let iconRect = CGRect(x: entry.frame.midX - 25, y: entry.frame.minY + 8, width: 50, height: 50)
            item.setDraggingFrame(iconRect, contents: entry.image)
            return item
        }
        guard !items.isEmpty else { return }
        let session = beginDraggingSession(with: items, event: event, source: self)
        session.draggingFormation = .pile
        session.animatesToStartingPositionsOnCancelOrFail = true
        session.draggingLeaderIndex = group.firstIndex { $0.url == hit.url } ?? 0
        setHovered(nil)
        onDraggingChange?(true)
    }

    override func mouseUp(with event: NSEvent) {
        pressLocation = nil
        pressedEntry = nil
    }

    override func rightMouseDown(with event: NSEvent) {
        guard let hit = entry(at: convert(event.locationInWindow, from: nil)) else { return }
        menuTarget = hit.url
        let menu = NSMenu()
        menu.addItem(withTitle: String(localized: "Open"), action: #selector(menuOpen), keyEquivalent: "").target = self
        menu.addItem(withTitle: String(localized: "Show in Finder"), action: #selector(menuReveal), keyEquivalent: "").target = self
        menu.addItem(withTitle: String(localized: "Send with AirDrop"), action: #selector(menuAirDrop), keyEquivalent: "").target = self
        menu.addItem(.separator())
        menu.addItem(withTitle: String(localized: "Remove from Shelf"), action: #selector(menuRemove), keyEquivalent: "").target = self
        NSMenu.popUpContextMenu(menu, with: event, for: self)
    }

    @objc private func menuOpen() { menuTarget.map { onOpen?($0) } }
    @objc private func menuReveal() { menuTarget.map { onReveal?($0) } }
    @objc private func menuAirDrop() { menuTarget.map { onAirDrop?($0) } }
    @objc private func menuRemove() { menuTarget.map { onRemove?($0) } }

    func draggingSession(_ session: NSDraggingSession, sourceOperationMaskFor context: NSDraggingContext) -> NSDragOperation {
        context == .outsideApplication ? [.copy, .generic] : .copy
    }

    func draggingSession(_ session: NSDraggingSession, endedAt screenPoint: NSPoint, operation: NSDragOperation) {
        onDraggingChange?(false)
    }
}
