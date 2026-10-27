import SwiftUI

struct IslandScene: View {
    let env: AppEnvironment

    var body: some View {
        IslandRootView()
            .environment(env.island)
            .environment(env.media)
            .environment(env.lyrics)
            .environment(env.shelf)
            .environment(env.clipboard)
            .environment(env.notes)
            .environment(env.system)
            .environment(env.volume)
            .environment(env.pomodoro)
            .environment(env.stopwatch)
            .environment(env.caffeine)
            .environment(env.calendar)
            .environment(env.appearance)
            .environment(env.updater)
            .foregroundStyle(.white)
            .preferredColorScheme(.dark)
    }
}

struct IslandRootView: View {
    @Environment(IslandViewModel.self) private var vm
    @Environment(Appearance.self) private var appearance
    @Namespace private var ns

    var body: some View {
        let p = vm.presentation
        let size = vm.size(for: p)
        let shape = NotchShape(topRadius: p.topRadius, bottomRadius: p.bottomRadius)
        let canvas = vm.canvasSize

        VStack(spacing: 0) {
            ZStack(alignment: .top) {
                content(for: p)
                    .padding(.horizontal, p.topRadius)
                    .frame(width: size.width, height: size.height, alignment: .top)
            }
            .frame(width: size.width, height: size.height)
            .background(IslandBackground(theme: appearance.theme, large: p.isLarge))
            .clipShape(shape)
            .overlay {
                shape.stroke(.white.opacity(vm.isHovering && !p.isLarge ? 0.18 : 0), lineWidth: 1)
            }
            .shadow(color: .black.opacity(p.isLarge ? 0.5 : (vm.isHovering ? 0.35 : 0)), radius: p.isLarge ? 22 : 10, y: p.isLarge ? 10 : 4)
            .contentShape(shape)

            Spacer(minLength: 0)
        }
        .frame(width: canvas.width, height: canvas.height, alignment: .top)
        .animation(Motion.forPresentation(p).speed(vm.animationSpeed), value: p.key)
        .animation(Motion.hover.speed(vm.animationSpeed), value: vm.isHovering)
        .animation(Motion.tab.speed(vm.animationSpeed), value: vm.selectedTab)
        .animation(Motion.activity, value: vm.metrics)
        .animation(Motion.activity, value: vm.prefsVersion)
        .animation(Motion.activity, value: vm.showsLyricLine)
    }

    @ViewBuilder
    private func content(for p: Presentation) -> some View {
        switch p {
        case .idle:
            Color.clear
        case .activity(let activity):
            CompactActivityView(activity: activity, notchWidth: vm.metrics.width, notchHeight: vm.metrics.height)
                .transition(.islandContent)
                .id(activity.contentKey)
        case .hud(let event):
            HUDView(event: event, notchWidth: vm.metrics.width, notchHeight: vm.metrics.height)
                .transition(.islandContent)
        case .dropZone:
            DropZoneView()
                .transition(.islandContent)
        case .expanded:
            ExpandedIslandView(ns: ns)
                .transition(.islandContent)
        }
    }
}
