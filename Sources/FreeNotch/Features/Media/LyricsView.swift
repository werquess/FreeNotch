import SwiftUI

struct LyricsView: View {
    let session: MediaSession
    @Environment(LyricsService.self) private var lyrics
    @Environment(MediaController.self) private var media
    @AppStorage(Prefs.Key.lyricsFontSize.rawValue) private var fontSize = 15.0

    var body: some View {
        Group {
            switch lyrics.state {
            case .synced:
                TimelineView(.explicit(lyrics.lineChangeDates(for: session))) { ctx in
                    let index = lyrics.lineIndex(at: session.position(at: ctx.date))
                    syncedList(current: index)
                }
            case .plain:
                ScrollView(showsIndicators: false) {
                    Text(lyrics.plainText ?? "")
                        .font(.system(size: fontSize - 2))
                        .foregroundStyle(.white.opacity(0.8))
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            case .loading, .idle:
                ProgressView().controlSize(.small).frame(maxWidth: .infinity, maxHeight: .infinity)
            case .notFound, .failed:
                Text(lyrics.state == .failed ? String(localized: "Couldn’t load lyrics") : String(localized: "No lyrics found for this song"))
                    .font(.system(size: 12))
                    .foregroundStyle(.white.opacity(0.45))
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
        .onAppear { lyrics.ensure(for: session) }
        .onChange(of: session.trackID) { _, _ in lyrics.ensure(for: session) }
    }

    private func syncedList(current: Int?) -> some View {
        ScrollViewReader { proxy in
            ScrollView(showsIndicators: false) {
                VStack(alignment: .leading, spacing: 8) {
                    ForEach(lyrics.lines) { line in
                        let isCurrent = line.id == current
                        Text(line.text)
                            .font(.system(size: isCurrent ? fontSize + 2 : fontSize, weight: isCurrent ? .bold : .semibold))
                            .foregroundStyle(.white.opacity(isCurrent ? 1 : (line.id < (current ?? 0) ? 0.3 : 0.45)))
                            .blur(radius: isCurrent ? 0 : 0.3)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .id(line.id)
                            .contentShape(Rectangle())
                            .onTapGesture { media.seek(to: line.time, in: session) }
                    }
                }
                .padding(.vertical, 40)
            }
            .mask(LinearGradient(colors: [.clear, .black, .black, .clear], startPoint: .top, endPoint: .bottom))
            .onChange(of: current) { _, new in
                guard let new else { return }
                withAnimation(.spring(response: 0.45, dampingFraction: 0.85)) { proxy.scrollTo(new, anchor: .center) }
            }
            .onAppear {
                if let current { proxy.scrollTo(current, anchor: .center) }
            }
        }
    }
}

struct CompactLyricLine: View {
    let session: MediaSession
    @Environment(LyricsService.self) private var lyrics

    var body: some View {
        TimelineView(.explicit(lyrics.lineChangeDates(for: session))) { ctx in
            let index = lyrics.lineIndex(at: session.position(at: ctx.date))
            Text(index.map { lyrics.lines[$0].text } ?? "♪")
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(Color(nsColor: session.accent))
                .lineLimit(1)
                .contentTransition(.opacity)
                .animation(.easeInOut(duration: 0.25), value: index)
                .frame(maxWidth: .infinity)
                .padding(.horizontal, 18)
        }
        .onAppear { lyrics.ensure(for: session) }
        .onChange(of: session.trackID) { _, _ in lyrics.ensure(for: session) }
    }
}
