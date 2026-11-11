import AppKit

@MainActor
final class AppEnvironment {
    static weak var current: AppEnvironment?

    let media = MediaController()
    let lyrics = LyricsService()
    let shelf = ShelfStore()
    let clipboard = ClipboardMonitor()
    let notes = NotesStore()
    let system = SystemMonitor()
    let volume = VolumeController()
    let pomodoro = PomodoroTimer()
    let stopwatch = Stopwatch()
    let calendar = CalendarService()
    let caffeine = CaffeineService()
    let appearance = Appearance()
    let updater = UpdateChecker()
    let signals = LiveSignals()
    let island: IslandViewModel

    let keyMonitor = MediaKeyMonitor()
    private let bluetooth = BluetoothMonitor()
    private var trustPoll: DispatchSourceTimer?
    private var replaceHUDSetting = Prefs.bool(.replaceSystemHUD)
    private var lastBrightness = DisplayBrightness.get()
    private var lastBluetoothEvent = Date.distantPast
    private var screenshotURL: URL?
    private var downloadURL: URL?

    init() {
        island = IslandViewModel(media: media, pomodoro: pomodoro, lyrics: lyrics, stopwatch: stopwatch,
                                 signals: signals, calendar: calendar, caffeine: caffeine, system: system)
        wire()
        Self.current = self
    }

