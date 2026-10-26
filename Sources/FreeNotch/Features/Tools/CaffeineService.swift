import Foundation
import IOKit.pwr_mgt
import Observation

@MainActor
@Observable
final class CaffeineService {
    private(set) var isActive = false
    private(set) var since: Date?

    @ObservationIgnored private var displayAssertion: IOPMAssertionID = 0
    @ObservationIgnored private var systemAssertion: IOPMAssertionID = 0
    @ObservationIgnored var onChange: ((Bool) -> Void)?

    init() {
        if Prefs.bool(.caffeineActive) { activate() }
    }

    func toggle() { isActive ? deactivate() : activate() }

    func activate() {
        guard !isActive else { return }
        let reason = "FreeNotch: Keep Awake" as CFString
        let a = IOPMAssertionCreateWithName(kIOPMAssertionTypePreventUserIdleDisplaySleep as CFString,
                                            IOPMAssertionLevel(kIOPMAssertionLevelOn), reason, &displayAssertion)
        let b = IOPMAssertionCreateWithName(kIOPMAssertionTypePreventUserIdleSystemSleep as CFString,
                                            IOPMAssertionLevel(kIOPMAssertionLevelOn), reason, &systemAssertion)
        guard a == kIOReturnSuccess || b == kIOReturnSuccess else { return }
        isActive = true
        since = .now
        Prefs.set(true, .caffeineActive)
        onChange?(true)
    }

    func deactivate() {
        guard isActive else { return }
        if displayAssertion != 0 { IOPMAssertionRelease(displayAssertion) }
        if systemAssertion != 0 { IOPMAssertionRelease(systemAssertion) }
        displayAssertion = 0
        systemAssertion = 0
        isActive = false
        since = nil
        Prefs.set(false, .caffeineActive)
        onChange?(false)
    }

    func releaseForTermination() {
        if displayAssertion != 0 { IOPMAssertionRelease(displayAssertion) }
        if systemAssertion != 0 { IOPMAssertionRelease(systemAssertion) }
    }
}
