import SwiftUI

struct ClipboardView: View {
    @Environment(ClipboardMonitor.self) private var clipboard
    @State private var copiedID: UUID?
    @State private var query = ""

    private var filtered: [ClipboardMonitor.Entry] {
        guard !query.isEmpty else { return clipboard.entries }
        return clipboard.entries.filter { $0.previewText.localizedCaseInsensitiveContains(query) }
    }

    var body: some View {
        VStack(spacing: 8) {
            HStack(spacing: 8) {
                HStack(spacing: 6) {
                    Image(systemName: "magnifyingglass").foregroundStyle(.white.opacity(0.4))
                    TextField(String(localized: "Search clipboard"), text: $query)
                        .textFieldStyle(.plain)
                }
                .font(.system(size: 12))
                .padding(.horizontal, 10)
                .padding(.vertical, 5)
                .background(Capsule().fill(.white.opacity(0.08)))

                @Bindable var clipboard = clipboard
                Toggle(isOn: $clipboard.isPaused) {
                    Image(systemName: clipboard.isPaused ? "pause.circle.fill" : "record.circle")
                }
                .toggleStyle(.button)
                .buttonStyle(.plain)
                .foregroundStyle(clipboard.isPaused ? .orange : .white.opacity(0.6))
                .help(clipboard.isPaused ? String(localized: "Recording paused") : String(localized: "Pause recording"))

                Button { withAnimation(Motion.tab) { clipboard.clear() } } label: {
                    Image(systemName: "trash")
                }
                .buttonStyle(.plain)
                .foregroundStyle(.white.opacity(0.6))
                .help(String(localized: "Clear history"))
            }

            if filtered.isEmpty {
                Text(query.isEmpty ? String(localized: "Things you copy will show up here") : String(localized: "No results"))
                    .font(.system(size: 12))
                    .foregroundStyle(.white.opacity(0.4))
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView(.vertical, showsIndicators: false) {
                    LazyVStack(spacing: 4) {
                        ForEach(filtered) { entry in
                            row(entry)
                        }
                    }
                }
            }
        }
    }

    private func row(_ entry: ClipboardMonitor.Entry) -> some View {
        Button {
            clipboard.copy(entry)
            withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) { copiedID = entry.id }
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.2) {
                withAnimation { if copiedID == entry.id { copiedID = nil } }
            }
        } label: {
            ClipRow(entry: entry, copied: copiedID == entry.id)
        }
        .buttonStyle(.plain)
        .contextMenu {
            Button(String(localized: "Copy")) { clipboard.copy(entry) }
            Button(String(localized: "Delete"), role: .destructive) { clipboard.delete(entry) }
        }
    }
}

private struct ClipRow: View {
    let entry: ClipboardMonitor.Entry
    let copied: Bool
    @State private var hovering = false

    var body: some View {
        HStack(spacing: 10) {
            if case .image(let img) = entry.content {
                Image(nsImage: img)
                    .resizable()
                    .aspectRatio(contentMode: .fill)
                    .frame(width: 22, height: 22)
                    .clipShape(RoundedRectangle(cornerRadius: 5))
            } else {
                Image(systemName: entry.symbol)
                    .font(.system(size: 11, weight: .semibold))
                    .frame(width: 22, height: 22)
                    .background(RoundedRectangle(cornerRadius: 6).fill(.white.opacity(0.1)))
            }
            Text(entry.previewText)
                .font(.system(size: 12))
                .lineLimit(1)
                .frame(maxWidth: .infinity, alignment: .leading)
            if copied {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundStyle(.green)
                    .transition(.scale.combined(with: .opacity))
            } else if entry.date != .distantPast {
                Text(entry.date, style: .relative)
                    .font(.system(size: 10))
                    .foregroundStyle(.white.opacity(0.35))
            }
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 5)
        .background(RoundedRectangle(cornerRadius: 9).fill(.white.opacity(hovering ? 0.1 : 0.04)))
        .contentShape(Rectangle())
        .onHover { hovering = $0 }
    }
}
