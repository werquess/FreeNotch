import SwiftUI

enum Theme {
    static let cardRadius: CGFloat = 14
    static let hairline = Color.white.opacity(0.10)
    static let hoverFill = Color.white.opacity(0.08)
    static let fieldFill = Color.white.opacity(0.08)
    static let secondary = Color.white.opacity(0.58)
    static let tertiary = Color.white.opacity(0.38)
}

private struct IslandCardModifier: ViewModifier {
    let radius: CGFloat
    let highlighted: Bool
    @Environment(Appearance.self) private var appearance

    func body(content: Content) -> some View {
        content
            .background {
                RoundedRectangle(cornerRadius: radius, style: .continuous)
                    .fill(highlighted ? Theme.hoverFill : appearance.cardFill)
            }
            .overlay {
                RoundedRectangle(cornerRadius: radius, style: .continuous)
                    .strokeBorder(appearance.hairline, lineWidth: 1)
            }
    }
}

extension View {
    func islandCard(radius: CGFloat = Theme.cardRadius, highlighted: Bool = false) -> some View {
        modifier(IslandCardModifier(radius: radius, highlighted: highlighted))
    }

    func islandField() -> some View {
        padding(.horizontal, 9)
            .padding(.vertical, 5)
            .background(Capsule().fill(Theme.fieldFill))
    }
}
