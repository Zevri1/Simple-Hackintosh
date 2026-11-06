import Foundation

public struct InstallerOption: Identifiable, Hashable, Sendable {
    public let title: String
    public let version: String
    public var id: String { version }

    public init(title: String, version: String) {
        self.title = title
        self.version = version
    }
}

public enum InstallerCatalogParser {
    /// Parses the output of Apple's softwareupdate --list-full-installers command.
    public static func parse(_ output: String) -> [InstallerOption] {
        let pattern = #"(?m)^\s*\*?\s*Title:\s*(.+?),\s*Version:\s*([0-9]+(?:\.[0-9]+)*) "#
            .trimmingCharacters(in: .whitespaces)
        guard let expression = try? NSRegularExpression(pattern: pattern) else { return [] }
        let source = output as NSString
        let matches = expression.matches(in: output, range: NSRange(location: 0, length: source.length))
        var seen = Set<String>()
        return matches.compactMap { match in
            guard match.numberOfRanges == 3 else { return nil }
            let title = source.substring(with: match.range(at: 1)).trimmingCharacters(in: .whitespacesAndNewlines)
            let version = source.substring(with: match.range(at: 2)).trimmingCharacters(in: .whitespacesAndNewlines)
            guard !title.isEmpty, !version.isEmpty, seen.insert(version).inserted else { return nil }
            return InstallerOption(title: title, version: version)
        }
    }
}

public enum CommandEscaping {
    public static func shellArgument(_ value: String) -> String {
        "'" + value.replacingOccurrences(of: "'", with: "'\\''") + "'"
    }

    public static func appleScriptString(_ value: String) -> String {
        let escaped = value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")
            .replacingOccurrences(of: "\r", with: "\\r")
            .replacingOccurrences(of: "\n", with: "\\n")
        return "\"\(escaped)\""
    }
}

public enum SHA256Digest {
    public static func matches(githubDigest: String?, actualHex: String) -> Bool {
        guard let githubDigest, githubDigest.lowercased().hasPrefix("sha256:") else { return false }
        let expected = String(githubDigest.dropFirst("sha256:".count)).lowercased()
        let actual = actualHex.lowercased()
        guard expected.count == 64, actual.count == 64 else { return false }
        return expected == actual && expected.allSatisfy(\.isHexDigit) && actual.allSatisfy(\.isHexDigit)
    }
}
