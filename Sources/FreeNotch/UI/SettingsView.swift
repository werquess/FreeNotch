import ServiceManagement
import SwiftUI

enum SettingsPane: String, CaseIterable, Identifiable {
    case general, appearance, tabs, activities, media, clipboard, notes, focus, updates, about

    var id: String { rawValue }

    var title: LocalizedStringKey {
        switch self {
        case .general: "General"
        case .appearance: "Appearance"
        case .tabs: "Tabs"
        case .activities: "Live Activities"
        case .media: "Media & Lyrics"
        case .clipboard: "Clipboard & Shelf"
        case .notes: "Notes"
        case .focus: "Focus"
        case .updates: "Updates"
        case .about: "About"
        }
    }

    var symbol: String {
        switch self {
        case .general: "gearshape.fill"
        case .appearance: "paintpalette.fill"
        case .tabs: "rectangle.3.group.fill"
        case .activities: "bolt.horizontal.fill"
        case .media: "music.note"
        case .clipboard: "doc.on.clipboard.fill"
        case .notes: "note.text"
        case .focus: "timer"
        case .updates: "arrow.down.circle.fill"
        case .about: "info.circle.fill"
        }
    }

    var tint: Color {
        switch self {
        case .general: .gray
        case .appearance: .purple
        case .tabs: .blue
        case .activities: .orange
        case .media: .pink
        case .clipboard: .teal
        case .notes: .yellow
        case .focus: .red
        case .updates: .green
        case .about: .indigo
        }
    }
}

@MainActor
@Observable
final class SettingsRouter {
    var pane: SettingsPane = .general
}

struct SettingsRootView: View {
    @Environment(SettingsRouter.self) private var router
    @Environment(UpdateChecker.self) private var updater

    var body: some View {
        @Bindable var router = router
        NavigationSplitView {
            List(SettingsPane.allCases, selection: Binding(get: { router.pane }, set: { if let p = $0 { router.pane = p } })) { pane in
                Label {
                    HStack {
                        Text(pane.title)
                        if pane == .updates, updater.availableRelease != nil {
                            Spacer()
                            Circle().fill(.red).frame(width: 7, height: 7)
                        }
                    }
                } icon: {
                    Image(systemName: pane.symbol)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white)
                        .frame(width: 22, height: 22)
                        .background(RoundedRectangle(cornerRadius: 6, style: .continuous).fill(pane.tint.gradient))
                }
                .tag(pane)
            }
            .navigationSplitViewColumnWidth(min: 190, ideal: 205, max: 240)
        } detail: {
            Group {
                switch router.pane {
                case .general: GeneralPane()
                case .appearance: AppearancePane()
                case .tabs: TabsPane()
                case .activities: ActivitiesPane()
                case .media: MediaPane()
                case .clipboard: ClipboardPane()
                case .notes: NotesPane()
                case .focus: FocusPane()
                case .updates: UpdatesPane()
                case .about: AboutPane()
                }
            }
            .formStyle(.grouped)
            .navigationTitle(router.pane.title)
        }
        .frame(minWidth: 740, minHeight: 540)
    }
}

private struct GeneralPane: View {
    @AppStorage(Prefs.Key.hoverGrow.rawValue) private var hoverGrow = true
    @AppStorage(Prefs.Key.hoverToExpand.rawValue) private var hoverToExpand = false
    @AppStorage(Prefs.Key.hoverDelay.rawValue) private var hoverDelay = 0.25
    @AppStorage(Prefs.Key.haptics.rawValue) private var haptics = true
    @AppStorage(Prefs.Key.escCloses.rawValue) private var escCloses = true
    @AppStorage(Prefs.Key.showDropZone.rawValue) private var showDropZone = true
    @AppStorage(Prefs.Key.defaultTab.rawValue) private var defaultTab = IslandTab.media.rawValue
    @State private var launchAtLogin = SMAppService.mainApp.status == .enabled
    @State private var language = AppLanguage.current
    @State private var needsRestart = false

