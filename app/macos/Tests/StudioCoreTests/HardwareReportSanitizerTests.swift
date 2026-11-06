import XCTest
@testable import StudioCore

final class HardwareReportSanitizerTests: XCTestCase {
    func testRedactsIdentifiersButKeepsHardwareFields() {
        let input: [String: Any] = [
            "machine_model": "MacBookPro16,1",
            "serial_number": "C02TEST123",
            "platform_UUID": "12345678-1234-1234-1234-123456789ABC",
            "processor_name": "Intel Core i7",
            "network": ["MAC Address": "AA:BB:CC:DD:EE:FF"]
        ]
        let result = HardwareReportSanitizer.redact(input) as? [String: Any]
        XCTAssertEqual(result?["machine_model"] as? String, "MacBookPro16,1")
        XCTAssertEqual(result?["processor_name"] as? String, "Intel Core i7")
        XCTAssertEqual(result?["serial_number"] as? String, "[REDACTED]")
        XCTAssertEqual(result?["platform_UUID"] as? String, "[REDACTED]")
        let network = result?["network"] as? [String: Any]
        XCTAssertEqual(network?["MAC Address"] as? String, "[REDACTED]")
    }

    func testRedactsMACAddressesInsideText() {
        let result = HardwareReportSanitizer.redact(
            "Interface address AA:BB:CC:DD:EE:FF detected"
        ) as? String
        XCTAssertEqual(result, "Interface address [REDACTED] detected")
    }
}
