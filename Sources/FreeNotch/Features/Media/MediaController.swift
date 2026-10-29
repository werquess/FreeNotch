import AppKit
import Observation
import SwiftUI

@MainActor
@Observable
final class MediaController {
    private(set) var sessions: [MediaSession] = []

    var selectedID: String?

    private(set) var browserHint: (app: BrowserApp, message: String)?

    @ObservationIgnored var onTrackChange: ((MediaSession) -> Void)?
    @ObservationIgnored private var observers: [NSObjectProtocol] = []
    @ObservationIgnored private var pollTimer: DispatchSourceTimer?
    @ObservationIgnored private var pollInterval: Double = 0
    @ObservationIgnored private var liveSyncClients = 0
    @ObservationIgnored private var scansInFlight: Set<BrowserApp> = []
    @ObservationIgnored private var knownBrowserTabs: Set<String> = []
    @ObservationIgnored private var artworkCache: [String: NSImage] = [:]
    @ObservationIgnored private let audio = AudioActivityMonitor()
    @ObservationIgnored private var expectedPlaying: [String: (playing: Bool, until: Date)] = [:]

    @ObservationIgnored private var blockedBrowsers: [BrowserApp: Date] = [:]
    @ObservationIgnored private var stuckTabs: [String: Date] = [:]

    var current: MediaSession? {
        if let selectedID, let s = sessions.first(where: { $0.id == selectedID }) { return s }
        return sessions.first(where: \.isPlaying) ?? sessions.first
    }

    var playing: MediaSession? {
        if let c = current, c.isPlaying { return c }
        return sessions.first(where: \.isPlaying)
    }

    var isPlaying: Bool { sessions.contains(where: \.isPlaying) }

    var isPlayingOutOfView: Bool { sessions.contains { $0.isPlaying && !$0.isInView } }

    func start() {
        let center = DistributedNotificationCenter.default()
        let natives: [(String, MediaKind)] = [
            ("com.spotify.client.PlaybackStateChanged", .spotify),
            ("com.apple.Music.playerInfo", .music)
        ]
        for (name, kind) in natives {
            let token = center.addObserver(forName: .init(name), object: nil, queue: .main) { [weak self] note in
                let info = note.userInfo ?? [:]
                MainActor.assumeIsolated { self?.handleNative(info, kind: kind) }
            }
            observers.append(token)
        }

        let ws = NSWorkspace.shared.notificationCenter
        let activation = ws.addObserver(forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main) { [weak self] note in
            let bundle = (note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication)?.bundleIdentifier
            MainActor.assumeIsolated { self?.appActivated(bundle) }
        }
        observers.append(activation)
        for name in [NSWorkspace.didLaunchApplicationNotification, NSWorkspace.didTerminateApplicationNotification] {
            let token = ws.addObserver(forName: name, object: nil, queue: .main) { [weak self] note in
                let bundle = (note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication)?.bundleIdentifier
                let terminated = note.name == NSWorkspace.didTerminateApplicationNotification
                MainActor.assumeIsolated { self?.appLifecycleChanged(bundle: bundle, terminated: terminated) }
            }
            observers.append(token)
        }

        let prefs = NotificationCenter.default.addObserver(forName: UserDefaults.didChangeNotification, object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.preferencesChanged() }
        }
        observers.append(prefs)

        audio.onChange = { [weak self] _ in self?.audioActivityChanged() }
        audio.start()

