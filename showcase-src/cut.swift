// Joins trimmed pieces of recordings into one 1080p H.264 mp4 with AVFoundation (no ffmpeg needed).
//   cut out.mp4  a.mov 1.0 30.0  b.mov 2.0 21.0 ...      each piece is: file, start second, end second
import AVFoundation

let a = CommandLine.arguments
precondition(a.count >= 5 && (a.count - 2) % 3 == 0, "usage: cut out.mp4 file start end [file start end ...]")
let composition = AVMutableComposition()
let track = composition.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
// CUT_INSET=x,y,w,h (source pixels): a second rectangle of the same recording laid over the top-right corner, e.g. the
// editor's Stats readout (fps and SDL draw calls), which the game itself cannot show. CUT_INSET_SCALE sets its size (1.2).
let insetRect = ProcessInfo.processInfo.environment["CUT_INSET"]?.split(separator: ",").map { CGFloat(Double($0)!) }
let insetTrack = insetRect == nil ? nil : composition.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
var at = CMTime.zero
var starts: [CMTime] = []
for i in stride(from: 2, to: a.count, by: 3) {
    let asset = AVURLAsset(url: URL(fileURLWithPath: a[i]))
    let source = asset.tracks(withMediaType: .video)[0]
    let start = CMTime(seconds: Double(a[i + 1])!, preferredTimescale: 600)
    let end = CMTimeMinimum(CMTime(seconds: Double(a[i + 2])!, preferredTimescale: 600), asset.duration)
    starts.append(at)
    try track.insertTimeRange(CMTimeRange(start: start, end: end), of: source, at: at)
    try insetTrack?.insertTimeRange(CMTimeRange(start: start, end: end), of: source, at: at)
    at = at + (end - start)
}
let out = URL(fileURLWithPath: a[1])
try? FileManager.default.removeItem(at: out)
let export = AVAssetExportSession(asset: composition, presetName: AVAssetExportPreset1920x1080)!
// CUT_CROP=x,y,w,h (source pixels): keep only that rectangle, fitted into 1920×1080. Used to save the Game view without the
// editor around it. CUT_CROPS="x,y,w,h;-;..." gives each piece its own rectangle ("-" = CUT_CROP), e.g. a full-window piece
// after Game-view pieces; CUT_INSET_ON="1;0;..." says which pieces show the inset (all by default).
let env = ProcessInfo.processInfo.environment
let rect = { (text: String) -> [CGFloat] in text.split(separator: ",").map { CGFloat(Double($0)!) } }
if let base = env["CUT_CROP"].map(rect), base.count == 4 {
    let crops = env["CUT_CROPS"].map { $0.split(separator: ";").map { $0 == "-" ? base : rect(String($0)) } } ?? Array(repeating: base, count: starts.count)
    let insetOn = env["CUT_INSET_ON"].map { $0.split(separator: ";").map { $0 == "1" } } ?? Array(repeating: true, count: starts.count)
    precondition(crops.count == starts.count && insetOn.count == starts.count, "CUT_CROPS / CUT_INSET_ON need one entry per piece")
    let size = CGSize(width: 1920, height: 1080)
    let layer = AVMutableVideoCompositionLayerInstruction(assetTrack: track)
    // CUT_PLACES="x,y,scale;-;..." puts a piece at an output position and scale instead of fitting it ("-" = fit), e.g. window
    // captures of different sizes laid on one screen-sized canvas so their real sizes compare.
    let places = env["CUT_PLACES"].map { $0.split(separator: ";").map { $0 == "-" ? nil : rect(String($0)) } } ?? Array(repeating: nil, count: starts.count)
    precondition(places.count == starts.count, "CUT_PLACES needs one entry per piece")
    for ((crop, start), place) in zip(zip(crops, starts), places) {
        let scale = place?[2] ?? min(size.width / crop[2], size.height / crop[3])
        let offset = place.map { CGPoint(x: $0[0], y: $0[1]) } ?? CGPoint(x: (size.width - crop[2] * scale) / 2, y: (size.height - crop[3] * scale) / 2)
        layer.setCropRectangle(CGRect(x: crop[0], y: crop[1], width: crop[2], height: crop[3]), at: start)
        layer.setTransform(CGAffineTransform(translationX: -crop[0], y: -crop[1]).concatenating(CGAffineTransform(scaleX: scale, y: scale))
            .concatenating(CGAffineTransform(translationX: offset.x, y: offset.y)), at: start)
    }
    let instruction = AVMutableVideoCompositionInstruction()
    instruction.timeRange = CMTimeRange(start: .zero, duration: at)
    instruction.layerInstructions = [layer]
    if let r = insetRect, r.count == 4, let insetTrack {
        let s = CGFloat(Double(env["CUT_INSET_SCALE"] ?? "1.2")!)
        let inset = AVMutableVideoCompositionLayerInstruction(assetTrack: insetTrack)
        inset.setCropRectangle(CGRect(x: r[0], y: r[1], width: r[2], height: r[3]), at: .zero)
        inset.setTransform(CGAffineTransform(translationX: -r[0], y: -r[1]).concatenating(CGAffineTransform(scaleX: s, y: s))
            .concatenating(CGAffineTransform(translationX: size.width - 16 - r[2] * s, y: 16)), at: .zero)
        for (on, start) in zip(insetOn, starts) { inset.setOpacity(on ? 1 : 0, at: start) }
        instruction.layerInstructions = [inset, layer]   // the first instruction draws on top
    }
    let video = AVMutableVideoComposition()
    video.renderSize = size
    video.frameDuration = CMTime(value: 1, timescale: 60)
    video.instructions = [instruction]
    export.videoComposition = video
}
export.outputURL = out
export.outputFileType = .mp4
export.shouldOptimizeForNetworkUse = true
let done = DispatchSemaphore(value: 0)
export.exportAsynchronously { done.signal() }
done.wait()
precondition(export.status == .completed, "export failed: \(String(describing: export.error))")
let result = AVURLAsset(url: out)
print("wrote \(a[1]): \(CMTimeGetSeconds(result.duration))s \(result.tracks(withMediaType: .video)[0].naturalSize)")
