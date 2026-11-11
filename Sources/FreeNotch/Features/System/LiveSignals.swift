import AppKit
import CoreAudio
import CoreMediaIO
import Network
import Observation

@MainActor
@Observable
final class LiveSignals {
    private(set) var micActive = false
    private(set) var cameraActive = false
    private(set) var capsLock = false
    private(set) var offline = false
    private(set) var screenshot: NSImage?

    @ObservationIgnored var onScreenshot: ((URL) -> Void)?
    @ObservationIgnored var onDownload: ((URL) -> Void)?
    @ObservationIgnored var onOutputChange: ((String, String) -> Void)?
    @ObservationIgnored var onReconnect: (() -> Void)?
    @ObservationIgnored var onOffline: (() -> Void)?
    @ObservationIgnored var onCaptureStart: ((Bool, Bool) -> Void)?

    @ObservationIgnored private var inputDevice: AudioObjectID = 0
    @ObservationIgnored private var inputListener: AudioObjectPropertyListenerBlock?
    @ObservationIgnored private var defaultInputListener: AudioObjectPropertyListenerBlock?
    @ObservationIgnored private var defaultOutputListener: AudioObjectPropertyListenerBlock?
    @ObservationIgnored private var lastOutput: AudioObjectID = 0
    @ObservationIgnored private var cameras: [CMIOObjectID] = []
    @ObservationIgnored private var cameraListener: CMIOObjectPropertyListenerBlock?
    @ObservationIgnored private var devicesListener: CMIOObjectPropertyListenerBlock?
    @ObservationIgnored private var pathMonitor: NWPathMonitor?
    @ObservationIgnored private var sawFirstPath = false
    @ObservationIgnored private var offlineTask: Task<Void, Never>?
    @ObservationIgnored private var announcedOffline = false
    @ObservationIgnored private var screenshotObservers: [NSObjectProtocol] = []
    @ObservationIgnored private var flagsMonitors: [Any] = []
    @ObservationIgnored private var capsPoll: DispatchSourceTimer?
    @ObservationIgnored private var screenshotQuery: NSMetadataQuery?
    @ObservationIgnored private var seenScreenshots: Set<String> = []
    @ObservationIgnored private var downloadsSource: DispatchSourceFileSystemObject?
    @ObservationIgnored private var downloadsSeen: Set<String> = []
    @ObservationIgnored private var downloadsDebounce: DispatchWorkItem?
    @ObservationIgnored private var started = Date.now

    private static let partialExtensions: Set<String> = ["crdownload", "download", "part", "partial", "tmp", "opdownload"]

    func start() {
        started = .now
        startCapture()
        startOutput()
        startNetwork()
        startCapsLock()
        startScreenshots()
        startDownloads()
    }

    func reloadPreferences() {
        if Prefs.bool(.activityScreenshot) { startScreenshots() } else { stopScreenshots() }
        if Prefs.bool(.activityDownload) { startDownloads() } else { stopDownloads() }
    }

    private func startCapture() {
        var defaultInput = AudioObjectPropertyAddress(mSelector: kAudioHardwarePropertyDefaultInputDevice,
                                                      mScope: kAudioObjectPropertyScopeGlobal,
                                                      mElement: kAudioObjectPropertyElementMain)
        let onDefault: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            DispatchQueue.main.async { MainActor.assumeIsolated { self?.watchInputDevice() } }
        }
        defaultInputListener = onDefault
        AudioObjectAddPropertyListenerBlock(AudioObjectID(kAudioObjectSystemObject), &defaultInput, .main, onDefault)
        watchInputDevice()

