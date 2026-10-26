import AppKit
import Observation
import QuickLookThumbnailing

@MainActor
@Observable
final class ShelfStore {
    struct Item: Identifiable, Hashable {
        let url: URL
        var id: URL { url }
        var name: String { url.lastPathComponent }
        var isDirectory: Bool { (try? url.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true }
    }

    private(set) var items: [Item] = []
    private(set) var thumbnails: [URL: NSImage] = [:]
    private(set) var tints: [URL: NSColor] = [:]
    var isDraggingOut = false

    @ObservationIgnored private var pendingThumbnails: Set<URL> = []

    init() {
        let paths = UserDefaults.standard.stringArray(forKey: Prefs.Key.shelfPaths.rawValue) ?? []
        items = paths.map { URL(fileURLWithPath: $0) }
            .filter { FileManager.default.fileExists(atPath: $0.path) }
            .map(Item.init)
        items.forEach(loadThumbnail)
    }

    func icon(for item: Item) -> NSImage {
        thumbnails[item.url] ?? NSWorkspace.shared.icon(forFile: item.url.path)
    }

    @discardableResult
    func add(_ urls: [URL]) -> Int {
        for url in urls {
            items.removeAll { $0.url == url }
            let item = Item(url: url)
            items.append(item)
            loadThumbnail(item)
        }
        persist()
        return urls.count
    }

    func add(providers: [NSItemProvider], completion: (@MainActor (Int) -> Void)? = nil) {
        URLLoader.load(providers) { [weak self] urls in
            guard let self, !urls.isEmpty else { return }
            completion?(self.add(urls))
        }
    }

    func remove(_ item: Item) {
        items.removeAll { $0 == item }
        thumbnails[item.url] = nil
        tints[item.url] = nil
        persist()
    }

    func clear() {
        items.removeAll()
        thumbnails.removeAll()
        tints.removeAll()
        persist()
    }

    func airDrop(_ selection: [Item]? = nil) {
        let urls = (selection ?? items).map(\.url)
        guard !urls.isEmpty, let service = NSSharingService(named: .sendViaAirDrop) else { return }
        service.perform(withItems: urls)
    }

    func reveal(_ item: Item) {
        NSWorkspace.shared.activateFileViewerSelecting([item.url])
    }

    func open(_ item: Item) {
        NSWorkspace.shared.open(item.url)
    }

    private func loadThumbnail(_ item: Item) {
        let url = item.url
        guard thumbnails[url] == nil, !pendingThumbnails.contains(url) else { return }
        if item.isDirectory {
            let icon = NSWorkspace.shared.icon(forFile: url.path)
            thumbnails[url] = icon
            computeTint(icon, for: url)
            return
        }
        pendingThumbnails.insert(url)
        let request = QLThumbnailGenerator.Request(fileAt: url, size: CGSize(width: 96, height: 96),
                                                   scale: NSScreen.main?.backingScaleFactor ?? 2,
                                                   representationTypes: .all)
        QLThumbnailGenerator.shared.generateBestRepresentation(for: request) { [weak self] rep, _ in
            let image = rep?.nsImage
            DispatchQueue.main.async {
                MainActor.assumeIsolated {
                    guard let self else { return }
                    self.pendingThumbnails.remove(url)
                    let final = image ?? NSWorkspace.shared.icon(forFile: url.path)
                    self.thumbnails[url] = final
                    self.computeTint(final, for: url)
                }
            }
        }
    }

    private func computeTint(_ image: NSImage, for url: URL) {
        guard let data = image.tiffRepresentation else { return }
        Task { [weak self] in
            let rgb = await Task.detached(priority: .utility) { ColorExtractor.averageRGB(from: data) }.value
            guard let self, let rgb else { return }
            self.tints[url] = NSColor(srgbRed: rgb.0, green: rgb.1, blue: rgb.2, alpha: 1)
        }
    }

    private func persist() {
        UserDefaults.standard.set(items.map(\.url.path), forKey: Prefs.Key.shelfPaths.rawValue)
    }
}
