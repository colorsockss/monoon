// Lays two window recordings side by side on one 1080p H.264 mp4 with AVFoundation (no ffmpeg needed): a game window on the
// left (1152 × 648) and a terminal on the right (fitted into 768 × 1000), each under a one-line title. The two recordings run
// on one clock: the left piece starts at its own second, the right one at its own, both shown from output time 0. When the
// left piece ends before the right one, its last frame holds.
//   sidebyside out.mp4  left.mov leftStart leftEnd leftCrop  right.mov rightStart rightEnd rightCrop  "left title" "right title"
// Crops are x,y,w,h in source pixels.   Build:  swiftc -O showcase-src/sidebyside.swift -o <tools>/sidebyside
import AVFoundation
import AppKit

let a = CommandLine.arguments
guard a.count == 12 else { print("usage: sidebyside out.mp4 left.mov s e x,y,w,h right.mov s e x,y,w,h leftTitle rightTitle"); exit(2) }
let rect = { (s: String) -> CGRect in let v = s.split(separator: ",").map { CGFloat(Double($0)!) }; return CGRect(x: v[0], y: v[1], width: v[2], height: v[3]) }
let size = CGSize(width: 1920, height: 1080)
let sec = { (s: String) in CMTime(seconds: Double(s)!, preferredTimescale: 600) }
let out = URL(fileURLWithPath: a[1])
let pieces = [(URL(fileURLWithPath: a[2]), sec(a[3]), sec(a[4]), rect(a[5]), CGRect(x: 0, y: 216, width: 1152, height: 648)),
              (URL(fileURLWithPath: a[6]), sec(a[7]), sec(a[8]), rect(a[9]), CGRect(x: 1152, y: 80, width: 768, height: 1000))]
let total = max(pieces[0].2 - pieces[0].1, pieces[1].2 - pieces[1].1)

let composition = AVMutableComposition()
var layers: [AVMutableVideoCompositionLayerInstruction] = []
for (url, start, end, crop, box) in pieces {
    let source = AVURLAsset(url: url)
    let video = try await source.loadTracks(withMediaType: .video)[0]
    let track = composition.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
    try track.insertTimeRange(CMTimeRange(start: start, end: end), of: video, at: .zero)
    let length = end - start
    if length < total {   // hold the last frame for the rest
        let last = CMTime(seconds: 1.0 / 30, preferredTimescale: 600)
        try track.insertTimeRange(CMTimeRange(start: end - last, duration: last), of: video, at: length)
        track.scaleTimeRange(CMTimeRange(start: length, duration: last), toDuration: total - length)
    }
    let scale = min(box.width / crop.width, box.height / crop.height)
    let offset = CGPoint(x: box.minX + (box.width - crop.width * scale) / 2, y: box.minY + (box.height - crop.height * scale) / 2)
    let layer = AVMutableVideoCompositionLayerInstruction(assetTrack: track)
    layer.setCropRectangle(crop, at: .zero)
    layer.setTransform(CGAffineTransform(translationX: -crop.minX, y: -crop.minY).concatenating(CGAffineTransform(scaleX: scale, y: scale))
        .concatenating(CGAffineTransform(translationX: offset.x, y: offset.y)), at: .zero)
    layers.append(layer)
}
let instruction = AVMutableVideoCompositionInstruction()
instruction.timeRange = CMTimeRange(start: .zero, duration: total)
instruction.layerInstructions = layers
instruction.backgroundColor = NSColor(red: 0.105, green: 0.086, blue: 0.149, alpha: 1).cgColor
let videoComposition = AVMutableVideoComposition()
videoComposition.renderSize = size
videoComposition.frameDuration = CMTime(value: 1, timescale: 60)
videoComposition.instructions = [instruction]

// Titles: Core Animation over the video (layer y runs up from the bottom).
let parent = CALayer(), videoLayer = CALayer()
parent.frame = CGRect(origin: .zero, size: size); videoLayer.frame = parent.frame
parent.addSublayer(videoLayer)
for (title, frame) in [(a[10], CGRect(x: 24, y: 1080 - 200, width: 1110, height: 44)), (a[11], CGRect(x: 1176, y: 1080 - 64, width: 730, height: 44))] {
    let text = CATextLayer()
    text.string = title; text.font = "Helvetica-Bold" as CFString; text.fontSize = 30
    text.alignmentMode = .left; text.isWrapped = false
    text.foregroundColor = NSColor(red: 1, green: 0.85, blue: 0.29, alpha: 1).cgColor
    text.frame = frame; text.contentsScale = 2
    text.display()
    parent.addSublayer(text)
}
videoComposition.animationTool = AVVideoCompositionCoreAnimationTool(postProcessingAsVideoLayer: videoLayer, in: parent)

try? FileManager.default.removeItem(at: out)
let export = AVAssetExportSession(asset: composition, presetName: AVAssetExportPreset1920x1080)!
export.videoComposition = videoComposition
try await export.export(to: out, as: .mp4)
print("wrote \(out.path): \(total.seconds)s")
