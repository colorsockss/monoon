// encode out.mp4 frames-dir [fps] : joins numbered PNG frames (0000.png, 0001.png, …) of a headless replay into an H.264 mp4.
// Frames are scaled up with nearest-neighbour by ENCODE_SCALE (2: 800×450 -> 1600×900) so pixel art stays sharp. No ffmpeg.
import AVFoundation; import CoreGraphics; import ImageIO

let a = CommandLine.arguments
precondition(a.count >= 3, "usage: encode out.mp4 frames-dir [fps]")
let fps = Int32(a.count > 3 ? a[3] : "60")!
let scale = Int(ProcessInfo.processInfo.environment["ENCODE_SCALE"] ?? "2")!
let files = try FileManager.default.contentsOfDirectory(atPath: a[2]).filter { $0.hasSuffix(".png") }.sorted()
precondition(!files.isEmpty, "no PNG frames in \(a[2])")
func load(_ name: String) -> CGImage {
    let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: a[2]).appendingPathComponent(name) as CFURL, nil)!
    return CGImageSourceCreateImageAtIndex(source, 0, nil)!
}
let first = load(files[0]), width = first.width * scale, height = first.height * scale
let out = URL(fileURLWithPath: a[1]); try? FileManager.default.removeItem(at: out)
let writer = try AVAssetWriter(outputURL: out, fileType: .mp4)
let input = AVAssetWriterInput(mediaType: .video, outputSettings: [AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: width,
    AVVideoHeightKey: height, AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 12_000_000]])
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
    c.interpolationQuality = .none
    c.draw(i == 0 ? first : load(name), in: CGRect(x: 0, y: 0, width: width, height: height))
    CVPixelBufferUnlockBaseAddress(buffer!, [])
    adaptor.append(buffer!, withPresentationTime: CMTime(value: CMTimeValue(i), timescale: fps))
}
input.markAsFinished()
let done = DispatchSemaphore(value: 0); writer.finishWriting { done.signal() }; done.wait()
precondition(writer.status == .completed, "\(writer.error!)")
print("\(out.path): \(files.count) frames, \(Double(files.count) / Double(fps)) s, \(width)×\(height)")