    var body: some View {
        Form {
            Section {
                Picker("Language", selection: $language) {
                    ForEach(AppLanguage.allCases) { Text($0.displayName).tag($0) }
                }
                .onChange(of: language) { _, new in
                    new.apply()
                    needsRestart = true
                }
                if needsRestart {
                    HStack {
                        Text("Restart FreeNotch to apply the new language.").foregroundStyle(.secondary)
                        Spacer()
                        Button("Restart Now") { AppLanguage.relaunch() }
                    }
                }
            }

            Section {
                Toggle("Grow slightly when the pointer is over the notch", isOn: $hoverGrow)
                Toggle("Open automatically on hover", isOn: $hoverToExpand)
                if hoverToExpand {
                    LabeledContent("Hover delay") {
                        HStack {
                            Slider(value: $hoverDelay, in: 0...1, step: 0.05).frame(width: 180)
                            Text(verbatim: "\(Int(hoverDelay * 1000)) ms").monospacedDigit().frame(width: 60, alignment: .trailing)
                        }
                    }
                }
            } header: {
                Text("Pointer")
            } footer: {
                Text(hoverToExpand
                     ? String(localized: "The notch opens when the pointer rests on it and closes when it leaves.")
                     : String(localized: "Click the notch to open it. Click outside or press Esc to close it."))
            }

            Section("Behavior") {
                Toggle("Show the drop zone while dragging files", isOn: $showDropZone)
                Toggle("Close with Esc", isOn: $escCloses)
                Toggle("Haptic feedback on open", isOn: $haptics)
                Picker("Tab shown when opening", selection: $defaultTab) {
                    ForEach(IslandTab.allCases) { Text($0.title).tag($0.rawValue) }
                }
            }

            Section("System") {
                Toggle("Launch at login", isOn: $launchAtLogin)
                    .onChange(of: launchAtLogin) { _, on in
                        do {
                            on ? try SMAppService.mainApp.register() : try SMAppService.mainApp.unregister()
                        } catch {
                            launchAtLogin = SMAppService.mainApp.status == .enabled
                        }
                    }
                LabeledContent("Permissions") {
                    HStack {
                        Button("Automation") { openPrivacy("Privacy_Automation") }
                        Button("Accessibility") { openPrivacy("Privacy_Accessibility") }
                        Button("Input Monitoring") { openPrivacy("Privacy_ListenEvent") }
                    }
                }
            }

            Section {
                Button("Reset All Settings", role: .destructive) { Prefs.resetSettings() }
            } footer: {
                Text("Your notes, clipboard history and shelf are kept.")
            }
        }
    }

    private func openPrivacy(_ anchor: String) {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?\(anchor)") {
            NSWorkspace.shared.open(url)
        }
    }
}

enum AppLanguage: String, CaseIterable, Identifiable {
    case system, en, tr, de, es, fr

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .system: String(localized: "System Default")
        case .en: "English"
        case .tr: "Türkçe"
        case .de: "Deutsch"
        case .es: "Español"
        case .fr: "Français"
        }
    }

    static var current: AppLanguage {
        guard let list = UserDefaults.standard.persistentDomain(forName: Bundle.main.bundleIdentifier ?? "")?["AppleLanguages"] as? [String],
              let first = list.first else { return .system }
        return AppLanguage(rawValue: String(first.prefix(2))) ?? .system
    }

    func apply() {
        if self == .system {
            UserDefaults.standard.removeObject(forKey: "AppleLanguages")
        } else {
            UserDefaults.standard.set([rawValue], forKey: "AppleLanguages")
        }
    }

    static func relaunch() {
        let path = Bundle.main.bundlePath
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        process.arguments = ["-c", "sleep 0.6; /usr/bin/open \"\(path)\""]
        try? process.run()
        NSApp.terminate(nil)
    }
}

