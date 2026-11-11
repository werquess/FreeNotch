import EventKit
import Observation
import SwiftUI

@MainActor
@Observable
final class CalendarService {
    struct Event: Identifiable, Equatable {
        let id: String
        let title: String
        let start: Date
        let end: Date
        let isAllDay: Bool
        let color: Color
        var link: URL? = nil
    }

    private(set) var events: [Event] = []
    private(set) var upcoming: Event?

    @ObservationIgnored private var meetingTask: Task<Void, Never>?

    private static let meetingHosts = ["zoom.us", "meet.google.com", "teams.microsoft.com", "teams.live.com", "webex.com", "facetime.apple.com", "whereby.com", "meet.jit.si"]
    private(set) var status = EKEventStore.authorizationStatus(for: .event)

    @ObservationIgnored private let store = EKEventStore()
    @ObservationIgnored private var observer: NSObjectProtocol?

    var isAuthorized: Bool { status == .fullAccess }

    init() {
        if isAuthorized { beginObserving() }
    }

    func requestAccess() {
        Task {
            _ = try? await store.requestFullAccessToEvents()
            status = EKEventStore.authorizationStatus(for: .event)
            if isAuthorized { beginObserving() }
        }
    }

    private func beginObserving() {
        reload()
        guard observer == nil else { return }
        observer = NotificationCenter.default.addObserver(forName: .EKEventStoreChanged, object: store, queue: .main) { [weak self] _ in
            MainActor.assumeIsolated { self?.reload() }
        }
    }

    func reload() {
        guard isAuthorized else { return }
        let now = Date.now
        let end = Calendar.current.date(byAdding: .hour, value: 24, to: now) ?? now
        let predicate = store.predicateForEvents(withStart: now, end: end, calendars: nil)
        events = store.events(matching: predicate)
            .filter { $0.endDate > now }
            .sorted { $0.startDate < $1.startDate }
            .prefix(5)
            .map {
                Event(id: $0.eventIdentifier ?? UUID().uuidString,
                      title: $0.title ?? String(localized: "Event"),
                      start: $0.startDate,
                      end: $0.endDate,
                      isAllDay: $0.isAllDay,
                      color: Color(cgColor: $0.calendar.cgColor),
                      link: Self.meetingLink(in: $0))
            }
        scheduleMeetingCheck()
    }

    private static func meetingLink(in event: EKEvent) -> URL? {
        if let url = event.url, isMeeting(url) { return url }
        let text = [event.location, event.notes].compactMap { $0 }.joined(separator: " ")
        guard let detector = try? NSDataDetector(types: NSTextCheckingResult.CheckingType.link.rawValue) else { return nil }
        let range = NSRange(text.startIndex..., in: text)
        return detector.matches(in: text, range: range).compactMap(\.url).first(where: isMeeting)
    }

    private static func isMeeting(_ url: URL) -> Bool {
        guard let host = url.host?.lowercased() else { return false }
        return meetingHosts.contains { host == $0 || host.hasSuffix("." + $0) }
    }

    func scheduleMeetingCheck() {
        meetingTask?.cancel()
        let now = Date.now
        let lead: TimeInterval = 5 * 60
        let linger: TimeInterval = 60
        let timed = events.filter { !$0.isAllDay }
        let current = timed.first { $0.start.addingTimeInterval(-lead) <= now && now < $0.start.addingTimeInterval(linger) }
        if current != upcoming { upcoming = current }
        var boundaries = timed.flatMap { [$0.start.addingTimeInterval(-lead), $0.start.addingTimeInterval(linger)] }.filter { $0 > now }
        boundaries.sort()
        let delay = boundaries.first.map { max(1, $0.timeIntervalSince(now) + 0.5) } ?? 1800
        meetingTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(delay))
            guard !Task.isCancelled else { return }
            self?.reload()
        }
    }
}
