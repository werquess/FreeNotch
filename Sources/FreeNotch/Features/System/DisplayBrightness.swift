import CoreGraphics
import Foundation

enum DisplayBrightness {
    private typealias GetFn = @convention(c) (CGDirectDisplayID, UnsafeMutablePointer<Float>) -> Int32
    private typealias SetFn = @convention(c) (CGDirectDisplayID, Float) -> Int32

    private nonisolated(unsafe) static let handle = dlopen(
        "/System/Library/PrivateFrameworks/DisplayServices.framework/DisplayServices", RTLD_LAZY)

    private static var builtinDisplay: CGDirectDisplayID {
        var ids = [CGDirectDisplayID](repeating: 0, count: 8)
        var count: UInt32 = 0
        CGGetOnlineDisplayList(8, &ids, &count)
        return ids.prefix(Int(count)).first { CGDisplayIsBuiltin($0) != 0 } ?? CGMainDisplayID()
    }

    static func get() -> Float? {
        guard let handle, let sym = dlsym(handle, "DisplayServicesGetBrightness") else { return nil }
        let fn = unsafeBitCast(sym, to: GetFn.self)
        var value: Float = 0
        return fn(builtinDisplay, &value) == 0 ? value : nil
    }

    static func set(_ value: Float) {
        guard let handle, let sym = dlsym(handle, "DisplayServicesSetBrightness") else { return }
        let fn = unsafeBitCast(sym, to: SetFn.self)
        _ = fn(builtinDisplay, min(max(value, 0), 1))
    }
}
