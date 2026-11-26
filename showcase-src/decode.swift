// decode movie.mp4 out-dir fps width : writes frames 00000.png… sampled at fps and scaled to width (no ffmpeg needed).
import AVFoundation; import CoreGraphics; import ImageIO; import UniformTypeIdentifiers
let a = CommandLine.arguments
precondition(a.count == 5, "usage: decode movie.mp4 out-dir fps width")
let asset = AVURLAsset(url: URL(fileURLWithPath: a[1])), fps = Double(a[3])!, width = Double(a[4])!
let gen = AVAssetImageGenerator(asset: asset)
gen.appliesPreferredTrackTransform = true
gen.maximumSize = CGSize(width: width, height: width)
gen.requestedTimeToleranceBefore = .zero; gen.requestedTimeToleranceAfter = .zero
let count = Int(CMTimeGetSeconds(asset.duration) * fps)
for i in 0..<count {
    let image = try gen.copyCGImage(at: CMTime(seconds: Double(i) / fps, preferredTimescale: 600), actualTime: nil)
    let url = URL(fileURLWithPath: a[2]).appendingPathComponent(String(format: "%05d.png", i))
    let d = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(d, image, nil); CGImageDestinationFinalize(d)
}
print(count)
