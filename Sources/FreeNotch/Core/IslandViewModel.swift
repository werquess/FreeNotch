import AppKit
import Observation

enum IslandTab: String, CaseIterable, Identifiable {
    case media, shelf, clipboard, notes, focus, system, tools
    var id: String { rawValue }

    var symbol: String {
        switch self {
        case .media: "music.note"
        case .shelf: "tray.full.fill"
        case .clipboard: "doc.on.clipboard.fill"
        case .notes: "note.text"
        case .focus: "timer"
        case .system: "gauge.with.dots.needle.67percent"
        case .tools: "square.grid.2x2.fill"
        }
    }

    var title: String {
        switch self {
        case .media: String(localized: "Media")
        case .shelf: String(localized: "Shelf")
        case .clipboard: String(localized: "Clipboard")
        case .notes: String(localized: "Notes")
        case .focus: String(localized: "Focus")
        case .system: String(localized: "System")
        case .tools: String(localized: "Tools")
        }
    }

    var subtitle: String {
        switch self {
        case .media: String(localized: "Spotify, Music and browser media with lyrics")
        case .shelf: String(localized: "Keep files around for a while, send them with AirDrop")
        case .clipboard: String(localized: "Everything you copied, searchable")
        case .notes: String(localized: "Notes with optional password lock, stored locally")
        case .focus: String(localized: "Pomodoro, stopwatch and calendar")
        case .system: String(localized: "Volume, brightness, battery, temperature, CPU and memory")
        case .tools: String(localized: "Camera mirror, keep awake and color picker")
        }
    }
}

enum LiveActivity: Equatable {
    case media
    case timer
    case charging(level: Int, pluggedIn: Bool)
    case bluetooth(name: String, symbol: String, connected: Bool)
    case timerDone(String)
    case trackPeek
    case shelfAdded(Int)
    case stopwatch
    case colorCopied(String)
    case updateAvailable(String)

    var sideWidth: CGFloat {
        switch self {
        case .media: 40
        case .timer: 62
        case .charging: 56
        case .bluetooth: 92
        case .timerDone: 92
        case .trackPeek: 120
        case .shelfAdded: 78
        case .stopwatch: 66
        case .colorCopied: 84
        case .updateAvailable: 92
        }
    }

    var extraHeight: CGFloat {
        self == .trackPeek ? 54 : 0
    }

    var key: String {
        switch self {
        case .media: "media"
        case .timer: "timer"
        case .charging: "charging"
        case .bluetooth: "bluetooth"
        case .timerDone: "timerDone"
        case .trackPeek: "peek"
        case .shelfAdded: "shelfAdded"
        case .stopwatch: "stopwatch"
        case .colorCopied: "colorCopied"
        case .updateAvailable: "update"
        }
    }

    var contentKey: String {
        self == .trackPeek ? LiveActivity.media.key : key
    }
}

struct HUDEvent: Equatable {
    enum Kind: Equatable { case volume, brightness, keyboard }
    var kind: Kind
    var level: Float
    var muted = false

    var symbol: String {
        switch kind {
        case .volume:
            return muted || level <= 0.001 ? "speaker.slash.fill" : "speaker.wave.3.fill"
        case .brightness:
            return "sun.max.fill"
        case .keyboard:
            return "light.max"
        }
    }
}

enum Presentation: Equatable {
    case idle
    case activity(LiveActivity)
    case hud(HUDEvent)
    case dropZone
    case expanded

    var key: String {
        switch self {
        case .idle: "idle"
        case .activity(let a): "act-\(a.key)"
        case .hud: "hud"
        case .dropZone: "drop"
        case .expanded: "expanded"
        }
    }

    var isExpanded: Bool { self == .expanded }
    var isLarge: Bool { self == .expanded || self == .dropZone }

    var topRadius: CGFloat {
        switch self {
        case .expanded, .dropZone: 14
        case .activity(.trackPeek): 10
        default: 6
        }
    }

    var bottomRadius: CGFloat {
        switch self {
        case .expanded: 28
        case .dropZone: 26
        case .activity(.trackPeek): 22
        case .idle: 10
        default: 14
        }
    }
}

