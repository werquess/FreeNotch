import Foundation
import Observation

@MainActor
@Observable
final class Stopwatch {
    struct Lap: Identifiable, Equatable {
        let id: Int
        let total: TimeInterval
        let split: TimeInterval
    }

    private(set) var startDate: Date?
    private(set) var accumulated: TimeInterval = 0
    private(set) var laps: [Lap] = []

    var isRunning: Bool { startDate != nil }
    var hasTime: Bool { isRunning || accumulated > 0 }

    func elapsed(at date: Date = .now) -> TimeInterval {
        accumulated + (startDate.map { date.timeIntervalSince($0) } ?? 0)
    }

    var referenceDate: Date { Date.now.addingTimeInterval(-elapsed()) }

    func toggle() { isRunning ? pause() : start() }

    func start() {
        guard !isRunning else { return }
        startDate = .now
    }

    func pause() {
        guard let startDate else { return }
        accumulated += Date.now.timeIntervalSince(startDate)
        self.startDate = nil
    }

    func lap() {
        let total = elapsed()
        let split = total - (laps.first?.total ?? 0)
        laps.insert(Lap(id: laps.count + 1, total: total, split: split), at: 0)
    }

    func reset() {
        startDate = nil
        accumulated = 0
        laps = []
    }

    static func format(_ t: TimeInterval, hundredths: Bool = true) -> String {
        let total = max(0, t)
        let h = Int(total) / 3600, m = (Int(total) / 60) % 60, s = Int(total) % 60
        let cs = Int((total - floor(total)) * 100)
        let base = h > 0 ? String(format: "%d:%02d:%02d", h, m, s) : String(format: "%02d:%02d", m, s)
        return hundredths ? base + String(format: ",%02d", cs) : base
    }
}