        var devices = CMIOObjectPropertyAddress(mSelector: CMIOObjectPropertySelector(kCMIOHardwarePropertyDevices),
                                                mScope: CMIOObjectPropertyScope(kCMIOObjectPropertyScopeGlobal),
                                                mElement: CMIOObjectPropertyElement(kCMIOObjectPropertyElementMain))
        let onDevices: CMIOObjectPropertyListenerBlock = { [weak self] _, _ in
            DispatchQueue.main.async { MainActor.assumeIsolated { self?.watchCameras() } }
        }
        devicesListener = onDevices
        CMIOObjectAddPropertyListenerBlock(CMIOObjectID(kCMIOObjectSystemObject), &devices, .main, onDevices)
        watchCameras()
    }

    private func watchInputDevice() {
        var address = runningAddress
        if inputDevice != 0, let inputListener {
            AudioObjectRemovePropertyListenerBlock(inputDevice, &address, .main, inputListener)
        }
        inputDevice = Self.defaultDevice(kAudioHardwarePropertyDefaultInputDevice)
        guard inputDevice != 0 else { micActive = false; return }
        let listener: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            DispatchQueue.main.async { MainActor.assumeIsolated { self?.readMic() } }
        }
        inputListener = listener
        AudioObjectAddPropertyListenerBlock(inputDevice, &address, .main, listener)
        readMic()
    }

    private var runningAddress: AudioObjectPropertyAddress {
        AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyDeviceIsRunningSomewhere,
                                   mScope: kAudioObjectPropertyScopeGlobal,
                                   mElement: kAudioObjectPropertyElementMain)
    }

    private func readMic() {
        var address = runningAddress
        var running: UInt32 = 0
        var size = UInt32(MemoryLayout<UInt32>.size)
        let status = AudioObjectGetPropertyData(inputDevice, &address, 0, nil, &size, &running)
        let active = status == noErr && running != 0
        guard active != micActive else { return }
        micActive = active
        if active { onCaptureStart?(micActive, cameraActive) }
    }

    private func watchCameras() {
        var address = cameraRunningAddress
        if let cameraListener {
            for id in cameras { CMIOObjectRemovePropertyListenerBlock(id, &address, .main, cameraListener) }
        }
        cameras = Self.cameraDevices()
        let listener: CMIOObjectPropertyListenerBlock = { [weak self] _, _ in
            DispatchQueue.main.async { MainActor.assumeIsolated { self?.readCameras() } }
        }
        cameraListener = listener
        for id in cameras { CMIOObjectAddPropertyListenerBlock(id, &address, .main, listener) }
        readCameras()
    }

    private var cameraRunningAddress: CMIOObjectPropertyAddress {
        CMIOObjectPropertyAddress(mSelector: CMIOObjectPropertySelector(kCMIODevicePropertyDeviceIsRunningSomewhere),
                                  mScope: CMIOObjectPropertyScope(kCMIOObjectPropertyScopeWildcard),
                                  mElement: CMIOObjectPropertyElement(kCMIOObjectPropertyElementWildcard))
    }

    private func readCameras() {
        var address = cameraRunningAddress
        let active = cameras.contains { id in
            var running: UInt32 = 0
            var used: UInt32 = 0
            let status = CMIOObjectGetPropertyData(id, &address, 0, nil, UInt32(MemoryLayout<UInt32>.size), &used, &running)
            return status == 0 && running != 0
        }
        guard active != cameraActive else { return }
        cameraActive = active
        if active { onCaptureStart?(micActive, cameraActive) }
    }

    private static func cameraDevices() -> [CMIOObjectID] {
        var address = CMIOObjectPropertyAddress(mSelector: CMIOObjectPropertySelector(kCMIOHardwarePropertyDevices),
                                                mScope: CMIOObjectPropertyScope(kCMIOObjectPropertyScopeGlobal),
                                                mElement: CMIOObjectPropertyElement(kCMIOObjectPropertyElementMain))
        var size: UInt32 = 0
        guard CMIOObjectGetPropertyDataSize(CMIOObjectID(kCMIOObjectSystemObject), &address, 0, nil, &size) == 0, size > 0 else { return [] }
        var ids = [CMIOObjectID](repeating: 0, count: Int(size) / MemoryLayout<CMIOObjectID>.size)
        var used: UInt32 = 0
        guard CMIOObjectGetPropertyData(CMIOObjectID(kCMIOObjectSystemObject), &address, 0, nil, size, &used, &ids) == 0 else { return [] }
        return ids
    }

    private static func defaultDevice(_ selector: AudioObjectPropertySelector) -> AudioObjectID {
        var address = AudioObjectPropertyAddress(mSelector: selector,
                                                 mScope: kAudioObjectPropertyScopeGlobal,
                                                 mElement: kAudioObjectPropertyElementMain)
        var device = AudioObjectID(0)
        var size = UInt32(MemoryLayout<AudioObjectID>.size)
        AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &size, &device)
        return device
    }

    private func startOutput() {
        lastOutput = Self.defaultDevice(kAudioHardwarePropertyDefaultOutputDevice)
        var address = AudioObjectPropertyAddress(mSelector: kAudioHardwarePropertyDefaultOutputDevice,
                                                 mScope: kAudioObjectPropertyScopeGlobal,
                                                 mElement: kAudioObjectPropertyElementMain)
        let listener: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            DispatchQueue.main.async { MainActor.assumeIsolated { self?.outputChanged() } }
        }
        defaultOutputListener = listener
        AudioObjectAddPropertyListenerBlock(AudioObjectID(kAudioObjectSystemObject), &address, .main, listener)
    }

    private func outputChanged() {
        let device = Self.defaultDevice(kAudioHardwarePropertyDefaultOutputDevice)
        guard device != 0, device != lastOutput else { return }
        lastOutput = device
        onOutputChange?(Self.name(of: device), Self.symbol(of: device))
    }

    private static func name(of device: AudioObjectID) -> String {
        var address = AudioObjectPropertyAddress(mSelector: kAudioObjectPropertyName,
                                                 mScope: kAudioObjectPropertyScopeGlobal,
                                                 mElement: kAudioObjectPropertyElementMain)
        var name: Unmanaged<CFString>?
        var size = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
        guard AudioObjectGetPropertyData(device, &address, 0, nil, &size, &name) == noErr, let value = name?.takeRetainedValue() else {
            return String(localized: "Speakers")
        }
        return value as String
    }

    private static func symbol(of device: AudioObjectID) -> String {
        var address = AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyTransportType,
                                                 mScope: kAudioObjectPropertyScopeGlobal,
                                                 mElement: kAudioObjectPropertyElementMain)
        var transport: UInt32 = 0
        var size = UInt32(MemoryLayout<UInt32>.size)
        AudioObjectGetPropertyData(device, &address, 0, nil, &size, &transport)
        let lower = name(of: device).lowercased()
        switch transport {
        case kAudioDeviceTransportTypeBluetooth, kAudioDeviceTransportTypeBluetoothLE:
            if lower.contains("airpods max") { return "airpodsmax" }
            if lower.contains("airpods pro") { return "airpodspro" }
            if lower.contains("airpods") { return "airpods" }
            return "headphones"
        case kAudioDeviceTransportTypeHDMI, kAudioDeviceTransportTypeDisplayPort:
            return "tv"
        case kAudioDeviceTransportTypeAirPlay:
            return "airplayaudio"
        case kAudioDeviceTransportTypeUSB:
            return "hifispeaker.fill"
        case kAudioDeviceTransportTypeBuiltIn:
            return lower.contains("headphone") || lower.contains("kulaklık") ? "headphones" : "laptopcomputer"
        default:
            return "speaker.wave.2.fill"
        }
    }

    private func startNetwork() {
        let monitor = NWPathMonitor()
        monitor.pathUpdateHandler = { [weak self] path in
            let down = path.status != .satisfied
            DispatchQueue.main.async {
                MainActor.assumeIsolated {
                    guard let self else { return }
                    let wasOffline = self.offline
                    self.offline = down
                    if self.sawFirstPath, wasOffline, !down { self.networkRestored() }
                    if self.sawFirstPath, !wasOffline, down { self.networkLost() }
                    self.sawFirstPath = true
                }
            }
        }
        monitor.start(queue: DispatchQueue(label: "island.network", qos: .utility))
        pathMonitor = monitor
    }

    private func networkLost() {
        offlineTask?.cancel()
        offlineTask = Task { [weak self] in
            try? await Task.sleep(for: .seconds(3))
            guard !Task.isCancelled, let self, self.offline else { return }
            self.announcedOffline = true
            self.onOffline?()
        }
    }

    private func networkRestored() {
        offlineTask?.cancel()
        offlineTask = nil
        guard announcedOffline else { return }
        announcedOffline = false
        onReconnect?()
    }

    private func startCapsLock() {
        capsLock = NSEvent.modifierFlags.contains(.capsLock)
        let update: (NSEvent) -> Void = { [weak self] event in
            let on = event.modifierFlags.contains(.capsLock)
            MainActor.assumeIsolated { if self?.capsLock != on { self?.capsLock = on } }
        }
        if let global = NSEvent.addGlobalMonitorForEvents(matching: .flagsChanged, handler: update) { flagsMonitors.append(global) }
        if let local = NSEvent.addLocalMonitorForEvents(matching: .flagsChanged, handler: { update($0); return $0 }) { flagsMonitors.append(local) }
        let poll = DispatchSource.makeTimerSource(queue: .main)
        poll.schedule(deadline: .now() + 2, repeating: 2, leeway: .milliseconds(800))
        poll.setEventHandler { [weak self] in
            MainActor.assumeIsolated {
                guard let self, !AXIsProcessTrusted() else { return }
                let on = CGEventSource.flagsState(.combinedSessionState).contains(.maskAlphaShift)
                if self.capsLock != on { self.capsLock = on }
            }
        }
        poll.resume()
        capsPoll = poll
    }

    private func startScreenshots() {
        guard Prefs.bool(.activityScreenshot), screenshotQuery == nil else { return }
        let query = NSMetadataQuery()
        query.predicate = NSPredicate(format: "kMDItemIsScreenCapture == 1 AND kMDItemFSCreationDate > %@", started as NSDate)
        query.searchScopes = [NSMetadataQueryUserHomeScope]
        screenshotObservers = [
            NotificationCenter.default.addObserver(forName: .NSMetadataQueryDidUpdate, object: query, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated { self?.screenshotsUpdated() }
            },
            NotificationCenter.default.addObserver(forName: .NSMetadataQueryDidFinishGathering, object: query, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated { self?.screenshotsUpdated(initial: true) }
            },
        ]
        query.start()
        screenshotQuery = query
    }

    private func stopScreenshots() {
        screenshotQuery?.stop()
        screenshotQuery = nil
        screenshotObservers.forEach(NotificationCenter.default.removeObserver)
        screenshotObservers = []
    }

    private func screenshotsUpdated(initial: Bool = false) {
        guard let query = screenshotQuery else { return }
        query.disableUpdates()
        defer { query.enableUpdates() }
        var fresh: URL?
        for case let item as NSMetadataItem in query.results {
            guard let path = item.value(forAttribute: NSMetadataItemPathKey) as? String, !seenScreenshots.contains(path) else { continue }
            seenScreenshots.insert(path)
            if !initial { fresh = URL(fileURLWithPath: path) }
        }
        guard let fresh else { return }
        screenshot = Self.thumbnail(fresh)
        onScreenshot?(fresh)
    }

    private static func thumbnail(_ url: URL) -> NSImage? {
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil) else { return nil }
        let options: [CFString: Any] = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceThumbnailMaxPixelSize: 120,
        ]
        guard let image = CGImageSourceCreateThumbnailAtIndex(source, 0, options as CFDictionary) else { return nil }
        return NSImage(cgImage: image, size: .zero)
    }

    private var downloadsURL: URL {
        FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSHomeDirectory()).appendingPathComponent("Downloads")
    }

    private func startDownloads() {
        guard Prefs.bool(.activityDownload), downloadsSource == nil else { return }
        let fd = open(downloadsURL.path, O_EVTONLY)
        guard fd >= 0 else { return }
        downloadsSeen = Set(listDownloads().map(\.path))
        let source = DispatchSource.makeFileSystemObjectSource(fileDescriptor: fd, eventMask: .write, queue: .main)
        source.setEventHandler { [weak self] in
            MainActor.assumeIsolated { self?.scheduleDownloadCheck() }
        }
        source.setCancelHandler { close(fd) }
        source.resume()
        downloadsSource = source
    }

    private func stopDownloads() {
        downloadsSource?.cancel()
        downloadsSource = nil
    }

    private func scheduleDownloadCheck() {
        downloadsDebounce?.cancel()
        let work = DispatchWorkItem { [weak self] in
            MainActor.assumeIsolated { self?.checkDownloads() }
        }
        downloadsDebounce = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8, execute: work)
    }

    private func listDownloads() -> [URL] {
        (try? FileManager.default.contentsOfDirectory(at: downloadsURL,
                                                       includingPropertiesForKeys: [.creationDateKey],
                                                       options: [.skipsHiddenFiles])) ?? []
    }

    private func checkDownloads() {
        let current = listDownloads()
        let paths = Set(current.map(\.path))
        let added = current.filter { !downloadsSeen.contains($0.path) && !Self.partialExtensions.contains($0.pathExtension.lowercased()) }
        downloadsSeen = paths
        guard let newest = added.max(by: { a, b in
            let da = (try? a.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast
            let db = (try? b.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast
            return da < db
        }) else { return }
        onDownload?(newest)
    }
}
