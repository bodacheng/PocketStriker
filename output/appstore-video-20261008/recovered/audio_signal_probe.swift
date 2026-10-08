import Foundation
import AVFoundation
import CoreMedia
import AudioToolbox

func inspect(_ path: String) throws -> [String: Any] {
    let asset = AVURLAsset(url: URL(fileURLWithPath: path))
    let tracks = asset.tracks(withMediaType: .audio)
    guard let track = tracks.first else {return ["path":path,"audioTrackCount":0,"status":"no-audio"]}
    var sampleRate = 0.0, channels = 0
    if let format = track.formatDescriptions.first,
       let description = CMAudioFormatDescriptionGetStreamBasicDescription(format as! CMAudioFormatDescription) {
        sampleRate = description.pointee.mSampleRate; channels = Int(description.pointee.mChannelsPerFrame)
    }
    let reader = try AVAssetReader(asset:asset)
    let output = AVAssetReaderTrackOutput(track:track, outputSettings:[AVFormatIDKey:kAudioFormatLinearPCM, AVLinearPCMIsFloatKey:true, AVLinearPCMBitDepthKey:32, AVLinearPCMIsNonInterleaved:false])
    reader.add(output); reader.startReading()
    var total = 0, nonzero = 0, sq = 0.0, peak = 0.0
    var counts:[Int:Int] = [:], powers:[Int:Double] = [:], peaks:[Int:Double] = [:]
    var longestZeroFrames = 0, currentZeroFrames = 0
    while let sample = output.copyNextSampleBuffer() {
        guard let block = CMSampleBufferGetDataBuffer(sample) else {continue}
        let length = CMBlockBufferGetDataLength(block)
        var data = Data(count:length)
        data.withUnsafeMutableBytes { raw in _ = CMBlockBufferCopyDataBytes(block,atOffset:0,dataLength:length,destination:raw.baseAddress!) }
        let first = CMSampleBufferGetPresentationTimeStamp(sample).seconds
        data.withUnsafeBytes { raw in
            let values = raw.bindMemory(to:Float.self)
            for frame in stride(from:0,to:values.count,by:max(1,channels)) {
                var framePeak = 0.0
                let second = Int(floor(first + Double(frame/max(1,channels))/max(1,sampleRate)))
                for channel in 0..<max(1,channels) {
                    guard frame+channel < values.count else {continue}
                    let value = Double(values[frame+channel]);let a = abs(value)
                    total += 1;sq += value*value;peak = max(peak,a);framePeak = max(framePeak,a)
                    counts[second,default:0] += 1;powers[second,default:0] += value*value;peaks[second,default:0] = max(peaks[second,default:0],a)
                    if value != 0 {nonzero += 1}
                }
                if framePeak == 0 {currentZeroFrames += 1;longestZeroFrames = max(longestZeroFrames,currentZeroFrames)}else{currentZeroFrames = 0}
            }
        }
    }
    let windows = counts.keys.sorted().map { second -> [String:Any] in
        ["second":second,"sampleValues":counts[second]!,"rms":sqrt(powers[second]!/Double(counts[second]!)),"peak":peaks[second]!] }
    return ["path":path,"audioTrackCount":tracks.count,"sampleRateHz":sampleRate,"channels":channels,"trackDurationSeconds":track.timeRange.duration.seconds,"readerStatus":String(describing:reader.status),"completed":reader.status == .completed,"error":reader.error?.localizedDescription ?? "","sampleValues":total,"nonzeroValues":nonzero,"peak":peak,"rms":total>0 ? sqrt(sq/Double(total)):0,"entirelySilent":total>0 && nonzero == 0,"longestExactlyZeroRunSeconds":Double(longestZeroFrames)/max(1,sampleRate),"perSecond":windows]
}
for path in CommandLine.arguments.dropFirst() {
    do {let result = try inspect(path);let data = try JSONSerialization.data(withJSONObject:result,options:[.prettyPrinted,.sortedKeys]);print(String(data:data,encoding:.utf8)!)}
    catch {print("Audio probe failed: \(error)");exit(1)}
}
