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
    }

    private(set) var events: [Event] = []
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
                      color: Color(cgColor: $0.calendar.cgColor))
            }
    }
}
