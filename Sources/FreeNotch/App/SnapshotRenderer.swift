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
        ]
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
