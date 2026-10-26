import Foundation

enum ScriptValue: Sendable {
    case string(String)
    case number(Double)
    case data(Data)
    case none

    var string: String? { if case .string(let s) = self { s } else { nil } }
    var number: Double? {
        switch self {
        case .number(let n): n
        case .string(let s): Double(s)
        default: nil
        }
    }
    var data: Data? { if case .data(let d) = self { d } else { nil } }
}

enum AppleScriptRunner {
    private static let queue = DispatchQueue(label: "island.applescript", qos: .userInitiated)

    static func run(_ source: String, completion: (@MainActor @Sendable ([ScriptValue]) -> Void)? = nil) {
        runDetailed(source) { values, _ in completion?(values) }
    }

    private nonisolated(unsafe) static var compiled: [String: NSAppleScript] = [:]

    private static func script(for source: String) -> NSAppleScript? {
        if let s = compiled[source] { return s }
        guard let s = NSAppleScript(source: source) else { return nil }
        var err: NSDictionary?
        guard s.compileAndReturnError(&err) else { return s }
        if compiled.count > 32 { compiled.removeAll() }
        compiled[source] = s
        return s
    }

    static func runDetailed(_ source: String, completion: @escaping @MainActor @Sendable ([ScriptValue], String?) -> Void) {
        queue.async {
            var error: NSDictionary?
            let result = script(for: source)?.executeAndReturnError(&error)
            let values: [ScriptValue] = (error == nil && result != nil) ? flatten(result!) : []
            let message = error?[NSAppleScript.errorMessage] as? String
            DispatchQueue.main.async {
                MainActor.assumeIsolated { completion(values, message) }
            }
        }
    }

    static func escape(_ s: String) -> String {
        s.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\"", with: "\\\"")
    }

    private static func flatten(_ d: NSAppleEventDescriptor) -> [ScriptValue] {
        if d.descriptorType == fourCC("list") {
            guard d.numberOfItems > 0 else { return [] }
            return (1...d.numberOfItems).map { d.atIndex($0).map(value) ?? .none }
        }
        return [value(d)]
    }

    private static func value(_ d: NSAppleEventDescriptor) -> ScriptValue {
        switch d.descriptorType {
        case fourCC("utxt"), fourCC("utf8"), fourCC("TEXT"):
            return .string(d.stringValue ?? "")
        case fourCC("long"), fourCC("shor"), fourCC("comp"), fourCC("doub"), fourCC("sing"), fourCC("magn"):
            return .number(d.doubleValue)
        case fourCC("null"), fourCC("msng"):
            return .none
        default:
            return .data(d.data)
        }
    }

    static func fourCC(_ s: String) -> DescType {
        s.utf8.reduce(0) { ($0 << 8) | DescType($1) }
    }
}
