import SwiftUI

struct HUDView: View {
    let event: HUDEvent
    let notchWidth: CGFloat
    let notchHeight: CGFloat
    @Environment(Appearance.self) private var appearance

    var body: some View {
        HStack(spacing: 0) {
            Image(systemName: event.symbol, variableValue: Double(event.muted ? 0 : event.level))
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(.white)
                .frame(width: 22, alignment: .leading)
                .frame(width: 96 - 14, alignment: .leading)
                .padding(.leading, 14)

            Spacer(minLength: notchWidth - 28)

            LevelBar(level: event.muted ? 0 : event.level, tint: tint)
                .frame(width: 64, height: 6)
                .padding(.trailing, 14)
                .frame(width: 96, alignment: .trailing)
        }
        .frame(height: notchHeight)
        .animation(Motion.level, value: event.level)
        .animation(Motion.level, value: event.muted)
    }

    private var tint: Color {
        switch event.kind {
        case .volume: appearance.accent
        case .brightness: .yellow
        case .keyboard: .cyan
        }
    }
}

struct LevelBar: View {
    var level: Float
    var tint: Color = .white

    var body: some View {
        GeometryReader { geo in
            ZStack(alignment: .leading) {
                Capsule().fill(.white.opacity(0.18))
                Capsule()
                    .fill(tint)
                    .frame(width: max(0, geo.size.width * CGFloat(min(max(level, 0), 1))))
            }
        }
    }
}
