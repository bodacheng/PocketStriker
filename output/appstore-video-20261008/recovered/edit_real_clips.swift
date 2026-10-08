import Foundation
import AVFoundation
import CoreMedia
import CoreGraphics
import AudioToolbox
import VideoToolbox
import CryptoKit

// Edits only declared continuous recordings. No still images, generated audio,
// overlays, speed changes, frame interpolation, or game/project operations.
enum Failure: Error, CustomStringConvertible {
    case message(String)
    var description: String { if case .message(let text) = self { return text }; return "Failure" }
}
func need(_ condition: @autoclosure () -> Bool, _ text: String) throws {
    if !condition() { throw Failure.message(text) }
}
func json(_ value: Any) throws -> Data {
    try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys])
}
func emit(_ value: Any) throws { print(String(data: try json(value), encoding: .utf8)!) }
func progress(_ text: String) { FileHandle.standardError.write(Data((text + "\n").utf8)) }
func hash(_ url: URL) throws -> String {
    let handle = try FileHandle(forReadingFrom: url)
    defer { try? handle.close() }
    var hasher = SHA256()
    while let data = try handle.read(upToCount: 1_048_576), !data.isEmpty { hasher.update(data: data) }
    return hasher.finalize().map { String(format: "%02x", $0) }.joined()
}
struct Identity: Codable {
    let sourceKind: String
    let captureUtc: String
    let appVersion: String
    let build: String?
    let sourceCommit: String?
    let provenance: String
    let continuousRealRecording: Bool
}
struct Clip: Decodable {
    let path: String
    let sourceSha256: String
    let startSeconds: Double
    let durationSeconds: Double
    let purpose: String
    let audioTrackIndex: Int?
    let sourceIdentity: Identity
}
struct Settings: Decodable { let scaleMode: String }
struct EditList: Decodable {
    let schema: String
    let appId: String
    let locale: String
    let settings: Settings
    let clips: [Clip]
}
let width = 886
let height = 1920
let fps: Int32 = 30
let bitrate = 11_000_000
let root = URL(fileURLWithPath: "/tmp/pocketstriker-iphone-preview-20261004").resolvingSymlinksInPath()
func identityJSON(_ identity: Identity) throws -> Any {
    try JSONSerialization.jsonObject(with: JSONEncoder().encode(identity))
}
func matrix(_ t: CGAffineTransform) -> [String: Double] {
    ["a": t.a, "b": t.b, "c": t.c, "d": t.d, "tx": t.tx, "ty": t.ty]
}
func fitted(_ size: CGSize, _ t: CGAffineTransform, mode: String) throws -> (CGAffineTransform, [String: Any]) {
    let bounds = CGRect(origin: .zero, size: size).applying(t).standardized
    try need(bounds.width.isFinite && bounds.height.isFinite && bounds.width > 0 && bounds.height > 0,
             "Invalid displayed source bounds/transform.")
    let sx = Double(width) / bounds.width, sy = Double(height) / bounds.height
    let s = mode == "fit" ? min(sx, sy) : max(sx, sy)
    let x = (Double(width) - bounds.width * s) / 2
    let y = (Double(height) - bounds.height * s) / 2
    // Explicit coefficients avoid ambiguity about transform concatenation order.
    let out = CGAffineTransform(a: t.a * s, b: t.b * s, c: t.c * s, d: t.d * s,
                               tx: (t.tx - bounds.minX) * s + x,
                               ty: (t.ty - bounds.minY) * s + y)
    let description: [String: Any] = [
        "encodedWidth": size.width, "encodedHeight": size.height,
        "preferredTransform": matrix(t), "displayedWidth": bounds.width,
        "displayedHeight": bounds.height, "scaleMode": mode, "uniformScale": s,
        "scalingDirection": s > 1 ? "upscale" : s < 1 ? "downscale" : "unchanged",
        "blackBorderPixelsPerSide": ["horizontal": max(0, x), "vertical": max(0, y)],
        "croppedOutputPixelsPerSide": ["horizontal": max(0, -x), "vertical": max(0, -y)],
        "uiMayBeCropped": mode == "fill" && (x < -0.01 || y < -0.01),
        "outputTransform": matrix(out)]
    return (out, description)
}
struct Prepared {
    let composition: AVMutableComposition
    let videoComposition: AVMutableVideoComposition
    let videoTrack: AVMutableCompositionTrack
    let audioTrack: AVMutableCompositionTrack?
    let duration: CMTime
    let report: [String: Any]
}
func prepare(_ listURL: URL) async throws -> Prepared {
    let list = try JSONDecoder().decode(EditList.self, from: Data(contentsOf: listURL))
    try need(list.schema == "PocketStrikerRealClipEdit-v1", "Unknown edit-list schema.")
    try need(list.appId == "6478905824", "Expected PocketStriker App ID 6478905824.")
    try need(["ja", "en-US", "zh-Hans"].contains(list.locale), "locale must be ja, en-US, or zh-Hans.")
    try need(["fit", "fill"].contains(list.settings.scaleMode), "Explicit scaleMode must be fit (black borders) or fill (crop).")
    try need(!list.clips.isEmpty && list.clips.count <= 12, "Provide 1–12 real video ranges; the empty example is not executable.")
    let total = list.clips.reduce(0.0) { $0 + $1.durationSeconds }
    try need(total.isFinite && total >= 15 && total <= 30, "Sum of actual range durations must be 15–30 seconds.")
    let composition = AVMutableComposition()
    guard let videoTrack = composition.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid) else {
        throw Failure.message("Cannot create composition video track.")
    }
    var audioTrack: AVMutableCompositionTrack?
    var audioPresence: [Bool] = []
    var instructions: [AVMutableVideoCompositionInstruction] = []
    var clips: [[String: Any]] = []
    var cursor = CMTime.zero
    var rangesByHash: [String: [(Double, Double)]] = [:]
    var hashes: [String: String] = [:]
    for (index, clip) in list.clips.enumerated() {
        try need(clip.startSeconds.isFinite && clip.startSeconds >= 0 && clip.durationSeconds.isFinite && clip.durationSeconds > 0,
                 "Clip \(index + 1) requires a finite nonnegative start and positive duration.")
        let identity = clip.sourceIdentity
        try need(["ios-device", "quicktime-iphone-mirror", "unity-gameview-candidate"].contains(identity.sourceKind), "Unsupported recording sourceKind.")
        try need(identity.continuousRealRecording && !identity.provenance.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                 "Every clip must identify a genuine continuous recording and its provenance.")
        try need(!identity.captureUtc.isEmpty && !identity.appVersion.isEmpty, "Every clip needs captureUtc and appVersion.")
        try need(clip.sourceSha256.range(of: "^[a-f0-9]{64}$", options: .regularExpression) != nil, "Provide the actual lowercase source SHA256.")
        let url = (clip.path.hasPrefix("/") ? URL(fileURLWithPath: clip.path) : listURL.deletingLastPathComponent().appendingPathComponent(clip.path)).resolvingSymlinksInPath()
        try need(["mp4", "mov", "m4v"].contains(url.pathExtension.lowercased()), "Only actual MP4/MOV/M4V recordings are accepted.")
        let digest: String
        if let known = hashes[url.path] { digest = known } else { digest = try hash(url); hashes[url.path] = digest }
        try need(digest == clip.sourceSha256, "Source hash mismatch in clip \(index + 1).")
        let end = clip.startSeconds + clip.durationSeconds
        for (oldStart, oldEnd) in rangesByHash[digest, default: []] {
            try need(end <= oldStart || clip.startSeconds >= oldEnd, "Repeated/overlapping source ranges are rejected; do not loop or pad footage.")
        }
        rangesByHash[digest, default: []].append((clip.startSeconds, end))
        let asset = AVURLAsset(url: url)
        let tracks = try await asset.loadTracks(withMediaType: .video)
        try need(tracks.count == 1, "Each input must have exactly one video track.")
        let sourceVideo = tracks[0]
        let sourceDuration = try await asset.load(.duration)
        let videoRange = try await sourceVideo.load(.timeRange)
        let range = CMTimeRange(start: CMTime(seconds: clip.startSeconds, preferredTimescale: 60_000),
                                duration: CMTime(seconds: clip.durationSeconds, preferredTimescale: 60_000))
        try need(sourceDuration.isNumeric && CMTimeCompare(range.end, sourceDuration) <= 0,
                 "Clip \(index + 1) extends beyond the real asset duration.")
        try need(CMTimeCompare(range.start, videoRange.start) >= 0 && CMTimeCompare(range.end, videoRange.end) <= 0,
                 "Clip \(index + 1) is outside the real video track range.")
        try videoTrack.insertTimeRange(range, of: sourceVideo, at: cursor)
        let size = try await sourceVideo.load(.naturalSize)
        let transform = try await sourceVideo.load(.preferredTransform)
        let (fitting, geometry) = try fitted(size, transform, mode: list.settings.scaleMode)
        let layer = AVMutableVideoCompositionLayerInstruction(assetTrack: videoTrack)
        layer.setTransform(fitting, at: cursor)
        let instruction = AVMutableVideoCompositionInstruction()
        instruction.timeRange = CMTimeRange(start: cursor, duration: range.duration)
        instruction.backgroundColor = CGColor(gray: 0, alpha: 1)
        instruction.layerInstructions = [layer]
        instructions.append(instruction)
        let audio = try await asset.loadTracks(withMediaType: .audio)
        try need(audio.count <= 1 || clip.audioTrackIndex != nil, "Multiple recorded audio tracks require an explicit audioTrackIndex.")
        let selectedAudio = clip.audioTrackIndex ?? 0
        if audio.isEmpty {
            try need(clip.audioTrackIndex == nil, "audioTrackIndex supplied for a clip without audio.")
        } else {
            try need(selectedAudio >= 0 && selectedAudio < audio.count, "audioTrackIndex is outside the source track list.")
            let sourceAudio = audio[selectedAudio]
            let audioRange = try await sourceAudio.load(.timeRange)
            try need(CMTimeCompare(range.start, audioRange.start) >= 0 && CMTimeCompare(range.end, audioRange.end) <= 0,
                     "Recorded audio does not cover the full selected range; choose a covered range, no silent padding is generated.")
            if audioTrack == nil { audioTrack = composition.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid) }
            guard let outputAudio = audioTrack else { throw Failure.message("Cannot create recorded audio composition track.") }
            try outputAudio.insertTimeRange(range, of: sourceAudio, at: cursor)
        }
        audioPresence.append(!audio.isEmpty)
        clips.append(["index": index + 1, "path": url.path, "sourceSha256": digest,
                      "sourceStartSeconds": clip.startSeconds, "durationSeconds": clip.durationSeconds,
                      "outputStartSeconds": cursor.seconds, "purpose": clip.purpose,
                      "sourceIdentity": try identityJSON(identity), "geometry": geometry,
                      "recordedAudioPresent": !audio.isEmpty,
                      "selectedAudioTrackIndex": audio.isEmpty ? NSNull() : selectedAudio])
        cursor = CMTimeAdd(cursor, range.duration)
    }
    try need(audioPresence.allSatisfy { $0 } || audioPresence.allSatisfy { !$0 },
             "Mixed audio/no-audio ranges are refused: supply consistently recorded audio or consistently silent source clips; no fabricated padding.")
    let videoComposition = AVMutableVideoComposition()
    videoComposition.renderSize = CGSize(width: width, height: height)
    videoComposition.frameDuration = CMTime(value: 1, timescale: fps)
    videoComposition.instructions = instructions
    let report: [String: Any] = ["schema": "PocketStrikerRealClipEncoding-v1", "appId": list.appId,
        "locale": list.locale, "editListSha256": try hash(listURL), "durationSeconds": cursor.seconds,
        "sourceIdentityStatus": "declared; device/build identity still requires operator verification",
        "containsUnityCandidate": list.clips.contains { $0.sourceIdentity.sourceKind == "unity-gameview-candidate" },
        "clips": clips, "outputWidth": width, "outputHeight": height, "requestedFPS": fps,
        "requestedCodec": "H.264 High Level 4.0", "targetVideoBitrate": bitrate,
        "scaleMode": list.settings.scaleMode, "playbackSpeedMultiplier": 1,
        "audioIncluded": audioTrack != nil, "audioPolicy": "only original recorded track; no generated audio",
        "requestedAudio": audioTrack == nil ? "none" : "AAC stereo 256000bps 48000Hz",
        "frameRateConversion": "render at 30fps without changing duration; no motion interpolation",
        "noOverlaysOrBlur": true, "mediaGeneratedDuringValidation": false,
        "actualEncodingVerified": false, "reviewReady": false,
        "remainingChecks": "Probe actual output codec/profile/level/FPS/audio and duration; visually review playback, UI geometry, language, source identity, version/resource match and ASC acceptance."]
    return Prepared(composition: composition, videoComposition: videoComposition, videoTrack: videoTrack,
                    audioTrack: audioTrack, duration: cursor, report: report)
}
func ready(_ input: AVAssetWriterInput, _ writer: AVAssetWriter) async throws {
    let since = Date()
    while !input.isReadyForMoreMediaData {
        try Task.checkCancellation()
        try need(writer.status != .failed && writer.status != .cancelled, "Writer stopped: \(writer.error?.localizedDescription ?? "unknown")")
        try need(Date().timeIntervalSince(since) < 30, "Writer input blocked for 30 seconds.")
        try await Task.sleep(nanoseconds: 3_000_000)
    }
}
func encode(_ listURL: URL, _ outputURL: URL) async throws {
    let output = outputURL.resolvingSymlinksInPath()
    try need(output.path.hasPrefix(root.path + "/") && !output.path.hasPrefix(root.appendingPathComponent("project").path + "/"),
             "Output must be inside the authorized preview /tmp folder, outside its project checkout.")
    try need(output.pathExtension.lowercased() == "mp4", "Use a new .mp4 output filename.")
    let reportURL = output.appendingPathExtension("encoding.json")
    try need(!FileManager.default.fileExists(atPath: output.path) && !FileManager.default.fileExists(atPath: reportURL.path),
             "Output/report already exists; originals are never overwritten.")
    let prepared = try await prepare(listURL)
    let writer = try AVAssetWriter(outputURL: output, fileType: .mp4)
    writer.shouldOptimizeForNetworkUse = true
    let video = AVAssetWriterInput(mediaType: .video, outputSettings: [
        AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: width, AVVideoHeightKey: height,
        AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: bitrate,
            AVVideoProfileLevelKey: AVVideoProfileLevelH264High40,
            AVVideoExpectedSourceFrameRateKey: fps, AVVideoMaxKeyFrameIntervalKey: fps * 2,
            AVVideoAllowFrameReorderingKey: false]])
    video.expectsMediaDataInRealTime = false
    try need(writer.canAdd(video), "Requested H.264 writer settings unavailable.")
    writer.add(video)
    let reader = try AVAssetReader(asset: prepared.composition)
    reader.timeRange = CMTimeRange(start: .zero, duration: prepared.duration)
    let frames = AVAssetReaderVideoCompositionOutput(videoTracks: [prepared.videoTrack],
        videoSettings: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
    frames.videoComposition = prepared.videoComposition
    frames.alwaysCopiesSampleData = false
    try need(reader.canAdd(frames), "Cannot read composed genuine video frames.")
    reader.add(frames)
    var audioOutput: AVAssetReaderAudioMixOutput?
    var audioInput: AVAssetWriterInput?
    if let track = prepared.audioTrack {
        let decoded = AVAssetReaderAudioMixOutput(audioTracks: [track], audioSettings: [
            AVFormatIDKey: kAudioFormatLinearPCM, AVSampleRateKey: 48_000,
            AVNumberOfChannelsKey: 2, AVLinearPCMBitDepthKey: 16,
            AVLinearPCMIsFloatKey: false, AVLinearPCMIsNonInterleaved: false,
            AVLinearPCMIsBigEndianKey: false])
        decoded.alwaysCopiesSampleData = false
        try need(reader.canAdd(decoded), "Cannot decode original recorded audio.")
        reader.add(decoded)
        let input = AVAssetWriterInput(mediaType: .audio, outputSettings: [
            AVFormatIDKey: kAudioFormatMPEG4AAC, AVSampleRateKey: 48_000,
            AVNumberOfChannelsKey: 2, AVEncoderBitRateKey: 256_000])
        input.expectsMediaDataInRealTime = false
        try need(writer.canAdd(input), "Cannot add AAC stereo 256kbps audio input.")
        writer.add(input); audioOutput = decoded; audioInput = input
    }
    try need(writer.startWriting(), "Writer start failed: \(writer.error?.localizedDescription ?? "unknown")")
    writer.startSession(atSourceTime: .zero)
    try need(reader.startReading(), "Reader start failed: \(reader.error?.localizedDescription ?? "unknown")")
    do {
        try await withThrowingTaskGroup(of: Void.self) { group in
            group.addTask {
                var count = 0
                while let sample = frames.copyNextSampleBuffer() {
                    try Task.checkCancellation()
                    try await ready(video, writer)
                    try need(video.append(sample), "Video append failed: \(writer.error?.localizedDescription ?? "unknown")")
                    count += 1
                    if count % 60 == 0 { progress("Encoded \(count) genuine composited video samples") }
                }
                try need(count > 0, "No actual video samples decoded.")
                video.markAsFinished()
            }
            if let decoded = audioOutput, let input = audioInput {
                group.addTask {
                    var count = 0
                    while let sample = decoded.copyNextSampleBuffer() {
                        try Task.checkCancellation()
                        try await ready(input, writer)
                        try need(input.append(sample), "Recorded audio append failed: \(writer.error?.localizedDescription ?? "unknown")")
                        count += 1
                    }
                    try need(count > 0, "Declared recorded audio yielded no real audio samples.")
                    input.markAsFinished()
                }
            }
            try await group.waitForAll()
        }
        try need(reader.status == .completed, "Media decoding did not complete: \(reader.error?.localizedDescription ?? "unknown")")
        writer.endSession(atSourceTime: prepared.duration)
        await writer.finishWriting()
        try need(writer.status == .completed, "Media encoding did not complete: \(writer.error?.localizedDescription ?? "unknown")")
    } catch {
        reader.cancelReading(); writer.cancelWriting()
        throw error
    }
    let bytes = (try FileManager.default.attributesOfItem(atPath: output.path)[.size] as? NSNumber)?.int64Value ?? 0
    try need(bytes > 0 && bytes <= 500_000_000, "Output empty or above 500MB limit.")
    var report = prepared.report
    report["output"] = output.path; report["fileBytes"] = bytes; report["outputSha256"] = try hash(output)
    report["writerCompleted"] = true
    report["mediaGeneratedDuringValidation"] = false
    report["encodedFromRealDeclaredClips"] = true
    report["encodingSettingsAreIntentNotMeasuredOutput"] = true
    try json(report).write(to: reportURL, options: .atomic)
    try emit(report)
}
func capabilities() throws {
    var session: VTCompressionSession?
    let result = VTCompressionSessionCreate(allocator: kCFAllocatorDefault, width: Int32(width), height: Int32(height),
        codecType: kCMVideoCodecType_H264, encoderSpecification: nil, imageBufferAttributes: nil,
        compressedDataAllocator: nil, outputCallback: nil, refcon: nil, compressionSessionOut: &session)
    try need(result == noErr && session != nil, "H.264 encoder capability probe failed: \(result)")
    defer { VTCompressionSessionInvalidate(session!) }
    let profile = VTSessionSetProperty(session!, key: kVTCompressionPropertyKey_ProfileLevel, value: kVTProfileLevel_H264_High_4_0)
    let rate = VTSessionSetProperty(session!, key: kVTCompressionPropertyKey_AverageBitRate, value: NSNumber(value: bitrate))
    let frameRate = VTSessionSetProperty(session!, key: kVTCompressionPropertyKey_ExpectedFrameRate, value: NSNumber(value: fps))
    try need(profile == noErr && rate == noErr && frameRate == noErr, "Encoder settings rejected.")
    try emit(["encoderSessionCreated": true, "requestedSettingsAccepted": true, "mediaGenerated": false,
              "width": width, "height": height, "fps": fps, "profile": "H264 High Level4.0", "targetBitrate": bitrate,
              "actualBitstreamVerified": false])
}
let help = """
Usage:
  edit_real_clips --help
  edit_real_clips --capabilities
  edit_real_clips --validate <ordered-real-clips.json>
  edit_real_clips <ordered-real-clips.json> <new-output.mp4>

Inputs are genuine MP4/MOV/M4V ranges with SHA256 and declared source identity.
Ordered durations must total 15–30s. Output is 886x1920, 30fps, requested H.264
High Level4.0 / 11Mbps. Original recorded audio is requested as AAC stereo
256kbps / 48kHz; absent audio creates no track. Mixed/gapped audio is refused.
Explicit scaleMode fit retains all UI with black borders; fill crops and is
documented. No overlays/blur, source looping, still slides, or speed change.
Validate/capabilities create no media. Actual output still requires probe and QA.
"""
@main struct Main {
    static func main() async {
        do {
            let args = Array(CommandLine.arguments.dropFirst())
            if args == ["--help"] || args.isEmpty { print(help); return }
            if args == ["--capabilities"] { try capabilities(); return }
            if args.count == 2 && args[0] == "--validate" {
                try emit(try await prepare(URL(fileURLWithPath: args[1])).report); return
            }
            try need(args.count == 2 && !args[0].hasPrefix("--"), "Invalid arguments. Use --help.")
            try await encode(URL(fileURLWithPath: args[0]), URL(fileURLWithPath: args[1]))
        } catch {
            FileHandle.standardError.write(Data("ERROR: \(error)\n".utf8)); exit(1)
        }
    }
}
