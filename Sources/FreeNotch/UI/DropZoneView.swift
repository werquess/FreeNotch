import AppKit
import SwiftUI
import UniformTypeIdentifiers

struct DropZoneView: View {
    @Environment(IslandViewModel.self) private var vm
    @Environment(ShelfStore.self) private var shelf
    @State private var shelfTargeted = false
    @State private var airDropTargeted = false
    @State private var pulse = false

    var body: some View {
        VStack(spacing: 0) {
            Color.clear.frame(height: vm.metrics.height)
            HStack(spacing: 10) {
                zone(symbol: "tray.and.arrow.down.fill",
                     title: String(localized: "Drop on Shelf"),
                     subtitle: shelf.items.isEmpty ? String(localized: "Files wait here for you") : String(localized: "\(shelf.items.count) on the shelf"),
                     targeted: shelfTargeted)
                    .onDrop(of: [.fileURL], isTargeted: $shelfTargeted.animation(Motion.hover)) { providers in
                        shelf.add(providers: providers) { count in
                            vm.showTransient(.shelfAdded(count), duration: 2.2)
                        }
                        return true
                    }

                zone(symbol: "airplayaudio", title: "AirDrop", subtitle: String(localized: "Send to a nearby device"), targeted: airDropTargeted)
                    .frame(width: 150)
                    .onDrop(of: [.fileURL], isTargeted: $airDropTargeted.animation(Motion.hover)) { providers in
                        URLLoader.load(providers) { urls in
                            NSSharingService(named: .sendViaAirDrop)?.perform(withItems: urls)
                        }
                        return true
                    }
            }
            .padding(.horizontal, 14)
            .padding(.bottom, 14)
            .padding(.top, 6)
        }
        .onAppear { pulse = true }
    }

    private func zone(symbol: String, title: String, subtitle: String, targeted: Bool) -> some View {
        let blue = Color(red: 0.16, green: 0.52, blue: 1.0)
        return RoundedRectangle(cornerRadius: 18, style: .continuous)
            .fill(blue.opacity(targeted ? 0.45 : 0.16))
            .overlay {
                RoundedRectangle(cornerRadius: 18, style: .continuous)
                    .strokeBorder(blue.opacity(targeted ? 1 : 0.75),
                                  style: StrokeStyle(lineWidth: targeted ? 2.5 : 1.5, dash: targeted ? [] : [7, 5]))
            }
            .overlay {
                VStack(spacing: 6) {
                    Image(systemName: symbol)
                        .font(.system(size: 24, weight: .semibold))
                        .symbolEffect(.bounce, value: targeted)
                        .symbolEffect(.pulse, options: .repeating, isActive: !targeted && pulse)
                    Text(title).font(.system(size: 13, weight: .semibold))
                    Text(subtitle).font(.system(size: 10)).foregroundStyle(.white.opacity(0.65))
                }
                .foregroundStyle(.white)
            }
            .scaleEffect(targeted ? 1.03 : 1)
            .shadow(color: blue.opacity(targeted ? 0.6 : 0), radius: 14)
    }
}

enum URLLoader {
    static func load(_ providers: [NSItemProvider], completion: @escaping @MainActor ([URL]) -> Void) {
        let group = DispatchGroup()
        let box = URLBox()
        for p in providers {
            group.enter()
            _ = p.loadObject(ofClass: URL.self) { url, _ in
                if let url, url.isFileURL { box.append(url) }
                group.leave()
            }
        }
        group.notify(queue: .main) {
            MainActor.assumeIsolated { completion(box.urls) }
        }
    }
}

final class URLBox: @unchecked Sendable {
    private let lock = NSLock()
    private var storage: [URL] = []
    var urls: [URL] { lock.lock(); defer { lock.unlock() }; return storage }
    func append(_ u: URL) { lock.lock(); storage.append(u); lock.unlock() }
}
