import Foundation
import IOBluetooth

@MainActor
final class BluetoothMonitor: NSObject {
    var onEvent: ((String, String, Bool) -> Void)?

    private var connectNotification: IOBluetoothUserNotification?
    private var startDate = Date()

    func start() {
        startDate = .now
        connectNotification = IOBluetoothDevice.register(
            forConnectNotifications: self, selector: #selector(deviceConnected(_:device:)))
    }

    @objc private func deviceConnected(_ note: IOBluetoothUserNotification, device: IOBluetoothDevice) {
        guard Self.isAudio(device) else { return }
        device.register(forDisconnectNotification: self, selector: #selector(deviceDisconnected(_:device:)))

        guard Date.now.timeIntervalSince(startDate) > 3 else { return }
        onEvent?(Self.shortName(device), Self.symbol(for: device), true)
    }

    @objc private func deviceDisconnected(_ note: IOBluetoothUserNotification, device: IOBluetoothDevice) {
        note.unregister()
        onEvent?(Self.shortName(device), Self.symbol(for: device), false)
    }

    private static func isAudio(_ device: IOBluetoothDevice) -> Bool {
        device.deviceClassMajor == kBluetoothDeviceClassMajorAudio
            || (device.name ?? "").localizedCaseInsensitiveContains("airpods")
    }

    private static func shortName(_ device: IOBluetoothDevice) -> String {
        let name = device.name ?? String(localized: "Headphones")
        if name.localizedCaseInsensitiveContains("airpods max") { return "AirPods Max" }
        if name.localizedCaseInsensitiveContains("airpods pro") { return "AirPods Pro" }
        if name.localizedCaseInsensitiveContains("airpods") { return "AirPods" }
        return name.count > 12 ? String(name.prefix(12)) + "…" : name
    }

    private static func symbol(for device: IOBluetoothDevice) -> String {
        let name = (device.name ?? "").lowercased()
        if name.contains("airpods max") { return "airpodsmax" }
        if name.contains("airpods pro") { return "airpodspro" }
        if name.contains("airpods") { return "airpods" }
        if name.contains("beats") { return "beats.headphones" }
        return "headphones"
    }
}