@MainActor
@Observable
final class IslandViewModel {
    enum Mode { case compact, expanded }

    private(set) var mode: Mode = .compact
    var selectedTab: IslandTab = .media
    private(set) var hud: HUDEvent?
    private(set) var transient: LiveActivity?
    private(set) var metrics = NotchMetrics.current(for: .islandScreen)

    private(set) var isHovering = false

    private(set) var isDragActive = false

    private(set) var prefsVersion = 0

    @ObservationIgnored var openSettings: (() -> Void)?
    @ObservationIgnored var onCollapse: (() -> Void)?
    @ObservationIgnored var collapseBlocker: (() -> Bool)?
    @ObservationIgnored private let media: MediaController
    @ObservationIgnored private let pomodoro: PomodoroTimer
    @ObservationIgnored private let lyrics: LyricsService
    @ObservationIgnored private let stopwatch: Stopwatch
    @ObservationIgnored private var hoverTask: Task<Void, Never>?
    @ObservationIgnored private var collapseTask: Task<Void, Never>?
    @ObservationIgnored private var hudTask: Task<Void, Never>?
    @ObservationIgnored private var transientTask: Task<Void, Never>?
    @ObservationIgnored private var isPointerInside = false
    @ObservationIgnored private var layoutSignature = Prefs.layoutSignature

