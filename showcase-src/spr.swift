// Sprite prep for generated art. Usage:
//   spr fit  in out W H [bottom]      trim transparent margin, fit inside W×H keeping aspect
//   spr rows in out W H y0 y1         trim, keep rows y0..y1 (fractions from top), stretch to W×H
//   spr tile in out W H mirrorX mirrorY   centre-crop to W:H, shrink, mirror so the edges repeat
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func load(_ path: String) -> CGImage {
    let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil)!
    return CGImageSourceCreateImageAtIndex(source, 0, nil)!
}

func canvas(_ w: Int, _ h: Int) -> CGContext {
    let c = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
                      space: CGColorSpace(name: CGColorSpace.sRGB)!,
                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    c.interpolationQuality = .high
    return c
}

func save(_ c: CGContext, _ path: String) {
    let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(d, c.makeImage()!, nil)
    precondition(CGImageDestinationFinalize(d), "cannot write \(path)")
}

// Bitmap memory and CGImage.cropping both count rows from the top.
func trim(_ image: CGImage) -> CGImage {
    let w = image.width, h = image.height
    let c = canvas(w, h)
    c.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    let px = c.data!.bindMemory(to: UInt8.self, capacity: w * h * 4)
    var x0 = w, y0 = h, x1 = -1, y1 = -1
    for y in 0..<h {
        for x in 0..<w where px[(y * w + x) * 4 + 3] > 16 {
            x0 = min(x0, x); x1 = max(x1, x); y0 = min(y0, y); y1 = max(y1, y)
        }
    }
    precondition(x1 >= 0, "image is fully transparent")
    return image.cropping(to: CGRect(x: x0, y: y0, width: x1 - x0 + 1, height: y1 - y0 + 1))!
}

