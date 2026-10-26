import AppKit
import Observation

@MainActor
@Observable
final class UpdateChecker {
    struct Release: Equatable {
        let version: String
        let notes: String
        let archiveURL: URL
        let pageURL: URL
    }

    enum State: Equatable {
        case idle
        case checking
        case upToDate
        case available(Release)
        case downloading
        case installing
        case failed(String)
    }

    private(set) var state: State = .idle
    private(set) var lastChecked: Date?

    @ObservationIgnored var onUpdateFound: ((Release) -> Void)?
    @ObservationIgnored private var scheduler: NSBackgroundActivityScheduler?

    let currentVersion = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.0.0"
    private let repository = Bundle.main.object(forInfoDictionaryKey: "FNUpdateRepository") as? String ?? "werquess/FreeNotch"

    var availableRelease: Release? {
        if case .available(let r) = state { return r }
        return nil
    }

    var releasesPage: URL { URL(string: "https://github.com/\(repository)/releases")! }

    init() {
        let last = Prefs.double(.lastUpdateCheck)
        lastChecked = last > 0 ? Date(timeIntervalSince1970: last) : nil
    }

    func start() {
        scheduler?.invalidate()
        scheduler = nil
        guard Prefs.bool(.autoCheckUpdates) else { return }

        let s = NSBackgroundActivityScheduler(identifier: "io.github.werquess.FreeNotch.update-check")
        s.repeats = true
        s.interval = 24 * 60 * 60
        s.tolerance = 2 * 60 * 60
        s.qualityOfService = .utility
        s.schedule { [weak self] completion in
            Task { @MainActor in
                await self?.check(userInitiated: false)
                completion(.finished)
            }
        }
        scheduler = s

        if (lastChecked.map { Date.now.timeIntervalSince($0) > 20 * 60 * 60 }) ?? true {
            Task { [weak self] in
                try? await Task.sleep(for: .seconds(20))
                await self?.check(userInitiated: false)
            }
        }
    }

    func check(userInitiated: Bool) async {
        if case .checking = state { return }
        if case .downloading = state { return }
        state = .checking
        do {
            let release = try await fetchLatest()
            lastChecked = .now
            Prefs.set(Date.now.timeIntervalSince1970, .lastUpdateCheck)
            guard let release, Self.isVersion(release.version, newerThan: currentVersion) else {
                state = .upToDate
                return
            }
            state = .available(release)
            if !userInitiated, Prefs.string(.skippedVersion) != release.version {
                onUpdateFound?(release)
            }
        } catch {
            state = userInitiated ? .failed(String(localized: "Could not reach GitHub. Check your connection.")) : .idle
        }
    }

    func skip(_ release: Release) {
        Prefs.set(release.version, .skippedVersion)
        state = .idle
    }

    func install(_ release: Release) async {
        let destination = URL(fileURLWithPath: Bundle.main.bundlePath)
        guard FileManager.default.isWritableFile(atPath: destination.deletingLastPathComponent().path) else {
            state = .failed(String(localized: "FreeNotch can't replace itself in this folder. Move it to Applications and try again."))
            return
        }
        state = .downloading
        do {
            let work = FileManager.default.temporaryDirectory.appendingPathComponent("FreeNotchUpdate-\(UUID().uuidString)")
            try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true)
            let (download, response) = try await URLSession.shared.download(from: release.archiveURL)
            guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw UpdateError.download }
            let archive = work.appendingPathComponent("FreeNotch.zip")
            try FileManager.default.moveItem(at: download, to: archive)

            state = .installing
            try await Self.run("/usr/bin/ditto", ["-x", "-k", archive.path, work.path])
            let newApp = work.appendingPathComponent("FreeNotch.app")
            guard let bundle = Bundle(url: newApp),
                  bundle.bundleIdentifier == Bundle.main.bundleIdentifier,
                  let version = bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String,
                  Self.isVersion(version, newerThan: currentVersion) || version == release.version else {
                throw UpdateError.invalidBundle
            }
            try? await Self.run("/usr/bin/xattr", ["-dr", "com.apple.quarantine", newApp.path])
            try relaunch(replacing: destination, with: newApp)
        } catch {
            state = .failed(String(localized: "The update could not be installed. You can download it from GitHub instead."))
        }
    }

    private func relaunch(replacing destination: URL, with newApp: URL) throws {
        let script = """
        while /bin/kill -0 \(ProcessInfo.processInfo.processIdentifier) 2>/dev/null; do /bin/sleep 0.2; done
        /bin/rm -rf "\(destination.path).old"
        /bin/mv "\(destination.path)" "\(destination.path).old" && /bin/mv "\(newApp.path)" "\(destination.path)" && /bin/rm -rf "\(destination.path).old"
        /usr/bin/open "\(destination.path)"
        """
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        process.arguments = ["-c", script]
        try process.run()
        NSApp.terminate(nil)
    }

    private func fetchLatest() async throws -> Release? {
        var request = URLRequest(url: URL(string: "https://api.github.com/repos/\(repository)/releases/latest")!)
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        request.setValue("FreeNotch/\(currentVersion)", forHTTPHeaderField: "User-Agent")
        request.timeoutInterval = 15
        let (data, response) = try await URLSession.shared.data(for: request)
        let code = (response as? HTTPURLResponse)?.statusCode ?? 0
        if code == 404 { return nil }
        guard code == 200, let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw UpdateError.download
        }
        let tag = (json["tag_name"] as? String ?? "").trimmingCharacters(in: CharacterSet(charactersIn: "vV"))
        let assets = json["assets"] as? [[String: Any]] ?? []
        guard !tag.isEmpty,
              let zip = assets.first(where: { ($0["name"] as? String)?.hasSuffix(".zip") == true }),
              let zipURL = (zip["browser_download_url"] as? String).flatMap(URL.init(string:)) else { return nil }
        let page = (json["html_url"] as? String).flatMap(URL.init(string:)) ?? releasesPage
        return Release(version: tag, notes: json["body"] as? String ?? "", archiveURL: zipURL, pageURL: page)
    }

    static func isVersion(_ a: String, newerThan b: String) -> Bool {
        let pa = a.split(separator: ".").map { Int($0) ?? 0 }
        let pb = b.split(separator: ".").map { Int($0) ?? 0 }
        for i in 0..<max(pa.count, pb.count) {
            let x = i < pa.count ? pa[i] : 0
            let y = i < pb.count ? pb[i] : 0
            if x != y { return x > y }
        }
        return false
    }

    private enum UpdateError: Error { case download, invalidBundle, process }

    private nonisolated static func run(_ tool: String, _ args: [String]) async throws {
        try await withCheckedThrowingContinuation { (cont: CheckedContinuation<Void, Error>) in
            let p = Process()
            p.executableURL = URL(fileURLWithPath: tool)
            p.arguments = args
            p.terminationHandler = { proc in
                proc.terminationStatus == 0 ? cont.resume() : cont.resume(throwing: UpdateError.process)
            }
            do { try p.run() } catch { cont.resume(throwing: error) }
        }
    }
}
