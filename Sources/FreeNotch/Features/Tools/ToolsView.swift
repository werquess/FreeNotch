import AppKit
import SwiftUI

struct ToolsView: View {
    enum Tool: String, CaseIterable, Identifiable {
        case camera
        var id: String { rawValue }
    }

    @State private var openTool: Tool?

    var body: some View {
        Group {
            switch openTool {
            case .camera:
                detail(title: String(localized: "Mirror")) {
                    CameraMirrorView()
                        .clipShape(RoundedRectangle(cornerRadius: Theme.cardRadius, style: .continuous))
                }
            case nil:
                grid
            }
        }
        .animation(Motion.tab, value: openTool)
    }

    private var grid: some View {
        HStack(spacing: 8) {
            ToolTile(symbol: "camera.fill", title: String(localized: "Mirror"), tint: .green) { openTool = .camera }
            CaffeinateTile()
            ColorPickerTile()
        }
        .transition(.opacity)
    }

    private func detail<C: View>(title: String, @ViewBuilder content: () -> C) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Button { openTool = nil } label: {
                Label(title, systemImage: "chevron.left")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(Theme.secondary)
            }
            .buttonStyle(.plain)
            content()
        }
        .transition(.opacity.combined(with: .scale(scale: 0.97)))
    }
}

struct ToolTile<Accessory: View>: View {
    let symbol: String
    let title: String
    let tint: Color
    var isOn = false
    var subtitle: String?
    let action: () -> Void
    @ViewBuilder var accessory: Accessory
    @State private var hovering = false

    var body: some View {
        Button(action: action) {
            VStack(spacing: 8) {
                ZStack {
                    Circle().fill(isOn ? tint : tint.opacity(0.16))
                    Image(systemName: symbol)
                        .font(.system(size: 20, weight: .semibold))
                        .foregroundStyle(isOn ? .black : tint)
                        .contentTransition(.symbolEffect(.replace))
                    accessory
                }
                .frame(width: 44, height: 44)
                .symbolEffect(.bounce, value: isOn)

                VStack(spacing: 2) {
                    Text(title)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.9))
                        .contentTransition(.opacity)
                    if let subtitle {
                        Text(subtitle)
                            .font(.system(size: 9, weight: .medium).monospacedDigit())
                            .foregroundStyle(Theme.tertiary)
                            .contentTransition(.opacity)
                    }
                }
                .multilineTextAlignment(.center)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .islandCard(highlighted: hovering)
            .overlay {
                if isOn {
                    RoundedRectangle(cornerRadius: Theme.cardRadius, style: .continuous)
                        .strokeBorder(tint.opacity(0.55), lineWidth: 1)
                }
            }
            .scaleEffect(hovering ? 1.015 : 1)
            .animation(.spring(response: 0.25, dampingFraction: 0.75), value: hovering)
            .animation(.spring(response: 0.3, dampingFraction: 0.8), value: isOn)
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
    }
}

extension ToolTile where Accessory == EmptyView {
    init(symbol: String, title: String, tint: Color, isOn: Bool = false, subtitle: String? = nil, action: @escaping () -> Void) {
        self.init(symbol: symbol, title: title, tint: tint, isOn: isOn, subtitle: subtitle, action: action) { EmptyView() }
    }
}

private struct CaffeinateTile: View {
    @Environment(CaffeineService.self) private var caffeine

    var body: some View {
        ToolTile(symbol: caffeine.isActive ? "cup.and.saucer.fill" : "cup.and.saucer",
                 title: caffeine.isActive ? String(localized: "Awake") : String(localized: "Keep Awake"),
                 tint: .orange,
                 isOn: caffeine.isActive,
                 subtitle: subtitle) {
            caffeine.toggle()
        }
    }

    private var subtitle: String {
        guard let since = caffeine.since else { return String(localized: "Off") }
        return String(localized: "Since \(since.formatted(date: .omitted, time: .shortened))")
    }
}

private struct ColorPickerTile: View {
    @Environment(IslandViewModel.self) private var vm
    @State private var lastHex: String?
    @State private var lastColor: Color?
    @State private var justCopied = false

    var body: some View {
        ToolTile(symbol: justCopied ? "checkmark" : "eyedropper.halffull",
                 title: justCopied ? String(localized: "Copied") : String(localized: "Color Picker"),
                 tint: justCopied ? .green : .pink,
                 isOn: justCopied,
                 subtitle: lastHex ?? String(localized: "Copies HEX to clipboard"),
                 action: pick) {
            if let lastColor, !justCopied {
                Circle()
                    .fill(lastColor)
                    .frame(width: 13, height: 13)
                    .overlay(Circle().strokeBorder(.black, lineWidth: 2))
                    .offset(x: 16, y: 16)
            }
        }
    }

    private func pick() {
        NSColorSampler().show { color in
            guard let c = color?.usingColorSpace(.sRGB) else { return }
            let hex = String(format: "#%02X%02X%02X",
                             Int((c.redComponent * 255).rounded()),
                             Int((c.greenComponent * 255).rounded()),
                             Int((c.blueComponent * 255).rounded()))
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(hex, forType: .string)
            lastHex = hex
            lastColor = Color(nsColor: c)
            justCopied = true
            NSSound(named: "Pop")?.play()

            vm.showTransient(.colorCopied(hex), duration: 2.2)
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.6) { justCopied = false }
        }
    }
}
