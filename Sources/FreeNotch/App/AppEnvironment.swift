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
    let island: IslandViewModel

    let keyMonitor = MediaKeyMonitor()
    private let bluetooth = BluetoothMonitor()
    private var trustPoll: DispatchSourceTimer?
    private var replaceHUDSetting = Prefs.bool(.replaceSystemHUD)

    init() {
        island = IslandViewModel(media: media, pomodoro: pomodoro, lyrics: lyrics, stopwatch: stopwatch)
        wire()
        Self.current = self
    }

    func start() {
        media.start()
        clipboard.start()
        system.start()
        volume.start()
        bluetooth.start()
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
            guard Prefs.bool(.activityBluetooth) else { return }
            self?.island.showTransient(.bluetooth(name: name, symbol: symbol, connected: connected), duration: 2.8)
        }

        pomodoro.onPhaseComplete = { [weak self] phase in
            NSSound(named: "Glass")?.play()
            self?.island.showTransient(.timerDone(phase.title), duration: 4)
        }

        media.onTrackChange = { [weak self] session in
            guard let self else { return }
            if Prefs.bool(.lyricsEnabled), Prefs.bool(.lyricsInCompact) { self.lyrics.ensure(for: session) }
            guard Prefs.bool(.trackPeek), Prefs.bool(.activityMedia) else { return }
            self.island.showTransient(.trackPeek, duration: 2.8)
        }
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
            if intercepting, let current = DisplayBrightness.get() {
                let delta = key == .brightnessUp ? step : -step
                let target = min(1, max(0, ((current + delta) / step).rounded() * step))
                DisplayBrightness.set(target)
                island.showHUD(HUDEvent(kind: .brightness, level: target))
                return true
            }

            DispatchQueue.main.asyncAfter(deadline: .now() + 0.06) { [weak self] in
                self?.island.showHUD(HUDEvent(kind: .brightness, level: DisplayBrightness.get() ?? 0.5))
            }
            return false

        case .keyboardUp, .keyboardDown:

            island.showHUD(HUDEvent(kind: .keyboard, level: key == .keyboardUp ? 1 : 0.3))
            return false
        }
    }
}
