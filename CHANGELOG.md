# Changelog

## 1.0.6

- Browser media stays out of the closed notch while you are watching it. It shows up as soon as you switch to another tab or app.
- Much lower idle energy use: browsers are only checked for media while your Mac is playing sound or the media panel is open
- Browser media is picked up right away when playback starts, even after enabling JavaScript from Apple Events
- Fixed the battery icon showing "1…" instead of 100 while charging

## 1.0.5

- Redesigned media player: larger artwork that fills the notch, a soft glow in the album's color, inline progress bar with times, album name and a bold play button
- Permissions now really stick across updates. FreeNotch is signed with a stable certificate, so after granting Accessibility and Input Monitoring one last time on this version, macOS will not ask again.
- The macOS volume and brightness pop-ups are reliably replaced by the FreeNotch HUD
- The brightness HUD now also appears when brightness is changed from Control Center

## 1.0.4

- Permissions now stick: macOS no longer asks again for Bluetooth, Accessibility and media control after every launch or update. After installing this version, grant them one last time.
- Bluetooth access is only requested when the AirPods and headphones notification is turned on

## 1.0.3

- Fixed the play/pause button flipping back and forth after a click
- Fixed album art flashing a placeholder when a song starts, and Spotify sometimes showing the previous song's cover
- Fixed Apple Music songs being detected as changed twice, which showed the song preview twice
- The song preview and the playing view in the closed notch now blend into each other smoothly
- Album art crossfades when the track changes
- Tools: softer hover highlight in each tool's color instead of a white flash
- Lighter, smoother tab transitions
- Fixed a tab staying dimmed after a cancelled drag

## 1.0.2

- New "Buy Me a Coffee" button in Settings › About to support FreeNotch's development
- GitHub Sponsor button and support section in the README

## 1.0.1

- Shelf: drag any file and the whole shelf gathers under the pointer as a pile. Hold ⌘ to drag just one.
- Shelf: real previews for images and PDFs, folders in their Finder color, and a soft glow in each file's color
- Shelf: right-click for Open, Show in Finder, AirDrop and Remove
- Fixed the drop zone saying "0 files" when a file was already on the shelf
- Fixed the notch background lagging behind the shape while opening
- Smoother volume HUD icon, and the volume bar now uses your accent color
- Demo GIFs in the README

## 1.0.0

The first public release of FreeNotch.

- Media: Spotify, Apple Music and browser playback with artwork, controls, scrubbing and per-source tabs
- Synced lyrics from LRCLIB, with an optional live line in the closed notch
- Drop zone that appears while dragging files, with Shelf and AirDrop targets
- Clipboard history with search
- Notes with local storage and optional password encryption
- Pomodoro timer, stopwatch and calendar events
- Volume and brightness HUD that can replace the macOS overlay
- System stats: CPU, memory, temperature and fan speed
- Mirror, Keep Awake and Color Picker tools
- Themes: OLED Black, Graphite, Liquid Glass and Frosted
- Custom accent color, reorderable tabs and adjustable size
- English, Turkish, German, Spanish and French
- Built-in updates
