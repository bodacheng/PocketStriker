# Bundled UI fonts

Noto Sans CJK SC Regular is bundled for the battle tips and progress text so
Chinese/Japanese glyphs are available on devices without an OS fallback.
The complete font also covers future remotely updated tips.

The game's existing Avalon Bold font lives here, outside the editor plugin,
with its original GUID so existing UI references keep their Latin styling.
Its importer explicitly references Noto Sans CJK SC as a bundled fallback.
Legacy built-in and missing font references in runtime UI use Noto directly.
Do not rely on macOS/iOS system fonts for localized character coverage.

Run `PocketStrikerFontValidation.Validate` (or `ValidateBatch`) to check all
localized glyphs, runtime prefab font dependencies and generated text geometry,
including startup messages and the preparation header at phone aspect ratios.

Source: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf
License: SIL Open Font License 1.1; see NotoSansCJK-LICENSE.txt.