private struct AppearancePane: View {
    @Environment(IslandViewModel.self) private var vm
    @Environment(Appearance.self) private var appearance
    @AppStorage(Prefs.Key.theme.rawValue) private var theme = IslandTheme.oled.rawValue
    @AppStorage(Prefs.Key.accentChoice.rawValue) private var accent = AccentChoice.system.rawValue
    @AppStorage(Prefs.Key.accentCustomHex.rawValue) private var customHex = "#FF375F"
    @AppStorage(Prefs.Key.expandedWidth.rawValue) private var width = 620.0
    @AppStorage(Prefs.Key.expandedHeight.rawValue) private var height = 178.0
    @AppStorage(Prefs.Key.notesHeight.rawValue) private var notesHeight = 330.0
    @AppStorage(Prefs.Key.animationSpeed.rawValue) private var speed = 1.0

    var body: some View {
        Form {
            Section("Theme") {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 120), spacing: 12)], spacing: 12) {
                    ForEach(IslandTheme.allCases) { t in
                        ThemeCard(theme: t, selected: theme == t.rawValue, accent: appearance.accent)
                            .onTapGesture { theme = t.rawValue }
                            .disabled(t == .liquidGlass && !IslandTheme.supportsLiquidGlass)
                            .opacity(t == .liquidGlass && !IslandTheme.supportsLiquidGlass ? 0.4 : 1)
                    }
                }
                .padding(.vertical, 4)
                Text(IslandTheme(rawValue: theme)?.detail ?? "")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                if !IslandTheme.supportsLiquidGlass {
                    Text("Liquid Glass requires macOS 26 or later.").font(.caption).foregroundStyle(.secondary)
                }
            }

            Section {
                HStack(spacing: 10) {
                    ForEach(AccentChoice.allCases.filter { $0 != .custom }) { choice in
                        AccentSwatch(color: Color(nsColor: choice.color(customHex: customHex)),
                                     selected: accent == choice.rawValue,
                                     isSystem: choice == .system)
                            .onTapGesture { accent = choice.rawValue }
                            .help(Text(choice.title))
                    }
                    ColorPicker("", selection: Binding(
                        get: { Color(nsColor: NSColor(hexString: customHex) ?? .white) },
                        set: { customHex = NSColor($0).hexString; accent = AccentChoice.custom.rawValue }
                    ), supportsOpacity: false)
                    .labelsHidden()
                    .overlay {
                        if accent == AccentChoice.custom.rawValue {
                            Circle().strokeBorder(.primary, lineWidth: 2).frame(width: 28, height: 28).allowsHitTesting(false)
                        }
                    }
                }
                .padding(.vertical, 4)
            } header: {
                Text("Accent Color")
            } footer: {
                Text("Used for the selected tab, sliders and highlights. “System” follows the accent color chosen in macOS System Settings.")
            }

            Section {
                IslandPreview(width: width, height: height, accent: appearance.accent, onAccent: appearance.onAccent)
                    .frame(maxWidth: .infinity)
                    .frame(height: 150)
                    .animation(Motion.open, value: width)
                    .animation(Motion.open, value: height)
                sizeSlider("Width", value: $width, range: 560...980)
                sizeSlider("Height", value: $height, range: 150...340)
                sizeSlider("Notes tab height", value: $notesHeight, range: 220...560)
                HStack {
                    Button("Recommended for 14-inch") { width = 620; height = 178; notesHeight = 330 }
                    Button("Recommended for 16-inch") { width = 700; height = 200; notesHeight = 380 }
                    Spacer()
                    Button("Preview on Notch") { vm.expand() }
                }
            } header: {
                Text("Size")
            }

            Section {
                LabeledContent("Animation speed") {
                    HStack {
                        Slider(value: $speed, in: 0.5...1.8, step: 0.1).frame(width: 200)
                        Text(verbatim: String(format: "%.1f×", speed)).monospacedDigit().frame(width: 44, alignment: .trailing)
                    }
                }
            } header: {
                Text("Motion")
            } footer: {
                Text("1.0× matches the feel of the iPhone Dynamic Island. Lower values are slower and softer.")
            }
        }
    }

    private func sizeSlider(_ title: LocalizedStringKey, value: Binding<Double>, range: ClosedRange<Double>) -> some View {
        LabeledContent(title) {
            HStack {
                Slider(value: value, in: range, step: 2).frame(width: 220)
                Text(verbatim: "\(Int(value.wrappedValue)) pt").monospacedDigit().frame(width: 56, alignment: .trailing)
            }
        }
    }
}