    func start() {
        media.start()
        clipboard.start()
        system.start()
        volume.start()
        if Prefs.bool(.activityBluetooth) { bluetooth.start() }
        signals.start()
        DisplayBrightness.observeChanges { [weak self] level in self?.brightnessChanged(level) }
        updater.start()

        keyMonitor.start()
        if Prefs.bool(.replaceSystemHUD), !MediaKeyMonitor.hasAccessibility { pollForAccessibility() }

        NotificationCenter.default.addObserver(forName: UserDefaults.didChangeNotification, object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.preferencesChanged() }
        }
    }

    func stop() {
        clipboard.stop()
        system.stop()
        keyMonitor.stop()
        notes.lockAll()
        caffeine.releaseForTermination()
    }

    func requestHUDTakeover() {
        if !MediaKeyMonitor.hasAccessibility {
            MediaKeyMonitor.requestAccessibility()
            pollForAccessibility()
        }
        keyMonitor.start()
    }

    private func preferencesChanged() {
        if Prefs.bool(.activityBluetooth) { bluetooth.start() } else { bluetooth.stop() }
        signals.reloadPreferences()
        let replace = Prefs.bool(.replaceSystemHUD)
        guard replace != replaceHUDSetting else { return }
        replaceHUDSetting = replace
        keyMonitor.start()
    }

    private func pollForAccessibility() {
        trustPoll?.cancel()
        var remaining = 90
        let t = DispatchSource.makeTimerSource(queue: .main)
        t.schedule(deadline: .now() + 2, repeating: 2, leeway: .milliseconds(500))
        t.setEventHandler { [weak self] in
            MainActor.assumeIsolated {
                guard let self else { return }
                remaining -= 1
                if MediaKeyMonitor.hasAccessibility {
                    self.keyMonitor.start()
                    self.trustPoll?.cancel()
                    self.trustPoll = nil
                } else if remaining <= 0 {
                    self.trustPoll?.cancel()
                    self.trustPoll = nil
                }
            }
        }
        t.resume()
        trustPoll = t
    }

    private func wire() {
        island.collapseBlocker = { [weak self] in self?.shelf.isDraggingOut == true }
        island.onCollapse = { [weak self] in
            if Prefs.bool(.autoLockNotes) { self?.notes.lockAll() }
        }

        volume.onChange = { [weak self] level, muted in
            self?.island.showHUD(HUDEvent(kind: .volume, level: level, muted: muted))
        }

        keyMonitor.handler = { [weak self] key, fine in
            self?.handleKey(key, fine: fine) ?? false
        }

        system.onPowerSourceChange = { [weak self] pluggedIn, level in
            guard Prefs.bool(.activityCharging) else { return }
            self?.island.showTransient(.charging(level: level, pluggedIn: pluggedIn), duration: 2.6)
        }

        bluetooth.onEvent = { [weak self] name, symbol, connected in
            self?.lastBluetoothEvent = .now
            guard Prefs.bool(.activityBluetooth) else { return }
            self?.island.showTransient(.bluetooth(name: name, symbol: symbol, connected: connected), duration: Self.readingTime(name, base: 2.8))
        }

        system.onLowBattery = { [weak self] level in
            guard Prefs.bool(.activityBatteryLow) else { return }
            NSSound(named: "Funk")?.play()
            self?.island.showTransient(.batteryLow(level), duration: 4)
        }

        signals.onOutputChange = { [weak self] name, symbol in
            guard let self, Prefs.bool(.activityAudioOutput) else { return }
            guard Date.now.timeIntervalSince(self.lastBluetoothEvent) > 4 else { return }
            self.island.showTransient(.audioOutput(name: name, symbol: symbol), duration: Self.readingTime(name, base: 2.6))
        }

        signals.onCaptureStart = { [weak self] mic, camera in
            guard let self, Prefs.bool(.activityCapture), self.island.mode == .compact else { return }
            self.island.showTransient(.capture(mic: mic, camera: camera), duration: 5)
        }

        signals.onOffline = { [weak self] in
            guard Prefs.bool(.activityNetwork) else { return }
            self?.island.showTransient(.offline, duration: 5)
        }

        signals.onReconnect = { [weak self] in
            guard Prefs.bool(.activityNetwork) else { return }
            self?.island.showTransient(.online, duration: 2.4)
        }

        signals.onScreenshot = { [weak self] url in
            guard let self, Prefs.bool(.activityScreenshot) else { return }
            self.screenshotURL = url
            self.island.showTransient(.screenshot(url.path), duration: 5)
        }

        signals.onDownload = { [weak self] url in
            guard let self, Prefs.bool(.activityDownload) else { return }
            self.downloadURL = url
            self.island.showTransient(.download(url.lastPathComponent), duration: Self.readingTime(url.lastPathComponent, base: 5))
        }

        island.activityTap = { [weak self] activity in
            guard let self else { return false }
            switch activity {
            case .screenshot:
                guard let url = self.screenshotURL else { return false }
                _ = self.shelf.add([url])
                self.island.clearTransient()
                self.island.expand(tab: .shelf)
                return true
            case .download:
                guard let url = self.downloadURL else { return false }
                NSWorkspace.shared.open(url)
                return true
            case .meeting:
                self.island.expand(tab: .focus)
                return true
            default:
                return false
            }
        }

        pomodoro.onPhaseComplete = { [weak self] phase in
            NSSound(named: "Glass")?.play()
            self?.island.showTransient(.timerDone(phase.title), duration: 4)
        }

        media.onTrackChange = { [weak self] session in
            guard let self else { return }
            if Prefs.bool(.lyricsEnabled), Prefs.bool(.lyricsInCompact) { self.lyrics.ensure(for: session) }
            guard Prefs.bool(.trackPeek), Prefs.bool(.activityMedia), !session.isInView else { return }
            self.island.showTransient(.trackPeek, duration: 2.8)
        }
    }

    private static func readingTime(_ text: String, base: Double) -> Double {
        min(8, max(base, 1.6 + Double(text.count) * 0.16))
    }

    private func handleKey(_ key: MediaKeyMonitor.Key, fine: Bool) -> Bool {
        let intercepting = keyMonitor.isIntercepting
        let step: Float = fine ? 1.0 / 64 : 1.0 / 16

        switch key {
        case .volumeUp, .volumeDown:
            guard intercepting else { return false }
            let delta = key == .volumeUp ? step : -step
            let target = ((volume.volume + delta) / step).rounded() * step
            if volume.isMuted, key == .volumeUp { volume.setMuted(false) }
            volume.setVolume(target)
            if target <= 0.001 || volume.volume == target {
                island.showHUD(HUDEvent(kind: .volume, level: max(0, min(1, target)), muted: volume.isMuted))
            }
            return true

        case .mute:
            guard intercepting else { return false }
            volume.setMuted(!volume.isMuted)
            return true

        case .brightnessUp, .brightnessDown:
            guard intercepting, let current = DisplayBrightness.get() else { return false }
            let delta = key == .brightnessUp ? step : -step
            let target = min(1, max(0, ((current + delta) / step).rounded() * step))
            lastBrightness = target
            DisplayBrightness.set(target)
            island.showHUD(HUDEvent(kind: .brightness, level: target))
            return true

        case .keyboardUp, .keyboardDown:

            island.showHUD(HUDEvent(kind: .keyboard, level: key == .keyboardUp ? 1 : 0.3))
            return false
        }
    }

    private func brightnessChanged(_ level: Float) {
        defer { lastBrightness = level }
        guard let last = lastBrightness, abs(level - last) >= 0.004 else { return }
        island.showHUD(HUDEvent(kind: .brightness, level: level))
    }
}
