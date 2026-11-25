// Turns generated "pixel art" into real pixel art: one art pixel = 2 output pixels, colours from one palette.
//   pix palette in.png out.txt N [more.png ...]  N-colour palette by median cut over all the sheets (one #RRGGBB per line)
//   pix boxes   in.png                           bounding boxes of the separate objects on a transparent sheet
//   pix sprite  in out W H pal.txt [x y w h]     crop (source px), trim, fit inside W×H art px, feet on the bottom row
//   pix tex     in out W H pal.txt [x y w h]     crop, no trim, stretch to exactly W×H art px
//   pix tiles   in out W H pal.txt cw ch shift clean [x y w h]   tex, then redraw the joints of a cw×ch grid (see below)
//   pix mirrorx in out                          left half mirrored onto the right half
//   pix sway    in out rows shift               top rows shifted sideways (gentle flame frames)
//   pix slice9  in out W H l t r b              rebuild a 2-px art PNG at W×H art px, repeating its edges and centre
// Each art pixel takes the palette colour most of its source pixels are nearest to, so edges stay hard.
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

struct Bitmap { var w: Int, h: Int, px: [UInt8] }   // RGBA, rows from the top, straight alpha

func load(_ path: String) -> Bitmap {
    let image = CGImageSourceCreateImageAtIndex(CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil)!, 0, nil)!
    let w = image.width, h = image.height
    var px = [UInt8](repeating: 0, count: w * h * 4)
    let c = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    c.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    for i in stride(from: 0, to: px.count, by: 4) where px[i + 3] > 0 && px[i + 3] < 255 {   // undo premultiplication
        for k in 0..<3 { px[i + k] = UInt8(min(255, Int(px[i + k]) * 255 / Int(px[i + 3]))) }
    }
    return Bitmap(w: w, h: h, px: px)
}

