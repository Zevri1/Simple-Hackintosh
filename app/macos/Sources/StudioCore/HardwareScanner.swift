import Foundation

public struct HardwareSnapshot: Sendable {
    public let createdAt: Date
    public let redactedJSON: String

    public init(createdAt: Date = Date(), redactedJSON: String) {
        self.createdAt = createdAt
        self.redactedJSON = redactedJSON
    }
}

public enum HardwareReportSanitizer {
    private static let sensitiveKeyFragments = [
        "serial", "uuid", "macaddress", "ethernetaddress", "bluetoothaddress"
    ]

    /// Removes common identifiers before a report is displayed or exported.
    public static func redact(_ value: Any) -> Any {
        if let dictionary = value as? [String: Any] {
            var safe: [String: Any] = [:]
            for (key, child) in dictionary {
                let normalized = key.lowercased().filter { $0.isLetter || $0.isNumber }
                if sensitiveKeyFragments.contains(where: { normalized.contains($0) }) {
                    safe[key] = "[REDACTED]"
                } else {
                    safe[key] = redact(child)
                }
            }
            return safe
        }
        if let array = value as? [Any] { return array.map { redact($0) } }
        if let string = value as? String { return redactMACAddresses(in: string) }
        return value
    }

    private static func redactMACAddresses(in value: String) -> String {
        let pattern = #"(?i)\b(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b"#
        guard let expression = try? NSRegularExpression(pattern: pattern) else { return value }
        let range = NSRange(value.startIndex..<value.endIndex, in: value)
        return expression.stringByReplacingMatches(
            in: value, range: range, withTemplate: "[REDACTED]"
        )
    }
}

public enum SystemHardwareScanner {
    public static func scan() throws -> HardwareSnapshot {
        let result = try CommandRunner.run(
            "/usr/sbin/system_profiler",
            arguments: [
                "SPHardwareDataType", "SPDisplaysDataType",
                "SPStorageDataType", "SPNetworkDataType", "-json"
            ]
        )
        guard let data = result.text.data(using: .utf8) else {
            throw NSError(
                domain: "SimpleHackintoshStudio.Hardware",
                code: 1,
                userInfo: [NSLocalizedDescriptionKey: "system_profiler returned invalid UTF-8."]
            )
        }
        let decoded = try JSONSerialization.jsonObject(with: data)
        let safeObject = HardwareReportSanitizer.redact(decoded)
        let safeData = try JSONSerialization.data(
            withJSONObject: safeObject, options: [.prettyPrinted, .sortedKeys]
        )
        guard let report = String(data: safeData, encoding: .utf8) else {
            throw NSError(
                domain: "SimpleHackintoshStudio.Hardware",
                code: 2,
                userInfo: [NSLocalizedDescriptionKey: "Could not encode the redacted hardware report."]
            )
        }
        return HardwareSnapshot(redactedJSON: report)
    }
}
