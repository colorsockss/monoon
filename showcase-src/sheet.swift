// Contact sheet: sheet out.png cell in1 in2 ...  (each image fitted into a cell on a grey checker background)
import CoreGraphics; import Foundation; import ImageIO; import UniformTypeIdentifiers
let a = CommandLine.arguments, cell = Int(a[2])!, files = Array(a[3...]), cols = min(files.count, 5), rows = (files.count + cols - 1) / cols
let c = CGContext(data: nil, width: cols * cell, height: rows * cell, bitsPerComponent: 8, bytesPerRow: 0, space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
c.setFillColor(CGColor(srgbRed: 0.45, green: 0.45, blue: 0.5, alpha: 1)); c.fill(CGRect(x: 0, y: 0, width: cols * cell, height: rows * cell))
for (i, f) in files.enumerated() {
    let s = CGImageSourceCreateWithURL(URL(fileURLWithPath: f) as CFURL, nil)!, img = CGImageSourceCreateImageAtIndex(s, 0, nil)!
    let k = min(Double(cell - 8) / Double(img.width), Double(cell - 8) / Double(img.height)), w = Double(img.width) * k, h = Double(img.height) * k
    c.draw(img, in: CGRect(x: Double(i % cols * cell) + (Double(cell) - w) / 2, y: Double((rows - 1 - i / cols) * cell) + (Double(cell) - h) / 2, width: w, height: h))
}
let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: a[1]) as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(d, c.makeImage()!, nil); CGImageDestinationFinalize(d)
