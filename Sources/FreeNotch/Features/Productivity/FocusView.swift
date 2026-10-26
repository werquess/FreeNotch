import SwiftUI

struct FocusView: View {
    @AppStorage(Prefs.Key.focusMode.rawValue) private var mode = "pomodoro"
    @Environment(Appearance.self) private var appearance
    @Namespace private var segNS

    var body: some View {
        HStack(spacing: 8) {
            VStack(spacing: 8) {
                segmented
                Group {
                    if mode == "stopwatch" {
                        StopwatchPanel().transition(.blurFade)
                    } else {
                        PomodoroPanel().transition(.blurFade)
                    }
                }
                .frame(maxHeight: .infinity)
            }
            .padding(10)
            .frame(width: 236)
            .frame(maxHeight: .infinity)
            .islandCard()
            .animation(Motion.tab, value: mode)

            CalendarCard().frame(maxWidth: .infinity)
        }
    }

    private var segmented: some View {
        HStack(spacing: 2) {
            segment("pomodoro", String(localized: "Pomodoro"), "timer")
            segment("stopwatch", String(localized: "Stopwatch"), "stopwatch")
        }
        .padding(2)
        .background(Capsule().fill(Theme.fieldFill))
    }

    private func segment(_ value: String, _ title: String, _ symbol: String) -> some View {
        let selected = mode == value
        return Button {
            withAnimation(Motion.tab) { mode = value }
        } label: {
            Label(title, systemImage: symbol)
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(selected ? appearance.onAccent : Theme.secondary)
                .frame(maxWidth: .infinity)
                .frame(height: 22)
                .background {
                    if selected { Capsule().fill(appearance.accent).matchedGeometryEffect(id: "seg", in: segNS) }
                }
                .contentShape(Capsule())
        }
        .buttonStyle(.plain)
    }
}

private struct Card<Content: View>: View {
    @ViewBuilder var content: Content
    var body: some View {
        content
            .padding(10)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .islandCard()
    }
}

private struct PomodoroPanel: View {
    @Environment(PomodoroTimer.self) private var timer

