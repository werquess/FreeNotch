import CoreImage
import Foundation

enum ColorExtractor {
    private static let context = CIContext(options: [.workingColorSpace: NSNull(), .useSoftwareRenderer: false])

    static func averageRGB(from data: Data) -> (CGFloat, CGFloat, CGFloat)? {
        guard let image = CIImage(data: data) else { return nil }
        guard let filter = CIFilter(name: "CIAreaAverage", parameters: [
            kCIInputImageKey: image,
            kCIInputExtentKey: CIVector(cgRect: image.extent)
        ]), let output = filter.outputImage else { return nil }

        var px = [UInt8](repeating: 0, count: 4)
        context.render(output, toBitmap: &px, rowBytes: 4,
                       bounds: CGRect(x: 0, y: 0, width: 1, height: 1),
                       format: .RGBA8, colorSpace: nil)

        var r = CGFloat(px[0]) / 255, g = CGFloat(px[1]) / 255, b = CGFloat(px[2]) / 255

        let maxC = max(r, g, b)
        if maxC < 0.55 {
            let boost = 0.55 / max(maxC, 0.05)
            r = min(1, r * boost); g = min(1, g * boost); b = min(1, b * boost)
        }
        return (r, g, b)
    }
}
