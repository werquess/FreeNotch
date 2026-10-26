import Foundation
import Observation

@MainActor
@Observable
final class PomodoroTimer {
    enum Phase: String {
        case focus, shortBreak, longBreak

        var title: String {
            switch self {
            case .focus: String(localized: "Focus done")
            case .shortBreak: String(localized: "Break over")
            case .longBreak: String(localized: "Long break over")
            }
        }

        var shortTitle: String {
            switch self {
            case .focus: String(localized: "Focus")
            case .shortBreak: String(localized: "Break")
            case .longBreak: String(localized: "Long break")
            }
        }
    }

    private(set) var phase: Phase = .focus
    private(set) var endDate: Date?
    private(set) var pausedRemaining: TimeInterval?
    private(set) var completedFocusSessions = 0

    @ObservationIgnored var onPhaseComplete: ((Phase) -> Void)?
    @ObservationIgnored private var completionTimer: DispatchSourceTimer?

    var isRunning: Bool { endDate != nil }
    var isPaused: Bool { pausedRemaining != nil }

    var phaseDuration: TimeInterval {
        switch phase {
        case .focus: Double(max(1, Prefs.int(.focusMinutes))) * 60
        case .shortBreak: Double(max(1, Prefs.int(.breakMinutes))) * 60
        case .longBreak: 15 * 60
        }
    }

    func remaining(at date: Date = .now) -> TimeInterval {
        if let endDate { return max(0, endDate.timeIntervalSince(date)) }
        return pausedRemaining ?? phaseDuration
    }

    func progress(at date: Date = .now) -> Double {
        1 - remaining(at: date) / phaseDuration
    }

    func start() {
        let duration = pausedRemaining ?? phaseDuration
        pausedRemaining = nil
        endDate = Date.now.addingTimeInterval(duration)
        scheduleCompletion(after: duration)
    }

    func pause() {
        guard let endDate else { return }
        pausedRemaining = max(0, endDate.timeIntervalSinceNow)
        self.endDate = nil
        cancelCompletion()
    }

    func toggle() { isRunning ? pause() : start() }

    func reset() {
        cancelCompletion()
        endDate = nil
        pausedRemaining = nil
    }

    func skip() {
        reset()
        advancePhase()
    }

    private func advancePhase() {
        if phase == .focus {
            completedFocusSessions += 1
            phase = completedFocusSessions % 4 == 0 ? .longBreak : .shortBreak
        } else {
            phase = .focus
        }
    }

    private func scheduleCompletion(after seconds: TimeInterval) {
        cancelCompletion()
        let t = DispatchSource.makeTimerSource(queue: .main)
        t.schedule(deadline: .now() + seconds, leeway: .milliseconds(200))
        t.setEventHandler { [weak self] in
            MainActor.assumeIsolated { self?.complete() }
        }
        t.resume()
        completionTimer = t
    }

    private func cancelCompletion() {
        completionTimer?.cancel()
        completionTimer = nil
    }

    private func complete() {
        let finished = phase
        cancelCompletion()
        endDate = nil
        pausedRemaining = nil
        advancePhase()
        onPhaseComplete?(finished)
    }
}
