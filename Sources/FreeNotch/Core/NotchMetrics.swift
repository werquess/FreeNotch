import AppKit

struct NotchMetrics: Equatable {
    var width: CGFloat
    var height: CGFloat
    var hasPhysicalNotch: Bool

    static let fallback = NotchMetrics(width: 190, height: 32, hasPhysicalNotch: false)

    static func current(for screen: NSScreen?) -> NotchMetrics {
        guard let screen else { return fallback }
        let top = screen.safeAreaInsets.top
        if top > 0, let left = screen.auxiliaryTopLeftArea, let right = screen.auxiliaryTopRightArea {
            let width = screen.frame.width - left.width - right.width
            return NotchMetrics(width: width.rounded(), height: top, hasPhysicalNotch: true)
        }

        let menuBar = screen.frame.maxY - screen.visibleFrame.maxY
        return NotchMetrics(width: 190, height: max(menuBar, 24), hasPhysicalNotch: false)
    }
}

extension NSScreen {
    static var islandScreen: NSScreen? {
        screens.first { $0.safeAreaInsets.top > 0 } ?? main
    }
}
