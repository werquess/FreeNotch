import SwiftUI

struct SystemView: View {
    @Environment(SystemMonitor.self) private var system
    @Environment(VolumeController.self) private var volume
    @Environment(Appearance.self) private var appearance
    @State private var brightness: Float = DisplayBrightness.get() ?? 0.5

    var body: some View {
        HStack(spacing: 8) {
            VStack(spacing: 8) {
                LevelSlider(symbol: volumeSymbol,
                            value: Binding(get: { volume.isMuted ? 0 : volume.volume }, set: { volume.setVolume($0) }),
                            tint: appearance.accent)
                LevelSlider(symbol: brightness < 0.5 ? "sun.min.fill" : "sun.max.fill",
                            value: Binding(get: { brightness }, set: { brightness = $0; DisplayBrightness.set($0) }),
                            tint: .yellow)
                BatteryCard()
            }
            .frame(width: 228)

            CPUTile()
            TemperatureTile()
            MemoryTile()
        }
        .onAppear { system.beginSampling() }
        .onDisappear { system.endSampling() }
    }

    private var volumeSymbol: String {
        if volume.isMuted || volume.volume <= 0.001 { return "speaker.slash.fill" }
        return volume.volume < 0.33 ? "speaker.wave.1.fill" : volume.volume < 0.66 ? "speaker.wave.2.fill" : "speaker.wave.3.fill"
    }
}

private struct LevelSlider: View {
    let symbol: String
    @Binding var value: Float
    let tint: Color
    @State private var dragging = false

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: symbol)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(tint == .white ? .white : tint)
                .frame(width: 18)
                .contentTransition(.symbolEffect(.replace))
            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule().fill(.white.opacity(0.12))
                    Capsule().fill(tint).frame(width: max(0, geo.size.width * CGFloat(value)))
                }
                .frame(height: dragging ? 8 : 6)
                .frame(maxHeight: .infinity)
                .contentShape(Rectangle())
                .gesture(DragGesture(minimumDistance: 0)
                    .onChanged { g in
                        dragging = true
                        value = Float(min(max(0, g.location.x / geo.size.width), 1))
                    }
                    .onEnded { _ in dragging = false })
                .animation(.spring(response: 0.25, dampingFraction: 0.8), value: dragging)
            }
            Text(verbatim: "\(Int((value * 100).rounded()))")
                .font(.system(size: 11, weight: .semibold, design: .rounded).monospacedDigit())
                .foregroundStyle(Theme.secondary)
                .frame(width: 24, alignment: .trailing)
                .contentTransition(.numericText())
        }
        .padding(.horizontal, 11)
        .frame(height: 34)
        .islandCard()
    }
}

private struct BatteryCard: View {
    @Environment(SystemMonitor.self) private var system

    var body: some View {
        HStack(spacing: 12) {
            MacBatteryIcon(level: system.batteryLevel, charging: system.isCharging,
                           lowPower: ProcessInfo.processInfo.isLowPowerModeEnabled, height: 22)
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.system(size: 12, weight: .semibold))
                Text(detail)
                    .font(.system(size: 10, weight: .medium).monospacedDigit())
                    .foregroundStyle(Theme.tertiary)
                    .lineLimit(1)
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, 12)
        .frame(maxHeight: .infinity)
        .islandCard()
    }

    private var title: String {
        if system.isCharging { return String(localized: "Charging") }
        if system.isPluggedIn { return String(localized: "Plugged in") }
        return String(localized: "Battery")
    }

    private var detail: String {
        var parts: [String] = []
        if !system.isPluggedIn, let m = system.minutesRemaining {
            parts.append(m >= 60 ? String(localized: "\(m / 60) h \(m % 60) min left") : String(localized: "\(m) min left"))
        }
        if let t = system.batteryTemperature { parts.append(String(format: "%.0f°", t)) }
        if parts.isEmpty { parts.append(system.isPluggedIn ? String(localized: "Fully charged") : String(localized: "Calculating…")) }
        return parts.joined(separator: " · ")
    }
}

private struct StatTile<Content: View>: View {
    let title: String
    @ViewBuilder var content: Content

    var body: some View {
        VStack(spacing: 6) {
            content
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            Text(title)
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(Theme.tertiary)
                .textCase(.uppercase)
                .tracking(0.6)
        }
        .padding(.vertical, 10)
        .padding(.horizontal, 8)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .islandCard()
    }
}

private struct Ring: View {
    let value: Double
    let tint: Color
    var lineWidth: CGFloat = 5

