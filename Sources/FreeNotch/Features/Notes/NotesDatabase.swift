import Foundation
import SQLite3

final class NotesDatabase {
    struct Row {
        var id: String
        var title: String
        var body: Data
        var locked: Bool
        var salt: Data?
        var pinned: Bool
        var created: Date
        var updated: Date
    }

    static var directory: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let dir = base.appendingPathComponent("FreeNotch", isDirectory: true)
        let legacy = base.appendingPathComponent("DynamicIsland", isDirectory: true)
        if !FileManager.default.fileExists(atPath: dir.path), FileManager.default.fileExists(atPath: legacy.path) {
            try? FileManager.default.moveItem(at: legacy, to: dir)
        }
        return dir
    }

    static var fileURL: URL { directory.appendingPathComponent("notes.sqlite") }

    private var db: OpaquePointer?
    private static let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)

    init() {
        try? FileManager.default.createDirectory(at: Self.directory, withIntermediateDirectories: true)
        guard sqlite3_open(Self.fileURL.path, &db) == SQLITE_OK else { db = nil; return }
        exec("PRAGMA journal_mode=WAL;")
        exec("""
        CREATE TABLE IF NOT EXISTS notes (
            id TEXT PRIMARY KEY,
            title TEXT NOT NULL DEFAULT '',
            body BLOB,
            locked INTEGER NOT NULL DEFAULT 0,
            salt BLOB,
            pinned INTEGER NOT NULL DEFAULT 0,
            created REAL NOT NULL,
            updated REAL NOT NULL
        );
        """)
    }

    deinit { sqlite3_close(db) }

    func all() -> [Row] {
        var rows: [Row] = []
        query("SELECT id, title, body, locked, salt, pinned, created, updated FROM notes ORDER BY pinned DESC, updated DESC") { stmt in
            rows.append(Row(
                id: Self.text(stmt, 0),
                title: Self.text(stmt, 1),
                body: Self.blob(stmt, 2) ?? Data(),
                locked: sqlite3_column_int(stmt, 3) != 0,
                salt: Self.blob(stmt, 4),
                pinned: sqlite3_column_int(stmt, 5) != 0,
                created: Date(timeIntervalSince1970: sqlite3_column_double(stmt, 6)),
                updated: Date(timeIntervalSince1970: sqlite3_column_double(stmt, 7))
            ))
        }
        return rows
    }

    func upsert(_ row: Row) {
        let sql = """
        INSERT INTO notes (id, title, body, locked, salt, pinned, created, updated) VALUES (?,?,?,?,?,?,?,?)
        ON CONFLICT(id) DO UPDATE SET title=excluded.title, body=excluded.body, locked=excluded.locked,
            salt=excluded.salt, pinned=excluded.pinned, updated=excluded.updated;
        """
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return }
        defer { sqlite3_finalize(stmt) }
        sqlite3_bind_text(stmt, 1, row.id, -1, Self.transient)
        sqlite3_bind_text(stmt, 2, row.title, -1, Self.transient)
        Self.bind(stmt, 3, row.body)
        sqlite3_bind_int(stmt, 4, row.locked ? 1 : 0)
        if let salt = row.salt { Self.bind(stmt, 5, salt) } else { sqlite3_bind_null(stmt, 5) }
        sqlite3_bind_int(stmt, 6, row.pinned ? 1 : 0)
        sqlite3_bind_double(stmt, 7, row.created.timeIntervalSince1970)
        sqlite3_bind_double(stmt, 8, row.updated.timeIntervalSince1970)
        sqlite3_step(stmt)
    }

    func delete(_ id: String) {
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(db, "DELETE FROM notes WHERE id = ?", -1, &stmt, nil) == SQLITE_OK else { return }
        defer { sqlite3_finalize(stmt) }
        sqlite3_bind_text(stmt, 1, id, -1, Self.transient)
        sqlite3_step(stmt)
    }

    private func exec(_ sql: String) {
        sqlite3_exec(db, sql, nil, nil, nil)
    }

    private func query(_ sql: String, row: (OpaquePointer) -> Void) {
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK, let stmt else { return }
        defer { sqlite3_finalize(stmt) }
        while sqlite3_step(stmt) == SQLITE_ROW { row(stmt) }
    }

    private static func bind(_ stmt: OpaquePointer?, _ index: Int32, _ data: Data) {
        _ = data.withUnsafeBytes { sqlite3_bind_blob(stmt, index, $0.baseAddress, Int32(data.count), transient) }
    }

    private static func text(_ stmt: OpaquePointer, _ i: Int32) -> String {
        sqlite3_column_text(stmt, i).map { String(cString: $0) } ?? ""
    }

    private static func blob(_ stmt: OpaquePointer, _ i: Int32) -> Data? {
        guard let ptr = sqlite3_column_blob(stmt, i) else { return nil }
        return Data(bytes: ptr, count: Int(sqlite3_column_bytes(stmt, i)))
    }
}
