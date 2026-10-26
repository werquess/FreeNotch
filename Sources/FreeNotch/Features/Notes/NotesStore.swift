import CommonCrypto
import CryptoKit
import Foundation
import Observation

@MainActor
@Observable
final class NotesStore {
    struct Note: Identifiable, Equatable {
        let id: String
        var title: String

        var body: String
        var isLocked: Bool
        var pinned: Bool
        let created: Date
        var updated: Date
    }

    enum LockError: Error { case wrongPassword, emptyPassword }

    private(set) var notes: [Note] = []

    private(set) var unlocked: Set<String> = []

    @ObservationIgnored private let db = NotesDatabase()
    @ObservationIgnored private var keys: [String: (key: SymmetricKey, salt: Data)] = [:]
    @ObservationIgnored private var cipherBodies: [String: (body: Data, salt: Data?)] = [:]
    @ObservationIgnored private var saveTasks: [String: Task<Void, Never>] = [:]

    init() {
        load()
        migrateQuickNote()
    }

    private func load() {
        notes = db.all().map { row in
            if row.locked { cipherBodies[row.id] = (row.body, row.salt) }
            return Note(id: row.id, title: row.title,
                        body: row.locked ? "" : String(decoding: row.body, as: UTF8.self),
                        isLocked: row.locked, pinned: row.pinned, created: row.created, updated: row.updated)
        }
    }

    private func migrateQuickNote() {
        guard let old = Prefs.string(.quickNote), !old.isEmpty else { return }
        let note = create(title: String(localized: "Quick note"))
        update(note.id, body: old)
        Prefs.set(nil, .quickNote)
    }

    @discardableResult
    func create(title: String = "") -> Note {
        let note = Note(id: UUID().uuidString, title: title, body: "", isLocked: false, pinned: false,
                        created: .now, updated: .now)
        notes.insert(note, at: notes.firstIndex { !$0.pinned } ?? notes.count)
        persist(note.id, immediately: true)
        return note
    }

    func update(_ id: String, title: String? = nil, body: String? = nil) {
        guard let i = notes.firstIndex(where: { $0.id == id }), canRead(notes[i]) else { return }
        if let title { notes[i].title = title }
        if let body { notes[i].body = body }
        notes[i].updated = .now
        persist(id)
    }

    func togglePin(_ id: String) {
        guard let i = notes.firstIndex(where: { $0.id == id }) else { return }
        notes[i].pinned.toggle()
        persist(id, immediately: true)
        sort()
    }

    func delete(_ id: String) {
        saveTasks[id]?.cancel()
        notes.removeAll { $0.id == id }
        keys[id] = nil
        cipherBodies[id] = nil
        unlocked.remove(id)
        db.delete(id)
    }

    func canRead(_ note: Note) -> Bool { !note.isLocked || unlocked.contains(note.id) }

    func setPassword(_ id: String, password: String) throws {
        guard !password.isEmpty else { throw LockError.emptyPassword }
        guard let i = notes.firstIndex(where: { $0.id == id }), canRead(notes[i]) else { return }
        let salt = Self.randomSalt()
        keys[id] = (Self.deriveKey(password, salt: salt), salt)
        notes[i].isLocked = true
        unlocked.insert(id)
        persist(id, immediately: true)
    }

    func unlock(_ id: String, password: String) throws {
        guard let cipher = cipherBodies[id], let salt = cipher.salt,
              let i = notes.firstIndex(where: { $0.id == id }) else { return }
        let key = Self.deriveKey(password, salt: salt)
        guard let box = try? AES.GCM.SealedBox(combined: cipher.body),
              let plain = try? AES.GCM.open(box, using: key) else { throw LockError.wrongPassword }
        keys[id] = (key, salt)
        notes[i].body = String(decoding: plain, as: UTF8.self)
        unlocked.insert(id)
    }

    func removePassword(_ id: String) {
        guard let i = notes.firstIndex(where: { $0.id == id }), unlocked.contains(id) else { return }
        notes[i].isLocked = false
        keys[id] = nil
        cipherBodies[id] = nil
        unlocked.remove(id)
        persist(id, immediately: true)
    }

    func lockAll() {
        for id in unlocked {
            saveTasks[id]?.cancel()
            write(id)
            keys[id] = nil
            if let i = notes.firstIndex(where: { $0.id == id }) { notes[i].body = "" }
        }
        unlocked.removeAll()
    }

    private func persist(_ id: String, immediately: Bool = false) {
        saveTasks[id]?.cancel()
        if immediately { write(id); return }
        saveTasks[id] = Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(450))
            guard !Task.isCancelled else { return }
            self?.write(id)
        }
    }

    private func write(_ id: String) {
        guard let note = notes.first(where: { $0.id == id }) else { return }
        var body = Data(note.body.utf8)
        var salt: Data?
        if note.isLocked {
            guard let k = keys[id], let sealed = try? AES.GCM.seal(body, using: k.key).combined else { return }
            body = sealed
            salt = k.salt
            cipherBodies[id] = (sealed, k.salt)
        }
        db.upsert(.init(id: note.id, title: note.title, body: body, locked: note.isLocked, salt: salt,
                        pinned: note.pinned, created: note.created, updated: note.updated))
    }

    private func sort() {
        notes.sort { a, b in a.pinned != b.pinned ? a.pinned : a.updated > b.updated }
    }

    private static func randomSalt() -> Data {
        var bytes = [UInt8](repeating: 0, count: 16)
        _ = SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes)
        return Data(bytes)
    }

    private static func deriveKey(_ password: String, salt: Data) -> SymmetricKey {
        var derived = [UInt8](repeating: 0, count: 32)
        let pw = Array(password.utf8)
        salt.withUnsafeBytes { saltPtr in
            _ = CCKeyDerivationPBKDF(CCPBKDFAlgorithm(kCCPBKDF2),
                                     password, pw.count,
                                     saltPtr.bindMemory(to: UInt8.self).baseAddress, salt.count,
                                     CCPseudoRandomAlgorithm(kCCPRFHmacAlgSHA256), 200_000,
                                     &derived, derived.count)
        }
        return SymmetricKey(data: derived)
    }
}
