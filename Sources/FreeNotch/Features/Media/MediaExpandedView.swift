import SwiftUI

struct MediaExpandedView: View {
    let ns: Namespace.ID
    @Environment(MediaController.self) private var media
    @AppStorage(Prefs.Key.lyricsEnabled.rawValue) private var lyricsEnabled = true
    @State private var showLyrics = false

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            if media.sessions.count > 1 {
                SessionTabs()
            }
            if let session = media.current {
                if showLyrics, lyricsEnabled {
                    lyricsLayout(session)
                } else {
                    playerLayout(session)
                }
            } else {
                emptyState
            }
        }
        .onAppear { media.beginLiveSync() }
        .onDisappear { media.endLiveSync() }
        .animation(Motion.tab, value: showLyrics)
        .animation(Motion.tab, value: media.sessions.map(\.id))
    }

    private func playerLayout(_ session: MediaSession) -> some View {
        let accent = Color(nsColor: session.accent)
        return GeometryReader { geo in
            let side = min(geo.size.height, 136)
            HStack(spacing: 18) {
                ArtworkView(image: session.artwork, size: side, corner: 18)
                    .matchedGeometryEffect(id: "artwork", in: ns)
                    .overlay {
                        RoundedRectangle(cornerRadius: 18, style: .continuous)
                            .strokeBorder(.white.opacity(0.08), lineWidth: 1)
                    }
                    .shadow(color: accent.opacity(0.5), radius: 22, y: 8)
                    .overlay(alignment: .bottomTrailing) { sourceBadge(session) }

                VStack(alignment: .leading, spacing: 0) {
                    HStack(alignment: .top, spacing: 10) {
                        VStack(alignment: .leading, spacing: 3) {
                            Text(session.title.isEmpty ? String(localized: "Unknown") : session.title)
                                .font(.system(size: 17, weight: .bold))
                                .lineLimit(1)
                            Text(session.artist)
                                .font(.system(size: 13, weight: .medium))
                                .foregroundStyle(.white.opacity(0.6))
                                .lineLimit(1)
                            HStack(spacing: 6) {
                                AudioBarsView(isAnimating: session.isPlaying, color: session.accent.blended(withFraction: 0.35, of: .white) ?? .white)
                                    .frame(width: 14, height: 10)
                                Text(session.album.isEmpty ? session.sourceLabel : session.album)
                                    .lineLimit(1)
                            }
                            .font(.system(size: 11, weight: .medium))
                            .foregroundStyle(Theme.tertiary)
                            .padding(.top, 3)
                        }
                        .contentTransition(.opacity)
                        Spacer(minLength: 0)
                        if lyricsEnabled {
                            Button { showLyrics = true } label: {
                                Image(systemName: "quote.bubble.fill").font(.system(size: 13))
                            }
                            .buttonStyle(IslandButtonStyle(size: 30))
                            .foregroundStyle(.white.opacity(0.75))
                            .help(String(localized: "Lyrics"))
                        }
                    }

                    Spacer(minLength: 6)
                    MediaScrubber(session: session)
                    Spacer(minLength: 4)
                    controls(session)
                }
                .frame(maxHeight: side)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
            .background(alignment: .leading) {
                RadialGradient(colors: [accent.opacity(0.22), .clear], center: .center, startRadius: 0, endRadius: side * 1.3)
                    .frame(width: side * 2.6, height: side * 2.2)
                    .offset(x: -side * 0.4)
                    .allowsHitTesting(false)
            }
        }
        .padding(.top, 2)
        .transition(.opacity)
    }

    private func lyricsLayout(_ session: MediaSession) -> some View {
        HStack(alignment: .top, spacing: 14) {
            VStack(spacing: 8) {
                ArtworkView(image: session.artwork, size: 108, corner: 16)
                    .matchedGeometryEffect(id: "artwork", in: ns)
                    .shadow(color: Color(nsColor: session.accent).opacity(0.4), radius: 14, y: 5)
                    .overlay(alignment: .topLeading) {
                        Button { showLyrics = false } label: {
                            Image(systemName: "chevron.left")
                                .font(.system(size: 10, weight: .bold))
                                .frame(width: 22, height: 22)
                                .background(Circle().fill(.black.opacity(0.55)))
                        }
                        .buttonStyle(.plain)
                        .padding(5)
                        .help(String(localized: "Player"))
                    }
                compactControls(session)
            }
            .frame(width: 112)
            LyricsView(session: session)
        }
        .transition(.opacity)
    }

    private func sourceBadge(_ session: MediaSession) -> some View {
        Image(nsImage: session.appIcon)
            .resizable()
            .frame(width: 22, height: 22)
            .padding(2)
            .background(Circle().fill(.black))
            .offset(x: 7, y: 7)
            .help(session.sourceLabel)
    }

    private func controls(_ s: MediaSession) -> some View {
        HStack(spacing: 26) {
            Button { media.previous(s) } label: {
                Image(systemName: "backward.fill").font(.system(size: 18))
            }
            .buttonStyle(IslandButtonStyle(size: 36))

            Button { media.playPause(s) } label: {
                Image(systemName: s.isPlaying ? "pause.fill" : "play.fill")
                    .font(.system(size: 20, weight: .semibold))
                    .foregroundStyle(.black)
                    .contentTransition(.symbolEffect(.replace))
                    .frame(width: 44, height: 44)
                    .background(Circle().fill(.white))
            }
            .buttonStyle(IslandButtonStyle(size: 46))

            Button { media.next(s) } label: {
                Image(systemName: "forward.fill").font(.system(size: 18))
            }
            .buttonStyle(IslandButtonStyle(size: 36))
        }
        .foregroundStyle(.white.opacity(0.9))
        .frame(maxWidth: .infinity)
    }

    private func compactControls(_ s: MediaSession) -> some View {
        HStack(spacing: 4) {
            Button { media.previous(s) } label: { Image(systemName: "backward.fill") }
                .buttonStyle(IslandButtonStyle(size: 26))
            Button { media.playPause(s) } label: {
                Image(systemName: s.isPlaying ? "pause.fill" : "play.fill")
                    .contentTransition(.symbolEffect(.replace))
            }
            .buttonStyle(IslandButtonStyle(size: 30, filled: true))
            Button { media.next(s) } label: { Image(systemName: "forward.fill") }
                .buttonStyle(IslandButtonStyle(size: 26))
        }
        .font(.system(size: 11))
    }

    private var emptyState: some View {
        VStack(spacing: 10) {
            Image(systemName: "music.note.list")
                .font(.system(size: 26))
                .foregroundStyle(.white.opacity(0.4))
            Text(String(localized: "Nothing is playing"))
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.white.opacity(0.6))
            if let hint = media.browserHint {
                Label(hint.message, systemImage: "exclamationmark.triangle.fill")
                    .font(.system(size: 10))
                    .foregroundStyle(.yellow.opacity(0.85))
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 30)
            }
            HStack(spacing: 10) {
                ForEach([MediaKind.spotify, .music], id: \.bundleID) { kind in
                    Button(kind == .spotify ? String(localized: "Open Spotify") : String(localized: "Open Music")) { media.openApp(kind) }
                        .buttonStyle(.plain)
                        .font(.system(size: 12, weight: .medium))
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(Capsule().fill(.white.opacity(0.1)))
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private struct SessionTabs: View {
    @Environment(MediaController.self) private var media
    @Environment(Appearance.self) private var appearance
    @Namespace private var tabNS

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 4) {
                ForEach(media.sessions) { s in
                    let selected = media.current?.id == s.id
                    Button { withAnimation(Motion.tab) { media.select(s.id) } } label: {
                        HStack(spacing: 5) {
                            Image(nsImage: s.appIcon).resizable().frame(width: 14, height: 14)
                            Text(s.title.isEmpty ? s.sourceLabel : s.title)
                                .font(.system(size: 11, weight: .medium))
                                .lineLimit(1)
                                .frame(maxWidth: 120)
                            if s.isPlaying {
                                AudioBarsView(isAnimating: true, color: s.accent).frame(width: 10, height: 9)
                            }
                        }
                        .padding(.horizontal, 9)
                        .padding(.vertical, 4)
                        .foregroundStyle(selected ? .white : .white.opacity(0.6))
                        .background {
                            if selected {
                                Capsule().fill(appearance.accent.opacity(0.28)).matchedGeometryEffect(id: "session", in: tabNS)
                            }
                        }
                        .contentShape(Capsule())
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .frame(height: 24)
    }
}

struct MediaScrubber: View {
    let session: MediaSession
    @Environment(MediaController.self) private var media
    @State private var dragValue: Double?
    @State private var hovering = false

    private var active: Bool { dragValue != nil || hovering }

    var body: some View {
        let duration = max(session.duration, 1)

        TimelineView(.periodic(from: .now, by: session.isPlaying ? 1 : 3600)) { ctx in
            let current = dragValue ?? session.position(at: ctx.date)
            HStack(spacing: 10) {
                Text(format(current))
                    .frame(minWidth: 34, alignment: .trailing)
                GeometryReader { geo in
                    ZStack(alignment: .leading) {
                        Capsule().fill(.white.opacity(0.14))
                        Capsule()
                            .fill(.white.opacity(active ? 1 : 0.85))
                            .frame(width: geo.size.width * CGFloat(min(current / duration, 1)))
                    }
                    .frame(height: active ? 7 : 4)
                    .frame(maxHeight: .infinity)
                    .contentShape(Rectangle())
                    .gesture(
                        DragGesture(minimumDistance: 0)
                            .onChanged { g in
                                dragValue = min(max(0, g.location.x / geo.size.width), 1) * duration
                            }
                            .onEnded { _ in
                                if let v = dragValue { media.seek(to: v, in: session) }
                                dragValue = nil
                            }
                    )
                    .onHover { hovering = $0 }
                    .animation(.spring(response: 0.25, dampingFraction: 0.8), value: active)
                    .animation(.linear(duration: 1), value: dragValue == nil ? current : 0)
                }
                .frame(height: 14)
                Text(session.duration > 0 ? "-" + format(duration - current) : String(localized: "Live"))
                    .frame(minWidth: 34, alignment: .leading)
            }
            .font(.system(size: 10, weight: .semibold, design: .rounded).monospacedDigit())
            .foregroundStyle(.white.opacity(0.5))
        }
    }

    private func format(_ t: TimeInterval) -> String {
        let s = Int(max(0, t))
        return s >= 3600 ? String(format: "%d:%02d:%02d", s / 3600, (s / 60) % 60, s % 60)
                         : String(format: "%d:%02d", s / 60, s % 60)
    }
}
