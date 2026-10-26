import SwiftUI

struct CompactActivityView: View {
    let activity: LiveActivity
    let notchWidth: CGFloat
    let notchHeight: CGFloat
    let ns: Namespace.ID

    @Environment(MediaController.self) private var media
    @Environment(PomodoroTimer.self) private var pomodoro
    @Environment(IslandViewModel.self) private var vm
    @Environment(Stopwatch.self) private var stopwatch
    @Environment(Appearance.self) private var appearance

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 0) {
                leading
                    .frame(width: activity.sideWidth, alignment: .leading)
                    .padding(.leading, 10)
                Spacer(minLength: notchWidth - 20)
                trailing
                    .frame(width: activity.sideWidth, alignment: .trailing)
                    .padding(.trailing, 10)
            }
            .frame(height: notchHeight)

            if activity == .trackPeek {
                peekDetails
            } else if activity == .media, vm.showsLyricLine, let session = media.playing {
                CompactLyricLine(session: session)
                    .frame(height: 22)
                    .transition(.opacity)
            }
        }
    }

    @ViewBuilder
    private var leading: some View {
        switch activity {
        case .media, .trackPeek:
            ArtworkView(image: media.playing?.artwork ?? media.current?.artwork, size: activity == .trackPeek ? 24 : 22, corner: 6)
                .matchedGeometryEffect(id: "artwork", in: ns)
        case .timer:
            Label {
                Text(pomodoro.phase.shortTitle)
            } icon: {
                Image(systemName: "timer")
            }
            .font(.system(size: 12, weight: .semibold))
            .foregroundStyle(.orange)
            .labelStyle(.iconOnly)
        case .charging(_, let pluggedIn):
            Image(systemName: pluggedIn ? "bolt.fill" : "powerplug")
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(pluggedIn ? .green : .white.opacity(0.7))
                .symbolEffect(.bounce, value: pluggedIn)
        case .bluetooth(let name, let symbol, _):
            HStack(spacing: 6) {
                Image(systemName: symbol).font(.system(size: 13, weight: .semibold))
                Text(name).font(.system(size: 11, weight: .medium)).lineLimit(1)
            }
            .foregroundStyle(.white)
        case .updateAvailable:
            Image(systemName: "arrow.down.circle.fill")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(appearance.accent)
                .symbolEffect(.bounce, value: activity)
        case .stopwatch:
            Image(systemName: "stopwatch.fill")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(.orange)
        case .colorCopied(let hex):
            Circle()
                .fill(Color(hex: hex))
                .frame(width: 16, height: 16)
                .overlay(Circle().strokeBorder(.white.opacity(0.3), lineWidth: 1))
        case .shelfAdded:
            Image(systemName: "tray.and.arrow.down.fill")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(Color(red: 0.3, green: 0.6, blue: 1))
                .symbolEffect(.bounce, value: activity)
        case .timerDone:
            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(.orange)
                .symbolEffect(.bounce, options: .repeat(2), value: activity)
        }
    }

    @ViewBuilder
    private var trailing: some View {
        switch activity {
        case .media, .trackPeek:
            AudioBarsView(isAnimating: media.isPlaying, color: (media.playing ?? media.current)?.accent ?? .white)
                .frame(width: 18, height: 14)
        case .timer:
            if let end = pomodoro.endDate {
                Text(end, style: .timer)
                    .font(.system(size: 13, weight: .semibold, design: .rounded).monospacedDigit())
                    .foregroundStyle(.orange)
                    .multilineTextAlignment(.trailing)
            } else {
                Image(systemName: "pause.fill").foregroundStyle(.orange)
            }
        case .charging(let level, let pluggedIn):
            MacBatteryIcon(level: level, charging: pluggedIn, height: 14)
        case .bluetooth(_, _, let connected):
            Text(connected ? String(localized: "Connected") : String(localized: "Disconnected"))
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(connected ? .green : .white.opacity(0.6))
        case .updateAvailable(let version):
            Text(verbatim: "v\(version)")
                .font(.system(size: 11, weight: .semibold).monospacedDigit())
                .foregroundStyle(appearance.accent)
        case .stopwatch:
            Text(stopwatch.referenceDate, style: .timer)
                .font(.system(size: 13, weight: .semibold, design: .rounded).monospacedDigit())
                .foregroundStyle(.orange)
                .multilineTextAlignment(.trailing)
        case .colorCopied(let hex):
            HStack(spacing: 4) {
                Image(systemName: "checkmark").font(.system(size: 10, weight: .bold))
                Text(hex).font(.system(size: 11, weight: .semibold).monospaced())
            }
            .foregroundStyle(.green)
        case .shelfAdded(let count):
            Text(count == 1 ? String(localized: "1 file") : String(localized: "\(count) files"))
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(Color(red: 0.3, green: 0.6, blue: 1))
        case .timerDone(let title):
            Text(title)
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.orange)
                .lineLimit(1)
        }
    }

    private var peekDetails: some View {
        VStack(spacing: 2) {
            Text((media.playing ?? media.current)?.title ?? "")
                .font(.system(size: 13, weight: .semibold))
                .lineLimit(1)
            Text((media.playing ?? media.current)?.artist ?? "")
                .font(.system(size: 11))
                .foregroundStyle(.white.opacity(0.6))
                .lineLimit(1)
        }
        .padding(.horizontal, 20)
        .frame(maxWidth: .infinity)
        .padding(.top, 6)
    }
}

