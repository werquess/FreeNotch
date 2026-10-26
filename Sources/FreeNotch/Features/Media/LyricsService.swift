import Foundation
import Observation

@MainActor
@Observable
final class LyricsService {
    struct Line: Identifiable, Equatable {
        let id: Int
        let time: TimeInterval
        let text: String
    }

    enum State: Equatable { case idle, loading, synced, plain, notFound, failed }

    private(set) var state: State = .idle
    private(set) var lines: [Line] = []
    private(set) var plainText: String?
    private(set) var trackKey: String?

    @ObservationIgnored private var cache: [String: ([Line], String?)] = [:]
    @ObservationIgnored private var task: Task<Void, Never>?

    var hasSyncedLines: Bool { state == .synced && !lines.isEmpty }

    func ensure(for session: MediaSession?) {
        guard Prefs.bool(.lyricsEnabled), let session, !session.title.isEmpty else { return }
        let key = "\(session.title)|\(session.artist)".lowercased()
        guard key != trackKey else { return }
        trackKey = key
        task?.cancel()

        if let cached = cache[key] {
            apply(cached.0, cached.1)
            return
        }
        state = .loading
        lines = []
        plainText = nil
        let title = session.title, artist = session.artist, album = session.album, duration = session.duration
        let isBrowser = !session.kind.isNative
        task = Task { [weak self] in
            let result = await Self.fetch(title: title, artist: artist, album: album, duration: duration, searchOnly: isBrowser)
            guard let self, !Task.isCancelled, self.trackKey == key else { return }
            switch result {
            case .success(let r):
                self.cache[key] = r
                self.apply(r.0, r.1)
            case .failure:
                self.state = .failed
            }
        }
    }

    func lineIndex(at position: TimeInterval) -> Int? {
        guard !lines.isEmpty else { return nil }
        var lo = 0, hi = lines.count - 1, found: Int?
        while lo <= hi {
            let mid = (lo + hi) / 2
            if lines[mid].time <= position + 0.15 { found = mid; lo = mid + 1 } else { hi = mid - 1 }
        }
        return found
    }

    func lineChangeDates(for session: MediaSession) -> [Date] {
        guard session.isPlaying, hasSyncedLines else { return [.now] }
        let now = Date.now
        let pos = session.position(at: now)
        var dates = [now]
        for line in lines where line.time > pos {
            dates.append(now.addingTimeInterval(line.time - pos))
            if dates.count > 400 { break }
        }
        return dates
    }

    private func apply(_ lines: [Line], _ plain: String?) {
        self.lines = lines
        self.plainText = plain
        state = !lines.isEmpty ? .synced : (plain?.isEmpty == false ? .plain : .notFound)
    }

    private nonisolated static func fetch(title: String, artist: String, album: String, duration: TimeInterval,
                                          searchOnly: Bool) async -> Result<([Line], String?), Error> {
        let session = URLSession.shared
        do {
            if !searchOnly, !artist.isEmpty {
                var comps = URLComponents(string: "https://lrclib.net/api/get")!
                comps.queryItems = [
                    .init(name: "track_name", value: title),
                    .init(name: "artist_name", value: artist),
                    .init(name: "album_name", value: album),
                    .init(name: "duration", value: String(Int(duration.rounded())))
                ]
                var req = URLRequest(url: comps.url!)
                req.setValue("DynamicIsland/0.2 (macOS)", forHTTPHeaderField: "User-Agent")
                let (data, response) = try await session.data(for: req)
                if (response as? HTTPURLResponse)?.statusCode == 200,
                   let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any] {
                    let parsed = parse(obj)
                    if !parsed.0.isEmpty || parsed.1 != nil { return .success(parsed) }
                }
            }

            let query = cleanTitle(title) + (artist.isEmpty || searchOnly ? "" : " \(artist)")
            var comps = URLComponents(string: "https://lrclib.net/api/search")!
            comps.queryItems = [.init(name: "q", value: query)]
            var req = URLRequest(url: comps.url!)
            req.setValue("DynamicIsland/0.2 (macOS)", forHTTPHeaderField: "User-Agent")
            let (data, _) = try await session.data(for: req)
            let results = (try JSONSerialization.jsonObject(with: data) as? [[String: Any]]) ?? []
            let best = results.first { ($0["syncedLyrics"] as? String)?.isEmpty == false } ?? results.first
            return .success(best.map(parse) ?? ([], nil))
        } catch {
            return .failure(error)
        }
    }

    private nonisolated static func parse(_ obj: [String: Any]) -> ([Line], String?) {
        let plain = obj["plainLyrics"] as? String
        guard let synced = obj["syncedLyrics"] as? String, !synced.isEmpty else { return ([], plain) }
        var result: [Line] = []
        for raw in synced.split(separator: "\n") {
            guard raw.hasPrefix("["), let close = raw.firstIndex(of: "]") else { continue }
            let stamp = raw[raw.index(after: raw.startIndex)..<close]
            let parts = stamp.split(separator: ":")
            guard parts.count == 2, let m = Double(parts[0]), let s = Double(parts[1]) else { continue }
            let text = raw[raw.index(after: close)...].trimmingCharacters(in: .whitespaces)
            result.append(Line(id: result.count, time: m * 60 + s, text: text.isEmpty ? "♪" : text))
        }
        return (result, plain)
    }

    private nonisolated static func cleanTitle(_ t: String) -> String {
        var s = t
        for pattern in ["\\(.*?\\)", "\\[.*?\\]", "(?i)official.*", "(?i)lyrics?", "(?i)video"] {
            s = s.replacingOccurrences(of: pattern, with: "", options: .regularExpression)
        }
        return s.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