        refreshNativeStates()
        reschedulePolling()
        if Prefs.bool(.sourceBrowsers) { scanBrowsers() }
    }

    private func appActivated(_ bundle: String?) {
        for s in sessions where s.isInView && s.kind.bundleID != bundle {
            mutate(s.id) { $0.isInView = false }
        }
        guard let bundle, BrowserApp(rawValue: bundle) != nil,
              sessions.contains(where: { $0.kind.bundleID == bundle }) else { return }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [weak self] in self?.scanBrowsers() }
    }

    private func audioActivityChanged() {
        if Prefs.bool(.sourceBrowsers) {
            scanBrowsers(force: audio.isActive)
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.2) { [weak self] in self?.scanBrowsers() }
        }
        reschedulePolling()
    }

    private var browsersNeedPolling: Bool {
        guard Prefs.bool(.sourceBrowsers) else { return false }
        return audio.isActive || liveSyncClients > 0 || sessions.contains { !$0.kind.isNative && $0.isPlaying }
    }

    func beginLiveSync() {
        liveSyncClients += 1
        refreshNativeStates()
        if Prefs.bool(.sourceBrowsers) { scanBrowsers(force: true) }
        reschedulePolling()
    }

    func endLiveSync() {
        liveSyncClients = max(0, liveSyncClients - 1)
        reschedulePolling()
    }

    func select(_ id: String) {
        selectedID = id
    }

    func playPause(_ session: MediaSession? = nil) {
        guard let s = session ?? current else { openApp(.spotify); return }
        mutate(s.id) {
            $0.anchorPosition = $0.position(at: .now)
            $0.anchorDate = .now
            $0.isPlaying.toggle()
            expectedPlaying[$0.id] = ($0.isPlaying, Date.now.addingTimeInterval(2))
        }
        switch s.kind {
        case .spotify, .music: native(s.kind, "playpause")
        case .browser(let app, let tab): runInTab(app, tab: tab, js: BrowserScripts.toggle)
        }
    }

    func next(_ session: MediaSession? = nil) {
        guard let s = session ?? current else { return }
        switch s.kind {
        case .spotify, .music: native(s.kind, "next track")
        case .browser(let app, let tab): runInTab(app, tab: tab, js: BrowserScripts.next)
        }
    }

    func previous(_ session: MediaSession? = nil) {
        guard let s = session ?? current else { return }
        switch s.kind {
        case .spotify, .music: native(s.kind, "previous track")
        case .browser(let app, let tab): runInTab(app, tab: tab, js: BrowserScripts.previous)
        }
    }

    func seek(to seconds: TimeInterval, in session: MediaSession? = nil) {
        guard let s = session ?? current else { return }
        mutate(s.id) {
            $0.anchorPosition = seconds
            $0.anchorDate = .now
        }
        switch s.kind {
        case .spotify, .music:
            native(s.kind, "set player position to \(seconds)")
        case .browser(let app, let tab):
            runInTab(app, tab: tab, js: BrowserScripts.seek(seconds))
        }
    }

    func openApp(_ kind: MediaKind) {
        guard let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: kind.bundleID) else { return }
        NSWorkspace.shared.openApplication(at: url, configuration: .init())
    }

    private func isEnabled(_ kind: MediaKind) -> Bool {
        switch kind {
        case .spotify: Prefs.bool(.sourceSpotify)
        case .music: Prefs.bool(.sourceMusic)
        case .browser: Prefs.bool(.sourceBrowsers)
        }
    }

    private func isRunning(_ kind: MediaKind) -> Bool {
        !NSRunningApplication.runningApplications(withBundleIdentifier: kind.bundleID).isEmpty
    }

    private func native(_ kind: MediaKind, _ command: String) {
        guard isRunning(kind) else { return }
        AppleScriptRunner.run("tell application \"\(kind.appName)\" to \(command)") { [weak self] _ in

            self?.refreshNativeState(kind)
        }
    }

    private func handleNative(_ info: [AnyHashable: Any], kind: MediaKind) {
        guard isEnabled(kind) else { return }
        let id = kind.bundleID
        let state = (info["Player State"] as? String)?.lowercased() ?? ""
        if state == "stopped" {
            removeSession(id)
            return
        }
        let title = info["Name"] as? String ?? ""
        let artist = info["Artist"] as? String ?? ""
        let durationMs = (info["Duration"] as? NSNumber)?.doubleValue
            ?? (info["Total Time"] as? NSNumber)?.doubleValue ?? 0
        let trackID = (info["Track ID"] as? String)
            ?? (info["PersistentID"] as? NSNumber).map { String(format: "%016llX", UInt64(bitPattern: $0.int64Value)) }
            ?? "\(title)|\(artist)"
        let position = (info["Playback Position"] as? NSNumber)?.doubleValue

        upsert(id: id, kind: kind, trackID: trackID, title: title, artist: artist,
               album: info["Album"] as? String ?? "", duration: durationMs / 1000,
               playing: state == "playing", position: position, authoritative: true)
        if position == nil { refreshNativeState(kind) }
    }

    private func refreshNativeStates() {
        if Prefs.bool(.sourceSpotify) { refreshNativeState(.spotify) }
        if Prefs.bool(.sourceMusic) { refreshNativeState(.music) }
    }

    private func refreshNativeState(_ kind: MediaKind) {
        guard isEnabled(kind), isRunning(kind) else { return }
        let idExpr = kind == .spotify ? "id of t" : "persistent ID of t"
        let durExpr = kind == .spotify ? "(duration of t) / 1000" : "duration of t"
        let script = """
        tell application "\(kind.appName)"
            set s to player state as string
            if s is "stopped" then return {s}
            set t to current track
            return {s, name of t, artist of t, album of t, \(durExpr), player position, \(idExpr)}
        end tell
        """
        AppleScriptRunner.run(script) { [weak self] v in
            guard let self else { return }
            guard let state = v.first?.string else { return }
            if state == "stopped" { self.removeSession(kind.bundleID); return }
            guard v.count >= 7 else { return }
            self.upsert(id: kind.bundleID, kind: kind,
                        trackID: (kind == .spotify ? v[6].string : v[6].string?.uppercased()) ?? "\(v[1].string ?? "")|\(v[2].string ?? "")",
                        title: v[1].string ?? "", artist: v[2].string ?? "", album: v[3].string ?? "",
                        duration: v[4].number ?? 0, playing: state == "playing", position: v[5].number)
        }
    }

    private func scanBrowsers(force: Bool = false) {
        guard Prefs.bool(.sourceBrowsers) else { return }
        for app in BrowserApp.allCases where app.isRunning && !scansInFlight.contains(app) {
            if !force, let since = blockedBrowsers[app], Date.now.timeIntervalSince(since) < 60 { continue }
            scansInFlight.insert(app)
            stuckTabs = stuckTabs.filter { Date.now.timeIntervalSince($0.value) < 60 }
            let skip = Set(stuckTabs.keys.compactMap { key -> String? in
                let prefix = "\(app.rawValue)#"
                return key.hasPrefix(prefix) ? String(key.dropFirst(prefix.count)) : nil
            })
            let script = BrowserScripts.scan(app, allTabs: Prefs.bool(.browserScanAllTabs), skip: skip)
            AppleScriptRunner.runDetailed(script, lane: .browser) { [weak self] values, error in
                guard let self else { return }
                self.scansInFlight.remove(app)
                self.applyScan(app, values: values.compactMap(\.string), error: error)
            }
        }
    }

    private func applyScan(_ app: BrowserApp, values: [String], error: String?) {
        let frontmost = NSWorkspace.shared.frontmostApplication?.bundleIdentifier
        var seen: Set<String> = []
        var jsBlocked = false
        if let error, error.localizedCaseInsensitiveContains("javascript") { jsBlocked = true }

        for line in values {
            guard let sep = line.firstIndex(of: "|") else { continue }
            let tab = String(line[..<sep])
            let payload = String(line[line.index(after: sep)...])
            if tab == "!ERR" {
                let parts = payload.split(separator: "|", maxSplits: 2, omittingEmptySubsequences: false)
                if parts.count == 3, parts[1] == "-1712" {
                    stuckTabs["\(app.rawValue)#\(parts[0])"] = .now
                } else if payload.localizedCaseInsensitiveContains("javascript") {
                    jsBlocked = true
                }
                continue
            }
            guard let data = payload.data(using: .utf8),
                  let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { continue }
            let id = "\(app.rawValue)#\(tab)"
            let playing = obj["p"] as? Bool ?? false

            guard playing || knownBrowserTabs.contains(id) else { continue }
            knownBrowserTabs.insert(id)
            seen.insert(id)

            let title = (obj["t"] as? String ?? "").replacingOccurrences(of: " - YouTube", with: "")
            let artist = obj["a"] as? String ?? ""
            let artURL = obj["art"] as? String ?? ""
            let page = obj["u"] as? String
            upsert(id: id, kind: .browser(app, tabID: tab), trackID: "\(tab)|\(title)", title: title,
                   artist: artist, album: obj["al"] as? String ?? "",
                   duration: obj["d"] as? Double ?? 0, playing: playing,
                   position: obj["c"] as? Double, artworkURL: artURL.isEmpty ? Self.youtubeThumb(page) : artURL,
                   pageURL: page, inView: (obj["f"] as? Bool ?? false) && frontmost == app.rawValue)
        }

        if jsBlocked, seen.isEmpty {
            browserHint = (app, app.permissionHint)
            blockedBrowsers[app] = .now
        } else {
            blockedBrowsers[app] = nil
            if browserHint?.app == app { browserHint = nil }
        }

        for s in sessions {
            if case .browser(let a, _) = s.kind, a == app, !seen.contains(s.id) {
                removeSession(s.id)
                knownBrowserTabs.remove(s.id)
            }
        }
        reschedulePolling()
    }

    private func runInTab(_ app: BrowserApp, tab: String, js: String) {
        guard app.isRunning else { return }
        AppleScriptRunner.runDetailed(BrowserScripts.run(app, tab: tab, js: js), lane: .browser) { [weak self] _, _ in
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) { self?.scanBrowsers(force: true) }
        }
    }

    private static func youtubeThumb(_ page: String?) -> String? {
        guard let page, let comps = URLComponents(string: page), comps.host?.contains("youtube") == true,
              let v = comps.queryItems?.first(where: { $0.name == "v" })?.value else { return nil }
        return "https://i.ytimg.com/vi/\(v)/hqdefault.jpg"
    }

    private func reschedulePolling() {
        let browsers = browsersNeedPolling && BrowserApp.allCases.contains { $0.isRunning && blockedBrowsers[$0] == nil }
        let lyricSync = Prefs.bool(.lyricsEnabled) && Prefs.bool(.lyricsInCompact)
            && sessions.contains { $0.kind.isNative && $0.isPlaying }

        let interval: Double
        if liveSyncClients > 0 {
            interval = 1.5
        } else if browsers {
            interval = max(2, Prefs.double(.browserPollSeconds))
        } else if lyricSync {
            interval = 5
        } else {
            interval = 0
        }
        guard interval != pollInterval else { return }
        pollInterval = interval
        pollTimer?.cancel()
        pollTimer = nil
        guard interval > 0 else { return }

        let t = DispatchSource.makeTimerSource(queue: .main)
        t.schedule(deadline: .now() + interval, repeating: interval, leeway: .milliseconds(Int(interval * 300)))
        t.setEventHandler { [weak self] in
            MainActor.assumeIsolated { self?.tick() }
        }
        t.resume()
        pollTimer = t
    }

    private func tick() {
        if browsersNeedPolling { scanBrowsers() }
        let lyricSync = Prefs.bool(.lyricsEnabled) && Prefs.bool(.lyricsInCompact)
        if liveSyncClients > 0 || lyricSync {
            for s in sessions where s.kind.isNative && (s.isPlaying || liveSyncClients > 0) {
                refreshNativeState(s.kind)
            }
        }
    }

    private func appLifecycleChanged(bundle: String?, terminated: Bool) {
        guard let bundle else { return }
        let relevant = BrowserApp(rawValue: bundle) != nil || bundle == MediaKind.spotify.bundleID || bundle == MediaKind.music.bundleID
        guard relevant else { return }
        if terminated {
            for s in sessions where s.kind.bundleID == bundle { removeSession(s.id) }
            if browserHint?.app.rawValue == bundle { browserHint = nil }
            if let app = BrowserApp(rawValue: bundle) { blockedBrowsers[app] = nil }
        }
        reschedulePolling()
    }

    private func preferencesChanged() {
        for s in sessions where !isEnabled(s.kind) { removeSession(s.id) }
        if !Prefs.bool(.sourceBrowsers) { browserHint = nil }
        reschedulePolling()
    }

    private func upsert(id: String, kind: MediaKind, trackID: String, title: String, artist: String, album: String,
                        duration: TimeInterval, playing: Bool, position: TimeInterval?,
                        artworkURL: String? = nil, pageURL: String? = nil, inView: Bool = false,
                        authoritative: Bool = false) {
        var playing = playing
        var position = position
        if let expected = expectedPlaying[id] {
            if expected.until < .now || authoritative {
                expectedPlaying[id] = nil
            } else if expected.playing != playing {
                playing = expected.playing
                position = nil
            }
        }
        if let i = sessions.firstIndex(where: { $0.id == id }) {
            var s = sessions[i]
            let trackChanged = s.trackID != trackID
            let startedPlaying = playing && !s.isPlaying
            s.trackID = trackID
            s.title = title
            s.artist = artist
            s.album = album
            s.duration = duration
            s.isPlaying = playing
            s.pageURL = pageURL ?? s.pageURL
            s.isInView = inView
            if let position {
                s.anchorPosition = position
                s.anchorDate = .now
            } else if trackChanged {
                s.anchorPosition = 0
                s.anchorDate = .now
            }
            if s != sessions[i] { sessions[i] = s }
            if trackChanged {
                loadArtwork(for: s, url: artworkURL)
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
                    self?.mutate(id) {
                        if $0.trackID == trackID, $0.artworkTrackID != trackID { $0.artwork = nil }
                    }
                }
            }
            if startedPlaying { selectedID = id }
            if trackChanged, playing { onTrackChange?(s) }
            if trackChanged || startedPlaying { reschedulePolling() }
        } else {
            var s = MediaSession(id: id, kind: kind, trackID: trackID, title: title, artist: artist, album: album,
                                 duration: duration, isPlaying: playing, anchorPosition: position ?? 0,
                                 anchorDate: .now, pageURL: pageURL)
            s.isInView = inView
            sessions.append(s)
            loadArtwork(for: s, url: artworkURL)
            if playing {
                selectedID = id
                onTrackChange?(s)
            }
            reschedulePolling()
        }
    }

    private func removeSession(_ id: String) {
        sessions.removeAll { $0.id == id }
        if selectedID == id { selectedID = nil }
    }

    private func mutate(_ id: String, _ change: (inout MediaSession) -> Void) {
        guard let i = sessions.firstIndex(where: { $0.id == id }) else { return }
        change(&sessions[i])
    }

    private func loadArtwork(for s: MediaSession, url: String?) {
        let id = s.id
        let trackID = s.trackID
        switch s.kind {
        case .spotify:
            loadSpotifyArtwork(id: id, trackID: trackID, retries: 3)
        case .music:
            let script = """
            tell application "Music"
                try
                    return raw data of artwork 1 of current track
                on error
                    return data of artwork 1 of current track
                end try
            end tell
            """
            AppleScriptRunner.run(script) { [weak self] v in
                guard let data = v.first?.data else { return }
                self?.setArtwork(data, id: id, trackID: trackID)
            }
        case .browser:
            if let url { fetchArtwork(url, id: id, trackID: trackID) }
        }
    }

    private func loadSpotifyArtwork(id: String, trackID: String, retries: Int) {
        let script = "tell application \"Spotify\" to return {id of current track, artwork url of current track}"
        AppleScriptRunner.run(script) { [weak self] v in
            guard let self, self.sessions.contains(where: { $0.id == id && $0.trackID == trackID }) else { return }
            guard v.count >= 2, v[0].string == trackID, let url = v[1].string else {
                guard retries > 0 else { return }
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) {
                    self.loadSpotifyArtwork(id: id, trackID: trackID, retries: retries - 1)
                }
                return
            }
            self.fetchArtwork(url, id: id, trackID: trackID)
        }
    }

    private func fetchArtwork(_ urlString: String, id: String, trackID: String) {
        if let cached = artworkCache[urlString] {
            mutate(id) {
                if $0.trackID == trackID {
                    $0.artwork = cached
                    $0.artworkTrackID = trackID
                }
            }
            return
        }
        guard let url = URL(string: urlString), url.scheme?.hasPrefix("http") == true else { return }
        Task { [weak self] in
            guard let (data, _) = try? await URLSession.shared.data(from: url) else { return }
            self?.setArtwork(data, id: id, trackID: trackID, cacheKey: urlString)
        }
    }

    private func setArtwork(_ data: Data, id: String, trackID: String, cacheKey: String? = nil) {
        guard let image = NSImage(data: data) else { return }
        if let cacheKey {
            if artworkCache.count > 40 { artworkCache.removeAll() }
            artworkCache[cacheKey] = image
        }
        mutate(id) {
            if $0.trackID == trackID {
                $0.artwork = image
                $0.artworkTrackID = trackID
            }
        }
        Task { [weak self] in
            let rgb = await Task.detached(priority: .utility) { ColorExtractor.averageRGB(from: data) }.value
            guard let self, let rgb else { return }
            self.mutate(id) {
                if $0.trackID == trackID { $0.accent = NSColor(srgbRed: rgb.0, green: rgb.1, blue: rgb.2, alpha: 1) }
            }
        }
    }

    #if DEBUG
    func debugInject(_ session: MediaSession) {
        sessions = [session]
        selectedID = session.id
    }
    #endif
}
