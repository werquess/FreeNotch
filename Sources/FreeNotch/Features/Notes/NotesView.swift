import SwiftUI

struct NotesView: View {
    @Environment(NotesStore.self) private var store
    @State private var selectedID: String?
    @State private var query = ""

    private var filtered: [NotesStore.Note] {
        guard !query.isEmpty else { return store.notes }
        return store.notes.filter {
            $0.title.localizedCaseInsensitiveContains(query) || (store.canRead($0) && $0.body.localizedCaseInsensitiveContains(query))
        }
    }

    private var selected: NotesStore.Note? {
        store.notes.first { $0.id == selectedID } ?? store.notes.first
    }

    var body: some View {
        HStack(spacing: 10) {
            sidebar.frame(width: 180)
            Group {
                if let note = selected {
                    NoteEditor(note: note).id(note.id)
                } else {
                    VStack(spacing: 8) {
                        Image(systemName: "note.text").font(.system(size: 26)).foregroundStyle(.white.opacity(0.4))
                        Text(String(localized: "No notes yet")).font(.system(size: 12)).foregroundStyle(.white.opacity(0.5))
                        Button(String(localized: "New note")) { selectedID = store.create().id }
                            .buttonStyle(.plain)
                            .padding(.horizontal, 12).padding(.vertical, 6)
                            .background(Capsule().fill(.white.opacity(0.12)))
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .islandCard()
        }
    }

    private var sidebar: some View {
        VStack(spacing: 6) {
            HStack(spacing: 6) {
                HStack(spacing: 4) {
                    Image(systemName: "magnifyingglass").foregroundStyle(.white.opacity(0.4))
                    TextField(String(localized: "Search"), text: $query).textFieldStyle(.plain)
                }
                .font(.system(size: 11))
                .padding(.horizontal, 8).padding(.vertical, 5)
                .background(Capsule().fill(.white.opacity(0.08)))

                Button {
                    withAnimation(Motion.tab) { selectedID = store.create().id }
                } label: { Image(systemName: "square.and.pencil") }
                .buttonStyle(IslandButtonStyle(size: 24))
                .help(String(localized: "New note"))
            }
            ScrollView(showsIndicators: false) {
                LazyVStack(spacing: 3) {
                    ForEach(filtered) { note in
                        NoteRow(note: note, selected: note.id == selected?.id)
                            .onTapGesture { selectedID = note.id }
                            .contextMenu {
                                Button(note.pinned ? String(localized: "Unpin") : String(localized: "Pin")) { store.togglePin(note.id) }
                                Button(String(localized: "Delete"), role: .destructive) { store.delete(note.id) }
                            }
                    }
                }
                .animation(Motion.tab, value: filtered.map(\.id))
            }
        }
    }
}

private struct NoteRow: View {
    let note: NotesStore.Note
    let selected: Bool
    @Environment(NotesStore.self) private var store

    var body: some View {
        HStack(spacing: 6) {
            VStack(alignment: .leading, spacing: 2) {
                Text(note.title.isEmpty ? String(localized: "Untitled") : note.title)
                    .font(.system(size: 12, weight: .semibold))
                    .lineLimit(1)
                Text(preview)
                    .font(.system(size: 10))
                    .foregroundStyle(.white.opacity(0.45))
                    .lineLimit(1)
            }
            Spacer(minLength: 0)
            if note.pinned { Image(systemName: "pin.fill").font(.system(size: 9)).foregroundStyle(.orange) }
            if note.isLocked {
                Image(systemName: store.canRead(note) ? "lock.open.fill" : "lock.fill")
                    .font(.system(size: 9))
                    .foregroundStyle(.white.opacity(0.6))
            }
        }
        .padding(.horizontal, 8).padding(.vertical, 6)
        .background(RoundedRectangle(cornerRadius: 9).fill(.white.opacity(selected ? 0.14 : 0.04)))
        .contentShape(Rectangle())
    }

    private var preview: String {
        if note.isLocked && !store.canRead(note) { return String(localized: "Locked") }
        let first = note.body.split(separator: "\n").first.map(String.init) ?? ""
        return first.isEmpty ? note.updated.formatted(date: .abbreviated, time: .shortened) : first
    }
}

private struct NoteEditor: View {
    let note: NotesStore.Note
    @Environment(NotesStore.self) private var store
    @State private var title = ""
    @State private var text = ""
    @State private var password = ""
    @State private var confirm = ""
    @State private var mode: Mode = .edit
    @State private var error: String?

    enum Mode { case edit, setPassword }

    var body: some View {
        Group {
            if !store.canRead(note) {
                unlockView
            } else if mode == .setPassword {
                setPasswordView
            } else {
                editor
            }
        }
        .padding(10)
        .onAppear { title = note.title; text = note.body }
        .onChange(of: note.body) { _, new in if new != text { text = new } }
        .animation(Motion.tab, value: mode)
        .animation(Motion.tab, value: store.canRead(note))
    }

    private var editor: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 6) {
                TextField(String(localized: "Title"), text: $title)
                    .textFieldStyle(.plain)
                    .font(.system(size: 15, weight: .bold))
                    .onChange(of: title) { _, v in store.update(note.id, title: v) }
                Spacer()
                Button { store.togglePin(note.id) } label: {
                    Image(systemName: note.pinned ? "pin.fill" : "pin")
                }
                .buttonStyle(IslandButtonStyle(size: 24))
                .help(note.pinned ? String(localized: "Unpin") : String(localized: "Pin"))

                Menu {
                    if note.isLocked {
                        Button(String(localized: "Remove lock")) { store.removePassword(note.id) }
                        Button(String(localized: "Lock now")) { store.lockAll() }
                    } else {
                        Button(String(localized: "Lock with password…")) { mode = .setPassword }
                    }
                } label: {
                    Image(systemName: note.isLocked ? "lock.fill" : "lock.open")
                }
                .menuStyle(.borderlessButton)
                .menuIndicator(.hidden)
                .frame(width: 24)
                .help(String(localized: "Lock"))

                Button { store.delete(note.id) } label: { Image(systemName: "trash") }
                    .buttonStyle(IslandButtonStyle(size: 24))
                    .help(String(localized: "Delete"))
            }
            .font(.system(size: 11))

            TextEditor(text: $text)
                .font(.system(size: 13))
                .scrollContentBackground(.hidden)
                .scrollIndicators(.never)
                .onChange(of: text) { _, v in store.update(note.id, body: v) }

            Text(String(localized: "Edited \(note.updated.formatted(date: .abbreviated, time: .shortened))"))
                .font(.system(size: 9))
                .foregroundStyle(.white.opacity(0.35))
        }
    }

