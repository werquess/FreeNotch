import AppKit

enum BrowserApp: String, CaseIterable, Sendable {
    case brave = "com.brave.Browser"
    case chrome = "com.google.Chrome"
    case edge = "com.microsoft.edgemac"
    case arc = "company.thebrowser.Browser"
    case safari = "com.apple.Safari"

    var appName: String {
        switch self {
        case .brave: "Brave Browser"
        case .chrome: "Google Chrome"
        case .edge: "Microsoft Edge"
        case .arc: "Arc"
        case .safari: "Safari"
        }
    }

    var shortName: String {
        switch self {
        case .brave: "Brave"
        case .chrome: "Chrome"
        case .edge: "Edge"
        case .arc: "Arc"
        case .safari: "Safari"
        }
    }

    var isRunning: Bool {
        !NSRunningApplication.runningApplications(withBundleIdentifier: rawValue).isEmpty
    }

    var permissionHint: String {
        switch self {
        case .safari: String(localized: "In Safari, open Settings › Advanced › Show features for web developers, then Develop › Allow JavaScript from Apple Events.")
        default: String(localized: "In \(shortName), choose View › Developer › Allow JavaScript from Apple Events.")
        }
    }
}

enum MediaKind: Equatable, Sendable {
    case spotify
    case music
    case browser(BrowserApp, tabID: String)

    var bundleID: String {
        switch self {
        case .spotify: "com.spotify.client"
        case .music: "com.apple.Music"
        case .browser(let app, _): app.rawValue
        }
    }

    var appName: String {
        switch self {
        case .spotify: "Spotify"
        case .music: "Music"
        case .browser(let app, _): app.appName
        }
    }

    var isNative: Bool {
        if case .browser = self { return false }
        return true
    }
}

struct MediaSession: Identifiable, Equatable {
    let id: String
    let kind: MediaKind
    var trackID: String
    var title: String
    var artist: String
    var album: String
    var duration: TimeInterval
    var isPlaying: Bool
    var anchorPosition: TimeInterval
    var anchorDate: Date
    var artwork: NSImage?
    var artworkTrackID: String?
    var accent: NSColor = .white
    var pageURL: String?
    var isInView = false

    private nonisolated(unsafe) static let iconCache = NSCache<NSString, NSImage>()

    var appIcon: NSImage {
        let key = kind.bundleID as NSString
        if let cached = Self.iconCache.object(forKey: key) { return cached }
        let icon: NSImage
        if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: kind.bundleID) {
            icon = NSWorkspace.shared.icon(forFile: url.path)
        } else {
            icon = NSImage(systemSymbolName: "music.note", accessibilityDescription: nil) ?? NSImage()
        }
        Self.iconCache.setObject(icon, forKey: key)
        return icon
    }

    var sourceLabel: String {
        switch kind {
        case .spotify: "Spotify"
        case .music: String(localized: "Music")
        case .browser(let app, _): app.shortName
        }
    }

    func position(at date: Date) -> TimeInterval {
        let p = isPlaying ? anchorPosition + date.timeIntervalSince(anchorDate) : anchorPosition
        return duration > 0 ? min(max(0, p), duration) : max(0, p)
    }

    static func == (a: MediaSession, b: MediaSession) -> Bool {
        a.id == b.id && a.trackID == b.trackID && a.title == b.title && a.artist == b.artist
            && a.duration == b.duration && a.isPlaying == b.isPlaying && a.anchorPosition == b.anchorPosition
            && a.anchorDate == b.anchorDate && a.artwork === b.artwork && a.accent == b.accent
            && a.isInView == b.isInView
    }
}
