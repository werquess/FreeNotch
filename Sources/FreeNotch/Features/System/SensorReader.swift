import Foundation
import IOKit

final class SensorReader {
    struct Reading {
        var cpuTemperature: Double?
        var batteryTemperature: Double?
        var fans: [Fan]
    }

    struct Fan {
        var rpm: Double
        var min: Double
        var max: Double
    }

    private typealias CreateFn = @convention(c) (CFAllocator?) -> Unmanaged<AnyObject>?
    private typealias SetMatchingFn = @convention(c) (AnyObject, CFDictionary) -> Int32
    private typealias CopyServicesFn = @convention(c) (AnyObject) -> Unmanaged<CFArray>?
    private typealias CopyPropertyFn = @convention(c) (AnyObject, CFString) -> Unmanaged<AnyObject>?
    private typealias CopyEventFn = @convention(c) (AnyObject, Int64, Int32, Int64) -> Unmanaged<AnyObject>?
    private typealias GetFloatFn = @convention(c) (AnyObject, Int32) -> Double

    private let copyEvent: CopyEventFn?
    private let getFloat: GetFloatFn?
    private var cpuServices: [AnyObject] = []
    private var batteryServices: [AnyObject] = []
    private var client: AnyObject?

    private var smc: io_connect_t = 0

    init() {
        let h = dlopen("/System/Library/Frameworks/IOKit.framework/IOKit", RTLD_LAZY)
        func sym<T>(_ name: String, _: T.Type) -> T? {
            guard let h, let p = dlsym(h, name) else { return nil }
            return unsafeBitCast(p, to: T.self)
        }
        copyEvent = sym("IOHIDServiceClientCopyEvent", CopyEventFn.self)
        getFloat = sym("IOHIDEventGetFloatValue", GetFloatFn.self)

        if let create = sym("IOHIDEventSystemClientCreate", CreateFn.self),
           let setMatching = sym("IOHIDEventSystemClientSetMatching", SetMatchingFn.self),
           let copyServices = sym("IOHIDEventSystemClientCopyServices", CopyServicesFn.self),
           let copyProperty = sym("IOHIDServiceClientCopyProperty", CopyPropertyFn.self),
           let c = create(kCFAllocatorDefault)?.takeRetainedValue() {
            client = c

            _ = setMatching(c, ["PrimaryUsagePage": 0xff00, "PrimaryUsage": 5] as CFDictionary)
            let services = (copyServices(c)?.takeRetainedValue() as? [AnyObject]) ?? []
            for s in services {
                let name = (copyProperty(s, "Product" as CFString)?.takeRetainedValue() as? String) ?? ""
                if name.contains("tdie") { cpuServices.append(s) }
                else if name.localizedCaseInsensitiveContains("battery") { batteryServices.append(s) }
            }
        }

        let service = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("AppleSMC"))
        if service != 0 {
            if IOServiceOpen(service, mach_task_self_, 0, &smc) != kIOReturnSuccess { smc = 0 }
            IOObjectRelease(service)
        }
    }

    deinit {
        if smc != 0 { IOServiceClose(smc) }
    }

    func read() -> Reading {
        Reading(cpuTemperature: maxTemperature(cpuServices),
                batteryTemperature: maxTemperature(batteryServices),
                fans: readFans())
    }

    private func maxTemperature(_ services: [AnyObject]) -> Double? {
        guard let copyEvent, let getFloat else { return nil }
        var best: Double?
        for s in services {
            guard let event = copyEvent(s, 15, 0, 0)?.takeRetainedValue() else { continue }
            let t = getFloat(event, 15 << 16)
            if t > 0, t < 130 { best = max(best ?? 0, t) }
        }
        return best
    }

    private struct KeyData {
        struct Vers { var major: UInt8 = 0, minor: UInt8 = 0, build: UInt8 = 0, reserved: UInt8 = 0, release: UInt16 = 0 }
        struct PLimit { var version: UInt16 = 0, length: UInt16 = 0, cpu: UInt32 = 0, gpu: UInt32 = 0, mem: UInt32 = 0 }
        struct KeyInfo { var dataSize: UInt32 = 0, dataType: UInt32 = 0, dataAttributes: UInt8 = 0 }
        var key: UInt32 = 0
        var vers = Vers()
        var pLimit = PLimit()
        var keyInfo = KeyInfo()
        var padding: UInt16 = 0
        var result: UInt8 = 0
        var status: UInt8 = 0
        var data8: UInt8 = 0
        var data32: UInt32 = 0
        var bytes: (UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8,
                    UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8)
            = (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
    }

    private func call(_ input: inout KeyData) -> KeyData? {
        var output = KeyData()
        var size = MemoryLayout<KeyData>.stride
        let r = IOConnectCallStructMethod(smc, 2, &input, MemoryLayout<KeyData>.stride, &output, &size)
        return r == kIOReturnSuccess && output.result == 0 ? output : nil
    }

    private func readKey(_ key: String) -> (type: String, bytes: [UInt8])? {
        guard smc != 0 else { return nil }
        let code = key.utf8.reduce(UInt32(0)) { ($0 << 8) | UInt32($1) }
        var info = KeyData()
        info.key = code
        info.data8 = 9
        guard let meta = call(&info) else { return nil }
        var req = KeyData()
        req.key = code
        req.keyInfo.dataSize = meta.keyInfo.dataSize
        req.data8 = 5
        guard let out = call(&req) else { return nil }
        let t = meta.keyInfo.dataType
        let type = String(bytes: [UInt8(t >> 24), UInt8((t >> 16) & 0xff), UInt8((t >> 8) & 0xff), UInt8(t & 0xff)], encoding: .ascii) ?? ""
        let bytes = withUnsafeBytes(of: out.bytes) { Array($0.prefix(Int(meta.keyInfo.dataSize))) }
        return (type, bytes)
    }

    private func readNumber(_ key: String) -> Double? {
        guard let (type, b) = readKey(key) else { return nil }
        switch type {
        case "flt " where b.count == 4:
            return Double(Float(bitPattern: UInt32(b[0]) | UInt32(b[1]) << 8 | UInt32(b[2]) << 16 | UInt32(b[3]) << 24))
        case "fpe2" where b.count == 2:
            return Double((Int(b[0]) << 8) | Int(b[1])) / 4
        case "ui8 " where b.count >= 1:
            return Double(b[0])
        default:
            return nil
        }
    }

    private func readFans() -> [Fan] {
        let count = Int(readNumber("FNum") ?? 0)
        guard count > 0 else { return [] }
        return (0..<min(count, 4)).map { i in
            Fan(rpm: max(0, readNumber("F\(i)Ac") ?? 0),
                min: readNumber("F\(i)Mn") ?? 0,
                max: readNumber("F\(i)Mx") ?? 0)
        }
    }
}
