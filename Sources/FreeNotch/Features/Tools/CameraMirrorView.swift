import AVFoundation
import SwiftUI

struct CameraMirrorView: NSViewRepresentable {
    func makeNSView(context: Context) -> CameraPreviewNSView { CameraPreviewNSView() }
    func updateNSView(_ nsView: CameraPreviewNSView, context: Context) {}

    static func dismantleNSView(_ nsView: CameraPreviewNSView, coordinator: ()) {
        nsView.camera.stop()
    }
}

final class CameraPreviewNSView: NSView {
    let camera = CameraSession()
    private let preview: AVCaptureVideoPreviewLayer

    override init(frame frameRect: NSRect) {
        preview = AVCaptureVideoPreviewLayer(session: camera.session)
        super.init(frame: frameRect)
        wantsLayer = true
        layer = CALayer()
        preview.videoGravity = .resizeAspectFill
        layer?.addSublayer(preview)
        camera.start { [weak self] in
            guard let connection = self?.preview.connection, connection.isVideoMirroringSupported else { return }
            connection.automaticallyAdjustsVideoMirroring = false
            connection.isVideoMirrored = true
        }
    }

    required init?(coder: NSCoder) { fatalError() }

    override func layout() {
        super.layout()
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        preview.frame = bounds
        CATransaction.commit()
    }
}

final class CameraSession: @unchecked Sendable {
    let session = AVCaptureSession()
    private let queue = DispatchQueue(label: "island.camera")
    private var configured = false

    func start(onReady: @escaping @MainActor () -> Void) {
        AVCaptureDevice.requestAccess(for: .video) { [self] granted in
            guard granted else { return }
            queue.async { [self] in
                if !configured {
                    session.beginConfiguration()
                    session.sessionPreset = .medium
                    if let device = AVCaptureDevice.default(for: .video),
                       let input = try? AVCaptureDeviceInput(device: device),
                       session.canAddInput(input) {
                        session.addInput(input)
                    }
                    session.commitConfiguration()
                    configured = true
                }
                session.startRunning()
                DispatchQueue.main.async { MainActor.assumeIsolated { onReady() } }
            }
        }
    }

    func stop() {
        queue.async { [self] in
            if session.isRunning { session.stopRunning() }
        }
    }
}
