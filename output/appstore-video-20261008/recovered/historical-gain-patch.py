from pathlib import Path
p=Path('/Users/daisei/PocketStriker/output/appstore-video-20261008/recovered/combined_preview_export.swift');s=p.read_text().replace('effectsVolume.setVolume(1, at: .zero)','effectsVolume.setVolume(0.5, at: .zero)').replace('"loops": 0,','"loops": 0, "recordedEffectsGain": 0.5,')
p.write_text(s)