private struct ThemeCard: View {
    let theme: IslandTheme
    let selected: Bool
    let accent: Color

    var body: some View {
        VStack(spacing: 8) {
            ZStack(alignment: .top) {
                LinearGradient(colors: [Color(red: 0.35, green: 0.25, blue: 0.75), Color(red: 0.9, green: 0.45, blue: 0.4)],
                               startPoint: .topLeading, endPoint: .bottomTrailing)
                NotchShape(topRadius: 6, bottomRadius: 14)
                    .fill(fill)
                    .overlay(NotchShape(topRadius: 6, bottomRadius: 14).stroke(.white.opacity(theme.isTranslucent ? 0.35 : 0), lineWidth: 1))
                    .frame(width: 96, height: 46)
                    .overlay(alignment: .topLeading) {
                        Capsule().fill(accent).frame(width: 14, height: 7).padding(.top, 8).padding(.leading, 14)
                    }
            }
            .frame(height: 64)
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous).strokeBorder(selected ? accent : .clear, lineWidth: 2.5))
            Text(theme.title).font(.callout.weight(selected ? .semibold : .regular))
        }
        .contentShape(Rectangle())
    }

    private var fill: AnyShapeStyle {
        switch theme {
        case .oled: AnyShapeStyle(Color.black)
        case .graphite: AnyShapeStyle(Color(white: 0.13))
        case .liquidGlass: AnyShapeStyle(Color.white.opacity(0.18))
        case .frosted: AnyShapeStyle(.ultraThinMaterial)
        }
    }
}

private struct AccentSwatch: View {
    let color: Color
    let selected: Bool
    let isSystem: Bool

    var body: some View {
        ZStack {
            if isSystem {
                Circle().fill(AngularGradient(colors: [.red, .orange, .yellow, .green, .blue, .purple, .pink, .red], center: .center))
            } else {
                Circle().fill(color)
                Circle().strokeBorder(.black.opacity(0.15), lineWidth: 1)
            }
            if selected {
                Circle().fill(.white).frame(width: 7, height: 7).shadow(radius: 1)
            }
        }
        .frame(width: 22, height: 22)
        .padding(3)
        .overlay(Circle().strokeBorder(selected ? color : .clear, lineWidth: 2))
        .contentShape(Circle())
    }
}

private struct IslandPreview: View {
    let width: Double
    let height: Double
    let accent: Color
    let onAccent: Color

    var body: some View {
        GeometryReader { geo in
            let scale = min(geo.size.width / 1000, geo.size.height / 400)
            ZStack(alignment: .top) {
                RoundedRectangle(cornerRadius: 10)
                    .fill(LinearGradient(colors: [.indigo.opacity(0.6), .purple.opacity(0.4)], startPoint: .top, endPoint: .bottom))
                NotchShape(topRadius: 14 * scale, bottomRadius: 28 * scale)
                    .fill(.black)
                    .frame(width: (width + 28) * scale, height: (height + 38) * scale)
                    .overlay(alignment: .topLeading) {
                        HStack(spacing: 4) {
                            Capsule().fill(accent).frame(width: 12, height: 7)
                            ForEach(0..<4, id: \.self) { _ in
                                Circle().fill(.white.opacity(0.5)).frame(width: 5, height: 5)
                            }
                        }
                        .padding(.top, 9 * scale)
                        .padding(.leading, 34 * scale)
                    }
            }
        }
    }
}

private struct TabsPane: View {
    @Environment(Appearance.self) private var appearance
    @State private var order: [IslandTab] = Prefs.orderedTabs
    @State private var disabled: Set<String> = Set(Prefs.strings(.disabledTabs))

    private var visible: [IslandTab] { order.filter { !disabled.contains($0.rawValue) } }