    init(media: MediaController, pomodoro: PomodoroTimer, lyrics: LyricsService, stopwatch: Stopwatch) {
        self.media = media
        self.pomodoro = pomodoro
        self.lyrics = lyrics
        self.stopwatch = stopwatch
        selectedTab = IslandTab(rawValue: Prefs.string(.defaultTab) ?? "") ?? .media
        NotificationCenter.default.addObserver(forName: UserDefaults.didChangeNotification, object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.preferencesChanged() }
        }
    }

    private func preferencesChanged() {
        let signature = Prefs.layoutSignature
        guard signature != layoutSignature else { return }
        layoutSignature = signature
        prefsVersion += 1
        if !visibleTabs.contains(selectedTab) { selectedTab = visibleTabs.first ?? .media }
    }

    var visibleTabs: [IslandTab] {
        _ = prefsVersion
        return Prefs.enabledTabs
    }

    var presentation: Presentation {
        if mode == .expanded { return .expanded }
        if isDragActive { return .dropZone }
        if let hud { return .hud(hud) }
        if let transient { return .activity(transient) }
        if pomodoro.isRunning, Prefs.bool(.activityTimer) { return .activity(.timer) }
        if stopwatch.isRunning, Prefs.bool(.activityStopwatch) { return .activity(.stopwatch) }
        if media.isPlaying, Prefs.bool(.activityMedia) { return .activity(.media) }
        return .idle
    }

    var showsLyricLine: Bool {
        _ = prefsVersion
        return Prefs.bool(.lyricsEnabled) && Prefs.bool(.lyricsInCompact) && media.isPlaying && lyrics.hasSyncedLines
    }

    func size(for p: Presentation) -> CGSize {
        _ = prefsVersion
        let flare = p.topRadius * 2
        let hoverBump: (CGFloat, CGFloat) = (isHovering && mode == .compact && Prefs.bool(.hoverGrow) && !p.isLarge) ? (18, 4) : (0, 0)
        var size: CGSize
        switch p {
        case .idle:
            size = CGSize(width: metrics.width + flare, height: metrics.height)
        case .activity(let a):
            var extra = a.extraHeight
            if a == .media, showsLyricLine { extra += 26 }
            size = CGSize(width: metrics.width + a.sideWidth * 2 + flare, height: metrics.height + extra)
        case .hud:
            size = CGSize(width: metrics.width + 96 * 2 + flare, height: metrics.height)
        case .dropZone:
            size = CGSize(width: max(metrics.width + 280, 500) + flare, height: metrics.height + 104)
        case .expanded:
            let width = max(Prefs.double(.expandedWidth), metrics.width + 420)
            let content = selectedTab == .notes ? Prefs.double(.notesHeight) : Prefs.double(.expandedHeight)
            size = CGSize(width: width + flare, height: metrics.height + content)
        }
        size.width += hoverBump.0
        size.height += hoverBump.1
        return size
    }

    var currentSize: CGSize { size(for: presentation) }
    var expandedSize: CGSize { size(for: .expanded) }

    var canvasSize: CGSize {
        _ = prefsVersion
        let width = max(Prefs.double(.expandedWidth), metrics.width + 420) + 28 + 120
        let height = metrics.height + max(Prefs.double(.notesHeight), Prefs.double(.expandedHeight), 140) + 60
        return CGSize(width: max(width, 760), height: height)
    }

    var animationSpeed: Double {
        _ = prefsVersion
        let s = Prefs.double(.animationSpeed)
        return s > 0 ? s : 1
    }

    func refreshMetrics() {
        metrics = NotchMetrics.current(for: .islandScreen)
    }

    func expand(tab: IslandTab? = nil) {
        collapseTask?.cancel()
        hoverTask?.cancel()
        if let tab, visibleTabs.contains(tab) { selectedTab = tab }
        guard mode != .expanded else { return }
        if Prefs.bool(.haptics) {
            NSHapticFeedbackManager.defaultPerformer.perform(.alignment, performanceTime: .now)
        }
        isHovering = false
        mode = .expanded
    }

    func collapse() {
        hoverTask?.cancel()
        collapseTask?.cancel()
        guard mode != .compact, collapseBlocker?() != true else { return }
        mode = .compact
        onCollapse?()
    }

    func toggle() { mode == .expanded ? collapse() : expand() }

    func sensorEntered() {
        guard mode == .compact else { return }
        isPointerInside = true
        if Prefs.bool(.hoverGrow), !isHovering {
            isHovering = true
        }
        guard Prefs.bool(.hoverToExpand) else { return }
        hoverTask?.cancel()
        let delay = Prefs.double(.hoverDelay)
        hoverTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(delay))
            guard let self, !Task.isCancelled, self.isPointerInside else { return }
            self.expand()
        }
    }

    func sensorExited() {
        isPointerInside = false
        hoverTask?.cancel()
        if isHovering { isHovering = false }
    }

    func panelEntered() {
        isPointerInside = true
        collapseTask?.cancel()
    }

    func panelExited() {
        isPointerInside = false

        guard mode == .expanded, Prefs.bool(.hoverToExpand) else { return }
        collapseTask?.cancel()
        collapseTask = Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(420))
            guard let self, !Task.isCancelled, !self.isPointerInside else { return }
            self.collapse()
        }
    }

    func dragBegan() {
        guard Prefs.bool(.showDropZone), mode == .compact, !isDragActive else { return }
        isDragActive = true
    }

    func dragEnded() {
        guard isDragActive else { return }
        isDragActive = false
    }

    func showHUD(_ event: HUDEvent) {
        guard Prefs.bool(.showHUD) else { return }
        switch event.kind {
        case .volume: guard Prefs.bool(.hudVolume) else { return }
        case .brightness, .keyboard: guard Prefs.bool(.hudBrightness) else { return }
        }
        hud = event
        hudTask?.cancel()
        hudTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(1.6))
            guard !Task.isCancelled else { return }
            self?.hud = nil
        }
    }

    func showTransient(_ activity: LiveActivity, duration: Double) {
        transient = activity
        transientTask?.cancel()
        transientTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(duration))
            guard !Task.isCancelled else { return }
            self?.transient = nil
        }
    }

    #if DEBUG

    func debugForce(mode: Mode, tab: IslandTab = .media, hud: HUDEvent? = nil, transient: LiveActivity? = nil,
                    hovering: Bool = false, dragging: Bool = false) {
        self.mode = mode
        self.selectedTab = tab
        self.hud = hud
        self.transient = transient
        self.isHovering = hovering
        self.isDragActive = dragging
        self.metrics = NotchMetrics(width: 200, height: 38, hasPhysicalNotch: true)
    }
    #endif
}
