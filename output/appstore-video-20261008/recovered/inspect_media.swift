import Foundation
import AVFoundation
import CoreMedia
import CoreGraphics
import AudioToolbox
import ImageIO
import UniformTypeIdentifiers
import CryptoKit

// Inspect genuine media and extract requested frames. Does not encode or alter video.
enum ProbeError: Error, CustomStringConvertible {
    case message(String)
    var description: String { if case .message(let text) = self { return text }; return "Probe failed" }
}
func require(_ value: Bool, _ message: String) throws {
    if !value { throw ProbeError.message(message) }
}
func code(_ value: FourCharCode) -> String {
    String(bytes: [UInt8((value >> 24) & 255), UInt8((value >> 16) & 255),
                   UInt8((value >> 8) & 255), UInt8(value & 255)], encoding: .ascii) ?? String(value)
}
func sha(_ url: URL) throws -> String {
    let handle = try FileHandle(forReadingFrom: url)
    defer { try? handle.close() }
    var h = SHA256()
    while let bytes = try handle.read(upToCount: 1_048_576), !bytes.isEmpty { h.update(data: bytes) }
    return h.finalize().map { String(format: "%02x", $0) }.joined()
}
func compressedProbe(_ asset: AVAsset, _ track: AVAssetTrack, _ range: CMTimeRange) throws -> [String: Any] {
    let reader = try AVAssetReader(asset: asset)
    let output = AVAssetReaderTrackOutput(track: track, outputSettings: nil)
    output.alwaysCopiesSampleData = false
    try require(reader.canAdd(output), "Cannot read compressed media track.")
    reader.add(output)
    try require(reader.startReading(), "Cannot start compressed media reader.")
    var samples = 0, bytes = 0
    var first: Double?, last: Double?, deltaMin = Double.infinity, deltaMax = 0.0
    while let sample = output.copyNextSampleBuffer() {
        samples += CMSampleBufferGetNumSamples(sample)
        bytes += CMSampleBufferGetTotalSampleSize(sample)
        let time = CMSampleBufferGetPresentationTimeStamp(sample).seconds
        if time.isFinite {
            if first == nil { first = time }
            if let previous = last, time > previous {
                deltaMin = min(deltaMin, time - previous); deltaMax = max(deltaMax, time - previous)
            }
            last = time
        }
    }
    try require(reader.status == .completed, "Compressed reading failed: \(reader.error?.localizedDescription ?? "unknown")")
    var result: [String: Any] = [
        "sampleCount": samples, "compressedPayloadBytes": bytes,
        "trackDurationSeconds": range.duration.seconds,
        "measuredPayloadBitrateBps": range.duration.seconds > 0 ? Double(bytes) * 8 / range.duration.seconds : 0,
        "readerCompleted": true,
        "bitrateBasis": "compressed payload bytes / track duration, excludes container overhead"
    ]
    if let first { result["firstPresentationSeconds"] = first }
    if let last { result["lastPresentationSeconds"] = last }
    if deltaMin.isFinite { result["positiveSampleDeltaMinSeconds"] = deltaMin }
    if deltaMax > 0 { result["positiveSampleDeltaMaxSeconds"] = deltaMax }
    return result
}
func signalProbe(_ asset: AVAsset, _ track: AVAssetTrack, _ range: CMTimeRange) throws -> [String: Any] {
    let reader = try AVAssetReader(asset: asset)
    let output = AVAssetReaderTrackOutput(track: track, outputSettings: [
        AVFormatIDKey: kAudioFormatLinearPCM, AVLinearPCMIsFloatKey: true,
        AVLinearPCMBitDepthKey: 32, AVLinearPCMIsNonInterleaved: false
    ])
    output.alwaysCopiesSampleData = false
    try require(reader.canAdd(output), "Cannot decode PCM audio.")
    reader.add(output)
    try require(reader.startReading(), "Cannot start PCM reader.")
    var values = 0, nonzero = 0, clipValues = 0, zeroFrames = 0, longestZero = 0
    var square = 0.0, peak = 0.0, rate = 0.0, channelCount = 0
    var perSecond: [Int: (Int, Double, Double)] = [:]
    while let sample = output.copyNextSampleBuffer() {
        guard let block = CMSampleBufferGetDataBuffer(sample),
              let format = CMSampleBufferGetFormatDescription(sample),
              let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(format) else {
            throw ProbeError.message("PCM sample has no usable data/format.")
        }
        rate = asbd.pointee.mSampleRate; channelCount = Int(asbd.pointee.mChannelsPerFrame)
        try require(rate > 0 && channelCount > 0, "Invalid decoded audio format.")
        var length = 0
        var pointer: UnsafeMutablePointer<Int8>?
        let status = CMBlockBufferGetDataPointer(block, atOffset: 0, lengthAtOffsetOut: nil,
                                                totalLengthOut: &length, dataPointerOut: &pointer)
        try require(status == kCMBlockBufferNoErr && pointer != nil, "Cannot access decoded audio bytes.")
        let floats = UnsafeRawPointer(pointer!).assumingMemoryBound(to: Float.self)
        let count = length / MemoryLayout<Float>.size
        let time = CMSampleBufferGetPresentationTimeStamp(sample).seconds
        for frame in 0..<(count / channelCount) {
            var allZero = true
            let second = Int(floor(time + Double(frame) / rate))
            var bucket = perSecond[second] ?? (0, 0.0, 0.0)
            for channel in 0..<channelCount {
                let v = Double(floats[frame * channelCount + channel])
                try require(v.isFinite, "Nonfinite PCM sample.")
                values += 1; square += v * v; peak = max(peak, abs(v))
                if v != 0 { nonzero += 1; allZero = false }
                if abs(v) >= 0.999 { clipValues += 1 }
                bucket.0 += 1; bucket.1 += v * v; bucket.2 = max(bucket.2, abs(v))
            }
            perSecond[second] = bucket
            if allZero { zeroFrames += 1; longestZero = max(longestZero, zeroFrames) }
            else { zeroFrames = 0 }
        }
    }
    try require(reader.status == .completed, "PCM decoding failed: \(reader.error?.localizedDescription ?? "unknown")")
    let windows: [[String: Any]] = perSecond.keys.sorted().map { second in
        let b = perSecond[second]!
        return ["second": second, "sampleValues": b.0, "rms": sqrt(b.1 / Double(b.0)), "peak": b.2]
    }
    return [
        "readerCompleted": true, "sampleRateHz": rate, "channels": channelCount,
        "trackDurationSeconds": range.duration.seconds, "sampleValues": values,
        "nonzeroValues": nonzero, "peak": peak, "rms": values > 0 ? sqrt(square / Double(values)) : 0,
        "entirelySilent": values > 0 && nonzero == 0, "nearFullScaleSampleValues": clipValues,
        "longestExactlyZeroRunSeconds": Double(longestZero) / max(1, rate), "perSecond": windows
    ]
}
@main struct Main {
    static func main() async {
        do {
            let args = Array(CommandLine.arguments.dropFirst())
            if args == ["--help"] || args.isEmpty {
                print("Usage: inspect_media <video.mp4> <new-QA-folder> <timesCSV|->\nExample: inspect_media clip.mp4 qa 0.1,4.1,4.3,8.3,8.5,13.5,13.7,17,21,25,28.4\nUse '-' for a metadata/audio-only probe. Refuses to overwrite report or frames.")
                return
            }
            try require(args.count == 3, "Expected video, output folder, and comma-separated frame times (or '-').")
            let source = URL(fileURLWithPath: args[0]).resolvingSymlinksInPath()
            let folder = URL(fileURLWithPath: args[1], isDirectory: true).resolvingSymlinksInPath()
            let reportURL = folder.appendingPathComponent("media-probe.json")
            try require(FileManager.default.fileExists(atPath: source.path), "Source file missing.")
            try require(!FileManager.default.fileExists(atPath: reportURL.path), "Refusing to overwrite existing media-probe.json.")
            let times: [Double]
            if args[2] == "-" { times = [] }
            else {
                let pieces = args[2].split(separator: ",", omittingEmptySubsequences: false)
                try require(pieces.count <= 300, "At most 300 requested frame times.")
                times = try pieces.map { text in
                    guard let value = Double(text.trimmingCharacters(in: .whitespaces)), value.isFinite, value >= 0 else {
                        throw ProbeError.message("Invalid frame time: \(text)")
                    }
                    return value
                }
            }
            let asset = AVURLAsset(url: source)
            let duration = try await asset.load(.duration)
            try require(duration.seconds.isFinite && duration.seconds > 0, "Invalid video duration.")
            try require(times.allSatisfy { $0 < duration.seconds }, "A requested frame time lies outside the actual video.")
            let videos = try await asset.loadTracks(withMediaType: .video)
            let audios = try await asset.loadTracks(withMediaType: .audio)
            var videoReports: [[String: Any]] = [], audioReports: [[String: Any]] = []
            for (index, track) in videos.enumerated() {
                let size = try await track.load(.naturalSize)
                let transform = try await track.load(.preferredTransform)
                let range = try await track.load(.timeRange)
                let rect = CGRect(origin: .zero, size: size).applying(transform)
                let fps = try await track.load(.nominalFrameRate)
                let formats = try await track.load(.formatDescriptions)
                var descriptions: [[String: Any]] = []
                for format in formats {
                    let dims = CMVideoFormatDescriptionGetDimensions(format)
                    var f: [String: Any] = ["codec": code(CMFormatDescriptionGetMediaSubType(format)),
                                            "encodedWidth": dims.width, "encodedHeight": dims.height]
                    if let extensions = CMFormatDescriptionGetExtensions(format),
                       let atoms = (extensions as NSDictionary)[kCMFormatDescriptionExtension_SampleDescriptionExtensionAtoms] as? NSDictionary,
                       let data = atoms["avcC"] as? Data, data.count >= 4 {
                        f["avcConfigurationVersion"] = data[0]
                        f["h264ProfileIDC"] = data[1]; f["h264LevelIDC"] = data[3]
                        f["h264Level"] = Double(data[3]) / 10
                    }
                    descriptions.append(f)
                }
                videoReports.append([
                    "index": index, "nominalFrameRate": fps, "naturalWidth": size.width,
                    "naturalHeight": size.height, "displayWidth": abs(rect.width), "displayHeight": abs(rect.height),
                    "transform": [transform.a, transform.b, transform.c, transform.d, transform.tx, transform.ty],
                    "formats": descriptions, "compressedSamples": try compressedProbe(asset, track, range)
                ])
            }
            for (index, track) in audios.enumerated() {
                let range = try await track.load(.timeRange)
                let formats = try await track.load(.formatDescriptions)
                var descriptions: [[String: Any]] = []
                for format in formats {
                    var f: [String: Any] = ["codec": code(CMFormatDescriptionGetMediaSubType(format))]
                    if let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(format) {
                        f["sampleRateHz"] = asbd.pointee.mSampleRate; f["channels"] = asbd.pointee.mChannelsPerFrame
                    }
                    descriptions.append(f)
                }
                audioReports.append([
                    "index": index, "formats": descriptions,
                    "compressedSamples": try compressedProbe(asset, track, range),
                    "decodedSignal": try signalProbe(asset, track, range)
                ])
            }
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            let generator = AVAssetImageGenerator(asset: asset)
            generator.appliesPreferredTrackTransform = true
            generator.requestedTimeToleranceBefore = .zero; generator.requestedTimeToleranceAfter = .zero
            var frameReports: [[String: Any]] = []
            for (index, second) in times.enumerated() {
                let file = folder.appendingPathComponent(String(format: "frame-%03d-%08.3f.png", index, second))
                try require(!FileManager.default.fileExists(atPath: file.path), "Refusing to overwrite a frame.")
                var actual = CMTime.invalid
                let image = try generator.copyCGImage(at: CMTime(seconds: second, preferredTimescale: 60_000), actualTime: &actual)
                guard let destination = CGImageDestinationCreateWithURL(file as CFURL, UTType.png.identifier as CFString, 1, nil) else {
                    throw ProbeError.message("Cannot create frame PNG.")
                }
                CGImageDestinationAddImage(destination, image, nil)
                try require(CGImageDestinationFinalize(destination), "PNG frame writing failed.")
                frameReports.append(["requestedSeconds": second, "actualSeconds": actual.seconds,
                    "timeDifferenceSeconds": actual.seconds - second, "width": image.width, "height": image.height,
                    "path": file.path, "sha256": try sha(file),
                    "requestedTimeToleranceBeforeSeconds": 0, "requestedTimeToleranceAfterSeconds": 0])
                print(file.path)
            }
            let bytes = (try FileManager.default.attributesOfItem(atPath: source.path)[.size] as? NSNumber)?.int64Value ?? 0
            let report: [String: Any] = [
                "schema": "PocketStrikerMediaProbe-v1", "source": source.path, "sha256": try sha(source),
                "fileBytes": bytes, "durationSeconds": duration.seconds, "videoTracks": videoReports,
                "audioTracks": audioReports, "frames": frameReports,
                "sourceModified": false, "videoEncoded": false,
                "limitations": ["Technical inspection does not confirm gameplay authenticity or store acceptance.",
                    "H.264 profile/level are read from avcC; SPS progressive syntax is not parsed.",
                    "Near-full-scale PCM values are reported as observations, not proof of audible clipping."]
            ]
            let data = try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys])
            try data.write(to: reportURL, options: .atomic)
            print(reportURL.path)
        } catch {
            FileHandle.standardError.write(Data("ERROR: \(error)\n".utf8)); exit(1)
        }
    }
}