    var body: some View {
        HStack(spacing: 14) {
            TimelineView(.periodic(from: .now, by: timer.isRunning ? 1 : 3600)) { ctx in
                ZStack {
                    Circle().stroke(.white.opacity(0.1), lineWidth: 6)
                    Circle()
                        .trim(from: 0, to: timer.progress(at: ctx.date))
                        .stroke(Color.orange.gradient, style: StrokeStyle(lineWidth: 6, lineCap: .round))
                        .rotationEffect(.degrees(-90))
                        .animation(.linear(duration: 1), value: timer.progress(at: ctx.date))
                    VStack(spacing: 0) {
                        Text(format(timer.remaining(at: ctx.date)))
                            .font(.system(size: 18, weight: .semibold, design: .rounded).monospacedDigit())
                            .contentTransition(.numericText(countsDown: true))
                        Text(timer.phase.shortTitle)
                            .font(.system(size: 9, weight: .medium))
                            .foregroundStyle(Theme.secondary)
                    }
                }
            }
            .frame(width: 92, height: 92)

            VStack(spacing: 8) {
                Button(action: timer.toggle) {
                    Image(systemName: timer.isRunning ? "pause.fill" : "play.fill")
                        .contentTransition(.symbolEffect(.replace))
                }
                .buttonStyle(IslandButtonStyle(size: 36, filled: true))
                HStack(spacing: 4) {
                    Button(action: timer.reset) { Image(systemName: "arrow.counterclockwise") }
                        .buttonStyle(IslandButtonStyle(size: 26))
                    Button(action: timer.skip) { Image(systemName: "forward.end.fill") }
                        .buttonStyle(IslandButtonStyle(size: 26))
                }
                Text(String(localized: "\(timer.completedFocusSessions) sessions"))
                    .font(.system(size: 9, weight: .medium))
                    .foregroundStyle(Theme.tertiary)
            }
            .font(.system(size: 12, weight: .semibold))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func format(_ t: TimeInterval) -> String {
        let s = Int(t.rounded(.up))
        return String(format: "%02d:%02d", s / 60, s % 60)
    }
}

private struct StopwatchPanel: View {
    @Environment(Stopwatch.self) private var stopwatch

    var body: some View {
        VStack(spacing: 6) {
            TimelineView(.animation(minimumInterval: 1.0 / 30, paused: !stopwatch.isRunning)) { ctx in
                Text(Stopwatch.format(stopwatch.elapsed(at: ctx.date)))
                    .font(.system(size: 28, weight: .semibold, design: .rounded).monospacedDigit())
                    .foregroundStyle(stopwatch.isRunning ? .white : Theme.secondary)
                    .lineLimit(1)
                    .minimumScaleFactor(0.7)
            }
            .frame(height: 34)

            HStack(spacing: 8) {
                Button {
                    stopwatch.isRunning ? stopwatch.lap() : stopwatch.reset()
                } label: {
                    Text(stopwatch.isRunning ? String(localized: "Lap") : String(localized: "Reset"))
                        .font(.system(size: 11, weight: .semibold))
                        .frame(width: 64, height: 26)
                        .background(Capsule().fill(Theme.fieldFill))
                }
                .buttonStyle(.plain)
                .disabled(!stopwatch.hasTime)
                .opacity(stopwatch.hasTime ? 1 : 0.4)

                Button(action: stopwatch.toggle) {
                    Text(stopwatch.isRunning ? String(localized: "Stop") : (stopwatch.hasTime ? String(localized: "Resume") : String(localized: "Start")))
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(stopwatch.isRunning ? .red : .green)
                        .frame(width: 64, height: 26)
                        .background(Capsule().fill((stopwatch.isRunning ? Color.red : Color.green).opacity(0.18)))
                        .contentTransition(.opacity)
                }
                .buttonStyle(.plain)
            }

            ScrollView(showsIndicators: false) {
                VStack(spacing: 2) {
                    ForEach(stopwatch.laps) { lap in
                        HStack {
                            Text(String(localized: "Lap \(lap.id)"))
                            Spacer()
                            Text(Stopwatch.format(lap.split))
                        }
                        .font(.system(size: 10, weight: .medium).monospacedDigit())
                        .foregroundStyle(Theme.secondary)
                        .transition(.move(edge: .top).combined(with: .opacity))
                    }
                }
                .animation(Motion.tab, value: stopwatch.laps)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private struct CalendarCard: View {
    @Environment(CalendarService.self) private var calendar

    var body: some View {
        Card {
            VStack(alignment: .leading, spacing: 6) {
                Label(String(localized: "Today"), systemImage: "calendar")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.6))

                if !calendar.isAuthorized {
                    Spacer()
                    Button(String(localized: "Allow calendar access")) { calendar.requestAccess() }
                        .buttonStyle(.plain)
                        .font(.system(size: 12, weight: .medium))
                        .padding(.horizontal, 12).padding(.vertical, 6)
                        .background(Capsule().fill(.white.opacity(0.12)))
                        .frame(maxWidth: .infinity)
                    Spacer()
                } else if calendar.events.isEmpty {
                    Spacer()
                    Text(String(localized: "No events in the next 24 hours"))
                        .font(.system(size: 11))
                        .foregroundStyle(.white.opacity(0.4))
                        .frame(maxWidth: .infinity)
                    Spacer()
                } else {
                    ForEach(calendar.events) { event in
                        HStack(spacing: 8) {
                            Capsule().fill(event.color).frame(width: 3, height: 26)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(event.title).font(.system(size: 12, weight: .medium)).lineLimit(1)
                                Text(event.isAllDay ? String(localized: "All day") : event.start.formatted(date: .omitted, time: .shortened))
                                    .font(.system(size: 10))
                                    .foregroundStyle(.white.opacity(0.5))
                            }
                        }
                    }
                }
            }
        }
    }
}