func save(_ b: Bitmap, _ path: String) {   // alpha is only ever 0 or 255 here, so straight = premultiplied
    var px = b.px
    let c = CGContext(data: &px, width: b.w, height: b.h, bitsPerComponent: 8, bytesPerRow: b.w * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(d, c.makeImage()!, nil)
    precondition(CGImageDestinationFinalize(d), "cannot write \(path)")
}

func opaque(_ b: Bitmap, _ x: Int, _ y: Int) -> Bool { b.px[(y * b.w + x) * 4 + 3] > 128 }

func readPalette(_ path: String) -> [[Int]] {
    try! String(contentsOfFile: path, encoding: .utf8).split(separator: "\n").map { line in
        let v = Int(line.dropFirst().prefix(6), radix: 16)!
        return [v >> 16, (v >> 8) & 255, v & 255]
    }
}

var nearestCache = [Int: Int]()
func nearest(_ pal: [[Int]], _ r: Int, _ g: Int, _ b: Int) -> Int {
    let key = r << 16 | g << 8 | b
    if let hit = nearestCache[key] { return hit }
    var best = 0, bestD = Int.max
    for (i, p) in pal.enumerated() {
        let d = 2 * (p[0] - r) * (p[0] - r) + 4 * (p[1] - g) * (p[1] - g) + 3 * (p[2] - b) * (p[2] - b)
        if d < bestD { bestD = d; best = i }
    }
    nearestCache[key] = best
    return best
}

let a = CommandLine.arguments
precondition(a.count >= 3, "usage: pix palette|boxes|sprite|tex ...")
let src = load(a[2])

switch a[1] {
case "palette":
    // Each distinct colour counts once (5 bits per channel), else the floor's greys take every slot and the moss, tunic and gold get none.
    var distinct = Set<Int>()
    for sheet in [src] + a.dropFirst(5).map(load) {
        for y in 0..<sheet.h { for x in 0..<sheet.w where opaque(sheet, x, y) {
            let i = (y * sheet.w + x) * 4
            distinct.insert((Int(sheet.px[i]) >> 3) << 16 | (Int(sheet.px[i + 1]) >> 3) << 8 | (Int(sheet.px[i + 2]) >> 3))
        } }
    }
    var boxes: [[[Int]]] = [distinct.map { [($0 >> 16) << 3 | 4, ($0 >> 8 & 255) << 3 | 4, ($0 & 255) << 3 | 4] }]
    while boxes.count < Int(a[4])! {
        // split the box with the widest channel at that channel's median
        var pick = 0, channel = 0, widest = -1
        for (i, box) in boxes.enumerated() where box.count > 1 {
            for k in 0..<3 {
                let range = box.map { $0[k] }.max()! - box.map { $0[k] }.min()!
                if range > widest { widest = range; pick = i; channel = k }
            }
        }
        let sorted = boxes[pick].sorted { $0[channel] < $1[channel] }
        boxes[pick] = Array(sorted[..<(sorted.count / 2)])
        boxes.append(Array(sorted[(sorted.count / 2)...]))
    }
    let lines = boxes.map { box -> String in
        let n = box.count
        return String(format: "#%02X%02X%02X", box.reduce(0) { $0 + $1[0] } / n, box.reduce(0) { $0 + $1[1] } / n, box.reduce(0) { $0 + $1[2] } / n)
    }
    try! lines.sorted().joined(separator: "\n").write(toFile: a[3], atomically: true, encoding: .utf8)
    print("wrote \(lines.count) colours to \(a[3])")

case "boxes":
    // flood fill on a coarse grid so parts a few pixels apart (a flame above its sconce) count as one object
    let cell = 12, gw = (src.w + cell - 1) / cell, gh = (src.h + cell - 1) / cell
    var filled = [Bool](repeating: false, count: gw * gh)
    for y in 0..<src.h { for x in 0..<src.w where opaque(src, x, y) { filled[(y / cell) * gw + x / cell] = true } }
    var seen = [Bool](repeating: false, count: gw * gh)
    var found: [(Int, Int, Int, Int)] = []
    for start in 0..<(gw * gh) where filled[start] && !seen[start] {
        var stack = [start], x0 = gw, y0 = gh, x1 = 0, y1 = 0
        seen[start] = true
        while let at = stack.popLast() {
            let x = at % gw, y = at / gw
            x0 = min(x0, x); x1 = max(x1, x); y0 = min(y0, y); y1 = max(y1, y)
            for (dx, dy) in [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)] {
                let nx = x + dx, ny = y + dy
                if nx >= 0, ny >= 0, nx < gw, ny < gh, filled[ny * gw + nx], !seen[ny * gw + nx] { seen[ny * gw + nx] = true; stack.append(ny * gw + nx) }
            }
        }
        if (x1 - x0 + 1) * (y1 - y0 + 1) >= 4 { found.append((x0 * cell, y0 * cell, (x1 - x0 + 1) * cell, (y1 - y0 + 1) * cell)) }
    }
    for b in found.sorted(by: { ($0.1 / 150, $0.0) < ($1.1 / 150, $1.0) }) { print(b.0, b.1, b.2, b.3) }   // reading order

case "grid":
    // Source pixels per drawn "pixel": the generator draws squares of one size, and the commonest short run lengths are its multiples.
    var runs = [Int: Int]()
    for y in stride(from: 0, to: src.h, by: 3) {
        var start = 0
        for x in 1...src.w {
            let same = x < src.w && opaque(src, x, y) && opaque(src, x - 1, y) && (0..<3).allSatisfy { abs(Int(src.px[(y * src.w + x) * 4 + $0]) - Int(src.px[(y * src.w + x - 1) * 4 + $0])) < 24 }
            if !same { if opaque(src, min(x, src.w) - 1, y), x - start >= 4, x - start <= 60 { runs[x - start, default: 0] += 1 }; start = x }
        }
    }
    for (length, count) in runs.sorted(by: { $0.value > $1.value }).prefix(12) { print(length, count) }

case "sprite", "tex", "tiles":
    // "@9.5 -" in place of W H: canvas sized from the trimmed object at 9.5 source pixels per art pixel, so one sheet keeps one scale.
    let perArt = a[4].hasPrefix("@") ? Double(a[4].dropFirst())! : 0
    var W = Int(a[4]) ?? 0, H = Int(a[5]) ?? 0
    let pal = readPalette(a[6])
    let crop = a[1] == "tiles" ? 11 : 7   // tiles takes four more arguments before the crop
    var x0 = 0, y0 = 0, x1 = src.w - 1, y1 = src.h - 1
    if a.count > crop + 3 { x0 = Int(a[crop])!; y0 = Int(a[crop + 1])!; x1 = min(src.w, x0 + Int(a[crop + 2])!) - 1; y1 = min(src.h, y0 + Int(a[crop + 3])!) - 1 }
    var dw = W, dh = H, ox = 0, oy = 0   // drawn size and offset inside the W×H canvas, in art pixels
    if a[1] == "sprite" {
        var tx0 = x1, ty0 = y1, tx1 = x0, ty1 = y0
        for y in y0...y1 { for x in x0...x1 where opaque(src, x, y) { tx0 = min(tx0, x); tx1 = max(tx1, x); ty0 = min(ty0, y); ty1 = max(ty1, y) } }
        precondition(tx1 >= tx0, "nothing opaque in the crop")
        (x0, y0, x1, y1) = (tx0, ty0, tx1, ty1)
        if perArt > 0 { W = Int((Double(x1 - x0 + 1) / perArt).rounded()); H = Int((Double(y1 - y0 + 1) / perArt).rounded()); dw = W; dh = H }
        let scale = min(Double(W) / Double(x1 - x0 + 1), Double(H) / Double(y1 - y0 + 1))
        dw = max(1, Int((Double(x1 - x0 + 1) * scale).rounded())); dh = max(1, Int((Double(y1 - y0 + 1) * scale).rounded()))
        ox = (W - dw) / 2; oy = H - dh
    }
    var art = [Int](repeating: -1, count: W * H)   // palette index per art pixel, -1 transparent
    let gain = Double(ProcessInfo.processInfo.environment["PIX_GAIN"] ?? "1")!   // darkens (<1) the source before it meets the palette
    var votes = [Int](repeating: 0, count: pal.count)
    for ay in 0..<dh { for ax in 0..<dw {
        let sx0 = x0 + ax * (x1 - x0 + 1) / dw, sx1 = max(sx0 + 1, x0 + (ax + 1) * (x1 - x0 + 1) / dw)
        let sy0 = y0 + ay * (y1 - y0 + 1) / dh, sy1 = max(sy0 + 1, y0 + (ay + 1) * (y1 - y0 + 1) / dh)
        for i in votes.indices { votes[i] = 0 }
        var solid = 0
        for y in sy0..<sy1 { for x in sx0..<sx1 where opaque(src, x, y) {
            let i = (y * src.w + x) * 4
            votes[nearest(pal, Int(Double(src.px[i]) * gain), Int(Double(src.px[i + 1]) * gain), Int(Double(src.px[i + 2]) * gain))] += 1; solid += 1
        } }
        if solid * 2 >= (sy1 - sy0) * (sx1 - sx0) { art[(oy + ay) * W + ox + ax] = votes.indices.max { votes[$0] < votes[$1] }! }
    } }
    if a[1] == "tiles" {
        // pix tiles in out W H pal cellW cellH shift clean [crop]: stones or bricks of cellW×cellH art px, odd rows shifted by `shift`.
        // The generator's joints never land exactly on the grid, so each cell gets a one-pixel joint on its right and bottom edge and
        // leftover joint-dark pixels within `clean` px of its edge take the cell's commonest colour. Cracks further inside stay.
        // A negative `clean` also bevels each cell (and cleans |clean| px).
        let cw = Int(a[7])!, ch = Int(a[8])!, shift = Int(a[9])!, clean = abs(Int(a[10])!), bevel = Int(a[10])! < 0
        func lum(_ i: Int) -> Int { 2 * pal[i][0] + 5 * pal[i][1] + pal[i][2] }
        func step(_ i: Int, _ way: Int) -> Int {   // the palette colour closest to i that is lighter (way 1) or darker (way -1)
            // same hue first: differences in (r-g) and (b-g) cost far more than a difference in brightness
            let far = { (j: Int) -> Int in
                let d = (0..<3).map { pal[j][$0] - pal[i][$0] }
                return d[1] * d[1] + 8 * ((d[0] - d[1]) * (d[0] - d[1]) + (d[2] - d[1]) * (d[2] - d[1]))
            }
            return pal.indices.filter { (lum($0) - lum(i)) * way > 60 }.min { far($0) < far($1) } ?? i
        }
        var used = [Int](repeating: 0, count: pal.count)
        for i in art { used[i] += 1 }
        let joint = pal.indices.filter { used[$0] * 200 > art.count }.min { lum($0) < lum($1) }!   // darkest colour covering over 0.5%
        for row in 0..<(H / ch) {
            let lead = row % 2 == 1 ? shift : 0
            var cx = lead - cw
            while cx < W {
                let xs = (max(0, cx)..<min(W, cx + cw)), ys = (row * ch)..<((row + 1) * ch)
                cx += cw
                if xs.isEmpty { continue }
                for i in votes.indices { votes[i] = 0 }
                for y in ys { for x in xs { votes[art[y * W + x]] += 1 } }
                let base = votes.indices.max { votes[$0] < votes[$1] }!
                for y in ys { for x in xs {
                    let fromLeft = (x - (cx - cw) + cw) % cw, fromTop = y - ys.lowerBound   // position inside the full (maybe wrapped) cell
                    let edge = min(fromLeft, fromTop, cw - 2 - fromLeft, ch - 2 - fromTop)
                    if fromLeft == cw - 1 || fromTop == ch - 1 { art[y * W + x] = joint }
                    else if edge < clean, lum(art[y * W + x]) * 10 < lum(base) * 8 { art[y * W + x] = base }
                    if bevel, art[y * W + x] == base {   // carved look: lit top and left edge, shaded bottom and right
                        if fromTop == 0 || fromLeft == 0 { art[y * W + x] = step(base, 1) }
                        else if fromTop == ch - 2 || fromLeft == cw - 2 { art[y * W + x] = step(base, -1) }
                    }
                } }
            }
        }
    }
    var out = Bitmap(w: W * 2, h: H * 2, px: [UInt8](repeating: 0, count: W * H * 16))
    for i in 0..<(W * H) where art[i] >= 0 {
        for py in 0..<2 { for px in 0..<2 {
            let o = ((i / W * 2 + py) * out.w + i % W * 2 + px) * 4
            for k in 0..<3 { out.px[o + k] = UInt8(pal[art[i]][k]) }
            out.px[o + 3] = 255
        } }
    }
    save(out, a[3])
    print("\(a[3]): \(dw)x\(dh) art px in \(W)x\(H), from source \(x0),\(y0) \(x1 - x0 + 1)x\(y1 - y0 + 1) (\(String(format: "%.2f", Double(x1 - x0 + 1) / Double(dw))) source px per art px)")

case "slice9":
    // pix slice9 in out W H l t r b : a 2-px-per-art-pixel PNG rebuilt at W×H art px. Corners stay, edges and centre repeat
    // (not stretch), so planks keep their spacing on a longer deck. Borders are in art px.
    let W = Int(a[4])!, H = Int(a[5])!, l = Int(a[6])!, t = Int(a[7])!, r = Int(a[8])!, b = Int(a[9])!
    let iw = src.w / 2, ih = src.h / 2
    func pick(_ o: Int, _ size: Int, _ inSize: Int, _ lo: Int, _ hi: Int) -> Int {
        if o < lo { return o }
        if o >= size - hi { return inSize - (size - o) }
        return lo + (o - lo) % max(1, inSize - lo - hi)
    }
    var out = Bitmap(w: W * 2, h: H * 2, px: [UInt8](repeating: 0, count: W * H * 16))
    for oy in 0..<H { for ox in 0..<W {
        let sx = pick(ox, W, iw, l, r), sy = pick(oy, H, ih, t, b)
        for py in 0..<2 { for px in 0..<2 { for k in 0..<4 {
            out.px[((oy * 2 + py) * out.w + ox * 2 + px) * 4 + k] = src.px[((sy * 2 + py) * src.w + sx * 2 + px) * 4 + k]
        } } }
    } }
    save(out, a[3])

case "sway":
    // pix sway in out rows shift : the top `rows` art rows moved sideways by `shift` art px (a flame tip leaning a little).
    let rows = Int(a[4])!, shift = Int(a[5])!
    var out = Bitmap(w: src.w, h: src.h, px: [UInt8](repeating: 0, count: src.px.count))
    for y in 0..<src.h { for x in 0..<src.w {
        let sx = y < rows * 2 ? x - shift * 2 : x
        guard sx >= 0, sx < src.w else { continue }
        for k in 0..<4 { out.px[(y * src.w + x) * 4 + k] = src.px[(y * src.w + sx) * 4 + k] }
    } }
    save(out, a[3])

case "mirrorx":
    // pix mirrorx in out : the left half copied, mirrored, over the right half (a double door whose two handles came out different)
    var out = src
    for y in 0..<src.h { for x in (src.w / 2)..<src.w { for k in 0..<4 { out.px[(y * src.w + x) * 4 + k] = src.px[(y * src.w + src.w - 1 - x) * 4 + k] } } }
    save(out, a[3])

case "white":
    // pix white in out : every opaque pixel white (a hit-flash silhouette drawn over the sprite)
    var out = src
    for i in stride(from: 0, to: out.px.count, by: 4) where out.px[i + 3] > 128 { out.px[i] = 255; out.px[i + 1] = 255; out.px[i + 2] = 255 }
    save(out, a[3])

default:
    preconditionFailure("unknown mode \(a[1])")
}
