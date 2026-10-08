from pathlib import Path
p=Path('/Users/daisei/PocketStriker/output/appstore-video-20261008/recovered/audio_signal_probe.swift')
s=p.read_text().replace('if let format = track.formatDescriptions.first as? CMAudioFormatDescription,','if let format = track.formatDescriptions.first,').replace('CMAudioFormatDescriptionGetStreamBasicDescription(format)','CMAudioFormatDescriptionGetStreamBasicDescription(format as! CMAudioFormatDescription)')
p.write_text(s)
