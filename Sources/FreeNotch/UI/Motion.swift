import SwiftUI

enum Motion {
    static let open = Animation.spring(response: 0.44, dampingFraction: 0.78)
    static let close = Animation.spring(response: 0.38, dampingFraction: 0.94)
    static let activity = Animation.spring(response: 0.38, dampingFraction: 0.78)
    static let hud = Animation.spring(response: 0.30, dampingFraction: 0.72)
    static let tab = Animation.spring(response: 0.30, dampingFraction: 0.86)
    static let level = Animation.smooth(duration: 0.16)
    static let hover = Animation.spring(response: 0.28, dampingFraction: 0.62)
    static let drop = Animation.spring(response: 0.34, dampingFraction: 0.7)

    static func forPresentation(_ p: Presentation) -> Animation {
        switch p {
        case .expanded: open
        case .dropZone: drop
        case .idle: close
        case .hud: hud
        case .activity: activity
        }
    }
}

private struct BlurOpacity: ViewModifier {
    let radius: CGFloat
    let opacity: Double
    let scale: CGFloat

    func body(content: Content) -> some View {
        content
            .blur(radius: radius)
            .opacity(opacity)
            .scaleEffect(scale, anchor: .top)
    }
}

extension AnyTransition {
    static var islandContent: AnyTransition {
        .asymmetric(
            insertion: .modifier(active: BlurOpacity(radius: 8, opacity: 0, scale: 0.92),
                                 identity: BlurOpacity(radius: 0, opacity: 1, scale: 1))
                .animation(.spring(response: 0.38, dampingFraction: 0.86).delay(0.05)),
            removal: .modifier(active: BlurOpacity(radius: 6, opacity: 0, scale: 0.96),
                               identity: BlurOpacity(radius: 0, opacity: 1, scale: 1))
                .animation(.easeOut(duration: 0.14))
        )
    }

    static var tabContent: AnyTransition {
        .asymmetric(
            insertion: .opacity.combined(with: .offset(y: 4))
                .animation(.smooth(duration: 0.22).delay(0.06)),
            removal: .opacity.animation(.easeOut(duration: 0.08))
        )
    }

    static var blurFade: AnyTransition {
        .modifier(active: BlurOpacity(radius: 5, opacity: 0, scale: 0.98),
                  identity: BlurOpacity(radius: 0, opacity: 1, scale: 1))
    }
}

struct IslandButtonStyle: ButtonStyle {
    var size: CGFloat = 30
    var filled = false

    func makeBody(configuration: Configuration) -> some View {
        StyleBody(configuration: configuration, size: size, filled: filled)
    }

    private struct StyleBody: View {
        let configuration: Configuration
        let size: CGFloat
        let filled: Bool
        @State private var hovering = false

        var body: some View {
            configuration.label
                .frame(width: size, height: size)
                .background(
                    Circle().fill(.white.opacity(filled ? 0.16 : (hovering ? 0.10 : 0)))
                )
                .contentShape(Circle())
                .scaleEffect(configuration.isPressed ? 0.86 : 1)
                .animation(.spring(response: 0.22, dampingFraction: 0.6), value: configuration.isPressed)
                .animation(.easeOut(duration: 0.12), value: hovering)
                .onHover { hovering = $0 }
        }
    }
}
