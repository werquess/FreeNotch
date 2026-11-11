import SwiftUI
import UniformTypeIdentifiers

struct CompactActivityView: View {
    let activity: LiveActivity
    let notchWidth: CGFloat
    let notchHeight: CGFloat

    @Environment(MediaController.self) private var media
    @Environment(PomodoroTimer.self) private var pomodoro
    @Environment(IslandViewModel.self) private var vm
    @Environment(Stopwatch.self) private var stopwatch
    @Environment(Appearance.self) private var appearance
    @Environment(LiveSignals.self) private var signals

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
                    .transition(.opacity.combined(with: .offset(y: -4)))
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
                MarqueeText(text: name, weight: .medium)
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
        case .capture(let mic, let camera):
            if camera {
                privacyDot(.green, symbol: "video.fill")
            } else if mic {
                privacyDot(.orange, symbol: "mic.fill")
            }
        case .meeting(let title, _):
            HStack(spacing: 6) {
                Image(systemName: "calendar")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(.red)
                MarqueeText(text: title)
            }
        case .capsLock:
            Image(systemName: "capslock.fill")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(.white)
        case .keepAwake:
            Image(systemName: "cup.and.saucer.fill")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(Color(red: 0.85, green: 0.62, blue: 0.38))
        case .offline:
            Image(systemName: "wifi.slash")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(.red)
        case .online:
            Image(systemName: "wifi")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(.green)
                .symbolEffect(.variableColor.iterative, options: .nonRepeating, value: activity)
        case .thermal:
            Image(systemName: "thermometer.high")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(.orange)
        case .screenshot:
            Group {
                if let image = signals.screenshot {
                    Image(nsImage: image)
                        .resizable()
                        .aspectRatio(contentMode: .fill)
                        .frame(width: 30, height: 20)
                        .clipShape(RoundedRectangle(cornerRadius: 4, style: .continuous))
                        .overlay(RoundedRectangle(cornerRadius: 4, style: .continuous).strokeBorder(.white.opacity(0.25), lineWidth: 0.5))
                } else {
                    Image(systemName: "camera.viewfinder")
                        .font(.system(size: 13, weight: .semibold))
                }
            }
        case .download(let name):
            Image(nsImage: NSWorkspace.shared.icon(for: UTType(filenameExtension: (name as NSString).pathExtension) ?? .data))
                .resizable()
                .frame(width: 20, height: 20)
        case .audioOutput(_, let symbol):
            Image(systemName: symbol)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(.white)
                .symbolEffect(.bounce, value: activity)
        case .batteryLow:
            Image(systemName: "battery.25percent")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(.red)
                .symbolEffect(.pulse, options: .repeat(2), value: activity)
        }
    }

    private func privacyDot(_ color: Color, symbol: String) -> some View {
        Image(systemName: symbol)
            .font(.system(size: 8, weight: .bold))
            .foregroundStyle(.black)
            .frame(width: 16, height: 16)
            .background(Circle().fill(color))
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
            MarqueeText(text: title, alignment: .trailing)
                .foregroundStyle(.orange)
        case .capture(let mic, let camera):
            if mic && camera {
                privacyDot(.orange, symbol: "mic.fill")
            } else {
                Text(camera ? String(localized: "Camera") : String(localized: "Mic"))
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(camera ? .green : .orange)
                    .lineLimit(1)
            }
        case .meeting(_, let start):
            Group {
                if start > .now {
                    Text(timerInterval: Date.now...start, countsDown: true)
                        .multilineTextAlignment(.trailing)
                } else {
                    Text(String(localized: "Now"))
                }
            }
            .font(.system(size: 13, weight: .semibold, design: .rounded).monospacedDigit())
            .foregroundStyle(.red)
        case .capsLock:
            Text(verbatim: "A")
                .font(.system(size: 12, weight: .bold, design: .rounded))
                .foregroundStyle(.white)
        case .keepAwake:
            Image(systemName: "bolt.fill")
                .font(.system(size: 10, weight: .bold))
                .foregroundStyle(Color(red: 0.85, green: 0.62, blue: 0.38))
        case .offline:
            Text(String(localized: "Offline"))
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.red)
        case .online:
            Text(String(localized: "Back online"))
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.green)
                .lineLimit(1)
        case .thermal:
            Text(String(localized: "Running hot"))
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(.orange)
                .lineLimit(1)
        case .screenshot:
            Text(String(localized: "Screenshot"))
                .font(.system(size: 11, weight: .semibold))
                .lineLimit(1)
        case .download(let name):
            MarqueeText(text: name, alignment: .trailing)
        case .audioOutput(let name, _):
            MarqueeText(text: name, alignment: .trailing)
        case .batteryLow(let level):
            Text(Double(level) / 100, format: .percent)
                .font(.system(size: 13, weight: .bold, design: .rounded).monospacedDigit())
                .foregroundStyle(.red)
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
                            Image(systemName: "bolt.fill")
                                .font(.system(size: height * (level >= 100 ? 0.5 : 0.62), weight: .heavy))
                        }
                        Text(verbatim: "\(level)")
                            .font(.system(size: height * (level >= 100 ? 0.64 : 0.74), weight: .bold, design: .rounded).monospacedDigit())
                            .tracking(level >= 100 ? -0.3 : 0)
                            .lineLimit(1)
                            .minimumScaleFactor(0.5)
                    }
                    .padding(.horizontal, height * 0.08)
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
                    .id(ObjectIdentifier(image))
                    .transition(.opacity)
            } else {
                ZStack {
                    LinearGradient(colors: [.gray.opacity(0.5), .gray.opacity(0.2)], startPoint: .topLeading, endPoint: .bottomTrailing)
                    Image(systemName: "music.note")
                        .font(.system(size: size * 0.42, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.7))
                }
                .transition(.opacity)
            }
        }
        .frame(width: size, height: size)
        .animation(.easeInOut(duration: 0.3), value: image.map(ObjectIdentifier.init))
        .clipShape(RoundedRectangle(cornerRadius: corner, style: .continuous))
    }
}

extension Color {
    init(hex: String) {
        let v = UInt32(hex.trimmingCharacters(in: CharacterSet(charactersIn: "#")), radix: 16) ?? 0
        self.init(red: Double((v >> 16) & 0xff) / 255, green: Double((v >> 8) & 0xff) / 255, blue: Double(v & 0xff) / 255)
    }
}
