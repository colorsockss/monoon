// frames movie.mov out.png t1 t2 ... : contact sheet of frames at the given seconds (checks a recording without ffmpeg)
import AVFoundation; import CoreGraphics; import ImageIO; import UniformTypeIdentifiers
let a = CommandLine.arguments, asset = AVURLAsset(url: URL(fileURLWithPath: a[1])), times = a[3...].map { Double($0)! }
let gen = AVAssetImageGenerator(asset: asset); gen.appliesPreferredTrackTransform = true; let side = Double(ProcessInfo.processInfo.environment["FRAME_MAX"] ?? "640")!; gen.maximumSize = CGSize(width: side, height: side)   // FRAME_MAX=1920 for a close look
gen.requestedTimeToleranceBefore = .zero; gen.requestedTimeToleranceAfter = .zero
let frames = times.compactMap { try? gen.copyCGImage(at: CMTime(seconds: $0, preferredTimescale: 600), actualTime: nil) }
let cols = min(3, frames.count), rows = (frames.count + cols - 1) / cols, w = frames[0].width, h = frames[0].height
let c = CGContext(data: nil, width: cols * w, height: rows * h, bitsPerComponent: 8, bytesPerRow: 0, space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
for (i, f) in frames.enumerated() { c.draw(f, in: CGRect(x: i % cols * w, y: (rows - 1 - i / cols) * h, width: w, height: h)) }
let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: a[2]) as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(d, c.makeImage()!, nil); CGImageDestinationFinalize(d)
let track = asset.tracks(withMediaType: .video)[0]
print("duration \(CMTimeGetSeconds(asset.duration))s size \(track.naturalSize) fps \(track.nominalFrameRate) frames \(frames.count)")
