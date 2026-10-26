import CoreAudio
import AudioToolbox
import Observation

@MainActor
@Observable
final class VolumeController {
    private(set) var volume: Float = 0
    private(set) var isMuted = false

    @ObservationIgnored var onChange: ((Float, Bool) -> Void)?

    @ObservationIgnored private var deviceID = AudioObjectID(kAudioObjectUnknown)
    @ObservationIgnored private var deviceListener: AudioObjectPropertyListenerBlock?
    @ObservationIgnored private var volumeListener: AudioObjectPropertyListenerBlock?
    @ObservationIgnored private var muteListener: AudioObjectPropertyListenerBlock?

    func start() {
        var addr = Self.address(kAudioHardwarePropertyDefaultOutputDevice, scope: kAudioObjectPropertyScopeGlobal)
        let block: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            MainActor.assumeIsolated { self?.attachToDefaultDevice() }
        }
        AudioObjectAddPropertyListenerBlock(AudioObjectID(kAudioObjectSystemObject), &addr, .main, block)
        deviceListener = block
        attachToDefaultDevice()
    }

    func setVolume(_ value: Float) {
        var v = min(max(value, 0), 1)
        var addr = Self.address(kAudioHardwareServiceDeviceProperty_VirtualMainVolume)
        AudioObjectSetPropertyData(deviceID, &addr, 0, nil, UInt32(MemoryLayout<Float32>.size), &v)
        if isMuted && v > 0 { setMuted(false) }
    }

    func setMuted(_ muted: Bool) {
        var m: UInt32 = muted ? 1 : 0
        var addr = Self.address(kAudioDevicePropertyMute)
        AudioObjectSetPropertyData(deviceID, &addr, 0, nil, UInt32(MemoryLayout<UInt32>.size), &m)
    }

    private func attachToDefaultDevice() {
        detachDeviceListeners()

        var id = AudioObjectID(kAudioObjectUnknown)
        var size = UInt32(MemoryLayout<AudioObjectID>.size)
        var addr = Self.address(kAudioHardwarePropertyDefaultOutputDevice, scope: kAudioObjectPropertyScopeGlobal)
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &addr, 0, nil, &size, &id) == noErr else { return }
        deviceID = id

        let vBlock: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
            MainActor.assumeIsolated { self?.read(notify: true) }
        }
        var vAddr = Self.address(kAudioHardwareServiceDeviceProperty_VirtualMainVolume)
        AudioObjectAddPropertyListenerBlock(id, &vAddr, .main, vBlock)
        volumeListener = vBlock

        var mAddr = Self.address(kAudioDevicePropertyMute)
        if AudioObjectHasProperty(id, &mAddr) {
            let mBlock: AudioObjectPropertyListenerBlock = { [weak self] _, _ in
                MainActor.assumeIsolated { self?.read(notify: true) }
            }
            AudioObjectAddPropertyListenerBlock(id, &mAddr, .main, mBlock)
            muteListener = mBlock
        }
        read(notify: false)
    }

    private func detachDeviceListeners() {
        guard deviceID != kAudioObjectUnknown else { return }
        if let volumeListener {
            var a = Self.address(kAudioHardwareServiceDeviceProperty_VirtualMainVolume)
            AudioObjectRemovePropertyListenerBlock(deviceID, &a, .main, volumeListener)
        }
        if let muteListener {
            var a = Self.address(kAudioDevicePropertyMute)
            AudioObjectRemovePropertyListenerBlock(deviceID, &a, .main, muteListener)
        }
        volumeListener = nil
        muteListener = nil
    }

    private func read(notify: Bool) {
        var v: Float32 = 0
        var size = UInt32(MemoryLayout<Float32>.size)
        var addr = Self.address(kAudioHardwareServiceDeviceProperty_VirtualMainVolume)
        if AudioObjectGetPropertyData(deviceID, &addr, 0, nil, &size, &v) == noErr { volume = v }

        var m: UInt32 = 0
        size = UInt32(MemoryLayout<UInt32>.size)
        var mAddr = Self.address(kAudioDevicePropertyMute)
        if AudioObjectGetPropertyData(deviceID, &mAddr, 0, nil, &size, &m) == noErr { isMuted = m != 0 }

        if notify { onChange?(volume, isMuted) }
    }

    private static func address(_ selector: AudioObjectPropertySelector,
                                scope: AudioObjectPropertyScope = kAudioDevicePropertyScopeOutput) -> AudioObjectPropertyAddress {
        AudioObjectPropertyAddress(mSelector: selector, mScope: scope, mElement: kAudioObjectPropertyElementMain)
    }
}