    var body: some View {
        Form {
            Section {
                HStack(spacing: 2) {
                    ForEach(visible) { tab in
                        Image(systemName: tab.symbol)
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(tab == visible.first ? appearance.onAccent : .white.opacity(0.75))
                            .frame(width: 28, height: 22)
                            .background { if tab == visible.first { Capsule().fill(appearance.accent) } }
                    }
                    Spacer()
                }
                .padding(.horizontal, 14)
                .frame(height: 38)
                .background(NotchShape(topRadius: 8, bottomRadius: 16).fill(.black))
                .animation(Motion.tab, value: visible)
            } header: {
                Text("Preview")
            } footer: {
                Text("You can also drag tab icons directly in the notch, or right-click one to move or hide it.")
            }

            Section {
                List {
                    ForEach(order) { tab in
                        HStack(spacing: 12) {
                            Image(systemName: "line.3.horizontal").foregroundStyle(.tertiary)
                            Image(systemName: tab.symbol)
                                .foregroundStyle(.white)
                                .frame(width: 26, height: 26)
                                .background(RoundedRectangle(cornerRadius: 7, style: .continuous).fill(appearance.accent.gradient))
                                .opacity(disabled.contains(tab.rawValue) ? 0.35 : 1)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(tab.title).font(.body.weight(.medium))
                                Text(tab.subtitle).font(.caption).foregroundStyle(.secondary)
                            }
                            Spacer()
                            Toggle("", isOn: binding(for: tab)).labelsHidden().toggleStyle(.switch)
                                .disabled(!disabled.contains(tab.rawValue) && visible.count <= 1)
                        }
                        .padding(.vertical, 2)
                    }
                    .onMove { from, to in
                        order.move(fromOffsets: from, toOffset: to)
                        Prefs.set(order.map(\.rawValue), .tabOrder)
                    }
                }
                .frame(minHeight: 360)
            } header: {
                Text("Tabs")
            } footer: {
                Text("Drag to reorder. Turn a tab off to remove it from the notch.")
            }

            Section {
                Button("Restore Default Tabs") {
                    order = IslandTab.allCases
                    disabled = []
                    Prefs.set(order.map(\.rawValue), .tabOrder)
                    Prefs.set([String](), .disabledTabs)
                }
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: UserDefaults.didChangeNotification)) { _ in
            let newOrder = Prefs.orderedTabs
            let newDisabled = Set(Prefs.strings(.disabledTabs))
            if newOrder != order { order = newOrder }
            if newDisabled != disabled { disabled = newDisabled }
        }
    }

    private func binding(for tab: IslandTab) -> Binding<Bool> {
        Binding(
            get: { !disabled.contains(tab.rawValue) },
            set: { on in
                if on { disabled.remove(tab.rawValue) } else { disabled.insert(tab.rawValue) }
                Prefs.setTabEnabled(tab, on)
            }
        )
    }
}

private struct ActivitiesPane: View {
    @AppStorage(Prefs.Key.activityMedia.rawValue) private var media = true
    @AppStorage(Prefs.Key.trackPeek.rawValue) private var trackPeek = true
    @AppStorage(Prefs.Key.activityTimer.rawValue) private var timer = true
    @AppStorage(Prefs.Key.activityStopwatch.rawValue) private var stopwatch = true
    @AppStorage(Prefs.Key.activityCharging.rawValue) private var charging = true
    @AppStorage(Prefs.Key.activityBluetooth.rawValue) private var bluetooth = true
    @AppStorage(Prefs.Key.showHUD.rawValue) private var hud = true
    @AppStorage(Prefs.Key.hudVolume.rawValue) private var hudVolume = true
    @AppStorage(Prefs.Key.hudBrightness.rawValue) private var hudBrightness = true
    @AppStorage(Prefs.Key.replaceSystemHUD.rawValue) private var replaceSystemHUD = true
    @State private var trusted = MediaKeyMonitor.hasAccessibility
    private let trustTimer = Timer.publish(every: 2, on: .main, in: .common).autoconnect()

