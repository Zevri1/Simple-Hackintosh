import Foundation

public struct InstallerEntry: Identifiable, Equatable, Sendable {
    public let productID: String?
    public let title: String
    public let version: String
    public let size: String?
    public let build: String?
    public let downloadURL: URL?
    public let expectedBytes: Int64?

    public var id: String { productID ?? version }

    public init(
        title: String,
        version: String,
        size: String? = nil,
        build: String? = nil,
        productID: String? = nil,
        downloadURL: URL? = nil,
        expectedBytes: Int64? = nil
    ) {
        self.productID = productID
        self.title = title
        self.version = version
        self.size = size
        self.build = build
        self.downloadURL = downloadURL
        self.expectedBytes = expectedBytes
    }
}

public enum InstallerCatalogParser {
    /// Parses softwareupdate output for compatibility with the existing CLI.
    public static func parse(_ output: String) -> [InstallerEntry] {
        var entries: [InstallerEntry] = []

        for rawLine in output.split(whereSeparator: \.isNewline) {
            let fields = String(rawLine).split(separator: ",", omittingEmptySubsequences: false)
            var title: String?
            var version: String?
            var size: String?
            var build: String?

            for rawField in fields {
                let field = String(rawField).trimmingCharacters(in: .whitespacesAndNewlines)
                if let value = value(after: "Title:", in: field) { title = value }
                if let value = value(after: "Version:", in: field) { version = value }
                if let value = value(after: "Size:", in: field) { size = value }
                if let value = value(after: "Build:", in: field) { build = value }
            }

            if let title, let version, !version.isEmpty {
                entries.append(InstallerEntry(title: title, version: version, size: size, build: build))
            }
        }

        var unique: [String: InstallerEntry] = [:]
        for entry in entries { unique[entry.version] = entry }
        return unique.values.sorted { compareVersions($0.version, $1.version) }
    }

    private static func value(after label: String, in field: String) -> String? {
        guard let range = field.range(of: label, options: [.caseInsensitive]) else { return nil }
        let rawValue = String(field[range.upperBound...])
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let cleaned = rawValue.trimmingCharacters(in: CharacterSet(charactersIn: "*"))
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return cleaned.isEmpty ? nil : cleaned
    }

    private static func compareVersions(_ lhs: String, _ rhs: String) -> Bool {
        let left = lhs.split(separator: ".").compactMap { Int($0) }
        let right = rhs.split(separator: ".").compactMap { Int($0) }
        for index in 0..<max(left.count, right.count) {
            let a = index < left.count ? left[index] : 0
            let b = index < right.count ? right[index] : 0
            if a != b { return a > b }
        }
        return lhs > rhs
    }
}
