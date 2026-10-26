import AppKit
import Observation

@MainActor
@Observable
final class ShelfStore {
    struct Item: Identifiable, Hashable {
        let url: URL
        var id: URL { url }
        var name: String { url.lastPathComponent }
        var icon: NSImage { NSWorkspace.shared.icon(forFile: url.path) }
    }

    private(set) var items: [Item] = []

    init() {
        let paths = UserDefaults.standard.stringArray(forKey: Prefs.Key.shelfPaths.rawValue) ?? []
        items = paths.map { URL(fileURLWithPath: $0) }
            .filter { FileManager.default.fileExists(atPath: $0.path) }
            .map(Item.init)
    }

    @discardableResult
    func add(_ urls: [URL]) -> Int {
        var added = 0
        for url in urls where !items.contains(where: { $0.url == url }) {
            items.append(Item(url: url))
            added += 1
        }
        persist()
        return added
    }

    func add(providers: [NSItemProvider], completion: (@MainActor (Int) -> Void)? = nil) {
        URLLoader.load(providers) { [weak self] urls in
            let count = self?.add(urls) ?? 0
            completion?(count)
        }
    }

    func remove(_ item: Item) {
        items.removeAll { $0 == item }
        persist()
    }

    func clear() {
        items.removeAll()
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

    private func persist() {
        UserDefaults.standard.set(items.map(\.url.path), forKey: Prefs.Key.shelfPaths.rawValue)
    }
}
