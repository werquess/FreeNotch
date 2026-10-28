import CoreAudio

@MainActor
final class AudioActivityMonitor {
    private(set) var isActive = false
    var onChange: ((Bool) -> Void)?

    private var deviceID = AudioObjectID(kAudioObjectUnknown)
    private var deviceListener: AudioObjectPropertyListenerBlock?
    private var runningListener: AudioObjectPropertyListenerBlock?

    func start() {
        guard deviceListener == nil else { return }
        var addr = Self.address(kAudioHardwarePropertyDefaultOutputDevice)
        let block: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            MainActor.assumeIsolated { self?.attachToDefaultDevice() }
        }
        AudioObjectAddPropertyListenerBlock(AudioObjectID(kAudioObjectSystemObject), &addr, .main, block)
        deviceListener = block
        attachToDefaultDevice()
    }

    private func attachToDefaultDevice() {
        detach()
        var id = AudioObjectID(kAudioObjectUnknown)
        var size = UInt32(MemoryLayout<AudioObjectID>.size)
        var addr = Self.address(kAudioHardwarePropertyDefaultOutputDevice)
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &addr, 0, nil, &size, &id) == noErr else { return }
        deviceID = id

        let block: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            MainActor.assumeIsolated { self?.read() }
        }
        var rAddr = Self.address(kAudioDevicePropertyDeviceIsRunningSomewhere)
        AudioObjectAddPropertyListenerBlock(id, &rAddr, .main, block)
        runningListener = block
        read()
    }

    private func detach() {
        guard deviceID != kAudioObjectUnknown, let runningListener else { return }
        var addr = Self.address(kAudioDevicePropertyDeviceIsRunningSomewhere)
        AudioObjectRemovePropertyListenerBlock(deviceID, &addr, .main, runningListener)
        self.runningListener = nil
    }

    private func read() {
        var running: UInt32 = 0
        var size = UInt32(MemoryLayout<UInt32>.size)
        var addr = Self.address(kAudioDevicePropertyDeviceIsRunningSomewhere)
        let active = AudioObjectGetPropertyData(deviceID, &addr, 0, nil, &size, &running) == noErr && running != 0
        guard active != isActive else { return }
        isActive = active
        onChange?(active)
    }

    private static func address(_ selector: AudioObjectPropertySelector) -> AudioObjectPropertyAddress {
        AudioObjectPropertyAddress(mSelector: selector, mScope: kAudioObjectPropertyScopeGlobal,
                                   mElement: kAudioObjectPropertyElementMain)
    }
}