struct MacBatteryIcon: View {
    let level: Int
    var charging = false
    var lowPower = false
    var showsPercent = true
    var height: CGFloat = 12

    var body: some View {
        let width = height * 2.15
        let radius = height * 0.34
        HStack(spacing: height * 0.07) {
            ZStack {
                RoundedRectangle(cornerRadius: radius, style: .continuous)
                    .fill(.white.opacity(0.3))
                Rectangle()
                    .fill(fillColor)
                    .frame(width: width * CGFloat(min(max(level, 0), 100)) / 100)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if showsPercent {
                    HStack(spacing: 0) {
                        if charging {
                            Image(systemName: "bolt.fill").font(.system(size: height * 0.62, weight: .heavy))
                        }
                        Text(verbatim: "\(level)")
                            .font(.system(size: height * 0.74, weight: .bold, design: .rounded).monospacedDigit())
                    }
                    .blendMode(.destinationOut)
                }
            }
            .frame(width: width, height: height)
            .clipShape(RoundedRectangle(cornerRadius: radius, style: .continuous))
            .compositingGroup()

            UnevenRoundedRectangle(topLeadingRadius: 0, bottomLeadingRadius: 0,
                                   bottomTrailingRadius: height * 0.12, topTrailingRadius: height * 0.12)
                .fill(.white.opacity(0.35))
                .frame(width: height * 0.13, height: height * 0.36)
        }
        .animation(.smooth(duration: 0.4), value: level)
        .accessibilityLabel(String(localized: "Battery \(level) percent"))
    }

    private var fillColor: Color {
        if charging { return .green }
        if lowPower { return .yellow }
        return level <= 20 ? .red : .white
    }
}

struct ArtworkView: View {
    let image: NSImage?
    let size: CGFloat
    let corner: CGFloat

    var body: some View {
        ZStack {
            if let image {
                Image(nsImage: image)
                    .resizable()
                    .interpolation(.high)
                    .aspectRatio(contentMode: .fill)
            } else {
                LinearGradient(colors: [.gray.opacity(0.5), .gray.opacity(0.2)], startPoint: .topLeading, endPoint: .bottomTrailing)
                Image(systemName: "music.note")
                    .font(.system(size: size * 0.42, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.7))
            }
        }
        .frame(width: size, height: size)
        .clipShape(RoundedRectangle(cornerRadius: corner, style: .continuous))
    }
}

extension Color {
    init(hex: String) {
        let v = UInt32(hex.trimmingCharacters(in: CharacterSet(charactersIn: "#")), radix: 16) ?? 0
        self.init(red: Double((v >> 16) & 0xff) / 255, green: Double((v >> 8) & 0xff) / 255, blue: Double(v & 0xff) / 255)
    }
}
