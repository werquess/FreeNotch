import Foundation

enum Prefs {
    enum Key: String, CaseIterable {
        case hoverToExpand
        case hoverDelay
        case hoverGrow
        case haptics
        case escCloses
        case showDropZone

        case expandedWidth
        case expandedHeight
        case notesHeight
        case animationSpeed

        case tabOrder
        case disabledTabs
        case defaultTab

        case showHUD
        case hudVolume
        case hudBrightness
        case trackPeek
        case activityMedia
        case activityTimer
        case activityCharging
        case activityBluetooth
        case activityStopwatch
        case replaceSystemHUD
        case caffeineActive
        case focusMode
        case theme
        case accentChoice
        case accentCustomHex
        case autoCheckUpdates
        case lastUpdateCheck
        case skippedVersion

        case sourceSpotify
        case sourceMusic
        case sourceBrowsers
        case browserScanAllTabs
        case browserPollSeconds

        case lyricsEnabled
        case lyricsInCompact
        case lyricsFontSize

        case clipboardLimit
        case clipboardEnabled
        case clipboardTexts

        case shelfPaths

        case autoLockNotes
        case quickNote

        case focusMinutes
        case breakMinutes
    }

    static let defaults: [String: Any] = [
        Key.hoverToExpand.rawValue: false,
        Key.hoverDelay.rawValue: 0.25,
        Key.hoverGrow.rawValue: true,
        Key.haptics.rawValue: true,
        Key.escCloses.rawValue: true,
        Key.showDropZone.rawValue: true,
        Key.expandedWidth.rawValue: 620.0,
        Key.expandedHeight.rawValue: 178.0,
        Key.notesHeight.rawValue: 330.0,
        Key.animationSpeed.rawValue: 1.0,
        Key.tabOrder.rawValue: IslandTab.allCases.map(\.rawValue),
        Key.disabledTabs.rawValue: [String](),
        Key.defaultTab.rawValue: IslandTab.media.rawValue,
        Key.showHUD.rawValue: true,
        Key.hudVolume.rawValue: true,
        Key.hudBrightness.rawValue: true,
        Key.trackPeek.rawValue: true,
        Key.activityMedia.rawValue: true,
        Key.activityTimer.rawValue: true,
        Key.activityCharging.rawValue: true,
        Key.activityBluetooth.rawValue: true,
        Key.activityStopwatch.rawValue: true,
        Key.replaceSystemHUD.rawValue: true,
        Key.caffeineActive.rawValue: false,
        Key.focusMode.rawValue: "pomodoro",
        Key.theme.rawValue: "oled",
        Key.accentChoice.rawValue: "system",
        Key.accentCustomHex.rawValue: "#FF375F",
        Key.autoCheckUpdates.rawValue: true,
        Key.sourceSpotify.rawValue: true,
        Key.sourceMusic.rawValue: true,
        Key.sourceBrowsers.rawValue: true,
        Key.browserScanAllTabs.rawValue: false,
        Key.browserPollSeconds.rawValue: 4.0,
        Key.lyricsEnabled.rawValue: true,
        Key.lyricsInCompact.rawValue: false,
        Key.lyricsFontSize.rawValue: 15.0,
        Key.clipboardLimit.rawValue: 20,
        Key.clipboardEnabled.rawValue: true,
        Key.autoLockNotes.rawValue: true,
        Key.focusMinutes.rawValue: 25,
        Key.breakMinutes.rawValue: 5,
    ]

    static func registerDefaults() {
        UserDefaults.standard.register(defaults: defaults)
    }

    static func resetSettings() {
        let keep: Set<Key> = [.clipboardTexts, .shelfPaths, .quickNote]
        for key in Key.allCases where !keep.contains(key) {
            UserDefaults.standard.removeObject(forKey: key.rawValue)
        }
    }

    static func bool(_ key: Key) -> Bool { UserDefaults.standard.bool(forKey: key.rawValue) }
    static func double(_ key: Key) -> Double { UserDefaults.standard.double(forKey: key.rawValue) }
    static func int(_ key: Key) -> Int { UserDefaults.standard.integer(forKey: key.rawValue) }
    static func string(_ key: Key) -> String? { UserDefaults.standard.string(forKey: key.rawValue) }
    static func strings(_ key: Key) -> [String] { UserDefaults.standard.stringArray(forKey: key.rawValue) ?? [] }
    static func set(_ value: Any?, _ key: Key) { UserDefaults.standard.set(value, forKey: key.rawValue) }

    static var orderedTabs: [IslandTab] {
        var result = strings(.tabOrder).compactMap(IslandTab.init(rawValue:))
        for tab in IslandTab.allCases where !result.contains(tab) { result.append(tab) }
        return result
    }

    static var enabledTabs: [IslandTab] {
        let disabled = Set(strings(.disabledTabs))
        let tabs = orderedTabs.filter { !disabled.contains($0.rawValue) }
        return tabs.isEmpty ? [.media] : tabs
    }

    static func moveTab(_ tab: IslandTab, by offset: Int) {
        let visible = enabledTabs
        guard let i = visible.firstIndex(of: tab), visible.indices.contains(i + offset) else { return }
        var order = orderedTabs
        guard let a = order.firstIndex(of: tab), let b = order.firstIndex(of: visible[i + offset]) else { return }
        order.swapAt(a, b)
        set(order.map(\.rawValue), .tabOrder)
    }

    static func moveTab(_ tab: IslandTab, before target: IslandTab) {
        var order = orderedTabs
        guard let from = order.firstIndex(of: tab) else { return }
        order.remove(at: from)
        let to = order.firstIndex(of: target) ?? order.count
        order.insert(tab, at: from <= to ? min(to + 1, order.count) : to)
        set(order.map(\.rawValue), .tabOrder)
    }

    static func setTabEnabled(_ tab: IslandTab, _ enabled: Bool) {
        var disabled = Set(strings(.disabledTabs))
        if enabled { disabled.remove(tab.rawValue) } else { disabled.insert(tab.rawValue) }
        set(Array(disabled), .disabledTabs)
    }

    static var layoutSignature: String {
        let keys: [Key] = [.expandedWidth, .expandedHeight, .notesHeight, .hoverGrow, .animationSpeed,
                           .tabOrder, .disabledTabs, .lyricsInCompact, .lyricsEnabled, .activityMedia,
                           .activityTimer, .activityStopwatch, .lyricsFontSize]
        return keys.map { "\(UserDefaults.standard.object(forKey: $0.rawValue) ?? "")" }.joined(separator: "|")
    }
}
