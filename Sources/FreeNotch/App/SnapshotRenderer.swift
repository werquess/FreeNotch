#if DEBUG
import AppKit
import SwiftUI

@MainActor
enum SnapshotRenderer {
    static func runIfRequested() -> Bool {
        let args = CommandLine.arguments

        if let b = args.firstIndex(of: "--browser-script"), b + 1 < args.count, let app = BrowserApp(rawValue: args[b + 1]) {
            print(BrowserScripts.scan(app, allTabs: false))
            return true
        }
        guard let i = args.firstIndex(of: "--snapshot") else { return false }
        let dir = URL(fileURLWithPath: i + 1 < args.count ? args[i + 1] : "snapshots")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        Prefs.registerDefaults()

        let env = AppEnvironment()
        let vm = env.island
        var cases: [(String, () -> Void)] = [
            ("01-idle", { vm.debugForce(mode: .compact) }),
            ("02-hud-volume", { vm.debugForce(mode: .compact, hud: HUDEvent(kind: .volume, level: 0.6)) }),
            ("03-hud-brightness", { vm.debugForce(mode: .compact, hud: HUDEvent(kind: .brightness, level: 0.3)) }),
            ("04-charging", { vm.debugForce(mode: .compact, transient: .charging(level: 76, pluggedIn: true)) }),
            ("05-airpods", { vm.debugForce(mode: .compact, transient: .bluetooth(name: "AirPods Pro", symbol: "airpodspro", connected: true)) }),
            ("06-timer-done", { vm.debugForce(mode: .compact, transient: .timerDone("Focus done")) }),
            ("07-peek", { vm.debugForce(mode: .compact, transient: .trackPeek) }),
            ("08-hover", { vm.debugForce(mode: .compact, hovering: true) }),
            ("09-dropzone", { vm.debugForce(mode: .compact, dragging: true) }),
            ("09b-shelf-added", { vm.debugForce(mode: .compact, transient: .shelfAdded(3)) }),
            ("20-capture-mic", { vm.debugForce(mode: .compact, transient: .capture(mic: true, camera: false)) }),
            ("21-capture-both", { vm.debugForce(mode: .compact, transient: .capture(mic: true, camera: true)) }),
            ("22-meeting", { vm.debugForce(mode: .compact, transient: .meeting(title: "Design review", start: .now.addingTimeInterval(240))) }),
            ("23-capslock", { vm.debugForce(mode: .compact, transient: .capsLock) }),
            ("24-keepawake", { vm.debugForce(mode: .compact, transient: .keepAwake) }),
            ("25-offline", { vm.debugForce(mode: .compact, transient: .offline) }),
            ("26-online", { vm.debugForce(mode: .compact, transient: .online) }),
            ("27-thermal", { vm.debugForce(mode: .compact, transient: .thermal) }),
            ("28-screenshot", { vm.debugForce(mode: .compact, transient: .screenshot("/tmp/x.png")) }),
            ("29-download", { vm.debugForce(mode: .compact, transient: .download("FreeNotch-1.1.0-final-surum-arm64.dmg")) }),
            ("30-output", { vm.debugForce(mode: .compact, transient: .audioOutput(name: "Oğuz’s AirPods Pro – Find My", symbol: "airpodspro")) }),
            ("32-bt-long", { vm.debugForce(mode: .compact, transient: .bluetooth(name: "Sony WH-1000XM5 Kablosuz Kulaklık", symbol: "headphones", connected: true)) }),
            ("31-battery-low", { vm.debugForce(mode: .compact, transient: .batteryLow(10)) }),
        ]
        if let art = ProcessInfo.processInfo.environment["SNAPSHOT_ART"].flatMap(NSImage.init(contentsOfFile:)) {
            var s = MediaSession(id: "com.spotify.client", kind: .spotify, trackID: "t", title: "About A Girl", artist: "Nirvana",
                                 album: "Bleach", duration: 168, isPlaying: true, anchorPosition: 69, anchorDate: .now)
            s.artwork = art
            s.accent = NSColor(srgbRed: 0.55, green: 0.5, blue: 0.45, alpha: 1)
            env.media.debugInject(s)
        }
        for tab in IslandTab.allCases {
            cases.append(("10-expanded-\(tab.rawValue)", { vm.debugForce(mode: .expanded, tab: tab) }))
        }

        for (name, apply) in cases {
            apply()
            let view = IslandScene(env: env)
                .frame(width: vm.canvasSize.width, height: vm.canvasSize.height)
                .background(Color(white: 0.35))
            let renderer = ImageRenderer(content: view)
            renderer.scale = 2
            guard let cg = renderer.cgImage else { continue }
            let rep = NSBitmapImageRep(cgImage: cg)
            try? rep.representation(using: .png, properties: [:])?.write(to: dir.appendingPathComponent("\(name).png"))
            print("📸 \(name)")
        }
        return true
    }
}
#endif
