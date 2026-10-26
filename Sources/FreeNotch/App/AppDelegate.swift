import AppKit
import SwiftUI

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate, NSMenuDelegate {
    private var env: AppEnvironment!
    private var windowController: IslandWindowController!
    private var statusItem: NSStatusItem?
    private var caffeineItem: NSStatusItem?
    private var settingsWindow: NSWindow?
    private var updateMenuItem: NSMenuItem?
    private let router = SettingsRouter()

    func applicationDidFinishLaunching(_ notification: Notification) {
        Prefs.registerDefaults()
        env = AppEnvironment()
        env.island.openSettings = { [weak self] in self?.openSettings() }
        windowController = IslandWindowController(env: env)
        env.updater.onUpdateFound = { [weak self] release in
            self?.env.island.showTransient(.updateAvailable(release.version), duration: 4)
        }
        env.start()
        setupStatusItem()
        env.caffeine.onChange = { [weak self] active in self?.updateCaffeineItem(active) }
        updateCaffeineItem(env.caffeine.isActive)
    }

    func applicationWillTerminate(_ notification: Notification) {
        env.stop()
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        openSettings()
        return true
    }

    private func setupStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        item.button?.image = NSImage(systemSymbolName: "capsule.fill", accessibilityDescription: "FreeNotch")
        item.button?.image?.isTemplate = true

        let menu = NSMenu()
        menu.delegate = self
        let toggle = NSMenuItem(title: String(localized: "Open or Close Notch"), action: #selector(toggleIsland), keyEquivalent: "")
        let settings = NSMenuItem(title: String(localized: "Settings…"), action: #selector(openSettingsAction), keyEquivalent: ",")
        let update = NSMenuItem(title: String(localized: "Check for Updates…"), action: #selector(checkForUpdates), keyEquivalent: "")
        let quit = NSMenuItem(title: String(localized: "Quit FreeNotch"), action: #selector(quit), keyEquivalent: "q")
        for i in [toggle, settings, update] {
            i.target = self
            menu.addItem(i)
        }
        menu.addItem(.separator())
        quit.target = self
        menu.addItem(quit)
        item.menu = menu
        statusItem = item
        updateMenuItem = update
    }

    func menuWillOpen(_ menu: NSMenu) {
        if let release = env.updater.availableRelease {
            updateMenuItem?.title = String(localized: "Update to \(release.version)…")
        } else {
            updateMenuItem?.title = String(localized: "Check for Updates…")
        }
    }

    private func updateCaffeineItem(_ active: Bool) {
        guard active else {
            if let caffeineItem { NSStatusBar.system.removeStatusItem(caffeineItem) }
            caffeineItem = nil
            return
        }
        guard caffeineItem == nil else { return }
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        let image = NSImage(systemSymbolName: "cup.and.saucer.fill", accessibilityDescription: String(localized: "Keep Awake is on"))
        image?.isTemplate = true
        item.button?.image = image
        item.button?.toolTip = String(localized: "Keep Awake is on. Your Mac won't go to sleep.")
        let menu = NSMenu()
        let header = NSMenuItem(title: String(localized: "Keep Awake is on"), action: nil, keyEquivalent: "")
        header.isEnabled = false
        menu.addItem(header)
        let off = NSMenuItem(title: String(localized: "Turn Off"), action: #selector(turnOffCaffeine), keyEquivalent: "")
        off.target = self
        menu.addItem(off)
        item.menu = menu
        caffeineItem = item
    }

    @objc private func turnOffCaffeine() {
        env.caffeine.deactivate()
    }

    @objc private func toggleIsland() {
        env.island.toggle()
    }

    @objc private func openSettingsAction() {
        openSettings()
    }

    @objc private func checkForUpdates() {
        openSettings(pane: .updates)
        Task { await env.updater.check(userInitiated: true) }
    }

    func openSettings(pane: SettingsPane? = nil) {
        env.island.collapse()
        if let pane { router.pane = pane }
        if settingsWindow == nil {
            let root = SettingsRootView()
                .environment(router)
                .environment(env.island)
                .environment(env.clipboard)
                .environment(env.notes)
                .environment(env.media)
                .environment(env.caffeine)
                .environment(env.appearance)
                .environment(env.updater)
            let host = NSHostingController(rootView: root)
            let window = NSWindow(contentViewController: host)
            window.title = String(localized: "FreeNotch Settings")
            window.styleMask = [.titled, .closable, .miniaturizable, .fullSizeContentView]
            window.titlebarAppearsTransparent = true
            window.toolbarStyle = .unified
            window.isReleasedWhenClosed = false
            window.setContentSize(NSSize(width: 780, height: 580))
            window.delegate = self
            window.center()
            settingsWindow = window
        }
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        settingsWindow?.makeKeyAndOrderFront(nil)
        settingsWindow?.orderFrontRegardless()
    }

    func windowWillClose(_ notification: Notification) {
        guard (notification.object as? NSWindow) === settingsWindow else { return }
        NSApp.setActivationPolicy(.accessory)
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}
