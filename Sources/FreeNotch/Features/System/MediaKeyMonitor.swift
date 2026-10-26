import AppKit
import ApplicationServices
import CoreGraphics

@MainActor
final class MediaKeyMonitor {
    enum Key { case volumeUp, volumeDown, mute, brightnessUp, brightnessDown, keyboardUp, keyboardDown }

    var handler: ((Key, Bool) -> Bool)?
    private(set) var isIntercepting = false
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?

    static var hasAccessibility: Bool { AXIsProcessTrusted() }

    static func requestAccessibility() {
        let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
    }

    @discardableResult
    func start() -> Bool {
        stop()
        let intercept = Prefs.bool(.replaceSystemHUD) && Self.hasAccessibility
        let mask = CGEventMask(1 << 14)
        let refcon = Unmanaged.passUnretained(self).toOpaque()
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: intercept ? .defaultTap : .listenOnly,
            eventsOfInterest: mask,
            callback: { _, type, event, refcon in
                guard let refcon else { return Unmanaged.passUnretained(event) }
                let monitor = Unmanaged<MediaKeyMonitor>.fromOpaque(refcon).takeUnretainedValue()
                if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
                    MainActor.assumeIsolated { monitor.reenable() }
                    return Unmanaged.passUnretained(event)
                }
                guard type.rawValue == 14, let ns = NSEvent(cgEvent: event), ns.subtype.rawValue == 8 else {
                    return Unmanaged.passUnretained(event)
                }
                let code = Int((ns.data1 & 0xFFFF_0000) >> 16)
                let isDown = ((ns.data1 & 0xFF00) >> 8) == 0x0A
                let fine = ns.modifierFlags.contains(.option) && ns.modifierFlags.contains(.shift)
                let consumed = MainActor.assumeIsolated { monitor.dispatch(code, isDown: isDown, fine: fine) }
                return consumed ? nil : Unmanaged.passUnretained(event)
            },
            userInfo: refcon
        ) else { return false }

        let src = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), src, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        self.tap = tap
        self.source = src
        isIntercepting = intercept
        return true
    }

    func stop() {
        if let tap { CGEvent.tapEnable(tap: tap, enable: false) }
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        tap = nil
        source = nil
        isIntercepting = false
    }

    private func reenable() {
        if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
    }

    private func dispatch(_ code: Int, isDown: Bool, fine: Bool) -> Bool {
        let key: Key? = switch code {
        case 0: .volumeUp
        case 1: .volumeDown
        case 7: .mute
        case 2: .brightnessUp
        case 3: .brightnessDown
        case 21: .keyboardUp
        case 22: .keyboardDown
        default: nil
        }
        guard let key else { return false }
        if isDown {
            let consumed = handler?(key, fine) ?? false
            return isIntercepting && consumed
        }

        return isIntercepting && key != .keyboardUp && key != .keyboardDown
    }
}
