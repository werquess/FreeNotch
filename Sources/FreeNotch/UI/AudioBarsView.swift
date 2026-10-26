import AppKit
import SwiftUI

struct AudioBarsView: NSViewRepresentable {
    var isAnimating: Bool
    var color: NSColor

    func makeNSView(context: Context) -> AudioBarsNSView { AudioBarsNSView() }

    func updateNSView(_ view: AudioBarsNSView, context: Context) {
        view.barColor = color
        view.setAnimating(isAnimating)
    }
}

final class AudioBarsNSView: NSView {
    private let bars: [CALayer] = (0..<4).map { _ in CALayer() }
    private let durations: [CFTimeInterval] = [0.46, 0.58, 0.38, 0.52]
    private var animating = false

    var barColor: NSColor = .white {
        didSet {
            guard oldValue != barColor else { return }
            bars.forEach { $0.backgroundColor = barColor.cgColor }
        }
    }

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
        layer?.masksToBounds = false
        for bar in bars {
            bar.backgroundColor = barColor.cgColor
            bar.cornerRadius = 1.5
            bar.transform = CATransform3DMakeScale(1, 0.3, 1)
            layer?.addSublayer(bar)
        }
    }

    required init?(coder: NSCoder) { fatalError() }

    override func layout() {
        super.layout()
        let w: CGFloat = 3
        let gap = (bounds.width - w * CGFloat(bars.count)) / CGFloat(bars.count - 1)
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        for (i, bar) in bars.enumerated() {
            bar.bounds = CGRect(x: 0, y: 0, width: w, height: bounds.height)
            bar.position = CGPoint(x: CGFloat(i) * (w + gap) + w / 2, y: bounds.midY)
        }
        CATransaction.commit()
    }

    func setAnimating(_ on: Bool) {
        guard on != animating else { return }
        animating = on
        let now = CACurrentMediaTime()
        for (i, bar) in bars.enumerated() {
            if on {
                let a = CABasicAnimation(keyPath: "transform.scale.y")
                a.fromValue = 0.25
                a.toValue = 1.0
                a.duration = durations[i]
                a.autoreverses = true
                a.repeatCount = .infinity
                a.beginTime = now + Double(i) * 0.08
                a.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
                a.fillMode = .backwards
                bar.add(a, forKey: "bounce")
            } else {
                bar.removeAnimation(forKey: "bounce")
                bar.transform = CATransform3DMakeScale(1, 0.3, 1)
            }
        }
    }
}
