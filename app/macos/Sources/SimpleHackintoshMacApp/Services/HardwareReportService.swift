#if os(macOS)
import Foundation

final class HardwareReportService {
    func createReport() async throws -> String {
        let system = try await ProcessRunner.runChecked(
            "/usr/sbin/system_profiler",
            arguments: [
                "SPHardwareDataType",
                "SPDisplaysDataType",
                "SPNetworkDataType",
                "SPStorageDataType",
                "SPAudioDataType"
            ]
        )
        let version = try await ProcessRunner.runChecked("/usr/bin/sw_vers", arguments: [])
        let redacted = system.replacingOccurrences(
            of: #"(?im)^\s*(Serial Number \(system\)|Serial Number|Hardware UUID|Provisioning UDID):.*$"#,
            with: "[redacted]",
            options: .regularExpression
        )
        return "SIMPLE HACKINTOSH — MAC HARDWARE REPORT\nGenerated: \(ISO8601DateFormatter().string(from: Date()))\n\nmacOS version\n\(version)\n\nHardware inventory\n\(redacted)\n"
    }
}
#endif