    private var unlockView: some View {
        VStack(spacing: 10) {
            Image(systemName: "lock.fill").font(.system(size: 26)).foregroundStyle(.white.opacity(0.7))
            Text(note.title.isEmpty ? String(localized: "Locked note") : note.title).font(.system(size: 13, weight: .semibold))
            SecureField(String(localized: "Password"), text: $password)
                .textFieldStyle(.plain)
                .padding(.horizontal, 10).padding(.vertical, 6)
                .background(Capsule().fill(.white.opacity(0.1)))
                .frame(width: 200)
                .onSubmit(unlock)
            if let error { Text(error).font(.system(size: 10)).foregroundStyle(.red) }
            Button(String(localized: "Unlock"), action: unlock)
                .buttonStyle(.plain)
                .padding(.horizontal, 14).padding(.vertical, 5)
                .background(Capsule().fill(.white.opacity(0.15)))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var setPasswordView: some View {
        VStack(spacing: 8) {
            Text(String(localized: "Lock note with a password")).font(.system(size: 13, weight: .semibold))
            Text(String(localized: "If you forget the password, the note can’t be recovered.")).font(.system(size: 10)).foregroundStyle(.white.opacity(0.5))
            SecureField(String(localized: "Password"), text: $password)
                .textFieldStyle(.plain)
                .padding(.horizontal, 10).padding(.vertical, 6)
                .background(Capsule().fill(.white.opacity(0.1)))
                .frame(width: 200)
            SecureField(String(localized: "Confirm password"), text: $confirm)
                .textFieldStyle(.plain)
                .padding(.horizontal, 10).padding(.vertical, 6)
                .background(Capsule().fill(.white.opacity(0.1)))
                .frame(width: 200)
                .onSubmit(setPassword)
            if let error { Text(error).font(.system(size: 10)).foregroundStyle(.red) }
            HStack {
                Button(String(localized: "Cancel")) { mode = .edit; reset() }
                Button(String(localized: "Lock"), action: setPassword)
            }
            .buttonStyle(.plain)
            .padding(.top, 2)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func unlock() {
        do {
            try store.unlock(note.id, password: password)
            text = store.notes.first { $0.id == note.id }?.body ?? ""
            reset()
        } catch {
            self.error = String(localized: "Wrong password")
        }
    }

    private func setPassword() {
        guard !password.isEmpty else { error = String(localized: "Password can’t be empty"); return }
        guard password == confirm else { error = String(localized: "Passwords don’t match"); return }
        try? store.setPassword(note.id, password: password)
        mode = .edit
        reset()
    }

    private func reset() {
        password = ""
        confirm = ""
        error = nil
    }
}
