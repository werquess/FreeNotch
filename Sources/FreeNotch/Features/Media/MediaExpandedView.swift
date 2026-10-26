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
        HStack(spacing: 18) {
            ArtworkView(image: session.artwork, size: 104, corner: 18)
                .matchedGeometryEffect(id: "artwork", in: ns)
                .shadow(color: Color(nsColor: session.accent).opacity(0.45), radius: 18, y: 6)
                .overlay(alignment: .bottomTrailing) { sourceBadge(session) }

            VStack(alignment: .leading, spacing: 0) {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 3) {
                        Text(session.title.isEmpty ? String(localized: "Unknown") : session.title)
                            .font(.system(size: 16, weight: .semibold))
                            .lineLimit(1)
                        Text(session.artist)
                            .font(.system(size: 13))
                            .foregroundStyle(.white.opacity(0.6))
                            .lineLimit(1)
                    }
                    Spacer()
                    if lyricsEnabled {
                        Button { showLyrics = true } label: {
                            Image(systemName: "quote.bubble.fill").font(.system(size: 13))
                        }
                        .buttonStyle(IslandButtonStyle(size: 28))
                        .help(String(localized: "Lyrics"))
                    }
                    AudioBarsView(isAnimating: session.isPlaying, color: session.accent)
                        .frame(width: 20, height: 16)
                        .padding(.top, 6)
                }

                Spacer(minLength: 6)
                MediaScrubber(session: session)
                Spacer(minLength: 6)
                controls(session)
            }
            .frame(maxHeight: 112)
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
            .frame(width: 24, height: 24)
            .shadow(radius: 3)
            .offset(x: 6, y: 6)
    }

    private func controls(_ s: MediaSession) -> some View {
        HStack(spacing: 22) {
            Spacer()
            Button { media.previous(s) } label: {
                Image(systemName: "backward.fill").font(.system(size: 17))
            }
            .buttonStyle(IslandButtonStyle(size: 34))

            Button { media.playPause(s) } label: {
                Image(systemName: s.isPlaying ? "pause.fill" : "play.fill")
                    .font(.system(size: 24))
                    .contentTransition(.symbolEffect(.replace.downUp))
            }
            .buttonStyle(IslandButtonStyle(size: 42))

            Button { media.next(s) } label: {
                Image(systemName: "forward.fill").font(.system(size: 17))
            }
            .buttonStyle(IslandButtonStyle(size: 34))
            Spacer()
        }
        .foregroundStyle(.white)
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

    var body: some View {
        let duration = max(session.duration, 1)
        let accent = Color(nsColor: session.accent)

        TimelineView(.periodic(from: .now, by: session.isPlaying ? 1 : 3600)) { ctx in
            let current = dragValue ?? session.position(at: ctx.date)
            VStack(spacing: 4) {
                GeometryReader { geo in
                    ZStack(alignment: .leading) {
                        Capsule().fill(.white.opacity(0.15))
                        Capsule()
                            .fill(accent)
                            .frame(width: geo.size.width * CGFloat(min(current / duration, 1)))
                    }
                    .frame(height: dragValue == nil ? 5 : 8)
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
                    .animation(.spring(response: 0.25, dampingFraction: 0.8), value: dragValue == nil)
                    .animation(.linear(duration: 1), value: dragValue == nil ? current : 0)
                }
                .frame(height: 10)

                HStack {
                    Text(format(current))
                    Spacer()
                    Text(session.duration > 0 ? "-" + format(duration - current) : String(localized: "Live"))
                }
                .font(.system(size: 10, weight: .medium, design: .rounded).monospacedDigit())
                .foregroundStyle(.white.opacity(0.45))
            }
        }
    }

    private func format(_ t: TimeInterval) -> String {
        let s = Int(max(0, t))
        return s >= 3600 ? String(format: "%d:%02d:%02d", s / 3600, (s / 60) % 60, s % 60)
                         : String(format: "%d:%02d", s / 60, s % 60)
    }
}