let a = CommandLine.arguments
precondition(a.count >= 6, "usage: spr fit|rows|tile in out W H ...")
let source = load(a[2]), w = Int(a[4])!, h = Int(a[5])!
switch a[1] {
case "fit":
    let t = trim(source)
    let scale = min(Double(w) / Double(t.width), Double(h) / Double(t.height))
    let dw = Double(t.width) * scale, dh = Double(t.height) * scale
    let c = canvas(w, h)
    c.draw(t, in: CGRect(x: (Double(w) - dw) / 2, y: a.count > 6 ? 0 : (Double(h) - dh) / 2, width: dw, height: dh))
    save(c, a[3])
case "rows":
    let t = trim(source), th = Double(t.height)
    let y0 = (Double(a[6])! * th).rounded(), y1 = (Double(a[7])! * th).rounded()
    let c = canvas(w, h)
    c.draw(t.cropping(to: CGRect(x: 0, y: y0, width: Double(t.width), height: y1 - y0))!, in: CGRect(x: 0, y: 0, width: w, height: h))
    save(c, a[3])
case "tile":
    let mx = a[6] == "1", my = a[7] == "1"
    let sw = Double(source.width), sh = Double(source.height)
    let cw = min(sw, sh * Double(w) / Double(h)), ch = cw * Double(h) / Double(w)
    let cell = source.cropping(to: CGRect(x: (sw - cw) / 2, y: (sh - ch) / 2, width: cw, height: ch))!
    let c = canvas(mx ? w * 2 : w, my ? h * 2 : h)
    for flipX in mx ? [false, true] : [false] {
        for flipY in my ? [false, true] : [false] {
            c.saveGState()
            c.translateBy(x: flipX ? Double(w * 2) : 0, y: flipY ? Double(h * 2) : 0)
            c.scaleBy(x: flipX ? -1 : 1, y: flipY ? -1 : 1)
            c.draw(cell, in: CGRect(x: 0, y: 0, width: w, height: h))
            c.restoreGState()
        }
    }
    save(c, a[3])
case "strip":
    // spr strip in out W H cellH [skip] : repeat a W×cellH cell down to H, flipping every other cell; skip = rows cut from the top
    let cellH = Int(a[6])!, skip = a.count > 7 ? Int(a[7])! : 0
    let sw = Double(source.width), sh = Double(source.height)
    let cw = min(sw, sh * Double(w) / Double(cellH)), ch = cw * Double(cellH) / Double(w)
    let cell = source.cropping(to: CGRect(x: (sw - cw) / 2, y: (sh - ch) / 2, width: cw, height: ch))!
    let c = canvas(w, h)
    var top = -skip, flip = false
    while top < h {
        c.saveGState()
        c.translateBy(x: 0, y: Double(h - top - (flip ? 0 : cellH)))   // CG y grows upward
        c.scaleBy(x: 1, y: flip ? -1 : 1)
        c.draw(cell, in: CGRect(x: 0, y: 0, width: w, height: cellH))
        c.restoreGState()
        top += cellH; flip.toggle()
    }
    save(c, a[3])
case "extend":
    // spr extend in out W H : W×H canvas with the image at the top; rows below it repeat the image's last row
    let c = canvas(w, h), ih = source.height
    c.draw(source.cropping(to: CGRect(x: 0, y: ih - 1, width: source.width, height: 1))!, in: CGRect(x: 0, y: 0, width: w, height: h - ih + 1))
    c.draw(source, in: CGRect(x: 0, y: h - ih, width: w, height: ih))
    save(c, a[3])
case "clearrows":
    // spr clearrows in out W H n : same image with its last n rows made transparent (stops a tiled layer's bottom row bleeding into its top edge)
    let n = Int(a[6])!, c = canvas(w, h)
    c.draw(source.cropping(to: CGRect(x: 0, y: 0, width: w, height: h - n))!, in: CGRect(x: 0, y: n, width: w, height: h - n))
    save(c, a[3])
case "hstrip":
    // spr hstrip in out W H cellW : repeat an H-tall cell across W, mirroring every other cell
    let cellW = Int(a[6])!
    let sw = Double(source.width), sh = Double(source.height)
    let ch = min(sh, sw * Double(h) / Double(cellW)), cw = ch * Double(cellW) / Double(h)
    let cell = source.cropping(to: CGRect(x: (sw - cw) / 2, y: (sh - ch) / 2, width: cw, height: ch))!
    let c = canvas(w, h)
    var left = 0, flip = false
    while left < w {
        c.saveGState()
        c.translateBy(x: Double(left + (flip ? cellW : 0)), y: 0)
        c.scaleBy(x: flip ? -1 : 1, y: 1)
        c.draw(cell, in: CGRect(x: 0, y: 0, width: cellW, height: h))
        c.restoreGState()
        left += cellW; flip.toggle()
    }
    save(c, a[3])
case "auto", "block", "slope":
    // Terrain kit from one fill texture. p = one art pixel in output pixels (a[6]).
    //   auto  in out T T p        4×4 sheet of T-px tiles in edge-mask order N1 E2 S4 W8 (bit set = neighbour connects)
    //   block in out W H p grass  one W×H block; grass 1 = green top, 0 = outline only
    //   slope in out W H p        right triangle rising to the right with a green top edge
    let p = Double(a[6])!
    let dark = CGColor(srgbRed: 0.18, green: 0.13, blue: 0.10, alpha: 1)
    let grassLight = CGColor(srgbRed: 0.55, green: 0.76, blue: 0.30, alpha: 1), grassMid = CGColor(srgbRed: 0.36, green: 0.58, blue: 0.20, alpha: 1)
    let side = Int(64 * p)   // fill pattern: the source centre shrunk so its art pixels are about p wide
    let pat = canvas(side, side); pat.draw(source, in: CGRect(x: 0, y: 0, width: side, height: side))
    let fill = pat.makeImage()!
    // All rectangles below are given top-down; CG counts from the bottom.
    func piece(_ c: CGContext, _ H: Int, _ x: Double, _ y: Double, _ bw: Double, _ bh: Double, n: Bool, e: Bool, s: Bool, w: Bool, grass: Bool) {
        func rect(_ rx: Double, _ ry: Double, _ rw: Double, _ rh: Double) -> CGRect { CGRect(x: x + rx, y: Double(H) - y - ry - rh, width: rw, height: rh) }
        c.saveGState(); c.clip(to: rect(0, 0, bw, bh))
        var ty = 0.0
        while ty < bh { var tx = 0.0; while tx < bw { c.draw(fill, in: rect(tx, ty, Double(side), Double(side))); tx += Double(side) }; ty += Double(side) }
        if !n {
            if grass {   // a thick turf: 5 art px with a ragged lower edge, the top 2 lit
                c.setFillColor(grassMid); c.fill(rect(0, 0, bw, 5 * p))
                var gx = 0.0, k = 0; while gx < bw { if k % 3 != 1 { c.fill(rect(gx, 5 * p, p, p)) }; if k % 5 == 2 { c.fill(rect(gx, 6 * p, p, p)) }; gx += p; k += 1 }
                c.setFillColor(grassLight); c.fill(rect(0, 0, bw, 2 * p))
            }
            else { c.setFillColor(dark); c.fill(rect(0, 0, bw, p)) }
        }
        c.setFillColor(dark)
        if !e { c.fill(rect(bw - p, 0, p, bh)) }
        if !w { c.fill(rect(0, 0, p, bh)) }
        if !s { c.fill(rect(0, bh - p, bw, p)) }
        c.restoreGState()
    }
    if a[1] == "auto" {
        let c = canvas(w * 4, h * 4)
        for mask in 0..<16 {
            piece(c, h * 4, Double(mask % 4 * w), Double(mask / 4 * h), Double(w), Double(h),
                  n: mask & 1 != 0, e: mask & 2 != 0, s: mask & 4 != 0, w: mask & 8 != 0, grass: true)
        }
        save(c, a[3])
    } else if a[1] == "block" {
        let c = canvas(w, h)
        let mask = a.count > 8 ? Int(a[8])! : 0   // sides that continue into a neighbour, same bits as auto
        piece(c, h, 0, 0, Double(w), Double(h), n: mask & 1 != 0, e: mask & 2 != 0, s: mask & 4 != 0, w: mask & 8 != 0, grass: a[7] == "1")
        save(c, a[3])
    } else {
        let c = canvas(w, h), W = Double(w), H = Double(h)
        let tri = CGMutablePath(); tri.addLines(between: [CGPoint(x: 0, y: 0), CGPoint(x: W, y: 0), CGPoint(x: W, y: H)]); tri.closeSubpath()
        c.addPath(tri); c.clip()
        piece(c, h, 0, 0, W, H, n: true, e: true, s: true, w: true, grass: false)
        for (color, drop) in [(grassMid, 6 * p), (grassLight, 2 * p)] {   // bands hang below the rising edge
            let band = CGMutablePath()
            band.addLines(between: [CGPoint(x: 0, y: 0), CGPoint(x: W, y: H), CGPoint(x: W, y: H - drop), CGPoint(x: 0, y: -drop)]); band.closeSubpath()
            c.addPath(band); c.setFillColor(color); c.fillPath()
        }
        save(c, a[3])
    }
default:
    preconditionFailure("unknown mode \(a[1])")
}
