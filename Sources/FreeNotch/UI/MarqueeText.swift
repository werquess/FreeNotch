import SwiftUI

struct MarqueeText: View {
    let text: String
    var size: CGFloat = 11
    var weight: Font.Weight = .semibold
    var alignment: Alignment = .leading
    var speed: CGFloat = 32
    var pause: Double = 1.1

    @State private var textWidth: CGFloat = 0
    @State private var boxWidth: CGFloat = 0
    @State private var phase: CGFloat = 0

    private let gap: CGFloat = 28
    private var font: Font { .system(size: size, weight: weight) }
    private var overflows: Bool { textWidth > boxWidth + 0.5 && boxWidth > 0 }

    var body: some View {
        Text(text)
            .font(font)
            .lineLimit(1)
            .hidden()
            .frame(minWidth: 0, maxWidth: .infinity, alignment: alignment)
            .overlay(alignment: overflows ? .leading : alignment) {
                if overflows {
                    HStack(spacing: gap) {
                        label
                        label
                    }
                    .offset(x: -phase)
                } else {
                    label
                }
            }
            .clipped()
            .mask {
                if overflows {
                    LinearGradient(stops: [
                        .init(color: .clear, location: 0),
                        .init(color: .black, location: 0.1),
                        .init(color: .black, location: 0.9),
                        .init(color: .clear, location: 1),
                    ], startPoint: .leading, endPoint: .trailing)
                } else {
                    Rectangle()
                }
            }
            .background {
                GeometryReader { box in
                    Color.clear
                        .onAppear { boxWidth = box.size.width }
                        .onChange(of: box.size.width) { _, width in boxWidth = width }
                }
            }
            .background {
                label
                    .hidden()
                    .background {
                        GeometryReader { measured in
                            Color.clear
                                .onAppear { textWidth = measured.size.width }
                                .onChange(of: measured.size.width) { _, width in textWidth = width }
                        }
                    }
            }
            .task(id: "\(text)|\(overflows)|\(Int(textWidth.rounded()))") { await run() }
    }

    private var label: some View {
        Text(text)
            .font(font)
            .lineLimit(1)
            .fixedSize()
    }

    private func run() async {
        phase = 0
        guard overflows else { return }
        while !Task.isCancelled {
            try? await Task.sleep(for: .seconds(pause))
            guard !Task.isCancelled else { return }
            let distance = textWidth + gap
            let duration = Double(distance / speed)
            withAnimation(.linear(duration: duration)) { phase = distance }
            try? await Task.sleep(for: .seconds(duration + 0.05))
            guard !Task.isCancelled else { return }
            phase = 0
        }
    }
}
