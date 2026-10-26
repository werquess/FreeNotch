import SwiftUI
import UniformTypeIdentifiers

struct ExpandedIslandView: View {
    let ns: Namespace.ID

    @Environment(IslandViewModel.self) private var vm
    @Environment(ShelfStore.self) private var shelf
    @Environment(SystemMonitor.self) private var system
    @Environment(Appearance.self) private var appearance
    @Environment(UpdateChecker.self) private var updater
    @Namespace private var tabNS
    @State private var dropTargeted = false
    @State private var draggingTab: IslandTab?

    var body: some View {
        VStack(spacing: 6) {
            header
                .frame(height: vm.metrics.height)

            tabContent
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }
        .padding(.horizontal, 14)
        .padding(.bottom, 12)
        .overlay {
            if dropTargeted {
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .fill(Color.blue.opacity(0.12))
                    .overlay {
                        RoundedRectangle(cornerRadius: 24, style: .continuous)
                            .strokeBorder(Color.blue, style: StrokeStyle(lineWidth: 2, dash: [7, 5]))
                    }
                    .padding(6)
                    .allowsHitTesting(false)
                    .transition(.opacity)
            }
        }
        .onDrop(of: [.fileURL], isTargeted: $dropTargeted.animation(.easeOut(duration: 0.15))) { providers in
            shelf.add(providers: providers)
            withAnimation(Motion.tab) { vm.selectedTab = .shelf }
            return true
        }
    }

    private var header: some View {
        HStack(spacing: 0) {
            HStack(spacing: 1) {
                ForEach(vm.visibleTabs) { tab in
                    tabButton(tab)
                }
            }
            .animation(Motion.tab, value: vm.visibleTabs)
            Spacer(minLength: vm.metrics.width + 8)
            HStack(spacing: 8) {
                Text(vm.selectedTab.title)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(Theme.secondary)
                    .contentTransition(.opacity)
                if system.hasBattery {
                    MacBatteryIcon(level: system.batteryLevel, charging: system.isCharging,
                                   lowPower: ProcessInfo.processInfo.isLowPowerModeEnabled, height: 12)
                }
                Button { vm.openSettings?() } label: {
                    Image(systemName: "gearshape.fill").font(.system(size: 11))
                        .overlay(alignment: .topTrailing) {
                            if updater.availableRelease != nil {
                                Circle().fill(appearance.accent).frame(width: 6, height: 6).offset(x: 3, y: -3)
                            }
                        }
                }
                .buttonStyle(IslandButtonStyle(size: 22))
                .foregroundStyle(.white.opacity(0.7))
                .help(String(localized: "Settings"))
            }
        }
    }

    private func tabButton(_ tab: IslandTab) -> some View {
        let selected = vm.selectedTab == tab
        let tabs = vm.visibleTabs
        let index = tabs.firstIndex(of: tab) ?? 0
        return Button {
            withAnimation(Motion.tab) { vm.selectedTab = tab }
        } label: {
            Image(systemName: tab.symbol)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(selected ? appearance.onAccent : .white.opacity(0.7))
                .frame(width: 26, height: 22)
                .background {
                    if selected {
                        Capsule()
                            .fill(appearance.accent)
                            .matchedGeometryEffect(id: "tabSelection", in: tabNS)
                    }
                }
                .contentShape(Capsule())
                .opacity(draggingTab == tab ? 0.4 : 1)
        }
        .buttonStyle(.plain)
        .help(tab.title)
        .draggable(tab.rawValue) {
            Image(systemName: tab.symbol)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(appearance.onAccent)
                .frame(width: 30, height: 24)
                .background(Capsule().fill(appearance.accent))
                .onAppear { draggingTab = tab }
        }
        .dropDestination(for: String.self) { items, _ in
            draggingTab = nil
            guard let raw = items.first, let moved = IslandTab(rawValue: raw), moved != tab else { return false }
            withAnimation(Motion.tab) { Prefs.moveTab(moved, before: tab) }
            return true
        } isTargeted: { _ in }
        .contextMenu {
            Button(String(localized: "Move Left")) { withAnimation(Motion.tab) { Prefs.moveTab(tab, by: -1) } }
                .disabled(index == 0)
            Button(String(localized: "Move Right")) { withAnimation(Motion.tab) { Prefs.moveTab(tab, by: 1) } }
                .disabled(index == tabs.count - 1)
            Divider()
            Button(String(localized: "Hide \(tab.title)")) { Prefs.setTabEnabled(tab, false) }
                .disabled(tabs.count <= 1)
            Divider()
            Button(String(localized: "Customize Tabs…")) { vm.openSettings?() }
        }
    }

    @ViewBuilder
    private var tabContent: some View {
        Group {
            switch vm.selectedTab {
            case .media: MediaExpandedView(ns: ns)
            case .shelf: ShelfView()
            case .clipboard: ClipboardView()
            case .notes: NotesView()
            case .system: SystemView()
            case .focus: FocusView()
            case .tools: ToolsView()
            }
        }
        .id(vm.selectedTab)
        .transition(.tabContent)
    }
}
