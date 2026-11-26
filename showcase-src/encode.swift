// encode out.mp4 frames-dir [fps] : joins numbered PNG frames (0000.png, 0001.png, …) of a headless replay into an H.264 mp4.
// Frames are fitted to ENCODE_HEIGHT lines (1080 unless film.py passes --height): nearest-neighbour when that is a whole multiple, otherwise a high-quality
// downscale of frames rendered larger (film.py renders at ceil(1080 / height) times the game size). No ffmpeg.
import AVFoundation; import CoreGraphics; import ImageIO

let a = CommandLine.arguments
precondition(a.count >= 3, "usage: encode out.mp4 frames-dir [fps]")
let fps = Int32(a.count > 3 ? a[3] : "60")!
let files = try FileManager.default.contentsOfDirectory(atPath: a[2]).filter { $0.hasSuffix(".png") }.sorted()
precondition(!files.isEmpty, "no PNG frames in \(a[2])")
func load(_ name: String) -> CGImage {
    let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: a[2]).appendingPathComponent(name) as CFURL, nil)!
    return CGImageSourceCreateImageAtIndex(source, 0, nil)!
}
let first = load(files[0])
let height = Int(ProcessInfo.processInfo.environment["ENCODE_HEIGHT"] ?? "") ?? 1080
let width = (first.width * height / first.height + 1) / 2 * 2
let exact = height % first.height == 0
// ENCODE_MAX_MB (8.5; H.264 overshoots its average by up to ~10%): average bitrate that keeps the file under GitHub's 10 MB attachment limit, at most 4 Mbit/s.
let seconds = Double(files.count) / Double(fps)
let maxBytes = (Double(ProcessInfo.processInfo.environment["ENCODE_MAX_MB"] ?? "8.5") ?? 8.5) * 1_000_000
let bitrate = Int(min(4_000_000, maxBytes * 8 * 0.95 / seconds))
let out = URL(fileURLWithPath: a[1]); try? FileManager.default.removeItem(at: out)
let writer = try AVAssetWriter(outputURL: out, fileType: .mp4)
let input = AVAssetWriterInput(mediaType: .video, outputSettings: [AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: width,
    AVVideoHeightKey: height, AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: bitrate]])
let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA, kCVPixelBufferWidthKey as String: width, kCVPixelBufferHeightKey as String: height])
writer.add(input); writer.startWriting(); writer.startSession(atSourceTime: .zero)
for (i, name) in files.enumerated() {
    while !input.isReadyForMoreMediaData { Thread.sleep(forTimeInterval: 0.002) }
    var buffer: CVPixelBuffer?
    CVPixelBufferPoolCreatePixelBuffer(nil, adaptor.pixelBufferPool!, &buffer)
    CVPixelBufferLockBaseAddress(buffer!, [])
    let c = CGContext(data: CVPixelBufferGetBaseAddress(buffer!), width: width, height: height, bitsPerComponent: 8,
        bytesPerRow: CVPixelBufferGetBytesPerRow(buffer!), space: CGColorSpace(name: CGColorSpace.sRGB)!,
        bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue)!
    c.interpolationQuality = exact ? .none : .high
    c.draw(i == 0 ? first : load(name), in: CGRect(x: 0, y: 0, width: width, height: height))
    CVPixelBufferUnlockBaseAddress(buffer!, [])
    adaptor.append(buffer!, withPresentationTime: CMTime(value: CMTimeValue(i), timescale: fps))
}
input.markAsFinished()
let done = DispatchSemaphore(value: 0); writer.finishWriting { done.signal() }; done.wait()
precondition(writer.status == .completed, "\(writer.error!)")
print("\(out.path): \(files.count) frames, \(Double(files.count) / Double(fps)) s, \(width)×\(height)")