    var body: some View {
        Form {
            Section("In the closed notch") {
                row("Now playing", "Artwork and a live equalizer", "music.note", $media)
                row("Show the song title when the track changes", "A short preview appears for each new song", "text.bubble", $trackPeek)
                    .disabled(!media)
                row("Pomodoro timer", "Countdown while a session runs", "timer", $timer)
                row("Stopwatch", "Elapsed time while running", "stopwatch", $stopwatch)
                row("Charging", "When the charger is connected or removed", "bolt.fill", $charging)
                row("AirPods and headphones", "When they connect or disconnect", "airpodspro", $bluetooth)
            }

            Section("HUD") {
                row("Volume and brightness HUD", "Shown in the notch when levels change", "slider.horizontal.3", $hud)
                row("Volume", nil, "speaker.wave.2.fill", $hudVolume).disabled(!hud)
                row("Brightness and keyboard backlight", "Requires Input Monitoring", "sun.max.fill", $hudBrightness).disabled(!hud)
            }

            Section {
                Toggle(isOn: $replaceSystemHUD) {
                    Label {
                        VStack(alignment: .leading, spacing: 1) {
                            Text("Replace the macOS volume and brightness overlay")
                            Text("FreeNotch handles the keys, so the system overlay never appears")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                    } icon: {
                        Image(systemName: "rectangle.on.rectangle.slash").foregroundStyle(.orange)
                    }
                }
                .onChange(of: replaceSystemHUD) { _, on in
                    if on { AppEnvironment.current?.requestHUDTakeover() }
                }
                if replaceSystemHUD {
                    LabeledContent("Accessibility access") {
                        if trusted {
                            Label("Granted", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                        } else {
                            Button("Grant Access…") { AppEnvironment.current?.requestHUDTakeover() }
                        }
                    }
                }
            } header: {
                Text("System Overlay")
            } footer: {
                Text("Requires Accessibility access in System Settings › Privacy & Security. Hold ⌥⇧ while pressing a key for fine steps.")
            }
            .onReceive(trustTimer) { _ in trusted = MediaKeyMonitor.hasAccessibility }
        }
    }

    private func row(_ title: LocalizedStringKey, _ detail: LocalizedStringKey?, _ symbol: String, _ value: Binding<Bool>) -> some View {
        Toggle(isOn: value) {
            Label {
                VStack(alignment: .leading, spacing: 1) {
                    Text(title)
                    if let detail { Text(detail).font(.caption).foregroundStyle(.secondary) }
                }
            } icon: {
                Image(systemName: symbol).foregroundStyle(.orange)
            }
        }
    }
}

private struct MediaPane: View {
    @Environment(MediaController.self) private var media
    @AppStorage(Prefs.Key.sourceSpotify.rawValue) private var spotify = true
    @AppStorage(Prefs.Key.sourceMusic.rawValue) private var music = true
    @AppStorage(Prefs.Key.sourceBrowsers.rawValue) private var browsers = true
    @AppStorage(Prefs.Key.browserScanAllTabs.rawValue) private var allTabs = false
    @AppStorage(Prefs.Key.browserPollSeconds.rawValue) private var poll = 4.0
    @AppStorage(Prefs.Key.lyricsEnabled.rawValue) private var lyrics = true
    @AppStorage(Prefs.Key.lyricsInCompact.rawValue) private var lyricsCompact = false
    @AppStorage(Prefs.Key.lyricsFontSize.rawValue) private var lyricsSize = 15.0

    var body: some View {
        Form {
            Section("Sources") {
                Toggle("Spotify", isOn: $spotify)
                Toggle("Apple Music", isOn: $music)
                Toggle("Browsers (Safari, Chrome, Brave, Edge, Arc)", isOn: $browsers)
            }

            if browsers {
                Section {
                    LabeledContent("Check every") {
                        HStack {
                            Slider(value: $poll, in: 2...15, step: 1).frame(width: 180)
                            Text(verbatim: "\(Int(poll)) s").monospacedDigit().frame(width: 44, alignment: .trailing)
                        }
                    }
                    Toggle("Scan all tabs", isOn: $allTabs)
                    if let hint = media.browserHint {
                        Label(hint.message, systemImage: "exclamationmark.triangle.fill")
                            .foregroundStyle(.orange)
                            .font(.callout)
                    }
                } header: {
                    Text("Browser Media")
                } footer: {
                    Text("Browsers don't announce playback, so FreeNotch checks only while a browser is open, and only the active tab plus known media sites. Enable “Allow JavaScript from Apple Events” once in your browser's Developer menu.")
                }
            }

            Section {
                Toggle("Enable lyrics", isOn: $lyrics)
                Toggle("Show the current line in the closed notch", isOn: $lyricsCompact).disabled(!lyrics)
                LabeledContent("Text size") {
                    HStack {
                        Slider(value: $lyricsSize, in: 11...24, step: 1).frame(width: 180)
                        Text(verbatim: "\(Int(lyricsSize)) pt").monospacedDigit().frame(width: 44, alignment: .trailing)
                    }
                }
                .disabled(!lyrics)
                Text("Sample lyric line")
                    .font(.system(size: lyricsSize, weight: .bold))
                    .foregroundStyle(lyrics ? .primary : .secondary)
            } header: {
                Text("Lyrics")
            } footer: {
                Text("Lyrics come from LRCLIB (lrclib.net) and are fetched once per song. Synced lines are highlighted as the song plays; click a line to jump to it.")
            }
        }
    }
}

private struct ClipboardPane: View {
    @Environment(ClipboardMonitor.self) private var clipboard
    @AppStorage(Prefs.Key.clipboardEnabled.rawValue) private var enabled = true
    @AppStorage(Prefs.Key.clipboardLimit.rawValue) private var limit = 20

    var body: some View {
        Form {
            Section {
                Toggle("Keep clipboard history", isOn: $enabled)
                Stepper(value: $limit, in: 5...200, step: 5) {
                    Text("Items to keep: \(limit)")
                }
                LabeledContent("Stored now") { Text("\(clipboard.entries.count) items") }
                Button("Clear History", role: .destructive) { clipboard.clear() }
            } header: {
                Text("Clipboard")
            } footer: {
                Text("Content that password managers mark as concealed is never stored. Only text is written to disk.")
            }
        }
    }
}

private struct NotesPane: View {
    @Environment(NotesStore.self) private var notes
    @AppStorage(Prefs.Key.autoLockNotes.rawValue) private var autoLock = true

    var body: some View {
        Form {
            Section {
                Toggle("Lock protected notes again when the notch closes", isOn: $autoLock)
                LabeledContent("Notes") { Text(verbatim: "\(notes.notes.count)") }
                LabeledContent("Locked notes") { Text(verbatim: "\(notes.notes.filter(\.isLocked).count)") }
                Button("Show Database in Finder") {
                    NSWorkspace.shared.activateFileViewerSelecting([NotesDatabase.fileURL])
                }
            } header: {
                Text("Notes")
            } footer: {
                Text("Notes live in a local SQLite database. Locked notes are encrypted with AES-GCM. Your password is never stored, so a forgotten password can't be recovered.")
            }
        }
    }
}

private struct FocusPane: View {
    @AppStorage(Prefs.Key.focusMinutes.rawValue) private var focus = 25
    @AppStorage(Prefs.Key.breakMinutes.rawValue) private var shortBreak = 5

    var body: some View {
        Form {
            Section("Pomodoro") {
                Stepper(value: $focus, in: 5...120, step: 5) { Text("Focus: \(focus) min") }
                Stepper(value: $shortBreak, in: 1...30) { Text("Short break: \(shortBreak) min") }
                LabeledContent("Long break") { Text("15 min, after every 4 sessions") }
            }
        }
    }
}

private struct UpdatesPane: View {
    @Environment(UpdateChecker.self) private var updater
    @AppStorage(Prefs.Key.autoCheckUpdates.rawValue) private var autoCheck = true

    var body: some View {
        Form {
            Section {
                LabeledContent("Current version") { Text(verbatim: updater.currentVersion) }
                Toggle("Check for updates automatically", isOn: $autoCheck)
                    .onChange(of: autoCheck) { _, _ in updater.start() }
                LabeledContent("Last checked") {
                    if let date = updater.lastChecked {
                        Text(date, style: .relative)
                    } else {
                        Text("Never")
                    }
                }
                HStack {
                    statusView
                    Spacer()
                    Button("Check Now") { Task { await updater.check(userInitiated: true) } }
                        .disabled(isBusy)
                }
            } header: {
                Text("Updates")
            } footer: {
                Text("Updates are downloaded and installed right inside FreeNotch. Checks happen once a day in the background.")
            }

            if let release = updater.availableRelease {
                Section {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("FreeNotch \(release.version) is available").font(.headline)
                        if !release.notes.isEmpty {
                            ScrollView {
                                Text(release.notes)
                                    .font(.callout)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .textSelection(.enabled)
                            }
                            .frame(maxHeight: 160)
                        }
                        HStack {
                            Button("Install and Relaunch") { Task { await updater.install(release) } }
                                .buttonStyle(.borderedProminent)
                            Button("Skip This Version") { updater.skip(release) }
                            Spacer()
                            Link("Release Notes", destination: release.pageURL)
                        }
                    }
                    .padding(.vertical, 4)
                }
            }
        }
    }

    private var isBusy: Bool {
        switch updater.state {
        case .checking, .downloading, .installing: true
        default: false
        }
    }

    @ViewBuilder
    private var statusView: some View {
        switch updater.state {
        case .idle: EmptyView()
        case .checking: HStack(spacing: 6) { ProgressView().controlSize(.small); Text("Checking…") }
        case .upToDate: Label("FreeNotch is up to date", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
        case .available(let r): Label("Version \(r.version) is available", systemImage: "arrow.down.circle.fill").foregroundStyle(.blue)
        case .downloading: HStack(spacing: 6) { ProgressView().controlSize(.small); Text("Downloading…") }
        case .installing: HStack(spacing: 6) { ProgressView().controlSize(.small); Text("Installing…") }
        case .failed(let message): Label(message, systemImage: "exclamationmark.triangle.fill").foregroundStyle(.orange)
        }
    }
}

private struct AboutPane: View {
    @Environment(UpdateChecker.self) private var updater

    var body: some View {
        Form {
            Section {
                VStack(spacing: 10) {
                    if let icon = NSImage(named: "AppIcon") ?? NSApp.applicationIconImage {
                        Image(nsImage: icon).resizable().frame(width: 84, height: 84)
                    }
                    Text(verbatim: "FreeNotch").font(.title.bold())
                    Text("Version \(updater.currentVersion)").foregroundStyle(.secondary)
                    Text("A free, open-source Dynamic Island for the MacBook notch. Event-driven, so it uses next to no energy while idle.")
                        .multilineTextAlignment(.center)
                        .foregroundStyle(.secondary)
                        .frame(maxWidth: 420)
                    HStack {
                        Link("GitHub", destination: URL(string: "https://github.com/werquess/FreeNotch")!)
                        Link("Report an Issue", destination: URL(string: "https://github.com/werquess/FreeNotch/issues")!)
                    }
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 8)
            }

            Section {
                RoadmapRow(symbol: "safari", title: "Mini Browser", detail: "A small browser that lives in the notch.")
                RoadmapRow(symbol: "sparkles", title: "Local and API Assistant", detail: "Ask about what's on your screen using a local model or your own API key.")
                RoadmapRow(symbol: "gauge.with.needle", title: "Usage Limits", detail: "See the usage limits of the assistant apps you use, at a glance.")
            } header: {
                Text("Coming in Future Versions")
            }
        }
    }
}

private struct RoadmapRow: View {
    let symbol: String
    let title: LocalizedStringKey
    let detail: LocalizedStringKey

    var body: some View {
        Label {
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.body.weight(.medium))
                Text(detail).font(.caption).foregroundStyle(.secondary)
            }
        } icon: {
            Image(systemName: symbol).foregroundStyle(.tint)
        }
    }
}
