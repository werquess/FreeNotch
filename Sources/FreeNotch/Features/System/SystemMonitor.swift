import Darwin
import Foundation
import IOKit.ps
import Observation

@MainActor
@Observable
final class SystemMonitor {
    private(set) var batteryLevel = 100
    private(set) var isCharging = false
    private(set) var isPluggedIn = false
    private(set) var minutesRemaining: Int?
    private(set) var hasBattery = true

    private(set) var cpuUsage: Double = 0
    private(set) var memoryUsed: UInt64 = 0
    let memoryTotal = ProcessInfo.processInfo.physicalMemory
    private(set) var thermalState = ProcessInfo.processInfo.thermalState
    private(set) var cpuHistory: [Double] = []
    private(set) var cpuTemperature: Double?
    private(set) var batteryTemperature: Double?
    private(set) var fans: [SensorReader.Fan] = []

    @ObservationIgnored var onPowerSourceChange: ((Bool, Int) -> Void)?

    @ObservationIgnored private var runLoopSource: CFRunLoopSource?
    @ObservationIgnored private var sampler: DispatchSourceTimer?
    @ObservationIgnored private var samplingClients = 0
    @ObservationIgnored private var lastTicks: (used: UInt64, total: UInt64)?
    @ObservationIgnored private var thermalObserver: NSObjectProtocol?
    @ObservationIgnored private var didReadOnce = false
    @ObservationIgnored private lazy var sensors = SensorReader()

    func start() {
        readPower()
        let context = Unmanaged.passUnretained(self).toOpaque()
        if let source = IOPSNotificationCreateRunLoopSource({ ctx in
            guard let ctx else { return }
            let monitor = Unmanaged<SystemMonitor>.fromOpaque(ctx).takeUnretainedValue()
            MainActor.assumeIsolated { monitor.readPower() }
        }, context)?.takeRetainedValue() {
            CFRunLoopAddSource(CFRunLoopGetMain(), source, .defaultMode)
            runLoopSource = source
        }

        thermalObserver = NotificationCenter.default.addObserver(
            forName: ProcessInfo.thermalStateDidChangeNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.thermalState = ProcessInfo.processInfo.thermalState }
        }
    }

    func stop() {
        if let runLoopSource { CFRunLoopRemoveSource(CFRunLoopGetMain(), runLoopSource, .defaultMode) }
        runLoopSource = nil
        sampler?.cancel()
        sampler = nil
    }

    private func readPower() {
        guard let blob = IOPSCopyPowerSourcesInfo()?.takeRetainedValue(),
              let list = IOPSCopyPowerSourcesList(blob)?.takeRetainedValue() as? [CFTypeRef] else { return }

        var found = false
        for ps in list {
            guard let desc = IOPSGetPowerSourceDescription(blob, ps)?.takeUnretainedValue() as? [String: Any],
                  (desc[kIOPSTypeKey] as? String) == kIOPSInternalBatteryType else { continue }
            found = true
            let current = desc[kIOPSCurrentCapacityKey] as? Int ?? 0
            let max = desc[kIOPSMaxCapacityKey] as? Int ?? 100
            let level = max > 0 ? Int((Double(current) / Double(max) * 100).rounded()) : 0
            let plugged = (desc[kIOPSPowerSourceStateKey] as? String) == kIOPSACPowerValue
            let charging = desc[kIOPSIsChargingKey] as? Bool ?? false
            let tte = desc[kIOPSTimeToEmptyKey] as? Int

            let pluggedChanged = didReadOnce && plugged != isPluggedIn
            batteryLevel = level
            isCharging = charging
            isPluggedIn = plugged
            minutesRemaining = (tte ?? -1) > 0 ? tte : nil
            if pluggedChanged { onPowerSourceChange?(plugged, level) }
        }
        hasBattery = found
        didReadOnce = true
    }

    func beginSampling() {
        samplingClients += 1
        guard sampler == nil else { return }
        sample()
        let t = DispatchSource.makeTimerSource(queue: .main)
        t.schedule(deadline: .now() + 2, repeating: .seconds(2), leeway: .milliseconds(500))
        t.setEventHandler { [weak self] in
            MainActor.assumeIsolated { self?.sample() }
        }
        t.resume()
        sampler = t
    }

    func endSampling() {
        samplingClients = max(0, samplingClients - 1)
        guard samplingClients == 0 else { return }
        sampler?.cancel()
        sampler = nil
        lastTicks = nil
    }

    private func sample() {
        if let ticks = Self.cpuTicks() {
            if let last = lastTicks, ticks.total > last.total {
                cpuUsage = Double(ticks.used - last.used) / Double(ticks.total - last.total)
                cpuHistory.append(cpuUsage)
                if cpuHistory.count > 30 { cpuHistory.removeFirst() }
            }
            lastTicks = ticks
        }
        memoryUsed = Self.usedMemory()
        let r = sensors.read()
        cpuTemperature = r.cpuTemperature
        batteryTemperature = r.batteryTemperature
        fans = r.fans
    }

    private nonisolated static func cpuTicks() -> (used: UInt64, total: UInt64)? {
        var info: processor_info_array_t?
        var infoCount: mach_msg_type_number_t = 0
        var cpuCount: natural_t = 0
        guard host_processor_info(mach_host_self(), PROCESSOR_CPU_LOAD_INFO, &cpuCount, &info, &infoCount) == KERN_SUCCESS,
              let info else { return nil }
        defer {
            vm_deallocate(mach_task_self_, vm_address_t(bitPattern: info), vm_size_t(Int(infoCount) * MemoryLayout<integer_t>.stride))
        }
        var used: UInt64 = 0, total: UInt64 = 0
        for i in 0..<Int(cpuCount) {
            let base = Int(CPU_STATE_MAX) * i
            let user = UInt64(UInt32(bitPattern: info[base + Int(CPU_STATE_USER)]))
            let system = UInt64(UInt32(bitPattern: info[base + Int(CPU_STATE_SYSTEM)]))
            let nice = UInt64(UInt32(bitPattern: info[base + Int(CPU_STATE_NICE)]))
            let idle = UInt64(UInt32(bitPattern: info[base + Int(CPU_STATE_IDLE)]))
            used += user + system + nice
            total += user + system + nice + idle
        }
        return (used, total)
    }

    private nonisolated static func usedMemory() -> UInt64 {
        var stats = vm_statistics64()
        var count = mach_msg_type_number_t(MemoryLayout<vm_statistics64_data_t>.size / MemoryLayout<integer_t>.size)
        let result = withUnsafeMutablePointer(to: &stats) {
            $0.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics64(mach_host_self(), HOST_VM_INFO64, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return 0 }
        let page = UInt64(vm_kernel_page_size)

        let app = UInt64(stats.internal_page_count) - UInt64(stats.purgeable_count)
        return (app + UInt64(stats.wire_count) + UInt64(stats.compressor_page_count)) * page
    }
}
