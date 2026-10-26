# Contributing to FreeNotch

Thanks for helping make FreeNotch better.

## Reporting bugs

Open an issue with:
- your macOS version and Mac model,
- what you expected and what happened instead,
- steps to reproduce, and a screenshot or screen recording if it's visual.

## Development setup

```bash
git clone https://github.com/werquess/FreeNotch.git
cd FreeNotch
swift build
./scripts/build_app.sh --run
```

`.build/debug/FreeNotch --snapshot snaps` renders every notch state to PNG files so you can check layouts without clicking around.

## Project layout

```
Sources/FreeNotch/
├── App/        App delegate and service wiring
├── Core/       Window management, state, preferences, appearance, updates
├── UI/         The notch shape, animations, compact and expanded views, settings
└── Features/   Media, Shelf, Clipboard, Notes, System, Productivity, Tools
Resources/      Info.plist, app icon, translations
Assets/         Logo and README artwork
scripts/        Build and release scripts
```

## Guidelines

- **Battery first.** FreeNotch should use effectively zero CPU while the notch is closed. Prefer system notifications over timers. If you need a timer, give it a generous leeway and only run it while its view is visible. Long-running animations should use Core Animation rather than repeating SwiftUI animations.
- **Keep changes focused.** Small pull requests are easier to review.
- **Match the existing style.** Animation curves live in `UI/Motion.swift`, colors and surfaces in `Core/Appearance.swift` and `UI/Theme.swift`.
- **User-facing text** goes through `String(localized:)` or SwiftUI string literals so it can be translated.

## Translations

Translations live in `Resources/Localization/<language>.lproj/Localizable.strings`. To add a language:

1. Copy an existing `.lproj` folder and rename it (for example `it.lproj`).
2. Translate the values on the right side of each line.
3. Add the language code to `CFBundleLocalizations` in `Resources/Info.plist` and to `AppLanguage` in `UI/SettingsView.swift`.

Fixes to existing translations are just as welcome.