    var body: some View {
        ZStack {
            Circle().stroke(.white.opacity(0.1), lineWidth: lineWidth)
            Circle()
                .trim(from: 0, to: min(max(value, 0), 1))
                .stroke(tint.gradient, style: StrokeStyle(lineWidth: lineWidth, lineCap: .round))
                .rotationEffect(.degrees(-90))
        }
        .animation(.smooth(duration: 0.6), value: value)
    }
}

private struct CPUTile: View {
    @Environment(SystemMonitor.self) private var system

    var body: some View {
        StatTile(title: "CPU") {
            VStack(spacing: 6) {
                ZStack {
                    Ring(value: system.cpuUsage, tint: .cyan)
                    Text(verbatim: "\(Int((system.cpuUsage * 100).rounded()))%")
                        .font(.system(size: 13, weight: .bold, design: .rounded).monospacedDigit())
                        .contentTransition(.numericText())
                }
                .frame(width: 52, height: 52)
                Sparkline(values: system.cpuHistory)
                    .stroke(Color.cyan.opacity(0.7), style: StrokeStyle(lineWidth: 1.2, lineCap: .round, lineJoin: .round))
                    .frame(height: 14)
                    .padding(.horizontal, 6)
            }
        }
    }
}

private struct TemperatureTile: View {
    @Environment(SystemMonitor.self) private var system

    var body: some View {
        StatTile(title: String(localized: "Temperature")) {
            VStack(spacing: 3) {
                if let t = system.cpuTemperature {
                    Text(verbatim: "\(Int(t.rounded()))°")
                        .font(.system(size: 34, weight: .semibold, design: .rounded).monospacedDigit())
                        .foregroundStyle(color(for: t).gradient)
                        .contentTransition(.numericText(value: t))
                        .animation(.smooth, value: Int(t.rounded()))
                } else {
                    Text("—").font(.system(size: 30, weight: .semibold, design: .rounded))
                }
                fanLine
            }
        }
    }

    @ViewBuilder
    private var fanLine: some View {
        if let fan = system.fans.first {
            VStack(spacing: 3) {
                Text(fan.rpm < 1 ? String(localized: "Fan idle") : String(localized: "Fan \(Int(fan.rpm.rounded())) rpm"))
                    .font(.system(size: 10, weight: .medium).monospacedDigit())
                    .foregroundStyle(Theme.secondary)
                    .contentTransition(.numericText())
                GeometryReader { geo in
                    ZStack(alignment: .leading) {
                        Capsule().fill(.white.opacity(0.1))
                        Capsule().fill(Color.teal)
                            .frame(width: geo.size.width * CGFloat(fan.max > 0 ? min(fan.rpm / fan.max, 1) : 0))
                    }
                }
                .frame(width: 54, height: 3)
                .animation(.smooth(duration: 0.6), value: fan.rpm)
            }
        } else {
            Text(thermalText)
                .font(.system(size: 10, weight: .medium))
                .foregroundStyle(Theme.secondary)
        }
    }

    private var thermalText: String {
        switch system.thermalState {
        case .nominal: String(localized: "Normal")
        case .fair: String(localized: "Warm")
        case .serious: String(localized: "Hot")
        case .critical: String(localized: "Critical")
        @unknown default: "—"
        }
    }

    private func color(for t: Double) -> Color {
        t < 60 ? .green : t < 80 ? .yellow : t < 95 ? .orange : .red
    }
}

private struct MemoryTile: View {
    @Environment(SystemMonitor.self) private var system

    var body: some View {
        let ratio = Double(system.memoryUsed) / Double(max(system.memoryTotal, 1))
        StatTile(title: String(localized: "Memory")) {
            VStack(spacing: 6) {
                ZStack {
                    Ring(value: ratio, tint: .purple)
                    Text(verbatim: "\(Int((ratio * 100).rounded()))%")
                        .font(.system(size: 13, weight: .bold, design: .rounded).monospacedDigit())
                        .contentTransition(.numericText())
                }
                .frame(width: 52, height: 52)
                Text(memoryText)
                    .font(.system(size: 10, weight: .medium).monospacedDigit())
                    .foregroundStyle(Theme.secondary)
            }
        }
    }

    private var memoryText: String {
        let gb = 1_073_741_824.0
        return String(format: "%.1f / %.0f GB", Double(system.memoryUsed) / gb, Double(system.memoryTotal) / gb)
    }
}

private struct Sparkline: Shape {
    var values: [Double]

    func path(in rect: CGRect) -> Path {
        var p = Path()
        guard values.count > 1 else { return p }
        let step = rect.width / CGFloat(values.count - 1)
        for (i, v) in values.enumerated() {
            let pt = CGPoint(x: CGFloat(i) * step, y: rect.maxY - CGFloat(min(max(v, 0), 1)) * rect.height)
            i == 0 ? p.move(to: pt) : p.addLine(to: pt)
        }
        return p
    }
}
