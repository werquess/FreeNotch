import AppKit
import Observation
import SwiftUI

enum IslandTheme: String, CaseIterable, Identifiable {
    case oled, graphite, liquidGlass, frosted

    var id: String { rawValue }

    var title: LocalizedStringKey {
        switch self {
        case .oled: "OLED Black"
        case .graphite: "Graphite"
        case .liquidGlass: "Liquid Glass"
        case .frosted: "Frosted"
        }
    }

    var detail: LocalizedStringKey {
        switch self {
        case .oled: "Pure black. Pixels stay off on the notch, lowest power."
        case .graphite: "Soft dark grey surfaces with gentle contrast."
        case .liquidGlass: "Translucent glass that refracts what is behind it."
        case .frosted: "Blurred, frosted material in classic macOS style."
        }
    }

    var isTranslucent: Bool { self == .liquidGlass || self == .frosted }

    static var supportsLiquidGlass: Bool {
        if #available(macOS 26.0, *) { return true }
        return false
    }
}

enum AccentChoice: String, CaseIterable, Identifiable {
    case system, white, red, orange, yellow, green, mint, teal, blue, indigo, purple, pink, custom

    var id: String { rawValue }

    var title: LocalizedStringKey {
        switch self {
        case .system: "System"
        case .white: "White"
        case .red: "Red"
        case .orange: "Orange"
        case .yellow: "Yellow"
        case .green: "Green"
        case .mint: "Mint"
        case .teal: "Teal"
        case .blue: "Blue"
        case .indigo: "Indigo"
        case .purple: "Purple"
        case .pink: "Pink"
        case .custom: "Custom"
        }
    }

    func color(customHex: String) -> NSColor {
        switch self {
        case .system: .controlAccentColor
        case .white: .white
        case .red: .systemRed
        case .orange: .systemOrange
        case .yellow: .systemYellow
        case .green: .systemGreen
        case .mint: .systemMint
        case .teal: .systemTeal
        case .blue: .systemBlue
        case .indigo: .systemIndigo
        case .purple: .systemPurple
        case .pink: .systemPink
        case .custom: NSColor(hexString: customHex) ?? .white
        }
    }
}

@MainActor
@Observable
final class Appearance {
    private(set) var theme: IslandTheme = .oled
    private(set) var accent: Color = .white
    private(set) var onAccent: Color = .black
    private(set) var accentNS: NSColor = .white

    @ObservationIgnored private var observers: [NSObjectProtocol] = []

    init() {
        reload()
        observers.append(NotificationCenter.default.addObserver(forName: UserDefaults.didChangeNotification, object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.reload() }
        })
        observers.append(NotificationCenter.default.addObserver(forName: NSColor.systemColorsDidChangeNotification, object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.reload() }
        })
        observers.append(DistributedNotificationCenter.default().addObserver(forName: .init("AppleColorPreferencesChangedNotification"), object: nil, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.reload() }
        })
    }

    private func reload() {
        var newTheme = IslandTheme(rawValue: Prefs.string(.theme) ?? "") ?? .oled
        if newTheme == .liquidGlass, !IslandTheme.supportsLiquidGlass { newTheme = .frosted }
        if newTheme != theme { theme = newTheme }

        let choice = AccentChoice(rawValue: Prefs.string(.accentChoice) ?? "") ?? .system
        let ns = (choice.color(customHex: Prefs.string(.accentCustomHex) ?? "#FFFFFF").usingColorSpace(.sRGB)) ?? .white
        guard ns != accentNS else { return }
        accentNS = ns
        accent = Color(nsColor: ns)
        let luminance = 0.2126 * ns.redComponent + 0.7152 * ns.greenComponent + 0.0722 * ns.blueComponent
        onAccent = luminance > 0.6 ? .black : .white
    }

    var cardFill: Color {
        switch theme {
        case .oled: .black
        case .graphite: Color.white.opacity(0.055)
        case .liquidGlass, .frosted: Color.white.opacity(0.07)
        }
    }

    var hairline: Color {
        switch theme {
        case .oled: Color.white.opacity(0.10)
        case .graphite: Color.white.opacity(0.07)
        case .liquidGlass, .frosted: Color.white.opacity(0.16)
        }
    }
}

struct IslandBackground: View {
    let theme: IslandTheme
    let large: Bool

    var body: some View {
        ZStack {
            Color.black
            if large {
                switch theme {
                case .oled:
                    EmptyView()
                case .graphite:
                    Color(white: 0.105).transition(.opacity)
                case .liquidGlass:
                    GlassBackdrop(liquid: true).transition(.opacity)
                case .frosted:
                    GlassBackdrop(liquid: false).transition(.opacity)
                }
            }
        }
        .animation(.easeInOut(duration: 0.25), value: large)
        .animation(.easeInOut(duration: 0.25), value: theme)
    }
}

private struct GlassBackdrop: NSViewRepresentable {
    let liquid: Bool

    func makeNSView(context: Context) -> NSView {
        if liquid, #available(macOS 26.0, *) {
            let glass = NSGlassEffectView()
            glass.style = .regular
            glass.tintColor = NSColor.black.withAlphaComponent(0.32)
            glass.cornerRadius = 0
            return glass
        }
        let effect = NSVisualEffectView()
        effect.material = .hudWindow
        effect.blendingMode = .behindWindow
        effect.state = .active
        effect.appearance = NSAppearance(named: .darkAqua)
        return effect
    }

    func updateNSView(_ view: NSView, context: Context) {}
}

extension NSColor {
    convenience init?(hexString: String) {
        let hex = hexString.trimmingCharacters(in: CharacterSet(charactersIn: "# "))
        guard hex.count == 6, let v = UInt32(hex, radix: 16) else { return nil }
        self.init(srgbRed: CGFloat((v >> 16) & 0xff) / 255, green: CGFloat((v >> 8) & 0xff) / 255,
                  blue: CGFloat(v & 0xff) / 255, alpha: 1)
    }

    var hexString: String {
        let c = usingColorSpace(.sRGB) ?? self
        return String(format: "#%02X%02X%02X", Int((c.redComponent * 255).rounded()),
                      Int((c.greenComponent * 255).rounded()), Int((c.blueComponent * 255).rounded()))
    }
}
