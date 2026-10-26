import SwiftUI
import UniformTypeIdentifiers

struct ShelfView: View {
    @Environment(ShelfStore.self) private var shelf
    @State private var airDropTargeted = false

    var body: some View {
        HStack(spacing: 12) {
            if shelf.items.isEmpty {
                emptyDropZone
            } else {
                itemsStrip
            }
            airDropZone
        }
    }

    private var emptyDropZone: some View {
        RoundedRectangle(cornerRadius: 14, style: .continuous)
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
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(String(localized: "\(shelf.items.count) items"))
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(.white.opacity(0.5))
                Spacer()
                Button(String(localized: "Clear")) { withAnimation(Motion.tab) { shelf.clear() } }
                    .buttonStyle(.plain)
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(.white.opacity(0.6))
            }
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 10) {
                    ForEach(shelf.items) { item in
                        ShelfTile(item: item)
                            .transition(.scale(scale: 0.6).combined(with: .opacity))
                    }
                }
                .animation(Motion.tab, value: shelf.items)
            }
        }
        .padding(12)
        .islandCard()
    }

    private var airDropZone: some View {
        Button { shelf.airDrop() } label: {
            VStack(spacing: 8) {
                Image(systemName: "airplayaudio")
                    .font(.system(size: 26, weight: .medium))
                    .symbolEffect(.pulse, isActive: airDropTargeted)
                Text("AirDrop")
                    .font(.system(size: 12, weight: .semibold))
            }
            .foregroundStyle(airDropTargeted ? .black : .white)
            .frame(width: 96)
            .frame(maxHeight: .infinity)
            .background(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .fill(airDropTargeted ? Color.cyan : Color.black)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
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

private struct ShelfTile: View {
    let item: ShelfStore.Item
    @Environment(ShelfStore.self) private var shelf
    @State private var hovering = false

    var body: some View {
        VStack(spacing: 6) {
            Image(nsImage: item.icon)
                .resizable()
                .frame(width: 48, height: 48)
                .scaleEffect(hovering ? 1.08 : 1)
            Text(item.name)
                .font(.system(size: 10, weight: .medium))
                .lineLimit(1)
                .truncationMode(.middle)
                .frame(width: 70)
        }
        .padding(6)
        .background(RoundedRectangle(cornerRadius: 12).fill(.white.opacity(hovering ? 0.1 : 0)))
        .overlay(alignment: .topTrailing) {
            if hovering {
                Button { withAnimation(Motion.tab) { shelf.remove(item) } } label: {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 14))
                        .foregroundStyle(.white, .gray)
                }
                .buttonStyle(.plain)
                .transition(.scale.combined(with: .opacity))
            }
        }
        .animation(.spring(response: 0.25, dampingFraction: 0.7), value: hovering)
        .onHover { hovering = $0 }
        .onTapGesture(count: 2) { shelf.open(item) }
        .onDrag { NSItemProvider(contentsOf: item.url) ?? NSItemProvider() }
        .contextMenu {
            Button(String(localized: "Open")) { shelf.open(item) }
            Button(String(localized: "Show in Finder")) { shelf.reveal(item) }
            Button(String(localized: "Send with AirDrop")) { shelf.airDrop([item]) }
            Divider()
            Button(String(localized: "Remove from Shelf"), role: .destructive) { shelf.remove(item) }
        }
    }
}
